using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Athoria.Gameplay.World
{
    /// <summary>
    /// Grama baixa, ervas secas e juncos altos desenhados com Graphics.DrawMeshInstanced:
    /// milhares de touceiras em poucas chamadas de desenho. O mapa é dividido em células;
    /// cada célula perto da câmera é preenchida uma vez (sempre igual, pela semente) lendo o
    /// terreno carregado: altura, inclinação, a textura pintada (camada do splat) e a
    /// profundidade da água. Objetos sobre o chão (casas, passarelas) bloqueiam a vegetação.
    /// O vento é feito no shader "Athoria/Folhagem Vento".
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class FoliageInstancer : MonoBehaviour
    {
        public enum Shape { Tuft, Reeds }

        [Serializable]
        public sealed class Layer
        {
            public string name = "Grama";
            public Shape shape = Shape.Tuft;
            public Material material;
            [Min(0f), Tooltip("Touceiras por m² onde a camada cabe.")] public float density = 1.5f;
            [Tooltip("Altura mínima e máxima (m).")] public Vector2 height = new(0.3f, 0.6f);
            [Tooltip("Largura em relação à altura.")] public float widthRatio = 1.1f;
            [Tooltip("Camadas de textura do terreno onde nasce (índices do splat).")] public int[] terrainLayers = Array.Empty<int>();
            [Range(0f, 1f)] public float minWeight = 0.35f;
            [Tooltip("Profundidade da água no chão (m): mínimo e máximo. Negativo = seco.")] public Vector2 depth = new(-99f, -0.05f);
            [Range(0f, 90f)] public float maxSlope = 32f;
            [Tooltip("Agrupamento: 0 = espalhado, 1 = só em manchas.")] [Range(0f, 1f)] public float clumping = 0.4f;
            [Min(1f)] public float drawDistance = 40f;
        }

        [SerializeField] private Layer[] layers = Array.Empty<Layer>();
        [SerializeField, Min(4f)] private float cellSize = 16f;
        [SerializeField, Tooltip("Áreas com vegetação mais densa (x, z, raio, multiplicador).")] private Vector4[] denseAreas = Array.Empty<Vector4>();
        [SerializeField, Tooltip("Áreas sem vegetação (x, z, raio, 0).")] private Vector4[] clearAreas = Array.Empty<Vector4>();
        [SerializeField] private int seed = 1337;
        [SerializeField, Tooltip("Células geradas por quadro (espalha o custo).")] private int cellsPerFrame = 2;

        private sealed class Cell
        {
            public List<Matrix4x4[]>[] batches;
            public Bounds bounds;
            public int lastUsed;
        }

        private readonly Dictionary<Vector2Int, Cell> _cells = new();
        private readonly Dictionary<Terrain, float[,,]> _alpha = new();
        private readonly List<Vector2Int> _wanted = new();
        private readonly List<Vector2Int> _drop = new();
        private readonly Plane[] _planes = new Plane[6];
        private readonly List<Matrix4x4> _scratch = new();
        private Mesh _tuft, _reeds;
        private float _maxDistance;
        private int _frame;

        private void Awake()
        {
            _tuft = BuildMesh(3, 1f, 1f, false);
            _reeds = BuildMesh(2, 1f, 1f, true);
            foreach (var l in layers) _maxDistance = Mathf.Max(_maxDistance, l.drawDistance);
        }

        private void OnDestroy()
        {
            if (_tuft != null) Destroy(_tuft);
            if (_reeds != null) Destroy(_reeds);
        }

        /// <summary>Planos cruzados (quads verticais). Cor do vértice A: 0 na base, 1 no topo (vento).</summary>
        private static Mesh BuildMesh(int planes, float width, float height, bool tall)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var tris = new List<int>();
            for (var p = 0; p < planes; p++)
            {
                var a = p * Mathf.PI / planes;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (width * 0.5f);
                var b = verts.Count;
                verts.Add(-dir); verts.Add(dir); verts.Add(dir + Vector3.up * height); verts.Add(-dir + Vector3.up * height);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                colors.Add(new Color(1, 1, 1, 0)); colors.Add(new Color(1, 1, 1, 0));
                colors.Add(new Color(1, 1, 1, 1)); colors.Add(new Color(1, 1, 1, 1));
                tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var mesh = new Mesh { name = tall ? "Juncos" : "Touceira" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.up * height * 0.5f, new Vector3(width + 1f, height + 1f, width + 1f));
            return mesh;
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || layers.Length == 0) return;
            _frame++;
            var pos = cam.transform.position;
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);

            // Células no raio: gera algumas por quadro, as mais próximas primeiro.
            var r = Mathf.CeilToInt(_maxDistance / cellSize);
            var center = new Vector2Int(Mathf.FloorToInt(pos.x / cellSize), Mathf.FloorToInt(pos.z / cellSize));
            _wanted.Clear();
            for (var dz = -r; dz <= r; dz++)
            for (var dx = -r; dx <= r; dx++)
            {
                var key = new Vector2Int(center.x + dx, center.y + dz);
                if (CellDistance(key, pos) > _maxDistance) continue;
                if (_cells.TryGetValue(key, out var cell)) { cell.lastUsed = _frame; continue; }
                _wanted.Add(key);
            }
            _wanted.Sort((a, b) => CellDistance(a, pos).CompareTo(CellDistance(b, pos)));
            for (var i = 0; i < Mathf.Min(cellsPerFrame, _wanted.Count); i++)
            {
                var cell = Generate(_wanted[i]);
                if (cell != null) { cell.lastUsed = _frame; _cells[_wanted[i]] = cell; }
            }

            // Desenha
            foreach (var kv in _cells)
            {
                var cell = kv.Value;
                if (cell.lastUsed != _frame) continue;
                if (!GeometryUtility.TestPlanesAABB(_planes, cell.bounds)) continue;
                var dist = CellDistance(kv.Key, pos);
                for (var l = 0; l < layers.Length; l++)
                {
                    var layer = layers[l];
                    if (layer.material == null || dist > layer.drawDistance) continue;
                    var mesh = layer.shape == Shape.Reeds ? _reeds : _tuft;
                    foreach (var batch in cell.batches[l])
                        Graphics.DrawMeshInstanced(mesh, 0, layer.material, batch, batch.Length, null,
                            ShadowCastingMode.Off, false, gameObject.layer, null, LightProbeUsage.Off);
                }
            }

            // Esquece células que ficaram longe
            _drop.Clear();
            foreach (var kv in _cells) if (_frame - kv.Value.lastUsed > 120) _drop.Add(kv.Key);
            foreach (var k in _drop) _cells.Remove(k);
        }

        private float CellDistance(Vector2Int key, Vector3 pos)
        {
            var cx = (key.x + 0.5f) * cellSize;
            var cz = (key.y + 0.5f) * cellSize;
            return Mathf.Max(0f, new Vector2(cx - pos.x, cz - pos.z).magnitude - cellSize * 0.71f);
        }

        // ------------------------------------------------------------------ geração

        private Cell Generate(Vector2Int key)
        {
            var ox = key.x * cellSize;
            var oz = key.y * cellSize;
            var center = new Vector3(ox + cellSize * 0.5f, 0f, oz + cellSize * 0.5f);
            if (FindTerrain(center) == null) return null;    // pedaço do mapa ainda não carregou
            var water = SwampWater.Current;
            var waterLevel = water != null ? water.WaterLevel : float.NegativeInfinity;

            var cell = new Cell { batches = new List<Matrix4x4[]>[layers.Length] };
            float minY = float.MaxValue, maxY = float.MinValue;
            var rng = new System.Random(seed ^ (key.x * 73856093) ^ (key.y * 19349663));

            for (var l = 0; l < layers.Length; l++)
            {
                var layer = layers[l];
                _scratch.Clear();
                var dense = Dense(center);
                var count = Mathf.RoundToInt(layer.density * dense * cellSize * cellSize);
                for (var i = 0; i < count; i++)
                {
                    var x = ox + (float)rng.NextDouble() * cellSize;
                    var z = oz + (float)rng.NextDouble() * cellSize;
                    var roll = (float)rng.NextDouble();
                    var size = Mathf.Lerp(layer.height.x, layer.height.y, (float)rng.NextDouble());
                    var yaw = (float)rng.NextDouble() * 360f;
                    var tilt = ((float)rng.NextDouble() - 0.5f) * 14f;

                    if (layer.clumping > 0f)
                    {
                        var clump = Mathf.PerlinNoise(x * 0.11f + seed * 0.01f + l * 17.3f, z * 0.11f + l * 9.1f);
                        if (clump < layer.clumping * 0.75f + roll * 0.0001f) continue;
                    }
                    if (Cleared(x, z)) continue;
                    var terrain = FindTerrain(new Vector3(x, 0f, z));
                    if (terrain == null) continue;
                    var td = terrain.terrainData;
                    var tp = terrain.transform.position;
                    var nx = (x - tp.x) / td.size.x;
                    var nz = (z - tp.z) / td.size.z;
                    var ground = td.GetInterpolatedHeight(nx, nz) + tp.y;
                    var depth = waterLevel - ground;
                    if (depth < layer.depth.x || depth > layer.depth.y) continue;
                    if (td.GetSteepness(nx, nz) > layer.maxSlope) continue;
                    if (layer.terrainLayers.Length > 0 && Weight(terrain, nx, nz, layer.terrainLayers) < layer.minWeight * (0.6f + roll * 0.8f)) continue;
                    // Algo em cima do chão (casa, passarela, rocha)? Então não nasce ali.
                    if (Physics.Raycast(new Vector3(x, ground + 30f, z), Vector3.down, out var hit, 31f, ~0, QueryTriggerInteraction.Ignore)
                        && !(hit.collider is TerrainCollider) && hit.point.y > ground + 0.05f) continue;

                    var p = new Vector3(x, ground - 0.03f, z);
                    _scratch.Add(Matrix4x4.TRS(p, Quaternion.Euler(tilt, yaw, tilt * 0.5f), new Vector3(size * layer.widthRatio, size, size * layer.widthRatio)));
                    minY = Mathf.Min(minY, p.y);
                    maxY = Mathf.Max(maxY, p.y + size);
                }
                var list = new List<Matrix4x4[]>();
                for (var s = 0; s < _scratch.Count; s += 1023)
                    list.Add(_scratch.GetRange(s, Mathf.Min(1023, _scratch.Count - s)).ToArray());
                cell.batches[l] = list;
            }
            if (minY > maxY) { minY = 0f; maxY = 1f; }
            cell.bounds = new Bounds(new Vector3(center.x, (minY + maxY) * 0.5f, center.z), new Vector3(cellSize + 2f, maxY - minY + 3f, cellSize + 2f));
            return cell;
        }

        private float Dense(Vector3 p)
        {
            var m = 1f;
            foreach (var a in denseAreas)
            {
                var d = new Vector2(p.x - a.x, p.z - a.y).magnitude;
                if (d < a.z) m = Mathf.Max(m, Mathf.Lerp(a.w, 1f, d / a.z));
            }
            return m;
        }

        private bool Cleared(float x, float z)
        {
            foreach (var a in clearAreas)
                if (new Vector2(x - a.x, z - a.y).sqrMagnitude < a.z * a.z) return true;
            return false;
        }

        private static Terrain FindTerrain(Vector3 p)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null) continue;
                var tp = t.transform.position;
                var size = t.terrainData.size;
                if (p.x >= tp.x && p.x <= tp.x + size.x && p.z >= tp.z && p.z <= tp.z + size.z) return t;
            }
            return null;
        }

        private float Weight(Terrain terrain, float nx, float nz, int[] wanted)
        {
            if (!_alpha.TryGetValue(terrain, out var maps))
            {
                var td = terrain.terrainData;
                maps = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
                _alpha[terrain] = maps;
                // terrenos descarregados somem do dicionário
                var dead = new List<Terrain>();
                foreach (var k in _alpha.Keys) if (k == null) dead.Add(k);
                foreach (var k in dead) _alpha.Remove(k);
            }
            var w = maps.GetLength(1);
            var h = maps.GetLength(0);
            var ax = Mathf.Clamp(Mathf.FloorToInt(nx * w), 0, w - 1);
            var az = Mathf.Clamp(Mathf.FloorToInt(nz * h), 0, h - 1);
            var sum = 0f;
            foreach (var i in wanted) if (i >= 0 && i < maps.GetLength(2)) sum += maps[az, ax, i];
            return sum;
        }

#if UNITY_EDITOR
        public void EditorSetup(Layer[] newLayers, Vector4[] dense, Vector4[] clear, float cell)
        {
            layers = newLayers; denseAreas = dense; clearAreas = clear; cellSize = cell;
        }
#endif
    }
}
