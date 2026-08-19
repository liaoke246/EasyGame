using UnityEngine;

namespace EasyGame.SideScroller.Data
{
    [CreateAssetMenu(menuName = "EasyGame 2D/Level Progression Config")]
    public sealed class LevelProgressionConfig : ScriptableObject
    {
        [SerializeField] private int[] experiencePerLevel = { 100, 150, 220, 320, 450, 620, 840, 1120, 1480 };

        public int ExperienceForLevel(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, experiencePerLevel.Length - 1);
            return experiencePerLevel[index];
        }
    }
}
