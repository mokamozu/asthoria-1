using Athoria.Gameplay.World;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Editor
{
    /// <summary>
    /// Transforma modelos dos pacotes PS1 (ThirdParty) em Prefabs do jogo:
    /// acha a textura de cada material pelo nome, cria um material URP Simple
    /// Lit sem brilho (com recorte de transparência para folhagem), ajusta a
    /// altura real e põe colisor e LODGroup (necessário para árvores do Terrain).
    /// </summary>
    internal static class PsxAssets
    {
        public const string Root = ThirdPartyImportSettings.Root;
        private const string MaterialFolder = "Assets/_Athoria/Materials/PSX";
        private const string PrefabFolder = "Assets/_Athoria/Prefabs/PSX";

        private static readonly Dictionary<string, Material> Cache = new();

        /// <summary>Shader de PS1 do jogo (tremor de vértices, textura afim, luz por vértice, névoa).</summary>
        public static Shader PsxShader => Shader.Find("Athoria/PSX Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");

        public static Texture2D Texture(string relativePath) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{relativePath}");

        /// <summary>Material PS1 (URP Simple Lit, sem brilho especular). Recorte alfa para folhas.</summary>
        public static Material MaterialFor(Texture2D texture, bool alphaClip, Color? tint = null, string name = null, bool doubleSided = false)
        {
            name ??= texture != null ? texture.name : "SemTextura";
            var key = $"{name}_{alphaClip}_{doubleSided}_{(tint ?? Color.white)}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            Directory.CreateDirectory(MaterialFolder);
            var path = $"{MaterialFolder}/PSX_{Sanitize(name)}{(alphaClip ? "_Recorte" : "")}{(doubleSided && !alphaClip ? "_2Lados" : "")}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = PsxShader;
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            mat.SetColor("_EmissionColor", Color.black);
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", tint ?? Color.white);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.DisableKeyword("_SPECULAR_COLOR");
            mat.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            mat.SetFloat("_Cutoff", 0.45f);
            if (alphaClip) mat.EnableKeyword("_ALPHATEST_ON");
            else mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", alphaClip || doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            mat.renderQueue = alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(mat);
            Cache[key] = mat;
            return mat;
        }

        /// <summary>
        /// Cria (ou recria) um Prefab a partir de um FBX dos pacotes.
        /// targetHeight &gt; 0 ajusta a escala para essa altura em metros.
        /// textureOverride: usa esta textura em todos os materiais (pacotes de uma textura por modelo).
        /// </summary>
        public static GameObject Wrap(string fbxRelative, string prefabName, string subFolder, float targetHeight,
            float colliderRadius = 0f, bool lodGroup = false, Texture2D textureOverride = null, Color? tint = null,
            bool boxCollider = false, float randomYaw = 0f, Texture2D fallbackTexture = null, bool doubleSided = false)
        {
            var fbxPath = $"{Root}/{fbxRelative}";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                Debug.LogWarning("[Athoria] Modelo não encontrado (o Unity já importou a pasta ThirdParty?): " + fbxPath);
                return null;
            }

            var folder = $"{PrefabFolder}/{subFolder}";
            Directory.CreateDirectory(folder);
            var root = new GameObject(prefabName);
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                visual.name = "Modelo";
                visual.transform.SetParent(root.transform, false);
                foreach (var c in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

                // Materiais PS1 com a textura certa
                var texFolder = Path.GetDirectoryName(fbxPath)?.Replace('\\', '/');
                var packFolder = $"{Root}/{fbxRelative.Split('/')[0]}";
                foreach (var r in visual.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (var i = 0; i < mats.Length; i++)
                    {
                        var tex = textureOverride != null ? textureOverride : FindTexture(packFolder, texFolder, mats[i], model.name);
                        if (tex == null) tex = fallbackTexture;
                        var clip = tex != null && HasAlpha(tex);
                        mats[i] = MaterialFor(tex, clip, tint, tex != null ? tex.name : model.name, doubleSided);
                    }
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.On;
                }

                // Altura real e base no chão
                var bounds = Bounds(visual);
                if (targetHeight > 0f && bounds.size.y > 0.001f)
                {
                    var s = targetHeight / bounds.size.y;
                    visual.transform.localScale *= s;
                    bounds = Bounds(visual);
                }
                visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                bounds = Bounds(visual);

                if (colliderRadius > 0f)
                {
                    var col = root.AddComponent<CapsuleCollider>();
                    col.radius = colliderRadius;
                    col.height = Mathf.Max(colliderRadius * 2f, bounds.size.y);
                    col.center = new Vector3(0f, col.height * 0.5f, 0f);
                }
                else if (boxCollider)
                {
                    var box = root.AddComponent<BoxCollider>();
                    box.center = bounds.center;
                    box.size = bounds.size;
                }

                if (lodGroup)
                {
                    var lod = root.AddComponent<LODGroup>();
                    lod.SetLODs(new[] { new LOD(0.01f, root.GetComponentsInChildren<Renderer>()) });
                    lod.RecalculateBounds();
                }
                foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
                return PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{prefabName}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Material PS1 a partir de uma textura dos pacotes; sem a textura, devolve o material antigo.</summary>
        public static Material Textured(string textureRelative, string name, Color tint, Material fallback)
        {
            var tex = Texture(textureRelative);
            return tex != null ? MaterialFor(tex, false, tint, name) : fallback;
        }

        private static readonly Dictionary<string, GameObject> KitCache = new();

        /// <summary>
        /// Peça de kit modular (parede, piso, pilar, adereço) como Prefab, criada uma vez por reconstrução.
        /// height 0 = tamanho original do modelo (as peças estruturais são ajustadas depois por KitDresser).
        /// </summary>
        public static GameObject Kit(string fbxRelative, float height = 0f, bool collider = false, Color? tint = null)
        {
            var key = $"{fbxRelative}|{height}|{collider}|{tint}";
            if (KitCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var name = Path.GetFileNameWithoutExtension(fbxRelative);
            var pack = fbxRelative.Split('/')[0];
            var lower = name.ToLowerInvariant();
            Texture2D fallback = null;
            if (pack == "Dungeon")
            {
                const string T = "Dungeon/PSX_Dungeon/Textures/";
                fallback = lower.StartsWith("door") ? Texture(T + "Doors_Map.png")
                    : lower is "barrel" or "box" or "chest" ? Texture(T + "CratesAndBarrels_Map.png")
                    : Texture(T + "Dungeon_Map.png");
            }
            else if (pack == "ModularMedieval") fallback = Texture("ModularMedieval/Textures/bauerhaus.png");
            var prefab = Wrap(fbxRelative, $"{pack}_{name}{(height > 0f ? "_" + height.ToString("0.##").Replace(',', '_').Replace('.', '_') : "")}",
                "Kit/" + pack, height, boxCollider: collider, fallbackTexture: fallback, doubleSided: true, tint: tint);
            KitCache[key] = prefab;
            return prefab;
        }

        /// <summary>Instancia um adereço do kit (base no chão) na posição do mundo.</summary>
        public static GameObject Place(GameObject prefab, Transform parent, Vector3 worldPos, float yaw, float scale = 1f)
        {
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        // ================= Armas e braços em primeira pessoa =================

        /// <summary>
        /// Monta o modelo PS1 de uma arma dentro de root no padrão das armas do jogo:
        /// empunhadura na origem, lâmina/haste apontando para +Y e o lado largo em X.
        /// Escudos: face para +Z, altura em Y, alça (origem) atrás do escudo.
        /// grip = fração do comprimento, a partir da base, onde a mão segura.
        /// </summary>
        public static bool BuildWeaponModel(Transform root, string fbxRelative, string textureRelative, float length, float grip,
            bool shield = false, bool flip = false, bool gun = false)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{fbxRelative}");
            if (model == null) return false;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Modelo";
            inst.transform.SetParent(root, false);
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            foreach (var a in inst.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(a);

            var tex = Texture(textureRelative);
            if (tex == null) Debug.LogWarning("[Athoria] Textura da arma não encontrada: " + textureRelative);
            Crisp(tex);
            var vmMat = ViewModelMaterial(tex, tex != null ? tex.name : model.name);
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++) mats[i] = vmMat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            void Turn(Vector3 euler) => inst.transform.localRotation = Quaternion.Euler(euler) * inst.transform.localRotation;
            var b = LocalBounds(root, inst);
            if (gun)
            {
                // comprimento em Y, altura da arma (coronha ↔ cano) em Z
                if (b.size.x > b.size.y && b.size.x >= b.size.z) Turn(new Vector3(0, 0, 90));
                else if (b.size.z > b.size.y && b.size.z > b.size.x) Turn(new Vector3(90, 0, 0));
                b = LocalBounds(root, inst);
                if (b.size.x > b.size.z) { Turn(new Vector3(0, 90, 0)); b = LocalBounds(root, inst); }
                // a coronha (ponta mais alta) fica embaixo; o cano aponta para +Y
                var pts = RootPoints(root, inst);
                float Extent(System.Func<Vector3, bool> where)
                {
                    var sel = pts.Where(where).ToList();
                    return sel.Count == 0 ? 0f : sel.Max(p => p.z) - sel.Min(p => p.z);
                }
                var low = b.min.y + b.size.y * 0.2f;
                var high = b.max.y - b.size.y * 0.2f;
                if (Extent(p => p.y > high) > Extent(p => p.y < low)) { Turn(new Vector3(0, 0, 180)); b = LocalBounds(root, inst); pts = RootPoints(root, inst); }
                // o cano fica no alto da arma: lado -Z (no padrão das armas do jogo, -Z é "em cima")
                high = b.max.y - b.size.y * 0.3f;
                var barrel = pts.Where(p => p.y > high).ToList();
                if (barrel.Count > 0 && barrel.Average(p => p.z) > b.center.z) { Turn(new Vector3(0, 180, 0)); b = LocalBounds(root, inst); }
                if (flip) { Turn(new Vector3(0, 0, 180)); b = LocalBounds(root, inst); }
            }
            else if (!shield)
            {
                // comprimento em Y
                if (b.size.x > b.size.y && b.size.x >= b.size.z) Turn(new Vector3(0, 0, 90));
                else if (b.size.z > b.size.y && b.size.z > b.size.x) Turn(new Vector3(90, 0, 0));
                b = LocalBounds(root, inst);
                // lado largo (fio da lâmina) em X
                if (b.size.z > b.size.x) { Turn(new Vector3(0, 90, 0)); b = LocalBounds(root, inst); }
                // flip: vira o fio da lâmina para o outro lado (lâminas curvas, como a katana)
                if (flip) { Turn(new Vector3(0, 180, 0)); b = LocalBounds(root, inst); }
            }
            else
            {
                // espessura em Z, altura em Y
                if (b.size.x < b.size.y && b.size.x < b.size.z) Turn(new Vector3(0, 90, 0));
                else if (b.size.y < b.size.x && b.size.y < b.size.z) Turn(new Vector3(90, 0, 0));
                b = LocalBounds(root, inst);
                if (b.size.x > b.size.y) { Turn(new Vector3(0, 0, 90)); b = LocalBounds(root, inst); }
                if (flip) { Turn(new Vector3(0, 180, 0)); b = LocalBounds(root, inst); }
            }

            var scale = length / Mathf.Max(0.001f, b.size.y);
            inst.transform.localScale *= scale;
            inst.transform.localPosition *= scale;
            b = LocalBounds(root, inst);
            var pivot = shield
                ? new Vector3(b.center.x, b.center.y, b.min.z)
                : new Vector3(b.center.x, b.min.y + b.size.y * grip, b.center.z);
            inst.transform.localPosition -= pivot;
            return true;
        }

        /// <summary>
        /// Braço em primeira pessoa com o modelo de mãos PS1 (esqueleto Rigify, os dois braços na mesma malha).
        /// Esconde o braço do outro lado, alinha o antebraço ao eixo +Z do braço do jogo, põe a palma
        /// no soquete da mão e fecha os dedos em volta da empunhadura.
        /// </summary>
        public static SkinnedMeshRenderer BuildArmModel(Transform arm, bool right, Vector3 socketLocal, float fingerCurl = 75f)
        {
            const string ModelPath = "FirstPersonArms/FPS_Psx_Hands/FPS_Psx_Hands.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{ModelPath}");
            if (model == null) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Modelo Mao";
            inst.transform.SetParent(arm, false);
            foreach (var a in inst.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(a);

            var side = right ? ".R" : ".L";
            var other = right ? ".L" : ".R";
            Transform Bone(string n) => inst.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
            var forearm = Bone("forearm" + side);
            var hand = Bone("hand" + side);
            var thumb = Bone("thumb.01" + side);
            var otherArm = Bone("upper_arm" + other);
            var skin = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            if (forearm == null || hand == null || skin == null)
            {
                Debug.LogWarning("[Athoria] Modelo de mãos sem os ossos esperados; usando o braço provisório.");
                Object.DestroyImmediate(inst);
                return null;
            }
            if (otherArm != null) otherArm.localScale = Vector3.one * 0.0001f;

            Vector3 L(Transform t) => arm.InverseTransformPoint(t.position);

            // 1) direção do antebraço -> +Z e tamanho (antebraço com ~27 cm)
            var dir = L(hand) - L(forearm);
            var turn = Quaternion.FromToRotation(dir, Vector3.forward);
            inst.transform.localRotation = turn * inst.transform.localRotation;
            inst.transform.localPosition = turn * inst.transform.localPosition;
            var scale = 0.27f / Mathf.Max(0.001f, dir.magnitude);
            inst.transform.localScale *= scale;
            inst.transform.localPosition *= scale;

            // 2) giro em volta do antebraço: polegar para cima, levemente para dentro
            if (thumb != null)
            {
                var t = L(thumb) - L(hand);
                t.z = 0f;
                var want = new Vector3(right ? -0.45f : 0.45f, 1f, 0f);
                if (t.sqrMagnitude > 1e-8f)
                {
                    var roll = Vector3.SignedAngle(t, want, Vector3.forward);
                    var r = Quaternion.AngleAxis(roll, Vector3.forward);
                    inst.transform.localRotation = r * inst.transform.localRotation;
                    inst.transform.localPosition = r * inst.transform.localPosition;
                }
            }

            // 3) dedos fechados em volta da empunhadura. Cada dedo gira sempre no mesmo eixo
            //    (perpendicular ao dedo e à palma), junta por junta, formando um anel.
            var inward = arm.TransformDirection(right ? Vector3.left : Vector3.right);
            var curls = new (string finger, float[] angles)[]
            {
                ("f_index", new[] { fingerCurl, fingerCurl * 1.15f, fingerCurl * 0.65f }),
                ("f_ring", new[] { fingerCurl, fingerCurl * 1.15f, fingerCurl * 0.65f }),
                ("thumb", new[] { fingerCurl * 0.35f, fingerCurl * 0.3f, fingerCurl * 0.15f }),
            };
            var ring = new List<Vector3>();
            foreach (var (finger, angles) in curls)
            {
                var first = Bone($"{finger}.01{side}");
                if (first == null || first.childCount == 0) continue;
                var u0 = first.GetChild(0).position - first.position;
                var axis = Vector3.Cross(u0, inward);
                if (axis.sqrMagnitude < 1e-12f) continue;
                axis.Normalize();
                var sign = Vector3.Dot(Quaternion.AngleAxis(10f, axis) * u0, inward) >= Vector3.Dot(u0, inward) ? 1f : -1f;
                for (var k = 1; k <= 3; k++)
                {
                    var bone = Bone($"{finger}.0{k}{side}");
                    if (bone == null) continue;
                    bone.rotation = Quaternion.AngleAxis(sign * angles[k - 1], axis) * bone.rotation;
                }
                if (finger == "thumb") continue;
                for (var k = 1; k <= 3; k++)
                {
                    var bone = Bone($"{finger}.0{k}{side}");
                    if (bone == null) continue;
                    ring.Add(L(bone));
                    if (k == 3 && bone.childCount > 0) ring.Add(L(bone.GetChild(0)));
                }
            }

            // 4) o centro do punho fechado vai para o soquete (onde fica a empunhadura da arma)
            var fist = Vector3.zero;
            if (ring.Count > 0)
            {
                foreach (var p in ring) fist += p;
                fist /= ring.Count;
            }
            else fist = L(hand) + new Vector3(0f, 0f, 0.09f);
            inst.transform.localPosition += socketLocal - fist;

            // 5) ponto de empunhadura no centro do punho, preso ao osso da mão; Y = eixo do cabo
            //    (sai do punho pelo lado do indicador).
            var index1 = Bone("f_index.01" + side);
            var ring1 = Bone("f_ring.01" + side);
            var handle = index1 != null && ring1 != null ? index1.position - ring1.position : arm.up;
            handle.Normalize();
            var gripPoint = new GameObject("Empunhadura").transform;
            gripPoint.SetParent(hand, false);
            gripPoint.position = arm.TransformPoint(socketLocal);
            var fwd = Vector3.ProjectOnPlane(arm.forward, handle);
            gripPoint.rotation = Quaternion.LookRotation(fwd.sqrMagnitude > 1e-6f ? fwd : arm.forward, handle);
            arm.gameObject.AddComponent<Athoria.Gameplay.Combat.HandGrip>().EditorSetup(hand, gripPoint);

            skin.updateWhenOffscreen = true;
            skin.shadowCastingMode = ShadowCastingMode.Off;
            var tex = Texture("FirstPersonArms/FPS_Psx_Hands/FPSarm_256px.png");
            Crisp(tex);
            skin.sharedMaterial = ViewModelMaterial(tex, "MaosPSX", 0.22f);
            return skin;
        }

        /// <summary>
        /// Transforma a espada longa em khopesh: cabo e um trecho reto, depois a lâmina
        /// se curva para frente em arco (como uma foice) e se alarga. A malha nova é salva
        /// como asset para o Prefab da arma.
        /// </summary>
        public static bool BendIntoKhopesh(Transform root, string meshAssetPath)
        {
            var mf = root.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            var src = mf.sharedMesh;
            var t = mf.transform;
            var verts = src.vertices.Select(v => root.InverseTransformPoint(t.TransformPoint(v))).ToArray();
            var ymin = verts.Min(v => v.y);
            var ymax = verts.Max(v => v.y);
            var span = ymax - ymin;
            var bendStart = ymin + span * 0.42f;
            var bladeLen = ymax - bendStart;
            const float arc = 150f * Mathf.Deg2Rad;
            var radius = bladeLen / arc;
            for (var i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                if (p.y <= bendStart) continue;
                var a = p.y - bendStart;
                var th = a / radius;
                var widen = 1f + 1.1f * Mathf.SmoothStep(0f, 1f, a / (bladeLen * 0.45f));
                var center = new Vector3(radius - radius * Mathf.Cos(th), bendStart + radius * Mathf.Sin(th), 0f);
                var normal = new Vector3(Mathf.Cos(th), -Mathf.Sin(th), 0f);
                verts[i] = center + normal * (p.x * widen) + new Vector3(0f, 0f, p.z);
            }

            var mesh = Object.Instantiate(src);
            mesh.name = "Khopesh";
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Directory.CreateDirectory(Path.GetDirectoryName(meshAssetPath));
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath) != null) AssetDatabase.DeleteAsset(meshAssetPath);
            AssetDatabase.CreateAsset(mesh, meshAssetPath);

            // malha já no espaço da raiz: a peça fica sem deslocamento, giro ou escala
            var holder = mf.gameObject;
            holder.transform.SetParent(root, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;
            mf.sharedMesh = mesh;
            foreach (Transform child in root.Cast<Transform>().ToList())
                if (child.gameObject != holder) Object.DestroyImmediate(child.gameObject);
            holder.name = "Modelo";
            return true;
        }

        /// <summary>Texturas das armas/braços sem mipmap (texel nítido, como no PS1) e em tamanho cheio.</summary>
        public static void Crisp(Texture2D tex)
        {
            if (tex == null) return;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            if (importer == null) return;
            if (!importer.mipmapEnabled && importer.maxTextureSize >= 1024 && importer.filterMode == FilterMode.Point) return;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Material dos modelos em primeira pessoa: textura + um pouco de brilho próprio
        /// (a própria textura como emissão), para a arma e as mãos não sumirem no escuro.
        /// </summary>
        public static Material ViewModelMaterial(Texture2D tex, string name, float glow = 0.32f)
        {
            Directory.CreateDirectory(MaterialFolder);
            var path = $"{MaterialFolder}/PSX_VM_{Sanitize(name)}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = PsxShader;
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            var clip = tex != null && HasAlpha(tex);
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.SetFloat("_AlphaClip", clip ? 1f : 0f);
            mat.SetFloat("_Cutoff", 0.45f);
            if (clip) mat.EnableKeyword("_ALPHATEST_ON"); else mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetTexture("_EmissionMap", tex);
            mat.SetColor("_EmissionColor", new Color(glow, glow, glow));
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static List<Vector3> RootPoints(Transform root, GameObject go)
        {
            var list = new List<Vector3>();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : (r as SkinnedMeshRenderer)?.sharedMesh;
                if (mesh == null) continue;
                foreach (var v in mesh.vertices) list.Add(root.InverseTransformPoint(r.transform.TransformPoint(v)));
            }
            return list;
        }

        private static Bounds LocalBounds(Transform root, GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var first = true;
            var b = new Bounds();
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : (r as SkinnedMeshRenderer)?.sharedMesh;
                if (mesh == null) continue;
                foreach (var v in mesh.vertices)
                {
                    var p = root.InverseTransformPoint(r.transform.TransformPoint(v));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        private static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            // em espaço local do pai (raiz na origem)
            var parent = go.transform.parent;
            if (parent != null) b.center = parent.InverseTransformPoint(b.center);
            return b;
        }

        private static readonly Dictionary<string, string[]> TextureLists = new();

        /// <summary>Acha a textura de um material: mesmo nome, nome parecido ou a textura do próprio modelo.</summary>
        private static Texture2D FindTexture(string packFolder, string modelFolder, Material mat, string modelName)
        {
            // Textura já ligada pelo importador
            if (mat != null)
            {
                foreach (var prop in new[] { "_BaseMap", "_MainTex" })
                    if (mat.HasProperty(prop) && mat.GetTexture(prop) is Texture2D t && t != null) return t;
            }

            if (!TextureLists.TryGetValue(packFolder, out var list))
            {
                list = AssetDatabase.FindAssets("t:Texture2D", new[] { packFolder }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
                TextureLists[packFolder] = list;
            }
            string Norm(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            var matName = mat != null ? Norm(mat.name.Replace("(Instance)", "")) : "";
            var model = Norm(modelName);

            string best = null;
            var bestScore = 0;
            foreach (var p in list)
            {
                var n = Norm(Path.GetFileNameWithoutExtension(p));
                var score = 0;
                if (matName.Length > 0 && n == matName) score = 100;
                else if (matName.Length > 2 && (n.Contains(matName) || matName.Contains(n))) score = 60;
                else if (n == model) score = 50;
                else if (model.Length > 2 && (n.Contains(model) || model.Contains(n))) score = 30;
                if (p.Replace('\\', '/').StartsWith(modelFolder ?? "")) score += 5;
                if (n.Contains("normal") || n.Contains("rough") || n.Contains("metal") || n.Contains("preview")) score -= 80;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best != null && bestScore >= 30 ? AssetDatabase.LoadAssetAtPath<Texture2D>(best) : null;
        }

        private static bool HasAlpha(Texture2D tex)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            return importer != null && importer.DoesSourceTextureHaveAlpha();
        }

        private static string Sanitize(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
    }

    /// <summary>Conjunto de peças de um kit modular usado para "vestir" a geometria de blocos.</summary>
    internal sealed class KitStyle
    {
        public GameObject[] Walls = new GameObject[0];
        public GameObject Floor;
        public GameObject Pillar;
        public GameObject Door;

        public bool IsEmpty => Walls.Length == 0 && Floor == null && Pillar == null && Door == null;

        public static KitStyle Of(GameObject floor, GameObject pillar, GameObject door, params GameObject[] walls) => new()
        {
            Walls = walls.Where(w => w != null).ToArray(), Floor = floor, Pillar = pillar, Door = door
        };
    }

    /// <summary>
    /// Troca o visual dos blocos cinza (cubos com colisor) por peças dos kits PS1,
    /// sem mexer na colisão nem no NavMesh: o cubo continua lá, só com o Renderer
    /// desligado. Paredes viram fileiras de módulos, pisos viram ladrilhos, colunas
    /// viram pilares e as portas de passagem ganham uma porta de verdade.
    /// </summary>
    internal static class KitDresser
    {
        private static readonly Dictionary<GameObject, Bounds> SizeCache = new();

        /// <summary>Veste todos os blocos dentro de root. skip: nomes que ficam como estão.</summary>
        public static int Dress(Transform root, KitStyle style, int seed, System.Func<string, bool> skip = null)
        {
            if (root == null || style == null || style.IsEmpty) return 0;
            var rng = new System.Random(seed);
            var boxes = root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => IsBlock(r, skip))
                .ToList();
            var dressed = 0;
            foreach (var r in boxes)
            {
                var t = r.transform;
                var size = t.localScale;
                var holder = new GameObject(t.name + " (kit)").transform;
                holder.SetParent(t.parent, false);
                holder.localPosition = t.localPosition;
                holder.localRotation = t.localRotation;

                bool ok;
                if (r.GetComponent<Portal>() != null) ok = style.Door != null && Fit(style.Door, holder, Vector3.zero, size, false);
                else if (size.y <= 0.35f && Mathf.Max(size.x, size.z) >= 1f) ok = Floor(style, holder, size);
                else if (Mathf.Max(size.x, size.z) <= 1.2f && size.y >= 1.5f) ok = style.Pillar != null && Fit(style.Pillar, holder, Vector3.zero, size, false);
                else if (Mathf.Min(size.x, size.z) <= 1f && Mathf.Max(size.x, size.z) >= 1.2f && size.y >= 1.2f) ok = Wall(style, holder, size, rng);
                else ok = false;

                if (ok)
                {
                    r.enabled = false;
                    dressed++;
                }
                else Object.DestroyImmediate(holder.gameObject);
            }
            return dressed;
        }

        private static bool IsBlock(MeshRenderer r, System.Func<string, bool> skip)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Cube") return false;
            if (PrefabUtility.IsPartOfPrefabInstance(r.gameObject)) return false;
            if (skip != null && skip(r.name)) return false;
            if (r.GetComponent<Portal>() != null) return true;
            var col = r.GetComponent<BoxCollider>();
            if (col == null || col.isTrigger) return false;
            // Só blocos simples (Transform, MeshFilter, MeshRenderer, BoxCollider): nada de lógica de jogo.
            return r.GetComponents<Component>().Length <= 4;
        }

        private static bool Wall(KitStyle style, Transform holder, Vector3 size, System.Random rng)
        {
            if (style.Walls.Length == 0) return false;
            var alongX = size.x >= size.z;
            var length = alongX ? size.x : size.z;
            var thickness = alongX ? size.z : size.x;
            var natural = Natural(style.Walls[0]);
            var pieceLen = Mathf.Max(natural.size.x, natural.size.z);
            var count = Mathf.Clamp(Mathf.RoundToInt(length / Mathf.Max(0.1f, pieceLen)), 1, 30);
            var step = length / count;
            var any = false;
            for (var i = 0; i < count; i++)
            {
                var offset = -length * 0.5f + step * (i + 0.5f);
                var center = alongX ? new Vector3(offset, 0f, 0f) : new Vector3(0f, 0f, offset);
                var pieceSize = alongX ? new Vector3(step, size.y, thickness) : new Vector3(thickness, size.y, step);
                any |= Fit(style.Walls[rng.Next(style.Walls.Length)], holder, center, pieceSize, false);
            }
            return any;
        }

        private static bool Floor(KitStyle style, Transform holder, Vector3 size)
        {
            if (style.Floor == null) return false;
            var natural = Natural(style.Floor);
            var cx = Mathf.Clamp(Mathf.RoundToInt(size.x / Mathf.Max(0.1f, natural.size.x)), 1, 40);
            var cz = Mathf.Clamp(Mathf.RoundToInt(size.z / Mathf.Max(0.1f, natural.size.z)), 1, 40);
            var sx = size.x / cx;
            var sz = size.z / cz;
            var any = false;
            for (var ix = 0; ix < cx; ix++)
            for (var iz = 0; iz < cz; iz++)
            {
                var center = new Vector3(-size.x * 0.5f + sx * (ix + 0.5f), 0f, -size.z * 0.5f + sz * (iz + 0.5f));
                any |= Fit(style.Floor, holder, center, new Vector3(sx, size.y, sz), true);
            }
            return any;
        }

        /// <summary>
        /// Encaixa a peça numa caixa (centro e tamanho no espaço do holder).
        /// Gira 90° quando o lado comprido da peça não bate com o da caixa.
        /// keepHeight: mantém a altura original (pisos) e alinha o topo ao topo da caixa.
        /// </summary>
        public static bool Fit(GameObject prefab, Transform holder, Vector3 center, Vector3 size, bool keepHeight)
        {
            if (prefab == null) return false;
            var nat = Natural(prefab).size;
            var rotate = (size.x >= size.z && nat.z > nat.x * 1.2f) || (size.z > size.x && nat.x > nat.z * 1.2f);
            float S(float target, float natural) => natural < 0.02f ? 1f : target / natural;
            var scale = rotate
                ? new Vector3(S(size.z, nat.x), keepHeight ? 1f : S(size.y, nat.y), S(size.x, nat.z))
                : new Vector3(S(size.x, nat.x), keepHeight ? 1f : S(size.y, nat.y), S(size.z, nat.z));
            var height = keepHeight ? nat.y : size.y;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
            go.transform.localRotation = rotate ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            go.transform.localScale = scale;
            go.transform.localPosition = center + Vector3.up * (size.y * 0.5f - height);
            if (!keepHeight) go.transform.localPosition = center - Vector3.up * size.y * 0.5f;
            return true;
        }

        /// <summary>Tamanho do modelo (a raiz dos Prefabs dos kits fica na base, no centro).</summary>
        private static Bounds Natural(GameObject prefab)
        {
            if (SizeCache.TryGetValue(prefab, out var b)) return b;
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var renderers = temp.GetComponentsInChildren<Renderer>();
                b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.one);
                foreach (var r in renderers) b.Encapsulate(r.bounds);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
            SizeCache[prefab] = b;
            return b;
        }
    }
}
