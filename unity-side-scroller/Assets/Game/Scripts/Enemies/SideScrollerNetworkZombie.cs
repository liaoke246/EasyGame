using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Network;
using EasyGame.SideScroller.UI;
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
        [SyncVar] private bool defeated;

        [SerializeField] private float patrolRadius = 3.5f;
        [SerializeField] private float moveSpeed = 1.35f;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private Transform visualRoot;
        private PixelCharacterAnimator spriteAnimator;
        private Vector3 spawnPosition;
        private double reviveAt;
        private double staggerUntil;
        private double nextContactDamageAt;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
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
                spriteAnimator.SetState(motion, facing, Mathf.Abs(body.linearVelocity.x));
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
                body.linearVelocity = Vector2.zero;
                if (NetworkTime.time >= reviveAt)
                {
                    transform.position = spawnPosition;
                    health = MaxHealth;
                    defeated = false;
                    bodyCollider.enabled = true;
                    motion = 1;
                }
                return;
            }

            if (Mathf.Abs(transform.position.x - spawnPosition.x) >= patrolRadius || !HasGroundAhead())
            {
                facing *= -1;
            }

            Vector2 velocity = body.linearVelocity;
            velocity.x = facing * moveSpeed;
            body.linearVelocity = velocity;
            if (NetworkTime.time >= staggerUntil)
            {
                motion = 1;
            }
            DealContactDamage();
        }

        [Server]
        public void ApplyDamage(int damage, SideScrollerNetworkPlayer attacker)
        {
            if (defeated)
            {
                return;
            }

            health = Mathf.Max(0, health - Mathf.Max(1, damage));
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
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, new Vector2(0.95f, 1.25f), 0f);
            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out SideScrollerNetworkPlayer player) && !player.IsDefeated)
                {
                    player.ApplyDamage(12, null);
                    nextContactDamageAt = NetworkTime.time + 0.85d;
                }
            }
        }

        private bool HasGroundAhead()
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 point = new Vector2(bounds.center.x + facing * (bounds.extents.x + 0.14f), bounds.min.y - 0.08f);
            Collider2D[] hits = Physics2D.OverlapCircleAll(point, 0.16f);
            foreach (Collider2D hit in hits)
            {
                if (hit != null && !hit.isTrigger && hit != bodyCollider && hit.attachedRigidbody != body)
                {
                    return true;
                }
            }
            return false;
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
