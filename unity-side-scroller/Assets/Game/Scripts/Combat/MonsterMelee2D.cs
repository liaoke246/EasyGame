using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Network;
using UnityEngine;

namespace EasyGame.SideScroller.Combat
{
    /// <summary>Contact starts an interruptible attack, never immediate damage.</summary>
    public sealed class MonsterMelee2D
    {
        public readonly CombatActionClock Clock = new CombatActionClock();
        private readonly CombatQuery2D query = new CombatQuery2D();
        private static readonly int[] Directions = { -1, 1 };

        public bool Step(int id, Collider2D body, bool grounded, double now, ref int facing)
        {
            var definition = CombatActions2D.Get(id);
            if (!Clock.Busy(now))
            {
                if (!grounded || now < Clock.ReadyAt(id)) return false;
                // Notice opponents on either side, then lock direction through recovery.
                int direction = 0;
                float nearest = float.PositiveInfinity;
                foreach (int look in Directions)
                {
                    var candidates = query.CollectTargets(body, definition.Center(body.bounds.center, look), definition.Size);
                    for (int index = 0; index < candidates.Count; index++)
                    {
                        var player = candidates[index].GetComponentInParent<SideScrollerNetworkPlayer>();
                        if (player == null || player.IsDefeated) continue;
                        float distance = ((Vector2)candidates[index].bounds.center - (Vector2)body.bounds.center).sqrMagnitude;
                        if (distance < nearest)
                        {
                            nearest = distance;
                            direction = candidates[index].bounds.center.x < body.bounds.center.x ? -1 : 1;
                        }
                    }
                }
                if (direction == 0 || !Clock.TryBegin(id, direction, grounded, now)) return false;
            }
            facing = Clock.Facing;
            Vector2 velocity = body.attachedRigidbody.linearVelocity;
            velocity.x = 0f;
            body.attachedRigidbody.linearVelocity = velocity;
            if (Clock.ConsumeHit(now))
            {
                // Re-query at contact: leaving the telegraphed box really dodges it.
                var hits = query.CollectTargets(body, definition.Center(ActorGeometry2D.BodyCenter(body), facing), definition.Size);
                for (int index = 0; index < hits.Count; index++)
                {
                    var player = hits[index].GetComponentInParent<SideScrollerNetworkPlayer>();
                    if (player != null && !player.IsDefeated) player.ApplyDamage(definition.Damage, null);
                }
            }
            return true;
        }
    }
}
