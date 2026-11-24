using System.IO;
using System.Linq;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Stats;
using Athoria.Gameplay.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Editor
{
    /// <summary>
    /// Gera o conteúdo PROVISÓRIO de combate: asset de feedback (sons +
    /// partículas), punhos, boneco de treino, dardo e armadilha.
    /// </summary>
    public static class CombatContentFactory
    {
        private const string DataFolder = "Assets/_Athoria/Data/Combate";
        private const string FxFolder = "Assets/_Athoria/Prefabs/FX";
        private const string WorldFolder = "Assets/_Athoria/Prefabs/Mundo";
        private const string SfxFolder = "Assets/_Athoria/Audio/SFX/Provisorios";

        public const string FeedbackPath = DataFolder + "/FeedbackCombate.asset";
        public const string FistsPath = "Assets/_Athoria/Data/Armas/Arma_Punhos.asset";
        public const string DummyPath = WorldFolder + "/BonecoDeTreino.prefab";
        public const string TrapPath = WorldFolder + "/ArmadilhaDeDardos.prefab";

        [MenuItem("Athoria/Dados/Gerar conteúdo de combate provisório")]
        public static void GenerateMenu()
        {
            GenerateAll();
            Debug.Log("[Athoria] Conteúdo de combate provisório gerado.");
        }

        public static CombatFeedback GenerateAll()
        {
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(FxFolder);
            Directory.CreateDirectory(WorldFolder);
            AssetDatabase.Refresh();

            var feedback = BuildFeedback();
            BuildFists();
            BuildDummy();
            BuildTrap(feedback);
            BuildArcaneBolt(feedback);
            AssetDatabase.SaveAssets();
            return feedback;
        }

        public static WeaponData LoadFists() => AssetDatabase.LoadAssetAtPath<WeaponData>(FistsPath);

        // ---------------- Feedback ----------------

        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/{name}.wav");

        private static CombatFeedback BuildFeedback()
        {
            var fb = AssetDatabase.LoadAssetAtPath<CombatFeedback>(FeedbackPath);
            if (fb == null)
            {
                fb = ScriptableObject.CreateInstance<CombatFeedback>();
                AssetDatabase.CreateAsset(fb, FeedbackPath);
            }

            var sparks = BuildFx("FX_Faiscas", new Color(1f, 0.85f, 0.4f), new Color(1f, 0.45f, 0.1f), 2.5f, 6f, 14, 0.035f, 0.6f, true);
            var chips = BuildFx("FX_Lascas", new Color(0.45f, 0.3f, 0.15f), new Color(0.3f, 0.18f, 0.08f), 1.5f, 3.5f, 10, 0.04f, 1.2f, false);
            var straw = BuildFx("FX_Palha", new Color(0.85f, 0.72f, 0.35f), new Color(0.6f, 0.48f, 0.2f), 1.2f, 3f, 14, 0.045f, 0.8f, false);
            var blood = BuildFx("FX_Sangue", new Color(0.45f, 0.02f, 0.02f), new Color(0.25f, 0f, 0f), 1.5f, 4f, 12, 0.05f, 1.5f, false);
            var dust = BuildFx("FX_Poeira", new Color(0.55f, 0.53f, 0.5f), new Color(0.35f, 0.34f, 0.32f), 0.8f, 2.5f, 10, 0.06f, 0.4f, false);

            fb.swingLight = new[] { Clip("golpe_vento_1"), Clip("golpe_vento_2"), Clip("golpe_vento_3") }.Where(c => c != null).ToArray();
            fb.swingHeavy = Clip("golpe_vento_pesado");
            fb.dodge = Clip("esquiva");
            fb.block = Clip("bloqueio");
            fb.guardBreak = Clip("guarda_quebrada");
            fb.blockParticles = sparks;
            fb.playerHurt = Clip("dano_recebido");
            fb.surfaces = new[]
            {
                Surface(SurfaceKind.Stone, new[] { Clip("acerto_pedra") }, dust),
                Surface(SurfaceKind.Wood, new[] { Clip("acerto_madeira") }, chips),
                Surface(SurfaceKind.Metal, new[] { Clip("acerto_metal") }, sparks),
                Surface(SurfaceKind.Flesh, new[] { Clip("acerto_carne") }, blood),
                Surface(SurfaceKind.Straw, new[] { Clip("acerto_palha") }, straw),
            };
            EditorUtility.SetDirty(fb);
            return fb;
        }

        private static CombatFeedback.SurfaceFx Surface(SurfaceKind kind, AudioClip[] clips, GameObject fx) =>
            new() { kind = kind, sounds = clips.Where(c => c != null).ToArray(), particles = fx };

        private static Material ParticleMaterial()
        {
            const string path = "Assets/_Athoria/Materials/Provisorios/Particula.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            mat = new Material(shader) { name = "Particula" };
            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            // Transparente com alpha blend
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject BuildFx(string name, Color a, Color b, float speedMin, float speedMax, int count, float size, float gravity, bool stretched)
        {
            var go = new GameObject(name);
            try
            {
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = ps.main;
                main.duration = 0.3f;
                main.loop = false;
                main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
                main.startColor = new ParticleSystem.MinMaxGradient(a, b);
                main.gravityModifier = gravity;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 64;
                main.stopAction = ParticleSystemStopAction.Destroy;

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 40f;
                shape.radius = 0.02f;

                var col = ps.colorOverLifetime;
                col.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
                col.color = grad;

                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = ParticleMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (stretched)
                {
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.velocityScale = 0.03f;
                    renderer.lengthScale = 1.5f;
                }

                return PrefabUtility.SaveAsPrefabAsset(go, $"{FxFolder}/{name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------------- Punhos ----------------

        private static void BuildFists()
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>(FistsPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(data, FistsPath);
            }
            data.EditorSetInfo("Punhos", "Combate desarmado. Rápido e fraco.");
            data.EditorConfigure(WeaponCategory.Unarmed, WeaponHandling.OneHanded, null,
                default, default, default, 6f, 2.2f, 1.5f, 6f, 14f, 2.2f);
        }

        // ---------------- Boneco de treino ----------------

        private static void BuildDummy()
        {
            var wood = EditorMaterials.GetOrCreate("Madeira", new Color(0.33f, 0.2f, 0.1f), 0f, 0.15f);
            var straw = EditorMaterials.GetOrCreate("Palha", new Color(0.72f, 0.6f, 0.3f), 0f, 0.1f);
            var sack = EditorMaterials.GetOrCreate("Saco", new Color(0.5f, 0.42f, 0.3f), 0f, 0.1f);

            var root = new GameObject("Boneco de Treino");
            try
            {
                Prim(root.transform, "Base", PrimitiveType.Cylinder, new Vector3(0, 0.05f, 0), new Vector3(0.6f, 0.05f, 0.6f), wood);
                var body = new GameObject("Corpo").transform;
                body.SetParent(root.transform, false);
                body.localPosition = new Vector3(0, 0.1f, 0);
                Prim(body, "Poste", PrimitiveType.Cylinder, new Vector3(0, 0.8f, 0), new Vector3(0.12f, 0.8f, 0.12f), wood);
                Prim(body, "Tronco", PrimitiveType.Capsule, new Vector3(0, 1.1f, 0), new Vector3(0.5f, 0.45f, 0.38f), straw);
                Prim(body, "Bracos", PrimitiveType.Cube, new Vector3(0, 1.3f, 0), new Vector3(1.0f, 0.1f, 0.1f), wood);
                Prim(body, "Cabeca", PrimitiveType.Sphere, new Vector3(0, 1.7f, 0), new Vector3(0.32f, 0.34f, 0.32f), sack);

                var health = root.AddComponent<Health>();
                SetFloat(health, "maxHealth", 120f);
                var reaction = root.AddComponent<HitReaction>();
                SetRef(reaction, "pivot", body);
                var reward = root.AddComponent<ExperienceReward>();
                var rSo = new SerializedObject(reward);
                rSo.FindProperty("experience").intValue = 15;
                rSo.FindProperty("displayName").stringValue = "Boneco de Treino";
                rSo.ApplyModifiedPropertiesWithoutUndo();
                var dummy = root.AddComponent<TrainingDummy>();
                SetRef(dummy, "body", body);
                var surface = root.AddComponent<SurfaceMaterial>();
                var so = new SerializedObject(surface);
                so.FindProperty("kind").enumValueIndex = (int)SurfaceKind.Straw;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, DummyPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------- Dardo e armadilha ----------------

        private static void BuildTrap(CombatFeedback feedback)
        {
            var bronze = EditorMaterials.GetOrCreate("BronzeEnvelhecido", new Color(0.55f, 0.38f, 0.18f), 0.8f, 0.45f);
            var stone = EditorMaterials.GetOrCreate("PedraArmadilha", new Color(0.3f, 0.29f, 0.27f), 0f, 0.1f);
            var red = EditorMaterials.GetOrCreate("VermelhoEscuro", new Color(0.35f, 0.06f, 0.05f), 0f, 0.2f);

            // Dardo
            var dartGo = new GameObject("Dardo");
            Projectile dartPrefab;
            try
            {
                var shaft = Prim(dartGo.transform, "Haste", PrimitiveType.Cube, Vector3.zero, new Vector3(0.025f, 0.025f, 0.28f), bronze);
                Object.DestroyImmediate(shaft.GetComponent<Collider>());
                var proj = dartGo.AddComponent<Projectile>();
                SetRef(proj, "feedback", feedback);
                dartPrefab = PrefabUtility.SaveAsPrefabAsset(dartGo, $"{WorldFolder}/Dardo.prefab").GetComponent<Projectile>();
            }
            finally
            {
                Object.DestroyImmediate(dartGo);
            }

            // Armadilha
            var root = new GameObject("Armadilha de Dardos");
            try
            {
                Prim(root.transform, "Bloco", PrimitiveType.Cube, Vector3.zero, new Vector3(0.6f, 0.6f, 0.6f), stone);
                Prim(root.transform, "Boca", PrimitiveType.Cube, new Vector3(0, 0, 0.31f), new Vector3(0.18f, 0.18f, 0.04f), red);
                var muzzle = new GameObject("Saida").transform;
                muzzle.SetParent(root.transform, false);
                muzzle.localPosition = new Vector3(0, 0, 0.45f);

                var trap = root.AddComponent<DartTrap>();
                SetRef(trap, "projectilePrefab", dartPrefab);
                SetRef(trap, "muzzle", muzzle);
                SetRef(trap, "fireSound", Clip("dardo_disparo"));
                PrefabUtility.SaveAsPrefabAsset(root, TrapPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------- Magia ----------------

        private static void BuildArcaneBolt(CombatFeedback feedback)
        {
            var crystal = EditorMaterials.GetOrCreate("CristalArcano", new Color(0.45f, 0.2f, 0.85f), 0f, 0.8f, new Color(0.9f, 0.4f, 2.2f));
            var root = new GameObject("Projetil Arcano");
            try
            {
                var core = Prim(root.transform, "Nucleo", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.16f, crystal);
                Object.DestroyImmediate(core.GetComponent<Collider>());
                core.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var shell = Prim(root.transform, "Aura", PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.11f, crystal);
                shell.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                Object.DestroyImmediate(shell.GetComponent<Collider>());
                shell.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

                var light = root.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.65f, 0.4f, 1f);
                light.range = 4f;
                light.intensity = 2.5f;
                light.shadows = LightShadows.None;

                var proj = root.AddComponent<Projectile>();
                SetRef(proj, "feedback", feedback);
                SetFloat(proj, "radius", 0.14f);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{WorldFolder}/ProjetilArcano.prefab");

                var staff = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Athoria/Data/Armas/Arma_Cajado.asset");
                if (staff != null) staff.EditorSetSpell(prefab, 12f, 18f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------- Utilitários ----------------

        private static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
