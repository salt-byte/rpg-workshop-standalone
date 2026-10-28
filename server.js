// RPG 工坊 · 独立版后端
// 作用：1) 托管 index.html；2) 把页面的生成请求转发给任意 OpenAI 兼容的模型接口（流式返回）。
// 只用 Node 18+ 自带模块，不需要 npm install。配置写在 .env（见 .env.example）。

const http = require('http');
const fs = require('fs');
const path = require('path');

// ---- 读取 .env（不依赖 dotenv）----
(function loadEnv() {
  const f = path.join(__dirname, '.env');
  if (!fs.existsSync(f)) return;
  for (const line of fs.readFileSync(f, 'utf8').split('\n')) {
    const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$/);
    if (m && !process.env[m[1]]) process.env[m[1]] = m[2].replace(/^["']|["']$/g, '');
  }
})();

const CFG = {
  base: (process.env.LLM_BASE_URL || 'https://api.deepseek.com/v1').replace(/\/$/, ''),
  key: process.env.LLM_API_KEY || '',
  model: {
    default: process.env.LLM_MODEL || 'deepseek-chat',
    quick: process.env.LLM_MODEL_QUICK || process.env.LLM_MODEL || 'deepseek-chat',
    complex: process.env.LLM_MODEL_COMPLEX || process.env.LLM_MODEL || 'deepseek-chat',
  },
  maxTokens: Number(process.env.LLM_MAX_TOKENS || 8192),
  temperature: Number(process.env.LLM_TEMPERATURE || 0.7),
  img: {
    base: (process.env.IMG_BASE_URL || '').replace(/\/$/, ''),
    key: process.env.IMG_API_KEY || '',
    model: process.env.IMG_MODEL || '',
    size: process.env.IMG_SIZE || '1024x1024',
  },
  token: process.env.APP_TOKEN || '',
  allowOrigin: process.env.ALLOW_ORIGIN || '*',
  port: Number(process.env.PORT || 8787),
};

const INDEX = path.join(__dirname, 'index.html');

function cors(res) {
  res.setHeader('Access-Control-Allow-Origin', CFG.allowOrigin);
  res.setHeader('Access-Control-Allow-Headers', 'content-type, x-app-token');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
}
function json(res, code, obj) {
  res.writeHead(code, { 'content-type': 'application/json; charset=utf-8' });
  res.end(JSON.stringify(obj));
}
function authed(req) {
  if (!CFG.token) return true;
  return req.headers['x-app-token'] === CFG.token;
}
function readBody(req) {
  return new Promise((resolve, reject) => {
    let s = '';
    req.on('data', c => { s += c; if (s.length > 2e6) { reject(new Error('body too large')); req.destroy(); } });
    req.on('end', () => resolve(s));
    req.on('error', reject);
  });
}

// ---- 转发到模型：OpenAI 兼容的 /chat/completions，stream=true，把 delta 文本原样写回给页面 ----
async function generate(req, res) {
  if (!authed(req)) return json(res, 401, { error: '口令不对（x-app-token）' });
  if (!CFG.key) return json(res, 500, { error: '.env 里没有 LLM_API_KEY' });
  let body;
  try { body = JSON.parse(await readBody(req) || '{}'); } catch (e) { return json(res, 400, { error: '请求不是 JSON' }); }
  const prompt = String(body.prompt || '');
  if (!prompt.trim()) return json(res, 400, { error: 'prompt 为空' });
  const model = CFG.model[body.tier] || CFG.model.default;

  const ctl = new AbortController();
  req.on('close', () => ctl.abort());
  let upstream;
  try {
    upstream = await fetch(CFG.base + '/chat/completions', {
      method: 'POST',
      headers: { 'content-type': 'application/json', authorization: 'Bearer ' + CFG.key },
      body: JSON.stringify({ model, stream: true, temperature: CFG.temperature, max_tokens: CFG.maxTokens, messages: [{ role: 'user', content: prompt }] }),
      signal: ctl.signal,
    });
  } catch (e) {
    return json(res, 502, { error: '连不上模型接口 ' + CFG.base + '：' + e.message });
  }
  if (!upstream.ok) {
    const t = await upstream.text().catch(() => '');
    return json(res, 502, { error: `模型接口返回 ${upstream.status}：${t.slice(0, 300)}` });
  }
  res.writeHead(200, { 'content-type': 'text/plain; charset=utf-8', 'cache-control': 'no-cache', 'x-accel-buffering': 'no' });
  const reader = upstream.body.getReader();
  const dec = new TextDecoder();
  let buf = '';
  const t0 = Date.now(); let chars = 0;
  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      buf += dec.decode(value, { stream: true });
      let i;
      while ((i = buf.indexOf('\n')) >= 0) {
        const line = buf.slice(0, i).trim(); buf = buf.slice(i + 1);
        if (!line.startsWith('data:')) continue;
        const data = line.slice(5).trim();
        if (data === '[DONE]') continue;
        try {
          const j = JSON.parse(data);
          const d = j.choices && j.choices[0] && j.choices[0].delta;
          const text = d && typeof d.content === 'string' ? d.content : ''; // reasoning_content（思考过程）不写回
          if (text) { res.write(text); chars += text.length; }
        } catch (e) { /* 半行，等下一块 */ }
      }
    }
  } catch (e) {
    // 客户端取消或上游断开
  }
  res.end();
  console.log(`[generate] ${model} tier=${body.tier || 'default'} prompt=${prompt.length}字 回复=${chars}字 ${((Date.now() - t0) / 1000).toFixed(1)}s`);
}

