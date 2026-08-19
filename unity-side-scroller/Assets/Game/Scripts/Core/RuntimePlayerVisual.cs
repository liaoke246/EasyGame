using UnityEngine;

namespace EasyGame.SideScroller.Core
{
    public static class RuntimePlayerVisual
    {
        public static Transform Create(Transform parent, Color bodyColor)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0.25f, 0f);

            GameObject body = new GameObject("Body");
            body.transform.SetParent(visual.transform, false);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.color = Color.white;
            bodyRenderer.sortingOrder = 10;

            PixelCharacterAnimator spriteAnimator = visual.AddComponent<PixelCharacterAnimator>();
            spriteAnimator.Initialize(bodyRenderer, Color.white);
            Animator legacyAnimator = parent.GetComponent<Animator>();
            if (legacyAnimator != null)
            {
                legacyAnimator.enabled = false;
            }
            return visual.transform;
        }

        public static Transform CreateZombie(Transform parent)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0.25f, 0f);

            GameObject body = new GameObject("Body");
            body.transform.SetParent(visual.transform, false);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.color = Color.white;
            bodyRenderer.sortingOrder = 9;

            PixelCharacterAnimator spriteAnimator = visual.AddComponent<PixelCharacterAnimator>();
            spriteAnimator.Initialize(bodyRenderer, new Color(0.58f, 0.78f, 0.55f));
            return visual.transform;
        }

        public static Transform CreateSlime(Transform parent, string color)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0.22f, 0f);

            GameObject body = new GameObject("Body");
            body.transform.SetParent(visual.transform, false);
            SpriteRenderer renderer = body.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 9;

            PixelSlimeAnimator animator = visual.AddComponent<PixelSlimeAnimator>();
            animator.Initialize(renderer, color);
            return visual.transform;
        }
    }
}
