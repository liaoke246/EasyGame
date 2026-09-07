using UnityEngine;

namespace EasyGame.SideScroller.Combat
{
    public enum CombatActionId { Basic, Cleave, Rising, Nova, ZombieClaw, SlimeSlam }

    /// <summary>One definition drives server hit timing, animation phases, range and UI.</summary>
    public readonly struct CombatActionDefinition
    {
        public readonly float Windup, Active, Recovery, Cooldown, Offset, Lift;
        public readonly Vector2 Size, Impulse;
        public readonly int Damage, PvpDamage;
        public readonly bool GroundOnly;
        public float Duration => Windup + Active + Recovery;

        public CombatActionDefinition(float windup, float active, float recovery, float cooldown,
            float offset, float lift, Vector2 size, int damage, int pvpDamage, Vector2 impulse, bool groundOnly = false)
        {
            Windup = windup; Active = active; Recovery = recovery; Cooldown = cooldown;
            Offset = offset; Lift = lift; Size = size; Damage = damage; PvpDamage = pvpDamage;
            Impulse = impulse; GroundOnly = groundOnly;
        }

        public Vector2 Center(Vector2 bodyCenter, int facing) => bodyCenter + new Vector2(Offset * facing, Lift);

        // Hold anticipation, then advance the contact and recovery frames. No
        // transform scaling or separate arm sprites are involved.
        public int Frame(float elapsed, int count)
        {
            // These are authored strip markers, not equal thirds: humanoid
            // frames 0..3 raise the blade, frame 4 is the visible slash, 5 recovers.
            // Slime frames 0..1 compress, 3..5 strike, 6..7 recover.
            if (elapsed < Windup)
            {
                int anticipationCount = count == 6 ? 4 : 2;
                return Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, elapsed) / Windup * anticipationCount), 0, anticipationCount - 1);
            }
            if (elapsed < Windup + Active)
                return count == 6 ? 4 : Mathf.Clamp(3 + Mathf.FloorToInt((elapsed - Windup) / Active * 3f), 3, count - 3);
            return count == 6 ? 5 : Mathf.Clamp(6 + Mathf.FloorToInt((elapsed - Windup - Active) / Recovery * 2f), 6, count - 1);
        }
    }

    public static class CombatActions2D
    {
        public const int PlayerActionCount = 4;
        public const int Count = 6;
        private static readonly CombatActionDefinition[] Definitions =
        {
            new CombatActionDefinition(.085f, .075f, .14f, .34f, .82f, 0f, new Vector2(1.45f, 1.25f), 30, 25, Vector2.zero),
            new CombatActionDefinition(.16f, .10f, .22f, 3f, 1.25f, 0f, new Vector2(2.5f, 1.45f), 45, 30, new Vector2(3f, 0f)),
            new CombatActionDefinition(.20f, .12f, .28f, 5f, .8f, .45f, new Vector2(1.8f, 2.3f), 40, 28, new Vector2(1.8f, 7f)),
            new CombatActionDefinition(.38f, .12f, .32f, 8f, 0f, 0f, new Vector2(4.8f, 2.1f), 55, 35, new Vector2(4f, 3f), true),
            new CombatActionDefinition(.38f, .10f, .34f, 1.3f, .55f, 0f, new Vector2(1.2f, 1.35f), 12, 12, Vector2.zero, true),
            new CombatActionDefinition(.46f, .12f, .38f, 1.6f, 0f, .12f, new Vector2(1.7f, .95f), 9, 9, Vector2.zero, true),
        };

        public static bool IsValid(int id) => id >= 0 && id < Count;
        public static bool IsPlayerAction(int id) => id >= 0 && id < PlayerActionCount;
        public static CombatActionDefinition Get(int id) => Definitions[IsValid(id) ? id : 0];
        public static string Label(int id) => id switch { 1 => "CLEAVE", 2 => "RISING", 3 => "NOVA", _ => "ATTACK" };
        public static string Key(int id) => id switch { 1 => "K", 2 => "L", 3 => "U", _ => "J" };
    }

    /// <summary>Pure fixed-tick action lifecycle, shared by players and monsters.</summary>
    public sealed class CombatActionClock
    {
        private readonly double[] readyAt = new double[CombatActions2D.Count];
        private bool hitConsumed;
        public int Action { get; private set; } = -1;
        public int Facing { get; private set; } = 1;
        public double StartedAt { get; private set; }
        public bool Busy(double now) => Action >= 0 && now < StartedAt + CombatActions2D.Get(Action).Duration;
        public double ReadyAt(int id) => CombatActions2D.IsValid(id) ? readyAt[id] : double.PositiveInfinity;

        public bool TryBegin(int id, int facing, bool grounded, double now)
        {
            if (!CombatActions2D.IsValid(id) || double.IsNaN(now) || double.IsInfinity(now) || Busy(now) || now < readyAt[id]) return false;
            var definition = CombatActions2D.Get(id);
            if (definition.GroundOnly && !grounded) return false;
            Action = id; Facing = facing < 0 ? -1 : 1; StartedAt = now; hitConsumed = false;
            readyAt[id] = now + definition.Cooldown;
            return true;
        }

        public bool ConsumeHit(double now)
        {
            if (Action < 0 || hitConsumed) return false;
            var definition = CombatActions2D.Get(Action);
            if (now < StartedAt + definition.Windup) return false;
            hitConsumed = true;
            // A suspended simulation must never deliver a long-expired strike.
            return now < StartedAt + definition.Windup + definition.Active;
        }

        public void Cancel() { Action = -1; hitConsumed = true; }
        public void Reset() { Cancel(); System.Array.Clear(readyAt, 0, readyAt.Length); }
    }
}
