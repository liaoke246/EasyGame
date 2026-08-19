using UnityEngine;

namespace EasyGame.SideScroller.UI
{
    public static class PixelHudDrawing
    {
        private static GUIStyle worldLabelStyle;

        public static void FillRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = previous;
        }

        public static void Bar(Rect rect, float normalized, Color fill)
        {
            FillRect(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), new Color(0.04f, 0.055f, 0.06f, 0.96f));
            FillRect(rect, new Color(0.16f, 0.11f, 0.1f, 0.94f));
            FillRect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(normalized), rect.height), fill);
            FillRect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(normalized), 2f), Color.Lerp(fill, Color.white, 0.3f));
        }

        public static void WorldBar(Vector3 worldPosition, string label, float normalized, Color fill, float width = 74f)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            Vector3 screen = camera.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0f || screen.x < -width || screen.x > Screen.width + width || screen.y < -30f || screen.y > Screen.height + 30f)
            {
                return;
            }

            worldLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };

            float x = screen.x - width * 0.5f;
            float y = Screen.height - screen.y;
            FillRect(new Rect(x - 2f, y - 2f, width + 4f, 18f), new Color(0.025f, 0.035f, 0.04f, 0.82f));
            GUI.Label(new Rect(x - 10f, y - 3f, width + 20f, 15f), label, worldLabelStyle);
            Bar(new Rect(x, y + 16f, width, 6f), normalized, fill);
        }
    }
}
