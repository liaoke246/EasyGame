using UnityEngine;

namespace EasyGame.SideScroller.Data
{
    [CreateAssetMenu(menuName = "EasyGame 2D/Player Movement Config")]
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        [Header("Horizontal")]
        [Min(0.1f)] public float moveSpeed = 7.25f;
        [Min(0.1f)] public float groundAcceleration = 58f;
        [Min(0.1f)] public float groundDeceleration = 72f;
        [Range(0.1f, 1f)] public float airControl = 0.72f;

        [Header("Jump")]
        [Min(0.1f)] public float jumpVelocity = 12.8f;
        [Range(0.01f, 0.3f)] public float coyoteTime = 0.12f;
        [Range(0.01f, 0.3f)] public float jumpBuffer = 0.14f;
        [Range(0.2f, 0.9f)] public float jumpCutMultiplier = 0.48f;
        [Min(1f)] public float fallGravityMultiplier = 1.75f;
        [Min(1f)] public float lowJumpGravityMultiplier = 1.32f;
        [Min(1f)] public float gravityScale = 3.15f;
        [Min(1f)] public float maxFallSpeed = 19f;

        [Header("Grounding")]
        [Min(0.01f)] public float groundProbeDistance = 0.12f;
        [Range(0.3f, 1f)] public float groundProbeWidth = 0.78f;
    }
}
