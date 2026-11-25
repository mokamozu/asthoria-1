using System.Collections.Generic;
using System.IO;
using Athoria.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Editor
{
    /// <summary>
    /// Carabina de Tambor da Evelyn (6 tiros, .38): modelo low-poly de PS1 montado
    /// por código, com texturas geradas (madeira escura riscada, metal enferrujado),
    /// e as peças que animam (cão, braço e tambor, balas, clarão).
    /// Medidas no espaço do "desenho" (X = cano, Y = cima), como na página de inspeção;
    /// a raiz segue o padrão das armas do jogo: empunhadura na origem, cano em +Y, topo em -Z.
    /// </summary>
    internal static class CarbineModelBuilder
    {
        private const string AssetPath = "Assets/_Athoria/Prefabs/ViewModels/Armas/Malhas/Carabina.asset";
        private const string MatFolder = "Assets/_Athoria/Materials/Carabina";
        private const float CY = 0f, BY = 0.11f;   // eixo do tambor e do cano

        private static Object _container;
        private static readonly Dictionary<string, Mesh> Meshes = new();

        public static void Build(Transform root, float length = 0.95f)
        {
            Meshes.Clear();
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            Directory.CreateDirectory(MatFolder);
            if (AssetDatabase.LoadMainAssetAtPath(AssetPath) != null) AssetDatabase.DeleteAsset(AssetPath);
            var holder = new Mesh { name = "Carabina" };
            AssetDatabase.CreateAsset(holder, AssetPath);
            _container = holder;

            // Texturas e materiais
            var wood = Save(WoodTexture(), "Madeira");
            var steelT = Save(MetalTexture(new Color32(56, 59, 63, 255), 5, 3), "Aco");
            var darkT = Save(MetalTexture(new Color32(36, 38, 41, 255), 3, 4), "AcoEscuro");
            var brassT = Save(BrassTexture(), "Latao");
            var flashT = Save(FlashTexture(), "Clarao");
            var mWood = Mat("Madeira", wood, Color.white);
            var mSteel = Mat("Aco", steelT, Color.white);
            var mDark = Mat("AcoEscuro", darkT, Color.white);
            var mBrass = Mat("Latao", brassT, Color.white);
            var mSpent = Mat("LataoGasto", brassT, new Color(0.62f, 0.5f, 0.34f));
            var mLead = Mat("Chumbo", darkT, new Color(0.62f, 0.58f, 0.55f));
            var mSight = Mat("Mira", steelT, new Color(0.9f, 0.72f, 0.3f));
            var mHole = Mat("Furo", null, Color.black);
            var mFlash = Mat("Clarao", flashT, Color.black, true);
            mFlash.SetColor("_EmissionColor", new Color(3f, 1.6f, 0.5f));
            mFlash.SetTexture("_EmissionMap", flashT);

            // Raiz do desenho: X do desenho -> +Y da arma; Y do desenho -> -Z (topo)
            var s = length / 2.37f;
            var model = new GameObject("Modelo").transform;
            model.SetParent(root, false);
            model.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.back);
            model.localScale = Vector3.one * s;
            var grip = new Vector3(-0.4f, -0.06f, 0f);   // punho da coronha, atrás do gatilho
            model.localPosition = -(model.localRotation * (grip * s));

            // Coronha com queda e chapa de metal
            var stock = Group(model, "Coronha", new Vector3(-0.42f, -0.04f, 0f), new Vector3(0f, 0f, 9.2f));
            Part(stock, "Madeira", Box(0.72f, 0.26f, 0.1f, 1.35f, 0.62f), mWood, new Vector3(-0.36f, -0.02f, 0f));
            Part(stock, "Chapa", Box(0.04f, 0.36f, 0.11f), mSteel, new Vector3(-0.73f, -0.02f, 0f));
            // Armação
            Part(model, "Culatra", Box(0.24f, 0.17f, 0.09f), mSteel, new Vector3(-0.31f, CY - 0.01f, 0f));
            Part(model, "Tira Superior", Box(0.38f, 0.045f, 0.085f), mSteel, new Vector3(-0.03f, CY + 0.2f, 0f));
            Part(model, "Ponte Inferior", Box(0.4f, 0.05f, 0.085f), mDark, new Vector3(-0.03f, CY - 0.2f, 0f));
            Part(model, "Apoio do Cano", Box(0.07f, 0.42f, 0.09f), mSteel, new Vector3(0.17f, CY + 0.01f, 0f));
            Part(model, "Alca de Mira", Box(0.05f, 0.04f, 0.02f), mSight, new Vector3(-0.2f, CY + 0.235f, 0f));
            // Cano e guarda-mão
            Part(model, "Cano", Prism(8, 0.04f, 0.036f, 1.02f), mSteel, new Vector3(0.7f, BY, 0f));
            Part(model, "Boca", Prism(8, 0.022f, 0.022f, 0.004f), mHole, new Vector3(1.212f, BY, 0f));
            Part(model, "Massa de Mira", Box(0.03f, 0.05f, 0.016f), mSight, new Vector3(1.16f, BY + 0.055f, 0f));
            Part(model, "Guarda-mao", Box(0.56f, 0.085f, 0.085f, 1f, 0.85f), mWood, new Vector3(0.5f, BY - 0.075f, 0f));
            Part(model, "Bracadeira", Box(0.03f, 0.12f, 0.1f), mDark, new Vector3(0.72f, BY - 0.04f, 0f));
            // Gatilho, alavanca em anel e cão
            Part(model, "Gatilho", Box(0.015f, 0.07f, 0.02f), mDark, new Vector3(-0.24f, CY - 0.15f, 0f));
            var lever = Group(model, "Alavanca", new Vector3(-0.1f, CY - 0.22f, 0f), Vector3.zero);
            Part(lever, "Barra", Box(0.2f, 0.02f, 0.024f), mDark, new Vector3(-0.08f, -0.005f, 0f));
            for (var i = 0; i < 10; i++)
            {
                var a = i * 36f * Mathf.Deg2Rad;
                Part(lever, "Anel", Box(0.05f, 0.016f, 0.024f), mDark,
                    new Vector3(-0.2f + Mathf.Cos(a) * 0.094f, -0.06f + Mathf.Sin(a) * 0.06f, 0f), new Vector3(0f, 0f, i * 36f + 90f));
            }
            var hammer = Group(model, "Cao", new Vector3(-0.26f, CY + 0.07f, 0f), Vector3.zero);
            Part(hammer, "Corpo", Box(0.035f, 0.1f, 0.03f), mDark, new Vector3(0f, 0.05f, 0f));
            Part(hammer, "Espora", Box(0.06f, 0.025f, 0.03f), mDark, new Vector3(-0.035f, 0.1f, 0f));

            // Tambor: braço (abre para o lado) e tambor (gira no eixo)
            var crane = Group(model, "Braco do Tambor", new Vector3(-0.03f, CY - 0.17f, 0f), Vector3.zero);
            var cyl = Group(crane, "Tambor", new Vector3(0f, 0.17f, 0f), Vector3.zero);
            Part(cyl, "Corpo", Prism(12, 0.165f, 0.165f, 0.3f), mSteel, Vector3.zero);
            Part(cyl, "Ejetor", Box(0.03f, 0.06f, 0.06f), mDark, new Vector3(0.165f, 0f, 0f));
            var rounds = new GameObject[6];
            var bullets = new GameObject[6];
            var cases = new Renderer[6];
            const float R = 0.11f;
            for (var k = 0; k < 6; k++)
            {
                var a = k * 60f * Mathf.Deg2Rad;
                float y = Mathf.Cos(a) * R, z = Mathf.Sin(a) * R;
                Part(cyl, "Boca Frente", Prism(8, 0.03f, 0.03f, 0.004f), mHole, new Vector3(0.151f, y, z));
                Part(cyl, "Boca Tras", Prism(8, 0.03f, 0.03f, 0.004f), mHole, new Vector3(-0.151f, y, z));
                var fa = a + 30f * Mathf.Deg2Rad;
                Part(cyl, "Canelura", Box(0.2f, 0.03f, 0.02f), mDark, new Vector3(0f, Mathf.Cos(fa) * 0.16f, Mathf.Sin(fa) * 0.16f), new Vector3(-fa * Mathf.Rad2Deg, 0f, 0f));
                var round = Group(cyl, $"Bala {k + 1}", new Vector3(0f, y, z), Vector3.zero);
                cases[k] = Part(round, "Estojo", Prism(6, 0.026f, 0.026f, 0.22f), mBrass, Vector3.zero).GetComponent<Renderer>();
                Part(round, "Aro", Prism(6, 0.031f, 0.031f, 0.012f), mBrass, new Vector3(-0.115f, 0f, 0f));
                bullets[k] = Part(round, "Projetil", Prism(6, 0.024f, 0.006f, 0.05f), mLead, new Vector3(0.12f, 0f, 0f));
                rounds[k] = round.gameObject;
            }

            // Clarão (duas placas cruzadas) e luz do disparo
            var flash = Group(model, "Clarao", new Vector3(1.32f, BY, 0f), Vector3.zero);
            Part(flash, "Placa A", Quad(0.38f), mFlash, Vector3.zero, new Vector3(0f, 90f, 0f));
            Part(flash, "Placa B", Quad(0.38f), mFlash, Vector3.zero, new Vector3(0f, 0f, 0f));
            Part(flash, "Placa C", Quad(0.38f), mFlash, Vector3.zero, new Vector3(90f, 0f, 0f));
            foreach (var r in flash.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            var lightGo = new GameObject("Luz do Disparo");
            lightGo.transform.SetParent(model, false);
            lightGo.transform.localPosition = new Vector3(1.32f, BY, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.2f);
            light.range = 7f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            // Animações
            var view = root.gameObject.AddComponent<RevolverCarbineView>();
            var so = new SerializedObject(view);
            so.FindProperty("model").objectReferenceValue = root;
            so.FindProperty("hammer").objectReferenceValue = hammer;
            so.FindProperty("lever").objectReferenceValue = lever;
            so.FindProperty("crane").objectReferenceValue = crane;
            so.FindProperty("cylinder").objectReferenceValue = cyl;
            SetArray(so.FindProperty("rounds"), rounds);
            SetArray(so.FindProperty("bullets"), bullets);
            SetArray(so.FindProperty("cases"), cases);
            so.FindProperty("liveCase").objectReferenceValue = mBrass;
            so.FindProperty("spentCase").objectReferenceValue = mSpent;
            so.FindProperty("flash").objectReferenceValue = flash.gameObject;
            so.FindProperty("flashLight").objectReferenceValue = light;
            AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Athoria/Audio/Armas/{n}.wav");
            so.FindProperty("clickSound").objectReferenceValue = Clip("carabina_clique");
            so.FindProperty("openSound").objectReferenceValue = Clip("carabina_abrir");
            so.FindProperty("closeSound").objectReferenceValue = Clip("carabina_fechar");
            so.FindProperty("roundSound").objectReferenceValue = Clip("carabina_bala");
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void SetArray(SerializedProperty p, Object[] items)
        {
            p.arraySize = items.Length;
            for (var i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // ---------- peças ----------
        private static Transform Group(Transform parent, string name, Vector3 pos, Vector3 euler)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(euler);
            return t;
        }

        private static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static Material Mat(string name, Texture2D tex, Color color, bool cutout = false)
        {
            var path = $"{MatFolder}/Carabina_{name}.mat";
            var shader = PsxAssets.PsxShader;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            // Brilho de view model (como as outras armas): textura × cor × 0,24
            var g = color * 0.24f; g.a = 1f;
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", tex != null ? g : Color.black);
            m.EnableKeyword("_EMISSION");
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            m.SetFloat("_Cutoff", 0.4f);
            if (cutout) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------- malhas low-poly, faces chapadas ----------
        private static Mesh Store(Mesh m)
        {
            m.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(m, _container);
            return m;
        }

        /// <summary>Caixa ao longo de X; a altura pode afinar de uma ponta à outra (coronha).</summary>
        private static Mesh Box(float lx, float ly, float lz, float yBack = 1f, float yFront = 1f)
        {
            var key = $"box{lx}{ly}{lz}{yBack}{yFront}";
            if (Meshes.TryGetValue(key, out var cached)) return cached;
            Vector3 V(int sx, int sy, int sz) => new(sx * lx * 0.5f, sy * ly * 0.5f * (sx < 0 ? yBack : yFront), sz * lz * 0.5f);
            var corners = new[] { V(-1, -1, -1), V(1, -1, -1), V(1, 1, -1), V(-1, 1, -1), V(-1, -1, 1), V(1, -1, 1), V(1, 1, 1), V(-1, 1, 1) };
            int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 0, 1, 5, 4 } };
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            foreach (var f in faces)
            {
                var b = v.Count;
                foreach (var i in f) v.Add(corners[i]);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
                tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            var m = new Mesh { name = "Caixa" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals();
            return Meshes[key] = Store(m);
        }

        /// <summary>Prisma de N lados ao longo de X (raio de trás e da frente), com tampas.</summary>
        private static Mesh Prism(int n, float rBack, float rFront, float len)
        {
            var key = $"prism{n}{rBack}{rFront}{len}";
            if (Meshes.TryGetValue(key, out var cached)) return cached;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            float h = len * 0.5f;
            Vector3 P(int i, float x, float r) { var a = i * Mathf.PI * 2f / n; return new Vector3(x, Mathf.Cos(a) * r, Mathf.Sin(a) * r); }
            for (var i = 0; i < n; i++)
            {
                var b = v.Count;
                v.Add(P(i, -h, rBack)); v.Add(P(i + 1, -h, rBack)); v.Add(P(i + 1, h, rFront)); v.Add(P(i, h, rFront));
                float u0 = (float)i / n, u1 = (float)(i + 1) / n;
                uv.Add(new Vector2(0, u0)); uv.Add(new Vector2(0, u1)); uv.Add(new Vector2(1, u1)); uv.Add(new Vector2(1, u0));
                tri.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            foreach (var (x, r, flip) in new[] { (-h, rBack, true), (h, rFront, false) })
            {
                var c = v.Count;
                v.Add(new Vector3(x, 0, 0)); uv.Add(new Vector2(0.5f, 0.5f));
                for (var i = 0; i < n; i++) { v.Add(P(i, x, r)); var a = i * Mathf.PI * 2f / n; uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f)); }
                for (var i = 0; i < n; i++)
                {
                    int a = c + 1 + i, b2 = c + 1 + (i + 1) % n;
                    if (flip) tri.AddRange(new[] { c, a, b2 }); else tri.AddRange(new[] { c, b2, a });
                }
            }
            var m = new Mesh { name = "Prisma" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals();
            return Meshes[key] = Store(m);
        }

        private static Mesh Quad(float size)
        {
            var key = $"quad{size}";
            if (Meshes.TryGetValue(key, out var cached)) return cached;
            var h = size * 0.5f;
            var m = new Mesh { name = "Placa" };
            m.SetVertices(new List<Vector3> { new(-h, -h, 0), new(h, -h, 0), new(h, h, 0), new(-h, h, 0) });
            m.SetUVs(0, new List<Vector2> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) });
            m.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            m.RecalculateNormals();
            return Meshes[key] = Store(m);
        }

        // ---------- texturas geradas (baixa resolução, sem filtro) ----------
        private static Texture2D Save(Texture2D t, string name)
        {
            t.name = name;
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Repeat;
            t.Apply(false, false);
            AssetDatabase.AddObjectToAsset(t, _container);
            return t;
        }

        private static Texture2D WoodTexture()
        {
            var rng = new System.Random(7);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            const int W = 64, H = 32;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color32[W * H];
            for (var i = 0; i < px.Length; i++) px[i] = new Color32(74, 42, 23, 255);
            for (var y = 0; y < H; y++)
            {
                var tone = R(-18f, 18f);
                var c = new Color32((byte)(74 + tone), (byte)(42 + tone * 0.5f), (byte)(23 + tone * 0.33f), 255);
                for (var x = 0; x < W; x += 1 + rng.Next(3))
                    if (rng.NextDouble() < 0.55) for (var k = 0; k < rng.Next(2, 9) && x + k < W; k++) px[y * W + x + k] = c;
            }
            for (var i = 0; i < 9; i++)   // riscos escuros
            {
                int y = rng.Next(H), x0 = rng.Next(W), len = rng.Next(8, 26);
                for (var k = 0; k < len && x0 + k < W; k++) px[y * W + x0 + k] = new Color32(28, 15, 7, 255);
            }
            t.SetPixels32(px);
            return t;
        }

        private static Texture2D MetalTexture(Color32 baseColor, int rustSpots, int seed)
        {
            var rng = new System.Random(seed);
            const int S = 32;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color32[S * S];
            for (var i = 0; i < px.Length; i++)
            {
                var v = rng.Next(-14, 14);
                px[i] = new Color32((byte)Mathf.Clamp(baseColor.r + v, 0, 255), (byte)Mathf.Clamp(baseColor.g + v, 0, 255), (byte)Mathf.Clamp(baseColor.b + v, 0, 255), 255);
            }
            for (var s = 0; s < rustSpots; s++)   // ferrugem em manchas
            {
                int cx = rng.Next(S), cy = rng.Next(S), r = rng.Next(1, 4);
                for (var j = 0; j < r * 6; j++)
                {
                    int x = (cx + rng.Next(-r, r + 1) + S) % S, y = (cy + rng.Next(-r, r + 1) + S) % S;
                    px[y * S + x] = rng.Next(2) == 0 ? new Color32(122, 58, 26, 255) : new Color32(84, 40, 20, 255);
                }
            }
            t.SetPixels32(px);
            return t;
        }

        private static Texture2D BrassTexture()
        {
            var t = new Texture2D(16, 8, TextureFormat.RGBA32, false);
            for (var x = 0; x < 16; x++)
            for (var y = 0; y < 8; y++)
                t.SetPixel(x, y, x < 2 ? new Color32(140, 101, 38, 255) : y == 6 ? new Color32(232, 199, 108, 255) : new Color32(201, 154, 62, 255));
            return t;
        }

        private static Texture2D FlashTexture()
        {
            var t = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (var x = 0; x < 16; x++)
            for (var y = 0; y < 16; y++)
            {
                float dx = Mathf.Abs(x - 7.5f), dy = Mathf.Abs(y - 7.5f);
                var star = dx * dy < 6f && dx + dy < 9f;   // estrela de quatro pontas
                var core = dx < 2.5f && dy < 2.5f;
                t.SetPixel(x, y, core ? new Color(1f, 0.9f, 0.55f, 1f) : star ? new Color(1f, 0.55f, 0.1f, 1f) : new Color(0f, 0f, 0f, 0f));
            }
            return t;
        }
    }
}
