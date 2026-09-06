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
        private const int MaxAutomaticRetries = 4;
        private const double ConnectionTimeoutSeconds = 15d;
        private bool clientRequested;
        private bool quitting;
        private int connectionFailures;
        private double connectionDeadline;
        private double retryAt = double.PositiveInfinity;

        public bool CanRetryConnection => clientRequested && !quitting &&
            !NetworkClient.active && mode == NetworkManagerMode.Offline;

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
                return QueryValue("offline") != "1";
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

            clientRequested = true;
            BeginConnection();
        }

        public override void Update()
        {
            base.Update();
            if (NetworkClient.isConnected)
            {
                if (NetworkClient.localPlayer != null)
                {
                    connectionFailures = 0;
                    ConnectionStatus = $"ONLINE  {Mathf.RoundToInt((float)(NetworkTime.rtt * 1000d))} MS  //  {ConnectedPlayerCount()}/4  //  PVP";
                }
                else if (clientRequested && Time.realtimeSinceStartupAsDouble >= connectionDeadline)
                {
                    ConnectionStatus = "SYNCHRONIZATION TIMED OUT";
                    StopClient();
                }
            }
            else if (NetworkServer.active)
            {
                ConnectionStatus = $"SERVER  //  {numPlayers}/4 PLAYERS";
            }
            else if (clientRequested && !quitting)
            {
                if (NetworkClient.isConnecting && Time.realtimeSinceStartupAsDouble >= connectionDeadline)
                {
                    ConnectionStatus = "CONNECTION TIMED OUT";
                    StopClient();
                }
                else if (!double.IsPositiveInfinity(retryAt) && CanRetryConnection)
                {
                    double remaining = retryAt - Time.realtimeSinceStartupAsDouble;
                    if (remaining <= 0d) BeginConnection();
                    else ConnectionStatus = $"RECONNECTING IN {Math.Ceiling(remaining)}s  //  {connectionFailures}/{MaxAutomaticRetries}";
                }
            }
        }

        public override void OnClientConnect()
        {
            ConnectionStatus = "ONLINE  //  SYNCHRONIZING";
            connectionDeadline = Time.realtimeSinceStartupAsDouble + ConnectionTimeoutSeconds;
            retryAt = double.PositiveInfinity;
            base.OnClientConnect();
        }

        public override void OnClientDisconnect()
        {
            ConnectionStatus = "CONNECTION LOST";
            MobileInputBridge.ResetState();
            base.OnClientDisconnect();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!clientRequested || quitting || NetworkServer.active) return;

            connectionFailures++;
            if (connectionFailures <= MaxAutomaticRetries)
                retryAt = Time.realtimeSinceStartupAsDouble + Math.Min(16d, Math.Pow(2d, connectionFailures));
            else
            {
                retryAt = double.PositiveInfinity;
                ConnectionStatus = "UNABLE TO CONNECT  //  RETRY WHEN READY";
            }
        }

        public void RetryConnection()
        {
            if (!CanRetryConnection) return;
            connectionFailures = 0;
            BeginConnection();
        }

        public override void OnApplicationQuit()
        {
            quitting = true;
            base.OnApplicationQuit();
        }

        private void BeginConnection()
        {
            retryAt = double.PositiveInfinity;
            Uri serverUri = ResolveServerUri();
            connectionDeadline = Time.realtimeSinceStartupAsDouble + ConnectionTimeoutSeconds;
            ConnectionStatus = $"CONNECTING  {serverUri.Host}";
            StartClient(serverUri);
        }

        public override void OnClientError(TransportError error, string reason)
        {
            ConnectionStatus = $"NETWORK ERROR  //  {error}";
            Debug.LogWarning($"Mirror client error: {error} - {reason}");
            if (NetworkClient.active) StopClient();
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
            if (!string.IsNullOrWhiteSpace(overrideUri) && Uri.TryCreate(overrideUri, UriKind.Absolute, out Uri parsed) &&
                (parsed.Scheme == "ws" || parsed.Scheme == "wss") && !string.IsNullOrWhiteSpace(parsed.Host) &&
                string.IsNullOrEmpty(parsed.UserInfo) && string.IsNullOrEmpty(parsed.Fragment) &&
                (!Application.absoluteURL.StartsWith("https:", StringComparison.OrdinalIgnoreCase) || parsed.Scheme == "wss"))
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

            int fragmentIndex = url.IndexOf('#', queryIndex);
            string query = fragmentIndex < 0 ? url.Substring(queryIndex + 1) : url.Substring(queryIndex + 1, fragmentIndex - queryIndex - 1);
            string[] pairs = query.Split('&');
            foreach (string pair in pairs)
            {
                string[] parts = pair.Split(new[] { '=' }, 2);
                if (parts.Length == 2 && parts[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    try { return Uri.UnescapeDataString(parts[1].Replace('+', ' ')); }
                    catch (UriFormatException) { return null; }
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
