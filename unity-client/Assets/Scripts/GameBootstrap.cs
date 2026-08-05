using UnityEngine;

namespace EasyGame
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateGame()
        {
            if (Object.FindFirstObjectByType<GameWorldController>() != null)
            {
                return;
            }

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 28f;
            QualitySettings.antiAliasing = 2;

            GameObject root = new GameObject("EasyGame Unity Runtime");
            GameObject mapObject = new GameObject("World Map");
            mapObject.transform.SetParent(root.transform, false);
            WorldMap map = mapObject.AddComponent<WorldMap>();

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.45f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.065f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.rotation = Quaternion.Euler(56.3f, 0f, 0f);
            CameraRig cameraRig = cameraObject.AddComponent<CameraRig>();

            GameObject lightObject = new GameObject("Sun");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.42f;
            sun.color = new Color(1f, 0.88f, 0.69f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.62f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.56f, 0.47f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.35f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.18f, 0.14f);
            RenderSettings.ambientIntensity = 1.08f;

            GameObject networkObject = new GameObject("EasyGameNetworkBridge");
            networkObject.transform.SetParent(root.transform, false);
            WebSocketBridge network = networkObject.AddComponent<WebSocketBridge>();
            GameHud hud = root.AddComponent<GameHud>();
            GameWorldController controller = root.AddComponent<GameWorldController>();
            controller.Initialize(network, map, cameraRig, hud);
        }
    }
}
