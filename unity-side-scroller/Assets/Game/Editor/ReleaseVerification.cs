using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EasyGame.SideScroller.Editor
{
    public static class ReleaseVerification
    {
        [Serializable] public sealed class FileHash { public string path; public string sha256; }
        [Serializable] public sealed class Manifest
        {
            public int schemaVersion = 1;
            public string version;
            public string unityVersion;
            public string target;
            public string sourceFingerprint;
            public string builtAtUtc;
            public FileHash[] files;
        }

        public static void RunRegressionTests()
        {
            MovementRegressionTests.Run();
            CombatRegressionTests.Run();
            NetworkRulesRegressionTests.Run();
            LifecycleRegressionTests.Run();
            SkillRegressionTests.Run();
            Debug.Log("GAMEPLAY REGRESSION SUITE PASSED");
        }

        public static string SourceFingerprint()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var inputs = new List<string>();
            foreach (string folder in new[] { "Assets/Game/Scripts", "Assets/Game/Editor", "Assets/WebGLTemplates/SideScroller", "Assets/Plugins/WebGL" })
                inputs.AddRange(Directory.GetFiles(Path.Combine(root, folder), "*", SearchOption.AllDirectories)
                    .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".html", StringComparison.Ordinal) || path.EndsWith(".js", StringComparison.Ordinal) || path.EndsWith(".jslib", StringComparison.Ordinal)));
            foreach (string file in new[] { "ProjectSettings/ProjectVersion.txt", "Packages/manifest.json", "Packages/packages-lock.json", "Assets/Mirror/version.txt", "Assets/Game/Resources/Config/PlayerMovement.asset", "Assets/Game/Resources/Config/LevelProgression.asset" })
                inputs.Add(Path.Combine(root, file));
            string list = string.Concat(inputs.Select(path => (path, relative: Path.GetRelativePath(root, path).Replace('\\', '/')))
                .OrderBy(item => item.relative, StringComparer.Ordinal)
                .Select(item => item.relative + "\n" + Hash(Encoding.UTF8.GetBytes(File.ReadAllText(item.path).Replace("\r\n", "\n"))) + "\n"));
            return Hash(Encoding.UTF8.GetBytes(list));
        }

        public static void WriteManifest(string output, string target, string sourceFingerprint)
        {
            if (SourceFingerprint() != sourceFingerprint)
                throw new InvalidOperationException("Source files changed during the build. Rebuild before publishing.");
            var manifest = new Manifest
            {
                version = PlayerSettings.bundleVersion,
                unityVersion = Application.unityVersion,
                target = target,
                sourceFingerprint = sourceFingerprint,
                builtAtUtc = DateTime.UtcNow.ToString("O"),
                files = Directory.GetFiles(output, "*", SearchOption.AllDirectories)
                    .Where(path => Path.GetFileName(path) != "release-manifest.json")
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => new FileHash { path = Path.GetRelativePath(output, path).Replace('\\', '/'), sha256 = Hash(File.ReadAllBytes(path)) }).ToArray(),
            };
            File.WriteAllText(Path.Combine(output, "release-manifest.json"), JsonUtility.ToJson(manifest, true) + "\n", new UTF8Encoding(false));
        }

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
