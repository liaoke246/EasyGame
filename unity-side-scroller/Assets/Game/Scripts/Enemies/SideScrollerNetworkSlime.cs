using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Network;
using EasyGame.SideScroller.UI;
using Mirror;
using UnityEngine;

namespace EasyGame.SideScroller.Enemies
{
    [RequireComponent(typeof(NetworkIdentity), typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class SideScrollerNetworkSlime : NetworkBehaviour
    {
        private const int MaxHealth = 45;

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
        private PixelSlimeAnimator spriteAnimator;
        private Vector3 spawnPosition;
        private double nextHopAt;
        private double nextContactDamageAt;
        private double staggerUntil;
        private double reviveAt;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
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
                string[] colors = { "green", "blue", "red" };
                Transform visual = RuntimePlayerVisual.CreateSlime(transform, colors[Mathf.Abs(colorVariant) % colors.Length]);
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
                body.linearVelocity = Vector2.zero;
                if (NetworkTime.time >= reviveAt)
                {
                    transform.position = spawnPosition;
                    health = MaxHealth;
                    defeated = false;
                    bodyCollider.enabled = true;
                    motion = 0;
                    nextHopAt = NetworkTime.time + 0.5d;
                }
                return;
            }

            bool grounded = GroundProbe2D.Check(bodyCollider, 0.72f, 0.12f, ~0);
            if (grounded && NetworkTime.time >= nextHopAt)
            {
                if (Mathf.Abs(transform.position.x - spawnPosition.x) >= patrolRadius || !HasGroundAhead())
                {
                    facing *= -1;
                }
                body.linearVelocity = new Vector2(facing * hopSpeed, hopVelocity);
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
                motion = 2;
                staggerUntil = NetworkTime.time + 0.16d;
                return;
            }

            defeated = true;
            motion = 3;
            bodyCollider.enabled = false;
            body.linearVelocity = Vector2.zero;
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

            Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position, new Vector2(0.9f, 0.72f), 0f);
            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out SideScrollerNetworkPlayer player) && !player.IsDefeated)
                {
                    player.ApplyDamage(9, null);
                    nextContactDamageAt = NetworkTime.time + 0.8d;
                }
            }
        }

        private bool HasGroundAhead()
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 point = new Vector2(bounds.center.x + facing * (bounds.extents.x + 0.18f), bounds.min.y - 0.08f);
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
            string[] names = { "GREEN SLIME", "BLUE SLIME", "RED SLIME" };
            Color[] colors = { new Color(0.3f, 0.85f, 0.4f), new Color(0.26f, 0.62f, 0.95f), new Color(0.95f, 0.3f, 0.25f) };
            int index = Mathf.Abs(colorVariant) % names.Length;
            PixelHudDrawing.WorldBar(transform.position + new Vector3(0f, 0.76f, 0f), names[index], health / (float)MaxHealth, colors[index], 70f);
        }
    }
}
