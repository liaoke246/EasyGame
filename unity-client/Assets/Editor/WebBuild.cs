using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EasyGame.Editor
{
    public static class WebBuild
    {
        [MenuItem("EasyGame/Build Web Client")]
        public static void BuildFromMenu()
        {
            Build(Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/unity-webgl")));
        }

        public static void BuildCommandLine()
        {
            string output = CommandLineValue("-buildOutput") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/unity-webgl"));
            Build(output);
        }

        private static void Build(string output)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            PlayerSettings.companyName = "EasyGame";
            PlayerSettings.productName = "EasyGame: Outbreak";
            PlayerSettings.bundleVersion = "0.3.0";
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.WebGL.template = "PROJECT:EasyGame";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;

            Directory.CreateDirectory(output);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Game.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.CleanBuildCache
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"WebGL build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }
            Debug.Log($"EasyGame WebGL build completed: {output} ({report.summary.totalSize} bytes)");
        }

        private static string CommandLineValue(string key)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (arguments[index] == key)
                {
                    return Path.GetFullPath(arguments[index + 1]);
                }
            }
            return null;
        }
    }
}
