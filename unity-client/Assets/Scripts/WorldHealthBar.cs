using UnityEngine;
using UnityEngine.Rendering;

namespace EasyGame
{
    public sealed class WorldHealthBar : MonoBehaviour
    {
        private const float InnerWidth = 0.74f;
        private Transform fill;
        private TextMesh label;
        private Camera worldCamera;
        private Color healthyColor;
        private Color dangerColor;

        public void Initialize(string displayName, float screenOffset, Color fullHealthColor)
        {
            transform.localPosition = new Vector3(0f, 0.18f, screenOffset);
            healthyColor = fullHealthColor;
            dangerColor = new Color(0.95f, 0.16f, 0.09f);

            Transform border = VisualFactory.Box(
                transform,
                "Health Border",
                Vector3.zero,
                new Vector3(InnerWidth + 0.08f, 0.13f, 0.035f),
                new Color(0.025f, 0.028f, 0.025f));
            fill = VisualFactory.Box(
                transform,
                "Health Fill",
                new Vector3(0f, 0f, -0.025f),
                new Vector3(InnerWidth, 0.075f, 0.04f),
                healthyColor);
            DisableShadows(border);
            DisableShadows(fill);

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                GameObject labelObject = new GameObject("Character Name");
                labelObject.transform.SetParent(transform, false);
                labelObject.transform.localPosition = new Vector3(0f, 0.18f, -0.03f);
                label = labelObject.AddComponent<TextMesh>();
                label.text = displayName;
                label.anchor = TextAnchor.LowerCenter;
                label.alignment = TextAlignment.Center;
                label.fontSize = 72;
                label.characterSize = 0.021f;
                label.fontStyle = FontStyle.Bold;
                label.color = Color.white;
                MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            SetValue(1, 1);
        }

        public void SetValue(int health, int maximumHealth)
        {
            if (fill == null)
            {
                return;
            }
            float ratio = Mathf.Clamp01(health / (float)Mathf.Max(1, maximumHealth));
            float width = Mathf.Max(0.001f, InnerWidth * ratio);
            fill.localScale = new Vector3(width, 0.075f, 0.04f);
            fill.localPosition = new Vector3(-(InnerWidth - width) * 0.5f, 0f, -0.025f);
            VisualFactory.Tint(fill, Color.Lerp(dangerColor, healthyColor, ratio));
            gameObject.SetActive(health > 0);
        }

        private void LateUpdate()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
            if (worldCamera != null)
            {
                transform.rotation = worldCamera.transform.rotation;
            }
        }

        private static void DisableShadows(Transform target)
        {
            if (target != null && target.TryGetComponent(out Renderer renderer))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
