using UnityEngine;

namespace EasyGame.SideScroller.Data
{
    [CreateAssetMenu(menuName = "EasyGame 2D/Level Progression Config")]
    public sealed class LevelProgressionConfig : ScriptableObject
    {
        [SerializeField] private int[] experiencePerLevel = { 100, 150, 220, 320, 450, 620, 840, 1120, 1480 };
        public int MaxLevel => (experiencePerLevel?.Length ?? 0) + 1;

        public int ExperienceForLevel(int level)
        {
            if (experiencePerLevel == null || experiencePerLevel.Length == 0) return 100;
            int index = Mathf.Clamp(level - 1, 0, experiencePerLevel.Length - 1);
            return Mathf.Max(1, experiencePerLevel[index]);
        }

        public void AddExperience(ref int level, ref int experience, int reward)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);
            long remaining = (long)Mathf.Max(0, experience) + Mathf.Max(0, reward);
            while (level < MaxLevel && remaining >= ExperienceForLevel(level))
            {
                remaining -= ExperienceForLevel(level);
                level++;
            }
            experience = level >= MaxLevel ? 0 : (int)remaining;
        }
    }
}