// ---- 生图：OpenAI 兼容的 /images/generations，把图片以 base64 返回给页面（服务端取图，不受浏览器跨域限制）----
async function image(req, res) {
  if (!authed(req)) return json(res, 401, { error: '口令不对（x-app-token）' });
  if (!CFG.img.base || !CFG.img.key || !CFG.img.model) return json(res, 500, { error: '.env 里没配 IMG_BASE_URL / IMG_API_KEY / IMG_MODEL' });
  let body;
  try { body = JSON.parse(await readBody(req) || '{}'); } catch (e) { return json(res, 400, { error: '请求不是 JSON' }); }
  const prompt = String(body.prompt || '').trim();
  if (!prompt) return json(res, 400, { error: 'prompt 为空' });
  const call = async (withFmt) => {
    const payload = { model: CFG.img.model, prompt, n: 1, size: body.size || CFG.img.size };
    if (withFmt) payload.response_format = 'b64_json';
    return fetch(CFG.img.base + '/images/generations', { method: 'POST', headers: { 'content-type': 'application/json', authorization: 'Bearer ' + CFG.img.key }, body: JSON.stringify(payload) });
  };
  let r;
  try {
    r = await call(true);
    if (r.status === 400) { const t = await r.clone().text(); if (/response_format/i.test(t)) r = await call(false); }
  } catch (e) { return json(res, 502, { error: '连不上图片接口 ' + CFG.img.base + '：' + e.message }); }
  if (!r.ok) { const t = await r.text().catch(() => ''); return json(res, 502, { error: `图片接口返回 ${r.status}：${t.slice(0, 300)}` }); }
  const j = await r.json().catch(() => ({}));
  const d = (j.data && j.data[0]) || (j.images && j.images[0]) || {};
  if (d.b64_json) return json(res, 200, { b64: d.b64_json, mime: 'image/png' });
  if (d.url) {
    try {
      const rr = await fetch(d.url);
      if (rr.ok) { const buf = Buffer.from(await rr.arrayBuffer()); return json(res, 200, { b64: buf.toString('base64'), mime: rr.headers.get('content-type') || 'image/png' }); }
    } catch (e) { /* 取不到就把链接给页面 */ }
    return json(res, 200, { url: d.url });
  }
  json(res, 502, { error: '图片接口没有返回图片：' + JSON.stringify(j).slice(0, 200) });
}

const server = http.createServer(async (req, res) => {
  cors(res);
  const url = new URL(req.url, 'http://x');
  if (req.method === 'OPTIONS') { res.writeHead(204); return res.end(); }
  if (url.pathname === '/api/health') {
    if (!authed(req)) return json(res, 401, { error: '口令不对' });
    return json(res, 200, { ok: true, model: CFG.model.default, base: CFG.base, hasKey: !!CFG.key, imgModel: (CFG.img.key && CFG.img.model) ? CFG.img.model : '' });
  }
  if (url.pathname === '/api/generate' && req.method === 'POST') return generate(req, res);
  if (url.pathname === '/api/image' && req.method === 'POST') return image(req, res);
  if (req.method === 'GET' && (url.pathname === '/' || url.pathname === '/index.html')) {
    if (!fs.existsSync(INDEX)) return json(res, 404, { error: '缺少 index.html' });
    res.writeHead(200, { 'content-type': 'text/html; charset=utf-8' });
    return fs.createReadStream(INDEX).pipe(res);
  }
  json(res, 404, { error: 'not found' });
});

server.listen(CFG.port, () => {
  console.log(`RPG 工坊 独立版：http://localhost:${CFG.port}`);
  console.log(`文本模型：${CFG.base}  ${CFG.model.default}  ${CFG.key ? '' : '（警告：LLM_API_KEY 为空）'}`);
  console.log(CFG.img.model ? `图片模型：${CFG.img.base}  ${CFG.img.model}` : '图片模型：未配置（页面里可以用浏览器直连，或手动上传立绘）');
  if (!CFG.token) console.log('提示：没设 APP_TOKEN，任何人拿到地址都能用你的 key 调模型；公网部署请在 .env 里设一个。');
});
