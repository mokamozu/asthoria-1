using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Editor
{
    /// <summary>Materiais das Terras Alagadas (texturas procedurais + relevo).</summary>
    internal sealed class FloodedMaterials
    {
        public Material Brick, BrickSoot, Plaster, PlasterDirty, PlasterGreen, Stone, Wood, WoodDark, Thatch, Slate, Iron,
            Bark, Burlap, Cloth, Dirt, Lime, Window, WindowLit, LampGlow, Ember, Bone, Rope, SwampWater, PlagueWater, Miasma;

        public static FloodedMaterials Create()
        {
            var S = (System.Func<string, string, Color, float, float, Material>)((n, t, c, b, w) => ProceduralTextures.Surface(n, t, c, b, w));
            var m = new FloodedMaterials
            {
                Brick = S("TA_Tijolo", "Tijolo", Color.white, 1f, 0.15f),
                BrickSoot = S("TA_TijoloFuligem", "Tijolo", new Color(0.62f, 0.58f, 0.55f), 1f, 0.1f),
                Plaster = S("TA_Reboco", "Reboco", Color.white, 1f, 0.05f),
                PlasterDirty = S("TA_RebocoSujo", "Reboco", new Color(0.78f, 0.74f, 0.62f), 1f, 0.05f),
                PlasterGreen = S("TA_RebocoMofado", "Reboco", new Color(0.66f, 0.74f, 0.6f), 1f, 0.1f),
                Stone = S("TA_PedraAntiga", "PedraAntiga", Color.white, 1.2f, 0.2f),
                Wood = S("TA_MadeiraPodre", "MadeiraPodre", Color.white, 1f, 0.15f),
                WoodDark = S("TA_MadeiraEscura", "MadeiraPodre", new Color(0.6f, 0.55f, 0.5f), 1f, 0.1f),
                Thatch = S("TA_Palha", "Palha", Color.white, 1f, 0f),
                Slate = S("TA_Telha", "Telha", Color.white, 1.2f, 0.3f),
                Iron = S("TA_Ferro", "Ferro", Color.white, 0.6f, 0.4f),
                Bark = S("TA_Casca", "Casca", Color.white, 1.3f, 0.1f),
                Burlap = S("TA_Juta", "Juta", Color.white, 0.8f, 0f),
                Cloth = S("TA_Pano", "Pano", Color.white, 0.5f, 0f),
                Dirt = S("TA_Terra", "TerraEstrada", Color.white, 1f, 0.05f),
                Lime = S("TA_Cal", "Cal", Color.white, 0.6f, 0f),
                Rope = S("TA_Corda", "Palha", new Color(0.7f, 0.62f, 0.48f), 0.5f, 0f),
                Bone = S("TA_Osso", "Cal", new Color(1f, 0.96f, 0.85f), 0.3f, 0.1f),
            };
            m.Window = ProceduralTextures.Surface("TA_Janela", "JanelaArco", Color.white, 0.8f, 0.6f, alphaClip: true);
            m.WindowLit = ProceduralTextures.Surface("TA_JanelaAcesa", "JanelaAcesa", Color.white, 0.5f, 0f, alphaClip: true, emission: new Color(0.9f, 0.55f, 0.2f));
            m.LampGlow = ProceduralTextures.Surface("TA_Lampiao", "Pano", new Color(1f, 0.85f, 0.6f), 0f, 0f, emission: new Color(2.4f, 1.4f, 0.55f));
            m.Ember = ProceduralTextures.Surface("TA_Brasa", "Pano", new Color(0.9f, 0.4f, 0.15f), 0f, 0f, emission: new Color(2.2f, 0.7f, 0.15f));
            // A janela acesa usa a própria textura como emissão.
            m.WindowLit.SetTexture("_EmissionMap", ProceduralTextures.Albedo("JanelaAcesa"));

            m.SwampWater = ProceduralTextures.Water("TA_AguaPantano", new Color(0.24f, 0.27f, 0.17f, 0.84f), 0f, 0.8f, 0.012f, new Color(0.49f, 0.51f, 0.41f));
            m.PlagueWater = ProceduralTextures.Water("TA_AguaPeste", new Color(0.025f, 0.022f, 0.03f, 0.97f), 1f, 1.8f, 0.004f, new Color(0.16f, 0.15f, 0.19f));
            m.PlagueWater.SetFloat("_IridescenceScale", 4.5f);
            m.PlagueWater.SetFloat("_Fresnel", 0.55f);
            m.PlagueWater.renderQueue = (int)RenderQueue.Transparent + 1;   // por cima da água comum

            // Miasma: partículas escuras e translúcidas.
            const string miasmaPath = ProceduralTextures.MaterialFolder + "/TA_Miasma.mat";
            m.Miasma = AssetDatabase.LoadAssetAtPath<Material>(miasmaPath);
            if (m.Miasma == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
                m.Miasma = new Material(shader);
                AssetDatabase.CreateAsset(m.Miasma, miasmaPath);
            }
            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex != null) m.Miasma.SetTexture("_BaseMap", tex);
            m.Miasma.SetColor("_BaseColor", Color.white);
            m.Miasma.SetFloat("_Surface", 1f);
            m.Miasma.SetFloat("_Blend", 0f);
            m.Miasma.SetOverrideTag("RenderType", "Transparent");
            m.Miasma.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.Miasma.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.Miasma.SetInt("_ZWrite", 0);
            m.Miasma.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.Miasma.renderQueue = (int)RenderQueue.Transparent + 2;
            EditorUtility.SetDirty(m.Miasma);
            return m;
        }
    }
}
