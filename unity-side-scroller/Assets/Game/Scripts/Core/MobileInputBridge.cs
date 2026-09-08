using System;
using UnityEngine;

namespace EasyGame.SideScroller.Core
{
    public sealed class MobileInputBridge : MonoBehaviour
    {
        public static bool LeftHeld { get; private set; }
        public static bool RightHeld { get; private set; }
        public static bool JumpHeld { get; private set; }

        private static bool jumpPressed;
        private static bool attackPressed;
        private static bool attackHeld;
        private static int skillPressed = -1;
        private static readonly bool[] skillHeld = new bool[3];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetState()
        {
            LeftHeld = false;
            RightHeld = false;
            JumpHeld = false;
            attackHeld = false;
            jumpPressed = false;
            attackPressed = false;
            skillPressed = -1;
            Array.Clear(skillHeld, 0, skillHeld.Length);
        }

        public void SetMobileControl(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string[] parts = message.Split(new[] { ':' }, 2);
            if (parts.Length != 2)
            {
                return;
            }

            bool pressed = parts[1] == "1" || parts[1].Equals("true", StringComparison.OrdinalIgnoreCase);
            switch (parts[0].ToLowerInvariant())
            {
                case "left":
                    LeftHeld = pressed;
                    break;
                case "right":
                    RightHeld = pressed;
                    break;
                case "jump":
                    if (pressed && !JumpHeld)
                    {
                        jumpPressed = true;
                    }
                    JumpHeld = pressed;
                    break;
                case "attack":
                    if (pressed && !attackHeld)
                    {
                        attackPressed = true;
                    }
                    attackHeld = pressed;
                    break;
                case "reset":
                    ResetState();
                    break;
                case "skill1":
                case "skill2":
                case "skill3":
                    int index = parts[0][5] - '1';
                    if (pressed && !skillHeld[index]) skillPressed = index + 1;
                    skillHeld[index] = pressed;
                    break;
            }
        }

        public static bool ConsumeJumpPressed()
        {
            bool value = jumpPressed;
            jumpPressed = false;
            return value;
        }

        public static bool ConsumeAttackPressed()
        {
            bool value = attackPressed;
            attackPressed = false;
            return value;
        }

        public static int ConsumeSkillPressed()
        {
            int value = skillPressed;
            skillPressed = -1;
            return value;
        }

        private void OnDisable()
        {
            ResetState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ResetState();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) ResetState();
        }
    }
}
