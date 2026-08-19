using System.Collections.Generic;
using UnityEngine;

namespace EasyGame
{
    public sealed class GameWorldController : MonoBehaviour
    {
        private readonly Dictionary<string, PlayerAvatar> players = new Dictionary<string, PlayerAvatar>();
        private readonly Dictionary<string, ZombieAvatar> zombies = new Dictionary<string, ZombieAvatar>();
        private readonly Dictionary<string, RocketVisual> rockets = new Dictionary<string, RocketVisual>();
        private WebSocketBridge network;
        private WorldMap map;
        private CameraRig cameraRig;
        private GameHud hud;
        private PlayerAvatar localPlayer;
        private string localPlayerId;
        private string selectedWeapon = "smg";
        private string lastDirection = "down";
        private float nextInputAt;
        private float nextOptimisticFireAt;
        private float weaponReadyAt;
        private Vector2 mobileMovement;
        private Vector2 mobileAim;
        private bool mobileFire;
        private bool demoMode;

        [System.Serializable]
        private sealed class MobileInputState
        {
            public float moveX;
            public float moveY;
            public float aimX;
            public float aimY;
            public bool fire;
            public string weapon;
        }

        public void Initialize(WebSocketBridge bridge, WorldMap worldMap, CameraRig rig, GameHud gameHud)
        {
            network = bridge;
            map = worldMap;
            cameraRig = rig;
            hud = gameHud;
            network.WelcomeReceived += OnWelcome;
            network.SnapshotReceived += OnSnapshot;
            network.AttackReceived += OnAttack;
            network.NotificationReceived += hud.AddNotification;
            network.StatsChanged += hud.SetStats;
        }

        private void Start()
        {
            if (WebSocketBridge.BrowserTransportAvailable)
            {
                hud.SetStatus("CONNECTING TO SURVIVAL SERVER");
                network.Connect();
            }
            else
            {
                StartEditorPreview();
            }
        }

        private void Update()
        {
            if (localPlayer == null)
            {
                return;
            }

            float keyboardHorizontal = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float keyboardVertical = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            Vector2 digitalMobileMovement = mobileMovement.sqrMagnitude > 0.02f ? mobileMovement.normalized : Vector2.zero;
            float horizontal = Mathf.Clamp(keyboardHorizontal + digitalMobileMovement.x, -1f, 1f);
            float vertical = Mathf.Clamp(keyboardVertical + digitalMobileMovement.y, -1f, 1f);
            Vector2 movement = new Vector2(horizontal, vertical);
            if (movement.sqrMagnitude > 1f)
            {
                movement.Normalize();
            }
            if (mobileAim.sqrMagnitude > 0.04f)
            {
                lastDirection = DirectionFromInput(mobileAim.x, mobileAim.y);
            }
            else if (movement.sqrMagnitude > 0.01f)
            {
                lastDirection = DirectionFromInput(horizontal, vertical);
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectWeapon("smg");
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectWeapon("shotgun");
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectWeapon("rocket");
            bool triggerHeld = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0) || mobileFire;
            bool firing = triggerHeld && Time.time >= weaponReadyAt;
            localPlayer.SimulateLocal(movement, lastDirection, firing, Time.deltaTime, map);
            if (firing)
            {
                OptimisticFire();
            }

            if (!demoMode && Time.unscaledTime >= nextInputAt)
            {
                nextInputAt = Time.unscaledTime + 1f / 30f;
                network.SendInput(new InputPayload
                {
                    up = vertical > 0.1f,
                    down = vertical < -0.1f,
                    left = horizontal < -0.1f,
                    right = horizontal > 0.1f,
                    fire = firing,
                    weapon = selectedWeapon,
                    direction = lastDirection
                });
            }
        }

        public void OnMobileInput(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                ResetMobileInput();
                return;
            }

            MobileInputState state = JsonUtility.FromJson<MobileInputState>(json);
            if (state == null)
            {
                ResetMobileInput();
                return;
            }

