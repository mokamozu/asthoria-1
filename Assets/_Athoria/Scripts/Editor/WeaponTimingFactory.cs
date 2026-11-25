using System.IO;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Items;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Editor
{
    /// <summary>
    /// Gera a tabela de tempos por categoria de arma e o material do rastro. A tabela só é
    /// escrita quando não existe (ajustes à mão ficam); use o menu para recriar.
    /// Proporções de cada golpe leve: antecipação / execução / acompanhamento / recuperação.
    /// Armas pesadas: antecipação e pausa no alto maiores, pico de velocidade mais tarde,
    /// mais pausa de impacto. Leves: quase sem pausa, combo cedo.
    /// </summary>
    public static class WeaponTimingFactory
    {
        public const string TablePath = "Assets/_Athoria/Data/Combate/TemposDasArmas.asset";
        public const string TrailMaterialPath = "Assets/_Athoria/Materials/Provisorios/RastroArma.mat";

        [MenuItem("Athoria/Dados/Recriar tabela de tempos das armas")]
        private static void Recreate() { GetOrCreateTable(true); Debug.Log("[Athoria] Tabela de tempos recriada."); }

        public static WeaponTimingTable GetOrCreateTable(bool force = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TablePath));
            var table = AssetDatabase.LoadAssetAtPath<WeaponTimingTable>(TablePath);
            if (table != null && !force && table.entries != null && table.entries.Length > 0) return table;
            if (table == null) { table = ScriptableObject.CreateInstance<WeaponTimingTable>(); AssetDatabase.CreateAsset(table, TablePath); }
            table.entries = new[]
            {
                //  categoria                 ant   pausa exec  acomp rec   combo  pico  valor inclin. | pes. +prep | stopL stopP  shkL  shkP | rastro (cor)
                T(WeaponCategory.Sword,       0.28f, 0.12f, 0.16f, 0.14f, 0.42f, 0.35f, 0.60f, 0.60f, 1.8f, 1.5f, 0.40f, 0.045f, 0.09f, 0.25f, 0.45f, new Color(0.85f, 0.9f, 1f, 0.35f)),
                T(WeaponCategory.Dagger,      0.22f, 0.05f, 0.14f, 0.10f, 0.54f, 0.20f, 0.55f, 0.58f, 1.7f, 1.35f, 0.30f, 0.030f, 0.06f, 0.18f, 0.35f, new Color(0.9f, 0.92f, 1f, 0.25f)),
                T(WeaponCategory.Axe,         0.32f, 0.18f, 0.15f, 0.18f, 0.35f, 0.40f, 0.65f, 0.62f, 2.0f, 1.6f, 0.45f, 0.060f, 0.11f, 0.32f, 0.55f, new Color(1f, 0.85f, 0.7f, 0.35f)),
                T(WeaponCategory.Hammer,      0.36f, 0.22f, 0.14f, 0.20f, 0.30f, 0.45f, 0.70f, 0.64f, 2.2f, 1.7f, 0.50f, 0.070f, 0.13f, 0.38f, 0.65f, new Color(0.95f, 0.85f, 0.75f, 0.3f)),
                T(WeaponCategory.Spear,       0.26f, 0.10f, 0.12f, 0.16f, 0.46f, 0.30f, 0.50f, 0.55f, 1.6f, 1.45f, 0.40f, 0.040f, 0.08f, 0.22f, 0.40f, new Color(0.85f, 0.9f, 1f, 0.25f)),
                T(WeaponCategory.Magic,       0.26f, 0.10f, 0.16f, 0.12f, 0.46f, 0.35f, 0.60f, 0.60f, 1.8f, 1.5f, 0.40f, 0.040f, 0.08f, 0.20f, 0.40f, new Color(0.7f, 0.5f, 1f, 0.45f)),
                T(WeaponCategory.Unarmed,     0.20f, 0.05f, 0.14f, 0.10f, 0.56f, 0.20f, 0.55f, 0.58f, 1.7f, 1.4f, 0.30f, 0.035f, 0.07f, 0.18f, 0.35f, Color.clear, trail: false),
                T(WeaponCategory.Firearm,     0.08f, 0.00f, 0.10f, 0.10f, 0.72f, 0.50f, 0.50f, 0.50f, 1.0f, 1.2f, 0.20f, 0.020f, 0.04f, 0.15f, 0.30f, Color.clear, trail: false),
                T(WeaponCategory.Shield,      0.28f, 0.10f, 0.16f, 0.14f, 0.42f, 0.35f, 0.60f, 0.60f, 1.8f, 1.5f, 0.40f, 0.050f, 0.09f, 0.25f, 0.45f, Color.clear, trail: false),
            };
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            return table;
        }

        private static WeaponTiming T(WeaponCategory c, float ant, float hold, float strike, float follow, float rec, float combo,
            float peakT, float peakV, float slope, float heavyMult, float heavyAnt, float stopL, float stopH, float shakeL, float shakeH,
            Color trailColor, bool trail = true) => new()
        {
            category = c, anticipation = ant, anticipationHold = hold, strike = strike, followThrough = follow, recovery = rec,
            comboWindow = combo, strikeCurve = WeaponTiming.Curve(peakT, peakV, slope),
            heavyDurationMultiplier = heavyMult, heavyAnticipation = heavyAnt,
            hitStopLight = stopL, hitStopHeavy = stopH, hitStopKill = 0.03f, shakeLight = shakeL, shakeHeavy = shakeH,
            trail = trail, trailColor = trailColor, trailStart = c == WeaponCategory.Spear ? 0.6f : 0.35f,
            trailLifetime = c == WeaponCategory.Hammer || c == WeaponCategory.Axe ? 0.15f : 0.11f,
        };

        /// <summary>Partículas/Unlit aditivo: a cor e o alfa vêm dos vértices do rastro.</summary>
        public static Material TrailMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);
            if (mat != null) return mat;
            Directory.CreateDirectory(Path.GetDirectoryName(TrailMaterialPath));
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            mat = new Material(shader) { name = "RastroArma" };
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);                 // aditivo
            mat.SetFloat("_Cull", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, TrailMaterialPath);
            return mat;
        }
    }
}
