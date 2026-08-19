using UnityEngine;

namespace EasyGame.SideScroller.Player
{
    [RequireComponent(typeof(Animator), typeof(PlayerMovement))]
    public sealed class PlayerAnimation : MonoBehaviour
    {
        private static readonly int MotionId = Animator.StringToHash("Motion");
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");

        private Animator animator;
        private PlayerMovement movement;
        private Transform visualRoot;
        private float actionLockedUntil;
        private int actionMotion;
        private float facing = 1f;

        public void Initialize(Animator targetAnimator, PlayerMovement targetMovement, Transform targetVisualRoot)
        {
            animator = targetAnimator;
            movement = targetMovement;
            visualRoot = targetVisualRoot;
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            movement = GetComponent<PlayerMovement>();
        }

        private void Update()
        {
            if (animator == null || movement == null)
            {
                return;
            }

            float horizontal = movement.HorizontalInput;
            if (Mathf.Abs(horizontal) > 0.05f)
            {
                facing = Mathf.Sign(horizontal);
            }

            if (visualRoot != null)
            {
                Vector3 scale = visualRoot.localScale;
                scale.x = Mathf.Abs(scale.x) * facing;
                visualRoot.localScale = scale;
            }

            int motion;
            if (Time.time < actionLockedUntil)
            {
                motion = actionMotion;
            }
            else if (!movement.IsGrounded)
            {
                motion = movement.Velocity.y >= 0f ? 2 : 3;
            }
            else
            {
                motion = Mathf.Abs(movement.Velocity.x) > 0.15f ? 1 : 0;
            }

            animator.SetInteger(MotionId, motion);
            animator.SetFloat(SpeedId, Mathf.Abs(movement.Velocity.x));
            animator.SetFloat(VerticalSpeedId, movement.Velocity.y);
        }

        public void PlayAttack(float duration = 0.22f)
        {
            actionMotion = 4;
            actionLockedUntil = Mathf.Max(actionLockedUntil, Time.time + duration);
        }

        public void PlayHit(float duration = 0.18f)
        {
            actionMotion = 5;
            actionLockedUntil = Mathf.Max(actionLockedUntil, Time.time + duration);
        }

        public void PlayDeath(float duration = 1f)
        {
            actionMotion = 6;
            actionLockedUntil = Mathf.Max(actionLockedUntil, Time.time + duration);
        }
    }
}
