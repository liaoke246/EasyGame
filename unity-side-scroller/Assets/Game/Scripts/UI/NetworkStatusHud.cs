using EasyGame.SideScroller.Network;
using UnityEngine;

namespace EasyGame.SideScroller.UI
{
    public sealed class NetworkStatusHud : MonoBehaviour
    {
        private GUIStyle style;

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.66f, 0.86f, 0.76f) },
            };
            GUI.Label(new Rect(Screen.width * 0.5f - 190f, 54f, 380f, 24f), SideScrollerNetworkManager.ConnectionStatus, style);
        }
    }
}
