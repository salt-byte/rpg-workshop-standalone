using System.Collections.Generic;
using UnityEngine;

namespace RPGWorkshop
{
    // 代码画的 12×12 像素瓦片和小人，没有任何图片素材也能跑。
    // 和 RPG 工坊网页里的地图预览是同一套画法。内容包 art/tiles/ 里放了同名 png 就优先用 png。
    public static class PixelArt
    {
        public const int N = 12;
        // 一格在世界里是 U×U。默认寻路体型半径 0.5，格子做大一点，一格宽的走廊才不会被堵死
        public const float U = 4f;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        public static ContentPack overrides;

        class Pal { public string g, g2, g3, p, p2, p3; }
        static readonly Dictionary<string, Pal> PAL = new Dictionary<string, Pal>
        {
            ["town"] = new Pal { g = "#6dab4f", g2 = "#5e9a43", g3 = "#8cc463", p = "#c9a46a", p2 = "#b38d55", p3 = "#dcbc85" },
            ["field"] = new Pal { g = "#5f9e46", g2 = "#4f8a3a", g3 = "#7fb85a", p = "#b8955e", p2 = "#9f7d4a", p3 = "#cfae76" },
            ["dungeon"] = new Pal { g = "#4b4a57", g2 = "#41404c", g3 = "#58576a", p = "#6b6878", p2 = "#5c596a", p3 = "#7b788a" },
        };

        public static bool Solid(char t) => t == '#' || t == 'T' || t == '~' || t == 'H';

