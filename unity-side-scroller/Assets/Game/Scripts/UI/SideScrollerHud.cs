using EasyGame.SideScroller.Network;
using UnityEngine;

namespace EasyGame.SideScroller.UI
{
    public sealed class SideScrollerHud : MonoBehaviour
    {
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;

        private void OnGUI()
        {
            BuildStyles();
            float scale = Mathf.Clamp(Screen.height / 720f, 0.72f, 1.2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            SideScrollerNetworkPlayer player = SideScrollerNetworkPlayer.Local;
            int health = player != null ? player.Health : 100;
            int maxHealth = player != null ? player.MaxHealth : 100;
            int experience = player != null ? player.Experience : 0;
            int kills = player != null ? player.Kills : 0;
            int deaths = player != null ? player.Deaths : 0;
            string playerName = player != null ? player.DisplayName : "OFFLINE SURVIVOR";

            Rect panel = new Rect(18f, 18f, 310f, 112f);
            PixelHudDrawing.FillRect(panel, new Color(0.025f, 0.045f, 0.052f, 0.9f));
            PixelHudDrawing.FillRect(new Rect(panel.x, panel.y, 5f, panel.height), new Color(0.84f, 0.66f, 0.26f, 1f));
            GUI.Label(new Rect(34f, 26f, 278f, 24f), playerName, titleStyle);
            GUI.Label(new Rect(34f, 49f, 278f, 19f), $"LV.1   K {kills} / D {deaths}   PVP ON", smallStyle);
            PixelHudDrawing.Bar(new Rect(34f, 76f, 270f, 14f), maxHealth > 0 ? health / (float)maxHealth : 0f, new Color(0.82f, 0.16f, 0.13f));
            GUI.Label(new Rect(39f, 72f, 260f, 20f), $"HP  {health} / {maxHealth}", labelStyle);
            PixelHudDrawing.Bar(new Rect(34f, 103f, 270f, 7f), (experience % 100) / 100f, new Color(0.86f, 0.66f, 0.19f));

            Rect mission = new Rect(width - 278f, 18f, 260f, 74f);
            PixelHudDrawing.FillRect(mission, new Color(0.025f, 0.045f, 0.052f, 0.82f));
            GUI.Label(new Rect(mission.x + 14f, 26f, 232f, 24f), "FOREST OUTSKIRTS", titleStyle);
            GUI.Label(new Rect(mission.x + 14f, 51f, 232f, 34f), "PVP ENABLED  //  SLIMES & INFECTED", smallStyle);

            if (!Application.isMobilePlatform)
            {
                GUI.Label(new Rect(22f, height - 42f, 620f, 24f), "A/D MOVE   SPACE/W JUMP   J/CLICK ATTACK", labelStyle);
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
                normal = { textColor = new Color(0.95f, 0.81f, 0.4f) },
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.73f, 0.82f, 0.78f) },
            };
        }
    }
}