            mobileMovement = Vector2.ClampMagnitude(new Vector2(state.moveX, state.moveY), 1f);
            mobileAim = Vector2.ClampMagnitude(new Vector2(state.aimX, state.aimY), 1f);
            mobileFire = state.fire;
            if (state.weapon == "smg" || state.weapon == "shotgun" || state.weapon == "rocket")
            {
                SelectWeapon(state.weapon);
            }
        }

        public void ResetMobileInput()
        {
            mobileMovement = Vector2.zero;
            mobileAim = Vector2.zero;
            mobileFire = false;
        }

        private void SelectWeapon(string weapon)
        {
            if (weapon == selectedWeapon)
            {
                return;
            }
            selectedWeapon = weapon;
            weaponReadyAt = Time.time + 0.22f;
            localPlayer?.SelectWeapon(weapon);
        }

        private void OptimisticFire()
        {
            if (Time.time < nextOptimisticFireAt)
            {
                return;
            }
            float cooldown = selectedWeapon == "smg" ? 0.095f : selectedWeapon == "shotgun" ? 0.62f : 1.05f;
            nextOptimisticFireAt = Time.time + cooldown;
            localPlayer.TriggerFire(selectedWeapon, lastDirection);
            Effects.MuzzleFlash(localPlayer.Muzzle, selectedWeapon);
        }

        private void OnWelcome(WelcomePayload welcome)
        {
            localPlayerId = welcome.playerId;
            map.Build(welcome.world);
            hud.SetStatus("SYNCHRONIZING WORLD");
        }

        private void OnSnapshot(WorldSnapshot snapshot)
        {
            if (snapshot == null || map.ServerHeight <= 0f)
            {
                return;
            }
            SyncPlayers(snapshot.players ?? new PlayerState[0]);
            SyncZombies(snapshot.zombies ?? new ZombieState[0]);
            SyncRockets(snapshot.rockets ?? new RocketState[0]);
            hud.SetWorldCounts(players.Count, zombies.Count);
            if (localPlayer != null)
            {
                hud.SetReady("READY");
            }
        }

