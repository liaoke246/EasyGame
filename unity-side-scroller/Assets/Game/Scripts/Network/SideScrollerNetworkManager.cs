using System;
using Mirror;
using UnityEngine;

namespace EasyGame.SideScroller.Network
{
    public sealed class SideScrollerNetworkManager : NetworkManager
    {
        public static string ConnectionStatus { get; private set; } = "OFFLINE PRACTICE";

        public static bool NetworkingRequested
        {
            get
            {
#if UNITY_SERVER
                return true;
#elif UNITY_EDITOR
                return HasArgument("-networked");
#else
                return !Application.absoluteURL.Contains("offline=1", StringComparison.OrdinalIgnoreCase);
#endif
            }
        }

        public override void Start()
        {
            base.Start();
            if (Utils.IsHeadless())
            {
                ConnectionStatus = "DEDICATED SERVER";
                return;
            }

            if (!NetworkingRequested)
            {
                ConnectionStatus = "OFFLINE PRACTICE";
                return;
            }

            Uri serverUri = ResolveServerUri();
            ConnectionStatus = $"CONNECTING  {serverUri.Host}";
            StartClient(serverUri);
        }

        public override void Update()
        {
            base.Update();
            if (NetworkClient.isConnected)
            {
                ConnectionStatus = $"ONLINE  {Mathf.RoundToInt((float)(NetworkTime.rtt * 1000d))} MS  //  {NetworkClient.spawned.Count}/4";
            }
            else if (NetworkServer.active)
            {
                ConnectionStatus = $"SERVER  //  {numPlayers}/4 PLAYERS";
            }
        }

        public override void OnClientConnect()
        {
            ConnectionStatus = "ONLINE  //  SYNCHRONIZING";
            base.OnClientConnect();
        }

        public override void OnClientDisconnect()
        {
            ConnectionStatus = "CONNECTION LOST  //  RETRY FROM LOBBY";
            base.OnClientDisconnect();
        }

        public override void OnClientError(TransportError error, string reason)
        {
            ConnectionStatus = $"NETWORK ERROR  //  {error}";
            Debug.LogWarning($"Mirror client error: {error} - {reason}");
        }

        private static Uri ResolveServerUri()
        {
            string overrideUri = QueryValue("server");
            if (!string.IsNullOrWhiteSpace(overrideUri) && Uri.TryCreate(Uri.UnescapeDataString(overrideUri), UriKind.Absolute, out Uri parsed))
            {
                return parsed;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out Uri page))
            {
                string scheme = page.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
                UriBuilder builder = new UriBuilder(scheme, page.Host)
                {
                    Path = "/side-scroller-socket",
                };
                if (!page.IsDefaultPort)
                {
                    builder.Port = page.Port;
                }
                return builder.Uri;
            }
#endif
            return new Uri("ws://127.0.0.1:27777/");
        }

        private static string QueryValue(string key)
        {
            string url = Application.absoluteURL;
            int queryIndex = url.IndexOf('?');
            if (queryIndex < 0)
            {
                return null;
            }

            string[] pairs = url.Substring(queryIndex + 1).Split('&');
            foreach (string pair in pairs)
            {
                string[] parts = pair.Split(new[] { '=' }, 2);
                if (parts.Length == 2 && parts[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return parts[1];
                }
            }
            return null;
        }

        private static bool HasArgument(string value)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
