using System.IO;
using Athoria.Gameplay.Characters;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Items;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Editor
{
    /// <summary>
    /// Cria as personagens jogáveis (Layla, Evelyn, Anaíra, Akane), o banco de
    /// personagens e a munição do rifle. Textos seguem a Bíblia de Athoria;
    /// classes, números e características são propostas de jogo ajustáveis.
    /// </summary>
    public static class CharacterContentFactory
    {
        private const string Folder = "Assets/_Athoria/Data/Personagens";
        public const string DatabasePath = Folder + "/BancoDePersonagens.asset";
        private const string Art = "Assets/_Athoria/Art/Legacy/";

        [MenuItem("Athoria/Dados/Gerar personagens")]
        public static void GenerateMenu()
        {
            GenerateAll();
            Debug.Log("[Athoria] Personagens geradas em " + Folder);
        }

        public static CharacterDatabase GenerateAll()
        {
            Directory.CreateDirectory(Folder);
            var weapons = PlaceholderWeaponFactory.GenerateAll();
            BuildRifleAmmo(weapons["Rifle"]);

            var layla = Make("Layla",
                "Layla Thornwood", "A Lança do Ipê Rosa", "Lanceira", "Lacúnia", new Color(0.95f, 0.55f, 0.72f),
                "Herdeira da tradicional família Thornwood, de Lacúnia. Carrega uma ligação artificial com o Ipê Rosa, " +
                "fruto dos experimentos conduzidos por sua própria mãe — uma força diferente daquela que se manifesta naturalmente nos outros.",
                "layla", "brasao_lacunia", 100, 100, 50,
                new[]
                {
                    Trait("Lança Thornwood", "+15% de dano com lanças.", TraitEffect.DamagePercentWithCategory, 15, WeaponCategory.Spear),
                    Trait("Ipê Rosa", "A ligação com o Ipê Rosa fortalece o corpo: +20 de vida máxima.", TraitEffect.MaxHealth, 20),
                },
                new[] { weapons["Lanca"], weapons["Adaga"] }, null);

            var evelyn = Make("Evelyn",
                "Evelyn Lancaster", "Dama de Ferro", "Atiradora de Elite", "Terras Alagadas", new Color(0.6f, 0.78f, 0.45f),
                "Atiradora de elite e médica de combate da família Lancaster, conhecida pelos saberes de botânica, alquimia e " +
                "sobrevivência nas Terras Alagadas. Não usa magia: confia na precisão e no conhecimento médico.",
                "evelyn", "brasao_terras_alagadas", 95, 100, 0,
                new[]
                {
                    Trait("Médica de Combate", "Regenera 2 de vida por segundo depois de alguns segundos sem levar dano.", TraitEffect.HealthRegenOutOfCombat, 2),
                    Trait("Olho de Atiradora", "+20% de dano com armas de fogo.", TraitEffect.DamagePercentWithCategory, 20, WeaponCategory.Firearm),
                },
                new[] { weapons["Rifle"], weapons["Adaga"] }, null);

            var anaira = Make("Anaira",
                "Anaíra", "Cavaleira de Costa dos Ventos", "Caçadora dos Mares", "Costa dos Ventos", new Color(0.96f, 0.7f, 0.3f),
                "Alta, de pele escura e sorriso contagiante, Anaíra é a alma alegre e vibrante da Costa dos Ventos, " +
                "terra de praias, festividades e comércio marítimo. É uma grande navegadora e luta com um khopesh.",
                "anaira", "brasao_costa_dos_ventos", 100, 110, 40,
                new[]
                {
                    Trait("Navegadora", "+10% de velocidade de movimento.", TraitEffect.MoveSpeedPercent, 10),
                    Trait("Lâmina Curva", "+15% de dano com espadas.", TraitEffect.DamagePercentWithCategory, 15, WeaponCategory.Sword),
                },
                new[] { weapons["Khopesh"], weapons["Adaga"] }, null);

            var akane = Make("Akane",
                "Akane", "Herdeira do Manto de Yuna", "Espadachim", "Kaminari no Kumi", new Color(0.55f, 0.72f, 1f),
                "Acolhida, criada e treinada por Yuna Kazehara, Akane tornou-se para ela uma filha. Recebeu de Yuna o manto de Cavaleira " +
                "e representa a nova geração de Kaminari no Kumi — sem carregar o sobrenome Kazehara.",
                "akane", "brasao_kaminari_no_kumi", 100, 100, 50,
                new[]
                {
                    Trait("Disciplina de Kaminari", "Esquivas custam 30% menos vigor.", TraitEffect.DodgeStaminaReductionPercent, 30),
                    Trait("Lâmina da Herdeira", "+10% de dano com espadas.", TraitEffect.DamagePercentWithCategory, 10, WeaponCategory.Sword),
                },
                new[] { weapons["Espada"], weapons["Adaga"] }, null);

            var db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<CharacterDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }
            db.EditorSet(new[] { layla, evelyn, anaira, akane });
            AssetDatabase.SaveAssets();
            return db;
        }

        private static CharacterTrait Trait(string name, string desc, TraitEffect effect, float value, WeaponCategory category = WeaponCategory.Sword) =>
            new() { name = name, description = desc, effect = effect, value = value, category = category };

        private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + path);

        private static CharacterData Make(string id, string displayName, string title, string cls, string region, Color accent,
            string description, string artKey, string emblem, float hp, float stamina, float mana,
            CharacterTrait[] traits, WeaponData[] weapons, WeaponData offHand)
        {
            var path = $"{Folder}/Personagem_{id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.EditorSetInfo(displayName, description, Sprite($"RetratosHUD/hud_{artKey}.png"));
            data.EditorConfigure(title, cls, region, accent,
                Sprite($"Retratos/{artKey}_normal.png"), Sprite($"RetratosHUD/hud_{artKey}.png"),
                Sprite($"CorpoInteiro/corpo_{artKey}.png"), Sprite($"Brasoes/{emblem}.png"),
                hp, stamina, mana, traits, weapons, offHand);
            return data;
        }

        private static void BuildRifleAmmo(WeaponData rifle)
        {
            var feedback = CombatContentFactory.GenerateAll();
            var brass = EditorMaterials.GetOrCreate("Latao", new Color(0.85f, 0.65f, 0.3f), 0.9f, 0.6f, new Color(0.6f, 0.4f, 0.1f));
            var root = new GameObject("Bala");
            try
            {
                var tracer = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tracer.name = "Rastro";
                Object.DestroyImmediate(tracer.GetComponent<Collider>());
                tracer.transform.SetParent(root.transform, false);
                tracer.transform.localScale = new Vector3(0.015f, 0.015f, 0.3f);
                var r = tracer.GetComponent<Renderer>();
                r.sharedMaterial = brass;
                r.shadowCastingMode = ShadowCastingMode.Off;

                var proj = root.AddComponent<Projectile>();
                var so = new SerializedObject(proj);
                so.FindProperty("feedback").objectReferenceValue = feedback;
                so.FindProperty("radius").floatValue = 0.03f;
                so.FindProperty("speed").floatValue = 90f;
                so.ApplyModifiedPropertiesWithoutUndo();

                Directory.CreateDirectory("Assets/_Athoria/Prefabs/Mundo");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Athoria/Prefabs/Mundo/Bala.prefab");
                var shot = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Athoria/Audio/Legacy/snd_tiro.wav");
                rifle.EditorSetSpell(prefab, 0f, 90f, shot);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
