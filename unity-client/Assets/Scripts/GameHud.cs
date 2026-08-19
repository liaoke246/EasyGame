using System.Collections.Generic;
using UnityEngine;

namespace EasyGame
{
    public sealed class GameHud : MonoBehaviour
    {
        private readonly Queue<FeedLine> feed = new Queue<FeedLine>();
        private readonly List<string> roster = new List<string>();
        private GUIStyle titleStyle;
        private GUIStyle valueStyle;
        private GUIStyle smallStyle;
        private GUIStyle centerStyle;
        private Texture2D panelTexture;
        private Texture2D healthTexture;
        private Texture2D healthBackTexture;
        private PlayerAvatar localPlayer;
        private NetworkStats stats;
        private bool ready;
        private bool mobileMode;
        private string status = "INITIALIZING UNITY CLIENT";
        private int onlineCount;
        private int zombieCount;

        public void SetPlayer(PlayerAvatar player)
        {
            localPlayer = player;
        }

        public void SetWorldCounts(int players, int zombies)
        {
            onlineCount = players;
            zombieCount = zombies;
        }

        public void SetRoster(PlayerState[] players)
        {
            roster.Clear();
            if (players == null)
            {
                return;
            }
            foreach (PlayerState player in players)
            {
                if (player != null && !string.IsNullOrWhiteSpace(player.displayId))
                {
                    roster.Add(player.displayId);
                }
            }
            roster.Sort(System.StringComparer.OrdinalIgnoreCase);
        }

        public void SetStats(NetworkStats value)
        {
            stats = value;
        }

        public void SetReady(string message)
        {
            ready = true;
            status = message;
        }

        public void SetStatus(string message)
        {
            status = message;
        }

        public void SetMobileMode(bool value)
        {
            mobileMode = value;
        }

