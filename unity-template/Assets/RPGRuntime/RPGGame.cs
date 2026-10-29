using System.Collections.Generic;
using NavMeshPlus.Components;
using NavMeshPlus.Extensions;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Tilemaps;

namespace RPGWorkshop
{
    // 游戏主体：读内容包 → 用 Tilemap 铺地图 → NavMeshPlus 烘焙可行走网格 → 点哪走哪
    // → Cinemachine 镜头跟随并限制在地图内 → 碰到 NPC 开对话、宝箱拿东西、传送门换地图。
    // 场景里什么都不用摆：按 Play 时自动创建全部对象。
    public class RPGGame : MonoBehaviour
    {
        public static bool AutoBoot = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!AutoBoot || FindAnyObjectByType<RPGGame>() != null) return;
            new GameObject("RPG Game").AddComponent<RPGGame>();
        }

        const float U = PixelArt.U;
        const float Speed = 4.5f * U;

        ContentPack pack;
        MapGrid map;
        Camera cam;
        CinemachineCamera vcam;
        CinemachineConfiner2D confiner;
        PolygonCollider2D bounds;
        Tilemap ground, obstacles, blockers;
        NavMeshSurface surface;
        int agentType;
        NavMeshAgent agent;
        SpriteRenderer playerSprite;
        Transform eventsRoot;

        readonly Dictionary<Sprite, Tile> tiles = new Dictionary<Sprite, Tile>();
        readonly Dictionary<MapEvent, SpriteRenderer> evObjs = new Dictionary<MapEvent, SpriteRenderer>();
        readonly HashSet<string> opened = new HashSet<string>(), beaten = new HashSet<string>();
        readonly List<string> bag = new List<string>(), log = new List<string>();
        readonly Dictionary<string, object> flags = new Dictionary<string, object>();
        MapEvent pending;
        System.Action guiAct; // 对话按钮的操作留到 Update 里执行，避免 IMGUI 布局和重绘之间状态不一致
        Vector2Int lastCell = new Vector2Int(-1, -1);

        // 对话状态
        string dlgName;
        Dictionary<string, object> dlg;
        Dictionary<string, object> node;
        List<string> fallbackLines;
        int fallbackIdx;

        void Start()
        {
            pack = ContentPack.Load(ContentPack.DefaultRoot);
            PixelArt.overrides = pack;
            PixelArt.ClearCache();
            SetupCamera();
            if (pack.maps.Count == 0) return;
            SetupWorld();
            LoadMap(pack.maps[0], null);
            Say("来到「" + map.name + "」。点地面走过去，点 NPC 说话，点宝箱打开；也可以用 WASD / 方向键走，空格和身边的人说话。");
        }

        // ---------- 搭建 ----------

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cgo.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.08f, .08f, .1f);
            cam.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
            cam.transparencySortMode = TransparencySortMode.CustomAxis;
            cam.transparencySortAxis = Vector3.up;
            if (!cam.TryGetComponent<CinemachineBrain>(out _)) cam.gameObject.AddComponent<CinemachineBrain>();

            var vgo = new GameObject("CM 跟随镜头");
            vcam = vgo.AddComponent<CinemachineCamera>();
            var lens = LensSettings.Default;
            lens.OrthographicSize = 6.5f * U;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 100f;
            vcam.Lens = lens;
            var composer = vgo.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10f;
            composer.Damping = new Vector3(.4f, .4f, 0);

            var bgo = new GameObject("地图边界（镜头不会拍到外面）");
            bounds = bgo.AddComponent<PolygonCollider2D>();
            bounds.isTrigger = true;
            confiner = vgo.AddComponent<CinemachineConfiner2D>();
            confiner.BoundingShape2D = bounds;
        }

        void SetupWorld()
        {
            agentType = 0; // 默认的 Humanoid 寻路体型

            var gridGo = new GameObject("Grid");
            gridGo.AddComponent<Grid>().cellSize = new Vector3(U, U, 0);
            ground = NewTilemap(gridGo, "地面", 0, false, true);
            obstacles = NewTilemap(gridGo, "障碍（墙 树 水 房子）", 1, true, true);
            blockers = NewTilemap(gridGo, "阻挡（NPC 宝箱站的格子）", 2, true, false);

            var sgo = new GameObject("NavMesh（NavMeshPlus）");
            sgo.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // 2D 寻路要把烘焙面转到 XY 平面
            surface = sgo.AddComponent<NavMeshSurface>();
            surface.agentTypeID = agentType;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.hideEditorLogs = true;
            sgo.AddComponent<CollectSources2d>();

            eventsRoot = new GameObject("事件（NPC 敌群 宝箱 传送点）").transform;

            var pgo = new GameObject("玩家");
            playerSprite = pgo.AddComponent<SpriteRenderer>();
            playerSprite.sprite = PixelArt.Person("player", true);
            playerSprite.sortingOrder = 5;
            vcam.Follow = pgo.transform;
        }

        Tilemap NewTilemap(GameObject grid, string name, int order, bool notWalkable, bool visible)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid.transform, false);
            var tm = go.AddComponent<Tilemap>();
            if (visible) go.AddComponent<TilemapRenderer>().sortingOrder = order;
            var mod = go.AddComponent<NavMeshModifier>();
            if (notWalkable)
            {
                mod.overrideArea = true;
                mod.area = 1; // Not Walkable
            }
            return tm;
        }

        Tile TileOf(Sprite s)
        {
            if (!tiles.TryGetValue(s, out var t))
            {
                t = ScriptableObject.CreateInstance<Tile>();
                t.sprite = s;
                t.colliderType = Tile.ColliderType.Grid; // 寻路按整格算
                tiles[s] = t;
            }
            return t;
        }

        // ---------- 坐标 ----------
        // 内容包里 y 向下（第 0 行在最上面），Unity 里 y 向上

        Vector3Int CellPos(int x, int y) => new Vector3Int(x, map.height - 1 - y, 0);
        Vector3 World(int x, int y) => new Vector3((x + .5f) * U, (map.height - 1 - y + .5f) * U, 0);
        Vector3 World(Vector2Int c) => World(c.x, c.y);
        Vector2Int CellOf(Vector3 w) => new Vector2Int(Mathf.FloorToInt(w.x / U), map.height - 1 - Mathf.FloorToInt(w.y / U));

        // ---------- 地图 ----------

        void LoadMap(MapGrid m, string fromMapId)
        {
            map = m;
            ground.ClearAllTiles();
            obstacles.ClearAllTiles();
            blockers.ClearAllTiles();
            foreach (var o in evObjs.Values) Destroy(o.gameObject);
            evObjs.Clear();
            pending = null;

            for (int y = 0; y < m.height; y++)
                for (int x = 0; x < m.width; x++)
                {
                    char t = m.At(x, y);
                    var p = CellPos(x, y);
                    // 村子的门可以走，画在地面层
                    var g = t == '+' && m.kind == "town" ? PixelArt.Obstacle(m, x, y) : PixelArt.Ground(m, x, y);
                    ground.SetTile(p, TileOf(g));
                    if (PixelArt.Solid(t))
                    {
                        var o = PixelArt.Obstacle(m, x, y);
                        if (o != null) obstacles.SetTile(p, TileOf(o));
                    }
                }

            foreach (var e in m.events)
            {
                if (e.type == "troop" && beaten.Contains(e.id)) continue;
                var go = new GameObject(e.type + " " + e.refId);
                go.transform.SetParent(eventsRoot, false);
                go.transform.position = World(e.x, e.y);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 4;
                switch (e.type)
                {
                    case "npc": sr.sprite = PixelArt.Person(e.refId, false); break;
                    case "troop": sr.sprite = PixelArt.Monster(); break;
                    case "chest": sr.sprite = PixelArt.Chest(opened.Contains(e.id)); break;
                    case "transfer": sr.sprite = PixelArt.Portal(m.kind); sr.sortingOrder = 2; break;
                    default: sr.sprite = PixelArt.Chest(true); break;
                }
                evObjs[e] = sr;
                if (Blocks(e)) blockers.SetTile(CellPos(e.x, e.y), TileOf(PixelArt.Blank()));
            }

            ground.CompressBounds();
            obstacles.CompressBounds();
            blockers.CompressBounds();
            Physics2D.SyncTransforms();
            surface.BuildNavMesh();

            bounds.SetPath(0, new[] { new Vector2(0, 0), new Vector2(m.width * U, 0), new Vector2(m.width * U, m.height * U), new Vector2(0, m.height * U) });
            confiner.InvalidateBoundingShapeCache();

            var start = m.spawn;
            if (fromMapId != null)
            {
                var back = m.events.Find(e => e.type == "transfer" && e.refId == fromMapId);
                if (back != null)
                {
                    foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                    {
                        var c = new Vector2Int(back.x + d.x, back.y + d.y);
                        if (!PixelArt.Solid(m.At(c.x, c.y)) && EventAt(c) == null) { start = c; break; }
                    }
                }
            }
            PlacePlayer(start);
        }

        bool Blocks(MapEvent e) => e.type == "npc" || e.type == "chest";

        void PlacePlayer(Vector2Int c)
        {
            var pos = World(c);
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(pos, out var hit, 2f * U, filter)) pos = new Vector3(hit.position.x, hit.position.y, 0);
            var tr = playerSprite.transform;
            if (agent == null)
            {
                tr.position = pos;
                agent = tr.gameObject.AddComponent<NavMeshAgent>();
                agent.agentTypeID = agentType;
                agent.radius = .5f;
                agent.height = 2f;
                agent.speed = Speed;
                agent.acceleration = 40f * U;
                agent.angularSpeed = 0f;
                agent.stoppingDistance = .05f * U;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                agent.updateRotation = false; // NavMeshPlus 2D 必须关掉这两个
                agent.updateUpAxis = false;
            }
            agent.Warp(pos);
            lastCell = CellOf(pos);
            vcam.PreviousStateIsValid = false; // 换地图时镜头直接切过去，不要从上一张图慢慢滑
        }

        MapEvent EventAt(Vector2Int c)
        {
            foreach (var e in map.events)
                if (e.x == c.x && e.y == c.y && !(e.type == "troop" && beaten.Contains(e.id))) return e;
            return null;
        }

        // ---------- 每帧 ----------

        void Update()
        {
            if (map == null || agent == null) return;
            if (guiAct != null) { var a = guiAct; guiAct = null; a(); }

            if (dlg != null || fallbackLines != null)
            {
                if (InputBridge.ConfirmDown()) AdvanceIfSingle();
                return;
            }

            var mv = InputBridge.Move();
            if (mv != Vector2.zero && agent.isOnNavMesh)
            {
                agent.ResetPath();
                pending = null;
                agent.Move(new Vector3(mv.x, mv.y, 0) * Speed * Time.deltaTime);
                playerSprite.flipX = mv.x < 0 || (mv.x == 0 && playerSprite.flipX);
            }
            else if (agent.hasPath)
            {
                if (Mathf.Abs(agent.velocity.x) > .05f) playerSprite.flipX = agent.velocity.x < 0;
            }

            if (InputBridge.ConfirmDown())
            {
                var near = Adjacent(CellOf(agent.transform.position));
                if (near != null) Interact(near);
            }

            if (InputBridge.ClickDown(out var sp) && GUIUtility.hotControl == 0 && !OverHud(sp)) OnClick(sp);

            if (pending != null && !agent.pathPending && agent.remainingDistance <= .1f * U)
            {
                var e = pending;
                pending = null;
                var me = CellOf(agent.transform.position);
                if (Mathf.Abs(me.x - e.x) + Mathf.Abs(me.y - e.y) <= 1) Interact(e);
                else Say("走不到那里。");
            }

            var cell = CellOf(agent.transform.position);
            if (cell != lastCell)
            {
                lastCell = cell;
                var e = EventAt(cell);
                if (e != null && (e.type == "transfer" || e.type == "troop")) Interact(e);
            }
        }

        bool OverHud(Vector2 sp) => sp.y < Screen.height * .22f && (dlg != null || fallbackLines != null);

        MapEvent Adjacent(Vector2Int me)
        {
            foreach (var d in new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                var e = EventAt(me + d);
                if (e != null && e.type != "transfer") return e;
            }
            return null;
        }

        void OnClick(Vector2 sp)
        {
            var w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -cam.transform.position.z));
            var c = CellOf(w);
            var e = EventAt(c);
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
            if (e != null && Blocks(e))
            {
                var me = CellOf(agent.transform.position);
                if (Mathf.Abs(me.x - e.x) + Mathf.Abs(me.y - e.y) <= 1) { agent.ResetPath(); Interact(e); return; }
                // 人和宝箱站的格子走不进去：在它四周挑一格路最短的走过去，到了再触发
                Vector3? best = null;
                float bestLen = float.MaxValue;
                var path = new NavMeshPath();
                foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    var n = new Vector2Int(e.x + d.x, e.y + d.y);
                    if (PixelArt.Solid(map.At(n.x, n.y)) || (EventAt(n) != null && Blocks(EventAt(n)))) continue;
                    if (!NavMesh.CalculatePath(agent.transform.position, World(n), filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    float len = 0;
                    for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                    if (len < bestLen) { bestLen = len; best = World(n); }
                }
                if (best == null) { Say("走不到那里。"); return; }
                pending = e;
                agent.SetDestination(best.Value);
                return;
            }
            pending = null;
            if (NavMesh.SamplePosition(w, out var hit, .7f * U, filter)) agent.SetDestination(new Vector3(hit.position.x, hit.position.y, 0));
        }

        // ---------- 事件 ----------

        void Interact(MapEvent e)
        {
            switch (e.type)
            {
                case "npc":
                    StartDialog(e.refId);
                    break;
                case "chest":
                    if (opened.Contains(e.id)) { Say("宝箱已经空了。"); break; }
                    opened.Add(e.id);
                    var item = pack.ItemName(e.refId);
                    bag.Add(item);
                    if (evObjs.TryGetValue(e, out var sr)) sr.sprite = PixelArt.Chest(true);
                    Say("打开宝箱，获得「" + item + "」。");
                    break;
                case "troop":
                    beaten.Add(e.id);
                    var tname = pack.troops.TryGetValue(e.refId, out var tr) ? J.S(tr, "name", e.refId) : e.refId;
                    if (evObjs.TryGetValue(e, out var tsr)) { Destroy(tsr.gameObject); evObjs.Remove(e); }
                    Say("遭遇敌群「" + tname + "」。战斗系统在第 2 步加，现在先按胜利处理。");
                    break;
                case "transfer":
                    var to = pack.maps.Find(m => m.id == e.refId);
                    if (to == null) { Say("这个传送点通往 " + e.refId + "，但内容包里没有这张地图。"); break; }
                    var from = map.id;
                    agent.ResetPath();
                    LoadMap(to, from);
                    Say("进入「" + to.name + "」。");
                    break;
                default:
                    Say("触发事件 " + e.id + "（" + e.type + "）。");
                    break;
            }
        }

        void StartDialog(string npcId)
        {
            agent.ResetPath();
            dlgName = pack.NpcName(npcId);
            dlg = pack.DialogFor(npcId);
            if (dlg != null)
            {
                var nodes = J.L(dlg, "nodes");
                node = nodes.Count > 0 ? nodes[0] as Dictionary<string, object> : null;
                if (node != null) return;
                dlg = null;
            }
            // 没有任务对白的 NPC：用角色表里的身份说一句
            var npc = pack.npcs.TryGetValue(npcId, out var n) ? n : null;
            fallbackLines = new List<string> { "你好，旅行者。" };
            var fn = J.S(npc, "function");
            var desc = J.S(npc, "personality", J.S(npc, "desc"));
            if (desc != "") fallbackLines.Add(desc);
            else if (fn != "") fallbackLines.Add("（" + fn + "）");
            fallbackIdx = 0;
        }

        void Choose(Dictionary<string, object> opt)
        {
            foreach (var ef in J.L(opt, "effects"))
                if (J.S(ef, "type") == "set_flag" && ef is Dictionary<string, object> efd)
                    flags[J.S(ef, "flag")] = efd.TryGetValue("value", out var fv) ? fv : true;
            var next = J.S(opt, "next");
            node = null;
            if (next != "")
                foreach (var nd in J.L(dlg, "nodes"))
                    if (J.S(nd, "node_id") == next) { node = nd as Dictionary<string, object>; break; }
            if (node == null) dlg = null;
        }

        void AdvanceIfSingle()
        {
            if (fallbackLines != null)
            {
                if (++fallbackIdx >= fallbackLines.Count) fallbackLines = null;
                return;
            }
            var opts = J.L(node, "options");
            if (opts.Count <= 1) Choose(opts.Count == 1 ? opts[0] as Dictionary<string, object> : null);
        }

        void Say(string s)
        {
            log.Add(s);
            if (log.Count > 4) log.RemoveAt(0);
        }

        // ---------- 界面（IMGUI，不依赖任何 UI 资源） ----------

        GUIStyle box, label, btn, nameStyle;

        void OnGUI()
        {
            float k = Mathf.Max(1f, Screen.height / 720f);
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true };
                label = new GUIStyle(GUI.skin.label) { wordWrap = true };
                btn = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
                nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            box.fontSize = label.fontSize = btn.fontSize = Mathf.RoundToInt(17 * k);
            box.padding = new RectOffset((int)(14 * k), (int)(14 * k), (int)(10 * k), (int)(10 * k));
            nameStyle.fontSize = Mathf.RoundToInt(12 * k);

            if (pack == null) return;
            if (pack.maps.Count == 0)
            {
                GUI.Box(new Rect(40 * k, 40 * k, Screen.width - 80 * k, 200 * k), "还不能开始：\n\n" + string.Join("\n", pack.problems), box);
                return;
            }

            // NPC 头顶的名字
            foreach (var kv in evObjs)
            {
                if (kv.Key.type != "npc") continue;
                var p = cam.WorldToScreenPoint(kv.Value.transform.position + Vector3.up * .75f * U);
                if (p.z < 0) continue;
                var s = pack.NpcName(kv.Key.refId);
                var sz = nameStyle.CalcSize(new GUIContent(s));
                var r = new Rect(p.x - sz.x / 2 - 4, Screen.height - p.y - sz.y / 2, sz.x + 8, sz.y);
                GUI.color = new Color(0, 0, 0, .55f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(r, s, nameStyle);
            }

            // 左上：地图名、背包、最近消息
            var top = "【" + map.name + "】" + (bag.Count > 0 ? "  背包：" + string.Join("、", bag) : "");
            GUILayout.BeginArea(new Rect(12 * k, 10 * k, Mathf.Min(Screen.width - 24 * k, 760 * k), 220 * k));
            GUILayout.Label(top, label);
            foreach (var l in log) GUILayout.Label(l, label);
            GUILayout.EndArea();

            // 底部：对话框
            if (dlg == null && fallbackLines == null) return;
            var rect = new Rect(20 * k, Screen.height - 230 * k, Screen.width - 40 * k, 210 * k);
            GUI.color = new Color(1, 1, 1, .96f);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUI.color = Color.white;
            if (fallbackLines != null)
            {
                GUILayout.Label("<b>" + dlgName + "</b>：" + fallbackLines[fallbackIdx], new GUIStyle(label) { richText = true });
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("继续（空格）", btn)) guiAct = AdvanceIfSingle;
            }
            else
            {
                var speaker = J.S(node, "speaker");
                if (speaker == "") speaker = dlgName;
                GUILayout.Label("<b>" + speaker + "</b>：" + J.S(node, "text"), new GUIStyle(label) { richText = true });
                GUILayout.FlexibleSpace();
                var opts = J.L(node, "options");
                if (opts.Count == 0)
                {
                    if (GUILayout.Button("结束对话（空格）", btn)) guiAct = () => Choose(null);
                }
                else
                {
                    for (int i = 0; i < opts.Count; i++)
                    {
                        var t = J.S(opts[i], "text");
                        if (t == "") t = "继续";
                        if (GUILayout.Button((opts.Count == 1 ? "" : (i + 1) + ". ") + t + (opts.Count == 1 ? "（空格）" : ""), btn))
                        {
                            var o = opts[i] as Dictionary<string, object>;
                            guiAct = () => Choose(o);
                        }
                    }
                }
            }
            GUILayout.EndArea();
        }
    }
}
