using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Athoria.Gameplay.Items;
using UnityEditor;
using UnityEngine;

namespace Athoria.Editor
{
    /// <summary>
    /// Gera armas PROVISÓRIAS (modelos feitos de blocos + dados de combate)
    /// para testar o sistema até existirem os modelos 3D definitivos.
    /// Para trocar o visual depois, basta apontar o "View Model Prefab" do
    /// WeaponData para o modelo novo e ajustar as poses no Inspector.
    /// Convenção dos modelos: empunhadura na origem, arma apontando para +Y.
    /// </summary>
    public static class PlaceholderWeaponFactory
    {
        private const string PrefabFolder = "Assets/_Athoria/Prefabs/ViewModels/Armas";
        private const string DataFolder = "Assets/_Athoria/Data/Armas";

        // Poses dos braços (espaço da câmera). Braço: raiz no cotovelo, apontando para +Z.
        private static readonly ViewPose RightOneHand = new(new Vector3(0.26f, -0.42f, 0.32f), new Vector3(-14f, -12f, 0f));
        private static readonly ViewPose RightTwoHand = new(new Vector3(0.22f, -0.44f, 0.3f), new Vector3(-16f, -18f, 0f));
        private static readonly ViewPose LeftSupport = new(new Vector3(0f, -0.5f, 0.38f), new Vector3(-22f, 22f, 0f));
        private static readonly ViewPose LeftShield = new(new Vector3(-0.34f, -0.46f, 0.34f), new Vector3(-5f, 14f, 0f));
        private static readonly ViewPose NoPose = default;

        private struct Spec
        {
            public string Id, Name, Description;
            public WeaponCategory Category;
            public WeaponHandling Handling;
            public Action<Transform> Build;
            public ViewPose Model, Holding, Support;
            public float Damage, Speed, Range, Light, Heavy, HeavyMult;
        }

        [MenuItem("Athoria/Dados/Gerar armas provisórias")]
        public static void GenerateMenu()
        {
            var all = GenerateAll();
            Debug.Log($"[Athoria] {all.Count} armas provisórias geradas em {DataFolder}.");
        }

        public static Dictionary<string, WeaponData> GenerateAll()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(DataFolder);

            var steel = EditorMaterials.GetOrCreate("Aco", new Color(0.62f, 0.63f, 0.66f), 0.85f, 0.55f);
            var darkSteel = EditorMaterials.GetOrCreate("AcoEscuro", new Color(0.28f, 0.28f, 0.30f), 0.8f, 0.4f);
            var wood = EditorMaterials.GetOrCreate("Madeira", new Color(0.33f, 0.2f, 0.1f), 0f, 0.15f);
            var leather = EditorMaterials.GetOrCreate("Couro", new Color(0.22f, 0.13f, 0.07f), 0f, 0.25f);
            var bronze = EditorMaterials.GetOrCreate("BronzeEnvelhecido", new Color(0.55f, 0.38f, 0.18f), 0.8f, 0.45f);
            var crystal = EditorMaterials.GetOrCreate("CristalArcano", new Color(0.45f, 0.2f, 0.85f), 0f, 0.8f, new Color(0.9f, 0.4f, 2.2f));
            var bloodRed = EditorMaterials.GetOrCreate("VermelhoEscuro", new Color(0.35f, 0.06f, 0.05f), 0f, 0.2f);

