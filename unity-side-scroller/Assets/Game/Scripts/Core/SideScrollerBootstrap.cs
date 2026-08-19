using EasyGame.SideScroller.Combat;
using EasyGame.SideScroller.Data;
using EasyGame.SideScroller.Player;
using EasyGame.SideScroller.Network;
using EasyGame.SideScroller.UI;
using EasyGame.SideScroller.World;
using Mirror;
using UnityEngine;

namespace EasyGame.SideScroller.Core
{
    public sealed class SideScrollerBootstrap : MonoBehaviour
    {
        private void Start()
        {
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
            Physics2D.gravity = new Vector2(0f, -9.81f);

            SideWorldBuilder world = GetComponent<SideWorldBuilder>();
            if (world == null)
            {
                world = gameObject.AddComponent<SideWorldBuilder>();
            }
            if (Utils.IsHeadless())
            {
                world.BuildServerCollision();
                Debug.Log("EasyGame 2D headless world initialized: collision-only.");
                return;
            }

            world.Build();

            if (!SideScrollerNetworkManager.NetworkingRequested)
            {
                GameObject player = CreateOfflinePlayer();
                ConfigureCamera(player.transform);
            }

            if (FindFirstObjectByType<SideScrollerHud>() == null)
            {
                new GameObject("Prototype HUD", typeof(SideScrollerHud));
            }
        }

        private static GameObject CreateOfflinePlayer()
        {
            GameObject player = new GameObject("Offline Player");
            player.transform.position = SideWorldBuilder.PlayerSpawn;

            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.mass = 1f;

            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.72f, 1.48f);
            collider.offset = new Vector2(0f, 0.02f);
            collider.sharedMaterial = new PhysicsMaterial2D("Player Material")
            {
                friction = 0f,
                bounciness = 0f,
            };

            PlayerInputReader input = player.AddComponent<PlayerInputReader>();
            PlayerMovement movement = player.AddComponent<PlayerMovement>();
            Animator animator = player.AddComponent<Animator>();
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Player/PlayerPrototype");
            PlayerAnimation animation = player.AddComponent<PlayerAnimation>();
            player.AddComponent<PlayerCombat>();

            Transform visualRoot = RuntimePlayerVisual.Create(player.transform, new Color(0.21f, 0.68f, 0.58f));
            PlayerMovementConfig config = Resources.Load<PlayerMovementConfig>("Config/PlayerMovement");
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
            }
            movement.Initialize(config, input);
            animation.Initialize(animator, movement, visualRoot);
            return player;
        }

        private static void ConfigureCamera(Transform target)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(SideCameraRig));
                cameraObject.tag = "MainCamera";
                camera = cameraObject.GetComponent<Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = 6.2f;
            camera.backgroundColor = new Color(0.04f, 0.075f, 0.1f);
            SideCameraRig rig = camera.GetComponent<SideCameraRig>();
            if (rig == null)
            {
                rig = camera.gameObject.AddComponent<SideCameraRig>();
            }
            rig.Initialize(target, SideWorldBuilder.MapBounds);
        }
    }
}
