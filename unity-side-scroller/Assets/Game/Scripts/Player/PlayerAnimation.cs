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
        private Core.PlayerAvatarAnimator spriteAnimator;
        private float actionLockedUntil;
        private int actionMotion;
        private float facing = 1f;
        private int combatAction = -1;
        private float combatElapsed;

        public void Initialize(Animator targetAnimator, PlayerMovement targetMovement, Transform targetVisualRoot)
        {
            animator = targetAnimator;
            movement = targetMovement;
            visualRoot = targetVisualRoot;
            spriteAnimator = visualRoot != null ? visualRoot.GetComponent<Core.PlayerAvatarAnimator>() : null;
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            movement = GetComponent<PlayerMovement>();
        }

        private void Update()
        {
            if (movement == null)
            {
                return;
            }

            float horizontal = movement.HorizontalInput;
            if (Mathf.Abs(horizontal) > 0.05f && Time.time >= actionLockedUntil)
            {
                facing = Mathf.Sign(horizontal);
            }

            if (visualRoot != null && spriteAnimator == null)
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

            if (spriteAnimator != null)
            {
                if (motion == 4 && combatAction >= 0) spriteAnimator.SetCombatAction(combatAction, combatElapsed, (int)facing);
                else spriteAnimator.SetState(motion, facing, Mathf.Abs(movement.Velocity.x));
            }
            else if (animator != null)
            {
                animator.SetInteger(MotionId, motion);
                animator.SetFloat(SpeedId, Mathf.Abs(movement.Velocity.x));
                animator.SetFloat(VerticalSpeedId, movement.Velocity.y);
            }
        }

        public void PlayAttack(float duration = 0.22f)
        {
            actionMotion = 4;
            actionLockedUntil = Mathf.Max(actionLockedUntil, Time.time + duration);
        }

        public void PlayCombatAction(int id, float elapsed, int direction)
        {
            combatAction = id; combatElapsed = elapsed; facing = direction;
            actionMotion = 4;
            actionLockedUntil = Time.time + Mathf.Max(0f, Combat.CombatActions2D.Get(id).Duration - elapsed);
            spriteAnimator?.SetCombatAction(id, elapsed, direction);
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
