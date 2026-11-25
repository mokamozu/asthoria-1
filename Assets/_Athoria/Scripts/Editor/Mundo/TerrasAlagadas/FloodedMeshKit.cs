using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Editor
{
    /// <summary>
    /// Malhas das Terras Alagadas com UV em metros: a textura repete do mesmo tamanho em
    /// qualquer parede, tábua ou telhado (um cubo esticado do Unity esticaria o tijolo).
    /// As malhas ficam guardadas num asset só (Data/Mundo/TerrasAlagadas/Malhas.asset) e
    /// são reaproveitadas por tamanho.
    /// </summary>
    internal static class FloodedMeshKit
    {
        public const string ContainerPath = "Assets/_Athoria/Data/Mundo/TerrasAlagadas/Malhas.asset";

        private static Mesh _container;
        private static readonly Dictionary<string, Mesh> Cache = new();

        /// <summary>Recomeça o asset de malhas (chamado no início da geração do mapa).</summary>
        public static void Begin()
        {
            Cache.Clear();
            Directory.CreateDirectory(Path.GetDirectoryName(ContainerPath));
            AssetDatabase.DeleteAsset(ContainerPath);
            _container = new Mesh { name = "Malhas Terras Alagadas" };   // asset principal vazio; as malhas vão dentro dele
            AssetDatabase.CreateAsset(_container, ContainerPath);
        }

        public static void End() { if (_container != null) { EditorUtility.SetDirty(_container); AssetDatabase.SaveAssets(); } }

        private static Mesh Store(Mesh mesh)
        {
            if (_container == null) Begin();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.AddObjectToAsset(mesh, _container);
            return mesh;
        }

        // ------------------------------------------------------------------ caixa

        /// <summary>Caixa centrada com UV em metros (tile = metros por repetição da textura).</summary>
        public static Mesh BoxMesh(Vector3 size, float tile)
        {
            var key = $"box_{size.x:F2}_{size.y:F2}_{size.z:F2}_{tile:F2}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            var h = size * 0.5f;
            void Face(Vector3 normal, Vector3 right, Vector3 up, float w, float hgt)
            {
                var c = Vector3.Scale(normal, h);
                var b = v.Count;
                var r = right * (w * 0.5f); var u = up * (hgt * 0.5f);
                v.Add(c - r - u); v.Add(c + r - u); v.Add(c + r + u); v.Add(c - r + u);
                for (var i = 0; i < 4; i++) n.Add(normal);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(w / tile, 0)); uv.Add(new Vector2(w / tile, hgt / tile)); uv.Add(new Vector2(0, hgt / tile));
                t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);
            var mesh = new Mesh { name = key };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            return Cache[key] = Store(mesh);
        }

        /// <summary>Peça em caixa com colisão (UV em metros).</summary>
        public static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, Vector3 euler = default,
            bool collider = true, float tile = 2f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, tile);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
            return go;
        }

        // ------------------------------------------------------------------ telhado de duas águas

        /// <summary>
        /// Telhado de duas águas: cumeeira ao longo de X. Submalha 0 = águas (telha), 1 = empenas
        /// (as paredes triangulares das pontas). Base em y = 0.
        /// </summary>
        public static Mesh GableMesh(float width, float depth, float height, float overhang, float tile)
        {
            var key = $"gable_{width:F2}_{depth:F2}_{height:F2}_{overhang:F2}_{tile:F2}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var slopes = new List<int>(); var ends = new List<int>();
            float hw = width * 0.5f + overhang, hd = depth * 0.5f + overhang;
            var slopeLen = Mathf.Sqrt(hd * hd + height * height);
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float w, float h, List<int> into)
            {
                var i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(w / tile, 0)); uv.Add(new Vector2(w / tile, h / tile)); uv.Add(new Vector2(0, h / tile));
                into.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            // água da frente (+Z) e de trás (-Z)
            Quad(new Vector3(hw, -overhang * 0.4f, hd), new Vector3(-hw, -overhang * 0.4f, hd), new Vector3(-hw, height, 0), new Vector3(hw, height, 0), hw * 2f, slopeLen, slopes);
            Quad(new Vector3(-hw, -overhang * 0.4f, -hd), new Vector3(hw, -overhang * 0.4f, -hd), new Vector3(hw, height, 0), new Vector3(-hw, height, 0), hw * 2f, slopeLen, slopes);
            // empenas (triângulos), no plano da parede
            float ew = width * 0.5f, ed = depth * 0.5f;
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                var i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                uv.Add(new Vector2(a.z / tile, a.y / tile)); uv.Add(new Vector2(b.z / tile, b.y / tile)); uv.Add(new Vector2(c.z / tile, c.y / tile));
                ends.AddRange(new[] { i, i + 1, i + 2 });
            }
            Tri(new Vector3(ew, 0, -ed), new Vector3(ew, height, 0), new Vector3(ew, 0, ed));
            Tri(new Vector3(-ew, 0, ed), new Vector3(-ew, height, 0), new Vector3(-ew, 0, -ed));
            var mesh = new Mesh { name = key, subMeshCount = 2 };
            mesh.SetVertices(v); mesh.SetUVs(0, uv);
            mesh.SetTriangles(slopes, 0); mesh.SetTriangles(ends, 1);
            mesh.RecalculateNormals();
            return Cache[key] = Store(mesh);
        }

        public static GameObject Gable(Transform parent, string name, Vector3 localPos, float yaw, float width, float depth, float height,
            Material roof, Material gableWall, float overhang = 0.4f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = GableMesh(width, depth, height, overhang, 2f);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { roof, gableWall };
            go.isStatic = true;
            return go;
        }

        // ------------------------------------------------------------------ painéis (janelas, portas, grades)

        /// <summary>Junta vários retângulos planos (centro, tamanho) numa malha só, virados para +Z.</summary>
        public static Mesh Panels(string key, List<(Vector3 center, Vector2 size)> rects)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            foreach (var (c, s) in rects)
            {
                var b = v.Count;
                var rx = Vector3.right * s.x * 0.5f; var uy = Vector3.up * s.y * 0.5f;
                v.Add(c - rx - uy); v.Add(c + rx - uy); v.Add(c + rx + uy); v.Add(c - rx + uy);
                for (var i = 0; i < 4; i++) n.Add(Vector3.forward);
                uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1));
                t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            return Store(mesh);
        }

        public static GameObject PanelObject(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = true;
            return go;
        }

        /// <summary>Várias caixas (barras de grade, estacas) numa malha só, com UV em metros.</summary>
        public static Mesh Bars(string key, List<(Vector3 center, Vector3 size)> boxes, float tile = 1f)
        {
            var combine = new List<CombineInstance>();
            foreach (var (c, s) in boxes)
                combine.Add(new CombineInstance { mesh = BoxMesh(s, tile), transform = Matrix4x4.Translate(c) });
            var mesh = new Mesh { name = key, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(combine.ToArray(), true, true);
            return Store(mesh);
        }

        public static GameObject Combined(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos, float yaw, Vector3? colliderSize = null, Vector3 colliderCenter = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (colliderSize.HasValue)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.size = colliderSize.Value;
                bc.center = colliderCenter;
            }
            go.isStatic = true;
            return go;
        }

        // ------------------------------------------------------------------ disco (água da peste)

        public static Mesh Disc(float radius, int segments, float tile)
        {
            var v = new List<Vector3> { Vector3.zero };
            var uv = new List<Vector2> { Vector2.zero };
            var t = new List<int>();
            for (var i = 0; i <= segments; i++)
            {
                var a = i * Mathf.PI * 2f / segments;
                var p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                v.Add(p); uv.Add(new Vector2(p.x / tile, p.z / tile));
                if (i > 0) t.AddRange(new[] { 0, i + 1, i });
            }
            var mesh = new Mesh { name = $"disco_{radius:F0}" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            return Store(mesh);
        }

        /// <summary>Plano grande (água do pântano) dividido em quadrados, para a neblina e o tremor PS1 funcionarem bem.</summary>
        public static Mesh Grid(float size, int cells)
        {
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            var step = size / cells;
            for (var z = 0; z <= cells; z++)
            for (var x = 0; x <= cells; x++)
            {
                v.Add(new Vector3(-size * 0.5f + x * step, 0f, -size * 0.5f + z * step));
                uv.Add(new Vector2(x, z));
            }
            for (var z = 0; z < cells; z++)
            for (var x = 0; x < cells; x++)
            {
                var i = z * (cells + 1) + x;
                t.AddRange(new[] { i, i + cells + 1, i + 1, i + 1, i + cells + 1, i + cells + 2 });
            }
            var mesh = new Mesh { name = "agua_grade", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            return Store(mesh);
        }
    }
}
