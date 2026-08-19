using UnityEngine;

namespace EasyGame.SideScroller.World
{
    [RequireComponent(typeof(Camera))]
    public sealed class SideCameraRig : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float smoothTime = 0.16f;
        [SerializeField] private Vector2 lookOffset = new Vector2(1.25f, 0.75f);
        private Transform target;
        private Camera sceneCamera;
        private Vector3 smoothVelocity;
        private Bounds worldBounds;

        public void Initialize(Transform followTarget, Bounds bounds)
        {
            target = followTarget;
            worldBounds = bounds;
            SnapToTarget();
        }

        private void Awake()
        {
            sceneCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null || sceneCamera == null)
            {
                return;
            }

            Vector3 desired = target.position + new Vector3(lookOffset.x, lookOffset.y, -10f);
            desired = ClampToBounds(desired);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref smoothVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        private void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            if (sceneCamera == null)
            {
                sceneCamera = GetComponent<Camera>();
            }

            transform.position = ClampToBounds(target.position + new Vector3(lookOffset.x, lookOffset.y, -10f));
        }

        private Vector3 ClampToBounds(Vector3 position)
        {
            float halfHeight = sceneCamera != null ? sceneCamera.orthographicSize : 6f;
            float halfWidth = sceneCamera != null ? halfHeight * sceneCamera.aspect : 10f;
            position.x = Mathf.Clamp(position.x, worldBounds.min.x + halfWidth, worldBounds.max.x - halfWidth);
            position.y = Mathf.Clamp(position.y, worldBounds.min.y + halfHeight, worldBounds.max.y - halfHeight);
            position.z = -10f;
            return position;
        }
    }
}
