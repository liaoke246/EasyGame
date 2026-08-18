using UnityEngine;

namespace EasyGame
{
    public static class Effects
    {
        private static Material particleMaterial;

        public static void MuzzleFlash(Transform muzzle, string weapon)
        {
            if (muzzle == null)
            {
                return;
            }
            GameObject root = new GameObject("Muzzle Flash");
            root.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
            root.AddComponent<FxLifetime>().Lifetime = 0.12f;
            Color core = weapon == "rocket" ? new Color(1f, 0.35f, 0.08f) : new Color(1f, 0.78f, 0.2f);
            Transform flash = VisualFactory.Sphere(root.transform, "Core", Vector3.forward * 0.08f, weapon == "shotgun" ? new Vector3(0.22f, 0.14f, 0.34f) : new Vector3(0.14f, 0.1f, 0.26f), core);
            VisualFactory.Tint(flash, core, true);
            ParticleSystem sparks = Burst(root.transform.position + root.transform.forward * 0.1f, 14, core, new Color(1f, 0.18f, 0.03f), 0.12f, 1.8f, 0.035f, 0.08f);
            sparks.transform.rotation = muzzle.rotation;
            Light light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = weapon == "rocket" ? 3.2f : 2f;
            light.intensity = weapon == "rocket" ? 4f : 2.4f;
            light.color = core;
        }

        public static void Tracer(Vector3 origin, Vector3 destination, bool hit, string weapon)
        {
            GameObject lineObject = new GameObject("Ballistic Tracer");
            destination.y = origin.y;
            lineObject.AddComponent<TracerFx>().Configure(origin, destination, hit, weapon);
        }

        public static void ImpactSpark(Vector3 position)
        {
            Burst(position, 8, new Color(1f, 0.58f, 0.16f), new Color(0.45f, 0.07f, 0.02f), 0.2f, 1.25f, 0.025f, 0.065f);
        }

        public static void Explosion(Vector3 position, CameraRig cameraRig)
        {
            position += Vector3.up * 0.18f;
            GameObject lightObject = new GameObject("Explosion Light");
            lightObject.transform.position = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.3f, 0.06f);
            light.range = 8f;
            light.intensity = 9f;
            FxLifetime lightLife = lightObject.AddComponent<FxLifetime>();
            lightLife.Lifetime = 0.34f;
            lightLife.FadeLight = true;

            Burst(position, 44, new Color(1f, 0.8f, 0.16f), new Color(0.9f, 0.12f, 0.015f), 0.48f, 4.8f, 0.07f, 0.24f);
            Burst(position, 70, new Color(1f, 0.35f, 0.04f), new Color(0.2f, 0.08f, 0.03f), 0.72f, 6.4f, 0.025f, 0.09f);
            ParticleSystem smoke = Burst(position, 24, new Color(0.27f, 0.22f, 0.18f, 0.82f), new Color(0.08f, 0.07f, 0.065f, 0f), 1.25f, 1.35f, 0.18f, 0.48f);
            ParticleSystem.MainModule smokeMain = smoke.main;
            smokeMain.gravityModifier = -0.16f;

            GameObject wave = new GameObject("Explosion Shockwave");
            wave.transform.position = position + Vector3.up * 0.02f;
            wave.AddComponent<ShockwaveFx>();
            cameraRig?.Shake(0.17f, 0.3f);
        }

