# RPG 工坊 · 独立版

> 最快的用法：不部署后端，直接双击打开 `index.html`，点右上角"⚙ 设置"，文本模型和图片模型都选"浏览器直连"，填接口地址、key、模型名即可。key 只存在你自己浏览器里。要给别人用、又不想让每个人都填 key，再按下面部署 server.js。

不依赖 claude.ai 的版本：一个 `index.html` 加一个几十行的 `server.js`。页面里所有 AI 生成都发给 `server.js`，由它转发到你配置的模型接口（任何 OpenAI 兼容接口：DeepSeek、通义、混元、Kimi、Claude，或公司内部网关）。项目数据存在使用者自己的浏览器里，导出内容包走浏览器下载。

```
standalone/
├─ index.html      页面（和 claude.ai 上那版是同一份代码，自动识别运行环境）
├─ server.js       后端：托管页面 + /api/generate 转发模型 + /api/health
├─ .env.example    配置样例，复制成 .env 填 key
├─ package.json
└─ README.md
```

## 本地跑起来（5 分钟）

1. 装 Node.js 18 或更新（https://nodejs.org）。
2. 复制 `.env.example` 为 `.env`，填 `LLM_API_KEY`；要换模型就改 `LLM_BASE_URL` 和 `LLM_MODEL`。
3. 在这个目录运行：
   ```
   npm start
   ```
4. 打开 http://localhost:8787 。页面右上角"⚙ 设置"里点"测试连接"，显示模型名就通了。

不需要 `npm install`，没有任何依赖。

## 部署给别人用

### 方案 A：整体部署（最简单）

`server.js` 同时托管页面和接口，所以只要把这个目录放到任何能跑 Node 的地方：

- 云函数 / 轻量服务器：腾讯云 Lighthouse、阿里云 ECS、Render、Railway、Fly.io 等。启动命令 `npm start`，环境变量按 `.env.example` 配置（这些平台一般在控制台填环境变量，不用上传 `.env`）。
- 公司内网：一台能访问模型网关的机器，`node server.js` 挂在后台（pm2 / systemd 都行）。

然后把地址发给使用者即可，页面不用改任何设置。

### 方案 B：前端放 GitHub Pages，后端单独部署

1. 把 `index.html` 推到一个 GitHub 仓库，Settings → Pages 开启，得到 `https://<你>.github.io/<仓库>/`。
2. `server.js` 按方案 A 部署到任意地方，`.env` 里把 `ALLOW_ORIGIN` 改成前端域名（如 `https://<你>.github.io`）。
3. 使用者第一次打开页面，点右上角"⚙ 设置"，填后端地址和口令，保存。

前端是纯静态文件，GitHub Pages 免费；后端必须单独跑，因为 API key 不能放进网页。

## 安全

- 在 `.env` 里设 `APP_TOKEN`（随便一串），页面"设置"里填同样的值。不设的话，任何拿到后端地址的人都能用你的 key 调模型。
- key 只存在后端的 `.env` 或平台的环境变量里，永远不要写进 `index.html`。
- `.env` 不要提交到 git（加进 `.gitignore`）。

## 数据在哪

- 项目和每个环节的产出：使用者浏览器的 localStorage；上传的立绘：浏览器的 IndexedDB。换电脑或清浏览器数据会丢，所以做完请用"导出内容包"或"导出原始 JSON"存一份。原始 JSON 可以在新环境里用"手动粘贴 JSON"逐环节贴回去。
- 独立版导出的文件名会转成英文（如 `rpg-pack-2026-10-28.zip`），因为部分浏览器会丢掉中文文件名。
- 需要多人共用一份项目时，再给 `server.js` 加一个存取项目的接口即可（页面已经按"有后端 db 就用，没有就用本地"写好，加接口的改动很小）。

## 图片模型（环节 3 直接生成立绘）

页面"设置"里选"浏览器直连"填 key，或在 `.env` 里配 `IMG_BASE_URL / IMG_API_KEY / IMG_MODEL` 走后端。支持 OpenAI 兼容的 `images/generations`：硅基流动（Kwai-Kolors/Kolors、FLUX）、OpenAI（gpt-image-1、dall-e-3）、智谱 CogView 等。Midjourney、即梦、通义万相不是这个格式，用它们就拿卡片上的 prompt 手动生成再上传。

## 接口说明（给想接公司网关的人）

- `GET /api/health` → `{ok, model, base, hasKey, imgModel}`
- `POST /api/image` body `{prompt, size}` → `{b64, mime}` 或 `{url}`
- `POST /api/generate` body `{prompt, tier}`，tier 为 `quick | default | complex`，对应 `.env` 里三个模型名；响应是流式纯文本（模型的增量输出直接写回），页面收完后自己解析 JSON。
- 转发用的是标准的 `POST {LLM_BASE_URL}/chat/completions`，`stream: true`。只要网关兼容这个格式就能直接用；不兼容的话只改 `server.js` 里 `generate()` 一处。

## 模型选择建议

- 对白、任务这些环节产出的 JSON 很长，选上下文和输出都 ≥ 8k 的模型，`LLM_MAX_TOKENS` 不要低于 8192。
- 推理模型（如 deepseek-reasoner）可以配在 `LLM_MODEL_COMPLEX`，页面"模型"下拉切到"深思"时使用；日常生成用普通对话模型更快更便宜。
