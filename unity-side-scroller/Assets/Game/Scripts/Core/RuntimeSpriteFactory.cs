using UnityEngine;

namespace EasyGame.SideScroller.Core
{
    public static class RuntimeSpriteFactory
    {
        private static Sprite whiteSprite;
        private static Sprite roundedSprite;

        public static Sprite White
        {
            get
            {
                if (whiteSprite == null)
                {
                    whiteSprite = CreateSprite("Placeholder White", 16, 16, false);
                }

                return whiteSprite;
            }
        }

        public static Sprite RoundedCharacter
        {
            get
            {
                if (roundedSprite == null)
                {
                    roundedSprite = CreateSprite("Placeholder Character", 24, 32, true);
                }

                return roundedSprite;
            }
        }

        private static Sprite CreateSprite(string name, int width, int height, bool rounded)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            Color[] pixels = new Color[width * height];
            float radius = Mathf.Min(width, height) * 0.22f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool visible = true;
                    if (rounded)
                    {
                        float edgeX = Mathf.Min(x + 0.5f, width - x - 0.5f);
                        float edgeY = Mathf.Min(y + 0.5f, height - y - 0.5f);
                        if (edgeX < radius && edgeY < radius)
                        {
                            float dx = radius - edgeX;
                            float dy = radius - edgeY;
                            visible = dx * dx + dy * dy <= radius * radius;
                        }
                    }

                    pixels[y * width + x] = visible ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                Mathf.Max(width, height),
                0,
                SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
