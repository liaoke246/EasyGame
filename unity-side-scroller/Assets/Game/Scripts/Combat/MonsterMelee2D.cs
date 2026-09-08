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
        public bool HasTarget { get; private set; }

        public bool Step(int id, Collider2D body, bool grounded, double now, ref int facing)
        {
            var definition = CombatActions2D.Get(id);
            // Acquire even during cooldown: a nearby player takes priority over
            // patrol. This also lets a monster notice someone behind its back.
            int direction = grounded ? FindTarget(body, definition, facing) : 0;
            HasTarget = direction != 0;
            if (!Clock.Busy(now))
            {
                if (HasTarget) facing = direction;
                if (!grounded || now < Clock.ReadyAt(id)) return false;
                if (direction == 0 || !Clock.TryBegin(id, direction, grounded, now)) return false;
            }
            else if (HasTarget) Clock.TrackDuringWindup(direction, now);
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

        private int FindTarget(Collider2D body, CombatActionDefinition definition, int currentFacing)
        {
            int direction = 0;
            float nearest = float.PositiveInfinity;
            foreach (int look in Directions)
            {
                var candidates = query.CollectTargets(body, definition.Center(body.bounds.center, look), definition.Size);
                for (int index = 0; index < candidates.Count; index++)
                {
                    var player = candidates[index].GetComponentInParent<SideScrollerNetworkPlayer>();
                    if (player == null || player.IsDefeated) continue;
                    Vector2 delta = candidates[index].bounds.center - body.bounds.center;
                    // A small overlap dead zone avoids rapid left/right jitter.
                    if (delta.sqrMagnitude >= nearest) continue;
                    nearest = delta.sqrMagnitude;
                    direction = Mathf.Abs(delta.x) < .12f ? currentFacing : delta.x < 0f ? -1 : 1;
                }
            }
            return direction;
        }
    }
}
