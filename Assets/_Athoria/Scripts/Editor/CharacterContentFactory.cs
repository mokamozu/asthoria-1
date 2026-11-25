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
            PixelFxImporter.ImportAll();
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
                new[] { weapons["Alabarda"] }, null);

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
                new[] { weapons["Rifle"], weapons["Adaga"] }, null);   // rifle + faca de campo

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
                new[] { weapons["Khopesh"] }, null);

            var akane = Make("Akane",
                "Akane", "Herdeira do Manto de Yuna", "Espadachim Espiritual", "Kaminari no Kumi", new Color(0.55f, 0.72f, 1f),
                "Acolhida, criada e treinada por Yuna Kazehara, Akane tornou-se para ela uma filha. Recebeu de Yuna o manto de Cavaleira " +
                "e representa a nova geração de Kaminari no Kumi — sem carregar o sobrenome Kazehara.",
                "akane", "brasao_kaminari_no_kumi", 100, 100, 50,
                new[]
                {
                    Trait("Disciplina de Kaminari", "Esquivas custam 30% menos vigor.", TraitEffect.DodgeStaminaReductionPercent, 30),
                    Trait("Lâmina da Herdeira", "+10% de dano com espadas.", TraitEffect.DamagePercentWithCategory, 10, WeaponCategory.Sword),
                },
                new[] { weapons["Katana"] }, null);

            // ----- Outras cavaleiras (cânone em claude/Athoria_Decisoes_de_Canon.md; números são propostas de jogo) -----
            BuildShotgunAmmo(weapons["Escopeta"]);
            var anne = Make("Anne",
                "Anne", "A Pirata dos Ventos", "Arpoadora", "Costa dos Ventos", new Color(0.92f, 0.5f, 0.22f),
                "Cavaleira da Costa dos Ventos, nascida e criada entre piratas e bandidos, de jeito rústico. Usa o arpão de pesca " +
                "para se mover pelos telhados e mastros, e uma escopeta calibre 12 antiga para resolver tudo de perto.",
                "anne", "brasao_costa_dos_ventos", 95, 110, 0,
                new[]
                {
                    Trait("Criada entre Piratas", "+15% de dano com armas de fogo.", TraitEffect.DamagePercentWithCategory, 15, WeaponCategory.Firearm),
                    Trait("Arpoadora", "+15% de dano com o arpão (lanças).", TraitEffect.DamagePercentWithCategory, 15, WeaponCategory.Spear),
                },
                new[] { weapons["Arpao"], weapons["Escopeta"] }, null);

            var elena = Make("Elena",
                "Elena", "Cavaleira das Terras Alagadas", "Guardiã da Peste", "Terras Alagadas", new Color(0.62f, 0.36f, 0.78f),
                "Cavaleira das Terras Alagadas que acredita que a peste do pântano é uma bênção. Veste uma armadura escura " +
                "e caminha onde os outros não ousam respirar.",
                "elena_terras_alagadas", "brasao_terras_alagadas", 110, 95, 40,
                new[]
                {
                    Trait("Abençoada pela Peste", "Regenera 1,5 de vida por segundo fora de combate.", TraitEffect.HealthRegenOutOfCombat, 1.5f),
                    Trait("Montante", "+15% de dano com espadas.", TraitEffect.DamagePercentWithCategory, 15, WeaponCategory.Sword),
                },
                new[] { weapons["Montante"] }, null, hasFullBody: false);

            var emily = Make("Emily",
                "Emily Thornwood", "A Sombra de Lacúnia", "Sombra", "Lacúnia", new Color(0.78f, 0.4f, 0.3f),
                "Irmã mais nova de Layla, nunca manifestou o Ipê Rosa. Age como uma sombra sob a cidade de Lacúnia — uma Cavaleira " +
                "pouco vista — e domina uma serpente que dorme abaixo do grande Ipê Rosa.",
                "emily", "brasao_lacunia", 90, 110, 50,
                new[]
                {
                    Trait("Sombra de Lacúnia", "Esquivas custam 35% menos vigor.", TraitEffect.DodgeStaminaReductionPercent, 35),
                    Trait("Presas", "+20% de dano com adagas.", TraitEffect.DamagePercentWithCategory, 20, WeaponCategory.Dagger),
                },
                new[] { weapons["PresaSerpente"] }, null);

            var yuna = Make("Yuna",
                "Yuna Kazehara", "A Carrasca de Mãos de Ferro", "Carrasca", "Kaminari no Kumi", new Color(0.4f, 0.72f, 0.62f),
                "Última sobrevivente sanguínea dos Kazehara. Depois do massacre de sua família, tornou-se uma Carrasca de mãos de ferro; " +
                "foi Akane quem recuperou parte de sua humanidade. Mestra e mãe adotiva de Akane.",
                "yuna", "brasao_kaminari_no_kumi", 115, 100, 30,
                new[]
                {
                    Trait("Mãos de Ferro", "+20% de dano com clavas e martelos.", TraitEffect.DamagePercentWithCategory, 20, WeaponCategory.Hammer),
                    Trait("Última Kazehara", "+25 de vida máxima.", TraitEffect.MaxHealth, 25),
                },
                new[] { weapons["Kanabo"] }, null);

            // Habilidades especiais (Q) — propostas de jogo ajustáveis
            layla.EditorSetAbility(Ability("Layla", "Bênção do Ipê Rosa", "A força do Ipê Rosa fecha feridas: recupera 40 de vida ao longo de 4 segundos.",
                "habilidade_layla", AbilityEffect.HealOverTime, 30f, 15f, 0f, 40f, 4f, 0f));
            evelyn.EditorSetAbility(Ability("Evelyn", "Kit de Campo", "Médica de combate: cura 35 de vida na hora. Não usa mana.",
                "habilidade_evelyn", AbilityEffect.Heal, 20f, 0f, 0f, 35f, 0f, 0f));
            anaira.EditorSetAbility(Ability("Anaira", "Ventos da Costa", "+30% de velocidade por 6 segundos e recupera 50 de vigor.",
                "habilidade_anaira", AbilityEffect.SpeedBoost, 25f, 10f, 0f, 50f, 6f, 30f));
            // Akane invoca espíritos: Mantras Ativos (começa com 4) e o Lobo Espiritual no Q.
            // Recarga e mana são PROVISÓRIAS (as do antigo Corte Relâmpago) até o balanceamento.
            var lobo = Ability("Akane", "Lobo Espiritual",
                "Invoca um lobo espiritual que acompanha Akane e ataca sozinho os inimigos próximos. " +
                "Ocupa 1 Mantra; a recarga só começa quando ele desaparece.",
                "habilidade_akane", AbilityEffect.SummonSpirit, 12f, 10f, 0f, 0f, 0f, 0f);
            lobo.EditorSetSpirit(SpiritContentFactory.BuildSpiritWolf());
            akane.EditorSetAbility(lobo);
            akane.EditorSetMantraSlots(4);

            anne.EditorSetAbility(Ability("Anne", "Arpão de Abordagem", "Dispara o arpão onde você mira (até 22 m) e se puxa até lá — paredes, telhados, árvores.",
                "habilidade_anne", AbilityEffect.Grapple, 6f, 0f, 15f, 0f, 0f, 22f));
            elena.EditorSetAbility(Ability("Elena", "Bênção da Peste", "A peste que ela venera fecha suas feridas: recupera 45 de vida ao longo de 5 segundos.",
                "habilidade_elena_terras_alagadas", AbilityEffect.HealOverTime, 28f, 15f, 0f, 45f, 5f, 0f));
            emily.EditorSetAbility(Ability("Emily", "Bote da Serpente", "Avança 5 metros como a serpente que comanda, mordendo tudo no caminho (30 de dano).",
                "habilidade_emily", AbilityEffect.DashStrike, 10f, 10f, 0f, 30f, 0f, 5f));
            yuna.EditorSetAbility(Ability("Yuna", "Gole da Cabaça", "Um gole da cabaça: recupera 40 de vida na hora.",
                "habilidade_yuna", AbilityEffect.Heal, 22f, 0f, 0f, 40f, 0f, 0f));

            // Efeitos PixelFX (se importados): círculos de cura e raio
            var healPink = PixelFxImporter.Load("fx_cura_ipe_rosa");
            var healGreen = PixelFxImporter.Load("fx_cura_evelyn");
            var bolt = PixelFxImporter.Load("fx_raio_kaminari");
            if (healPink != null) healPink.EditorSetScale(64f, 0.85f);
            if (healGreen != null) healGreen.EditorSetScale(64f, 0.85f);
            if (bolt != null) bolt.EditorSetScale(64f, 1f);
            layla.Ability.EditorSetFx(healPink, 2.6f, null);
            evelyn.Ability.EditorSetFx(healGreen, 2.6f, null);
            anaira.Ability.EditorSetFx(null, 0f, null);
            akane.Ability.EditorSetFx(null, 0f, bolt);

            var db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<CharacterDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }
            db.EditorSet(new[] { layla, evelyn, anaira, akane, anne, elena, emily, yuna });
            AssetDatabase.SaveAssets();
            return db;
        }

        private static AbilityData Ability(string id, string name, string desc, string icon, AbilityEffect effect,
            float cooldown, float mana, float stamina, float amount, float duration, float magnitude)
        {
            var path = $"{Folder}/Habilidade_{id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<AbilityData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<AbilityData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.EditorSetInfo(name, desc, Sprite($"Icones/Habilidades/{icon}.png"));
            data.EditorConfigure(effect, cooldown, mana, stamina, amount, duration, magnitude);
            return data;
        }

        private static CharacterTrait Trait(string name, string desc, TraitEffect effect, float value, WeaponCategory category = WeaponCategory.Sword) =>
            new() { name = name, description = desc, effect = effect, value = value, category = category };

        private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + path);

        private static CharacterData Make(string id, string displayName, string title, string cls, string region, Color accent,
            string description, string artKey, string emblem, float hp, float stamina, float mana,
            CharacterTrait[] traits, WeaponData[] weapons, WeaponData offHand, bool hasFullBody = true)
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
                hasFullBody ? Sprite($"CorpoInteiro/corpo_{artKey}.png") : null, Sprite($"Brasoes/{emblem}.png"),
                hp, stamina, mana, traits, weapons, offHand);
            data.EditorSetAlternatePortrait(Sprite($"Retratos/{artKey}_alternativo.png"));
            var arms = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/_Athoria/Art/Personagens/Bracos/bracos_{artKey}.png");
            PsxAssets.Crisp(arms);
            data.EditorSetArmsTexture(arms);
            return data;
        }

        /// <summary>Chumbo da escopeta: bala curta que some depois de ~15 m; 8 por disparo, em cone.</summary>
        private static void BuildShotgunAmmo(WeaponData shotgun)
        {
            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Athoria/Prefabs/Mundo/Bala.prefab");
            if (bullet == null) return;
            const string path = "Assets/_Athoria/Prefabs/Mundo/Chumbo.prefab";
            var root = (GameObject)PrefabUtility.InstantiatePrefab(bullet);
            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "Chumbo";
                var trail = root.transform.Find("Rastro");
                if (trail != null) trail.localScale = new Vector3(0.012f, 0.012f, 0.14f);
                var so = new SerializedObject(root.GetComponent<Projectile>());
                so.FindProperty("lifetime").floatValue = 0.22f;
                so.FindProperty("speed").floatValue = 70f;
                so.ApplyModifiedPropertiesWithoutUndo();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                var shot = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Athoria/Audio/Legacy/snd_tiro.wav");
                shotgun.EditorSetSpell(prefab, 0f, 70f, shot);
                shotgun.EditorSetSpread(8, 7f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
