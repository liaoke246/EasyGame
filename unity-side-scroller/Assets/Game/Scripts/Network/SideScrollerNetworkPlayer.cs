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
        [SyncVar] private int activeAction;
        [SyncVar] private double actionStartedAt;
        [SyncVar] private double cleaveReadyAt;
        [SyncVar] private double risingReadyAt;
        [SyncVar] private double novaReadyAt;

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
        public string ActionHint => Time.unscaledTime < hintUntil ? actionHint : string.Empty;
        public float SkillRemaining(int id) => Mathf.Max(0f, (float)((id == 1 ? cleaveReadyAt : id == 2 ? risingReadyAt : novaReadyAt) - NetworkTime.time));

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
        private int queuedAction = -1;
        private readonly CombatActionClock actionClock = new CombatActionClock();
        private CombatActionView2D actionView;
        private double staggerUntil;
        private string actionHint;
        private float hintUntil;
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
            if (!Utils.IsHeadless()) actionView = CombatActionView2D.Create(transform);

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
                SkillHudBridge.Publish(SkillRemaining(1), SkillRemaining(2), SkillRemaining(3), defeated);
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
            if (NetworkTime.time >= staggerUntil)
                motor.Step(serverInput.Horizontal * (actionClock.Busy(NetworkTime.time) ? .2f : 1f), serverInput.JumpHeld, Time.fixedDeltaTime);
            Vector2 velocity = body.linearVelocity;
            horizontalSpeed = Mathf.Round(Mathf.Abs(velocity.x) * 20f) / 20f;
            if (Mathf.Abs(serverInput.Horizontal) > 0.05f && Time.time >= actionLockedUntil)
            {
                facing = serverInput.Horizontal > 0f ? 1 : -1;
            }

            if (queuedAction >= 0)
            {
                int requested = queuedAction;
                queuedAction = -1;
                BeginAttack(requested);
            }
            if (actionClock.ConsumeHit(NetworkTime.time))
            {
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
                input.ConsumeSkillPressed();
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
            int skill = input.ConsumeSkillPressed();
            bool attack = input.ConsumeAttackPressed();
            if (skill >= 1)
            {
                CmdRequestSkill(skill);
            }
            else if (attack)
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
            if (defeated || Time.time < actionLockedUntil)
            {
                return;
            }
            motor.QueueJump();
        }

        [Command]
        private void CmdRequestAttack()
        {
            if (!defeated && queuedAction < 0 && Time.time >= actionLockedUntil)
                queuedAction = 0;
        }

        [Command]
        private void CmdRequestSkill(int id)
        {
            if (!defeated && id >= 1 && CombatActions2D.IsPlayerAction(id) && queuedAction < 0)
                queuedAction = id;
        }

        [Server]
        private void BeginAttack(int id)
        {
            if (!CombatActions2D.IsPlayerAction(id) || defeated) return;
            if (Time.time < actionLockedUntil || !actionClock.TryBegin(id, facing, motor.IsGrounded, NetworkTime.time))
            {
                if (id > 0 && connectionToClient != null)
                    TargetActionRejected(connectionToClient, NetworkTime.time < actionClock.ReadyAt(id) ? "COOLDOWN" :
                        CombatActions2D.Get(id).GroundOnly && !motor.IsGrounded ? "LAND TO CAST" : "RECOVERING");
                return;
            }
            activeAction = id;
            actionStartedAt = actionClock.StartedAt;
            actionLockedUntil = Time.time + CombatActions2D.Get(id).Duration;
            cleaveReadyAt = actionClock.ReadyAt(1);
            risingReadyAt = actionClock.ReadyAt(2);
            novaReadyAt = actionClock.ReadyAt(3);
            motion = 4;
        }

        [TargetRpc]
        private void TargetActionRejected(NetworkConnectionToClient target, string reason)
        {
            actionHint = reason;
            hintUntil = Time.unscaledTime + .9f;
        }

        [Server]
        private void ResolveAttackHits()
        {
            Vector2 bodyCenter = ActorGeometry2D.BodyCenter(bodyCollider);
            var definition = CombatActions2D.Get(activeAction);
            Vector2 center = definition.Center(bodyCenter, actionClock.Facing);
            var hits = combatQuery.CollectTargets(bodyCollider, center, definition.Size);
            for (int index = 0; index < hits.Count; index++)
            {
                Collider2D hit = hits[index];
                Vector2 impactPoint = hit.bounds.center;
                int knockDirection = activeAction == (int)CombatActionId.Nova ? (hit.bounds.center.x < bodyCenter.x ? -1 : 1) : actionClock.Facing;
                Vector2 impulse = new Vector2(knockDirection * definition.Impulse.x, definition.Impulse.y);
                SideScrollerNetworkZombie zombie = hit.GetComponentInParent<SideScrollerNetworkZombie>();
                if (zombie != null)
                {
                    int before = zombie.Health;
                    zombie.ApplyDamage(definition.Damage, this);
                    zombie.ApplyCombatImpulse(impulse);
                    if (zombie.Health < before) RpcCombatImpact(impactPoint, activeAction, knockDirection);
                    continue;
                }
                SideScrollerNetworkSlime slime = hit.GetComponentInParent<SideScrollerNetworkSlime>();
                if (slime != null)
                {
                    int before = slime.Health;
                    slime.ApplyDamage(definition.Damage, this);
                    slime.ApplyCombatImpulse(impulse);
                    if (slime.Health < before) RpcCombatImpact(impactPoint, activeAction, knockDirection);
                    continue;
                }
                SideScrollerNetworkPlayer player = hit.GetComponentInParent<SideScrollerNetworkPlayer>();
                if (player != null && player != this)
                {
                    int previousHealth = player.Health;
                    player.ApplyDamage(definition.PvpDamage, this);
                    if (player.Health < previousHealth) RpcCombatImpact(impactPoint, activeAction, knockDirection);
                    if (player.Health < previousHealth && !player.IsDefeated && impulse != Vector2.zero)
                    {
                        player.body.linearVelocity = new Vector2(impulse.x * .65f, Mathf.Max(player.body.linearVelocity.y, impulse.y * .75f));
                        player.staggerUntil = NetworkTime.time + .22d;
                    }
                }
            }
        }

        [ClientRpc]
        private void RpcCombatImpact(Vector2 point, int id, int direction)
        {
            actionView?.ConfirmImpact(point, id, direction, avatarKind);
        }

        [Server]
        public void ApplyDamage(int damage, SideScrollerNetworkPlayer attacker)
        {
            if (damage <= 0 || defeated || NetworkTime.time < invulnerableUntil)
            {
                return;
            }

            health = Mathf.Max(0, health - Mathf.Max(1, damage));
            actionClock.Cancel();
            queuedAction = -1;
            if (health > 0)
            {
                motion = 5;
                actionLockedUntil = Time.time + 0.16f;
                staggerUntil = NetworkTime.time + .16d;
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
            actionClock.Cancel();
            queuedAction = -1;
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
            actionClock.Reset();
            cleaveReadyAt = risingReadyAt = novaReadyAt = 0d;
            staggerUntil = 0d;
            health = MaxHealthValue;
            defeated = false;
            motion = 0;
            invulnerableUntil = NetworkTime.time + 1.25d;
        }

        private void RefreshVisuals()
        {
            if (spriteAnimator != null)
            {
                if (motion == 4)
                {
                    float elapsed = (float)(NetworkTime.time - actionStartedAt);
                    spriteAnimator.SetCombatAction(activeAction, elapsed, facing);
                    actionView?.Show(activeAction, facing, elapsed, bodyCollider, avatarKind);
                }
                else spriteAnimator.SetState(motion, facing, horizontalSpeed);
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
