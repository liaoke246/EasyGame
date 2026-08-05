using System;

namespace EasyGame
{
    [Serializable]
    public sealed class PlayerIdentity
    {
        public string displayId;
        public string characterId;
        public string roleName;
        public string color;
    }

    [Serializable]
    public sealed class ObstacleState
    {
        public string id;
        public string type;
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class WorldDefinition
    {
        public float width;
        public float height;
        public ObstacleState[] obstacles;
    }

    [Serializable]
    public sealed class WelcomePayload
    {
        public string playerId;
        public string guestToken;
        public PlayerIdentity identity;
        public WorldDefinition world;
    }

    [Serializable]
    public sealed class PlayerState
    {
        public string id;
        public string displayId;
        public string characterId;
        public string roleName;
        public string color;
        public float x;
        public float y;
        public float vx;
        public float vy;
        public string direction;
        public int health;
        public int maxHealth;
        public bool attacking;
        public int kills;
        public bool respawning;
        public string weapon;
    }

    [Serializable]
    public sealed class ZombieState
    {
        public string id;
        public string kind;
        public float x;
        public float y;
        public float vx;
        public float vy;
        public string direction;
        public int health;
        public int maxHealth;
    }

    [Serializable]
    public sealed class RocketState
    {
        public string id;
        public string ownerId;
        public float x;
        public float y;
        public float vx;
        public float vy;
    }

    [Serializable]
    public sealed class WorldSnapshot
    {
        public long serverTime;
        public PlayerState[] players;
        public ZombieState[] zombies;
        public RocketState[] rockets;
    }

    [Serializable]
    public sealed class WeaponTrace
    {
        public float endX;
        public float endY;
        public bool hit;
    }

    [Serializable]
    public sealed class AttackEvent
    {
        public string attackerId;
        public string weapon;
        public string phase;
        public string direction;
        public float x;
        public float y;
        public string[] hitPlayerIds;
        public string[] hitZombieIds;
        public string[] killedZombieIds;
        public WeaponTrace[] traces;
    }

    [Serializable]
    public sealed class NotificationEvent
    {
        public string kind;
        public string text;
    }

    [Serializable]
    public sealed class InputPayload
    {
        public bool up;
        public bool down;
        public bool left;
        public bool right;
        public bool fire;
        public string weapon;
    }

    [Serializable]
    public sealed class ProbePayload
    {
        public int sequence;
        public long clientSentAt;
        public long serverTime;
    }

    public readonly struct NetworkStats
    {
        public readonly bool Connected;
        public readonly int LatencyMs;
        public readonly int PacketLossPercent;

        public NetworkStats(bool connected, int latencyMs, int packetLossPercent)
        {
            Connected = connected;
            LatencyMs = latencyMs;
            PacketLossPercent = packetLossPercent;
        }
    }
}
