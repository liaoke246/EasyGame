using EasyGame.SideScroller.Player;
using UnityEngine;

namespace EasyGame.SideScroller.Combat
{
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerAnimation))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        private PlayerInputReader input;
        private PlayerAnimation playerAnimation;
        private PlayerMovement movement;
        private Collider2D bodyCollider;
        private CombatActionView2D view;
        private readonly CombatActionClock clock = new CombatActionClock();
        private int facing = 1;

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            playerAnimation = GetComponent<PlayerAnimation>();
            movement = GetComponent<PlayerMovement>();
            bodyCollider = GetComponent<Collider2D>();
            view = CombatActionView2D.Create(transform);
        }

        private void Update()
        {
            if (!clock.Busy(Time.time) && Mathf.Abs(input.Horizontal) > .05f) facing = input.Horizontal < 0f ? -1 : 1;
            int skill = input.ConsumeSkillPressed();
            bool attack = input.ConsumeAttackPressed();
            int requested = skill > 0 ? skill : attack ? 0 : -1;
            if (requested >= 0) clock.TryBegin(requested, facing, movement.IsGrounded, Time.time);
            bool busy = clock.Busy(Time.time);
            movement.ActionMovementScale = busy ? .2f : 1f;
            if (busy)
            {
                float elapsed = (float)(Time.time - clock.StartedAt);
                playerAnimation.PlayCombatAction(clock.Action, elapsed, clock.Facing);
                view.Show(clock.Action, clock.Facing, elapsed, bodyCollider, Core.PlayerProfileSelection.RequestedAvatar);
            }
            SkillHudBridge.Publish(Mathf.Max(0f, (float)(clock.ReadyAt(1) - Time.time)),
                Mathf.Max(0f, (float)(clock.ReadyAt(2) - Time.time)), Mathf.Max(0f, (float)(clock.ReadyAt(3) - Time.time)), false);
        }
    }
}
