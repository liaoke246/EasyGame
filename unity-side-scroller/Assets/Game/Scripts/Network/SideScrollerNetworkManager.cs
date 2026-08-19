using System;
using EasyGame.SideScroller.Enemies;
using Mirror;
using UnityEngine;

namespace EasyGame.SideScroller.Network
{
    public sealed class SideScrollerNetworkManager : NetworkManager
    {
        [SerializeField] private GameObject zombiePrefab;

        public GameObject ZombiePrefab
        {
            get => zombiePrefab;
            set => zombiePrefab = value;
        }

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

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (zombiePrefab == null)
            {
                Debug.LogWarning("No side-scroller zombie prefab is configured.");
                return;
            }

            Vector3[] positions =
            {
                new Vector3(2f, 0.15f, 0f),
                new Vector3(15f, 2.15f, 0f),
                new Vector3(28f, 0.15f, 0f),
                new Vector3(42f, 3.15f, 0f),
                new Vector3(57f, 1.15f, 0f),
                new Vector3(71f, 3.15f, 0f),
                new Vector3(86f, 0.15f, 0f),
                new Vector3(103f, 2.15f, 0f),
                new Vector3(112f, -2.15f, 0f),
                new Vector3(34f, -2.15f, 0f),
            };

            foreach (Vector3 position in positions)
            {
                GameObject zombie = Instantiate(zombiePrefab, position, Quaternion.identity);
                NetworkServer.Spawn(zombie);
            }
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
