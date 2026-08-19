using UnityEngine;

namespace EasyGame.SideScroller.Core
{
    public static class RuntimePlayerVisual
    {
        public static Transform Create(Transform parent, Color bodyColor)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);

            GameObject body = new GameObject("Body");
            body.transform.SetParent(visual.transform, false);
            body.transform.localScale = new Vector3(0.82f, 1.52f, 1f);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = RuntimeSpriteFactory.RoundedCharacter;
            bodyRenderer.color = bodyColor;
            bodyRenderer.sortingOrder = 10;

            GameObject face = new GameObject("Visor");
            face.transform.SetParent(visual.transform, false);
            face.transform.localPosition = new Vector3(0.2f, 0.2f, -0.01f);
            face.transform.localScale = new Vector3(0.23f, 0.12f, 1f);
            SpriteRenderer faceRenderer = face.AddComponent<SpriteRenderer>();
            faceRenderer.sprite = RuntimeSpriteFactory.White;
            faceRenderer.color = new Color(0.86f, 0.93f, 0.78f);
            faceRenderer.sortingOrder = 11;
            return visual.transform;
        }
    }
}
