using UnityEngine;

namespace EasyGame.SideScroller.Network
{
    /// <summary>Latest input intent with bounded lifetime. Sequence numbers reject reordered packets.</summary>
    public sealed class ServerPlayerInput
    {
        public const double TimeoutSeconds = 0.35d;
        public float Horizontal { get; private set; }
        public bool JumpHeld { get; private set; }
        private uint sequence;
        private bool received;
        private double receivedAt;

        public bool Receive(float horizontal, bool jumpHeld, uint nextSequence, double now)
        {
            if (received && unchecked((int)(nextSequence - sequence)) <= 0) return false;
            if (float.IsNaN(horizontal) || float.IsInfinity(horizontal)) return false;
            sequence = nextSequence;
            received = true;
            receivedAt = now;
            Horizontal = Mathf.Clamp(horizontal, -1f, 1f);
            JumpHeld = jumpHeld;
            return true;
        }

        public void Expire(double now)
        {
            if (!received || now - receivedAt >= TimeoutSeconds) ClearIntent();
        }

        public void ClearIntent()
        {
            Horizontal = 0f;
            JumpHeld = false;
        }
    }
}
