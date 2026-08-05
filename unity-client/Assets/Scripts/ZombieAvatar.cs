using UnityEngine;

namespace EasyGame
{
    public sealed class ZombieAvatar : MonoBehaviour
    {
        private CharacterModel model;
        private Vector3 targetPosition;
        private bool receivedState;
        private float worldHeight;

        public string ZombieId { get; private set; }

        public void Initialize(ZombieState state, float mapHeight)
        {
            ZombieId = state.id;
            worldHeight = mapHeight;
            gameObject.name = $"Zombie {state.kind} {state.id}";
            model = gameObject.AddComponent<CharacterModel>();
            model.BuildZombie(state.kind);
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
            model.ApplyMotion(state.direction, speed, false, state.health <= 0);
        }

        private void LateUpdate()
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
    }
}
