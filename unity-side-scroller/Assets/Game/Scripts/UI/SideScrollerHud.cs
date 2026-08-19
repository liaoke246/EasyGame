using UnityEngine;

namespace EasyGame.SideScroller.UI
{
    public sealed class SideScrollerHud : MonoBehaviour
    {
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private Texture2D hudFrame;
        private Texture2D healthFill;
        private Texture2D energyFill;
        private Texture2D experienceFill;

        private void OnGUI()
        {
            BuildStyles();
            float scale = Mathf.Clamp(Screen.height / 720f, 0.72f, 1.25f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            if (hudFrame != null)
            {
                if (healthFill != null)
                {
                    GUI.DrawTexture(new Rect(22f, 28f, 112f, 108f), healthFill, ScaleMode.StretchToFill, true);
                }
                if (experienceFill != null)
                {
                    GUI.DrawTexture(new Rect(146f, 94f, 64f, 8f), experienceFill, ScaleMode.StretchToFill, true);
                }
                if (energyFill != null)
                {
                    GUI.DrawTexture(new Rect(144f, 116f, 98f, 12f), energyFill, ScaleMode.StretchToFill, true);
                }
                GUI.DrawTexture(new Rect(18f, 18f, 232f, 128f), hudFrame, ScaleMode.StretchToFill, true);
            }
            GUI.Label(new Rect(258f, 30f, 250f, 28f), "DEAD RAILS // ONLINE", titleStyle);
            GUI.Label(new Rect(258f, 59f, 210f, 22f), "LV. 1   HP 100 / 100", labelStyle);
            GUI.Label(new Rect(258f, 82f, 210f, 22f), "EXP 0 / 100", labelStyle);

            GUI.Label(new Rect(width - 292f, 26f, 270f, 24f), "SAFE ZONE OUTSKIRTS", titleStyle);
            GUI.Label(new Rect(width - 292f, 54f, 270f, 42f), "CO-OP SURVIVAL\n10 INFECTED DETECTED", labelStyle);

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

            hudFrame = Resources.Load<Texture2D>("ThirdParty/GandalfHardcore/hud-frame");
            healthFill = Resources.Load<Texture2D>("ThirdParty/GandalfHardcore/hud-health");
            energyFill = Resources.Load<Texture2D>("ThirdParty/GandalfHardcore/hud-energy");
            experienceFill = Resources.Load<Texture2D>("ThirdParty/GandalfHardcore/hud-exp");

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
        }
    }
}
