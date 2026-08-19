using UnityEngine;

namespace EasyGame.SideScroller.UI
{
    public sealed class SideScrollerHud : MonoBehaviour
    {
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle panelStyle;

        private void OnGUI()
        {
            BuildStyles();
            float scale = Mathf.Clamp(Screen.height / 720f, 0.72f, 1.25f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            Rect panel = new Rect(18f, 18f, 268f, 98f);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.Label(new Rect(34f, 30f, 240f, 28f), "DEAD RAILS // PROTOTYPE", titleStyle);
            GUI.Label(new Rect(34f, 60f, 240f, 22f), "LV. 1   HP 100 / 100", labelStyle);
            GUI.Label(new Rect(34f, 82f, 240f, 22f), "EXP 0 / 100", labelStyle);

            GUI.Label(new Rect(width - 292f, 26f, 270f, 24f), "SAFE ZONE OUTSKIRTS", titleStyle);
            GUI.Label(new Rect(width - 292f, 54f, 270f, 42f), "Phase 1-2 playable build\nMirror server-authority next", labelStyle);

            if (!Application.isMobilePlatform)
            {
                GUI.Label(new Rect(22f, Screen.height / scale - 42f, 520f, 24f), "A/D MOVE   SPACE JUMP   J ATTACK", labelStyle);
            }
        }

        private void BuildStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.82f, 0.55f) },
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.78f, 0.84f, 0.8f) },
            };
            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = Texture2D.grayTexture;
        }
    }
}
