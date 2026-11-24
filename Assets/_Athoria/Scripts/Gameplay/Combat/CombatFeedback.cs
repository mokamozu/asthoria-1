using System;
using UnityEngine;

namespace Athoria.Gameplay.Combat
{
    /// <summary>
    /// Sons e partículas de combate num só lugar. Trocar os sons provisórios
    /// pelos definitivos é só substituir as referências neste asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Athoria/Combate/Feedback de Combate", fileName = "FeedbackCombate")]
    public sealed class CombatFeedback : ScriptableObject
    {
        [Serializable]
        public struct SurfaceFx
        {
            public SurfaceKind kind;
            public AudioClip[] sounds;
            public GameObject particles;
        }

        [Header("Golpes")]
        public AudioClip[] swingLight;
        public AudioClip swingHeavy;
        public AudioClip dodge;

        [Header("Defesa")]
        public AudioClip block;
        public AudioClip guardBreak;
        public GameObject blockParticles;

        [Header("Jogador")]
        public AudioClip playerHurt;

        [Header("Impacto por superfície")]
        public SurfaceFx[] surfaces;

        public void PlayImpact(SurfaceKind kind, Vector3 point, Vector3 normal, float volume = 1f)
        {
            foreach (var s in surfaces)
            {
                if (s.kind != kind) continue;
                Play(Pick(s.sounds), point, volume);
                SpawnParticles(s.particles, point, normal);
                return;
            }
        }

        public static void SpawnParticles(GameObject prefab, Vector3 point, Vector3 normal)
        {
            if (prefab == null) return;
            var rot = normal.sqrMagnitude > 0.001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
            var fx = Instantiate(prefab, point, rot);
            Destroy(fx, 2f);
        }

        public static AudioClip Pick(AudioClip[] clips) =>
            clips == null || clips.Length == 0 ? null : clips[UnityEngine.Random.Range(0, clips.Length)];

        /// <summary>Toca um som 3D num ponto, com leve variação de tom.</summary>
        public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariation = 0.08f, bool spatial = true)
        {
            if (clip == null) return;
            var go = new GameObject("SFX " + clip.name);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
            source.spatialBlend = spatial ? 1f : 0f;
            source.minDistance = 2f;
            source.maxDistance = 40f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, source.pitch) + 0.1f);
        }
    }
}
