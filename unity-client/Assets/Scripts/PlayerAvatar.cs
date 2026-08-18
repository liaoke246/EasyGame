using UnityEngine;

namespace EasyGame
{
    public sealed class PlayerAvatar : MonoBehaviour
    {
        private CharacterModel model;
        private Vector3 networkPosition;
        private Vector3 predictedVelocity;
        private float worldHeight;
        private bool localPlayer;
        private bool receivedState;
        private Color uniformColor;
        private string characterId;
        private string spawnSkin = "default";
        private WorldHealthBar healthBar;

        public string PlayerId { get; private set; }
        public string DisplayId { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public int Kills { get; private set; }
        public bool Respawning { get; private set; }
        public string Weapon { get; private set; } = "smg";
        public Transform Muzzle => model != null ? model.Muzzle : transform;

        public void Initialize(PlayerState state, bool isLocal, float mapHeight)
        {
            PlayerId = state.id;
            DisplayId = state.displayId;
            localPlayer = isLocal;
            worldHeight = mapHeight;
            gameObject.name = isLocal ? $"Local Player {state.displayId}" : $"Remote Player {state.displayId}";
            uniformColor = GameCoordinates.ParseColor(state.color, new Color(0.25f, 0.55f, 0.36f));
            characterId = state.characterId;
            spawnSkin = NormalizeSkin(state.spawnSkin);
            model = gameObject.AddComponent<CharacterModel>();
            model.BuildPlayer(uniformColor, characterId, spawnSkin);
            GameObject barObject = new GameObject("Player Name And Health");
            barObject.transform.SetParent(transform, false);
            healthBar = barObject.AddComponent<WorldHealthBar>();
            healthBar.Initialize(DisplayId, 2.18f, isLocal ? new Color(0.28f, 0.95f, 0.42f) : new Color(0.28f, 0.72f, 1f));
            ApplyNetworkState(state, true);
        }

        public void ApplyNetworkState(PlayerState state, bool immediate = false)
        {
            networkPosition = GameCoordinates.ToUnity(state.x, state.y, worldHeight);
            if (!receivedState || immediate || Vector3.Distance(transform.position, networkPosition) > 1.25f)
            {
                transform.position = networkPosition;
                receivedState = true;
            }
            if (localPlayer)
            {
                predictedVelocity = new Vector3(state.vx, 0f, -state.vy) * GameCoordinates.WorldScale;
            }
            Health = state.health;
            MaxHealth = state.maxHealth;
            Kills = state.kills;
            Respawning = state.respawning;
            Weapon = string.IsNullOrEmpty(state.weapon) ? Weapon : state.weapon;
            string nextSkin = NormalizeSkin(state.spawnSkin);
            if (nextSkin != spawnSkin)
            {
                spawnSkin = nextSkin;
                model.SetPlayerAppearance(uniformColor, characterId, spawnSkin);
            }
            model.RequestWeapon(Weapon);
            float speed = Mathf.Sqrt(state.vx * state.vx + state.vy * state.vy) * GameCoordinates.WorldScale;
            model.ApplyMotion(state.direction, speed, state.attacking, state.respawning);
            healthBar.SetValue(Health, MaxHealth);
        }

        public void SimulateLocal(Vector2 input, string direction, bool firing, float deltaTime, WorldMap map)
        {
            if (!localPlayer)
            {
                return;
            }
            if (Respawning)
            {
                predictedVelocity = Vector3.zero;
                model.ApplyMotion(direction, 0f, false, true);
                return;
            }
            Vector3 desired = new Vector3(input.x, 0f, input.y) * 2.05f;
            bool reversing = Vector3.Dot(predictedVelocity, desired) < 0f;
            float acceleration = input.sqrMagnitude < 0.01f ? 36f : reversing ? 42f : 26f;
            predictedVelocity = Vector3.MoveTowards(predictedVelocity, desired, acceleration * deltaTime);
            if (predictedVelocity.magnitude > 2.05f)
            {
                predictedVelocity = predictedVelocity.normalized * 2.05f;
            }

            Vector3 next = transform.position + predictedVelocity * deltaTime;
            Vector3 xOnly = new Vector3(next.x, transform.position.y, transform.position.z);
            if (!map.IsBlocked(xOnly, 0.22f))
            {
                transform.position = xOnly;
            }
            else
            {
                predictedVelocity.x = 0f;
            }
            Vector3 zOnly = new Vector3(transform.position.x, transform.position.y, next.z);
            if (!map.IsBlocked(zOnly, 0.22f))
            {
                transform.position = zOnly;
            }
            else
            {
                predictedVelocity.z = 0f;
            }
            model.ApplyMotion(direction, predictedVelocity.magnitude, firing, false);
        }

        public void SelectWeapon(string weapon)
        {
            Weapon = weapon;
            model.RequestWeapon(weapon);
        }

        public void TriggerFire(string weapon = null, string direction = null)
        {
            model.TriggerFire(weapon, direction);
        }

        private void LateUpdate()
        {
            if (!receivedState)
            {
                return;
            }
            float sharpness = localPlayer ? 2.2f : 13f;
            transform.position = Vector3.Lerp(transform.position, networkPosition, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }

        private static string NormalizeSkin(string value)
        {
            return value == "usagi" ? "usagi" : "default";
        }
    }
}
