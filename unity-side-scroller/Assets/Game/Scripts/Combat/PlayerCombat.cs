using EasyGame.SideScroller.Player;
using UnityEngine;

namespace EasyGame.SideScroller.Combat
{
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerAnimation))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        [SerializeField, Min(CombatTiming2D.AttackCooldown)] private float attackCooldown = CombatTiming2D.AttackCooldown;
        private PlayerInputReader input;
        private PlayerAnimation playerAnimation;
        private float nextAttackAt;

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            playerAnimation = GetComponent<PlayerAnimation>();
        }

        private void Update()
        {
            if (!input.ConsumeAttackPressed() || Time.time < nextAttackAt)
            {
                return;
            }

            nextAttackAt = Time.time + Mathf.Max(CombatTiming2D.AttackCooldown, attackCooldown);
            playerAnimation.PlayAttack(CombatTiming2D.AttackDuration);
        }
    }
}
