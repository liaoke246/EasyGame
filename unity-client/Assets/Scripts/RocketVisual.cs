using UnityEngine;

namespace EasyGame
{
    public sealed class RocketVisual : MonoBehaviour
    {
        private Vector3 targetPosition;
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
                transform.position = visualMuzzle.Value;
            }
        }

        public void ApplyNetworkState(RocketState state, bool immediate = false)
        {
            targetPosition = GameCoordinates.ToUnity(state.x, state.y, worldHeight) + Vector3.up * 0.42f;
            Vector3 velocity = new Vector3(state.vx, 0f, -state.vy);
            if (velocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            }
            if (immediate)
            {
                transform.position = targetPosition;
            }
        }

        private void Update()
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - Mathf.Exp(-18f * Time.deltaTime));
        }
    }
}
