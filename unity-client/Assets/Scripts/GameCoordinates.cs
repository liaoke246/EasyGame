using UnityEngine;

namespace EasyGame
{
    public static class GameCoordinates
    {
        public const float WorldScale = 0.01f;

        public static Vector3 ToUnity(float serverX, float serverY, float worldHeight)
        {
            return new Vector3(serverX * WorldScale, 0f, (worldHeight - serverY) * WorldScale);
        }

        public static Vector2 DirectionVector(string direction)
        {
            const float diagonal = 0.70710678f;
            switch (direction)
            {
                case "up": return new Vector2(0f, 1f);
                case "up-right": return new Vector2(diagonal, diagonal);
                case "right": return new Vector2(1f, 0f);
                case "down-right": return new Vector2(diagonal, -diagonal);
                case "down": return new Vector2(0f, -1f);
                case "down-left": return new Vector2(-diagonal, -diagonal);
                case "left": return new Vector2(-1f, 0f);
                case "up-left": return new Vector2(-diagonal, diagonal);
                default: return Vector2.down;
            }
        }

        public static Quaternion DirectionRotation(string direction)
        {
            Vector2 vector = DirectionVector(direction);
            return Quaternion.LookRotation(new Vector3(vector.x, 0f, vector.y), Vector3.up);
        }

        public static Color ParseColor(string html, Color fallback)
        {
            return !string.IsNullOrWhiteSpace(html) && ColorUtility.TryParseHtmlString(html, out Color color)
                ? color
                : fallback;
        }
    }
}