        public static ParticleSystem CreateRocketTrail(Transform parent)
        {
            GameObject root = new GameObject("Rocket Trail");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0.22f, -0.28f);
            ParticleSystem system = root.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.65f, 0.08f), new Color(0.32f, 0.24f, 0.2f, 0.25f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 48f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.04f;
            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial();
            return system;
        }

        private static ParticleSystem Burst(Vector3 position, int count, Color start, Color end, float lifetime, float speed, float minSize, float maxSize)
        {
            GameObject root = new GameObject("Particle Burst");
            root.transform.position = position;
            ParticleSystem system = root.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.65f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(start, end);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.55f;
            main.maxParticles = Mathf.Max(count, 16);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial();
            system.Emit(count);
            Object.Destroy(root, lifetime + 0.35f);
            return system;
        }

        private static Material ParticleMaterial()
        {
            if (particleMaterial != null)
            {
                return particleMaterial;
            }
            Shader shader = Resources.Load<Shader>("EasyGameParticles");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            particleMaterial = new Material(shader) { color = Color.white, name = "Runtime Particle Material" };
            return particleMaterial;
        }
    }

    public sealed class FxLifetime : MonoBehaviour
    {
        public float Lifetime = 0.2f;
        public bool FadeLight;
        private float createdAt;
        private float initialIntensity;
        private Light attachedLight;

        private void Awake()
        {
            createdAt = Time.time;
        }

        private void Start()
        {
            attachedLight = GetComponent<Light>();
            initialIntensity = attachedLight != null ? attachedLight.intensity : 0f;
        }

        private void Update()
        {
            float progress = Mathf.Clamp01((Time.time - createdAt) / Mathf.Max(0.01f, Lifetime));
            if (FadeLight && attachedLight != null)
            {
                attachedLight.intensity = initialIntensity * (1f - progress);
            }
            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }

    public sealed class ShockwaveFx : MonoBehaviour
    {
        private LineRenderer line;
        private float age;

        private void Awake()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 65;
            line.widthMultiplier = 0.09f;
            line.numCornerVertices = 3;
            line.sharedMaterial = VisualFactory.Material(new Color(1f, 0.35f, 0.05f), true);
            for (int index = 0; index < 65; index++)
            {
                float angle = index / 64f * Mathf.PI * 2f;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            float progress = Mathf.Clamp01(age / 0.42f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 2.7f, 1f - Mathf.Pow(1f - progress, 3f));
            line.widthMultiplier = Mathf.Lerp(0.12f, 0.005f, progress);
            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }

    public sealed class TracerFx : MonoBehaviour
    {
        private LineRenderer line;
        private Vector3 origin;
        private Vector3 destination;
        private float duration;
        private float age;
        private bool hit;
        private bool completed;
        private float baseWidth;

        public void Configure(Vector3 start, Vector3 end, bool impacted, string weapon)
        {
            origin = start;
            destination = end;
            hit = impacted;
            float distance = Vector3.Distance(origin, destination);
            float speed = weapon == "shotgun" ? 92f : 76f;
            duration = Mathf.Clamp(distance / speed, 0.035f, weapon == "shotgun" ? 0.085f : 0.13f);

            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 6;
            baseWidth = weapon == "shotgun" ? 0.012f : 0.024f;
            line.widthMultiplier = baseWidth;
            Color color = impacted ? new Color(1f, 0.55f, 0.14f) : new Color(1f, 0.86f, 0.36f);
            line.sharedMaterial = VisualFactory.Material(color, true);
            line.SetPosition(0, origin);
            line.SetPosition(1, origin);
        }

        private void Update()
        {
            if (completed || line == null)
            {
                return;
            }
            age += Time.deltaTime;
            float progress = Mathf.Clamp01(age / Mathf.Max(0.01f, duration));
            float tailProgress = Mathf.Max(0f, progress - 0.24f);
            float easedHead = 1f - Mathf.Pow(1f - progress, 2f);
            line.SetPosition(0, Vector3.Lerp(origin, destination, tailProgress));
            line.SetPosition(1, Vector3.Lerp(origin, destination, easedHead));
            line.widthMultiplier = baseWidth * Mathf.Lerp(1f, 0.82f, progress);

            if (progress < 1f)
            {
                return;
            }
            completed = true;
            if (hit)
            {
                Effects.ImpactSpark(destination);
            }
            Destroy(gameObject, 0.035f);
        }
    }
}
