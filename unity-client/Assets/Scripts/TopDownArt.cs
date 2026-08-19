using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EasyGame
{
    public static class TopDownArt
    {
        private const string Root = "Art/KenneyTopdown/";
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        public static Sprite LoadSprite(string relativePath, float pixelsPerUnit = 64f)
        {
            string key = $"{relativePath}@{pixelsPerUnit}";
            if (Sprites.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D texture = LoadTexture(relativePath);
            if (texture == null)
            {
                Debug.LogError($"Missing top-down art asset: {Root}{relativePath}");
                return null;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = relativePath.Replace('/', '-');
            Sprites[key] = sprite;
            return sprite;
        }

        public static Texture2D LoadTexture(string relativePath)
        {
            if (Textures.TryGetValue(relativePath, out Texture2D cached))
            {
                return cached;
            }

            Texture2D texture = Resources.Load<Texture2D>(Root + relativePath);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            Textures[relativePath] = texture;
            return texture;
        }

        public static SpriteRenderer CreateWorldSprite(
            Transform parent,
            string name,
            string relativePath,
            Vector3 localPosition,
            Vector2 worldSize,
            int sortingOrder,
            float rotation = 0f,
            Color? tint = null)
        {
            GameObject instance = new GameObject(name);
            Transform result = instance.transform;
            result.SetParent(parent, false);
            result.localPosition = localPosition;
            result.localRotation = Quaternion.Euler(90f, 0f, rotation);

            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadSprite(relativePath);
            renderer.sortingOrder = sortingOrder;
            renderer.color = tint ?? Color.white;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (renderer.sprite != null)
            {
                Vector2 nativeSize = renderer.sprite.bounds.size;
                result.localScale = new Vector3(
                    worldSize.x / Mathf.Max(0.001f, nativeSize.x),
                    worldSize.y / Mathf.Max(0.001f, nativeSize.y),
                    1f);
            }
            return renderer;
        }

        public static Transform CreateTiledPlane(
            Transform parent,
            string name,
            string relativePath,
            Vector3 localPosition,
            Vector2 worldSize,
            float tileWorldSize,
            int sortingOrder,
            Color? tint = null)
        {
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Quad);
            instance.name = name;
            if (instance.TryGetComponent(out Collider collider))
            {
                Object.Destroy(collider);
            }

            Transform result = instance.transform;
            result.SetParent(parent, false);
            result.localPosition = localPosition;
            result.localRotation = Quaternion.Euler(90f, 0f, 0f);
            result.localScale = new Vector3(worldSize.x, worldSize.y, 1f);

            Texture2D texture = LoadTexture(relativePath);
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Repeat;
            }
            Shader shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Sprites/Default");
            Material material = new Material(shader)
            {
                name = $"TopDown {relativePath}",
                mainTexture = texture,
                color = tint ?? Color.white
            };
            float repeatX = worldSize.x / Mathf.Max(0.1f, tileWorldSize);
            float repeatY = worldSize.y / Mathf.Max(0.1f, tileWorldSize);
            material.mainTextureScale = new Vector2(repeatX, repeatY);
            MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return result;
        }

        public static Transform CreateColorPlane(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector2 worldSize,
            Color color,
            int sortingOrder)
        {
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Quad);
            instance.name = name;
            if (instance.TryGetComponent(out Collider collider))
            {
                Object.Destroy(collider);
            }

            Transform result = instance.transform;
            result.SetParent(parent, false);
            result.localPosition = localPosition;
            result.localRotation = Quaternion.Euler(90f, 0f, 0f);
            result.localScale = new Vector3(worldSize.x, worldSize.y, 1f);
            Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            Material material = new Material(shader) { color = color, name = $"TopDown {name}" };
            MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return result;
        }
    }
}
