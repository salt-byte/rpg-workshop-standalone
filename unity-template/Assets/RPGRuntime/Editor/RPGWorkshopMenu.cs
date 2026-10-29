using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RPGWorkshop.EditorTools
{
    // 菜单栏「RPG 工坊」：不懂 Unity 也只需要点这里的三个按钮
    public static class RPGWorkshopMenu
    {
        const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("RPG 工坊/1. 导入内容包（选 RPG 工坊导出的 zip）", priority = 1)]
        static void ImportPack()
        {
            var zip = EditorUtility.OpenFilePanel("选择 RPG 工坊导出的内容包", "", "zip");
            if (string.IsNullOrEmpty(zip)) return;
            var dest = ContentPack.DefaultRoot;
            try
            {
                if (Directory.Exists(dest)) Directory.Delete(dest, true);
                Directory.CreateDirectory(dest);
                ZipFile.ExtractToDirectory(zip, dest);
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("导入失败", e.Message, "好");
                return;
            }
            AssetDatabase.Refresh();
            var pack = ContentPack.Load(dest);
            var msg = pack.maps.Count > 0
                ? $"已导入：{pack.maps.Count} 张地图、{pack.npcs.Count} 个 NPC、{pack.dialogs.Count} 段对白。\n\n下一步：点菜单「RPG 工坊 → 2. 打开游戏场景」，再按上方的 ▶ 播放。"
                : "导入了，但有问题：\n" + string.Join("\n", pack.problems);
            EditorUtility.DisplayDialog("导入内容包", msg, "好");
        }

        [MenuItem("RPG 工坊/2. 打开游戏场景", priority = 2)]
        static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        // 空场景就够了：按播放时 RPGGame 会自动创建地图、镜头、玩家
        public static string EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return ScenePath;
        }

        [MenuItem("RPG 工坊/3. 打开内容包文件夹", priority = 3)]
        static void Reveal()
        {
            Directory.CreateDirectory(ContentPack.DefaultRoot);
            EditorUtility.RevealInFinder(ContentPack.DefaultRoot);
        }
    }
}
