using System.Collections.Generic;
using UnityEngine;

namespace Athoria.Gameplay.Effects
{
    /// <summary>
    /// Animação de efeito em pixel art vinda de uma sprite sheet (PNG + JSON no
    /// formato do Aseprite, ex.: exportada pelo PixelFX). Criada pelo importador
    /// do menu Athoria > Efeitos. Os sprites são montados em tempo de execução
    /// a partir dos retângulos de cada quadro.
    /// </summary>
    [CreateAssetMenu(menuName = "Athoria/Efeitos/Animação de efeito (sprite sheet)", fileName = "NovoEfeito")]
    public sealed class SpriteFxAnimation : ScriptableObject
    {
        [SerializeField] private Texture2D texture;
        [SerializeField] private Rect[] frames = System.Array.Empty<Rect>();
        [SerializeField, Tooltip("Duração de cada quadro em segundos.")] private float[] durations = System.Array.Empty<float>();
        [SerializeField, Tooltip("Pivô normalizado (0..1, origem embaixo à esquerda). Efeitos de chão: pivô no chão.")]
        private Vector2 pivot = new(0.5f, 0f);
        [SerializeField] private bool loop;
        [SerializeField, Min(1f)] private float pixelsPerUnit = 64f;
        [SerializeField, Min(0.01f), Tooltip("Multiplicador de tamanho no mundo.")] private float worldScale = 1f;

        [System.NonSerialized] private Sprite[] _sprites;

        public int FrameCount => frames.Length;
        public bool Loop => loop;
        public float WorldScale => worldScale;
        public float Duration
        {
            get
            {
                var t = 0f;
                foreach (var d in durations) t += d;
                return t;
            }
        }

        public float FrameDuration(int i) => durations.Length > i ? durations[i] : 1f / 30f;

        public Sprite GetSprite(int i)
        {
            if (_sprites == null || _sprites.Length != frames.Length) BuildSprites();
            return _sprites[i];
        }

        private void BuildSprites()
        {
            _sprites = new Sprite[frames.Length];
            for (var i = 0; i < frames.Length; i++)
            {
                _sprites[i] = Sprite.Create(texture, frames[i], pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                _sprites[i].name = $"{name}_{i}";
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(Texture2D tex, Rect[] rects, float[] frameDurations, Vector2 newPivot, bool isLoop)
        {
            texture = tex;
            frames = rects;
            durations = frameDurations;
            pivot = newPivot;
            loop = isLoop;
            _sprites = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorSetScale(float ppu, float scale)
        {
            pixelsPerUnit = ppu;
            worldScale = scale;
            _sprites = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
