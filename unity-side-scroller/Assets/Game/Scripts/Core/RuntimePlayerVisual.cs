using UnityEngine;

namespace EasyGame.SideScroller.Core
{
    public static class RuntimePlayerVisual
    {
        public const string FeetAnchorName = "Feet Anchor";

        public static Transform Create(Transform parent, Color bodyColor)
        {
            Transform visual = CreateFeetAnchor(parent);

            GameObject body = new GameObject("Body");
            body.transform.SetParent(visual, false);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.color = Color.white;
            bodyRenderer.sortingOrder = 10;

            PixelCharacterAnimator spriteAnimator = visual.gameObject.AddComponent<PixelCharacterAnimator>();
            spriteAnimator.Initialize(bodyRenderer, Color.white);
            Animator legacyAnimator = parent.GetComponent<Animator>();
            if (legacyAnimator != null)
            {
                legacyAnimator.enabled = false;
            }
            return visual;
        }

        public static Transform CreateZombie(Transform parent)
        {
            Transform visual = CreateFeetAnchor(parent);

            GameObject body = new GameObject("Body");
            body.transform.SetParent(visual, false);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.color = Color.white;
            bodyRenderer.sortingOrder = 9;

            PixelCharacterAnimator spriteAnimator = visual.gameObject.AddComponent<PixelCharacterAnimator>();
            spriteAnimator.Initialize(bodyRenderer, new Color(0.58f, 0.78f, 0.55f));
            return visual;
        }

        public static Transform CreateSlime(Transform parent, string color)
        {
            Transform visual = CreateFeetAnchor(parent);

            GameObject body = new GameObject("Body");
            body.transform.SetParent(visual, false);
            SpriteRenderer renderer = body.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 9;

            PixelSlimeAnimator animator = visual.gameObject.AddComponent<PixelSlimeAnimator>();
            animator.Initialize(renderer, color);
            return visual;
        }

        private static Transform CreateFeetAnchor(Transform parent)
        {
            GameObject anchor = new GameObject(FeetAnchorName);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = ActorGeometry2D.FeetLocalPosition(parent.GetComponent<Collider2D>());
            return anchor.transform;
        }
    }
}
