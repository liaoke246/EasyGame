using System;
using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Data;
using UnityEngine;

namespace EasyGame.SideScroller.Player
{
    /// <summary>
    /// The fixed-step movement authority shared by offline and server players.
    /// Input edges are queued independently of rendering; only Step consumes them.
    /// Unity remains responsible for collision resolution and gravity integration.
    /// </summary>
    public sealed class PlayerMotor2D
    {
        private static PhysicsMaterial2D movementMaterial;
        private readonly Rigidbody2D body;
        private readonly Collider2D bodyCollider;
        private readonly PlayerMovementConfig config;
        private readonly int groundMask;
        private float coyoteRemaining;
        private float jumpBufferRemaining;

        public bool IsGrounded { get; private set; }
        public Vector2 Velocity => body.linearVelocity;

        public PlayerMotor2D(Rigidbody2D body, Collider2D bodyCollider, PlayerMovementConfig config, int groundMask = Physics2D.DefaultRaycastLayers)
        {
            this.body = body != null ? body : throw new ArgumentNullException(nameof(body));
            this.bodyCollider = bodyCollider != null ? bodyCollider : throw new ArgumentNullException(nameof(bodyCollider));
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
            this.groundMask = groundMask;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (movementMaterial == null)
            {
                movementMaterial = new PhysicsMaterial2D("Player Movement - No Friction")
                {
                    friction = 0f,
                    bounciness = 0f,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
            bodyCollider.sharedMaterial = movementMaterial;
            Reset();
        }

        public void QueueJump()
        {
            jumpBufferRemaining = config.jumpBuffer;
        }

        public void Reset()
        {
            coyoteRemaining = 0f;
            jumpBufferRemaining = 0f;
            IsGrounded = false;
            body.gravityScale = config.gravityScale;
        }

        public void Step(float horizontal, bool jumpHeld, float deltaTime)
        {
            if (deltaTime <= 0f || !body.simulated || !bodyCollider.enabled)
            {
                return;
            }

            if (float.IsNaN(horizontal) || float.IsInfinity(horizontal))
            {
                horizontal = 0f;
            }
            horizontal = Mathf.Clamp(horizontal, -1f, 1f);
            IsGrounded = GroundProbe2D.Check(bodyCollider, config.groundProbeWidth, config.groundProbeDistance, groundMask);
            coyoteRemaining = IsGrounded ? config.coyoteTime : Mathf.Max(0f, coyoteRemaining - deltaTime);

            Vector2 velocity = body.linearVelocity;
            float targetSpeed = horizontal * config.moveSpeed;
            float acceleration = Mathf.Abs(targetSpeed) > 0.01f ? config.groundAcceleration : config.groundDeceleration;
            if (!IsGrounded)
            {
                acceleration *= config.airControl;
            }
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, acceleration * deltaTime);

            // Test the buffer before decrementing: a press during the final
            // airborne frame must still fire on the first grounded physics step.
            if (jumpBufferRemaining > 0f && coyoteRemaining > 0f)
            {
                velocity.y = config.jumpVelocity;
                jumpBufferRemaining = 0f;
                coyoteRemaining = 0f;
                IsGrounded = false;
            }
            else
            {
                jumpBufferRemaining = Mathf.Max(0f, jumpBufferRemaining - deltaTime);
            }

            float gravityMultiplier = velocity.y < -0.01f ? config.fallGravityMultiplier
                : velocity.y > 0.01f && !jumpHeld ? config.lowJumpGravityMultiplier : 1f;
            body.gravityScale = config.gravityScale * gravityMultiplier;

            // Physics adds gravity after FixedUpdate. Include that upcoming
            // integration in the cap so actual falling speed stays within it.
            float gravityStep = Physics2D.gravity.y * body.gravityScale * deltaTime;
            velocity.y = Mathf.Max(velocity.y, -config.maxFallSpeed - Mathf.Min(0f, gravityStep));
            body.linearVelocity = velocity;
        }
    }
}
