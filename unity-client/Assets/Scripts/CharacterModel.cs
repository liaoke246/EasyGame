using System.Collections.Generic;
using UnityEngine;

namespace EasyGame
{
    public sealed class CharacterModel : MonoBehaviour
    {
        private const float WalkCycleRate = 7.8f;
        private readonly Dictionary<string, Transform> weapons = new Dictionary<string, Transform>();
        private Transform modelRoot;
        private Transform bodyRoot;
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform leftArm;
        private Transform rightArm;
        private Transform rightHandSocket;
        private Transform activeWeapon;
        private Transform muzzle;
        private Vector3 leftLegRestPosition;
        private Vector3 rightLegRestPosition;
        private float baseModelScale = 1f;
        private Vector3 weaponRestPosition;
        private Quaternion weaponRestRotation;
        private Quaternion facingTarget = Quaternion.identity;
        private string currentWeapon = "smg";
        private string pendingWeapon;
        private float walkPhase;
        private float movementAmount;
        private float firePose;
        private float fireUntil;
        private float recoil;
        private float switchClock;
        private bool zombie;
        private bool initialized;
        private bool respawning;

        public Transform Muzzle => muzzle;
        public string CurrentWeapon => currentWeapon;

        public void BuildPlayer(Color uniformColor, string characterId)
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            zombie = false;
            BuildBody(uniformColor, characterId, false);
            BuildWeapons();
            ActivateWeapon("smg");
        }

