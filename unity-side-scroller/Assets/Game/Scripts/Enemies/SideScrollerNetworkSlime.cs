using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Combat;
using EasyGame.SideScroller.Network;
using EasyGame.SideScroller.UI;
using EasyGame.SideScroller.World;
using Mirror;
using UnityEngine;

namespace EasyGame.SideScroller.Enemies
{
    [RequireComponent(typeof(NetworkIdentity), typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class SideScrollerNetworkSlime : NetworkBehaviour
    {
        private const int MaxHealth = 45;
        public int Health => health;
        private static readonly string[] VariantNames = { "GREEN SLIME", "BLUE SLIME", "RED SLIME" };
        private static readonly string[] VariantAssets = { "green", "blue", "red" };
        private static readonly Color[] VariantColors = { new Color(0.3f, 0.85f, 0.4f), new Color(0.26f, 0.62f, 0.95f), new Color(0.95f, 0.3f, 0.25f) };

        [SyncVar] private int health = MaxHealth;
        [SyncVar] private int facing = 1;
        [SyncVar] private int motion;
        [SyncVar] private int colorVariant;
        [SyncVar] private bool defeated;
        [SyncVar] private double attackStartedAt;

        [SerializeField] private float patrolRadius = 3.2f;
        [SerializeField] private float hopSpeed = 2.45f;
        [SerializeField] private float hopVelocity = 6.2f;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private SideScrollerNetworkTransform networkTransform;
        private PixelSlimeAnimator spriteAnimator;
        private Vector3 spawnPosition;
        private double nextHopAt;
        private double nextContactDamageAt;
        private double staggerUntil;
        private double reviveAt;
        private readonly MonsterMelee2D melee = new MonsterMelee2D();
        private CombatActionView2D actionView;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            networkTransform = GetComponent<SideScrollerNetworkTransform>();
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public override void OnStartServer()
        {
            spawnPosition = transform.position;
            facing = netId % 2 == 0 ? 1 : -1;
            colorVariant = (int)(netId % 3);
            nextHopAt = NetworkTime.time + 0.35d + (netId % 5) * 0.11d;
            body.simulated = true;
        }

        public override void OnStartClient()
        {
            if (!Utils.IsHeadless())
            {
                Transform visual = RuntimePlayerVisual.CreateSlime(transform, VariantAssets[Mathf.Abs(colorVariant) % VariantAssets.Length]);
                spriteAnimator = visual.GetComponent<PixelSlimeAnimator>();
                actionView = CombatActionView2D.Create(transform);
            }
            if (!isServer)
            {
                body.simulated = false;
            }
        }

        private void Update()
        {
            if (isClient && spriteAnimator != null)
            {
                if (motion == 7)
                {
                    float elapsed = (float)(NetworkTime.time - attackStartedAt);
                    spriteAnimator.SetCombatAction((int)CombatActionId.SlimeSlam, elapsed, facing);
                    actionView.Show((int)CombatActionId.SlimeSlam, facing, elapsed, bodyCollider);
                }
                else spriteAnimator.SetState(motion, facing);
            }
        }

        private void FixedUpdate()
        {
            if (!isServer)
            {
                return;
            }

            if (defeated)
            {
                if (NetworkTime.time >= reviveAt)
                {
                    body.position = spawnPosition;
                    if (networkTransform != null) networkTransform.ServerTeleport(spawnPosition, Quaternion.identity);
                    else transform.position = spawnPosition;
                    health = MaxHealth;
                    defeated = false;
                    bodyCollider.enabled = true;
                    body.simulated = true;
                    body.linearVelocity = Vector2.zero;
                    staggerUntil = 0d;
                    nextContactDamageAt = NetworkTime.time + 0.5d;
                    melee.Clock.Reset();
                    motion = 0;
                    nextHopAt = NetworkTime.time + 0.5d;
                }
                return;
            }

            if (body.position.y < SideWorldBuilder.MapBounds.min.y - 4f)
            {
                ApplyDamage(MaxHealth, null);
                return;
            }

            bool grounded = GroundProbe2D.Check(bodyCollider, 0.72f, 0.12f, Physics2D.DefaultRaycastLayers);
            bool staggered = NetworkTime.time < staggerUntil;
            if (!staggered && NetworkTime.time >= nextContactDamageAt &&
                melee.Step((int)CombatActionId.SlimeSlam, bodyCollider, grounded, NetworkTime.time, ref facing))
            {
                motion = 7;
                attackStartedAt = melee.Clock.StartedAt;
                nextHopAt = NetworkTime.time + .6d;
                return;
            }
            if (melee.HasTarget && grounded && !staggered && NetworkTime.time >= nextContactDamageAt)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                motion = 0;
                nextHopAt = NetworkTime.time + .35d;
                return;
            }
            if (grounded && !staggered && NetworkTime.time >= nextHopAt)
            {
                float patrolOffset = body.position.x - spawnPosition.x;
                if (patrolOffset >= patrolRadius)
                {
                    facing = -1;
                }
                else if (patrolOffset <= -patrolRadius)
                {
                    facing = 1;
                }
                bool groundAhead = HasGroundAhead(facing);
                if (!groundAhead && HasGroundAhead(-facing))
                {
                    facing *= -1;
                    groundAhead = true;
                }
                body.linearVelocity = new Vector2(groundAhead ? facing * hopSpeed : 0f, hopVelocity);
                motion = 1;
                nextHopAt = NetworkTime.time + 1.15d + (netId % 4) * 0.12d;
            }
            else if (grounded)
            {
                Vector2 velocity = body.linearVelocity;
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, 18f * Time.fixedDeltaTime);
                body.linearVelocity = velocity;
                if (NetworkTime.time >= staggerUntil)
                {
                    motion = 0;
                }
            }
            else if (NetworkTime.time >= staggerUntil)
            {
                motion = 1;
            }

        }

        [Server]
        public void ApplyDamage(int damage, SideScrollerNetworkPlayer attacker)
        {
            if (defeated || damage <= 0)
            {
                return;
            }

            health = Mathf.Max(0, health - damage);
            melee.Clock.Cancel();
            if (health > 0)
            {
                motion = 2;
                staggerUntil = NetworkTime.time + 0.16d;
                return;
            }

            defeated = true;
            motion = 3;
            bodyCollider.enabled = false;
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            reviveAt = NetworkTime.time + 3.5d;
            attacker?.AwardMonsterDefeat(15);
        }

        [Server]
        public void ApplyCombatImpulse(Vector2 impulse)
        {
            if (defeated || impulse == Vector2.zero) return;
            body.linearVelocity = new Vector2(impulse.x, Mathf.Max(body.linearVelocity.y, impulse.y));
            staggerUntil = NetworkTime.time + .28d;
        }

        private bool HasGroundAhead(int direction)
        {
            Bounds bounds = bodyCollider.bounds;
            // Probe where the hop will land, not just the next few centimetres.
            float gravity = Mathf.Max(0.1f, Mathf.Abs(Physics2D.gravity.y * body.gravityScale));
            float landingDistance = Mathf.Max(bounds.extents.x + 0.18f, hopSpeed * (2f * hopVelocity / gravity) + bounds.extents.x);
            Vector2 point = new Vector2(bounds.center.x + direction * landingDistance, bounds.min.y + 0.04f);
            return GroundProbe2D.HasSupportBelow(bodyCollider, point, 0.2f);
        }

        private void OnGUI()
        {
            if (!isClient || Utils.IsHeadless() || defeated)
            {
                return;
            }
            int index = Mathf.Abs(colorVariant) % VariantNames.Length;
            PixelHudDrawing.WorldBar(ActorGeometry2D.HeadWorldPosition(bodyCollider, 0.16f), VariantNames[index], health / (float)MaxHealth, VariantColors[index], 70f);
        }
    }
}
