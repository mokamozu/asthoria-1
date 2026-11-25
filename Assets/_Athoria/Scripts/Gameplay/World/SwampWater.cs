using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// Nível da água da região (pântano). Fica na cena da área; quem precisa saber a
    /// profundidade (a jogadora, a vegetação) pergunta a SwampWater.Current.
    /// Os efeitos na jogadora ficam em WaterDepthAndPlagueController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwampWater : MonoBehaviour
    {
        [SerializeField] private float waterLevel = 12f;

        public static SwampWater Current { get; private set; }
        public float WaterLevel => waterLevel;

        /// <summary>Profundidade da água num ponto (m; negativo = fora d'água).</summary>
        public float DepthAt(Vector3 feet) => waterLevel - feet.y;

        private void OnEnable() => Current = this;
        private void OnDisable() { if (Current == this) Current = null; }
    }
}
