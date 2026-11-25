using System.Collections.Generic;
using System.IO;
using Athoria.Gameplay.Bosses;
using Athoria.Gameplay.Combat;
using Athoria.Gameplay.Items;
using Athoria.Gameplay.Loot;
using Athoria.Gameplay.Stats;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Athoria.Editor
{
    /// <summary>
    /// Gera o chefe "Ancião do Brejo" (gigante de pedra e musgo): malhas de rocha procedurais
    /// com musgo por cima (estilo PS1), esqueleto de pivôs para o MossAncientAnimator,
    /// materiais, efeitos (poeira, pedras, nuvem venenosa, avisos no chão), dados
    /// (BossData + 4 BossAttackData) e o Prefab. Os números dos dados só são escritos
    /// na primeira geração: ajustes feitos à mão nos assets são mantidos.
    /// </summary>
    public static class BossContentFactory
    {
        private const string DataFolder = "Assets/_Athoria/Data/Chefes";
        private const string PrefabFolder = "Assets/_Athoria/Prefabs/Chefes";
        private const string ArtFolder = "Assets/_Athoria/Art/Chefes";
        private const string AudioFolder = "Assets/_Athoria/Audio/Chefes";
        private const string ContainerPath = ArtFolder + "/AnciaoDoBrejo_Gerado.asset";
        public const string MossAncientPath = PrefabFolder + "/Chefe_AnciaoDoBrejo.prefab";

        private static Object _container;
        private static Material _stone, _moss, _eye, _wood, _lantern;

        [MenuItem("Athoria/Dados/Gerar chefes (Ancião do Brejo)")]
        public static void GenerateMenu()
        {
            var items = ItemContentFactory.GenerateAll();
            GenerateAll(items, LootContentFactory.GenerateAll(items));
            Debug.Log("[Athoria] Chefe gerado em " + MossAncientPath + ". Use Reconstruir tudo (ou Sandbox) para colocá-lo na cena.");
        }

        public static GameObject GenerateAll(Dictionary<string, ItemData> items, Dictionary<string, LootTable> loot)
        {
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(ArtFolder);
            AssetDatabase.Refresh();

            NewContainer();
            BuildMaterials();
            var data = MossAncientData(items);
            var prefab = BuildMossAncient(data, loot != null && loot.TryGetValue(LootContentFactory.MossAncient, out var t) ? t : null);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        /// <summary>Coloca o chefe adormecido na cena (o objeto pai precisa estar antes do NavMesh ser gerado).</summary>
        public static GameObject Place(Transform parent, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MossAncientPath);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        // ============================================================== dados

        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{name}.wav");

        private static T Asset<T>(string path, out bool fresh) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            fresh = a == null;
            if (fresh)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
            }
            return a;
        }

        private static BossAttackData Attack(string id, string displayName, string description, System.Action<BossAttackData> fill,
            string windupSound, string impactSound)
        {
            var a = Asset<BossAttackData>($"{DataFolder}/AtaqueChefe_{id}.asset", out var fresh);
            if (fresh) { fill(a); a.EditorSetInfo(displayName, description); }
            a.windupSound = Clip(windupSound);
            a.impactSound = Clip(impactSound);
            EditorUtility.SetDirty(a);
            return a;
        }

        private static BossData MossAncientData(Dictionary<string, ItemData> items)
        {
            // Valores provisórios (balanceamento a definir).
            var slam = Attack("Anciao_SocoPesado", "Soco Pesado", "Ergue o braço devagar e esmaga o chão à frente.", a =>
                a.EditorConfigure(BossAttackKind.HeavySlam, 0f, 4.5f, 1f, 1.6f, 0.25f, 1.4f, 3f, 1.0f, 45f, 2.5f, 2.8f, 2.6f),
                "chefe_rosnado", "chefe_impacto");
            var jump = Attack("Anciao_Salto", "Salto Esmagador", "Agacha, mira onde a jogadora está e cai em cima da área.", a =>
                a.EditorConfigure(BossAttackKind.JumpAttack, 7f, 18f, 1f, 1.2f, 1.0f, 1.8f, 9f, 1.2f, 55f, 3f, 3.5f, 0f),
                "chefe_pedras", "chefe_impacto");
            var belch = Attack("Anciao_ArrotoVenenoso", "Arroto Venenoso", "Puxa o ar e solta uma nuvem tóxica que fica no lugar.", a =>
            {
                a.EditorConfigure(BossAttackKind.PoisonBelch, 3f, 9f, 0.8f, 1.4f, 0.8f, 1.2f, 12f, 1.0f, 0f, 0f, 3.2f, 0f);
                a.damageType = DamageType.Poison;
                a.cloudDuration = 7f;
                a.poisonDps = 8f;
                a.poisonTick = 0.5f;
            }, "chefe_inspirar", "chefe_arroto");
            var quick = Attack("Anciao_GolpeRapido", "Golpe Rápido", "Varrida curta e forte para quem fica colado nele.", a =>
            {
                a.EditorConfigure(BossAttackKind.QuickStrike, 0f, 3.2f, 1f, 0.35f, 0.3f, 0.9f, 4f, 0f, 38f, 2f, 1f, 3f);
                a.arc = 110f;
            }, "chefe_rosnado", "chefe_golpe");

            var d = Asset<BossData>($"{DataFolder}/Chefe_AnciaoDoBrejo.asset", out var fresh);
            if (fresh)
            {
                d.EditorSetInfo("Ancião do Brejo", "Gigante de pedra e musgo que dorme disfarçado de rocha. Desperta com o cheiro de musgo fresco.");
                d.maxHealth = 1500f; d.defense = 6f; d.damageResistance = 0.25f; d.minDamageFraction = 0.15f;
                d.staggerThreshold = 120f; d.staggerDuration = 1.4f; d.staggerImmunity = 8f;
                d.walkSpeed = 1.6f; d.turnSpeed = 70f; d.thinkTime = 0.6f; d.closePressureTime = 1.2f; d.closeRange = 3.2f;
                d.wakeItemAmount = 40; d.activationRadius = 6f; d.awakenDuration = 5.2f;
                d.experience = 600;
            }
            d.wakeItem = items != null && items.TryGetValue("Musgo", out var musgo) ? musgo : d.wakeItem;
            d.heavySlam = slam; d.jumpAttack = jump; d.poisonBelch = belch; d.quickStrike = quick;
            d.roarSound = Clip("chefe_grito");
            d.rocksSound = Clip("chefe_pedras");
            d.stepSound = Clip("chefe_passo");
            d.hurtSound = Clip("chefe_rosnado");
            d.deathSound = Clip("chefe_grito");
            EditorUtility.SetDirty(d);
            return d;
        }

        // ============================================================== Prefab

        private static GameObject BuildMossAncient(BossData data, LootTable loot)
        {
            var root = new GameObject("Ancião do Brejo");
            try
            {
                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);

                // ---- esqueleto de pivôs (todos com rotação zero; membros pendem em -Y, frente = +Z)
                var hips = Pivot(visual, "Quadril", new Vector3(0f, 1.95f, 0f));
                Rock(hips, "Bacia", new Vector3(0f, -0.05f, 0f), new Vector3(0.95f, 0.6f, 0.75f), 2, 0.5f, 11);

                var torso = Pivot(hips, "Tronco", new Vector3(0f, 0.15f, 0f));
                var chest = Rock(torso, "Peito", new Vector3(0f, 1.05f, 0.05f), new Vector3(1.3f, 1.1f, 1.0f), 2, 0.95f, 12).transform;
                Rock(torso, "Corcova", new Vector3(0f, 1.65f, -0.6f), new Vector3(0.95f, 0.6f, 0.55f), 2, 1f, 13);
                Rock(torso, "Pedra das Costas", new Vector3(0.55f, 0.75f, -0.75f), new Vector3(0.45f, 0.4f, 0.35f), 1, 0.9f, 14);

                var head = Pivot(torso, "Cabeca", new Vector3(0f, 1.85f, 0.6f));
                Rock(head, "Cranio", new Vector3(0f, 0.22f, 0.26f), new Vector3(0.5f, 0.42f, 0.46f), 2, 0.85f, 21);
                Rock(head, "Sobrancelha", new Vector3(0f, 0.46f, 0.58f), new Vector3(0.56f, 0.14f, 0.2f), 1, 1f, 22);
                var eyes = new List<Renderer>();
                foreach (var side in new[] { -1f, 1f })
                {
                    var eye = new GameObject(side < 0 ? "Olho E" : "Olho D");
                    eye.transform.SetParent(head, false);
                    eye.transform.localPosition = new Vector3(0.18f * side, 0.33f, 0.71f);
                    eye.AddComponent<MeshFilter>().sharedMesh = RockMesh($"Olho{(side < 0 ? "E" : "D")}", new Vector3(0.1f, 0.06f, 0.05f), 1, 0f, 0f, 31);
                    var r = eye.AddComponent<MeshRenderer>();
                    r.sharedMaterials = new[] { _eye, _eye };
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    eyes.Add(r);
                }
                var jaw = Pivot(head, "Mandibula", new Vector3(0f, 0.04f, 0.15f));
                Rock(jaw, "Queixo", new Vector3(0f, -0.1f, 0.3f), new Vector3(0.42f, 0.15f, 0.36f), 1, 0.2f, 23);
                var mouth = Pivot(jaw, "Boca", new Vector3(0f, 0.02f, 0.62f));

                var shoulderR = Arm(torso, true, out var elbowR, out var wristR, out var fingersR);
                var shoulderL = Arm(torso, false, out var elbowL, out var wristL, out var fingersL);
                var thighR = Leg(hips, true, out var shinR, out var footR);
                var thighL = Leg(hips, false, out var shinL, out var footL);

                // Galho com a lanterna velha pendurada no ombro esquerdo
                var branch = Pivot(shoulderL, "Galho", new Vector3(-0.15f, 0.45f, -0.25f));
                branch.localRotation = Quaternion.Euler(-30f, 0f, 20f);
                Part(branch, "Madeira", RockMesh("Galho", new Vector3(0.07f, 0.6f, 0.07f), 1, 0.25f, 0f, 41), new Vector3(0f, 0.55f, 0f), _wood);
                var lanternPivot = Pivot(branch, "Lanterna", new Vector3(0f, 1.1f, 0f));
                lanternPivot.rotation = Quaternion.identity;
                Part(lanternPivot, "Corpo da Lanterna", RockMesh("Lanterna", new Vector3(0.13f, 0.17f, 0.13f), 0, 0.05f, 0f, 42), new Vector3(0f, -0.28f, 0f), _lantern);
                var light = new GameObject("Luz da Lanterna").AddComponent<Light>();
                light.transform.SetParent(lanternPivot, false);
                light.transform.localPosition = new Vector3(0f, -0.28f, 0f);
                light.type = LightType.Point; light.color = new Color(1f, 0.72f, 0.35f); light.range = 6f; light.intensity = 1.4f;
                light.shadows = LightShadows.None;

                // ---- animação procedural
                var anim = visual.gameObject.AddComponent<MossAncientAnimator>();
                var aso = new SerializedObject(anim);
                var rig = aso.FindProperty("rig");
                void R(string n, Object o) => rig.FindPropertyRelative(n).objectReferenceValue = o;
                R("hips", hips); R("torso", torso); R("chest", chest); R("head", head); R("jaw", jaw);
                R("shoulderR", shoulderR); R("elbowR", elbowR); R("wristR", wristR);
                R("shoulderL", shoulderL); R("elbowL", elbowL); R("wristL", wristL);
                R("thighR", thighR); R("shinR", shinR); R("footR", footR);
                R("thighL", thighL); R("shinL", shinL); R("footL", footL);
                Array(rig.FindPropertyRelative("fingersR"), fingersR);
                Array(rig.FindPropertyRelative("fingersL"), fingersL);
                Array(rig.FindPropertyRelative("eyes"), eyes.ToArray());
                aso.ApplyModifiedPropertiesWithoutUndo();

                // ---- física, navegação e sistemas
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 1.5f; agent.height = 4f; agent.speed = data.walkSpeed; agent.angularSpeed = data.turnSpeed;
                agent.acceleration = 6f; agent.stoppingDistance = 0.5f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.enabled = false;
                root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

                var body = root.AddComponent<CapsuleCollider>();
                body.center = new Vector3(0f, 2.1f, 0.15f); body.radius = 1.35f; body.height = 4.3f;
                var arms = root.AddComponent<BoxCollider>();       // ombros largos
                arms.center = new Vector3(0f, 3.4f, 0.1f); arms.size = new Vector3(3.9f, 1.3f, 1.6f);

                var health = root.AddComponent<Health>();
                var hso = new SerializedObject(health);
                hso.FindProperty("maxHealth").floatValue = data.maxHealth;
                hso.ApplyModifiedPropertiesWithoutUndo();

                var surf = root.AddComponent<SurfaceMaterial>();
                var sso = new SerializedObject(surf);
                sso.FindProperty("kind").enumValueIndex = (int)SurfaceKind.Stone;
                sso.ApplyModifiedPropertiesWithoutUndo();

                var reward = root.AddComponent<ExperienceReward>();
                var rso = new SerializedObject(reward);
                rso.FindProperty("experience").intValue = data.experience;
                rso.FindProperty("displayName").stringValue = data.DisplayName;
                rso.ApplyModifiedPropertiesWithoutUndo();

                if (loot != null)
                {
                    var dropper = root.AddComponent<LootDropper>();
                    var lso = new SerializedObject(dropper);
                    lso.FindProperty("table").objectReferenceValue = loot;
                    lso.ApplyModifiedPropertiesWithoutUndo();
                }

                // ---- efeitos
                var fx = root.AddComponent<BossEffects>();
                var mossFall = MossFall(chest);
                var fso = new SerializedObject(fx);
                fso.FindProperty("dustPrefab").objectReferenceValue = DustPrefab();
                fso.FindProperty("debrisPrefab").objectReferenceValue = DebrisPrefab();
                fso.FindProperty("mossFall").objectReferenceValue = mossFall;
                fso.FindProperty("warningMaterial").objectReferenceValue = ParticleMat("Aviso", Disc());
                fso.FindProperty("shockwaveMaterial").objectReferenceValue = ParticleMat("Onda", Ring());
                fso.FindProperty("poisonCloudPrefab").objectReferenceValue = PoisonCloudPrefab();
                fso.ApplyModifiedPropertiesWithoutUndo();

                var cinematic = root.AddComponent<BossEncounterCinematic>();
                var cso = new SerializedObject(cinematic);
                cso.FindProperty("focus").objectReferenceValue = head;
                cso.ApplyModifiedPropertiesWithoutUndo();

                var brain = root.AddComponent<MossAncientBrain>();
                var bso = new SerializedObject(brain);
                bso.FindProperty("data").objectReferenceValue = data;
                bso.FindProperty("anim").objectReferenceValue = anim;
                bso.FindProperty("fx").objectReferenceValue = fx;
                bso.FindProperty("cinematic").objectReferenceValue = cinematic;
                bso.FindProperty("mouth").objectReferenceValue = mouth;
                bso.ApplyModifiedPropertiesWithoutUndo();

                root.AddComponent<MossAwakener>();

                return PrefabUtility.SaveAsPrefabAsset(root, MossAncientPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Transform Arm(Transform torso, bool right, out Transform elbow, out Transform wrist, out Transform[] fingers)
        {
            var s = right ? 1f : -1f;
            var tag = right ? "D" : "E";
            var shoulder = Pivot(torso, "Ombro " + tag, new Vector3(1.5f * s, 1.6f, 0.05f));
            Rock(shoulder, "Pedra do Ombro " + tag, new Vector3(0f, 0.08f, 0f), new Vector3(0.68f, 0.55f, 0.62f), 2, 1f, right ? 51 : 61);
            Rock(shoulder, "Braco " + tag, new Vector3(0f, -0.55f, 0f), new Vector3(0.4f, 0.6f, 0.42f), 1, 0.4f, right ? 52 : 62);
            elbow = Pivot(shoulder, "Cotovelo " + tag, new Vector3(0f, -1.1f, 0f));
            Rock(elbow, "Antebraco " + tag, new Vector3(0f, -0.62f, 0.03f), new Vector3(0.52f, 0.72f, 0.5f), 2, 0.55f, right ? 53 : 63);
            wrist = Pivot(elbow, "Pulso " + tag, new Vector3(0f, -1.3f, 0.03f));
            Rock(wrist, "Punho " + tag, new Vector3(0f, -0.28f, 0.04f), new Vector3(0.48f, 0.36f, 0.44f), 1, 0.25f, right ? 54 : 64);
            fingers = new Transform[4];
            for (var i = 0; i < 4; i++)
            {
                var f = Pivot(wrist, $"Dedo {tag}{i + 1}", new Vector3((-0.27f + 0.18f * i) * s, -0.5f, 0.22f));
                Rock(f, "Falange", new Vector3(0f, -0.17f, 0f), new Vector3(0.1f, 0.2f, 0.11f), 1, 0f, (right ? 70 : 80) + i);
                fingers[i] = f;
            }
            return shoulder;
        }

        private static Transform Leg(Transform hips, bool right, out Transform shin, out Transform foot)
        {
            var s = right ? 1f : -1f;
            var tag = right ? "D" : "E";
            var thigh = Pivot(hips, "Coxa " + tag, new Vector3(0.6f * s, -0.15f, 0f));
            Rock(thigh, "Pedra da Coxa " + tag, new Vector3(0f, -0.42f, 0f), new Vector3(0.46f, 0.55f, 0.48f), 2, 0.35f, right ? 91 : 95);
            shin = Pivot(thigh, "Joelho " + tag, new Vector3(0f, -0.85f, 0f));
            Rock(shin, "Canela " + tag, new Vector3(0f, -0.38f, 0f), new Vector3(0.42f, 0.5f, 0.42f), 1, 0.2f, right ? 92 : 96);
            foot = Pivot(shin, "Tornozelo " + tag, new Vector3(0f, -0.78f, 0f));
            Rock(foot, "Pe " + tag, new Vector3(0f, -0.08f, 0.18f), new Vector3(0.48f, 0.18f, 0.62f), 1, 0.15f, right ? 93 : 97);
            return thigh;
        }

        private static Transform Pivot(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        private static GameObject Rock(Transform parent, string name, Vector3 localPos, Vector3 radii, int subdiv, float moss, int seed) =>
            Part(parent, name, RockMesh(name, radii, subdiv, 0.22f, moss, seed), localPos, null);

        private static GameObject Part(Transform parent, string name, Mesh mesh, Vector3 localPos, Material single)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = single != null ? new[] { single, single } : new[] { _stone, _moss };
            return go;
        }

        private static void Array(SerializedProperty prop, Object[] values)
        {
            prop.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ============================================================== malhas de rocha

        /// <summary>
        /// Icosfera deformada por ruído, com faces chapadas (PS1). Submalha 0 = pedra,
        /// 1 = musgo: faces viradas para cima (com ruído nas bordas) ficam verdes.
        /// </summary>
        private static Mesh RockMesh(string name, Vector3 radii, int subdiv, float rough, float moss, int seed)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Icosphere(subdiv, verts, tris);
            for (var i = 0; i < verts.Count; i++)
            {
                var d = verts[i];
                var n = (0.65f * Noise(d * 1.7f, seed) + 0.35f * Noise(d * 3.9f, seed + 3)) * 2f - 1f;
                verts[i] = Vector3.Scale(d * (1f + rough * n), radii);
            }

            var pos = new List<Vector3>();
            var nor = new List<Vector3>();
            var uv = new List<Vector2>();
            var stone = new List<int>();
            var green = new List<int>();
            for (var i = 0; i < tris.Count; i += 3)
            {
                var a = verts[tris[i]]; var b = verts[tris[i + 1]]; var c = verts[tris[i + 2]];
                var normal = Vector3.Cross(b - a, c - a).normalized;
                var center = (a + b + c) / 3f;
                if (Vector3.Dot(normal, center) < 0f) { (b, c) = (c, b); normal = -normal; }
                var isMoss = moss > 0f && normal.y + (Noise(center * 2.3f, seed + 7) - 0.5f) * 0.9f > 1f - moss * 0.6f;
                var list = isMoss ? green : stone;
                foreach (var p in new[] { a, b, c })
                {
                    list.Add(pos.Count);
                    pos.Add(p);
                    nor.Add(normal);
                    var an = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                    uv.Add((an.y >= an.x && an.y >= an.z ? new Vector2(p.x, p.z) : an.x >= an.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y)) * 0.8f);
                }
            }

            var mesh = new Mesh { name = "Rocha_" + name };
            mesh.SetVertices(pos);
            mesh.SetNormals(nor);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(stone, 0);
            mesh.SetTriangles(green, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            Add(mesh);
            return mesh;
        }

        private static void Icosphere(int subdiv, List<Vector3> verts, List<int> tris)
        {
            var t = (1f + Mathf.Sqrt(5f)) / 2f;
            foreach (var v in new[]
                     {
                         new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                         new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                         new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
                     })
                verts.Add(v.normalized);
            tris.AddRange(new[]
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            });
            for (var s = 0; s < subdiv; s++)
            {
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out var idx)) return idx;
                    verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
                    cache[key] = verts.Count - 1;
                    return verts.Count - 1;
                }
                var next = new List<int>();
                for (var i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris.Clear();
                tris.AddRange(next);
            }
        }

        private static float Hash(int x, int y, int z, int s)
        {
            unchecked
            {
                var h = x * 374761393 + y * 668265263 + z * 1274126177 + s * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>Ruído de valor 3D em [0,1].</summary>
        private static float Noise(Vector3 p, int s)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float fx = p.x - x, fy = p.y - y, fz = p.z - z;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
            float L(float a, float b, float t) => a + (b - a) * t;
            return L(
                L(L(Hash(x, y, z, s), Hash(x + 1, y, z, s), fx), L(Hash(x, y + 1, z, s), Hash(x + 1, y + 1, z, s), fx), fy),
                L(L(Hash(x, y, z + 1, s), Hash(x + 1, y, z + 1, s), fx), L(Hash(x, y + 1, z + 1, s), Hash(x + 1, y + 1, z + 1, s), fx), fy),
                fz);
        }

        // ============================================================== materiais e texturas

        private static void NewContainer()
        {
            AssetDatabase.DeleteAsset(ContainerPath);
            var holder = new Mesh { name = "AnciaoDoBrejo_Gerado" };
            AssetDatabase.CreateAsset(holder, ContainerPath);
            _container = holder;
        }

        private static void Add(Object o) => AssetDatabase.AddObjectToAsset(o, _container);

        private static void BuildMaterials()
        {
            var white = Tex("Branco", 4, 4, (x, y, r) => Color.white);
            var stoneTex = Tex("Pedra", 32, 32, (x, y, r) =>
            {
                var v = Noise(new Vector3(x * 0.35f, y * 0.35f, 0f), 5) * 0.6f + (float)r.NextDouble() * 0.4f;
                var c = Color.Lerp(new Color(0.26f, 0.25f, 0.22f), new Color(0.47f, 0.45f, 0.4f), v);
                if (r.NextDouble() < 0.04) c *= 0.55f;                                     // rachaduras
                return c;
            });
            var mossTex = Tex("Musgo", 32, 32, (x, y, r) =>
            {
                var v = Noise(new Vector3(x * 0.5f, y * 0.5f, 3f), 9) * 0.5f + (float)r.NextDouble() * 0.5f;
                var c = Color.Lerp(new Color(0.16f, 0.27f, 0.07f), new Color(0.4f, 0.55f, 0.16f), v);
                if (r.NextDouble() < 0.03) c = new Color(0.55f, 0.6f, 0.25f);               // folhinhas claras
                return c;
            });
            _stone = Psx("Pedra", stoneTex, Color.white, new Color(0.05f, 0.05f, 0.045f), white);
            _moss = Psx("Musgo", mossTex, Color.white, new Color(0.03f, 0.06f, 0.015f), white);
            _wood = Psx("Madeira", stoneTex, new Color(0.55f, 0.38f, 0.22f), Color.black, white);
            _eye = Psx("Olhos", white, new Color(1f, 0.8f, 0.4f), new Color(2.6f, 1.6f, 0.45f), white);
            _lantern = Psx("Lanterna", white, new Color(1f, 0.85f, 0.55f), new Color(2.2f, 1.4f, 0.5f), white);
        }

        private static Texture2D Tex(string name, int w, int h, System.Func<int, int, System.Random, Color> pixel)
        {
            var rng = new System.Random(name.GetHashCode());
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_" + name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var px = new Color[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                px[y * w + x] = pixel(x, y, rng);
            t.SetPixels(px);
            t.Apply(false, false);
            Add(t);
            return t;
        }

        private static Material Psx(string name, Texture2D tex, Color tint, Color emission, Texture2D emissionMap)
        {
            var m = new Material(PsxAssets.PsxShader) { name = "Anciao_" + name };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.SetTexture("_EmissionMap", emission.maxColorComponent > 0f && emissionMap != null ? (emission.r > 1f ? emissionMap : tex) : null);
            m.SetColor("_EmissionColor", emission);
            if (emission.maxColorComponent > 0f) m.EnableKeyword("_EMISSION"); else m.DisableKeyword("_EMISSION");
            Add(m);
            return m;
        }

        private static Material ParticleMat(string name, Texture2D tex)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Anciao_FX_" + name };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            Add(m);
            return m;
        }

        private static Texture2D Radial(string name, int size, System.Func<float, float> alpha)
        {
            var t = Tex(name, size, size, (x, y, r) =>
            {
                var dx = (x + 0.5f) / size * 2f - 1f;
                var dy = (y + 0.5f) / size * 2f - 1f;
                return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(Mathf.Sqrt(dx * dx + dy * dy))));
            });
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // Aviso: disco translúcido com borda forte e um anel interno
        private static Texture2D Disc() => Radial("Aviso", 64, d =>
            d > 1f ? 0f : 0.3f + 0.7f * Mathf.Clamp01((d - 0.86f) / 0.1f) + (Mathf.Abs(d - 0.55f) < 0.04f ? 0.35f : 0f));

        // Onda de choque: anel
        private static Texture2D Ring() => Radial("Onda", 64, d => d > 1f ? 0f : Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) / 0.13f));

        // ============================================================== partículas

        private static ParticleSystem NewParticles(string name, Transform parent, Material mat, bool loop)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = loop;
            main.playOnAwake = !loop || parent == null;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private static void Fade(ParticleSystem ps, float fadeIn = 0f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                fadeIn > 0f
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private static ParticleSystem SavePrefab(ParticleSystem ps, string file)
        {
            try { return PrefabUtility.SaveAsPrefabAsset(ps.gameObject, $"{PrefabFolder}/{file}.prefab").GetComponent<ParticleSystem>(); }
            finally { Object.DestroyImmediate(ps.gameObject); }
        }

        private static ParticleSystem DustPrefab()
        {
            var ps = NewParticles("FX Poeira do Chefe", null, ParticleMat("Poeira", Radial("Fumaca", 16, d => (1f - d) * 1.4f)), false);
            var main = ps.main;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.38f, 0.26f, 0.7f), new Color(0.32f, 0.3f, 0.22f, 0.6f));
            main.gravityModifier = -0.03f;
            main.maxParticles = 60;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.limitVelocityOverLifetime;
            vel.enabled = true; vel.limit = 0.6f; vel.dampen = 0.15f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.7f));
            Fade(ps);
            return SavePrefab(ps, "FX_PoeiraChefe");
        }

        private static ParticleSystem DebrisPrefab()
        {
            var ps = NewParticles("FX Pedras do Chefe", null, ParticleMat("Pedras", Tex("Solido", 4, 4, (x, y, r) => Color.white)), false);
            var main = ps.main;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.29f, 0.26f), new Color(0.25f, 0.33f, 0.12f));
            main.gravityModifier = 2.2f;
            main.maxParticles = 40;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.8f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            return SavePrefab(ps, "FX_PedrasChefe");
        }

        private static ParticleSystem MossFall(Transform chest)
        {
            var ps = NewParticles("Musgo Caindo", chest, ParticleMat("MusgoCaindo", Tex("SolidoMusgo", 4, 4, (x, y, r) => Color.white)), true);
            ps.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var main = ps.main;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.45f, 0.12f), new Color(0.38f, 0.3f, 0.18f));
            main.gravityModifier = 0.9f;
            main.maxParticles = 200;
            var em = ps.emission;
            em.rateOverTime = 70f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.4f, 0.6f, 1.8f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            return ps;
        }

        private static PoisonCloud PoisonCloudPrefab()
        {
            var root = new GameObject("Nuvem Venenosa");
            try
            {
                var cloud = root.AddComponent<PoisonCloud>();
                var ps = NewParticles("Nuvem", root.transform, ParticleMat("Veneno", Radial("Nevoa", 16, d => (1f - d) * 1.2f)), true);
                var main = ps.main;
                main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.62f, 0.12f, 0.45f), new Color(0.3f, 0.48f, 0.1f, 0.35f));
                main.gravityModifier = -0.02f;
                main.maxParticles = 160;
                var em = ps.emission;
                em.rateOverTime = 32f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 2.6f;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                var rot = ps.rotationOverLifetime;
                rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.25f));
                Fade(ps, 0.25f);
                var so = new SerializedObject(cloud);
                so.FindProperty("particles").objectReferenceValue = ps;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/FX_NuvemVenenosa.prefab").GetComponent<PoisonCloud>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
