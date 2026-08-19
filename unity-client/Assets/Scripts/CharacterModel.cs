using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EasyGame
{
    public sealed class CharacterModel : MonoBehaviour
    {
        private const float WalkCycleRate = 6.6f;
        private static readonly string[] PlayerLooks =
        {
            "Survivor1/survivor1",
            "Soldier1/soldier1",
            "ManBlue/manBlue",
            "ManBrown/manBrown",
            "ManOld/manOld",
            "WomanGreen/womanGreen",
            "Hitman1/hitman1"
        };

        private readonly Dictionary<string, Sprite> weaponSprites = new Dictionary<string, Sprite>();
        private Transform modelRoot;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer launcherRenderer;
        private Transform muzzle;
        private Quaternion facingTarget = Quaternion.identity;
        private string appearancePrefix;
        private string currentWeapon = "smg";
        private string currentSkin = "default";
        private string pendingWeapon;
        private float baseModelScale = 1f;
        private float walkPhase;
        private float movementAmount;
        private float fireUntil;
        private float recoil;
        private float switchClock;
        private bool zombie;
        private bool initialized;
        private bool respawning;

        public Transform Muzzle => muzzle;
        public string CurrentWeapon => currentWeapon;

        public void BuildPlayer(Color uniformColor, string characterId, string spawnSkin = "default")
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            zombie = false;
            currentSkin = NormalizeSkin(spawnSkin);
            appearancePrefix = PlayerAppearance(characterId, currentSkin);
            baseModelScale = currentSkin == "usagi" ? 1.34f : 1.26f;
            BuildSpriteBody(Color.Lerp(Color.white, uniformColor, 0.08f));
            LoadWeaponSprites();
            ActivateWeapon("smg");
        }

        public void SetPlayerAppearance(Color uniformColor, string characterId, string spawnSkin)
        {
            if (zombie)
            {
                return;
            }
            string normalized = NormalizeSkin(spawnSkin);
            if (normalized == currentSkin)
            {
                return;
            }

            currentSkin = normalized;
            appearancePrefix = PlayerAppearance(characterId, currentSkin);
            baseModelScale = currentSkin == "usagi" ? 1.34f : 1.26f;
            LoadWeaponSprites();
            ActivateWeapon(currentWeapon);
            bodyRenderer.color = currentSkin == "usagi"
                ? new Color(1f, 0.91f, 0.6f)
                : Color.Lerp(Color.white, uniformColor, 0.08f);
        }

        public void BuildZombie(string kind)
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            zombie = true;
            appearancePrefix = "Zombie1/zoimbie1";
            baseModelScale = kind == "brute" ? 1.48f : kind == "runner" ? 1.14f : 1.28f;
            Color tint = kind == "runner"
                ? new Color(1f, 0.82f, 0.72f)
                : kind == "brute"
                    ? new Color(0.76f, 0.92f, 0.68f)
                    : Color.white;
            BuildSpriteBody(tint);
            bodyRenderer.sprite = TopDownArt.LoadSprite($"Characters/{appearancePrefix}_hold");
            currentWeapon = "none";
            UpdateMuzzleOffset();
        }

        public void ApplyMotion(string direction, float speed, bool attacking, bool isRespawning, float aimX = 0f, float aimY = 0f)
        {
            facingTarget = AimRotation(direction, aimX, aimY);
            movementAmount = Mathf.Clamp01(speed / (zombie ? 0.9f : 2.05f));
            respawning = isRespawning;
            if (attacking)
            {
                fireUntil = Mathf.Max(fireUntil, Time.time + 0.08f);
            }
        }

        public void RequestWeapon(string weapon)
        {
            if (zombie || string.IsNullOrEmpty(weapon) || weapon == currentWeapon || weapon == pendingWeapon)
            {
                return;
            }
            if (!weaponSprites.ContainsKey(weapon))
            {
                return;
            }
            pendingWeapon = weapon;
            switchClock = 0f;
        }

        public void TriggerFire(string weapon = null, string direction = null, float aimX = 0f, float aimY = 0f)
        {
            if (zombie)
            {
                return;
            }
            if (!string.IsNullOrEmpty(weapon) && weaponSprites.ContainsKey(weapon) && currentWeapon != weapon)
            {
                pendingWeapon = null;
                ActivateWeapon(weapon);
            }
            if (!string.IsNullOrEmpty(direction))
            {
                facingTarget = AimRotation(direction, aimX, aimY);
                transform.rotation = facingTarget;
            }
            fireUntil = Time.time + 0.16f;
            recoil = 1f;
        }

        private void Update()
        {
            if (!initialized || modelRoot == null)
            {
                return;
            }

            float turnSharpness = 1f - Mathf.Exp(-22f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, facingTarget, turnSharpness);
            UpdateWeaponSwitch();
            UpdatePose();

            float targetScale = respawning ? 0f : baseModelScale;
            Vector3 stableScale = Vector3.one * targetScale;
            modelRoot.localScale = Vector3.Lerp(modelRoot.localScale, stableScale, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        private void BuildSpriteBody(Color tint)
        {
            CreateShadow();
            modelRoot = VisualFactory.Empty(transform, "Complete2DCharacter", new Vector3(0f, 0.055f, 0f));
            modelRoot.localRotation = Quaternion.Euler(90f, 0f, 90f);

            GameObject bodyObject = new GameObject("Unified Character Sprite");
            bodyObject.transform.SetParent(modelRoot, false);
            bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sortingOrder = 20;
            bodyRenderer.color = tint;
            bodyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            bodyRenderer.receiveShadows = false;

            GameObject launcherObject = new GameObject("Rocket Launcher Overlay");
            launcherObject.transform.SetParent(modelRoot, false);
            launcherObject.transform.localPosition = new Vector3(0.19f, -0.08f, -0.012f);
            launcherObject.transform.localScale = new Vector3(1.35f, 1.35f, 1f);
            launcherRenderer = launcherObject.AddComponent<SpriteRenderer>();
            launcherRenderer.sprite = TopDownArt.LoadSprite("Weapons/weapon_silencer");
            launcherRenderer.color = new Color(0.58f, 0.68f, 0.37f);
            launcherRenderer.sortingOrder = 21;
            launcherRenderer.enabled = false;
            launcherRenderer.shadowCastingMode = ShadowCastingMode.Off;
            launcherRenderer.receiveShadows = false;

            muzzle = VisualFactory.Empty(transform, "Muzzle", new Vector3(0f, 0.16f, 0.62f));
            modelRoot.localScale = Vector3.one * baseModelScale;
        }

        private void CreateShadow()
        {
            SpriteRenderer shadow = TopDownArt.CreateWorldSprite(
                transform,
                "Character Shadow",
                "Tiles/tile_243",
                new Vector3(0f, 0.012f, 0f),
                new Vector2(0.72f, 0.44f),
                12,
                0f,
                new Color(0.04f, 0.055f, 0.045f, 0.38f));
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void LoadWeaponSprites()
        {
            weaponSprites.Clear();
            weaponSprites["smg"] = TopDownArt.LoadSprite($"Characters/{appearancePrefix}_machine");
            weaponSprites["shotgun"] = TopDownArt.LoadSprite($"Characters/{appearancePrefix}_gun");
            weaponSprites["rocket"] = TopDownArt.LoadSprite($"Characters/{appearancePrefix}_hold");
        }

        private void UpdateWeaponSwitch()
        {
            if (string.IsNullOrEmpty(pendingWeapon))
            {
                return;
            }
            switchClock += Time.deltaTime;
            if (switchClock >= 0.11f && currentWeapon != pendingWeapon)
            {
                ActivateWeapon(pendingWeapon);
            }
            if (switchClock >= 0.26f)
            {
                pendingWeapon = null;
                switchClock = 0f;
            }
        }

        private void ActivateWeapon(string weapon)
        {
            if (!weaponSprites.TryGetValue(weapon, out Sprite sprite))
            {
                weapon = "smg";
                sprite = weaponSprites[weapon];
            }
            currentWeapon = weapon;
            bodyRenderer.sprite = sprite;
            launcherRenderer.enabled = weapon == "rocket";
            UpdateMuzzleOffset();
        }

        private void UpdateMuzzleOffset()
        {
            if (muzzle == null)
            {
                return;
            }
            float reach = currentWeapon == "shotgun" ? 0.74f : currentWeapon == "rocket" ? 0.78f : 0.66f;
            muzzle.localPosition = new Vector3(0f, 0.16f, reach * baseModelScale);
        }

        private void UpdatePose()
        {
            if (movementAmount > 0.02f)
            {
                walkPhase += Time.deltaTime * Mathf.Lerp(3.8f, WalkCycleRate, movementAmount);
            }

            float step = Mathf.Sin(walkPhase * Mathf.PI * 2f);
            float stride = movementAmount;
            float switchLowering = !string.IsNullOrEmpty(pendingWeapon)
                ? Mathf.Sin(Mathf.Clamp01(switchClock / 0.26f) * Mathf.PI)
                : 0f;
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 11f);

            float lean = step * (zombie ? 2.8f : 1.8f) * stride;
            float sideStep = step * (zombie ? 0.022f : 0.016f) * stride;
            float recoilOffset = zombie ? 0f : recoil * 0.035f;
            modelRoot.localPosition = new Vector3(sideStep, 0.055f, -recoilOffset);
            modelRoot.localRotation = Quaternion.Euler(90f, 0f, 90f + lean);

            if (launcherRenderer != null && launcherRenderer.enabled)
            {
                launcherRenderer.transform.localPosition = new Vector3(0.19f - recoil * 0.025f, -0.08f, -0.012f);
            }
            if (!zombie && Time.time < fireUntil && switchLowering < 0.1f && currentSkin == "usagi")
            {
                bodyRenderer.color = new Color(1f, 0.94f, 0.72f);
            }
        }

        private static string PlayerAppearance(string characterId, string spawnSkin)
        {
            if (spawnSkin == "usagi")
            {
                return "WomanGreen/womanGreen";
            }
            int hash = StableHash(characterId ?? "survivor");
            return PlayerLooks[Math.Abs(hash % PlayerLooks.Length)];
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                foreach (char character in value)
                {
                    hash = hash * 31 + character;
                }
                return hash == int.MinValue ? 0 : hash;
            }
        }

        private static string NormalizeSkin(string spawnSkin)
        {
            return spawnSkin == "usagi" ? "usagi" : "default";
        }

        private static Quaternion AimRotation(string direction, float aimX, float aimY)
        {
            Vector3 continuousAim = new Vector3(aimX, 0f, -aimY);
            return continuousAim.sqrMagnitude >= 0.01f
                ? Quaternion.LookRotation(continuousAim.normalized, Vector3.up)
                : GameCoordinates.DirectionRotation(direction);
        }
    }
}