        private void SyncPlayers(PlayerState[] states)
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (PlayerState state in states)
            {
                seen.Add(state.id);
                if (!players.TryGetValue(state.id, out PlayerAvatar avatar))
                {
                    avatar = new GameObject("Player").AddComponent<PlayerAvatar>();
                    bool isLocal = state.id == localPlayerId;
                    avatar.Initialize(state, isLocal, map.ServerHeight);
                    players[state.id] = avatar;
                    if (isLocal)
                    {
                        localPlayer = avatar;
                        selectedWeapon = state.weapon;
                        cameraRig.Follow(avatar.transform);
                        hud.SetPlayer(avatar);
                    }
                }
                avatar.ApplyNetworkState(state);
            }
            RemoveMissing(players, seen);
        }

        private void SyncZombies(ZombieState[] states)
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (ZombieState state in states)
            {
                seen.Add(state.id);
                if (!zombies.TryGetValue(state.id, out ZombieAvatar avatar))
                {
                    avatar = new GameObject("Zombie").AddComponent<ZombieAvatar>();
                    avatar.Initialize(state, map.ServerHeight);
                    zombies[state.id] = avatar;
                }
                avatar.ApplyNetworkState(state);
            }
            RemoveMissing(zombies, seen);
        }

        private void SyncRockets(RocketState[] states)
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (RocketState state in states)
            {
                seen.Add(state.id);
                if (!rockets.TryGetValue(state.id, out RocketVisual rocket))
                {
                    rocket = new GameObject("Rocket").AddComponent<RocketVisual>();
                    Vector3? visualMuzzle = null;
                    if (players.TryGetValue(state.ownerId, out PlayerAvatar owner))
                    {
                        visualMuzzle = owner.Muzzle.position;
                    }
                    rocket.Initialize(state, map.ServerHeight, visualMuzzle);
                    rockets[state.id] = rocket;
                }
                rocket.ApplyNetworkState(state);
            }
            RemoveMissing(rockets, seen);
        }

        private void OnAttack(AttackEvent attack)
        {
            if (attack == null)
            {
                return;
            }
            if (attack.phase == "impact" && attack.weapon == "rocket")
            {
                Effects.Explosion(GameCoordinates.ToUnity(attack.x, attack.y, map.ServerHeight), cameraRig);
                return;
            }

            Vector3 origin = GameCoordinates.ToUnity(attack.x, attack.y, map.ServerHeight) + Vector3.up * 0.72f;
            if (players.TryGetValue(attack.attackerId, out PlayerAvatar attacker))
            {
                attacker.TriggerFire(attack.weapon, attack.direction);
                origin = attacker.Muzzle.position;
                if (attack.attackerId != localPlayerId)
                {
                    Effects.MuzzleFlash(attacker.Muzzle, attack.weapon);
                }
            }
            if (attack.traces == null)
            {
                return;
            }
            foreach (WeaponTrace trace in attack.traces)
            {
                Vector3 destination = GameCoordinates.ToUnity(trace.endX, trace.endY, map.ServerHeight);
                Effects.Tracer(origin, destination, trace.hit, attack.weapon);
            }
        }

        private void StartEditorPreview()
        {
            demoMode = true;
            localPlayerId = "editor-player";
            WorldDefinition world = new WorldDefinition
            {
                width = 3840f,
                height = 2160f,
                obstacles = new[]
                {
                    new ObstacleState { id = "preview-cabin", type = "cabin", x = 3080f, y = 260f, width = 340f, height = 250f, hitboxInset = 8f },
                    new ObstacleState { id = "preview-pond", type = "pond", x = 260f, y = 1330f, width = 520f, height = 310f, hitboxInset = 8f },
                    new ObstacleState { id = "preview-tree", type = "tree", x = 870f, y = 940f, width = 82f, height = 94f, hitboxInset = 22f },
                    new ObstacleState { id = "preview-rock", type = "rock", x = 2450f, y = 1430f, width = 68f, height = 52f, hitboxInset = 7f },
                    new ObstacleState { id = "preview-garden", type = "garden", x = 2650f, y = 1690f, width = 470f, height = 250f, hitboxInset = 10f }
                }
            };
            map.Build(world);
            OnSnapshot(new WorldSnapshot
            {
                players = new[]
                {
                    new PlayerState { id = localPlayerId, spawnSkin = "usagi", displayId = "NOVA-27", characterId = "ranger", roleName = "Ranger", color = "#4c956c", x = 1920f, y = 1080f, direction = "down", health = 100, maxHealth = 100, weapon = "smg" }
                },
                zombies = new[]
                {
                    new ZombieState { id = "walker", kind = "walker", x = 1700f, y = 960f, direction = "right", health = 70, maxHealth = 70 },
                    new ZombieState { id = "runner", kind = "runner", x = 2160f, y = 970f, direction = "left", health = 45, maxHealth = 45 },
                    new ZombieState { id = "brute", kind = "brute", x = 2250f, y = 1320f, direction = "up", health = 150, maxHealth = 150 }
                },
                rockets = new RocketState[0]
            });
            hud.SetReady("EDITOR VISUAL PREVIEW");
        }

        private static string DirectionFromInput(float x, float y)
        {
            string vertical = y > 0f ? "up" : "down";
            string horizontal = x > 0f ? "right" : "left";
            if (Mathf.Abs(x) > 0.01f && Mathf.Abs(y) > 0.01f)
            {
                return $"{vertical}-{horizontal}";
            }
            if (Mathf.Abs(x) > 0.01f)
            {
                return horizontal;
            }
            return vertical;
        }

        private static void RemoveMissing<T>(Dictionary<string, T> collection, HashSet<string> seen) where T : Component
        {
            List<string> missing = null;
            foreach (KeyValuePair<string, T> entry in collection)
            {
                if (!seen.Contains(entry.Key))
                {
                    missing ??= new List<string>();
                    missing.Add(entry.Key);
                }
            }
            if (missing == null)
            {
                return;
            }
            foreach (string id in missing)
            {
                Destroy(collection[id].gameObject);
                collection.Remove(id);
            }
        }
    }
}
