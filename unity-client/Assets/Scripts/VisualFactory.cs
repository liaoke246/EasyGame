using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EasyGame
{
    public static class VisualFactory
    {
        private static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();

        public static Transform Box(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            return Primitive(PrimitiveType.Cube, parent, name, localPosition, localScale, color);
        }

        public static Transform Sphere(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            return Primitive(PrimitiveType.Sphere, parent, name, localPosition, localScale, color);
        }

        public static Transform Cylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            return Primitive(PrimitiveType.Cylinder, parent, name, localPosition, localScale, color);
        }

        public static Transform Empty(Transform parent, string name, Vector3 localPosition)
        {
            GameObject instance = new GameObject(name);
            Transform result = instance.transform;
            result.SetParent(parent, false);
            result.localPosition = localPosition;
            return result;
        }

        public static Material Material(Color color, bool emissive = false)
        {
            Color32 packed = color;
            int key = packed.r | (packed.g << 8) | (packed.b << 16) | (packed.a << 24);
            if (emissive)
            {
                key ^= unchecked((int)0x5f000000);
            }
            if (Materials.TryGetValue(key, out Material cached))
            {
                return cached;
            }

            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Legacy Shaders/Diffuse");
            Material material = new Material(shader)
            {
                color = color,
                name = $"Runtime {ColorUtility.ToHtmlStringRGBA(color)}"
            };
            material.SetFloat("_Glossiness", 0.18f);
            material.SetFloat("_Metallic", emissive ? 0.05f : 0.12f);
            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.4f);
            }
            Materials[key] = material;
            return material;
        }

        public static void Tint(Transform target, Color color, bool emissive = false)
        {
            if (target != null && target.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = Material(color, emissive);
            }
        }

        private static Transform Primitive(
            PrimitiveType type,
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            GameObject instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            if (instance.TryGetComponent(out Collider collider))
            {
                Object.Destroy(collider);
            }
            Transform result = instance.transform;
            result.SetParent(parent, false);
            result.localPosition = localPosition;
            result.localScale = localScale;
            Renderer renderer = instance.GetComponent<Renderer>();
            renderer.sharedMaterial = Material(color);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return result;
        }
    }
}
