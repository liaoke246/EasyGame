using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Combat;
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
        [SyncVar] private float horizontalSpeed;
        [SyncVar] private bool defeated;

        public static SideScrollerNetworkPlayer Local { get; private set; }
        public string DisplayName => displayName;
        public int Health => health;
        public int MaxHealth => MaxHealthValue;
        public int Experience => experience;
        public int Level => level;
        public int ExperienceToNextLevel => progression.ExperienceForLevel(level);
        public bool IsMaxLevel => level >= progression.MaxLevel;
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
        private Player.PlayerMotor2D motor;
        private readonly ServerPlayerInput serverInput = new ServerPlayerInput();
        private readonly CombatQuery2D combatQuery = new CombatQuery2D();
        private LevelProgressionConfig progression;
        private SideScrollerNetworkTransform networkTransform;
        private uint inputSequence;
        private bool profileConfigured;
        private bool attackQueued;
        private double attackHitsAt = double.PositiveInfinity;
        private double nextAttackAt;
        private float attackDirection = 1f;
        private float presentationAttackUntil;
        private int presentationAttackFacing = 1;
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
            progression = Resources.Load<LevelProgressionConfig>("Config/LevelProgression");
            if (progression == null) progression = ScriptableObject.CreateInstance<LevelProgressionConfig>();
            motor = new Player.PlayerMotor2D(body, bodyCollider, config, ~0);
            networkTransform = GetComponent<SideScrollerNetworkTransform>();

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
            motor.Reset();
            int connectionId = connectionToClient != null ? connectionToClient.connectionId : 0;
            displayName = $"SURVIVOR-{connectionId + 1:00}";
            invulnerableUntil = NetworkTime.time + 1.25d;
        }

        public override void OnStartClient()
        {
            ConfigureAvatarCollider();
            if (!isServer)
            {
                body.simulated = false;
            }
            RefreshVisuals();
            gameObject.name = displayName;
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

            if (isServer && !defeated && body.position.y < SideWorldBuilder.MapBounds.min.y - 4f)
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
                serverInput.ClearIntent();
                if (NetworkTime.time >= respawnAt)
                {
                    ServerRespawn();
                }
                return;
            }

            serverInput.Expire(NetworkTime.time);
            motor.Step(serverInput.Horizontal, serverInput.JumpHeld, Time.fixedDeltaTime);
            Vector2 velocity = body.linearVelocity;
            horizontalSpeed = Mathf.Round(Mathf.Abs(velocity.x) * 20f) / 20f;
            if (Mathf.Abs(serverInput.Horizontal) > 0.05f && Time.time >= actionLockedUntil)
            {
                facing = serverInput.Horizontal > 0f ? 1 : -1;
            }

            if (attackQueued)
            {
                attackQueued = false;
                BeginAttack();
            }
            if (NetworkTime.time >= attackHitsAt)
            {
                attackHitsAt = double.PositiveInfinity;
                ResolveAttackHits();
            }

            if (Time.time >= actionLockedUntil)
            {
                motion = !motor.IsGrounded ? (velocity.y >= 0f ? 2 : 3) : (Mathf.Abs(velocity.x) > 0.15f ? 1 : 0);
            }
        }

        private void SendLocalInput()
        {
            if (defeated)
            {
                input.ConsumeJumpPressed();
                input.ConsumeJumpReleased();
                input.ConsumeAttackPressed();
                return;
            }
            if (NetworkTime.localTime >= nextInputAt)
            {
                nextInputAt = NetworkTime.localTime + 1d / 30d;
                CmdSetMovement(input.Horizontal, input.JumpHeld, ++inputSequence);
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
        private void CmdSetMovement(float horizontal, bool jumpHeld, uint sequence)
        {
            if (defeated)
            {
                serverInput.ClearIntent();
                return;
            }
            serverInput.Receive(horizontal, jumpHeld, sequence, NetworkTime.time);
        }

        [Command]
        private void CmdConfigureProfile(string requestedName, PlayerAvatarKind requestedAvatar)
        {
            if (profileConfigured) return;
            profileConfigured = true;
            string fallback = displayName;
            displayName = PlayerProfileSelection.SanitizeName(requestedName, fallback);
            avatarKind = PlayerProfileSelection.SanitizeAvatar(requestedAvatar);
            ConfigureAvatarCollider();
            gameObject.name = displayName;
        }

        [Command]
        private void CmdRequestJump()
        {
            if (defeated)
            {
                return;
            }
            motor.QueueJump();
        }

        [Command]
        private void CmdRequestAttack()
        {
            if (!defeated && NetworkTime.time >= nextAttackAt && Time.time >= actionLockedUntil)
                attackQueued = true;
        }

        [Server]
        private void BeginAttack()
        {
            if (NetworkTime.time < nextAttackAt || Time.time < actionLockedUntil) return;
            actionLockedUntil = Time.time + CombatTiming2D.AttackDuration;
            nextAttackAt = NetworkTime.time + CombatTiming2D.AttackCooldown;
            attackHitsAt = NetworkTime.time + 0.085d;
            attackDirection = facing;
            motion = 4;
            RpcPlayAttack(facing);
        }

        [Server]
        private void ResolveAttackHits()
        {
            Vector2 bodyCenter = ActorGeometry2D.BodyCenter(bodyCollider);
            Vector2 center = new Vector2(bodyCenter.x + attackDirection * 0.82f, bodyCenter.y);
            var hits = combatQuery.CollectTargets(bodyCollider, center, new Vector2(1.45f, 1.25f));
            for (int index = 0; index < hits.Count; index++)
            {
                Collider2D hit = hits[index];
                SideScrollerNetworkZombie zombie = hit.GetComponentInParent<SideScrollerNetworkZombie>();
                if (zombie != null)
                {
                    zombie.ApplyDamage(30, this);
                    continue;
                }
                SideScrollerNetworkSlime slime = hit.GetComponentInParent<SideScrollerNetworkSlime>();
                if (slime != null)
                {
                    slime.ApplyDamage(30, this);
                    continue;
                }
                SideScrollerNetworkPlayer player = hit.GetComponentInParent<SideScrollerNetworkPlayer>();
                if (player != null && player != this)
                {
                    player.ApplyDamage(25, this);
                }
            }
        }

        [Server]
        public void ApplyDamage(int damage, SideScrollerNetworkPlayer attacker)
        {
            if (damage <= 0 || defeated || NetworkTime.time < invulnerableUntil)
            {
                return;
            }

            health = Mathf.Max(0, health - Mathf.Max(1, damage));
            attackHitsAt = double.PositiveInfinity;
            attackQueued = false;
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
            progression.AddExperience(ref level, ref experience, experienceReward);
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
            serverInput.ClearIntent();
            motor.Reset();
            attackQueued = false;
            attackHitsAt = double.PositiveInfinity;
            horizontalSpeed = 0f;
            body.linearVelocity = Vector2.zero;
            bodyCollider.enabled = false;
            body.simulated = false;
            respawnAt = NetworkTime.time + 2.4d;
            if (attacker != null && attacker != this)
            {
                attacker.kills++;
                attacker.AwardMonsterDefeat(35);
            }
        }

        [Server]
        private void ServerRespawn()
        {
            Transform start = NetworkManager.singleton != null ? NetworkManager.singleton.GetStartPosition() : null;
            Vector3 destination = start != null ? start.position : SideWorldBuilder.PlayerSpawn;
            body.position = destination;
            if (networkTransform != null) networkTransform.ServerTeleport(destination, Quaternion.identity);
            else transform.position = destination;
            body.linearVelocity = Vector2.zero;
            bodyCollider.enabled = true;
            body.simulated = true;
            serverInput.ClearIntent();
            motor.Reset();
            actionLockedUntil = 0f;
            nextAttackAt = NetworkTime.time;
            health = MaxHealthValue;
            defeated = false;
            motion = 0;
            invulnerableUntil = NetworkTime.time + 1.25d;
        }

        [ClientRpc]
        private void RpcPlayAttack(int attackFacing)
        {
            if (spriteAnimator != null)
            {
                presentationAttackFacing = attackFacing;
                spriteAnimator.PlayAttack(attackFacing);
                presentationAttackUntil = Time.time + CombatTiming2D.AttackDuration;
            }
        }

        private void RefreshVisuals()
        {
            if (spriteAnimator != null)
            {
                int visibleMotion = Time.time < presentationAttackUntil && motion < 5 ? 4 : motion;
                int visibleFacing = visibleMotion == 4 && Time.time < presentationAttackUntil ? presentationAttackFacing : facing;
                spriteAnimator.SetState(visibleMotion, visibleFacing, horizontalSpeed);
            }
            else if (animator != null)
            {
                animator.SetInteger("Motion", motion);
                animator.SetFloat("Speed", horizontalSpeed);
                animator.SetFloat("VerticalSpeed", body.linearVelocity.y);
            }
            if (visualRoot != null && spriteAnimator == null)
            {
                Vector3 scale = visualRoot.localScale;
                scale.x = Mathf.Abs(scale.x) * facing;
                visualRoot.localScale = scale;
            }
        }

        private void OnAvatarChanged(PlayerAvatarKind previous, PlayerAvatarKind next)
        {
            ConfigureAvatarCollider();
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

        private void ConfigureAvatarCollider()
        {
            if (bodyCollider is CapsuleCollider2D capsule)
                ActorGeometry2D.ConfigurePlayerAvatar(capsule, avatarKind);
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
