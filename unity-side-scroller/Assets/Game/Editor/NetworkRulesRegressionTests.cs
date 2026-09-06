using System;
using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Data;
using EasyGame.SideScroller.Network;
using EasyGame.SideScroller.World;
using UnityEngine;

namespace EasyGame.SideScroller.Editor
{
    public static class NetworkRulesRegressionTests
    {
        public static void Run()
        {
            var input = new ServerPlayerInput();
            Check(input.Receive(8f, true, 1, 0d), "first input accepted");
            Check(input.Horizontal == 1f && input.JumpHeld, "input clamped");
            Check(!input.Receive(-1f, false, 0, 0.1d), "old input rejected");
            Check(!input.Receive(float.NaN, false, 2, 0.1d), "NaN rejected");
            Check(!input.Receive(float.PositiveInfinity, false, 2, 0.1d), "infinite input rejected");
            input.Expire(0.36d);
            Check(input.Horizontal == 0f && !input.JumpHeld, "stale input released");
            var wrap = new ServerPlayerInput();
            wrap.Receive(1f, false, uint.MaxValue, 1d);
            Check(wrap.Receive(-1f, false, 0, 1.1d), "sequence rollover accepted");

            Check(PlayerProfileSelection.SanitizeAvatar((PlayerAvatarKind)255) == PlayerAvatarKind.Warrior, "invalid avatar rejected");
            Check(PlayerProfileSelection.SanitizeName("<b>ALICE</b>", "PLAYER").IndexOf('<') < 0, "name cannot inject rich text");
            Check(PlayerProfileSelection.SanitizeName("01234567890123456789", "PLAYER").Length == 14, "name bounded");
            Check(SideCameraRig.ClampAxis(100f, -10f, 10f, 30f) == 0f, "oversize viewport centered");

            var progression = ScriptableObject.CreateInstance<LevelProgressionConfig>();
            try
            {
                int level = 1, experience = 0;
                progression.AddExperience(ref level, ref experience, 270);
                Check(level == 3 && experience == 20, "multiple levels preserve overflow XP");
                progression.AddExperience(ref level, ref experience, -100);
                Check(experience == 20, "negative reward ignored");
                progression.AddExperience(ref level, ref experience, int.MaxValue);
                Check(level == progression.MaxLevel && experience == 0, "XP capped without integer overflow");
            }
            finally { UnityEngine.Object.DestroyImmediate(progression); }
            Debug.Log("Network rules regressions passed: bounded/reordered/stale inputs, names, camera bounds, progression.");
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Network rules regression: " + name);
        }
    }
}
