using EasyGame.SideScroller.Core;
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
            CreatePrototypeSlash();
        }

        private void CreatePrototypeSlash()
        {
            GameObject slash = new GameObject("Prototype Slash");
            slash.transform.SetParent(transform, false);
            slash.transform.localPosition = new Vector3(0.72f, 0.05f, 0f);
            slash.transform.localScale = new Vector3(0.48f, 0.12f, 1f);
            SpriteRenderer renderer = slash.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.White;
            renderer.color = new Color(1f, 0.82f, 0.3f, 0.9f);
            renderer.sortingOrder = 12;
            Destroy(slash, 0.09f);
        }
    }
}
