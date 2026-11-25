using Athoria.Gameplay.Player;
using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// Água rasa do pântano: abaixo do nível da água o jogador anda mais devagar
    /// e não consegue correr tão rápido. Funciona pela altura, sem colisores.
    /// </summary>
    public sealed class SwampWater : MonoBehaviour
    {
        [SerializeField] private float waterLevel = 0.5f;
        [SerializeField, Range(0.2f, 1f)] private float wadingMultiplier = 0.65f;
        [SerializeField, Tooltip("Profundidade (m) a partir da qual a lentidão é total.")] private float fullDepth = 0.5f;

        private PlayerController _player;

        public float WaterLevel => waterLevel;

        private void Update()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerController>();
                if (_player == null) return;
            }
            var depth = waterLevel - _player.transform.position.y;
            var k = Mathf.Clamp01(depth / fullDepth);
            _player.EnvironmentMultiplier = Mathf.Lerp(1f, wadingMultiplier, k);
        }

        private void OnDisable()
        {
            if (_player != null) _player.EnvironmentMultiplier = 1f;
        }
    }
}