        public void BuildZombie(string kind)
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            zombie = true;
            Color coat = kind == "runner" ? new Color(0.42f, 0.25f, 0.2f) : new Color(0.25f, 0.32f, 0.22f);
            BuildBody(coat, kind, true);
            baseModelScale = kind == "brute" ? 1.22f : kind == "runner" ? 0.94f : 1f;
            modelRoot.localScale = Vector3.one * baseModelScale;
        }

        public void ApplyMotion(string direction, float speed, bool attacking, bool isRespawning)
        {
            facingTarget = GameCoordinates.DirectionRotation(direction);
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
            if (!weapons.ContainsKey(weapon))
            {
                return;
            }
            pendingWeapon = weapon;
            switchClock = 0f;
        }

        public void TriggerFire(string weapon = null, string direction = null)
        {
            if (zombie)
            {
                return;
            }
            if (!string.IsNullOrEmpty(weapon) && weapons.ContainsKey(weapon) && currentWeapon != weapon)
            {
                pendingWeapon = null;
                ActivateWeapon(weapon);
            }
            if (!string.IsNullOrEmpty(direction))
            {
                facingTarget = GameCoordinates.DirectionRotation(direction);
                transform.rotation = facingTarget;
            }
            fireUntil = Time.time + 0.22f;
            recoil = 1f;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            float turnSharpness = 1f - Mathf.Exp(-18f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, facingTarget, turnSharpness);
            UpdateWeaponSwitch();
            UpdatePose();

            float targetScale = respawning ? 0f : baseModelScale;
            modelRoot.localScale = Vector3.Lerp(modelRoot.localScale, Vector3.one * targetScale, 1f - Mathf.Exp(-9f * Time.deltaTime));
        }

        private void UpdateWeaponSwitch()
        {
            if (string.IsNullOrEmpty(pendingWeapon))
            {
                return;
            }
            switchClock += Time.deltaTime;
            if (switchClock >= 0.14f && currentWeapon != pendingWeapon)
            {
                ActivateWeapon(pendingWeapon);
            }
            if (switchClock >= 0.34f)
            {
                pendingWeapon = null;
                switchClock = 0f;
            }
        }

        private void UpdatePose()
        {
            float strideTarget = movementAmount;
            if (strideTarget > 0.02f)
            {
                walkPhase += Time.deltaTime * Mathf.Lerp(3.8f, WalkCycleRate, strideTarget);
            }
            float step = Mathf.Sin(walkPhase * Mathf.PI * 2f);
            float liftLeft = Mathf.Max(0f, Mathf.Sin(walkPhase * Mathf.PI * 2f + Mathf.PI * 0.5f));
            float liftRight = Mathf.Max(0f, Mathf.Sin(walkPhase * Mathf.PI * 2f - Mathf.PI * 0.5f));
            float swing = step * (zombie ? 23f : 31f) * strideTarget;

            leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            leftLeg.localPosition = leftLegRestPosition + Vector3.up * (liftLeft * 0.045f * strideTarget);
            rightLeg.localPosition = rightLegRestPosition + Vector3.up * (liftRight * 0.045f * strideTarget);

            if (zombie)
            {
                leftArm.localRotation = Quaternion.Euler(-67f - swing * 0.18f, 0f, -5f);
                rightArm.localRotation = Quaternion.Euler(-67f + swing * 0.18f, 0f, 5f);
                bodyRoot.localRotation = Quaternion.Euler(5f + Mathf.Abs(step) * 1.5f * strideTarget, 0f, -step * 2.2f * strideTarget);
                return;
            }

            bool switching = !string.IsNullOrEmpty(pendingWeapon);
            float targetFirePose = Time.time < fireUntil && !switching ? 1f : 0f;
            firePose = Mathf.MoveTowards(firePose, targetFirePose, Time.deltaTime * (targetFirePose > firePose ? 11f : 5f));
            float switchLowering = switching ? Mathf.Sin(Mathf.Clamp01(switchClock / 0.34f) * Mathf.PI) : 0f;
            float ready = Mathf.Clamp01(0.72f + firePose * 0.28f - switchLowering * 0.9f);
            float armSwing = swing * 0.1f * (1f - ready);
            leftArm.localRotation = Quaternion.Euler(Mathf.Lerp(armSwing, -64f, ready), -9f * ready, -4f);
            rightArm.localRotation = Quaternion.Euler(Mathf.Lerp(-armSwing, -62f, ready), 7f * ready, 4f);
            bodyRoot.localRotation = Quaternion.Euler(0f, 0f, -step * 1.6f * strideTarget);

            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 9f);
            if (activeWeapon != null)
            {
                activeWeapon.localPosition = weaponRestPosition + new Vector3(0f, 0f, -recoil * 0.065f);
                activeWeapon.localRotation = weaponRestRotation * Quaternion.Euler(-recoil * 5f, 0f, 0f);
            }
        }

        private void BuildBody(Color uniform, string variant, bool isZombie)
        {
            modelRoot = VisualFactory.Empty(transform, "CompleteCharacter", Vector3.zero);
            bodyRoot = VisualFactory.Empty(modelRoot, "Spine", new Vector3(0f, 0.72f, 0f));
            Color outline = new Color(0.075f, 0.07f, 0.06f);
            Color clothDark = Color.Lerp(uniform, Color.black, 0.36f);
            Color clothLight = Color.Lerp(uniform, Color.white, 0.2f);
            Color skin = isZombie ? new Color(0.48f, 0.58f, 0.33f) : new Color(0.62f, 0.43f, 0.3f);

            VisualFactory.Box(bodyRoot, "TorsoShadow", new Vector3(0f, 0.03f, 0f), new Vector3(0.58f, 0.58f, 0.36f), outline);
            VisualFactory.Box(bodyRoot, "Jacket", new Vector3(0f, 0.055f, 0.025f), new Vector3(0.52f, 0.52f, 0.34f), uniform);
            VisualFactory.Box(bodyRoot, "ChestPanel", new Vector3(0f, 0.08f, 0.205f), new Vector3(0.31f, 0.28f, 0.035f), clothLight);
            VisualFactory.Box(bodyRoot, "Belt", new Vector3(0f, -0.205f, 0.025f), new Vector3(0.56f, 0.105f, 0.37f), new Color(0.18f, 0.13f, 0.085f));
            VisualFactory.Box(bodyRoot, "Buckle", new Vector3(0f, -0.2f, 0.218f), new Vector3(0.12f, 0.085f, 0.025f), new Color(0.56f, 0.45f, 0.24f));
            VisualFactory.Box(bodyRoot, "Backpack", new Vector3(0f, 0.05f, -0.235f), new Vector3(0.42f, 0.46f, 0.18f), clothDark);
            VisualFactory.Box(bodyRoot, "LeftPouch", new Vector3(-0.23f, -0.25f, 0.13f), new Vector3(0.13f, 0.16f, 0.12f), clothDark);
            VisualFactory.Box(bodyRoot, "RightPouch", new Vector3(0.23f, -0.25f, 0.13f), new Vector3(0.13f, 0.16f, 0.12f), clothDark);

            Transform neck = VisualFactory.Empty(bodyRoot, "Neck", new Vector3(0f, 0.41f, 0f));
            VisualFactory.Box(neck, "Head", new Vector3(0f, 0.22f, 0f), new Vector3(0.42f, 0.42f, 0.4f), skin);
            VisualFactory.Box(neck, "Face", new Vector3(0f, 0.19f, 0.211f), new Vector3(0.33f, 0.22f, 0.02f), isZombie ? new Color(0.34f, 0.43f, 0.22f) : new Color(0.73f, 0.52f, 0.36f));
            VisualFactory.Box(neck, "LeftEye", new Vector3(-0.095f, 0.23f, 0.226f), new Vector3(0.055f, 0.04f, 0.018f), isZombie ? new Color(0.9f, 0.18f, 0.08f) : outline);
            VisualFactory.Box(neck, "RightEye", new Vector3(0.095f, 0.23f, 0.226f), new Vector3(0.055f, 0.04f, 0.018f), isZombie ? new Color(0.9f, 0.18f, 0.08f) : outline);
            Color headwear = isZombie ? clothDark : CharacterHeadwear(variant);
            VisualFactory.Box(neck, "Headwear", new Vector3(0f, 0.43f, -0.005f), new Vector3(0.48f, 0.16f, 0.44f), headwear);
            VisualFactory.Box(neck, "HeadwearBrim", new Vector3(0f, 0.34f, 0.16f), new Vector3(0.5f, 0.07f, 0.2f), headwear);
            if (isZombie)
            {
                VisualFactory.Box(neck, "Jaw", new Vector3(0.06f, 0.08f, 0.23f), new Vector3(0.25f, 0.09f, 0.07f), new Color(0.27f, 0.12f, 0.09f));
                VisualFactory.Box(bodyRoot, "ShirtTear", new Vector3(-0.11f, 0.07f, 0.226f), new Vector3(0.16f, 0.2f, 0.035f), new Color(0.31f, 0.09f, 0.07f));
            }

            leftLeg = BuildLeg(modelRoot, "LeftLeg", -0.15f, clothDark, outline);
            rightLeg = BuildLeg(modelRoot, "RightLeg", 0.15f, clothDark, outline);
            leftLegRestPosition = leftLeg.localPosition;
            rightLegRestPosition = rightLeg.localPosition;
            leftArm = BuildArm(bodyRoot, "LeftArm", -0.35f, uniform, skin, outline);
            rightArm = BuildArm(bodyRoot, "RightArm", 0.35f, uniform, skin, outline);
            rightHandSocket = VisualFactory.Empty(rightArm, "RightHandSocket", new Vector3(0f, -0.49f, 0f));
        }

        private static Transform BuildLeg(Transform parent, string name, float x, Color cloth, Color boot)
        {
            Transform pivot = VisualFactory.Empty(parent, name, new Vector3(x, 0.48f, 0f));
            VisualFactory.Box(pivot, "Trouser", new Vector3(0f, -0.19f, 0f), new Vector3(0.23f, 0.43f, 0.25f), cloth);
            VisualFactory.Box(pivot, "KneePad", new Vector3(0f, -0.26f, 0.14f), new Vector3(0.24f, 0.13f, 0.06f), new Color(0.13f, 0.15f, 0.13f));
            VisualFactory.Box(pivot, "Boot", new Vector3(0f, -0.43f, 0.07f), new Vector3(0.25f, 0.2f, 0.38f), boot);
            return pivot;
        }

        private static Transform BuildArm(Transform parent, string name, float x, Color sleeve, Color hand, Color glove)
        {
            Transform pivot = VisualFactory.Empty(parent, name, new Vector3(x, 0.29f, 0f));
            VisualFactory.Box(pivot, "Sleeve", new Vector3(0f, -0.2f, 0f), new Vector3(0.22f, 0.42f, 0.23f), sleeve);
            VisualFactory.Box(pivot, "Glove", new Vector3(0f, -0.46f, 0.015f), new Vector3(0.2f, 0.17f, 0.21f), glove);
            VisualFactory.Box(pivot, "HandInset", new Vector3(0f, -0.455f, 0.12f), new Vector3(0.13f, 0.1f, 0.04f), hand);
            return pivot;
        }

        private void BuildWeapons()
        {
            weapons["smg"] = BuildSmg();
            weapons["shotgun"] = BuildShotgun();
            weapons["rocket"] = BuildRocketLauncher();
            foreach (Transform weapon in weapons.Values)
            {
                weapon.gameObject.SetActive(false);
            }
        }

        private Transform BuildSmg()
        {
            Transform root = WeaponRoot("SMG");
            root.localScale = Vector3.one * 0.72f;
            Color metal = new Color(0.25f, 0.28f, 0.27f);
            VisualFactory.Box(root, "Receiver", new Vector3(0f, 0f, 0.28f), new Vector3(0.16f, 0.17f, 0.48f), metal);
            VisualFactory.Box(root, "Stock", new Vector3(0f, 0.015f, -0.09f), new Vector3(0.12f, 0.15f, 0.28f), new Color(0.23f, 0.17f, 0.1f));
            VisualFactory.Box(root, "Magazine", new Vector3(0f, -0.17f, 0.25f), new Vector3(0.1f, 0.29f, 0.13f), new Color(0.08f, 0.09f, 0.085f));
            VisualFactory.Cylinder(root, "Barrel", new Vector3(0f, 0.015f, 0.69f), new Vector3(0.055f, 0.24f, 0.055f), metal).localRotation = Quaternion.Euler(90f, 0f, 0f);
            AddMuzzle(root, 0.94f);
            return root;
        }

        private Transform BuildShotgun()
        {
            Transform root = WeaponRoot("Shotgun");
            root.localScale = Vector3.one * 0.78f;
            Color steel = new Color(0.28f, 0.3f, 0.29f);
            Color wood = new Color(0.34f, 0.18f, 0.08f);
            VisualFactory.Box(root, "Stock", new Vector3(0f, 0f, -0.08f), new Vector3(0.16f, 0.18f, 0.42f), wood);
            VisualFactory.Box(root, "Receiver", new Vector3(0f, 0.015f, 0.28f), new Vector3(0.18f, 0.17f, 0.3f), steel);
            VisualFactory.Box(root, "Pump", new Vector3(0f, -0.01f, 0.57f), new Vector3(0.2f, 0.2f, 0.28f), wood);
            VisualFactory.Cylinder(root, "Barrel", new Vector3(0f, 0.04f, 0.78f), new Vector3(0.065f, 0.31f, 0.065f), steel).localRotation = Quaternion.Euler(90f, 0f, 0f);
            AddMuzzle(root, 1.09f);
            return root;
        }

        private Transform BuildRocketLauncher()
        {
            Transform root = WeaponRoot("RocketLauncher");
            root.localScale = Vector3.one * 0.84f;
            Color tube = new Color(0.25f, 0.3f, 0.19f);
            VisualFactory.Cylinder(root, "Tube", new Vector3(0f, 0.04f, 0.38f), new Vector3(0.14f, 0.55f, 0.14f), tube).localRotation = Quaternion.Euler(90f, 0f, 0f);
            VisualFactory.Cylinder(root, "RearRing", new Vector3(0f, 0.04f, -0.2f), new Vector3(0.19f, 0.1f, 0.19f), new Color(0.11f, 0.12f, 0.1f)).localRotation = Quaternion.Euler(90f, 0f, 0f);
            VisualFactory.Cylinder(root, "FrontRing", new Vector3(0f, 0.04f, 0.96f), new Vector3(0.19f, 0.1f, 0.19f), new Color(0.11f, 0.12f, 0.1f)).localRotation = Quaternion.Euler(90f, 0f, 0f);
            VisualFactory.Box(root, "Sight", new Vector3(0.11f, 0.18f, 0.42f), new Vector3(0.08f, 0.14f, 0.24f), new Color(0.08f, 0.09f, 0.08f));
            AddMuzzle(root, 1.08f);
            return root;
        }

        private Transform WeaponRoot(string name)
        {
            Transform root = VisualFactory.Empty(rightHandSocket, name, new Vector3(0f, -0.015f, 0.14f));
            root.localRotation = Quaternion.Euler(62f, 0f, 0f);
            return root;
        }

        private static void AddMuzzle(Transform weapon, float z)
        {
            Transform muzzle = VisualFactory.Empty(weapon, "Muzzle", new Vector3(0f, 0.02f, z));
            VisualFactory.Cylinder(muzzle, "MuzzleRing", Vector3.zero, new Vector3(0.08f, 0.025f, 0.08f), new Color(0.055f, 0.06f, 0.055f)).localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void ActivateWeapon(string weapon)
        {
            foreach (KeyValuePair<string, Transform> entry in weapons)
            {
                entry.Value.gameObject.SetActive(entry.Key == weapon);
            }
            currentWeapon = weapon;
            activeWeapon = weapons[weapon];
            muzzle = activeWeapon.Find("Muzzle");
            weaponRestPosition = activeWeapon.localPosition;
            weaponRestRotation = activeWeapon.localRotation;
        }

        private static Color CharacterHeadwear(string variant)
        {
            switch (variant)
            {
                case "farmer": return new Color(0.49f, 0.32f, 0.11f);
                case "herbalist": return new Color(0.35f, 0.2f, 0.45f);
                case "smith": return new Color(0.33f, 0.18f, 0.14f);
                default: return new Color(0.18f, 0.28f, 0.18f);
            }
        }
    }
}
