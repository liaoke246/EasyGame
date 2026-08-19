using UnityEngine;

namespace EasyGame.SideScroller.Player
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private bool jumpPressed;
        private bool jumpReleased;
        private bool attackPressed;

        public float Horizontal { get; private set; }
        public bool JumpHeld { get; private set; }

        private void Update()
        {
            float keyboardAxis = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                keyboardAxis -= 1f;
            }
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                keyboardAxis += 1f;
            }

            Horizontal = Mathf.Clamp(keyboardAxis, -1f, 1f);

            bool jumpNow = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            if (jumpNow && !JumpHeld)
            {
                jumpPressed = true;
            }
            if (!jumpNow && JumpHeld)
            {
                jumpReleased = true;
            }
            JumpHeld = jumpNow;

            if (Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0))
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
            Horizontal = 0f;
            JumpHeld = false;
            jumpPressed = false;
            jumpReleased = false;
            attackPressed = false;
        }
    }
}
