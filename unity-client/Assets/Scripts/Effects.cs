using UnityEngine;

namespace EasyGame
{
    public static class Effects
    {
        private static Material particleMaterial;
        private static Texture2D softCircleTexture;
        private static Sprite softCircleSprite;

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
            position += Vector3.up * 0.12f;
            GameObject fireball = new GameObject("2D Layered Rocket Explosion");
            fireball.transform.position = position;
            fireball.AddComponent<ExplosionDiscFx>();

            Burst(position, 34, new Color(1f, 0.82f, 0.2f), new Color(0.9f, 0.16f, 0.025f), 0.48f, 4.2f, 0.045f, 0.16f);
            Burst(position, 46, new Color(1f, 0.38f, 0.05f), new Color(0.24f, 0.09f, 0.035f), 0.74f, 5.4f, 0.025f, 0.085f);
            ParticleSystem smoke = Burst(position, 18, new Color(0.23f, 0.2f, 0.16f, 0.72f), new Color(0.07f, 0.07f, 0.06f, 0f), 1.05f, 1.1f, 0.14f, 0.38f);
            ParticleSystem.MainModule smokeMain = smoke.main;
            smokeMain.gravityModifier = -0.08f;

            GameObject wave = new GameObject("Explosion Shockwave");
            wave.transform.position = position + Vector3.up * 0.02f;
            wave.AddComponent<ShockwaveFx>();
            cameraRig?.Shake(0.2f, 0.34f);
        }

        public static ParticleSystem CreateRocketTrail(Transform parent)
        {
            GameObject root = new GameObject("Rocket Trail");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0.22f, -0.28f);
            ParticleSystem system = root.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.095f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.maxParticles = 80;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 72f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f;
            shape.radius = 0.025f;
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient trailGradient = new Gradient();
            trailGradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.58f), 0f), new GradientColorKey(new Color(1f, 0.28f, 0.035f), 0.55f), new GradientColorKey(new Color(0.12f, 0.1f, 0.08f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.62f, 0.55f), new GradientAlphaKey(0f, 1f) });
            color.color = trailGradient;
            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 22;
            return system;
        }

        public static Sprite SoftCircleSprite()
        {
            if (softCircleSprite != null)
            {
                return softCircleSprite;
            }
            Texture2D texture = SoftCircleTexture();
            softCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width, 0u, SpriteMeshType.FullRect);
            softCircleSprite.name = "EasyGame Soft Circle";
            return softCircleSprite;
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
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 72;
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
            particleMaterial.mainTexture = SoftCircleTexture();
            return particleMaterial;
        }

        private static Texture2D SoftCircleTexture()
        {
            if (softCircleTexture != null)
            {
                return softCircleTexture;
            }
            const int size = 64;
            softCircleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "EasyGame Soft Particle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny));
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    softCircleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            softCircleTexture.Apply(false, true);
            return softCircleTexture;
        }
    }

    public sealed class ExplosionDiscFx : MonoBehaviour
    {
        private SpriteRenderer core;
        private SpriteRenderer fire;
        private SpriteRenderer smoke;
        private readonly SpriteRenderer[] lobes = new SpriteRenderer[5];
        private float age;

        private void Awake()
        {
            core = CreateLayer("White-hot Core", Vector3.zero, new Color(1f, 0.94f, 0.58f, 1f), 82);
            fire = CreateLayer("Orange Fireball", Vector3.zero, new Color(1f, 0.28f, 0.035f, 0.9f), 80);
            smoke = CreateLayer("Soot Halo", Vector3.zero, new Color(0.16f, 0.12f, 0.09f, 0.58f), 76);
            Vector3[] offsets =
            {
                new Vector3(-0.38f, 0.01f, 0.08f),
                new Vector3(0.31f, 0.012f, 0.24f),
                new Vector3(0.18f, 0.014f, -0.34f),
                new Vector3(-0.2f, 0.016f, -0.28f),
                new Vector3(0.43f, 0.018f, -0.06f)
            };
            for (int index = 0; index < lobes.Length; index++)
            {
                lobes[index] = CreateLayer($"Flame Lobe {index + 1}", offsets[index], new Color(1f, 0.48f, 0.06f, 0.82f), 79 - index % 2);
            }
        }

        private SpriteRenderer CreateLayer(string layerName, Vector3 localPosition, Color color, int order)
        {
            GameObject layer = new GameObject(layerName);
            layer.transform.SetParent(transform, false);
            layer.transform.localPosition = localPosition;
            layer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = Effects.SoftCircleSprite();
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float coreProgress = Mathf.Clamp01(age / 0.18f);
            float fireProgress = Mathf.Clamp01(age / 0.46f);
            float smokeProgress = Mathf.Clamp01(Mathf.Max(0f, age - 0.08f) / 0.72f);
            Animate(core, Mathf.Lerp(0.25f, 1.55f, EaseOut(coreProgress)), 1f - coreProgress);
            Animate(fire, Mathf.Lerp(0.42f, 2.65f, EaseOut(fireProgress)), (1f - fireProgress) * 0.9f);
            Animate(smoke, Mathf.Lerp(0.8f, 3.8f, EaseOut(smokeProgress)), (1f - smokeProgress) * 0.58f);
            for (int index = 0; index < lobes.Length; index++)
            {
                float staggered = Mathf.Clamp01((age - index * 0.018f) / (0.34f + index * 0.025f));
                Animate(lobes[index], Mathf.Lerp(0.28f, 1.25f + index * 0.07f, EaseOut(staggered)), (1f - staggered) * 0.82f);
            }
            if (age >= 0.82f)
            {
                Destroy(gameObject);
            }
        }

        private static void Animate(SpriteRenderer renderer, float scale, float alpha)
        {
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            Color color = renderer.color;
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
        }

        private static float EaseOut(float progress)
        {
            return 1f - Mathf.Pow(1f - progress, 3f);
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
        private float baseWidth;
        private Color baseColor;

        public void Configure(Vector3 start, Vector3 end, bool impacted, string weapon)
        {
            origin = start;
            destination = end;
            duration = weapon == "shotgun" ? 0.065f : 0.085f;

            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 6;
            line.numCornerVertices = 0;
            baseWidth = weapon == "shotgun" ? 0.01f : 0.018f;
            line.widthMultiplier = baseWidth;
            baseColor = impacted ? new Color(1f, 0.55f, 0.14f) : new Color(1f, 0.86f, 0.36f);
            line.sharedMaterial = VisualFactory.Material(baseColor, true);
            line.SetPosition(0, origin);
            line.SetPosition(1, destination);
            if (impacted)
            {
                Effects.ImpactSpark(destination);
            }
        }

        private void Update()
        {
            if (line == null)
            {
                return;
            }
            age += Time.deltaTime;
            float progress = Mathf.Clamp01(age / Mathf.Max(0.01f, duration));
            float alpha = 1f - progress;
            Color fading = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            line.startColor = fading;
            line.endColor = fading;
            line.widthMultiplier = baseWidth * Mathf.Lerp(1f, 0.45f, progress);

            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
