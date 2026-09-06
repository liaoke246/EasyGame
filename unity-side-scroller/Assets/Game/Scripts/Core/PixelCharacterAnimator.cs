using System.Collections.Generic;
using UnityEngine;

namespace EasyGame.SideScroller.Core
{
    /// <summary>
    /// Plays the approved 80x64 GandalfHardcore warrior frames with a single
    /// SpriteRenderer. Each gameplay state keeps the same silhouette and pivot.
    /// </summary>
    public sealed class PixelCharacterAnimator : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite[]> SequenceCache = new Dictionary<string, Sprite[]>();
        private SpriteRenderer target;
        private string characterFolder;
        private string filePrefix;
        private Sprite[] idle;
        private Sprite[] walk;
        private Sprite[] run;
        private Sprite[] jump;
        private Sprite[] fall;
        private Sprite[] attack;
        private Sprite[] death;
        private Color baseTint = Color.white;
        private int motion = -1;
        private float facing = 1f;
        private float speed;
        private float stateStartedAt;
        private float movementPhase;

        public void Initialize(SpriteRenderer targetRenderer, Color tint, string characterFolderName = "Warrior", string spritePrefix = "warrior")
        {
            target = targetRenderer;
            baseTint = tint;
            characterFolder = $"ThirdParty/GandalfHardcore/Characters/{characterFolderName}";
            filePrefix = spritePrefix;
            idle = LoadSequence("idle", 5);
            walk = LoadSequence("walk", 8);
            run = LoadSequence("run", 8);
            jump = LoadSequence("jump", 4);
            fall = LoadSequence("fall", 4);
            attack = LoadSequence("attack", 6);
            death = LoadSequence("death", 10);
            SetState(0, 1f, 0f);
            ApplyFrame();
        }

        public void SetState(int nextMotion, float nextFacing, float nextSpeed, bool restart = false)
        {
            if (Mathf.Abs(nextFacing) > 0.01f)
            {
                facing = Mathf.Sign(nextFacing);
            }
            speed = Mathf.Max(0f, nextSpeed);
            if (motion != nextMotion || restart)
            {
                motion = nextMotion;
                stateStartedAt = Time.time;
            }
        }

        private void LateUpdate()
        {
            if (motion == 1)
            {
                // Integrate the cycle instead of dividing absolute state age by
                // a changing interval, which jumped between feet while slowing.
                movementPhase += Time.deltaTime * Mathf.Lerp(0.55f, 1.6f, Mathf.InverseLerp(0f, 8f, speed));
                movementPhase %= 0.8f;
            }
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (target == null)
            {
                return;
            }

            float elapsed = Time.time - stateStartedAt;
            target.sprite = FrameForMotion(elapsed);
            target.color = motion == 5 && Mathf.FloorToInt(elapsed / 0.055f) % 2 == 0
                ? Color.Lerp(baseTint, Color.white, 0.72f)
                : baseTint;
            target.transform.localPosition = motion == 5
                ? new Vector3(-0.06f * Mathf.Sin(elapsed * 55f), 0f, 0f)
                : Vector3.zero;
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = Vector3.one;

            // The source art faces left. Gameplay facing +1 means right.
            Vector3 rootScale = transform.localScale;
            rootScale.x = Mathf.Abs(rootScale.x) * -facing;
            rootScale.y = Mathf.Abs(rootScale.y);
            transform.localScale = rootScale;
        }

        private Sprite FrameForMotion(float elapsed)
        {
            switch (motion)
            {
                case 1:
                    Sprite[] movement = speed >= 4.5f ? run : walk;
                    return Loop(movement, movementPhase, 0.1f);
                case 2:
                    return Once(jump, elapsed, 0.075f);
                case 3:
                    return Once(fall, elapsed, 0.09f);
                case 4:
                    return Once(attack, elapsed, 0.047f);
                case 5:
                    return idle.Length > 0 ? idle[0] : null;
                case 6:
                    return Once(death, elapsed, 0.085f);
                default:
                    return Loop(idle, elapsed, 0.15f);
            }
        }

        private static Sprite Loop(Sprite[] frames, float elapsed, float interval)
        {
            if (frames == null || frames.Length == 0)
            {
                return null;
            }
            return frames[Mathf.FloorToInt(Mathf.Max(0f, elapsed) / interval) % frames.Length];
        }

        private static Sprite Once(Sprite[] frames, float elapsed, float interval)
        {
            if (frames == null || frames.Length == 0)
            {
                return null;
            }
            int index = Mathf.Clamp(Mathf.FloorToInt(elapsed / interval), 0, frames.Length - 1);
            return frames[index];
        }

        private Sprite[] LoadSequence(string name, int count)
        {
            string key = $"{characterFolder}/{filePrefix}_{name}";
            if (SequenceCache.TryGetValue(key, out Sprite[] cached))
            {
                return cached;
            }
            Sprite[] frames = new Sprite[count];
            for (int index = 0; index < count; index++)
            {
                frames[index] = Resources.Load<Sprite>($"{characterFolder}/{filePrefix}_{name}_{index}");
                if (frames[index] == null)
                {
                    Debug.LogError($"Missing licensed pixel character frame: {filePrefix}_{name}_{index}. Run scripts/install-side-scroller-art.ps1.");
                }
            }
            SequenceCache[key] = frames;
            return frames;
        }
    }
}
