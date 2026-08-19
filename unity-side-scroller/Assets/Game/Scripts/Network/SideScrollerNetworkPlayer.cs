using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Data;
using EasyGame.SideScroller.Enemies;
using EasyGame.SideScroller.World;
using Mirror;
using UnityEngine;

namespace EasyGame.SideScroller.Network
{
    [RequireComponent(typeof(NetworkIdentity), typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class SideScrollerNetworkPlayer : NetworkBehaviour
    {
        [SyncVar] private string displayName = "SURVIVOR";
        [SyncVar] private int health = 100;
        [SyncVar] private int level = 1;
        [SyncVar] private int experience;
        [SyncVar] private int facing = 1;
        [SyncVar] private int motion;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private Player.PlayerInputReader input;
        private Animator animator;
        private Transform visualRoot;
        private PixelCharacterAnimator spriteAnimator;
        private PlayerMovementConfig config;
        private float serverHorizontal;
        private bool serverJumpHeld;
        private float serverCoyoteRemaining;
        private float serverJumpBufferRemaining;
        private float actionLockedUntil;
        private double nextInputAt;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            input = GetComponent<Player.PlayerInputReader>();
            animator = GetComponent<Animator>();
            config = Resources.Load<PlayerMovementConfig>("Config/PlayerMovement");
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
            }

            Transform existingVisual = transform.Find("Visual");
            if (Utils.IsHeadless())
            {
                visualRoot = existingVisual;
                if (visualRoot != null)
                {
                    visualRoot.gameObject.SetActive(false);
                }
                if (animator != null)
                {
                    animator.enabled = false;
                }
            }
            else if (existingVisual == null)
            {
                visualRoot = RuntimePlayerVisual.Create(transform, ColorForObject(GetComponent<NetworkIdentity>().netId));
            }
            else
            {
                visualRoot = existingVisual;
            }
            spriteAnimator = visualRoot != null ? visualRoot.GetComponent<PixelCharacterAnimator>() : null;

            input.enabled = false;
            body.gravityScale = config.gravityScale;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public override void OnStartServer()
        {
            body.simulated = true;
            displayName = $"SURVIVOR-{connectionToClient.connectionId + 1:00}";
        }

        public override void OnStartClient()
        {
            if (!isServer)
            {
                body.simulated = false;
            }
            RefreshVisuals();
        }

        public override void OnStartLocalPlayer()
        {
            input.enabled = true;
            Camera camera = Camera.main;
            if (camera != null && camera.TryGetComponent(out SideCameraRig rig))
            {
                rig.Initialize(transform, SideWorldBuilder.MapBounds);
            }
        }

        public override void OnStopLocalPlayer()
        {
            input.enabled = false;
        }

        private void Update()
        {
            if (isLocalPlayer)
            {
                SendLocalInput();
            }

            if (isClient)
            {
                RefreshVisuals();
            }

            if (isServer && transform.position.y < SideWorldBuilder.MapBounds.min.y - 4f)
            {
                transform.position = SideWorldBuilder.PlayerSpawn;
                body.linearVelocity = Vector2.zero;
            }
        }

        private void FixedUpdate()
        {
            if (!isServer || config == null)
            {
                return;
            }

            bool grounded = ProbeGround();
            serverCoyoteRemaining = grounded ? config.coyoteTime : Mathf.Max(0f, serverCoyoteRemaining - Time.fixedDeltaTime);
            serverJumpBufferRemaining = Mathf.Max(0f, serverJumpBufferRemaining - Time.fixedDeltaTime);

            Vector2 velocity = body.linearVelocity;
            float targetSpeed = serverHorizontal * config.moveSpeed;
            float acceleration = Mathf.Abs(targetSpeed) > 0.01f ? config.groundAcceleration : config.groundDeceleration;
            if (!grounded)
            {
                acceleration *= config.airControl;
            }
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, acceleration * Time.fixedDeltaTime);

            if (serverJumpBufferRemaining > 0f && serverCoyoteRemaining > 0f)
            {
                velocity.y = config.jumpVelocity;
                serverJumpBufferRemaining = 0f;
                serverCoyoteRemaining = 0f;
                grounded = false;
            }

            if (velocity.y < -0.01f)
            {
                velocity.y += Physics2D.gravity.y * body.gravityScale * (config.fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
            }
            else if (velocity.y > 0.01f && !serverJumpHeld)
            {
                velocity.y += Physics2D.gravity.y * body.gravityScale * (config.lowJumpGravityMultiplier - 1f) * Time.fixedDeltaTime;
            }

            velocity.y = Mathf.Max(velocity.y, -config.maxFallSpeed);
            body.linearVelocity = velocity;
            if (Mathf.Abs(serverHorizontal) > 0.05f)
            {
                facing = serverHorizontal > 0f ? 1 : -1;
            }

            if (Time.time >= actionLockedUntil)
            {
                motion = !grounded ? (velocity.y >= 0f ? 2 : 3) : (Mathf.Abs(velocity.x) > 0.15f ? 1 : 0);
            }
        }

        private void SendLocalInput()
        {
            if (NetworkTime.localTime >= nextInputAt)
            {
                nextInputAt = NetworkTime.localTime + 1d / 30d;
                CmdSetMovement(input.Horizontal, input.JumpHeld);
            }
            if (input.ConsumeJumpPressed())
            {
                CmdRequestJump();
            }
            input.ConsumeJumpReleased();
            if (input.ConsumeAttackPressed())
            {
                CmdRequestAttack();
            }
        }

        [Command(channel = Channels.Unreliable)]
        private void CmdSetMovement(float horizontal, bool jumpHeld)
        {
            serverHorizontal = Mathf.Clamp(horizontal, -1f, 1f);
            serverJumpHeld = jumpHeld;
        }

        [Command]
        private void CmdRequestJump()
        {
            serverJumpBufferRemaining = config.jumpBuffer;
        }

        [Command]
        private void CmdRequestAttack()
        {
            if (Time.time < actionLockedUntil)
            {
                return;
            }
            actionLockedUntil = Time.time + 0.25f;
            motion = 4;
            ResolveAttackHits();
            RpcPlayAttack();
        }

        [Server]
        private void ResolveAttackHits()
        {
            Vector2 center = new Vector2(transform.position.x + facing * 0.82f, transform.position.y);
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, new Vector2(1.45f, 1.25f), 0f);
            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out SideScrollerNetworkZombie zombie))
                {
                    zombie.ApplyDamage(30);
                }
            }
        }

        [ClientRpc]
        private void RpcPlayAttack()
        {
            if (spriteAnimator != null)
            {
                spriteAnimator.SetState(4, facing, 0f);
            }
        }

        private void RefreshVisuals()
        {
            if (spriteAnimator != null)
            {
                spriteAnimator.SetState(motion, facing, Mathf.Abs(body.linearVelocity.x));
            }
            else if (animator != null)
            {
                animator.SetInteger("Motion", motion);
                animator.SetFloat("Speed", Mathf.Abs(body.linearVelocity.x));
                animator.SetFloat("VerticalSpeed", body.linearVelocity.y);
            }
            if (visualRoot != null && spriteAnimator == null)
            {
                Vector3 scale = visualRoot.localScale;
                scale.x = Mathf.Abs(scale.x) * facing;
                visualRoot.localScale = scale;
            }
            gameObject.name = $"{displayName}  LV.{level}  HP.{health}  EXP.{experience}";
        }

        private bool ProbeGround()
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 size = new Vector2(bounds.size.x * config.groundProbeWidth, 0.08f);
            Vector2 origin = new Vector2(bounds.center.x, bounds.min.y - 0.02f);
            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, config.groundProbeDistance, ~0);
            return hit.collider != null && hit.collider != bodyCollider;
        }

        private static Color ColorForObject(uint objectId)
        {
            Color[] palette =
            {
                new Color(0.21f, 0.68f, 0.58f),
                new Color(0.32f, 0.54f, 0.86f),
                new Color(0.82f, 0.48f, 0.3f),
                new Color(0.64f, 0.42f, 0.76f),
            };
            return palette[(int)(objectId % (uint)palette.Length)];
        }

    }
}