            var specs = new[]
            {
                new Spec
                {
                    Id = "Espada", Name = "Espada de Ferro (provisória)", Description = "Lâmina reta equilibrada. Arma de uma mão.",
                    Category = WeaponCategory.Sword, Handling = WeaponHandling.OneHanded,
                    Build = t =>
                    {
                        Part(t, "Empunhadura", new Vector3(0, 0, 0), new Vector3(0.035f, 0.17f, 0.035f), leather);
                        Part(t, "Pomo", new Vector3(0, -0.1f, 0), new Vector3(0.05f, 0.04f, 0.05f), bronze);
                        Part(t, "Guarda", new Vector3(0, 0.1f, 0), new Vector3(0.2f, 0.03f, 0.05f), bronze);
                        Part(t, "Lamina", new Vector3(0, 0.44f, 0), new Vector3(0.055f, 0.66f, 0.012f), steel);
                        Part(t, "Ponta", new Vector3(0, 0.78f, 0), new Vector3(0.039f, 0.039f, 0.012f), steel, new Vector3(0, 0, 45));
                    },
                    Model = new ViewPose(Vector3.zero, new Vector3(50f, 0f, 18f)), Holding = RightOneHand,
                    Damage = 18, Speed = 1.4f, Range = 2.1f, Light = 12, Heavy = 25, HeavyMult = 1.8f
                },
                new Spec
                {
                    Id = "Machado", Name = "Machado de Batalha (provisório)", Description = "Golpes pesados que rompem defesas. Uma mão.",
                    Category = WeaponCategory.Axe, Handling = WeaponHandling.OneHanded,
                    Build = t =>
                    {
                        Part(t, "Cabo", new Vector3(0, 0.18f, 0), new Vector3(0.04f, 0.62f, 0.04f), wood);
                        Part(t, "Cabeca", new Vector3(0.07f, 0.43f, 0), new Vector3(0.16f, 0.16f, 0.025f), darkSteel);
                        Part(t, "Fio", new Vector3(0.155f, 0.43f, 0), new Vector3(0.025f, 0.22f, 0.02f), steel);
                    },
                    Model = new ViewPose(Vector3.zero, new Vector3(45f, 0f, 14f)), Holding = RightOneHand,
                    Damage = 22, Speed = 1.1f, Range = 2f, Light = 14, Heavy = 28, HeavyMult = 1.9f
                },
                new Spec
                {
                    Id = "Adaga", Name = "Adaga (provisória)", Description = "Rápida e leve. Recompensa a Destreza.",
                    Category = WeaponCategory.Dagger, Handling = WeaponHandling.OneHanded,
                    Build = t =>
                    {
                        Part(t, "Empunhadura", Vector3.zero, new Vector3(0.03f, 0.11f, 0.03f), leather);
                        Part(t, "Guarda", new Vector3(0, 0.065f, 0), new Vector3(0.1f, 0.02f, 0.035f), bronze);
                        Part(t, "Lamina", new Vector3(0, 0.2f, 0), new Vector3(0.04f, 0.25f, 0.01f), steel);
                    },
                    Model = new ViewPose(Vector3.zero, new Vector3(75f, 0f, 10f)), Holding = new ViewPose(new Vector3(0.25f, -0.4f, 0.34f), new Vector3(-10f, -14f, 0f)),
                    Damage = 11, Speed = 2.2f, Range = 1.5f, Light = 8, Heavy = 16, HeavyMult = 2.2f
                },
                new Spec
                {
                    Id = "Martelo", Name = "Martelo de Guerra (provisório)", Description = "Arma de duas mãos, lenta e devastadora.",
                    Category = WeaponCategory.Hammer, Handling = WeaponHandling.TwoHanded,
                    Build = t =>
                    {
                        Part(t, "Cabo", new Vector3(0, 0.2f, 0), new Vector3(0.045f, 0.8f, 0.045f), wood);
                        Part(t, "Cabeca", new Vector3(0, 0.6f, 0), new Vector3(0.24f, 0.13f, 0.13f), darkSteel);
                        Part(t, "Aro", new Vector3(0, 0.52f, 0), new Vector3(0.07f, 0.03f, 0.07f), bronze);
                    },
                    Model = new ViewPose(Vector3.zero, new Vector3(45f, 0f, 30f)), Holding = RightTwoHand, Support = LeftSupport,
                    Damage = 34, Speed = 0.75f, Range = 2.3f, Light = 22, Heavy = 40, HeavyMult = 1.7f
                },
                new Spec
                {
                    Id = "Lanca", Name = "Lança (provisória)", Description = "Maior alcance. Duas mãos.",
                    Category = WeaponCategory.Spear, Handling = WeaponHandling.TwoHanded,
                    Build = t =>
                    {
                        Part(t, "Haste", new Vector3(0, 0.2f, 0), new Vector3(0.035f, 1.7f, 0.035f), wood);
                        Part(t, "Ponta", new Vector3(0, 1.12f, 0), new Vector3(0.06f, 0.06f, 0.015f), steel, new Vector3(0, 0, 45));
                        Part(t, "Lamina", new Vector3(0, 1.2f, 0), new Vector3(0.045f, 0.16f, 0.012f), steel);
                        Part(t, "Fita", new Vector3(0, 1.0f, 0), new Vector3(0.045f, 0.05f, 0.045f), bloodRed);
                    },
                    Model = new ViewPose(new Vector3(0f, 0f, -0.05f), new Vector3(82f, 0f, 0f)),
                    Holding = new ViewPose(new Vector3(0.22f, -0.42f, 0.26f), new Vector3(-4f, -6f, 0f)),
                    Support = new ViewPose(new Vector3(0f, -0.46f, 0.42f), new Vector3(-6f, 8f, 0f)),
                    Damage = 20, Speed = 1f, Range = 3f, Light = 14, Heavy = 28, HeavyMult = 1.8f
                },
                new Spec
                {
                    Id = "Escudo", Name = "Escudo de Madeira (provisório)", Description = "Mão secundária. Bloqueia ataques frontais.",
                    Category = WeaponCategory.Shield, Handling = WeaponHandling.OffHand,
                    Build = t =>
                    {
                        Part(t, "Tabuas", new Vector3(0, 0.04f, 0.05f), new Vector3(0.36f, 0.44f, 0.035f), wood);
                        Part(t, "Borda Sup", new Vector3(0, 0.26f, 0.05f), new Vector3(0.38f, 0.025f, 0.045f), bronze);
                        Part(t, "Borda Inf", new Vector3(0, -0.18f, 0.05f), new Vector3(0.38f, 0.025f, 0.045f), bronze);
                        Part(t, "Umbo", new Vector3(0, 0.04f, 0.08f), new Vector3(0.1f, 0.1f, 0.04f), darkSteel);
                    },
                    Model = new ViewPose(Vector3.zero, new Vector3(0f, -12f, 0f)), Holding = LeftShield,
                    Damage = 4, Speed = 1f, Range = 1.4f, Light = 10, Heavy = 15, HeavyMult = 1.5f
                },
                new Spec
                {
                    Id = "Cajado", Name = "Cajado Arcano (provisório)", Description = "Arma mágica de duas mãos. Canaliza Inteligência.",
                    Category = WeaponCategory.Magic, Handling = WeaponHandling.TwoHanded,
                    Build = t =>
                    {
                        Part(t, "Haste", new Vector3(0, 0.25f, 0), new Vector3(0.04f, 1.1f, 0.04f), wood);
                        Part(t, "Garra", new Vector3(0, 0.82f, 0), new Vector3(0.09f, 0.04f, 0.09f), bronze);
                        Part(t, "Cristal", new Vector3(0, 0.9f, 0), new Vector3(0.08f, 0.08f, 0.08f), crystal, new Vector3(45, 0, 45));
                    },
                    Model = new ViewPose(Vector3.zero, new Vector3(25f, 0f, 12f)), Holding = RightTwoHand, Support = LeftSupport,
                    Damage = 14, Speed = 1f, Range = 2.4f, Light = 10, Heavy = 20, HeavyMult = 1.6f
                },
            };

