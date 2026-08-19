using EasyGame.SideScroller.Network;
using Mirror;
using UnityEngine;

namespace EasyGame.SideScroller.UI
{
    public sealed class NetworkStatusHud : MonoBehaviour
    {
        private GUIStyle style;

        private void OnGUI()
        {
            if (Utils.IsHeadless())
            {
                return;
            }

            style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.66f, 0.86f, 0.76f) },
            };
            float top = Screen.width < 780f ? 142f : 54f;
            GUI.Label(new Rect(Screen.width * 0.5f - 190f, top, 380f, 24f), SideScrollerNetworkManager.ConnectionStatus, style);
        }
    }
}
