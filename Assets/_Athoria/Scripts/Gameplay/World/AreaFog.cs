using UnityEngine;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// Neblina de jogo antigo (PS1/N64): névoa linear que engole o cenário e a
    /// câmera que para de desenhar logo depois dela. O fundo vira a própria cor
    /// da névoa, então nada "aparece" no horizonte — e tudo além não é desenhado,
    /// o que poupa muito trabalho da placa de vídeo. Um por cena de área.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AreaFog : MonoBehaviour
    {
        [SerializeField] private Color fogColor = new(0.11f, 0.14f, 0.13f);
        [SerializeField, Min(0f), Tooltip("Onde a névoa começa (m).")] private float fogStart = 14f;
        [SerializeField, Min(1f), Tooltip("Onde a névoa fica total (m). A câmera não desenha além disso.")] private float fogEnd = 60f;
        [SerializeField, Tooltip("Fundo com a cor da névoa (estilo N64). Desligado = mostra o céu.")] private bool solidBackground = true;
        [SerializeField, Tooltip("Névoa exponencial ao quadrado (bruma densa e suave). fogEnd continua sendo onde a câmera para de desenhar.")]
        private bool exponentialSquared;
        [SerializeField, Min(0.001f)] private float density = 0.018f;

        private Camera _applied;

        public float FogEnd => fogEnd;

        private void OnEnable()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = exponentialSquared ? FogMode.ExponentialSquared : FogMode.Linear;
            RenderSettings.fogDensity = density;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            _applied = null;
        }

        private void LateUpdate()
        {
            // O jogador (e a câmera) pode nascer depois da cena: aplica quando aparecer.
            var cam = Camera.main;
            if (cam == null || cam == _applied) return;
            cam.farClipPlane = fogEnd + 2f;
            if (solidBackground)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = fogColor;
            }
            else cam.clearFlags = CameraClearFlags.Skybox;
            _applied = cam;
        }
    }
}
