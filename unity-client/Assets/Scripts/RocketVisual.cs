using UnityEngine;

namespace EasyGame
{
    public sealed class RocketVisual : MonoBehaviour
    {
        private const float ProjectileHeight = 0.72f;
        private Vector3 targetPosition;
        private Vector3 networkVelocity;
        private ParticleSystem trail;
        private float worldHeight;

        public string RocketId { get; private set; }

        public void Initialize(RocketState state, float mapHeight)
        {
            RocketId = state.id;
            worldHeight = mapHeight;
            gameObject.name = $"Rocket {state.id}";
            TopDownArt.CreateWorldSprite(
                transform,
                "2D Rocket Shell",
                "Weapons/weapon_silencer",
                new Vector3(0f, 0f, 0f),
                new Vector2(0.56f, 0.18f),
                24,
                90f,
                new Color(0.72f, 0.82f, 0.4f));
            trail = Effects.CreateRocketTrail(transform);
            ApplyNetworkState(state, true);
        }

        public void ApplyNetworkState(RocketState state, bool immediate = false)
        {
            targetPosition = GameCoordinates.ToUnity(state.x, state.y, worldHeight) + Vector3.up * ProjectileHeight;
            networkVelocity = new Vector3(state.vx, 0f, -state.vy) * GameCoordinates.WorldScale;
            if (networkVelocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(networkVelocity.normalized, Vector3.up);
            }
            if (immediate)
            {
                transform.position = targetPosition;
                return;
            }
            if (networkVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 direction = networkVelocity.normalized;
                Vector3 error = targetPosition - transform.position;
                Vector3 lateralError = error - direction * Vector3.Dot(error, direction);
                transform.position += lateralError;
            }
        }

        private void Update()
        {
            targetPosition += networkVelocity * Time.deltaTime;
            if (networkVelocity.sqrMagnitude < 0.01f)
            {
                transform.position = targetPosition;
                return;
            }
            Vector3 direction = networkVelocity.normalized;
            float speed = networkVelocity.magnitude;
            float alongError = Vector3.Dot(targetPosition - transform.position, direction);
            float correction = Mathf.Clamp(alongError * 10f, -speed * 0.35f, speed * 0.35f);
            transform.position += direction * Mathf.Max(0f, speed + correction) * Time.deltaTime;
        }
    }
}