            specs = specs.Concat(new[]
            {
                new Spec
                {
                    Id = "Khopesh", Name = "Khopesh (provisório)", Description = "Espada curva em forma de foice. Golpes cortantes de uma mão.",
                    Category = WeaponCategory.Sword, Handling = WeaponHandling.OneHanded,
                    Build = t =>
                    {
                        Part(t, "Empunhadura", Vector3.zero, new Vector3(0.035f, 0.16f, 0.035f), leather);
                        Part(t, "Pomo", new Vector3(0, -0.095f, 0), new Vector3(0.05f, 0.035f, 0.05f), bronze);
                        Part(t, "Guarda", new Vector3(0, 0.09f, 0), new Vector3(0.09f, 0.025f, 0.045f), bronze);
                        Part(t, "Lamina Reta", new Vector3(0, 0.23f, 0), new Vector3(0.045f, 0.26f, 0.012f), steel);
                        Part(t, "Curva", new Vector3(0.07f, 0.43f, 0), new Vector3(0.07f, 0.24f, 0.012f), steel, new Vector3(0, 0, -35f));
                        Part(t, "Gancho", new Vector3(0.155f, 0.5f, 0), new Vector3(0.05f, 0.12f, 0.012f), steel, new Vector3(0, 0, -80f));
                    },
                    Model = new ViewPose(Vector3.zero, new Vector3(45f, 0f, 14f)), Holding = RightOneHand,
                    Damage = 19, Speed = 1.35f, Range = 1.9f, Light = 11, Heavy = 24, HeavyMult = 1.85f
                },
                new Spec
                {
                    Id = "Arpao", Name = "Arpão de Caça (provisório)", Description = "Arpão de pesca e caça da Costa dos Ventos. Estocadas de longo alcance com uma mão.",
                    Category = WeaponCategory.Spear, Handling = WeaponHandling.OneHanded,
                    Build = t =>
                    {
                        Part(t, "Haste", new Vector3(0, 0.25f, 0), new Vector3(0.03f, 1.3f, 0.03f), wood);
                        Part(t, "Ponta", new Vector3(0, 0.95f, 0), new Vector3(0.05f, 0.05f, 0.012f), steel, new Vector3(0, 0, 45));
                        Part(t, "Farpa Esq", new Vector3(-0.03f, 0.9f, 0), new Vector3(0.012f, 0.07f, 0.01f), steel, new Vector3(0, 0, -30));
                        Part(t, "Farpa Dir", new Vector3(0.03f, 0.9f, 0), new Vector3(0.012f, 0.07f, 0.01f), steel, new Vector3(0, 0, 30));
                        Part(t, "Corda", new Vector3(0, -0.2f, 0), new Vector3(0.06f, 0.08f, 0.06f), leather);
                    },
                    Model = new ViewPose(new Vector3(0f, 0f, -0.15f), new Vector3(82f, 0f, 0f)),
                    Holding = new ViewPose(new Vector3(0.22f, -0.4f, 0.26f), new Vector3(-4f, -6f, 0f)),
                    Damage = 17, Speed = 1.3f, Range = 2.6f, Light = 10, Heavy = 22, HeavyMult = 1.8f
                },
                new Spec
                {
                    Id = "Rifle", Name = "Rifle de Precisão (provisório)", Description = "Arma de fogo de longo alcance. Segure o ataque para um tiro mirado.",
                    Category = WeaponCategory.Firearm, Handling = WeaponHandling.TwoHanded,
                    Build = t =>
                    {
                        Part(t, "Coronha", new Vector3(0, -0.22f, 0.01f), new Vector3(0.05f, 0.3f, 0.09f), wood);
                        Part(t, "Corpo", new Vector3(0, 0.08f, 0), new Vector3(0.045f, 0.34f, 0.06f), darkSteel);
                        Part(t, "Cano", new Vector3(0, 0.5f, -0.01f), new Vector3(0.025f, 0.55f, 0.025f), darkSteel);
                        Part(t, "Guarda-mao", new Vector3(0, 0.33f, 0.01f), new Vector3(0.045f, 0.22f, 0.05f), wood);
                        Part(t, "Luneta", new Vector3(0, 0.12f, -0.055f), new Vector3(0.035f, 0.22f, 0.035f), bronze);
                    },
                    Model = new ViewPose(new Vector3(0f, -0.02f, -0.1f), new Vector3(88f, 0f, 0f)),
                    Holding = new ViewPose(new Vector3(0.16f, -0.36f, 0.16f), new Vector3(-2f, -4f, 0f)),
                    Support = new ViewPose(new Vector3(0.02f, -0.4f, 0.36f), new Vector3(-4f, 10f, 0f)),
                    Damage = 30, Speed = 0.6f, Range = 40f, Light = 4, Heavy = 8, HeavyMult = 1.6f
                },
            }).ToArray();

            var result = new Dictionary<string, WeaponData>();
            foreach (var spec in specs)
            {
                var prefab = BuildPrefab(spec);
                var data = GetOrCreateData(spec.Id);
                data.EditorSetInfo(spec.Name, spec.Description);
                data.EditorConfigure(spec.Category, spec.Handling, prefab, spec.Model, spec.Holding,
                    spec.Handling == WeaponHandling.TwoHanded ? spec.Support : NoPose,
                    spec.Damage, spec.Speed, spec.Range, spec.Light, spec.Heavy, spec.HeavyMult);
                result[spec.Id] = data;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static GameObject BuildPrefab(Spec spec)
        {
            var root = new GameObject("VM_" + spec.Id);
            try
            {
                spec.Build(root.transform);
                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/VM_{spec.Id}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static WeaponData GetOrCreateData(string id)
        {
            var path = $"{DataFolder}/Arma_{id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (data != null) return data;
            data = ScriptableObject.CreateInstance<WeaponData>();
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        private static void Part(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
