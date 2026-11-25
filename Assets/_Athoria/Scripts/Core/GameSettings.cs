using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Athoria.Core
{
    /// <summary>
    /// Configurações do jogador (resolução, tela cheia, qualidade, volume,
    /// sensibilidade do mouse). Salvas no PlayerPrefs e aplicadas ao abrir o jogo.
    /// </summary>
    public static class GameSettings
    {
        public static readonly Vector2Int[] Resolutions =
        {
            new(1280, 720), new(1366, 768), new(1600, 900), new(1920, 1080), new(2560, 1440)
        };

        private const string KeyResolution = "athoria.resolucao";
        private const string KeyFullscreen = "athoria.telaCheia";
        private const string KeyQuality = "athoria.qualidade";
        private const string KeyVolume = "athoria.volume";
        private const string KeyMouse = "athoria.sensibilidade";
        private const string KeyPixel = "athoria.pixelizacao";

        /// <summary>Resolução interna do 3D (a interface fica nítida): estilo PS1 = imagem pixelada.</summary>
        public static readonly float[] PixelScales = { 1f, 0.5f, 0.34f };
        private static readonly string[] PixelLabels = { "Desligado", "Suave", "PS1" };
        public static int PixelLevel { get; private set; } = 2;
        public static string PixelLabel => PixelLabels[PixelLevel];
        public static void StepPixel(int delta) { PixelLevel = Wrap(PixelLevel + delta, PixelScales.Length); Commit(); }

        public static int ResolutionIndex { get; private set; } = 3;
        public static bool Fullscreen { get; private set; } = true;
        public static int Quality { get; private set; }
        public static float MasterVolume { get; private set; } = 0.8f;
        /// <summary>Multiplicador da sensibilidade do mouse (1 = padrão).</summary>
        public static float MouseSensitivity { get; private set; } = 1f;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadAndApply()
        {
            Changed = null;
            ResolutionIndex = PlayerPrefs.GetInt(KeyResolution, ClosestResolution());
            Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
            Quality = PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel());
            MasterVolume = PlayerPrefs.GetFloat(KeyVolume, 0.8f);
            MouseSensitivity = PlayerPrefs.GetFloat(KeyMouse, 1f);
            PixelLevel = PlayerPrefs.GetInt(KeyPixel, 2);
            Clamp();
            Apply();
        }

        private static int ClosestResolution()
        {
            var screen = Screen.currentResolution;
            var best = 0;
            for (var i = 0; i < Resolutions.Length; i++)
                if (Resolutions[i].x <= screen.width && Resolutions[i].y <= screen.height) best = i;
            return best;
        }

        private static void Clamp()
        {
            ResolutionIndex = Mathf.Clamp(ResolutionIndex, 0, Resolutions.Length - 1);
            Quality = Mathf.Clamp(Quality, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            MasterVolume = Mathf.Clamp01(MasterVolume);
            MouseSensitivity = Mathf.Clamp(MouseSensitivity, 0.2f, 3f);
            PixelLevel = Mathf.Clamp(PixelLevel, 0, PixelScales.Length - 1);
        }

        public static string ResolutionLabel => $"{Resolutions[ResolutionIndex].x} × {Resolutions[ResolutionIndex].y}";
        public static string QualityLabel => QualitySettings.names.Length > 0 ? QualitySettings.names[Quality] : "Padrão";

        public static void StepResolution(int delta) { ResolutionIndex = Wrap(ResolutionIndex + delta, Resolutions.Length); Commit(); }
        public static void ToggleFullscreen() { Fullscreen = !Fullscreen; Commit(); }
        public static void StepQuality(int delta) { Quality = Wrap(Quality + delta, Mathf.Max(1, QualitySettings.names.Length)); Commit(); }
        public static void StepVolume(int delta) { MasterVolume = Mathf.Round((MasterVolume + delta * 0.1f) * 10f) / 10f; Commit(); }
        public static void StepMouse(int delta) { MouseSensitivity = Mathf.Round((MouseSensitivity + delta * 0.1f) * 10f) / 10f; Commit(); }

        private static int Wrap(int v, int count) => (v % count + count) % count;

        private static void Commit()
        {
            Clamp();
            PlayerPrefs.SetInt(KeyResolution, ResolutionIndex);
            PlayerPrefs.SetInt(KeyFullscreen, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(KeyQuality, Quality);
            PlayerPrefs.SetFloat(KeyVolume, MasterVolume);
            PlayerPrefs.SetFloat(KeyMouse, MouseSensitivity);
            PlayerPrefs.SetInt(KeyPixel, PixelLevel);
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        /// <summary>Visual PS1: o 3D é desenhado em baixa resolução e ampliado sem suavizar (pixels nítidos).</summary>
        private static void ApplyPixelation()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset urp) return;
            urp.renderScale = PixelScales[PixelLevel];
            urp.upscalingFilter = PixelLevel == 0 ? UpscalingFilterSelection.Auto : UpscalingFilterSelection.Point;
            urp.msaaSampleCount = PixelLevel == 0 ? 4 : 1;
            ApplyRetroShaders();
        }

        /// <summary>
        /// Valores globais dos shaders retrô (Athoria/PSX Lit e Athoria/PSX Tela):
        /// tremor dos vértices, textura afim e cores reduzidas com pontilhado.
        /// </summary>
        public static void ApplyRetroShaders()
        {
            switch (PixelLevel)
            {
                case 0:
                    Shader.SetGlobalVector("_PSX_SnapResolution", Vector4.zero);
                    Shader.SetGlobalFloat("_PSX_Affine", 0f);
                    Shader.SetGlobalFloat("_PSX_ColorLevels", 0f);
                    Shader.SetGlobalFloat("_PSX_Dither", 0f);
                    break;
                case 1:
                    Shader.SetGlobalVector("_PSX_SnapResolution", new Vector4(640f, 360f, 0f, 0f));
                    Shader.SetGlobalFloat("_PSX_Affine", 0.3f);
                    Shader.SetGlobalFloat("_PSX_ColorLevels", 64f);
                    Shader.SetGlobalFloat("_PSX_Dither", 0.45f);
                    break;
                default:
                    Shader.SetGlobalVector("_PSX_SnapResolution", new Vector4(320f, 180f, 0f, 0f));
                    Shader.SetGlobalFloat("_PSX_Affine", 0.6f);
                    Shader.SetGlobalFloat("_PSX_ColorLevels", 32f);
                    Shader.SetGlobalFloat("_PSX_Dither", 0.6f);
                    break;
            }
        }

        private static void Apply()
        {
            AudioListener.volume = MasterVolume;
            if (QualitySettings.GetQualityLevel() != Quality) QualitySettings.SetQualityLevel(Quality, true);
            ApplyPixelation();
#if !UNITY_EDITOR
            var r = Resolutions[ResolutionIndex];
            Screen.SetResolution(r.x, r.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        }
    }
}
