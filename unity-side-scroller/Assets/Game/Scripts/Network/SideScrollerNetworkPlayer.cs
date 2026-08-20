using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Data;
using EasyGame.SideScroller.Enemies;
using EasyGame.SideScroller.UI;
using EasyGame.SideScroller.World;
using Mirror;
using UnityEngine;

namespace EasyGame.SideScroller.Network
{
    [RequireComponent(typeof(NetworkIdentity), typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class SideScrollerNetworkPlayer : NetworkBehaviour
    {
        private const int MaxHealthValue = 100;

        [SyncVar] private string displayName = "SURVIVOR";
        [SyncVar(hook = nameof(OnAvatarChanged))] private PlayerAvatarKind avatarKind = PlayerAvatarKind.Warrior;
        [SyncVar] private int health = MaxHealthValue;
        [SyncVar] private int level = 1;
        [SyncVar] private int experience;
        [SyncVar] private int kills;
        [SyncVar] private int deaths;
        [SyncVar] private int facing = 1;
        [SyncVar] private int motion;
        [SyncVar] private bool defeated;

        public static SideScrollerNetworkPlayer Local { get; private set; }
        public string DisplayName => displayName;
        public int Health => health;
        public int MaxHealth => MaxHealthValue;
        public int Experience => experience;
        public int Kills => kills;
        public int Deaths => deaths;
        public bool IsDefeated => defeated;
        public PlayerAvatarKind AvatarKind => avatarKind;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private Player.PlayerInputReader input;
        private Animator animator;
        private Transform visualRoot;
        private PlayerAvatarAnimator spriteAnimator;
        private PlayerMovementConfig config;
        private float serverHorizontal;
        private bool serverJumpHeld;
        private float serverCoyoteRemaining;
        private float serverJumpBufferRemaining;
        private float actionLockedUntil;
        private double nextInputAt;
        private double respawnAt;
        private double invulnerableUntil;

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

            Transform existingVisual = transform.Find(RuntimePlayerVisual.FeetAnchorName);
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
                visualRoot = RuntimePlayerVisual.CreatePlayer(transform, avatarKind, ColorForObject(GetComponent<NetworkIdentity>().netId));
            }
            else
            {
                visualRoot = existingVisual;
            }
            spriteAnimator = visualRoot != null ? visualRoot.GetComponent<PlayerAvatarAnimator>() : null;

            input.enabled = false;
            body.gravityScale = config.gravityScale;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public override void OnStartServer()
        {
            body.simulated = true;
            int connectionId = connectionToClient != null ? connectionToClient.connectionId : 0;
            displayName = $"SURVIVOR-{connectionId + 1:00}";
            invulnerableUntil = NetworkTime.time + 1.25d;
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
            Local = this;
            input.enabled = true;
            CmdConfigureProfile(PlayerProfileSelection.RequestedName, PlayerProfileSelection.RequestedAvatar);
            Camera camera = Camera.main;
            if (camera != null && camera.TryGetComponent(out SideCameraRig rig))
            {
                rig.Initialize(transform, SideWorldBuilder.MapBounds);
            }
        }

        public override void OnStopLocalPlayer()
        {
            if (Local == this)
            {
                Local = null;
            }
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

            if (isServer && !defeated && transform.position.y < SideWorldBuilder.MapBounds.min.y - 4f)
            {
                ServerDefeat(null);
            }
        }

        private void FixedUpdate()
        {
            if (!isServer || config == null)
            {
                return;
            }

            if (defeated)
            {
                body.linearVelocity = Vector2.zero;
                serverHorizontal = 0f;
                if (NetworkTime.time >= respawnAt)
                {
                    ServerRespawn();
                }
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
            if (defeated)
            {
                return;
            }
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
            if (defeated)
            {
                serverHorizontal = 0f;
                serverJumpHeld = false;
                return;
            }
            serverHorizontal = Mathf.Clamp(horizontal, -1f, 1f);
            serverJumpHeld = jumpHeld;
        }

        [Command]
        private void CmdConfigureProfile(string requestedName, PlayerAvatarKind requestedAvatar)
        {
            string fallback = displayName;
            displayName = PlayerProfileSelection.SanitizeName(requestedName, fallback);
            avatarKind = PlayerProfileSelection.SanitizeAvatar(requestedAvatar);
        }

        [Command]
        private void CmdRequestJump()
        {
            if (defeated)
            {
                return;
            }
            serverJumpBufferRemaining = config.jumpBuffer;
        }

        [Command]
        private void CmdRequestAttack()
        {
            if (defeated || Time.time < actionLockedUntil)
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
            Vector2 bodyCenter = ActorGeometry2D.BodyCenter(bodyCollider);
            Vector2 center = new Vector2(bodyCenter.x + facing * 0.82f, bodyCenter.y);
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, new Vector2(1.45f, 1.25f), 0f);
            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out SideScrollerNetworkZombie zombie))
                {
                    zombie.ApplyDamage(30, this);
                    continue;
                }
                if (hit.TryGetComponent(out SideScrollerNetworkSlime slime))
                {
                    slime.ApplyDamage(30, this);
                    continue;
                }
                if (hit.TryGetComponent(out SideScrollerNetworkPlayer player) && player != this)
                {
                    player.ApplyDamage(25, this);
                }
            }
        }

        [Server]
        public void ApplyDamage(int damage, SideScrollerNetworkPlayer attacker)
        {
            if (defeated || NetworkTime.time < invulnerableUntil)
            {
                return;
            }

            health = Mathf.Max(0, health - Mathf.Max(1, damage));
            if (health > 0)
            {
                motion = 5;
                actionLockedUntil = Mathf.Max(actionLockedUntil, Time.time + 0.14f);
                return;
            }

            ServerDefeat(attacker);
        }

        [Server]
        public void AwardMonsterDefeat(int experienceReward)
        {
            experience += Mathf.Max(0, experienceReward);
        }

        [Server]
        private void ServerDefeat(SideScrollerNetworkPlayer attacker)
        {
            if (defeated)
            {
                return;
            }

            health = 0;
            defeated = true;
            deaths++;
            motion = 6;
            serverHorizontal = 0f;
            serverJumpHeld = false;
            body.linearVelocity = Vector2.zero;
            bodyCollider.enabled = false;
            respawnAt = NetworkTime.time + 2.4d;
            if (attacker != null && attacker != this)
            {
                attacker.kills++;
                attacker.experience += 35;
            }
        }

        [Server]
        private void ServerRespawn()
        {
            int spawnIndex = connectionToClient != null ? Mathf.Abs(connectionToClient.connectionId) % 4 : 0;
            transform.position = SideWorldBuilder.PlayerSpawn + new Vector3(spawnIndex * SideWorldBuilder.PlayerSpawnSpacing, 0f, 0f);
            body.linearVelocity = Vector2.zero;
            bodyCollider.enabled = true;
            health = MaxHealthValue;
            defeated = false;
            motion = 0;
            invulnerableUntil = NetworkTime.time + 1.25d;
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

        private void OnAvatarChanged(PlayerAvatarKind previous, PlayerAvatarKind next)
        {
            if (Utils.IsHeadless())
            {
                return;
            }

            if (visualRoot != null)
            {
                Destroy(visualRoot.gameObject);
            }
            visualRoot = RuntimePlayerVisual.CreatePlayer(transform, next, ColorForObject(netId));
            spriteAnimator = visualRoot.GetComponent<PlayerAvatarAnimator>();
        }

        private bool ProbeGround()
        {
            return GroundProbe2D.Check(bodyCollider, config.groundProbeWidth, config.groundProbeDistance, ~0);
        }

        private void OnGUI()
        {
            if (!isClient || Utils.IsHeadless() || isLocalPlayer)
            {
                return;
            }

            string label = defeated ? $"{displayName}  RESPAWN" : displayName;
            Color color = isLocalPlayer ? new Color(0.34f, 0.9f, 0.66f) : new Color(0.95f, 0.68f, 0.25f);
            PixelHudDrawing.WorldBar(ActorGeometry2D.HeadWorldPosition(bodyCollider, 0.18f), label, health / (float)MaxHealthValue, color, 96f);
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
