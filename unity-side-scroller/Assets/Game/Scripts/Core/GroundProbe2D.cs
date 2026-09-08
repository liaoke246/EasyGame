using UnityEngine;
using UnityEngine.SceneManagement;

namespace EasyGame.SideScroller.Core
{
    public static class GroundProbe2D
    {
        private const float Skin = 0.025f;
        private const float MinimumGroundNormalY = 0.65f;
        private static readonly RaycastHit2D[] Hits = new RaycastHit2D[16];

        public static bool Check(Collider2D bodyCollider, float widthFactor, float distance, int layerMask)
        {
            if (bodyCollider == null || !bodyCollider.enabled || !bodyCollider.gameObject.activeInHierarchy)
            {
                return false;
            }

            // Overlapping a one-way platform during ascent is not a landing.
            if (bodyCollider.attachedRigidbody != null && bodyCollider.attachedRigidbody.linearVelocity.y > 0.05f)
            {
                return false;
            }
            Bounds bounds = bodyCollider.bounds;
            float halfWidth = bounds.extents.x * Mathf.Clamp01(widthFactor);
            Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + Skin);
            float rayDistance = Mathf.Max(0f, distance) + Skin;
            return HasSupportBelow(bodyCollider, origin, rayDistance, layerMask)
                || HasSupportBelow(bodyCollider, origin + Vector2.left * halfWidth, rayDistance, layerMask)
                || HasSupportBelow(bodyCollider, origin + Vector2.right * halfWidth, rayDistance, layerMask);
        }

        public static bool HasSupportBelow(Collider2D bodyCollider, Vector2 origin, float distance, int layerMask = Physics2D.DefaultRaycastLayers)
        {
            if (bodyCollider == null || distance <= 0f)
            {
                return false;
            }

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(layerMask);
            filter.useTriggers = false;
            PhysicsScene2D scene = bodyCollider.gameObject.scene.GetPhysicsScene2D();
            int count = scene.Raycast(origin, Vector2.down, distance, filter, Hits);
            for (int index = 0; index < count; index++)
            {
                RaycastHit2D result = Hits[index];
                Collider2D hit = result.collider;
                if (hit == null || hit.isTrigger || hit == bodyCollider)
                {
                    continue;
                }
                Rigidbody2D hitBody = hit.attachedRigidbody;
                if (hitBody != null && (hitBody == bodyCollider.attachedRigidbody || hitBody.bodyType == RigidbodyType2D.Dynamic))
                {
                    continue;
                }
                // A ray starting inside a platform returns an artificial opposite
                // normal. Reject it, along with wall/underside contacts.
                if (result.fraction <= 0f || result.normal.y < MinimumGroundNormalY)
                {
                    continue;
                }
                return true;
            }
            return false;
        }
    }
}
