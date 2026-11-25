using UnityEngine;

namespace Athoria.Gameplay.Bosses
{
    /// <summary>
    /// Efeitos visuais do chefe: poeira, pedras voando, musgo caindo, avisos no chão,
    /// onda de choque e a nuvem de veneno. Só visual (o dano fica nas hitboxes).
    /// </summary>
    public sealed class BossEffects : MonoBehaviour
    {
        [SerializeField, Tooltip("Poeira e terra do impacto.")] private ParticleSystem dustPrefab;
        [SerializeField, Tooltip("Pedras e torrões que voam (salto).")] private ParticleSystem debrisPrefab;
        [SerializeField, Tooltip("Musgo e plantinhas caindo do corpo ao despertar.")] private ParticleSystem mossFall;
        [SerializeField] private Material warningMaterial;
        [SerializeField] private Material shockwaveMaterial;
        [SerializeField] private PoisonCloud poisonCloudPrefab;
        [SerializeField] private Color warningColor = new(1f, 0.35f, 0.1f, 0.7f);
        [SerializeField] private Color shockwaveColor = new(0.85f, 0.78f, 0.55f, 0.8f);
        [SerializeField] private Color poisonWarningColor = new(0.55f, 0.85f, 0.15f, 0.6f);

        public GroundMarker Warning(Vector3 center, float radius, float duration, bool poison = false) =>
            GroundMarker.Warning(warningMaterial, center, radius, duration, poison ? poisonWarningColor : warningColor);

        public void Shockwave(Vector3 center, float radius) =>
            GroundMarker.Shockwave(shockwaveMaterial != null ? shockwaveMaterial : warningMaterial, center, radius, 0.55f, shockwaveColor);

        public void Dust(Vector3 at, float scale) => Spawn(dustPrefab, at, scale);
        public void Debris(Vector3 at, float scale) => Spawn(debrisPrefab, at, scale);

        public void MossFall(bool on)
        {
            if (mossFall == null) return;
            if (on) mossFall.Play(true); else mossFall.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public PoisonCloud Cloud(GameObject source, Vector3 at, float radius, float duration, float dps, float tick)
        {
            if (poisonCloudPrefab == null) return null;
            var cloud = Instantiate(poisonCloudPrefab, at, Quaternion.identity);
            cloud.Begin(source, radius, duration, dps, tick);
            return cloud;
        }

        private static void Spawn(ParticleSystem prefab, Vector3 at, float scale)
        {
            if (prefab == null) return;
            var ps = Instantiate(prefab, at, Quaternion.identity);
            ps.transform.localScale = Vector3.one * Mathf.Max(0.2f, scale);
            ps.Play(true);
            Destroy(ps.gameObject, 4f);
        }
    }
}
