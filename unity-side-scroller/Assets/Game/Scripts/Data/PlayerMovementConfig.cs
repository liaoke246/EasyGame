using UnityEngine;

namespace EasyGame.SideScroller.Data
{
    [CreateAssetMenu(menuName = "EasyGame 2D/Player Movement Config")]
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        [Header("Horizontal")]
        [Min(0.1f)] public float moveSpeed = 5.6f;
        [Min(0.1f)] public float groundAcceleration = 26f;
        [Min(0.1f)] public float groundDeceleration = 34f;
        [Range(0.1f, 1f)] public float airControl = 0.58f;

        [Header("Jump")]
        [Min(0.1f)] public float jumpVelocity = 15f;
        [Range(0.01f, 0.3f)] public float coyoteTime = 0.16f;
        [Range(0.01f, 0.3f)] public float jumpBuffer = 0.18f;
        [Min(1f)] public float fallGravityMultiplier = 1.65f;
        [Min(1f)] public float lowJumpGravityMultiplier = 1.08f;
        [Min(1f)] public float gravityScale = 3.05f;
        [Min(1f)] public float maxFallSpeed = 18f;

        [Header("Grounding")]
        [Min(0.01f)] public float groundProbeDistance = 0.16f;
        [Range(0.3f, 1f)] public float groundProbeWidth = 0.78f;
    }
}
