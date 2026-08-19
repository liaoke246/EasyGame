using UnityEngine;

namespace EasyGame
{
    public sealed class RocketVisual : MonoBehaviour
    {
        private const float ProjectileHeight = 0.72f;
        private Vector3 targetPosition;
        private Vector3 networkVelocity;
        private ParticleSystem trail;
        private float worldHeight;
        private static Sprite rocketSprite;

        public string RocketId { get; private set; }

        public void Initialize(RocketState state, float mapHeight)
        {
            RocketId = state.id;
            worldHeight = mapHeight;
            gameObject.name = $"Rocket {state.id}";
            GameObject shell = new GameObject("2D Rocket Shell");
            shell.transform.SetParent(transform, false);
            shell.transform.localRotation = Quaternion.Euler(90f, 0f, 90f);
            shell.transform.localScale = new Vector3(0.72f, 0.72f, 1f);
            SpriteRenderer shellRenderer = shell.AddComponent<SpriteRenderer>();
            shellRenderer.sprite = CreateRocketSprite();
            shellRenderer.sortingOrder = 24;

            GameObject glow = new GameObject("Rocket Engine Glow");
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.015f, -0.32f);
            glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            glow.transform.localScale = new Vector3(0.32f, 0.32f, 1f);
            SpriteRenderer glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = Effects.SoftCircleSprite();
            glowRenderer.color = new Color(1f, 0.48f, 0.08f, 0.72f);
            glowRenderer.sortingOrder = 23;
            trail = Effects.CreateRocketTrail(transform);
            ApplyNetworkState(state, true);
        }

        public void ApplyNetworkState(RocketState state, bool immediate = false)
        {
            targetPosition = GameCoordinates.ToUnity(state.x, state.y, worldHeight) + Vector3.up * ProjectileHeight;
            networkVelocity = new Vector3(state.vx, 0f, -state.vy) * GameCoordinates.WorldScale;
            if (networkVelocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(networkVelocity.normalized, Vector3.up);
            }
            if (immediate)
            {
                transform.position = targetPosition;
                return;
            }
            if (networkVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 direction = networkVelocity.normalized;
                Vector3 error = targetPosition - transform.position;
                Vector3 lateralError = error - direction * Vector3.Dot(error, direction);
                transform.position += lateralError;
            }
        }

        private void Update()
        {
            targetPosition += networkVelocity * Time.deltaTime;
            if (networkVelocity.sqrMagnitude < 0.01f)
            {
                transform.position = targetPosition;
                return;
            }
            Vector3 direction = networkVelocity.normalized;
            float speed = networkVelocity.magnitude;
            float alongError = Vector3.Dot(targetPosition - transform.position, direction);
            float correction = Mathf.Clamp(alongError * 10f, -speed * 0.35f, speed * 0.35f);
            transform.position += direction * Mathf.Max(0f, speed + correction) * Time.deltaTime;
        }

        private static Sprite CreateRocketSprite()
        {
            if (rocketSprite != null)
            {
                return rocketSprite;
            }
            const int width = 96;
            const int height = 32;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "EasyGame 2D Rocket",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32 transparent = new Color32(0, 0, 0, 0);
            Color32 outline = new Color32(42, 45, 34, 255);
            Color32 body = new Color32(116, 126, 76, 255);
            Color32 highlight = new Color32(181, 166, 93, 255);
            Color32 band = new Color32(205, 105, 47, 255);
            for (int y = 0; y < height; y++)
            {
                float vertical = Mathf.Abs((y + 0.5f) / height * 2f - 1f);
                for (int x = 0; x < width; x++)
                {
                    float horizontal = (x + 0.5f) / width;
                    float halfHeight = horizontal < 0.1f || horizontal > 0.97f
                        ? 0f
                        : horizontal < 0.18f
                            ? Mathf.Lerp(0f, 0.42f, (horizontal - 0.1f) / 0.08f)
                            : horizontal < 0.76f
                                ? 0.42f
                                : 0.42f * (0.97f - horizontal) / 0.21f;
                    bool fin = horizontal > 0.12f && horizontal < 0.34f && vertical < Mathf.Lerp(0.64f, 0.42f, (horizontal - 0.12f) / 0.22f);
                    float edge = Mathf.Max(halfHeight, fin ? 0.64f : 0f);
                    Color32 pixel = transparent;
                    if (edge > 0f && vertical <= edge)
                    {
                        bool isOutline = vertical > edge - 0.08f || horizontal < 0.14f || horizontal > 0.93f;
                        pixel = isOutline ? outline : horizontal > 0.58f && horizontal < 0.65f ? band : vertical < 0.08f ? highlight : body;
                    }
                    texture.SetPixel(x, y, pixel);
                }
            }
            texture.Apply(false, true);
            rocketSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 96f, 0u, SpriteMeshType.FullRect);
            rocketSprite.name = "EasyGame 2D Rocket Sprite";
            return rocketSprite;
        }
    }
}
