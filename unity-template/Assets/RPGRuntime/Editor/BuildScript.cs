using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RPGWorkshop.EditorTools
{
    // 云端打包入口（GitHub Actions 里的 GameCI 调这个方法），本地也可以从菜单点
    public static class BuildScript
    {
        public const string WebGLOut = "build/play";

        [MenuItem("RPG 工坊/打包网页版（输出到 build/play）", priority = 20)]
        public static void BuildWebGL()
        {
            var scene = RPGWorkshopMenu.EnsureScene();
            PlayerSettings.companyName = "RPG Workshop";
            PlayerSettings.productName = "RPG 工坊试玩";
            PlayerSettings.runInBackground = true;
            // GitHub Pages 不会给压缩文件加 Content-Encoding，开 decompressionFallback 让浏览器端自己解压
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "PROJECT:RPG";

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = WebGLOut,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            Debug.Log("RPG 工坊网页版打包结果：" + report.summary.result + "，大小 " + report.summary.totalSize / 1024 / 1024 + " MB");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
