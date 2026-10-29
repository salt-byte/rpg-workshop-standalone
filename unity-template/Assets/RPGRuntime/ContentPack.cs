using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RPGWorkshop
{
    // 一张地图的格子数据，来自内容包 tables/map_grids.json（RPG 工坊环节 8 导出）
    public class MapGrid
    {
        public string id, name, kind;
        public int width, height;
        public string[] rows;          // rows[0] 是最上面一行
        public Vector2Int spawn;       // 格子坐标，y 向下
        public List<MapEvent> events = new List<MapEvent>();

        public char At(int x, int y)
        {
            if (y < 0 || y >= rows.Length || x < 0 || x >= rows[y].Length) return '#';
            return rows[y][x];
        }
    }

    public class MapEvent
    {
        public string id, type, refId;
        public int x, y;
    }

    // 内容包：RPG 工坊导出的 zip 解压后的文件夹。只读这一步需要的表。
    public class ContentPack
    {
        public string root;
        public string title = "";
        public readonly List<MapGrid> maps = new List<MapGrid>();
        public readonly Dictionary<string, Dictionary<string, object>> npcs = new Dictionary<string, Dictionary<string, object>>();
        public readonly Dictionary<string, Dictionary<string, object>> items = new Dictionary<string, Dictionary<string, object>>();
        public readonly Dictionary<string, Dictionary<string, object>> troops = new Dictionary<string, Dictionary<string, object>>();
        public readonly Dictionary<string, Dictionary<string, object>> enemies = new Dictionary<string, Dictionary<string, object>>();
        public readonly List<object> quests = new List<object>();
        public readonly List<object> dialogs = new List<object>();
        public Dictionary<string, object> hero;
        public readonly List<string> problems = new List<string>();

        public static string DefaultRoot => Path.Combine(Application.streamingAssetsPath, "content");

        public static ContentPack Load(string root)
        {
            var p = new ContentPack { root = root };
            if (!Directory.Exists(root))
            {
                p.problems.Add("找不到内容包文件夹：" + root + "。在菜单「RPG 工坊 → 导入内容包」里选 RPG 工坊导出的 zip。");
                return p;
            }

            var grids = Read("tables/map_grids.json", p) as List<object>;
            var mapRows = Read("tables/maps.json", p) as List<object>;
            var names = new Dictionary<string, string>();
            if (mapRows != null) foreach (var m in mapRows) names[J.S(m, "id")] = J.S(m, "name");
            if (grids == null)
            {
                p.problems.Add("内容包里没有 tables/map_grids.json。请用最新版 RPG 工坊重新导出（环节 8 要有地图）。");
            }
            else
            {
                foreach (var g in grids)
                {
                    var rows = new List<string>();
                    foreach (var r in J.L(g, "rows")) rows.Add(r as string ?? "");
                    if (rows.Count == 0) continue;
                    var mg = new MapGrid
                    {
                        id = J.S(g, "map_id"),
                        kind = J.S(g, "kind", "field"),
                        width = J.I(g, "width", rows[0].Length),
                        height = J.I(g, "height", rows.Count),
                        rows = rows.ToArray(),
                    };
                    mg.name = names.TryGetValue(mg.id, out var n) && n != "" ? n : mg.id;
                    var sp = J.D(g, "spawn");
                    mg.spawn = sp != null ? new Vector2Int(J.I(sp, "x"), J.I(sp, "y")) : new Vector2Int(mg.width / 2, mg.height / 2);
                    foreach (var e in J.L(g, "events"))
                        mg.events.Add(new MapEvent { id = J.S(e, "id"), type = J.S(e, "type"), refId = J.S(e, "ref"), x = J.I(e, "x"), y = J.I(e, "y") });
                    p.maps.Add(mg);
                }
            }

            Index(Read("tables/npcs.json", p), p.npcs);
            Index(Read("tables/items.json", p), p.items);
            Index(Read("tables/troops.json", p), p.troops);
            Index(Read("tables/enemies.json", p), p.enemies);
            if (Read("tables/quests.json", p) is List<object> q) p.quests.AddRange(q);
            if (Read("dialogue/dialogs.json", p) is List<object> d) p.dialogs.AddRange(d);
            if (Read("tables/hero.json", p) is List<object> h && h.Count > 0) p.hero = h[0] as Dictionary<string, object>;
            if (Read("inputs.json", p) is Dictionary<string, object> inp) p.title = J.S(inp, "name");
            return p;
        }

        static object Read(string rel, ContentPack p)
        {
            var path = Path.Combine(p.root, rel);
            if (!File.Exists(path)) return null;
            try { return MiniJson.Parse(File.ReadAllText(path)); }
            catch (System.Exception e) { p.problems.Add(rel + " 读取失败：" + e.Message); return null; }
        }

        static void Index(object rows, Dictionary<string, Dictionary<string, object>> into)
        {
            if (!(rows is List<object> l)) return;
            foreach (var r in l)
                if (r is Dictionary<string, object> d) into[J.S(d, "id")] = d;
        }

        public string NpcName(string id) => npcs.TryGetValue(id, out var n) ? J.S(n, "name", id) : id;
        public string ItemName(string id) => items.TryGetValue(id, out var n) ? J.S(n, "name", id) : id;

        // 和 NPC 说话时用的对白：优先这个 NPC 发布的第一个任务的「接任务」对白
        public Dictionary<string, object> DialogFor(string npcId)
        {
            foreach (var q in quests)
            {
                if (J.S(q, "giver_npc") != npcId) continue;
                var qid = J.S(q, "id");
                foreach (var d in dialogs)
                    if (J.S(d, "quest_id") == qid && J.S(d, "stage") == "accept") return d as Dictionary<string, object>;
            }
            return null;
        }

        // 内容包里放了 art/tiles/<名字>.png 就用它替换代码画的瓦片（AI 生成或买的素材都放这里）
        public Texture2D TileOverride(string name)
        {
            var path = Path.Combine(root, "art", "tiles", name + ".png");
            if (!File.Exists(path)) return null;
            var t = new Texture2D(2, 2) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            return t.LoadImage(File.ReadAllBytes(path)) ? t : null;
        }
    }
}
