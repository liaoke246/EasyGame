using EasyGame.SideScroller.Combat;
using EasyGame.SideScroller.Data;
using EasyGame.SideScroller.Player;
using EasyGame.SideScroller.UI;
using EasyGame.SideScroller.World;
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
            world.Build();

            GameObject player = CreateOfflinePlayer();
            ConfigureCamera(player.transform);

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

            Transform visualRoot = CreatePlayerVisual(player.transform);
            PlayerMovementConfig config = Resources.Load<PlayerMovementConfig>("Config/PlayerMovement");
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
            }
            movement.Initialize(config, input);
            animation.Initialize(animator, movement, visualRoot);
            return player;
        }

        private static Transform CreatePlayerVisual(Transform parent)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);

            GameObject body = new GameObject("Body");
            body.transform.SetParent(visual.transform, false);
            body.transform.localScale = new Vector3(0.82f, 1.52f, 1f);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = RuntimeSpriteFactory.RoundedCharacter;
            bodyRenderer.color = new Color(0.21f, 0.68f, 0.58f);
            bodyRenderer.sortingOrder = 10;

            GameObject face = new GameObject("Visor");
            face.transform.SetParent(visual.transform, false);
            face.transform.localPosition = new Vector3(0.2f, 0.2f, -0.01f);
            face.transform.localScale = new Vector3(0.23f, 0.12f, 1f);
            SpriteRenderer faceRenderer = face.AddComponent<SpriteRenderer>();
            faceRenderer.sprite = RuntimeSpriteFactory.White;
            faceRenderer.color = new Color(0.86f, 0.93f, 0.78f);
            faceRenderer.sortingOrder = 11;
            return visual.transform;
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
