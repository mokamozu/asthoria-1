using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Troca a textura dos braços em primeira pessoa conforme a personagem
    /// (armadura da Layla, luvas da Evelyn, mangas da Anaíra, couro da Akane).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArmsSkin : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers = System.Array.Empty<Renderer>();

        private Material[] _instances;

        public void Apply(Texture texture)
        {
            if (texture == null || renderers == null) return;
            if (_instances == null)
            {
                _instances = new Material[renderers.Length];
                for (var i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    _instances[i] = new Material(renderers[i].sharedMaterial);
                    renderers[i].sharedMaterial = _instances[i];
                }
            }
            foreach (var m in _instances)
            {
                if (m == null) continue;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", texture);
            }
        }

        private void OnDestroy()
        {
            if (_instances == null) return;
            foreach (var m in _instances) if (m != null) Destroy(m);
        }

#if UNITY_EDITOR
        public void EditorSetRenderers(Renderer[] list) => renderers = list;
#endif
    }
}
