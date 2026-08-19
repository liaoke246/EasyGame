using System;
using System.IO;
using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Data;
using EasyGame.SideScroller.Network;
using EasyGame.SideScroller.Player;
using EasyGame.SideScroller.UI;
using EasyGame.SideScroller.World;
using Mirror;
using Mirror.SimpleWeb;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EasyGame.SideScroller.Editor
{
    public static class SideScrollerProjectBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/SideScroller.unity";
        private const string MovementConfigPath = "Assets/Game/Resources/Config/PlayerMovement.asset";
        private const string LevelConfigPath = "Assets/Game/Resources/Config/LevelProgression.asset";
        private const string ControllerPath = "Assets/Game/Resources/Player/PlayerPrototype.controller";
        private const string NetworkPlayerPrefabPath = "Assets/Game/Prefabs/NetworkPlayer.prefab";

        [MenuItem("EasyGame 2D/Prepare Project")]
        public static void PrepareProject()
        {
            EnsureFolders();
            CreateConfigAssets();
            CreateAnimatorController();
            GameObject networkPlayerPrefab = CreateNetworkPlayerPrefab();
            CreateScene(networkPlayerPrefab);
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("EasyGame 2D project prepared successfully.");
        }

        [MenuItem("EasyGame 2D/Prune Imported Mirror Examples")]
        public static void PruneImportedMirrorExamples()
        {
            string[] unusedPaths =
            {
                "Assets/Mirror/Examples",
                "Assets/Mirror/Hosting",
                "Assets/ScriptTemplates",
            };

            foreach (string path in unusedPaths)
            {
                if (AssetDatabase.IsValidFolder(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"Removed unused imported dependency content: {path}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("EasyGame 2D/Build Web Client")]
        public static void BuildWebGLFromMenu()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../PrebuiltWebGL"));
            BuildWebGL(output);
        }

        public static void BuildWebGLCommandLine()
        {
            string output = CommandLineValue("-buildOutput") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../PrebuiltWebGL"));
            BuildWebGL(output);
        }

        [MenuItem("EasyGame 2D/Build Windows Dedicated Server")]
        public static void BuildWindowsServerFromMenu()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../PrebuiltServerWindows/DeadRailsServer.exe"));
            BuildDedicatedServer(output, BuildTarget.StandaloneWindows64);
        }

        public static void BuildWindowsServerCommandLine()
        {
            string output = CommandLineValue("-serverOutput") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../PrebuiltServerWindows/DeadRailsServer.exe"));
            BuildDedicatedServer(output, BuildTarget.StandaloneWindows64);
        }

        [MenuItem("EasyGame 2D/Build Linux Dedicated Server")]
        public static void BuildLinuxServerFromMenu()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../PrebuiltServerLinux/DeadRailsServer.x86_64"));
            BuildDedicatedServer(output, BuildTarget.StandaloneLinux64);
        }

        public static void BuildLinuxServerCommandLine()
        {
            string output = CommandLineValue("-serverOutput") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../PrebuiltServerLinux/DeadRailsServer.x86_64"));
            BuildDedicatedServer(output, BuildTarget.StandaloneLinux64);
        }

        private static void BuildWebGL(string output)
        {
            PrepareProject();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            Directory.CreateDirectory(output);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.CleanBuildCache,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"EasyGame 2D WebGL build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }

            Debug.Log($"EasyGame 2D WebGL build completed: {output} ({report.summary.totalSize} bytes)");
        }

        private static void BuildDedicatedServer(string output, BuildTarget target)
        {
            PrepareProject();
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("Dedicated server output directory is invalid."));
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.CleanBuildCache,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"EasyGame 2D dedicated server build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }

            Debug.Log($"EasyGame 2D dedicated server completed: {output} ({report.summary.totalSize} bytes)");
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/Game/Art",
                "Assets/Game/Audio",
                "Assets/Game/ThirdParty",
                "Assets/Game/Scenes",
                "Assets/Game/Prefabs",
                "Assets/Game/Resources/Config",
                "Assets/Game/Resources/Player",
                "Assets/Game/Scripts/Core",
                "Assets/Game/Scripts/Player",
                "Assets/Game/Scripts/Combat",
                "Assets/Game/Scripts/Enemies",
                "Assets/Game/Scripts/Network",
                "Assets/Game/Scripts/Items",
                "Assets/Game/Scripts/Inventory",
                "Assets/Game/Scripts/Skills",
                "Assets/Game/Scripts/World",
                "Assets/Game/Scripts/UI",
                "Assets/Game/Scripts/Data",
            };

            foreach (string folder in folders)
            {
                Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, folder));
            }
        }

        private static void CreateConfigAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(MovementConfigPath) == null)
            {
                PlayerMovementConfig movement = ScriptableObject.CreateInstance<PlayerMovementConfig>();
                AssetDatabase.CreateAsset(movement, MovementConfigPath);
            }

            if (AssetDatabase.LoadAssetAtPath<LevelProgressionConfig>(LevelConfigPath) == null)
            {
                LevelProgressionConfig levels = ScriptableObject.CreateInstance<LevelProgressionConfig>();
                AssetDatabase.CreateAsset(levels, LevelConfigPath);
            }
        }

        private static void CreateAnimatorController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                return;
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Motion", AnimatorControllerParameterType.Int);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            string[] names = { "Idle", "Run", "Jump", "Fall", "Attack", "Hit", "Death" };
            for (int index = 0; index < names.Length; index++)
            {
                AnimationClip clip = CreateMotionClip(names[index], index);
                AnimatorState state = stateMachine.AddState(names[index], new Vector3(280f, 55f + index * 62f, 0f));
                state.motion = clip;
                if (index == 0)
                {
                    stateMachine.defaultState = state;
                }

                AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(state);
                transition.hasExitTime = false;
                transition.duration = 0.045f;
                transition.canTransitionToSelf = false;
                transition.AddCondition(AnimatorConditionMode.Equals, index, "Motion");
            }
        }

        private static AnimationClip CreateMotionClip(string name, int motion)
        {
            string path = $"Assets/Game/Resources/Player/{name}.anim";
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                return existing;
            }

            AnimationClip clip = new AnimationClip { name = name, frameRate = 30f };
            string bodyPath = "Visual/Body";
            switch (motion)
            {
                case 0:
                    clip.SetCurve(bodyPath, typeof(Transform), "m_LocalPosition.y", Curve(0f, 0f, 0.45f, 0.025f, 0.9f, 0f));
                    SetLoop(clip, true);
                    break;
                case 1:
                    clip.SetCurve(bodyPath, typeof(Transform), "m_LocalPosition.y", Curve(0f, 0f, 0.1f, 0.075f, 0.2f, 0f, 0.3f, 0.075f, 0.4f, 0f));
                    clip.SetCurve(bodyPath, typeof(Transform), "m_LocalScale.x", Curve(0f, 0.82f, 0.1f, 0.78f, 0.2f, 0.82f, 0.3f, 0.78f, 0.4f, 0.82f));
                    SetLoop(clip, true);
                    break;
                case 2:
                    clip.SetCurve(bodyPath, typeof(Transform), "m_LocalScale.y", Curve(0f, 1.42f, 0.18f, 1.55f));
                    break;
                case 3:
                    clip.SetCurve(bodyPath, typeof(Transform), "m_LocalScale.y", Curve(0f, 1.55f, 0.2f, 1.42f));
                    break;
                case 4:
                    clip.SetCurve(bodyPath, typeof(Transform), "m_LocalScale.x", Curve(0f, 0.82f, 0.08f, 0.98f, 0.2f, 0.82f));
                    clip.SetCurve(bodyPath, typeof(Transform), "m_LocalPosition.x", Curve(0f, 0f, 0.08f, 0.12f, 0.2f, 0f));
                    break;
                case 5:
                    clip.SetCurve(bodyPath, typeof(Transform), "m_LocalPosition.x", Curve(0f, 0f, 0.05f, -0.1f, 0.1f, 0.08f, 0.18f, 0f));
                    break;
                case 6:
                    clip.SetCurve(bodyPath, typeof(Transform), "localEulerAnglesRaw.z", Curve(0f, 0f, 0.55f, -82f));
                    clip.SetCurve(bodyPath, typeof(Transform), "m_LocalPosition.y", Curve(0f, 0f, 0.55f, -0.52f));
                    break;
            }

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static AnimationCurve Curve(params float[] timeValuePairs)
        {
            Keyframe[] keys = new Keyframe[timeValuePairs.Length / 2];
            for (int index = 0; index < keys.Length; index++)
            {
                keys[index] = new Keyframe(timeValuePairs[index * 2], timeValuePairs[index * 2 + 1]);
            }
            return new AnimationCurve(keys);
        }

        private static void SetLoop(AnimationClip clip, bool loop)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static GameObject CreateNetworkPlayerPrefab()
        {
            GameObject root = new GameObject("Network Player");
            root.AddComponent<NetworkIdentity>();
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.mass = 1f;
            body.gravityScale = 3.15f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D collider = root.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.72f, 1.48f);
            collider.offset = new Vector2(0f, 0.02f);
            collider.sharedMaterial = new PhysicsMaterial2D("Network Player Material") { friction = 0f, bounciness = 0f };

            PlayerInputReader input = root.AddComponent<PlayerInputReader>();
            input.enabled = false;
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

            SideScrollerNetworkTransform networkTransform = root.AddComponent<SideScrollerNetworkTransform>();
            networkTransform.target = root.transform;
            networkTransform.syncDirection = SyncDirection.ServerToClient;
            networkTransform.syncInterval = 1f / 30f;
            networkTransform.updateMethod = UpdateMethod.FixedUpdate;
            networkTransform.syncPosition = true;
            networkTransform.syncRotation = false;
            networkTransform.syncScale = false;
            networkTransform.coordinateSpace = CoordinateSpace.World;
            root.AddComponent<SideScrollerNetworkPlayer>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, NetworkPlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateScene(GameObject networkPlayerPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("SideScroller Game", typeof(SideScrollerBootstrap), typeof(SideWorldBuilder));
            root.transform.position = Vector3.zero;

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SideCameraRig));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.2f;
            camera.backgroundColor = new Color(0.04f, 0.075f, 0.1f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            GameObject networkObject = new GameObject("Mirror Network");
            SimpleWebTransport transport = networkObject.AddComponent<SimpleWebTransport>();
            transport.port = 27777;
            transport.clientUseWss = false;
            transport.clientWebsocketSettings = new ClientWebsocketSettings
            {
                ClientPortOption = WebsocketPortOption.DefaultSameAsServer,
                CustomClientPort = 27777,
            };
            SideScrollerNetworkManager manager = networkObject.AddComponent<SideScrollerNetworkManager>();
            manager.transport = transport;
            manager.playerPrefab = networkPlayerPrefab;
            manager.maxConnections = 4;
            manager.autoCreatePlayer = true;
            manager.dontDestroyOnLoad = false;
            manager.sendRate = 30;
            manager.headlessStartMode = HeadlessStartOptions.AutoStartServer;
            networkObject.AddComponent<NetworkStatusHud>();

            CreateStartPosition("Player Spawn A", new Vector3(-7f, -2.15f, 0f));
            CreateStartPosition("Player Spawn B", new Vector3(-5.5f, -2.15f, 0f));
            CreateStartPosition("Player Spawn C", new Vector3(-4f, -2.15f, 0f));
            CreateStartPosition("Player Spawn D", new Vector3(-2.5f, -2.15f, 0f));

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void CreateStartPosition(string name, Vector3 position)
        {
            GameObject start = new GameObject(name, typeof(NetworkStartPosition));
            start.transform.position = position;
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "EasyGame";
            PlayerSettings.productName = "EasyGame: Dead Rails";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.WebGL.template = "PROJECT:SideScroller";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
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
