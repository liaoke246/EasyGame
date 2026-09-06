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
    public sealed class SideScrollerNetworkZombie : NetworkBehaviour
    {
        private const int MaxHealth = 60;

        [SyncVar] private int health = MaxHealth;
        [SyncVar] private int facing = -1;
        [SyncVar] private int motion = 1;
        [SyncVar] private float animationSpeed;
        [SyncVar] private bool defeated;

        [SerializeField] private float patrolRadius = 3.5f;
        [SerializeField] private float moveSpeed = 1.35f;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private SideScrollerNetworkTransform networkTransform;
        private Transform visualRoot;
        private PixelCharacterAnimator spriteAnimator;
        private Vector3 spawnPosition;
        private double reviveAt;
        private double staggerUntil;
        private double nextContactDamageAt;
        private readonly CombatQuery2D contactQuery = new CombatQuery2D();

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            networkTransform = GetComponent<SideScrollerNetworkTransform>();
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (!Utils.IsHeadless())
            {
                visualRoot = RuntimePlayerVisual.CreateZombie(transform);
                spriteAnimator = visualRoot.GetComponent<PixelCharacterAnimator>();
            }
        }

        public override void OnStartServer()
        {
            spawnPosition = transform.position;
            facing = netId % 2 == 0 ? 1 : -1;
            body.simulated = true;
        }

        public override void OnStartClient()
        {
            if (!isServer)
            {
                body.simulated = false;
            }
        }

        private void Update()
        {
            if (isClient && spriteAnimator != null)
            {
                spriteAnimator.SetState(motion, facing, animationSpeed);
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
                    motion = 1;
                }
                return;
            }

            if (body.position.y < SideWorldBuilder.MapBounds.min.y - 4f)
            {
                ApplyDamage(MaxHealth, null);
                return;
            }

            float patrolOffset = body.position.x - spawnPosition.x;
            if (patrolOffset >= patrolRadius)
            {
                facing = -1;
            }
            else if (patrolOffset <= -patrolRadius)
            {
                facing = 1;
            }

            bool grounded = GroundProbe2D.Check(bodyCollider, 0.72f, 0.12f, Physics2D.DefaultRaycastLayers);
            bool groundAhead = grounded && HasGroundAhead(facing);
            if (grounded && !groundAhead && HasGroundAhead(-facing))
            {
                facing *= -1;
                groundAhead = true;
            }

            Vector2 velocity = body.linearVelocity;
            bool canMove = groundAhead && NetworkTime.time >= staggerUntil;
            velocity.x = canMove ? facing * moveSpeed : 0f;
            body.linearVelocity = velocity;
            animationSpeed = Mathf.Abs(velocity.x);
            if (NetworkTime.time >= staggerUntil)
            {
                motion = !grounded ? (velocity.y > 0f ? 2 : 3) : canMove ? 1 : 0;
            }
            if (NetworkTime.time >= staggerUntil)
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
                motion = 5;
                staggerUntil = NetworkTime.time + 0.16d;
                return;
            }

            defeated = true;
            motion = 6;
            bodyCollider.enabled = false;
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            animationSpeed = 0f;
            reviveAt = NetworkTime.time + 2.2d;
            attacker?.AwardMonsterDefeat(20);
        }

        [Server]
        private void DealContactDamage()
        {
            if (NetworkTime.time < nextContactDamageAt)
            {
                return;
            }

            Vector2 bodyCenter = ActorGeometry2D.BodyCenter(bodyCollider);
            Vector2 center = new Vector2(bodyCenter.x + facing * 0.38f, bodyCenter.y);
            var hits = contactQuery.CollectTargets(bodyCollider, center, new Vector2(0.95f, 1.25f));
            for (int index = 0; index < hits.Count; index++)
            {
                Collider2D hit = hits[index];
                SideScrollerNetworkPlayer player = hit.GetComponentInParent<SideScrollerNetworkPlayer>();
                if (player != null && !player.IsDefeated)
                {
                    player.ApplyDamage(12, null);
                    nextContactDamageAt = NetworkTime.time + 0.85d;
                }
            }
        }

        private bool HasGroundAhead(int direction)
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 point = new Vector2(bounds.center.x + direction * (bounds.extents.x + 0.14f), bounds.min.y + 0.04f);
            return GroundProbe2D.HasSupportBelow(bodyCollider, point, 0.2f);
        }

        private void OnGUI()
        {
            if (!isClient || Utils.IsHeadless() || defeated)
            {
                return;
            }
            PixelHudDrawing.WorldBar(ActorGeometry2D.HeadWorldPosition(bodyCollider, 0.18f), "INFECTED", health / (float)MaxHealth, new Color(0.62f, 0.82f, 0.28f), 72f);
        }
    }
}
