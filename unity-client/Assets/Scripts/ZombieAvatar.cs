using UnityEngine;

namespace EasyGame
{
    public sealed class ZombieAvatar : MonoBehaviour
    {
        private CharacterModel model;
        private Vector3 targetPosition;
        private bool receivedState;
        private float worldHeight;
        private WorldHealthBar healthBar;

        public string ZombieId { get; private set; }

        public void Initialize(ZombieState state, float mapHeight)
        {
            ZombieId = state.id;
            worldHeight = mapHeight;
            gameObject.name = $"Zombie {state.kind} {state.id}";
            model = gameObject.AddComponent<CharacterModel>();
            model.BuildZombie(state.kind);
            GameObject barObject = new GameObject("Zombie Health");
            barObject.transform.SetParent(transform, false);
            healthBar = barObject.AddComponent<WorldHealthBar>();
            float barHeight = state.kind == "brute" ? 0.88f : 0.72f;
            healthBar.Initialize(null, barHeight, state.kind == "brute" ? new Color(1f, 0.46f, 0.08f) : new Color(0.92f, 0.18f, 0.12f));
            ApplyNetworkState(state, true);
        }

        public void ApplyNetworkState(ZombieState state, bool immediate = false)
        {
            targetPosition = GameCoordinates.ToUnity(state.x, state.y, worldHeight);
            if (!receivedState || immediate || Vector3.Distance(transform.position, targetPosition) > 1f)
            {
                transform.position = targetPosition;
                receivedState = true;
            }
            float speed = Mathf.Sqrt(state.vx * state.vx + state.vy * state.vy) * GameCoordinates.WorldScale;
            model.ApplyMotion(state.direction, speed, false, state.health <= 0, state.vx, state.vy);
            healthBar.SetValue(state.health, state.maxHealth);
        }

        private void LateUpdate()
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
    }
}
