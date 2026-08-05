using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace EasyGame
{
    public sealed class WebSocketBridge : MonoBehaviour
    {
        private const float ProbeIntervalSeconds = 2f;
        private const float ProbeTimeoutSeconds = 4f;
        private readonly Dictionary<int, double> pendingProbes = new Dictionary<int, double>();
        private readonly Queue<bool> probeResults = new Queue<bool>();
        private int nextProbeSequence = 1;
        private float nextProbeAt;
        private float latencyMs = -1f;

        public event Action<WelcomePayload> WelcomeReceived;
        public event Action<WorldSnapshot> SnapshotReceived;
        public event Action<AttackEvent> AttackReceived;
        public event Action<NotificationEvent> NotificationReceived;
        public event Action<NetworkStats> StatsChanged;

        public bool Connected { get; private set; }

        public static bool BrowserTransportAvailable
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void EG_Connect(string receiverName);

        [DllImport("__Internal")]
        private static extern void EG_SendInput(string json);

        [DllImport("__Internal")]
        private static extern void EG_SendPing(string json);

        [DllImport("__Internal")]
        private static extern void EG_Disconnect();
#endif

        public void Connect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            EG_Connect(gameObject.name);
#endif
        }

        public void SendInput(InputPayload input)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Connected)
            {
                EG_SendInput(JsonUtility.ToJson(input));
            }
#endif
        }

        private void Update()
        {
            if (!Connected || Time.unscaledTime < nextProbeAt)
            {
                return;
            }

            nextProbeAt = Time.unscaledTime + ProbeIntervalSeconds;
            ExpireProbes();
            SendProbe();
        }

        private void SendProbe()
        {
            int sequence = nextProbeSequence++;
            pendingProbes[sequence] = Time.realtimeSinceStartupAsDouble;
            ProbePayload payload = new ProbePayload
            {
                sequence = sequence,
                clientSentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
#if UNITY_WEBGL && !UNITY_EDITOR
            EG_SendPing(JsonUtility.ToJson(payload));
#endif
        }

        private void ExpireProbes()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            List<int> expired = null;
            foreach (KeyValuePair<int, double> probe in pendingProbes)
            {
                if (now - probe.Value < ProbeTimeoutSeconds)
                {
                    continue;
                }
                expired ??= new List<int>();
                expired.Add(probe.Key);
            }

            if (expired == null)
            {
                return;
            }
            foreach (int sequence in expired)
            {
                pendingProbes.Remove(sequence);
                RecordProbe(false);
            }
        }

        private void RecordProbe(bool success)
        {
            probeResults.Enqueue(success);
            while (probeResults.Count > 20)
            {
                probeResults.Dequeue();
            }
            PublishStats();
        }

        private void PublishStats()
        {
            int lost = 0;
            foreach (bool result in probeResults)
            {
                if (!result)
                {
                    lost++;
                }
            }
            int loss = probeResults.Count == 0 ? 0 : Mathf.RoundToInt(lost * 100f / probeResults.Count);
            StatsChanged?.Invoke(new NetworkStats(Connected, Mathf.Max(0, Mathf.RoundToInt(latencyMs)), loss));
        }

        public void OnConnected(string _)
        {
            Connected = true;
            pendingProbes.Clear();
            nextProbeAt = 0f;
            PublishStats();
        }

        public void OnDisconnected(string _)
        {
            Connected = false;
            pendingProbes.Clear();
            PublishStats();
        }

        public void OnWelcome(string json)
        {
            WelcomeReceived?.Invoke(JsonUtility.FromJson<WelcomePayload>(json));
        }

        public void OnSnapshot(string json)
        {
            SnapshotReceived?.Invoke(JsonUtility.FromJson<WorldSnapshot>(json));
        }

        public void OnAttack(string json)
        {
            AttackReceived?.Invoke(JsonUtility.FromJson<AttackEvent>(json));
        }

        public void OnNotification(string json)
        {
            NotificationReceived?.Invoke(JsonUtility.FromJson<NotificationEvent>(json));
        }

        public void OnPong(string json)
        {
            ProbePayload pong = JsonUtility.FromJson<ProbePayload>(json);
            if (!pendingProbes.TryGetValue(pong.sequence, out double sentAt))
            {
                return;
            }
            pendingProbes.Remove(pong.sequence);
            float sample = (float)((Time.realtimeSinceStartupAsDouble - sentAt) * 1000d);
            latencyMs = latencyMs < 0f ? sample : Mathf.Lerp(latencyMs, sample, 0.3f);
            RecordProbe(true);
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            EG_Disconnect();
#endif
        }
    }
}
