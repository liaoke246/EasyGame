using UnityEngine;

namespace EasyGame.SideScroller.Core
{
    public static class GroundProbe2D
    {
        public static bool Check(Collider2D bodyCollider, float widthFactor, float distance, int layerMask)
        {
            if (bodyCollider == null || !bodyCollider.enabled)
            {
                return false;
            }

            Bounds bounds = bodyCollider.bounds;
            float probeHeight = Mathf.Max(0.08f, distance + 0.04f);
            Vector2 center = new Vector2(bounds.center.x, bounds.min.y - distance * 0.5f);
            Vector2 size = new Vector2(bounds.size.x * widthFactor, probeHeight);
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, layerMask);
            foreach (Collider2D hit in hits)
            {
                if (hit == null || hit.isTrigger || hit == bodyCollider)
                {
                    continue;
                }
                if (hit.attachedRigidbody != null && hit.attachedRigidbody == bodyCollider.attachedRigidbody)
                {
                    continue;
                }
                return true;
            }
            return false;
        }
    }
}
