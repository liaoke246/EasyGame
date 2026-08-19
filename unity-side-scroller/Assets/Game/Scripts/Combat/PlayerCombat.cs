using EasyGame.SideScroller.Player;
using UnityEngine;

namespace EasyGame.SideScroller.Combat
{
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerAnimation))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float attackCooldown = 0.3f;
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

            nextAttackAt = Time.time + attackCooldown;
            playerAnimation.PlayAttack();
        }
    }
}
