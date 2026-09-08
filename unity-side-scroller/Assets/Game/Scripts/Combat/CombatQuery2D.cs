using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EasyGame.SideScroller.Combat
{
    /// <summary>
    /// A reusable, server-side attack query. Each rigidbody can be damaged once
    /// per swing and solid world geometry blocks the path to its hitbox.
    /// </summary>
    public sealed class CombatQuery2D
    {
        private readonly List<Collider2D> overlaps = new List<Collider2D>(32);
        private readonly List<Collider2D> targets = new List<Collider2D>(16);
        private readonly List<RaycastHit2D> blockers = new List<RaycastHit2D>(16);
        private readonly HashSet<Rigidbody2D> visitedBodies = new HashSet<Rigidbody2D>();
        private readonly ContactFilter2D filter = new ContactFilter2D { useTriggers = false };

        public IReadOnlyList<Collider2D> CollectTargets(Collider2D attacker, Vector2 center, Vector2 size)
        {
            targets.Clear();
            visitedBodies.Clear();
            if (attacker == null || !attacker.enabled)
            {
                return targets;
            }

            PhysicsScene2D scene = attacker.gameObject.scene.GetPhysicsScene2D();
            scene.OverlapBox(center, size, 0f, filter, overlaps);
            Vector2 origin = attacker.bounds.center;
            Rigidbody2D attackerBody = attacker.attachedRigidbody;
            foreach (Collider2D candidate in overlaps)
            {
                if (candidate == null || candidate == attacker || candidate.isTrigger)
                {
                    continue;
                }
                Rigidbody2D targetBody = candidate.attachedRigidbody;
                if (targetBody == null || targetBody == attackerBody || targetBody.bodyType != RigidbodyType2D.Dynamic || visitedBodies.Contains(targetBody))
                {
                    continue;
                }
                if (!HasClearPath(scene, attacker, candidate, origin))
                {
                    continue;
                }
                visitedBodies.Add(targetBody);
                targets.Add(candidate);
            }
            return targets;
        }

        private bool HasClearPath(PhysicsScene2D scene, Collider2D attacker, Collider2D target, Vector2 origin)
        {
            Vector2 offset = target.ClosestPoint(origin) - origin;
            float distance = offset.magnitude;
            if (distance < 0.001f)
            {
                return true;
            }

            scene.Raycast(origin, offset / distance, distance, filter, blockers);
            foreach (RaycastHit2D hit in blockers)
            {
                Collider2D obstacle = hit.collider;
                if (obstacle == null || obstacle.isTrigger || obstacle == attacker || obstacle == target)
                {
                    continue;
                }
                Rigidbody2D obstacleBody = obstacle.attachedRigidbody;
                if (obstacleBody == attacker.attachedRigidbody || obstacleBody == target.attachedRigidbody)
                {
                    continue;
                }
                if (obstacleBody == null || obstacleBody.bodyType != RigidbodyType2D.Dynamic)
                {
                    return false;
                }
            }
            return true;
        }
    }

    public static class CombatTiming2D
    {
        // Six licensed attack frames last 0.282 seconds. Keep the full recovery
        // visible before allowing another server-authoritative swing.
        public const float AttackDuration = 0.30f;
        public const float AttackCooldown = 0.34f;
    }
}
