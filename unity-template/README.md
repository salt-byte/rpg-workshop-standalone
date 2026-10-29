# RPG 工坊 · Unity 模板（第 1 步）

用这个模板，可以把 RPG 工坊导出的内容包直接变成 Unity 里能走、能对话的游戏。

## 这一步能做什么

- **地图**：读取内容包里的地图格子，用 Tilemap 一格一格铺出来，分成地面、障碍、阻挡三层。
- **点哪走哪**：用 NavMeshPlus 烘焙可行走网格，角色会自动绕开墙、树、水和房子。
  - 点 NPC 或宝箱：先走到旁边，再触发。
  - 也可以用 WASD 或方向键走。
- **镜头**：用 Cinemachine 跟随主角，到了地图边缘会停住，不会拍到地图外面。
- **事件**：
  - NPC：开对话。内容来自 `dialogs.json`，可以选选项，选项会写 flag。
  - 宝箱：打开后东西进背包。
  - 传送门：走上去切换地图，到新地图后站在回程门旁边。
  - 敌群：现在碰到就按胜利处理。战斗是第 2 步的内容。

瓦片和小人现在都是代码画的像素图，所以没有任何素材也能直接跑。要换美术，就把同名 png 放进内容包的 `art/tiles/` 文件夹，会自动替换。可用的文件名见下面的列表。

## 第一次打开（大约 20 分钟，只需要做一次）

1. 安装 [Unity Hub](https://unity.com/download)，用 Unity 账号登录。个人版免费。
2. 在 Unity Hub 的 Installs 里装 **Unity 6（6000.0 LTS）**，模块全部不用勾。
3. 电脑上要有 git。Mac 在终端运行 `git --version`，如果提示安装命令行工具，点安装即可。
4. 打开工程：在 Unity Hub 点 Projects → Add → Add project from disk，选这个 `unity-template` 文件夹，然后打开。
   - 第一次打开会自动下载插件（Cinemachine、NavMeshPlus、Input System），需要几分钟。
   - 如果弹窗问是否启用新输入系统（"enable the new input system backends"），点 **Yes**，Unity 会重启一次。

## 每次做新游戏

1. 在 RPG 工坊网页里跑完各个环节，环节 10 点"导出内容包"，得到一个 zip。
2. 在 Unity 顶部菜单点 **RPG 工坊 → 1. 导入内容包**，选这个 zip。
3. 点 **RPG 工坊 → 2. 打开游戏场景**。
4. 按编辑器顶部中间的 ▶ 播放，就能玩了。

工程里已经带了一份示例内容包（雾港村 → 雾林 → 古墓），没导入自己的内容也可以直接按 ▶ 试玩。

## 打包成独立游戏

依次点 File → Build Profiles，选 macOS 或 Windows，再点 Build。

网页版（WebGL）读内容包的方式不同，第 1 步还不支持。

## 替换美术用的瓦片文件名（放进 `content/art/tiles/`）

| 文件名 | 用在哪里 |
|---|---|
| `town_grass` `field_grass` | 村镇 / 野外的地面 |
| `town_path` `field_path` | 路 |
| `dungeon_floor` | 地牢地板 |
| `tree` `water` | 树、水 |
| `wall_face` `wall_top` | 墙的正面 / 墙顶 |
| `house_wall` `house_roof` `door` | 房子 |
| `player` `npc` `enemy` | 主角、NPC、敌人 |
| `chest` `chest_open` `portal` `stairs` | 宝箱、传送门、地牢台阶 |

## 文件说明

| 文件 | 作用 |
|---|---|
| `Assets/RPGRuntime/RPGGame.cs` | 游戏主体：铺地图、寻路、镜头、事件、对话框 |
| `Assets/RPGRuntime/ContentPack.cs` | 读取内容包 |
| `Assets/RPGRuntime/PixelArt.cs` | 代码画的像素瓦片和小人 |
| `Assets/RPGRuntime/Editor/RPGWorkshopMenu.cs` | 顶部的「RPG 工坊」菜单 |
| `Assets/StreamingAssets/content/` | 当前使用的内容包 |
