using UnityEngine;

namespace EasyGame
{
    public sealed class CameraRig : MonoBehaviour
    {
        private Transform target;
        private Vector3 velocity;
        private float shakeStrength;
        private float shakeUntil;

        public void Follow(Transform followTarget)
        {
            target = followTarget;
        }

        public void Shake(float strength, float duration)
        {
            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeUntil = Mathf.Max(shakeUntil, Time.time + duration);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }
            Vector3 desired = target.position + new Vector3(0f, 22f, 0f);
            Vector3 position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.12f, 120f, Time.deltaTime);
            if (Time.time < shakeUntil)
            {
                float fade = Mathf.InverseLerp(shakeUntil, shakeUntil - 0.28f, Time.time);
                Vector2 shake = Random.insideUnitCircle * shakeStrength * fade;
                position += new Vector3(shake.x, 0f, shake.y);
            }
            else
            {
                shakeStrength = 0f;
            }
            transform.position = position;
        }
    }
}
