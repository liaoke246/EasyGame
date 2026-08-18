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

        public void Initialize(RocketState state, float mapHeight, Vector3? visualMuzzle = null)
        {
            RocketId = state.id;
            worldHeight = mapHeight;
            gameObject.name = $"Rocket {state.id}";
            Transform shell = VisualFactory.Cylinder(transform, "Shell", new Vector3(0f, 0.22f, 0f), new Vector3(0.075f, 0.24f, 0.075f), new Color(0.28f, 0.34f, 0.19f));
            shell.localRotation = Quaternion.Euler(90f, 0f, 0f);
            VisualFactory.Cylinder(transform, "Warhead", new Vector3(0f, 0.22f, 0.25f), new Vector3(0.09f, 0.1f, 0.09f), new Color(0.42f, 0.15f, 0.08f)).localRotation = Quaternion.Euler(90f, 0f, 0f);
            trail = Effects.CreateRocketTrail(transform);
            ApplyNetworkState(state, !visualMuzzle.HasValue);
            if (visualMuzzle.HasValue)
            {
                Vector3 spawn = visualMuzzle.Value;
                spawn.y = ProjectileHeight;
                transform.position = spawn;
            }
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
            }
        }

        private void Update()
        {
            targetPosition += networkVelocity * Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - Mathf.Exp(-24f * Time.deltaTime));
        }
    }
}
