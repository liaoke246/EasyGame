using EasyGame.SideScroller.Core;
using UnityEngine;

namespace EasyGame.SideScroller.Player
{
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private bool jumpPressed;
        private bool jumpReleased;
        private bool attackPressed;
        private int skillPressed = -1;
        private bool focused = true;
        private bool paused;

        public float Horizontal { get; private set; }
        public bool JumpHeld { get; private set; }

        private void Update()
        {
            if (!focused || paused)
            {
                ResetInput();
                return;
            }

            float keyboardAxis = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                keyboardAxis -= 1f;
            }
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                keyboardAxis += 1f;
            }
            if (MobileInputBridge.LeftHeld)
            {
                keyboardAxis -= 1f;
            }
            if (MobileInputBridge.RightHeld)
            {
                keyboardAxis += 1f;
            }

            Horizontal = Mathf.Clamp(keyboardAxis, -1f, 1f);

            bool jumpNow = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || MobileInputBridge.JumpHeld;
            // Always drain mobile edges, even when a keyboard/held condition is
            // already true. Short-circuiting here replays an old tap next frame.
            bool mobileJumpPressed = MobileInputBridge.ConsumeJumpPressed();
            bool mobileAttackPressed = MobileInputBridge.ConsumeAttackPressed();
            int mobileSkill = MobileInputBridge.ConsumeSkillPressed();
            if (mobileSkill > 0) skillPressed = mobileSkill;
            if (Input.GetKeyDown(KeyCode.K)) skillPressed = 1;
            if (Input.GetKeyDown(KeyCode.L)) skillPressed = 2;
            if (Input.GetKeyDown(KeyCode.U)) skillPressed = 3;
            if ((jumpNow && !JumpHeld) || mobileJumpPressed)
            {
                jumpPressed = true;
            }
            if (!jumpNow && JumpHeld)
            {
                jumpReleased = true;
            }
            JumpHeld = jumpNow;

            if (Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0) || mobileAttackPressed)
            {
                attackPressed = true;
            }
        }

        public bool ConsumeJumpPressed()
        {
            bool value = jumpPressed;
            jumpPressed = false;
            return value;
        }

        public bool ConsumeJumpReleased()
        {
            bool value = jumpReleased;
            jumpReleased = false;
            return value;
        }

        public bool ConsumeAttackPressed()
        {
            bool value = attackPressed;
            attackPressed = false;
            return value;
        }

        private void OnDisable()
        {
            ResetInput();
        }

        public int ConsumeSkillPressed()
        {
            int value = skillPressed;
            skillPressed = -1;
            return value;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus) ResetInput();
        }

        private void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            if (isPaused) ResetInput();
        }

        private void ResetInput()
        {
            Horizontal = 0f;
            JumpHeld = false;
            jumpPressed = false;
            jumpReleased = false;
            attackPressed = false;
            skillPressed = -1;
        }
    }
}
