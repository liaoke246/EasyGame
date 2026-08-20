using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Data;
using UnityEngine;

namespace EasyGame.SideScroller.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(PlayerInputReader))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private PlayerMovementConfig config;
        [SerializeField] private LayerMask groundMask = ~0;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private PlayerInputReader input;
        private float coyoteRemaining;
        private float jumpBufferRemaining;

        public bool IsGrounded { get; private set; }
        public float HorizontalInput => input != null ? input.Horizontal : 0f;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

        public void Initialize(PlayerMovementConfig movementConfig, PlayerInputReader inputReader)
        {
            config = movementConfig;
            input = inputReader;
            ApplyConfiguration();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            input = GetComponent<PlayerInputReader>();
            ApplyConfiguration();
        }

        private void ApplyConfiguration()
        {
            if (body != null && config != null)
            {
                body.gravityScale = config.gravityScale;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
        }

        private void Update()
        {
            if (config == null || input == null)
            {
                return;
            }

            IsGrounded = ProbeGround();
            coyoteRemaining = IsGrounded ? config.coyoteTime : Mathf.Max(0f, coyoteRemaining - Time.deltaTime);

            if (input.ConsumeJumpPressed())
            {
                jumpBufferRemaining = config.jumpBuffer;
            }
            else
            {
                jumpBufferRemaining = Mathf.Max(0f, jumpBufferRemaining - Time.deltaTime);
            }

            input.ConsumeJumpReleased();
        }

        private void FixedUpdate()
        {
            if (config == null || input == null)
            {
                return;
            }

            Vector2 velocity = body.linearVelocity;
            float targetSpeed = input.Horizontal * config.moveSpeed;
            float acceleration = Mathf.Abs(targetSpeed) > 0.01f ? config.groundAcceleration : config.groundDeceleration;
            if (!IsGrounded)
            {
                acceleration *= config.airControl;
            }
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, acceleration * Time.fixedDeltaTime);

            if (jumpBufferRemaining > 0f && coyoteRemaining > 0f)
            {
                velocity.y = config.jumpVelocity;
                jumpBufferRemaining = 0f;
                coyoteRemaining = 0f;
                IsGrounded = false;
            }

            if (velocity.y < -0.01f)
            {
                velocity.y += Physics2D.gravity.y * body.gravityScale * (config.fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
            }
            else if (velocity.y > 0.01f && !input.JumpHeld)
            {
                velocity.y += Physics2D.gravity.y * body.gravityScale * (config.lowJumpGravityMultiplier - 1f) * Time.fixedDeltaTime;
            }

            velocity.y = Mathf.Max(velocity.y, -config.maxFallSpeed);
            body.linearVelocity = velocity;
        }

        private bool ProbeGround()
        {
            if (bodyCollider == null || config == null)
            {
                return false;
            }

            return GroundProbe2D.Check(bodyCollider, config.groundProbeWidth, config.groundProbeDistance, groundMask);
        }
    }
}
