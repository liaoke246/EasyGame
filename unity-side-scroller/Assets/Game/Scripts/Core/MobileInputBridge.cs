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
                    if (pressed)
                    {
                        attackPressed = true;
                    }
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

        private void OnDisable()
        {
            LeftHeld = false;
            RightHeld = false;
            JumpHeld = false;
            jumpPressed = false;
            attackPressed = false;
        }
    }
}