        public void AddNotification(NotificationEvent notification)
        {
            if (notification == null || string.IsNullOrWhiteSpace(notification.text))
            {
                return;
            }
            feed.Enqueue(new FeedLine(notification.kind, notification.text, Time.time + 8f));
            while (feed.Count > 5)
            {
                feed.Dequeue();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.width / 1440f, 0.72f, 1.15f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float logicalWidth = Screen.width / scale;
            float logicalHeight = Screen.height / scale;

            if (!ready)
            {
                DrawLoading(logicalWidth, logicalHeight);
                return;
            }

            GUI.DrawTexture(new Rect(20f, 20f, 305f, 112f), panelTexture);
            GUI.Label(new Rect(38f, 31f, 250f, 28f), localPlayer != null ? localPlayer.DisplayId : "TRAVELER", titleStyle);
            string weapon = localPlayer != null ? localPlayer.Weapon.ToUpperInvariant() : "SMG";
            int kills = localPlayer != null ? localPlayer.Kills : 0;
            GUI.Label(new Rect(38f, 59f, 250f, 24f), $"{weapon}   KILLS {kills}", valueStyle);
            int health = localPlayer != null ? localPlayer.Health : 100;
            int maxHealth = localPlayer != null ? Mathf.Max(1, localPlayer.MaxHealth) : 100;
            GUI.DrawTexture(new Rect(38f, 91f, 250f, 15f), healthBackTexture);
            GUI.DrawTexture(new Rect(38f, 91f, 250f * Mathf.Clamp01(health / (float)maxHealth), 15f), healthTexture);
            GUI.Label(new Rect(138f, 89f, 120f, 20f), $"{health} / {maxHealth}", smallStyle);

            GUI.DrawTexture(new Rect(logicalWidth - 335f, 20f, 315f, 80f), panelTexture);
            string connection = WebSocketBridge.BrowserTransportAvailable
                ? stats.Connected ? "CONNECTED" : "RECONNECTING"
                : "EDITOR PREVIEW";
            GUI.Label(new Rect(logicalWidth - 315f, 31f, 280f, 24f), connection, valueStyle);
            GUI.Label(new Rect(logicalWidth - 315f, 60f, 280f, 22f), $"PING {stats.LatencyMs} ms   LOST {stats.PacketLossPercent}%", smallStyle);

            GUI.Label(new Rect(logicalWidth * 0.5f - 160f, 24f, 320f, 30f), $"SURVIVORS {onlineCount}   INFECTED {zombieCount}", centerStyle);
            if (!mobileMode)
            {
                GUI.Label(new Rect(logicalWidth * 0.5f - 250f, logicalHeight - 52f, 500f, 28f), "WASD MOVE   SPACE FIRE   1 / 2 / 3 SWITCH", centerStyle);
            }

            float rosterHeight = 42f + Mathf.Min(roster.Count, mobileMode ? 4 : 7) * 21f;
            float rosterY = 108f;
            GUI.DrawTexture(new Rect(logicalWidth - 335f, rosterY, 315f, rosterHeight), panelTexture);
            GUI.Label(new Rect(logicalWidth - 315f, rosterY + 8f, 280f, 24f), $"ROOM  {roster.Count}", valueStyle);
            int rosterLimit = Mathf.Min(roster.Count, mobileMode ? 4 : 7);
            for (int index = 0; index < rosterLimit; index++)
            {
                string marker = localPlayer != null && roster[index] == localPlayer.DisplayId ? "●" : "•";
                GUI.Label(new Rect(logicalWidth - 315f, rosterY + 32f + index * 21f, 280f, 21f), $"{marker} {roster[index]}", smallStyle);
            }
            if (roster.Count > rosterLimit)
            {
                GUI.Label(new Rect(logicalWidth - 315f, rosterY + 32f + rosterLimit * 21f, 280f, 21f), $"+{roster.Count - rosterLimit} MORE", smallStyle);
            }

            while (feed.Count > 0 && feed.Peek().ExpiresAt <= Time.time)
            {
                feed.Dequeue();
            }
            float feedX = mobileMode ? logicalWidth * 0.5f - 210f : logicalWidth - 430f;
            float y = mobileMode ? 60f : rosterY + rosterHeight + 10f;
            foreach (FeedLine line in feed)
            {
                GUI.DrawTexture(new Rect(feedX, y, 420f, 26f), panelTexture);
                GUI.Label(new Rect(feedX + 10f, y + 2f, 400f, 22f), $"{EventPrefix(line.Kind)}  {line.Text}", smallStyle);
                y += 29f;
            }
        }

        private void DrawLoading(float width, float height)
        {
            GUI.DrawTexture(new Rect(0f, 0f, width, height), healthBackTexture);
            GUI.Label(new Rect(width * 0.5f - 260f, height * 0.5f - 55f, 520f, 48f), "BLOCK CRISIS", centerStyle);
            GUI.Label(new Rect(width * 0.5f - 260f, height * 0.5f + 4f, 520f, 32f), status, centerStyle);
        }

        private void EnsureStyles()
        {
            if (panelTexture != null)
            {
                return;
            }
            panelTexture = SolidTexture(new Color(0.035f, 0.055f, 0.045f, 0.88f));
            healthTexture = SolidTexture(new Color(0.26f, 0.78f, 0.35f, 1f));
            healthBackTexture = SolidTexture(new Color(0.025f, 0.03f, 0.025f, 0.94f));
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.88f, 0.55f) } };
            valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(0.82f, 0.88f, 0.81f) } };
            centerStyle = new GUIStyle(valueStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
        }

        private static Texture2D SolidTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static string EventPrefix(string kind)
        {
            return kind == "join" ? "[JOIN]"
                : kind == "leave" ? "[LEFT]"
                : kind == "defeat" ? "[DOWN]"
                : kind == "hit" ? "[KILL]"
                : "[EVENT]";
        }

        private readonly struct FeedLine
        {
            public readonly string Kind;
            public readonly string Text;
            public readonly float ExpiresAt;

            public FeedLine(string kind, string text, float expiresAt)
            {
                Kind = kind;
                Text = text;
                ExpiresAt = expiresAt;
            }
        }
    }
}
