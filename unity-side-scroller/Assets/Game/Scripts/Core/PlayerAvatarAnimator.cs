using UnityEngine;

namespace EasyGame.SideScroller.Core
{
    /// <summary>
    /// Presentation adapter shared by all playable avatars. Gameplay code sends
    /// one motion vocabulary while each sprite family translates it locally.
    /// </summary>
    public sealed class PlayerAvatarAnimator : MonoBehaviour
    {
        private PixelCharacterAnimator characterAnimator;
        private PixelSlimeAnimator slimeAnimator;

        public void Initialize(SpriteRenderer renderer, PlayerAvatarKind avatar, Color tint)
        {
            switch (avatar)
            {
                case PlayerAvatarKind.Ranger:
                    characterAnimator = gameObject.AddComponent<PixelCharacterAnimator>();
                    characterAnimator.Initialize(renderer, tint, "Female", "female");
                    break;
                case PlayerAvatarKind.Slime:
                    slimeAnimator = gameObject.AddComponent<PixelSlimeAnimator>();
                    slimeAnimator.Initialize(renderer, "blue");
                    break;
                default:
                    characterAnimator = gameObject.AddComponent<PixelCharacterAnimator>();
                    characterAnimator.Initialize(renderer, tint);
                    break;
            }
        }

        public void SetState(int motion, float facing, float speed)
        {
            if (characterAnimator != null)
            {
                characterAnimator.SetState(motion, facing, speed);
            }
            if (slimeAnimator != null)
            {
                slimeAnimator.SetPlayerState(motion, facing);
            }
        }

        public void PlayAttack(float facing)
        {
            if (characterAnimator != null)
            {
                characterAnimator.SetState(4, facing, 0f, true);
            }
            if (slimeAnimator != null)
            {
                slimeAnimator.SetPlayerState(4, facing, true);
            }
        }

        public void SetCombatAction(int id, float elapsed, int facing)
        {
            characterAnimator?.SetCombatAction(id, elapsed, facing);
            slimeAnimator?.SetCombatAction(id, elapsed, facing);
        }
    }
}
