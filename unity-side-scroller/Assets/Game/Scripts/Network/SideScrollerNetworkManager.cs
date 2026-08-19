using System;
using EasyGame.SideScroller.Core;
using EasyGame.SideScroller.Enemies;
using EasyGame.SideScroller.World;
using Mirror;
using UnityEngine;

namespace EasyGame.SideScroller.Network
{
    public sealed class SideScrollerNetworkManager : NetworkManager
    {
        [SerializeField] private GameObject zombiePrefab;
        [SerializeField] private GameObject slimePrefab;

        public GameObject ZombiePrefab
        {
            get => zombiePrefab;
            set => zombiePrefab = value;
        }

        public GameObject SlimePrefab
        {
            get => slimePrefab;
            set => slimePrefab = value;
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
                ConnectionStatus = $"ONLINE  {Mathf.RoundToInt((float)(NetworkTime.rtt * 1000d))} MS  //  {ConnectedPlayerCount()}/4  //  PVP";
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
            if (zombiePrefab == null || slimePrefab == null)
            {
                Debug.LogWarning("Side-scroller enemy prefabs are not fully configured.");
                return;
            }

            Vector3[] feetPositions =
            {
                new Vector3(2f, 0f, 0f),
                new Vector3(15f, 2f, 0f),
                new Vector3(42f, 3f, 0f),
                new Vector3(71f, 3f, 0f),
                new Vector3(103f, 2f, 0f),
                new Vector3(112f, SideWorldBuilder.FloorSurfaceY, 0f),
            };

            foreach (Vector3 feetPosition in feetPositions)
            {
                Vector3 position = ActorGeometry2D.RootPositionForFeet(feetPosition, ActorGeometry2D.HumanoidFeetLocalY);
                GameObject zombie = Instantiate(zombiePrefab, position, Quaternion.identity);
                NetworkServer.Spawn(zombie);
            }

            Vector3[] slimeFeetPositions =
            {
                new Vector3(5f, SideWorldBuilder.FloorSurfaceY, 0f),
                new Vector3(17f, 2f, 0f),
                new Vector3(27f, SideWorldBuilder.FloorSurfaceY, 0f),
                new Vector3(32f, SideWorldBuilder.FloorSurfaceY, 0f),
                new Vector3(45f, 3f, 0f),
                new Vector3(56f, 1f, 0f),
                new Vector3(69f, 3f, 0f),
                new Vector3(84f, SideWorldBuilder.FloorSurfaceY, 0f),
                new Vector3(89f, SideWorldBuilder.FloorSurfaceY, 0f),
                new Vector3(101f, 2f, 0f),
                new Vector3(114f, SideWorldBuilder.FloorSurfaceY, 0f),
                new Vector3(120f, SideWorldBuilder.FloorSurfaceY, 0f),
            };

            foreach (Vector3 feetPosition in slimeFeetPositions)
            {
                Vector3 position = ActorGeometry2D.RootPositionForFeet(feetPosition, ActorGeometry2D.SlimeFeetLocalY);
                GameObject slime = Instantiate(slimePrefab, position, Quaternion.identity);
                NetworkServer.Spawn(slime);
            }
        }

        private static int ConnectedPlayerCount()
        {
            int count = 0;
            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (identity != null && identity.GetComponent<SideScrollerNetworkPlayer>() != null)
                {
                    count++;
                }
            }
            return count;
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
