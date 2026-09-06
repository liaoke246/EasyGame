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
        private static readonly string[] VariantNames = { "GREEN SLIME", "BLUE SLIME", "RED SLIME" };
        private static readonly string[] VariantAssets = { "green", "blue", "red" };
        private static readonly Color[] VariantColors = { new Color(0.3f, 0.85f, 0.4f), new Color(0.26f, 0.62f, 0.95f), new Color(0.95f, 0.3f, 0.25f) };

        [SyncVar] private int health = MaxHealth;
        [SyncVar] private int facing = 1;
        [SyncVar] private int motion;
        [SyncVar] private int colorVariant;
        [SyncVar] private bool defeated;

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
        private readonly CombatQuery2D contactQuery = new CombatQuery2D();

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
                spriteAnimator.SetState(motion, facing);
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

            if (!staggered)
            {
                DealContactDamage();
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
        private void DealContactDamage()
        {
            if (NetworkTime.time < nextContactDamageAt)
            {
                return;
            }

            var hits = contactQuery.CollectTargets(bodyCollider, ActorGeometry2D.BodyCenter(bodyCollider), new Vector2(0.9f, 0.72f));
            for (int index = 0; index < hits.Count; index++)
            {
                Collider2D hit = hits[index];
                SideScrollerNetworkPlayer player = hit.GetComponentInParent<SideScrollerNetworkPlayer>();
                if (player != null && !player.IsDefeated)
                {
                    player.ApplyDamage(9, null);
                    nextContactDamageAt = NetworkTime.time + 0.8d;
                }
            }
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
