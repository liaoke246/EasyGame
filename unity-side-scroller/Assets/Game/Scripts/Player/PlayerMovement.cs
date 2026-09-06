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
        private PlayerMotor2D motor;

        public bool IsGrounded => motor != null && motor.IsGrounded;
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
                motor = new PlayerMotor2D(body, bodyCollider, config, groundMask);
            }
        }

        private void Update()
        {
            if (config == null || input == null)
            {
                return;
            }

            if (input.ConsumeJumpPressed())
            {
                motor?.QueueJump();
            }

            input.ConsumeJumpReleased();
        }

        private void FixedUpdate()
        {
            if (config == null || input == null)
            {
                return;
            }

            motor?.Step(input.Horizontal, input.JumpHeld, Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            motor?.Reset();
        }
    }
}
