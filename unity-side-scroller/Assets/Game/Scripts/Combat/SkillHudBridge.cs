using System.Runtime.InteropServices;
using UnityEngine;

namespace EasyGame.SideScroller.Combat
{
    public static class SkillHudBridge
    {
        private static float nextUpdateAt;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void EasyGameUpdateSkills(float cleave, float rising, float nova, int defeated);
#endif
        public static void Publish(float cleave, float rising, float nova, bool defeated)
        {
            if (Time.unscaledTime < nextUpdateAt) return;
            nextUpdateAt = Time.unscaledTime + .08f;
#if UNITY_WEBGL && !UNITY_EDITOR
            EasyGameUpdateSkills(cleave, rising, nova, defeated ? 1 : 0);
#endif
        }
    }
}
