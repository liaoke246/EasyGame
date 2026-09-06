using UnityEngine;

namespace EasyGame.SideScroller.Core
{
    /// <summary>
    /// Single source of truth for actor collision and presentation anchors.
    /// Physics roots stay at their rigidbody origin; sprites are attached to a
    /// bottom-centre feet anchor derived from the collider.
    /// </summary>
    public static class ActorGeometry2D
    {
        public static readonly Vector2 HumanoidColliderSize = new Vector2(0.72f, 1.48f);
        public static readonly Vector2 HumanoidColliderOffset = new Vector2(0f, 0.02f);
        public static readonly Vector2 SlimeColliderSize = new Vector2(0.72f, 0.48f);
        public static readonly Vector2 SlimeColliderOffset = new Vector2(0f, 0.02f);

        public static float HumanoidFeetLocalY => HumanoidColliderOffset.y - HumanoidColliderSize.y * 0.5f;
        public static float SlimeFeetLocalY => SlimeColliderOffset.y - SlimeColliderSize.y * 0.5f;

        public static void ConfigureHumanoid(CapsuleCollider2D collider)
        {
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = HumanoidColliderSize;
            collider.offset = HumanoidColliderOffset;
        }

        public static void ConfigureSlime(CapsuleCollider2D collider)
        {
            collider.direction = CapsuleDirection2D.Horizontal;
            collider.size = SlimeColliderSize;
            collider.offset = SlimeColliderOffset;
        }

        public static void ConfigurePlayerAvatar(CapsuleCollider2D collider, PlayerAvatarKind avatar)
        {
            if (avatar != PlayerAvatarKind.Slime)
            {
                ConfigureHumanoid(collider);
                return;
            }
            ConfigureSlime(collider);
            // All player roots retain the same feet coordinate, including when
            // profile replication arrives after spawn. Only the silhouette changes.
            collider.offset = new Vector2(0f, HumanoidFeetLocalY + SlimeColliderSize.y * 0.5f);
        }

        public static Vector3 RootPositionForFeet(Vector3 feetPosition, float feetLocalY)
        {
            return new Vector3(feetPosition.x, feetPosition.y - feetLocalY, feetPosition.z);
        }

        public static Vector3 FeetLocalPosition(Collider2D collider)
        {
            if (collider == null)
            {
                return Vector3.zero;
            }

            return new Vector3(collider.offset.x, collider.offset.y - LocalHalfHeight(collider), 0f);
        }

        public static Vector2 BodyCenter(Collider2D collider)
        {
            if (collider == null)
            {
                return Vector2.zero;
            }

            // Attack queries use the current physics pose, not the interpolated
            // Transform used for rendering between physics ticks.
            return collider.bounds.center;
        }

        public static Vector3 HeadWorldPosition(Collider2D collider, float padding)
        {
            if (collider == null)
            {
                return Vector3.zero;
            }

            Vector3 localHead = new Vector3(collider.offset.x, collider.offset.y + LocalHalfHeight(collider), 0f);
            Vector3 worldHead = collider.transform.TransformPoint(localHead);
            worldHead.y += padding;
            return worldHead;
        }

        private static float LocalHalfHeight(Collider2D collider)
        {
            return collider switch
            {
                CapsuleCollider2D capsule => capsule.size.y * 0.5f,
                BoxCollider2D box => box.size.y * 0.5f,
                CircleCollider2D circle => circle.radius,
                _ => collider.bounds.size.y * 0.5f,
            };
        }
    }
}