        static float Hash(int x, int y, int s = 0)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + s * 982451653;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296f;
            }
        }

        static Color C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        // 画布：坐标原点在左上角，和网页版一致
        class Canvas
        {
            public readonly Color[] px = new Color[N * N];
            public void R(int x, int y, int w, int h, string col) => R(x, y, w, h, C(col));
            public void R(int x, int y, int w, int h, Color c)
            {
                for (int j = y; j < y + h; j++)
                    for (int i = x; i < x + w; i++)
                        if (i >= 0 && j >= 0 && i < N && j < N)
                        {
                            int k = (N - 1 - j) * N + i;
                            px[k] = c.a >= 1f ? c : Color.Lerp(px[k], new Color(c.r, c.g, c.b, 1f), c.a);
                        }
            }
        }

        static Sprite Make(string key, Canvas cv)
        {
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = key };
            t.SetPixels(cv.px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N / U);
        }

        static Sprite FromOverride(string name)
        {
            var tex = overrides?.TileOverride(name);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), Mathf.Max(tex.width, tex.height) / U);
        }

        // variant 不为空时先找带后缀的变体（比如 tree_1.png），没有再找不带后缀的
        static Sprite Cached(string key, string overrideName, System.Action<Canvas> draw, string variant = null)
        {
            if (cache.TryGetValue(key, out var s)) return s;
            if (variant != null) s = FromOverride(overrideName + variant);
            if (s == null) s = FromOverride(overrideName);
            if (s == null)
            {
                var cv = new Canvas();
                draw(cv);
                s = Make(key, cv);
            }
            cache[key] = s;
            return s;
        }

        public static void ClearCache() => cache.Clear();

        // 地面层：草地 / 路 / 地板，每格都有
        public static Sprite Ground(MapGrid g, int x, int y)
        {
            char t = g.At(x, y);
            bool path = t == ',' || (t == '+' && g.kind != "town");
            var K = PAL.TryGetValue(g.kind, out var k) ? k : PAL["field"];
            if (g.kind == "dungeon")
            {
                int v = (int)(Hash(x, y) * 4);
                return path
                    ? Cached("dfloor" + v, "dungeon_floor", c => { c.R(0, 0, 12, 12, K.p); c.R(0, 0, 12, 1, K.p3); c.R(0, 6, 12, 1, K.p2); c.R(5, 0, 1, 6, K.p2); c.R(10, 6, 1, 6, K.p2); if (v == 1) c.R(2, 9, 2, 1, K.p3); }, "_" + v)
                    : Cached("dground" + v, "dungeon_floor", c => { c.R(0, 0, 12, 12, K.p); c.R(0, 0, 12, 1, K.p2); c.R(0, 0, 1, 12, K.p2); if (v == 2) c.R(4, 5, 2, 1, K.p3); }, "_" + v);
            }
            if (path)
            {
                bool Edge(int a, int b) { char n = g.At(a, b); return n != ',' && n != '+'; }
                bool u = Edge(x, y - 1), d = Edge(x, y + 1), l = Edge(x - 1, y), r = Edge(x + 1, y);
                int v = (int)(Hash(x, y) * 3);
                string key = $"{g.kind}path{u}{d}{l}{r}{v}";
                return Cached(key, g.kind + "_path", c =>
                {
                    c.R(0, 0, 12, 12, K.p);
                    c.R(2 + v * 3, 3 + v * 2, 2, 1, K.p2);
                    if (v == 2) c.R(7, 8, 1, 1, K.p3);
                    if (u) c.R(0, 0, 12, 1, K.g2);
                    if (d) c.R(0, 11, 12, 1, K.g2);
                    if (l) c.R(0, 0, 1, 12, K.g2);
                    if (r) c.R(11, 0, 1, 12, K.g2);
                }, "_" + v);
            }
            int gv = (int)(Hash(x, y) * 10);
            return Cached(g.kind + "grass" + gv, g.kind + "_grass", c =>
            {
                c.R(0, 0, 12, 12, K.g);
                for (int i = 0; i < 3; i++) c.R((int)(Hash(gv, i, 3) * 11), (int)(Hash(i, gv, 9) * 10), 1, 2, K.g2);
                if (gv == 9) { c.R(4, 5, 1, 1, "#f2e36b"); c.R(8, 3, 1, 1, "#f28fb0"); }
                else if (gv == 0) c.R(3, 6, 2, 1, K.g3);
            }, gv == 9 ? "_2" : gv <= 1 ? "_1" : null);
        }

        // 障碍层：墙、树、水、房子、门。返回 null 表示这一格没有障碍
        public static Sprite Obstacle(MapGrid g, int x, int y)
        {
            char t = g.At(x, y);
            var K = PAL.TryGetValue(g.kind, out var k) ? k : PAL["field"];
            switch (t)
            {
                case 'T':
                    if (g.kind == "dungeon") return Cached("dvoid", "wall_top", c => c.R(0, 0, 12, 12, "#26252e"));
                {
                    int tv = (int)(Hash(x, y, 5) * 3);
                    return Cached(g.kind + "tree" + tv, "tree", c =>
                    {
                        c.R(5, 8, 2, 4, "#6b4a2b"); c.R(2, 1, 8, 8, "#2e6b34"); c.R(1, 3, 10, 5, "#2e6b34");
                        c.R(3, 2, 4, 3, "#3f8a43"); c.R(3, 2, 2, 1, "#5aa85b"); c.R(2, 7, 8, 1, "#245729");
                    }, "_" + tv);
                }
                case '~':
                {
                    bool N_(int a, int b) => g.At(a, b) != '~';
                    bool u = N_(x, y - 1), d = N_(x, y + 1), l = N_(x - 1, y), r = N_(x + 1, y);
                    return Cached($"water{u}{d}{l}{r}", "water", c =>
                    {
                        c.R(0, 0, 12, 12, "#3a78c2"); c.R(2, 4, 3, 1, "#6aa5e6"); c.R(6, 8, 2, 1, "#5590d6");
                        if (u) c.R(0, 0, 12, 2, "#9fd0f5");
                        if (d) c.R(0, 11, 12, 1, "#2a5c9a");
                        if (l) c.R(0, 0, 1, 12, "#9fd0f5");
                        if (r) c.R(11, 0, 1, 12, "#9fd0f5");
                    });
                }
                case '#':
                {
                    char below = g.At(x, y + 1);
                    bool face = y + 1 < g.height && below != '#';
                    return face
                        ? Cached("wallface", "wall_face", c =>
                        {
                            c.R(0, 0, 12, 12, "#5a5868");
                            for (int j = 0; j < 12; j += 4) { c.R(0, j, 12, 1, "#3c3a48"); c.R((j / 4) % 2 == 1 ? 0 : 6, j, 1, 4, "#3c3a48"); }
                            c.R(0, 0, 12, 2, "#77758a");
                        })
                        : Cached("walltop", "wall_top", c => { c.R(0, 0, 12, 12, "#26252e"); c.R(3, 4, 2, 2, "#2e2d38"); });
                }
                case 'H':
                {
                    char below = g.At(x, y + 1);
                    bool face = below != 'H' && below != '+';
                    if (face)
                    {
                        bool win = Hash(x, y) < .6f;
                        return Cached("housewall" + win, win ? "house_wall_window" : "house_wall", c =>
                        {
                            c.R(0, 0, 12, 12, "#d9c8a6"); c.R(0, 0, 12, 1, "#a88b5c"); c.R(0, 11, 12, 1, "#a88b5c");
                            if (win) { c.R(3, 3, 5, 5, "#5a3d2a"); c.R(4, 4, 3, 3, "#8fc3e8"); c.R(5, 4, 1, 3, "#5a3d2a"); }
                        });
                    }
                    bool top = g.At(x, y - 1) != 'H', l = g.At(x - 1, y) != 'H', r = g.At(x + 1, y) != 'H';
                    return Cached($"roof{top}{l}{r}", "house_roof", c =>
                    {
                        c.R(0, 0, 12, 12, "#b5483a");
                        for (int j = 1; j < 12; j += 3) c.R(0, j, 12, 1, "#8f3328");
                        if (top) c.R(0, 0, 12, 2, "#d4685a");
                        if (l) c.R(0, 0, 1, 12, "#7a2a21");
                        if (r) c.R(11, 0, 1, 12, "#7a2a21");
                    });
                }
                case '+':
                    if (g.kind != "town") return null;
                    return Cached("door", "door", c =>
                    {
                        c.R(0, 0, 12, 12, "#d9c8a6"); c.R(0, 0, 12, 1, "#a88b5c");
                        c.R(3, 2, 6, 10, "#7a4b25"); c.R(3, 2, 6, 1, "#5a3519"); c.R(7, 7, 1, 1, "#f2c14e");
                    });
            }
            return null;
        }

        static readonly string[] Hues = { "#3d6fd1", "#c2533a", "#3a9a6a", "#a347b8", "#d19a2e", "#2f8fa8", "#7a5ad6", "#b8476c" };
        static readonly string[] Hairs = { "#3a2a1e", "#6b4423", "#d8b25a", "#999999", "#222222" };

        public static Sprite Person(string id, bool player)
        {
            int h = 0;
            foreach (char ch in id) h = h * 31 + ch;
            h = Mathf.Abs(h);
            string body = player ? "#e0457b" : Hues[h % Hues.Length], hair = player ? "#2a1f3d" : Hairs[h % Hairs.Length];
            string key = player ? "player" : "npc" + body + hair;
            return Cached(key, player ? "player" : "npc", c =>
            {
                c.R(3, 11, 6, 1, new Color(0, 0, 0, .25f));
                c.R(4, 1, 4, 4, "#f1c9a0"); c.R(4, 0, 4, 2, hair); c.R(3, 1, 1, 2, hair); c.R(8, 1, 1, 2, hair);
                c.R(5, 3, 1, 1, "#222222"); c.R(7, 3, 1, 1, "#222222");
                c.R(3, 5, 6, 4, body); c.R(2, 6, 1, 3, body); c.R(9, 6, 1, 3, body);
                c.R(2, 9, 1, 1, "#f1c9a0"); c.R(9, 9, 1, 1, "#f1c9a0");
                c.R(4, 9, 2, 2, "#3b3550"); c.R(6, 9, 2, 2, "#3b3550");
                if (player) c.R(3, 5, 6, 1, "#f2c14e");
            }, player ? null : "_" + (h % Hues.Length));
        }

        public static Sprite Monster(string id)
        {
            int h = 0;
            foreach (char ch in id) h = h * 31 + ch;
            int v = Mathf.Abs(h) % 5;
            return Cached("monster" + v, "enemy", c =>
        {
            const string col = "#c2453f";
            c.R(2, 11, 8, 1, new Color(0, 0, 0, .25f));
            c.R(2, 4, 8, 7, col); c.R(3, 3, 6, 1, col); c.R(1, 6, 1, 4, col); c.R(10, 6, 1, 4, col);
            c.R(3, 5, 2, 2, "#ffffff"); c.R(7, 5, 2, 2, "#ffffff"); c.R(4, 6, 1, 1, "#111111"); c.R(7, 6, 1, 1, "#111111");
            c.R(4, 9, 4, 1, "#3a0d0d"); c.R(2, 2, 1, 2, col); c.R(9, 2, 1, 2, col);
        }, "_" + v);
        }

        public static Sprite Chest(bool open) => Cached("chest" + open, open ? "chest_open" : "chest", c =>
        {
            c.R(1, 10, 10, 1, new Color(0, 0, 0, .25f));
            c.R(1, open ? 5 : 4, 10, 6, "#8a5a2b"); c.R(1, open ? 5 : 4, 10, 1, "#5e3a17"); c.R(1, 7, 10, 1, "#c9a04a");
            if (open) { c.R(1, 2, 10, 3, "#5e3a17"); c.R(2, 5, 8, 2, "#2a1a0a"); }
            else { c.R(1, 3, 10, 2, "#a66f36"); c.R(5, 6, 2, 2, "#f2d16b"); }
        });

        public static Sprite Portal(string kind) => kind == "dungeon"
            ? Cached("stairs", "stairs", c => { c.R(1, 1, 10, 10, "#1b1a22"); for (int i = 0; i < 4; i++) c.R(1 + i, 3 + i * 2, 10 - i * 2, 1, "#77758a"); })
            : Cached("portal", "portal", c => { c.R(2, 2, 8, 9, "#9b6bff"); c.R(3, 3, 6, 7, "#c9b0ff"); c.R(5, 4, 2, 5, "#ffffff"); });

        // 只有碰撞、看不见的瓦片：NPC 和宝箱站的格子用它挡住寻路
        static Sprite blank;
        public static Sprite Blank()
        {
            if (blank != null) return blank;
            var t = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            t.SetPixel(0, 0, new Color(0, 0, 0, 0));
            t.Apply();
            blank = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1 / U);
            return blank;
        }
    }
}
