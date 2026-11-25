using System.Linq;
using Athoria.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Editor
{
    /// <summary>
    /// Braços em primeira pessoa com o WRAD ARMS (wriks, CC0): braço inteiro, do ombro aos dedos,
    /// com a textura original (pele clara). Os braços de referência antigos continuam no rig,
    /// invisíveis, guiando as poses, golpes e pegadas; o FirstPersonArmsIK leva os pulsos do
    /// WRAD até eles com IK de dois ossos.
    /// </summary>
    public static class WradArmsBuilder
    {
        private const string Folder = "Assets/_Athoria/ThirdParty/WradArms";
        private const string Fbx = Folder + "/arms.fbx";
        private const string Texture = Folder + "/arm_albedo_pale.png";

        public static bool Available => AssetDatabase.LoadAssetAtPath<GameObject>(Fbx) != null;

        public static FirstPersonArmsIK Build(Transform motionRoot, Transform rightArm, Transform leftArm, Transform rightSocket, Transform leftSocket)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (model == null) return null;

            var holder = new GameObject("Bracos WRAD").transform;           // eixos = câmera (o IK usa este espaço)
            holder.SetParent(motionRoot, false);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Modelo WRAD";
            inst.transform.SetParent(holder, false);
            foreach (var a in inst.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);

            Transform B(string n) => inst.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
            var root = B("root");
            var head = B("head");
            var headEnd = B("head_end");
            if (root == null || head == null || B("wrist.r") == null || B("wrist.l") == null)
            {
                Debug.LogWarning("[Athoria] WRAD ARMS sem os ossos esperados; ficam os braços antigos.");
                Object.DestroyImmediate(holder.gameObject);
                return null;
            }

            // 1) alinhar: o osso "head" olha para a frente da câmera, ombros embaixo dele
            var fwd = headEnd != null ? headEnd.position - head.position : Vector3.forward;
            var up = head.position - root.position;
            if (Vector3.Cross(fwd, up).sqrMagnitude < 1e-8f) up = Vector3.up;
            var align = Quaternion.LookRotation(holder.forward, holder.up) * Quaternion.Inverse(Quaternion.LookRotation(fwd, up));
            inst.transform.rotation = align * inst.transform.rotation;

            // 2) escala: antebraço com 32 cm (braços de primeira pessoa são um pouco longos)
            var forearmLen = Vector3.Distance(B("forearm.r").position, B("wrist.r").position);
            inst.transform.localScale *= 0.32f / Mathf.Max(1e-5f, forearmLen);

            // 3) posição: o "head" do rig fica no olho (câmera), um pouco para trás
            inst.transform.position += holder.TransformPoint(new Vector3(0f, 0.02f, -0.1f)) - head.position;

            // 4) dedos fechados em volta do soquete (punho), como uma mão segurando um cabo.
            //    Guarda as duas poses (aberta e fechada) para as ações abrirem a mão (pegar, interagir).
            var fingerSets = new System.Collections.Generic.Dictionary<string, (Transform[] bones, Quaternion[] open, Quaternion[] closed)>();
            foreach (var side in new[] { "r", "l" })
            {
                var bones = new System.Collections.Generic.List<Transform>();
                foreach (var f in new[] { "index", "middle", "ring", "pinky", "thumb" })
                    for (var k = 1; k <= 3; k++) { var b = B($"finger_{f}{k}.{side}"); if (b != null) bones.Add(b); }
                var open = bones.Select(b => b.localRotation).ToArray();
                CurlFingers(B, side);
                fingerSets[side] = (bones.ToArray(), open, bones.Select(b => b.localRotation).ToArray());
            }

            // 5) material com a textura original
            var skin = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin != null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Texture);
                PsxAssets.Crisp(tex);
                skin.sharedMaterial = PsxAssets.ViewModelMaterial(tex, "MaosWRAD", 0.22f);
                skin.shadowCastingMode = ShadowCastingMode.Off;
                skin.updateWhenOffscreen = true;
            }

            // 6) IK ligado aos braços de referência
            var ik = holder.gameObject.AddComponent<FirstPersonArmsIK>();
            var so = new SerializedObject(ik);
            so.FindProperty("rigRoot").objectReferenceValue = holder;
            Wire(so.FindProperty("right"), B, "r", rightArm, rightSocket, new Vector3(0.7f, -1f, -0.3f));
            Wire(so.FindProperty("left"), B, "l", leftArm, leftSocket, new Vector3(-0.7f, -1f, -0.3f));
            so.ApplyModifiedPropertiesWithoutUndo();
            ik.EditorSetFingers(fingerSets["r"].bones, fingerSets["r"].open, fingerSets["r"].closed,
                fingerSets["l"].bones, fingerSets["l"].open, fingerSets["l"].closed);

            // 7) os braços antigos ficam invisíveis (só guiam as poses)
            foreach (var arm in new[] { rightArm, leftArm })
                foreach (var r in arm.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            return ik;
        }

        private static void Wire(SerializedProperty p, System.Func<string, Transform> B, string side, Transform arm, Transform socket, Vector3 hint)
        {
            var grip = arm.GetComponent<HandGrip>();
            var target = grip != null && grip.HandBone != null ? grip.HandBone : arm;
            var anchor = grip != null && grip.Point != null ? grip.Point : socket;
            var wrist = B("wrist." + side);
            p.FindPropertyRelative("upper").objectReferenceValue = B("bicep." + side);
            p.FindPropertyRelative("lower").objectReferenceValue = B("forearm." + side);
            p.FindPropertyRelative("hand").objectReferenceValue = wrist;
            p.FindPropertyRelative("twist0").objectReferenceValue = B("forearm.Twist0." + side);
            p.FindPropertyRelative("twist1").objectReferenceValue = B("forearm.Twist1." + side);
            p.FindPropertyRelative("target").objectReferenceValue = target;
            p.FindPropertyRelative("anchor").objectReferenceValue = anchor;
            p.FindPropertyRelative("socket").objectReferenceValue = B("socket." + side);
            p.FindPropertyRelative("elbowHint").vector3Value = hint;

            // Giro que leva o pulso WRAD a ficar como a mão de referência (dedos para o mesmo lado,
            // polegar para o mesmo lado). offset = inverso(mão de referência) * pulso desejado.
            Transform P(string n) => arm.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
            var S = side == "r" ? ".R" : ".L";
            var pIndex = P("f_index.01" + S);
            var pRing = P("f_ring.01" + S);
            var pThumb = P("thumb.01" + S);
            var wMiddle = B("finger_middle1." + side);
            var wThumb = B("finger_thumb1." + side);
            var offset = Quaternion.identity;
            if (pIndex != null && pRing != null && pThumb != null && wMiddle != null && wThumb != null)
            {
                var pF = (pIndex.position + pRing.position) * 0.5f - target.position;
                var pT = pThumb.position - target.position;
                var wF = wMiddle.position - wrist.position;
                var wT = wThumb.position - wrist.position;
                var rp = Quaternion.LookRotation(pF, Vector3.ProjectOnPlane(pT, pF));
                var rw = Quaternion.LookRotation(wF, Vector3.ProjectOnPlane(wT, wF));
                offset = Quaternion.Inverse(target.rotation) * (rp * Quaternion.Inverse(rw)) * wrist.rotation;
            }
            else Debug.LogWarning($"[Athoria] WRAD: não achei os dedos para alinhar a mão ({side}); usando giro neutro.");
            p.FindPropertyRelative("rotationOffset").quaternionValue = offset;
        }

        private static void CurlFingers(System.Func<string, Transform> B, string side)
        {
            var socket = B("socket." + side);
            if (socket == null) return;
            var fingers = new (string name, float[] angles)[]
            {
                ("index", new[] { 55f, 70f, 40f }), ("middle", new[] { 60f, 75f, 45f }),
                ("ring", new[] { 62f, 78f, 45f }), ("pinky", new[] { 65f, 80f, 45f }),
                ("thumb", new[] { 20f, 25f, 15f }),
            };
            foreach (var (name, angles) in fingers)
            {
                for (var k = 1; k <= 3; k++)
                {
                    var bone = B($"finger_{name}{k}.{side}");
                    if (bone == null || bone.childCount == 0) continue;
                    var child = bone.GetChild(0);
                    var dir = child.position - bone.position;
                    var inward = socket.position - bone.position;
                    var axis = Vector3.Cross(dir, inward);
                    if (axis.sqrMagnitude < 1e-12f) continue;
                    axis.Normalize();
                    // sinal: o giro tem que aproximar o dedo do centro do punho
                    var test = Quaternion.AngleAxis(5f, axis) * dir;
                    var sign = Vector3.Dot(test.normalized, inward.normalized) >= Vector3.Dot(dir.normalized, inward.normalized) ? 1f : -1f;
                    bone.rotation = Quaternion.AngleAxis(sign * angles[k - 1], axis) * bone.rotation;
                }
            }
        }
    }
}
