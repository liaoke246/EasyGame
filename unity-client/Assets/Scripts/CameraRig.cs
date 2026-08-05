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
            Vector3 desired = target.position + new Vector3(0f, 13.2f, -8.8f);
            Vector3 position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.13f, 100f, Time.deltaTime);
            if (Time.time < shakeUntil)
            {
                float fade = Mathf.InverseLerp(shakeUntil, shakeUntil - 0.28f, Time.time);
                position += Random.insideUnitSphere * shakeStrength * fade;
            }
            else
            {
                shakeStrength = 0f;
            }
            transform.position = position;
        }
    }
}
