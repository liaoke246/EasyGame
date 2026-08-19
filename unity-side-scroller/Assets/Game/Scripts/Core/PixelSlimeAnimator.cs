using UnityEngine;

namespace EasyGame.SideScroller.Core
{
    public sealed class PixelSlimeAnimator : MonoBehaviour
    {
        private const string SlimeFolder = "ThirdParty/GandalfHardcore/Enemies/Slime";

        private SpriteRenderer target;
        private Sprite[] idle;
        private Sprite[] jump;
        private Sprite[] death;
        private int motion = -1;
        private float facing = 1f;
        private float stateStartedAt;

        public void Initialize(SpriteRenderer targetRenderer, string color)
        {
            target = targetRenderer;
            idle = LoadSequence(color, "idle", 4);
            jump = LoadSequence(color, "jump", 8);
            death = LoadSequence(color, "death", 4);
            SetState(0, 1f);
            ApplyFrame();
        }

        public void SetState(int nextMotion, float nextFacing)
        {
            if (Mathf.Abs(nextFacing) > 0.01f)
            {
                facing = Mathf.Sign(nextFacing);
            }
            if (motion != nextMotion)
            {
                motion = nextMotion;
                stateStartedAt = Time.time;
            }
        }

        private void LateUpdate()
        {
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (target == null)
            {
                return;
            }

            float elapsed = Time.time - stateStartedAt;
            target.sprite = motion switch
            {
                1 => Once(jump, elapsed, 0.065f),
                3 => Once(death, elapsed, 0.095f),
                _ => Loop(idle, elapsed, 0.16f),
            };
            target.color = motion == 2 && Mathf.FloorToInt(elapsed / 0.055f) % 2 == 0
                ? new Color(1f, 0.72f, 0.72f)
                : Color.white;
            target.transform.localPosition = Vector3.zero;
            target.transform.localScale = Vector3.one;

            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * facing;
            scale.y = Mathf.Abs(scale.y);
            transform.localScale = scale;
        }

        private static Sprite Loop(Sprite[] frames, float elapsed, float interval)
        {
            return frames == null || frames.Length == 0 ? null : frames[Mathf.FloorToInt(elapsed / interval) % frames.Length];
        }

        private static Sprite Once(Sprite[] frames, float elapsed, float interval)
        {
            if (frames == null || frames.Length == 0)
            {
                return null;
            }
            return frames[Mathf.Clamp(Mathf.FloorToInt(elapsed / interval), 0, frames.Length - 1)];
        }

        private static Sprite[] LoadSequence(string color, string name, int count)
        {
            Sprite[] frames = new Sprite[count];
            for (int index = 0; index < count; index++)
            {
                frames[index] = Resources.Load<Sprite>($"{SlimeFolder}/slime-{color}_{name}_{index}");
                if (frames[index] == null)
                {
                    Debug.LogError($"Missing licensed slime frame: slime-{color}_{name}_{index}. Run scripts/install-side-scroller-art.ps1.");
                }
            }
            return frames;
        }
    }
}
