using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Network;
using Mirror;
using UnityEngine;

namespace EasyGame.SideScroller.Enemies
{
    [RequireComponent(typeof(NetworkIdentity), typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class SideScrollerNetworkZombie : NetworkBehaviour
    {
        [SyncVar] private int health = 60;
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
                    health = 60;
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
            motion = 1;
        }

        [Server]
        public void ApplyDamage(int damage)
        {
            if (defeated)
            {
                return;
            }

            health = Mathf.Max(0, health - Mathf.Max(1, damage));
            if (health > 0)
            {
                motion = 5;
                return;
            }

            defeated = true;
            motion = 6;
            bodyCollider.enabled = false;
            body.linearVelocity = Vector2.zero;
            reviveAt = NetworkTime.time + 2.2d;
        }

        private bool HasGroundAhead()
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 origin = new Vector2(bounds.center.x + facing * (bounds.extents.x + 0.12f), bounds.min.y + 0.08f);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 0.35f, ~0);
            return hit.collider != null && hit.collider != bodyCollider;
        }
    }
}
