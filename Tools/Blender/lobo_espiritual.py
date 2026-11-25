"""
ATHORIA — Lobo Espiritual (Spirit Wolf) da Akane
=================================================

Gera, só com código, um modelo low-poly no estilo PS1/N64 (cerca de 420 triângulos),
as texturas pixeladas com pontilhado, o esqueleto com pesos e 7 animações:

    Idle, Walk, Run, Attack, Hurt, Death, Summon

e exporta tudo em FBX (+ as texturas PNG) pronto para o Unity.

Como usar
---------
1. Pelo terminal (sem abrir a interface):
       blender --background --python lobo_espiritual.py -- --out ./LoboEspiritual
   Opcional: --render  (gera prévias PNG de cada animação com a luz de estúdio)
             --blend   (salva também o .blend)
2. Pelo Blender: aba Scripting > Abrir este arquivo > Run Script.
   Os arquivos vão para a pasta "LoboEspiritual" ao lado do .blend (ou da pasta temporária).

Convenções
----------
* Metros; Z para cima; o lobo olha para -Y (o padrão que o Unity recebe como +Z).
* Pesos rígidos (um osso por vértice), como nos jogos de PS1.
* Materiais: "Corpo", "Mascara", "Espirito" (transparente), "Brilho" (emissivo).
  No Unity o construtor do projeto troca por materiais PSX com as mesmas texturas.
* Rotações das poses são escritas em eixos do esqueleto (X = lado, Y = comprimento,
  Z = altura) e convertidas para o espaço de cada osso.
  Sinais úteis (o lobo olha para -Y): rotação NEGATIVA em X levanta o focinho e
  joga uma pata para a frente.
"""

import math
import os
import random
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

FPS = 24
NAME = "LoboEspiritual"


# =============================================================================
# 1. Utilidades de cena
# =============================================================================

def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = None
    if "--out" in argv:
        out = argv[argv.index("--out") + 1]
    if out is None:
        base = bpy.path.abspath("//") or bpy.app.tempdir
        out = os.path.join(base, NAME)
    return os.path.abspath(out), "--render" in argv, "--blend" in argv


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.unit_settings.system = "METRIC"
    return scene


# =============================================================================
# 2. Texturas pixeladas (geradas pixel a pixel, com pontilhado Bayer 4x4)
# =============================================================================

BAYER4 = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def bayer(x, y):
    return (BAYER4[y % 4][x % 4] + 0.5) / 16.0


def lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(len(a)))


def ramp(stops, t):
    """Gradiente por faixas: stops = [(t, cor), ...] em ordem crescente."""
    t = max(0.0, min(1.0, t))
    for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
        if t <= t1:
            return lerp(c0, c1, (t - t0) / max(1e-6, t1 - t0))
    return stops[-1][1]


def dithered_ramp(stops, t, x, y, steps=6):
    """Quantiza o gradiente em poucos tons e decide a faixa pelo pontilhado (visual de 15 bits)."""
    v = t * steps
    lo = math.floor(v)
    pick = lo + (1 if (v - lo) > bayer(x, y) else 0)
    return ramp(stops, pick / steps)


def make_image(name, w, h, pixel_fn):
    img = bpy.data.images.new(name, w, h, alpha=True)
    px = []
    for y in range(h):
        for x in range(w):
            px.extend(pixel_fn(x, y))
    img.pixels[:] = px
    return img


def texture_body():
    """Tronco: branco-azulado em cima -> azul-marinho/roxo nas patas, manchas de ferrugem nas costelas e ombros."""
    W, H = 64, 64
    stops = [
        (0.00, (0.10, 0.07, 0.18)),   # roxo das patas
        (0.30, (0.16, 0.18, 0.36)),   # azul-marinho
        (0.55, (0.47, 0.52, 0.80)),   # azul-lavanda
        (0.80, (0.80, 0.86, 0.97)),   # branco-azulado
        (1.00, (0.92, 0.95, 1.00)),
    ]
    rng = random.Random(11)
    spots = []
    for _ in range(14):  # manchas: u 0.2-0.7 (ombros/costelas), v 0.5-0.85
        spots.append((rng.uniform(0.22, 0.68), rng.uniform(0.52, 0.80), rng.uniform(0.04, 0.075)))

    def px(x, y):
        u, v = (x + 0.5) / W, (y + 0.5) / H
        c = dithered_ramp(stops, v, x, y)
        heat = 0.0
        for su, sv, sr in spots:
            d = math.hypot((u - su), (v - sv) * 1.2) / sr
            heat = max(heat, 1.0 - d)
        if heat > 0.0:
            if heat > 0.45:
                c = (0.58, 0.12, 0.11) if heat > 0.7 else (0.70, 0.22, 0.20)
            elif heat > bayer(x, y) * 0.45:
                c = lerp(c, (0.66, 0.28, 0.32), 0.6)   # borda pontilhada
        return (*c, 1.0)

    return make_image("Lobo_Corpo", W, H, px)


def texture_mask():
    """Máscara de crânio cor de osso: sombreado nas laterais, sulcos, ponta e focinho escuros."""
    W, H = 32, 32
    bone = [(0.0, (0.55, 0.50, 0.42)), (0.5, (0.86, 0.82, 0.70)), (1.0, (0.95, 0.92, 0.82))]

    def px(x, y):
        u, v = (x + 0.5) / W, (y + 0.5) / H
        side = 1.0 - abs(u - 0.5) * 2.0           # 1 no meio, 0 nas laterais
        c = dithered_ramp(bone, 0.25 + side * 0.75, x, y, steps=4)
        if 0.42 < v < 0.50 and abs(u - 0.5) > 0.12:  # sulco sobre os olhos
            c = (0.45, 0.40, 0.34)
        if x == W // 2 and v < 0.85:               # linha central
            c = tuple(k * 0.85 for k in c)
        if v > 0.9:                                # nariz
            c = (0.10, 0.08, 0.10)
        return (*c, 1.0)

    return make_image("Lobo_Mascara", W, H, px)


def texture_spirit():
    """Juba, cauda e espinhos: branco-gelo -> lavanda, pontas recortadas e semitransparentes."""
    W, H = 32, 64
    stops = [(0.0, (0.86, 0.93, 1.00)), (0.6, (0.80, 0.84, 1.00)), (1.0, (0.66, 0.64, 0.96))]
    rng = random.Random(5)
    cut = [rng.uniform(0.72, 1.0) for _ in range(W)]   # cada coluna termina numa altura

    def px(x, y):
        u, v = (x + 0.5) / W, (y + 0.5) / H
        c = dithered_ramp(stops, v, x, y, steps=5)
        if (x * 7 + y // 6) % 9 == 0:              # fios de pelo
            c = tuple(k * 0.88 for k in c)
        a = 1.0
        fade_from = cut[x] - 0.3
        if v > fade_from:
            t = (v - fade_from) / max(0.01, cut[x] - fade_from)
            a = 0.0 if t > bayer(x, y) else 0.85
        if v > cut[x]:
            a = 0.0
        return (*c, a)

    return make_image("Lobo_Espirito", W, H, px)


def texture_glow():
    """Olhos e lágrimas: amarelo-claro brilhante, a ponta (embaixo) some em pontilhado."""
    W, H = 8, 32

    def px(x, y):
        v = (y + 0.5) / H
        c = lerp((1.0, 0.78, 0.30), (1.0, 0.97, 0.70), v)
        a = 1.0 if v > 0.25 else (1.0 if v / 0.25 > bayer(x, y) else 0.0)
        return (*c, a)

    return make_image("Lobo_Brilho", W, H, px)


# =============================================================================
# 3. Materiais (texturas sem filtro, faces chapadas)
# =============================================================================

def make_material(name, image, transparent=False, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nodes, links = nt.nodes, nt.links
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"                    # pixel duro, sem filtro
    tex.location = (-400, 200)
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 1.0
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.0
    if transparent:
        links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = "DITHERED"
        else:
            mat.blend_method = "CLIP"
    if emission > 0.0:
        links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = emission
    mat.use_backface_culling = False
    return mat


# =============================================================================
# 4. Construtor de malha (UV por projeção, um osso por vértice)
# =============================================================================

class MeshBuilder:
    MATS = ["Corpo", "Mascara", "Espirito", "Brilho"]

    def __init__(self):
        self.verts, self.bones = [], []
        self.faces, self.face_mats, self.face_uvs = [], [], []

    def v(self, co, bone):
        self.verts.append(Vector(co))
        self.bones.append(bone)
        return len(self.verts) - 1

    def face(self, idx, mat, uvs=None):
        if uvs is None:
            uvs = [project_uv(mat, self.verts[i]) for i in idx]
        self.faces.append(list(idx))
        self.face_mats.append(self.MATS.index(mat))
        self.face_uvs.append(uvs)

    # ---- peças ----
    def loft(self, rings, mat, sides, axis, cap_start=True, cap_end=True, angle0=0.0, v_range=None):
        """rings = [(centro, raio_a, raio_b, osso)]; axis = 'Y' (corpo, cabeça, cauda) ou 'Z' (pernas)."""
        loops = []
        for ri, (c, ra, rb, bone) in enumerate(rings):
            c = Vector(c)
            ring = []
            for s in range(sides):
                a = angle0 + s * 2 * math.pi / sides
                if axis == "Y":
                    off = Vector((math.cos(a) * ra, 0.0, math.sin(a) * rb))
                else:
                    off = Vector((math.cos(a) * ra, math.sin(a) * rb, 0.0))
                ring.append(self.v(c + off, bone))
            loops.append(ring)
        for r in range(len(loops) - 1):
            for s in range(sides):
                a, b = loops[r][s], loops[r][(s + 1) % sides]
                c2, d = loops[r + 1][(s + 1) % sides], loops[r + 1][s]
                uvs = None
                if v_range:  # material Espirito: v sobe ao longo do comprimento
                    v0 = v_range[0] + (v_range[1] - v_range[0]) * r / (len(loops) - 1)
                    v1 = v_range[0] + (v_range[1] - v_range[0]) * (r + 1) / (len(loops) - 1)
                    u0, u1 = s / sides, (s + 1) / sides
                    uvs = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
                self.face([a, b, c2, d], mat, uvs)
        if cap_start:
            self.face(list(reversed(loops[0])), mat, [(0.5, (v_range or (0.2,))[0])] * sides if v_range else None)
        if cap_end:
            self.face(loops[-1], mat, [(0.5, (v_range or (0, 0.2))[1])] * sides if v_range else None)
        return loops

    def blade(self, base_a, base_b, tip, bone, width=0.0, mat="Espirito"):
        """Espinho/chama em losango (2 triângulos), v = 0 na base e 1 na ponta."""
        a, b, t = Vector(base_a), Vector(base_b), Vector(tip)
        mid = (a + b) * 0.5
        side = (b - a).normalized() * (width if width else (b - a).length * 0.6)
        m = mid + (t - mid) * 0.45
        ia, ib = self.v(a, bone), self.v(b, bone)
        il, ir = self.v(m - side, bone), self.v(m + side, bone)
        it = self.v(t, bone)
        self.face([ia, ib, ir], mat, [(0.4, 0.0), (0.6, 0.0), (0.9, 0.45)])
        self.face([ia, ir, it, il], mat, [(0.4, 0.0), (0.9, 0.45), (0.5, 1.0), (0.1, 0.45)])

    def strip(self, points, width, bones, mat, normal_axis):
        """Fio (lágrima): plano fino entre pontos; v = 1 no topo e ~0 na ponta."""
        n = len(points)
        left, right = [], []
        for i, p in enumerate(points):
            w = width * (1.0 - 0.6 * i / (n - 1))
            off = Vector(normal_axis) * w
            left.append(self.v(Vector(p) - off, bones[i]))
            right.append(self.v(Vector(p) + off, bones[i]))
        for i in range(n - 1):
            v0, v1 = 1.0 - i / (n - 1), 1.0 - (i + 1) / (n - 1)
            self.face([left[i], right[i], right[i + 1], left[i + 1]], mat,
                      [(0.2, v0), (0.8, v0), (0.8, v1), (0.2, v1)])

    def tri_count(self):
        return sum(len(f) - 2 for f in self.faces)

    def build(self, name, materials):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(v) for v in self.verts], [], self.faces)
        mesh.update()
        uv = mesh.uv_layers.new(name="UVMap")
        for poly, uvs, mi in zip(mesh.polygons, self.face_uvs, self.face_mats):
            poly.material_index = mi
            poly.use_smooth = False                    # faces chapadas
            for li, loop in enumerate(poly.loop_indices):
                uv.data[loop].uv = uvs[li]
        for m in materials:
            mesh.materials.append(m)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        for i, bone in enumerate(self.bones):
            vg = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
            vg.add([i], 1.0, "REPLACE")
        return obj


def project_uv(mat, co):
    """Projeções planas: corpo pelo lado (comprimento x altura); máscara de cima (largura x focinho)."""
    if mat == "Corpo":
        return ((co.y + 0.75) / 2.2, max(0.0, min(1.0, co.z / 1.05)))
    if mat == "Mascara":
        return (0.5 + co.x * 3.0, max(0.0, min(0.99, (-co.y - 0.60) / 0.40)))
    return (0.5, 0.5)


# =============================================================================
# 5. Geometria do lobo (olha para -Y)
# =============================================================================

def build_wolf_mesh():
    mb = MeshBuilder()

    # --- tronco esguio: peito fundo, cintura fina, quadril ---
    mb.loft([
        ((0, -0.52, 0.84), 0.10, 0.12, "Spine_2"),
        ((0, -0.36, 0.72), 0.16, 0.22, "Spine_2"),
        ((0, -0.10, 0.74), 0.14, 0.17, "Spine_1"),
        ((0, 0.16, 0.78), 0.10, 0.11, "Spine_1"),
        ((0, 0.38, 0.77), 0.12, 0.13, "Pelvis"),
        ((0, 0.54, 0.75), 0.07, 0.08, "Pelvis"),
    ], "Corpo", 6, "Y", angle0=math.pi / 2)

    # --- pescoço ---
    mb.loft([
        ((0, -0.50, 0.86), 0.10, 0.11, "Neck"),
        ((0, -0.62, 0.99), 0.08, 0.08, "Neck"),
    ], "Corpo", 6, "Y", cap_start=False, cap_end=False, angle0=math.pi / 2)

    # --- cabeça: máscara de crânio ---
    mb.loft([
        ((0, -0.60, 1.03), 0.10, 0.10, "Head"),
        ((0, -0.71, 1.05), 0.12, 0.10, "Head"),
        ((0, -0.86, 1.00), 0.07, 0.06, "Head"),
        ((0, -0.98, 0.97), 0.035, 0.035, "Head"),
    ], "Mascara", 6, "Y", angle0=math.pi / 2)
    # nariz
    tip = mb.v((0, -1.03, 0.975), "Head")
    base = [mb.v((x, -0.985, z), "Head") for x, z in ((-0.03, 0.975), (0, 1.0), (0.03, 0.975), (0, 0.955))]
    for i in range(4):
        mb.face([base[i], base[(i + 1) % 4], tip], "Mascara", [(0.5, 0.97)] * 3)

    # --- mandíbula (osso Jaw) ---
    j = [mb.v(p, "Jaw") for p in ((-0.06, -0.68, 0.95), (0.06, -0.68, 0.95), (0, -0.95, 0.94),
                                  (-0.04, -0.70, 0.90), (0.04, -0.70, 0.90), (0, -0.92, 0.915))]
    mb.face([j[0], j[1], j[2]], "Mascara")
    mb.face([j[5], j[4], j[3]], "Mascara")
    mb.face([j[0], j[3], j[4], j[1]], "Mascara")
    mb.face([j[1], j[4], j[5], j[2]], "Mascara")
    mb.face([j[2], j[5], j[3], j[0]], "Mascara")

    # --- orelhas longas, azul-escuras ---
    for side, bone in ((-1, "Ear_L"), (1, "Ear_R")):
        b = [mb.v((side * 0.05, -0.62, 1.10), bone), mb.v((side * 0.11, -0.64, 1.08), bone),
             mb.v((side * 0.08, -0.58, 1.09), bone)]
        t = mb.v((side * 0.15, -0.58, 1.34), bone)
        dark = [(0.3, 0.12)] * 3
        mb.face([b[0], b[1], t], "Corpo", dark)
        mb.face([b[1], b[2], t], "Corpo", dark)
        mb.face([b[2], b[0], t], "Corpo", dark)
        mb.face([b[0], b[2], b[1]], "Corpo", dark)

    # --- olhos brilhantes ---
    for side in (-1, 1):
        cx = side * 0.06
        q = [mb.v((cx - 0.022, -0.795, 1.035), "Head"), mb.v((cx + 0.022, -0.795, 1.035), "Head"),
             mb.v((cx + 0.022, -0.80, 1.062), "Head"), mb.v((cx - 0.022, -0.80, 1.062), "Head")]
        mb.face(q, "Brilho", [(0.2, 0.95), (0.8, 0.95), (0.8, 1.0), (0.2, 1.0)])

    # --- lágrimas luminosas: dos olhos até perto do chão (dois planos cruzados cada) ---
    for side, s in ((-1, "L"), (1, "R")):
        x = side * 0.06
        pts = [(x, -0.80, 1.03), (x, -0.81, 0.74), (x, -0.80, 0.43), (x, -0.79, 0.12)]
        bones = ["Head", f"Tear_{s}_1", f"Tear_{s}_2", f"Tear_{s}_2"]
        mb.strip(pts, 0.011, bones, "Brilho", (1, 0, 0))
        mb.strip(pts, 0.011, bones, "Brilho", (0, 1, 0))

    # --- pernas finas (digitígradas) com garras ---
    def leg(x, rings, claw_y, prefix):
        loops = mb.loft(rings, "Corpo", 4, "Z", cap_start=True, cap_end=True, angle0=math.pi / 4)
        paw = rings[-1][0]
        t = mb.v((paw[0], claw_y, 0.0), f"{prefix}_Paw")
        last = loops[-1]
        mb.face([last[0], last[1], t], "Corpo", [(0.5, 0.02)] * 3)
        mb.face([last[1], last[2], t], "Corpo", [(0.5, 0.02)] * 3)

    for side, s in ((-1, "L"), (1, "R")):
        x = side * 0.10
        leg(x, [((x, -0.34, 0.70), 0.06, 0.08, f"Front_{s}_Upper"),
                ((x, -0.36, 0.44), 0.035, 0.045, f"Front_{s}_Lower"),
                ((x, -0.35, 0.12), 0.03, 0.035, f"Front_{s}_Lower"),
                ((x, -0.39, 0.03), 0.045, 0.06, f"Front_{s}_Paw")], -0.50, f"Front_{s}")
        leg(x, [((x, 0.40, 0.72), 0.07, 0.10, f"Back_{s}_Upper"),
                ((x, 0.30, 0.45), 0.045, 0.05, f"Back_{s}_Lower"),
                ((x, 0.46, 0.22), 0.035, 0.04, f"Back_{s}_Lower"),
                ((x, 0.42, 0.03), 0.045, 0.06, f"Back_{s}_Paw")], 0.30, f"Back_{s}")

    # --- cauda longa e flutuante ---
    tail = [((0, 0.54, 0.76), 0.05, 0.05, "Tail_1"),
            ((0, 0.80, 0.64), 0.07, 0.07, "Tail_1"),
            ((0, 1.06, 0.48), 0.08, 0.08, "Tail_2"),
            ((0, 1.30, 0.34), 0.03, 0.03, "Tail_3")]
    mb.loft(tail, "Espirito", 4, "Y", cap_start=False, cap_end=True, v_range=(0.05, 0.5))
    fronds = [((0, 0.80, 0.64), "Tail_1"), ((0, 1.06, 0.48), "Tail_2"), ((0, 1.30, 0.34), "Tail_3")]
    for i, (c, bone) in enumerate(fronds):
        c = Vector(c)
        for dx, dz in ((0.09, 0.06), (-0.09, 0.06), (0.0, 0.11)):
            tipp = c + Vector((dx * 2.2, 0.30 + i * 0.04, dz - 0.10 - i * 0.05))
            mb.blade(c + Vector((-0.04, -0.02, 0)), c + Vector((0.04, 0.02, 0)), tipp, bone, 0.05)

    # --- juba volumosa e espetada em volta do pescoço ---
    rng = random.Random(3)
    neck_c = Vector((0, -0.55, 0.90))
    for k in range(20):
        a = math.radians(-70 + k * (320 / 19))          # quase a volta toda, menos embaixo
        d = Vector((math.cos(a), 0.0, math.sin(a)))
        if abs(math.degrees(a) - 90) < 50:
            bone = "Mane_Top"
        else:
            bone = "Mane_L" if d.x < 0 else "Mane_R"
        base_c = neck_c + d * 0.10
        length = rng.uniform(0.24, 0.38)
        tipp = base_c + d * length + Vector((0, 0.10 + rng.uniform(0, 0.12), 0.04))
        side = Vector((-d.z, 0, d.x)) * 0.05
        mb.blade(base_c - side + Vector((0, 0.06, 0)), base_c + side, tipp, bone, 0.06)

    # peito: tufo apontando para baixo
    for k in range(4):
        x = -0.09 + k * 0.06
        mb.blade((x - 0.04, -0.48, 0.66), (x + 0.04, -0.48, 0.66), (x * 1.2, -0.52, 0.38 + 0.03 * (k % 2)), "Mane_Chest", 0.05)

    # chamas no alto da cabeça
    for k, x in enumerate((-0.05, 0.0, 0.05)):
        mb.blade((x - 0.03, -0.66, 1.12), (x + 0.03, -0.64, 1.12), (x * 2.0, -0.55, 1.40 + 0.06 * (k == 1)), "Crown", 0.04)

    # espinhos dorsais ao longo da coluna
    for k in range(7):
        y = -0.30 + k * 0.13
        bone = "Dorsal_1" if y < 0.12 else "Dorsal_2"
        h = 0.30 - k * 0.025
        mb.blade((-0.03, y - 0.04, 0.90 - k * 0.012), (0.03, y + 0.04, 0.90 - k * 0.012), (0.0, y + 0.18, 0.90 + h), bone, 0.05)

    return mb


# =============================================================================
# 6. Esqueleto
# =============================================================================

# nome: (cabeça, cauda, pai)
BONES = {
    "Root": ((0, 0, 0), (0, -0.25, 0), None),
    "Pelvis": ((0, 0.40, 0.77), (0, 0.10, 0.77), "Root"),
    "Spine_1": ((0, 0.10, 0.77), (0, -0.25, 0.76), "Pelvis"),
    "Spine_2": ((0, -0.25, 0.76), (0, -0.50, 0.84), "Spine_1"),
    "Neck": ((0, -0.50, 0.84), (0, -0.62, 1.00), "Spine_2"),
    "Head": ((0, -0.62, 1.00), (0, -0.95, 1.00), "Neck"),
    "Jaw": ((0, -0.68, 0.94), (0, -0.93, 0.92), "Head"),
    "Ear_L": ((-0.08, -0.62, 1.09), (-0.15, -0.58, 1.32), "Head"),
    "Ear_R": ((0.08, -0.62, 1.09), (0.15, -0.58, 1.32), "Head"),
    "Crown": ((0, -0.65, 1.12), (0, -0.56, 1.38), "Head"),
    "Tear_L_1": ((-0.06, -0.81, 0.86), (-0.06, -0.81, 0.58), "Head"),
    "Tear_L_2": ((-0.06, -0.81, 0.58), (-0.06, -0.79, 0.12), "Tear_L_1"),
    "Tear_R_1": ((0.06, -0.81, 0.86), (0.06, -0.81, 0.58), "Head"),
    "Tear_R_2": ((0.06, -0.81, 0.58), (0.06, -0.79, 0.12), "Tear_R_1"),
    "Mane_Top": ((0, -0.55, 0.98), (0, -0.45, 1.25), "Neck"),
    "Mane_L": ((-0.08, -0.55, 0.90), (-0.35, -0.45, 0.92), "Neck"),
    "Mane_R": ((0.08, -0.55, 0.90), (0.35, -0.45, 0.92), "Neck"),
    "Mane_Chest": ((0, -0.48, 0.68), (0, -0.52, 0.40), "Spine_2"),
    "Dorsal_1": ((0, -0.10, 0.90), (0, 0.05, 1.15), "Spine_1"),
    "Dorsal_2": ((0, 0.25, 0.88), (0, 0.40, 1.10), "Pelvis"),
    "Tail_1": ((0, 0.54, 0.76), (0, 0.80, 0.64), "Pelvis"),
    "Tail_2": ((0, 0.80, 0.64), (0, 1.06, 0.48), "Tail_1"),
    "Tail_3": ((0, 1.06, 0.48), (0, 1.30, 0.34), "Tail_2"),
}
for _s, _x in (("L", -0.10), ("R", 0.10)):
    BONES.update({
        f"Front_{_s}_Upper": ((_x, -0.34, 0.72), (_x, -0.36, 0.44), "Spine_2"),
        f"Front_{_s}_Lower": ((_x, -0.36, 0.44), (_x, -0.35, 0.10), f"Front_{_s}_Upper"),
        f"Front_{_s}_Paw": ((_x, -0.35, 0.10), (_x, -0.48, 0.02), f"Front_{_s}_Lower"),
        f"Back_{_s}_Upper": ((_x, 0.40, 0.74), (_x, 0.30, 0.45), "Pelvis"),
        f"Back_{_s}_Lower": ((_x, 0.30, 0.45), (_x, 0.46, 0.20), f"Back_{_s}_Upper"),
        f"Back_{_s}_Paw": ((_x, 0.46, 0.20), (_x, 0.36, 0.02), f"Back_{_s}_Lower"),
    })


def build_armature():
    arm_data = bpy.data.armatures.new(NAME + "_Rig")
    arm = bpy.data.objects.new(NAME, arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (head, tail, _) in BONES.items():
        eb = arm_data.edit_bones.new(name)
        eb.head, eb.tail = Vector(head), Vector(tail)
        eb.roll = 0.0
    for name, (_, _, parent) in BONES.items():
        if parent:
            arm_data.edit_bones[name].parent = arm_data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return arm


def skin(mesh_obj, arm):
    mesh_obj.parent = arm
    mod = mesh_obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm


# =============================================================================
# 7. Animações
# =============================================================================

class Animator:
    """Cria uma Action e grava poses com rotações em eixos do esqueleto (graus)."""

    def __init__(self, arm):
        self.arm = arm
        self.rest = {b.name: b.matrix_local.to_quaternion() for b in arm.data.bones}

    def begin(self, name, length, loop):
        if self.arm.animation_data is None:
            self.arm.animation_data_create()
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        self.arm.animation_data.action = action
        self.action, self.length, self.loop = action, length, loop
        self.keys = {}   # (bone, frame) -> dict
        return action

    def key(self, frame, bone, rot=None, loc=None, scale=None):
        self.keys.setdefault((bone, frame), {}).update(
            {k: v for k, v in (("rot", rot), ("loc", loc), ("scale", scale)) if v is not None})

    def wave(self, bone, axis, amplitude, period, phase=0.0, step=None, base=(0, 0, 0)):
        """Oscilação em loop (cauda, juba): grava a cada `step` quadros."""
        step = step or max(2, period // 8)
        for f in range(0, self.length + 1, step):
            a = amplitude * math.sin(2 * math.pi * (f / period) + phase)
            r = list(base)
            r["XYZ".index(axis)] += a
            self.key(f, bone, rot=tuple(r))

    def _local_rot(self, bone, euler_deg):
        q_arm = Quaternion((1, 0, 0), 0)
        for axis, deg in zip("XYZ", euler_deg):
            if deg:
                q_arm = Quaternion(Vector(tuple(1.0 if a == axis else 0.0 for a in "XYZ")), math.radians(deg)) @ q_arm
        b = self.rest[bone]
        return b.inverted() @ q_arm @ b

    def _local_vec(self, bone, v):
        return self.rest[bone].inverted() @ Vector(v)

    def commit(self):
        """Grava os quadros; bones sem chave ficam em repouso. Loops repetem o quadro 0 no fim."""
        used = {b for b, _ in self.keys}
        frames_of = {}
        for (b, f) in self.keys:
            frames_of.setdefault(b, set()).add(f)
        for b in used:
            channels = set()
            for f in frames_of[b]:
                channels.update(self.keys[(b, f)].keys())
            start = self.keys.setdefault((b, 0), {})
            start.setdefault("rot", (0, 0, 0))            # canais sem chave no 0 começam em repouso
            if "loc" in channels:
                start.setdefault("loc", (0, 0, 0))
            if "scale" in channels:
                start.setdefault("scale", 1.0)
            if self.loop:
                k0 = self.keys[(b, 0)]
                self.keys.setdefault((b, self.length), {}).update(k0)
        for (b, f), k in sorted(self.keys.items(), key=lambda i: i[0][1]):
            pb = self.arm.pose.bones[b]
            pb.rotation_quaternion = self._local_rot(b, k.get("rot", (0, 0, 0)))
            pb.keyframe_insert("rotation_quaternion", frame=f)
            if "loc" in k:
                pb.location = self._local_vec(b, k["loc"])
                pb.keyframe_insert("location", frame=f)
            if "scale" in k:
                s = k["scale"]
                pb.scale = (s, s, s) if isinstance(s, (int, float)) else s
                pb.keyframe_insert("scale", frame=f)
        # tangentes lineares: movimento direto, "travado", de jogo antigo
        for fc in iter_fcurves(self.action):
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
        self.action.frame_range = (0, self.length)
        for pb in self.arm.pose.bones:   # volta ao repouso para a próxima Action
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.location = (0, 0, 0)
            pb.scale = (1, 1, 1)


def iter_fcurves(action):
    """F-Curves de uma Action (formato antigo ou em camadas do Blender 4.4+)."""
    if hasattr(action, "layers") and len(action.layers):
        for layer in action.layers:
            for strip in layer.strips:
                for bag in getattr(strip, "channelbags", []):
                    yield from bag.fcurves
    elif hasattr(action, "fcurves"):
        yield from action.fcurves


LEGS = ["Front_L", "Front_R", "Back_L", "Back_R"]
SPIRIT_BONES = ["Mane_Top", "Mane_L", "Mane_R", "Mane_Chest", "Crown", "Dorsal_1", "Dorsal_2"]
TEARS = ["Tear_L_1", "Tear_R_1", "Tear_L_2", "Tear_R_2"]


def anim_idle(an):
    """Respiração lenta, cabeça balançando, cauda e espinhos ondulando, lágrimas pulsando."""
    L = 48
    an.begin("Idle", L, True)
    for f, s in ((0, 1.0), (24, 1.04), (48, 1.0)):
        an.key(f, "Spine_2", rot=(-1.5 * (s - 1) / 0.04, 0, 0), scale=(s, 1.0, s))   # tórax expande
        an.key(f, "Head", rot=(2.0 * (s - 1) / 0.04, 0, 0))
    an.wave("Tail_1", "Z", 6, L, 0.0, 6)
    an.wave("Tail_2", "Z", 9, L, 0.8, 6)
    an.wave("Tail_3", "Z", 12, L, 1.6, 6)
    for i, b in enumerate(SPIRIT_BONES):
        an.wave(b, "Y", 4, L // 2 if i % 2 else L, i * 0.7, 6)
    for i, b in enumerate(TEARS):
        for f in range(0, L + 1, 12):
            an.key(f, b, scale=(1.0, 1.0 + 0.06 * math.sin(2 * math.pi * f / 24 + i), 1.0))
    an.commit()


def gait(an, name, L, swing, lift, bob, phases, head_pitch, spine_flex):
    an.begin(name, L, True)
    step = max(2, L // 8)
    for f in range(0, L + 1, step):
        t = 2 * math.pi * f / L
        for leg, ph in zip(LEGS, phases):
            s = math.sin(t + ph)
            an.key(f, f"{leg}_Upper", rot=(-swing * s, 0, 0))
            bend = lift * max(0.0, math.cos(t + ph))
            an.key(f, f"{leg}_Lower", rot=((bend if leg.startswith("Front") else -bend), 0, 0))
            an.key(f, f"{leg}_Paw", rot=(-0.5 * bend if leg.startswith("Front") else 0.5 * bend, 0, 0))
        an.key(f, "Root", loc=(0, 0, bob * abs(math.sin(t))))
        an.key(f, "Spine_1", rot=(spine_flex * math.sin(t), 0, 0))
        an.key(f, "Head", rot=(head_pitch + 3 * math.sin(2 * t), 0, 0))
        an.key(f, "Tail_1", rot=(-8, 0, 10 * math.sin(t)))
        an.key(f, "Tail_2", rot=(0, 0, 14 * math.sin(t - 0.6)))
        an.key(f, "Tail_3", rot=(0, 0, 18 * math.sin(t - 1.2)))
        for i, b in enumerate(SPIRIT_BONES):
            an.key(f, b, rot=(6 * math.sin(t + i), 0, 0))
    an.commit()


def anim_walk(an):
    gait(an, "Walk", 32, swing=22, lift=30, bob=0.015, phases=(0, math.pi, math.pi * 0.5, math.pi * 1.5),
         head_pitch=4, spine_flex=2)


def anim_run(an):
    # galope: dianteiras juntas, traseiras juntas, coluna flexionando
    gait(an, "Run", 16, swing=38, lift=55, bob=0.06, phases=(0, 0.35, math.pi, math.pi + 0.35),
         head_pitch=12, spine_flex=9)


def anim_attack(an):
    """Recua flexionando as traseiras, dá o bote com a boca aberta e as garras à frente."""
    an.begin("Attack", 24, False)
    an.key(0, "Root", loc=(0, 0, 0))
    # recuo (quadro 7)
    an.key(7, "Root", loc=(0, 0.12, -0.10))
    an.key(7, "Pelvis", rot=(10, 0, 0))
    for s in "LR":
        an.key(7, f"Back_{s}_Upper", rot=(-25, 0, 0))
        an.key(7, f"Back_{s}_Lower", rot=(-35, 0, 0))
        an.key(7, f"Front_{s}_Upper", rot=(15, 0, 0))
    an.key(7, "Head", rot=(14, 0, 0))
    an.key(7, "Ear_L", rot=(20, 0, 0))
    an.key(7, "Ear_R", rot=(20, 0, 0))
    # bote (quadro 12)
    an.key(12, "Root", loc=(0, -0.45, 0.10))
    an.key(12, "Pelvis", rot=(-6, 0, 0))
    an.key(12, "Head", rot=(-10, 0, 0))
    an.key(12, "Jaw", rot=(35, 0, 0))
    for s in "LR":
        an.key(12, f"Front_{s}_Upper", rot=(-55, 0, 0))
        an.key(12, f"Front_{s}_Lower", rot=(-20, 0, 0))
        an.key(12, f"Back_{s}_Upper", rot=(35, 0, 0))
        an.key(12, f"Back_{s}_Lower", rot=(0, 0, 0))
    for b in SPIRIT_BONES:
        an.key(12, b, scale=1.15)
    # mordida fecha (quadro 15) e volta
    an.key(15, "Jaw", rot=(0, 0, 0))
    an.key(15, "Root", loc=(0, -0.40, 0.0))
    for b in SPIRIT_BONES:
        an.key(24, b, scale=1.0)
    for b in ("Root",):
        an.key(24, b, loc=(0, 0, 0))
    for b in ["Pelvis", "Head", "Jaw", "Ear_L", "Ear_R"] + [f"{l}_{p}" for l in LEGS for p in ("Upper", "Lower")]:
        an.key(24, b, rot=(0, 0, 0))
    an.commit()


def anim_hurt(an):
    """Impacto brusco: cabeça e tronco para trás, patas recuam, orelhas baixas, espinhos tremem."""
    an.begin("Hurt", 12, False)
    an.key(3, "Root", loc=(0, 0.14, 0.02))
    an.key(3, "Spine_2", rot=(-12, 0, 4))
    an.key(3, "Head", rot=(-22, 0, 8))
    an.key(3, "Ear_L", rot=(45, 0, 0))
    an.key(3, "Ear_R", rot=(45, 0, 0))
    for s in "LR":
        an.key(3, f"Front_{s}_Upper", rot=(18, 0, 0))
    for f, sc in ((2, 0.85), (4, 1.15), (6, 0.9), (8, 1.05), (12, 1.0)):
        for b in SPIRIT_BONES:
            an.key(f, b, scale=sc)
    for b in ["Root"]:
        an.key(12, b, loc=(0, 0, 0))
    for b in ("Spine_2", "Head", "Ear_L", "Ear_R", "Front_L_Upper", "Front_R_Upper"):
        an.key(12, b, rot=(0, 0, 0))
    an.commit()


def anim_death(an):
    """Pernas cedem, o corpo tomba de lado, a cabeça cai sem força e o espírito se dissipa."""
    an.begin("Death", 40, False)
    an.key(0, "Root", loc=(0, 0, 0))
    for s in "LR":
        an.key(10, f"Front_{s}_Upper", rot=(-30, 0, 0))
        an.key(10, f"Front_{s}_Lower", rot=(70, 0, 0))
        an.key(10, f"Back_{s}_Upper", rot=(30, 0, 0))
        an.key(10, f"Back_{s}_Lower", rot=(-60, 0, 0))
    an.key(10, "Root", loc=(0, 0, -0.30))
    an.key(10, "Head", rot=(18, 0, 0))
    # tomba de lado (gira no eixo do comprimento)
    an.key(22, "Root", rot=(0, 85, 0), loc=(0, 0, 0.10))
    an.key(28, "Head", rot=(30, 0, -25))
    an.key(28, "Neck", rot=(10, 0, -15))
    an.key(28, "Jaw", rot=(18, 0, 0))
    for s in "LR":
        an.key(28, f"Front_{s}_Upper", rot=(-45, 0, 0))
        an.key(28, f"Back_{s}_Upper", rot=(40, 0, 0))
    an.key(28, "Tail_1", rot=(20, 0, 0))
    an.key(28, "Tail_2", rot=(15, 0, 0))
    # dissipação dos espinhos e das lágrimas
    for b in SPIRIT_BONES + TEARS:
        an.key(12, b, scale=1.0)
        an.key(40, b, scale=0.01)
    an.key(40, "Root", rot=(0, 85, 0), loc=(0, 0, 0.10))
    an.commit()


def anim_summon(an):
    """Surge do chão (escala 0), salta, uiva em silêncio com a juba expandida e assenta no Idle."""
    an.begin("Summon", 40, False)
    an.key(0, "Root", loc=(0, 0, -0.6), scale=0.01)
    an.key(8, "Root", loc=(0, 0, -0.1), scale=0.8)
    an.key(14, "Root", loc=(0, 0, 0.30), scale=1.08)
    for s in "LR":
        an.key(14, f"Front_{s}_Upper", rot=(-30, 0, 0))
        an.key(14, f"Back_{s}_Upper", rot=(30, 0, 0))
    an.key(18, "Root", loc=(0, 0, 0.0), scale=1.0)
    for s in "LR":
        an.key(18, f"Front_{s}_Upper", rot=(0, 0, 0))
        an.key(18, f"Back_{s}_Upper", rot=(0, 0, 0))
    # uivo para o alto (quadros 20-32)
    for f in (22, 30):
        an.key(f, "Spine_2", rot=(-10, 0, 0))
        an.key(f, "Neck", rot=(-30, 0, 0))
        an.key(f, "Head", rot=(-30, 0, 0))
        an.key(f, "Jaw", rot=(28, 0, 0))
        for b in SPIRIT_BONES:
            an.key(f, b, scale=1.35)
    for b in SPIRIT_BONES + TEARS:
        an.key(0, b, scale=0.01)
        an.key(10, b, scale=0.6)
    # assenta na pose de repouso
    for b in ("Spine_2", "Neck", "Head", "Jaw"):
        an.key(40, b, rot=(0, 0, 0))
    for b in SPIRIT_BONES + TEARS:
        an.key(40, b, scale=1.0)
    an.key(40, "Root", loc=(0, 0, 0), scale=1.0)
    an.commit()


ANIMATIONS = [anim_idle, anim_walk, anim_run, anim_attack, anim_hurt, anim_death, anim_summon]


# =============================================================================
# 8. Luz de estúdio e prévia
# =============================================================================

def setup_studio(scene):
    world = bpy.data.worlds.new("Estudio")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.035, 0.035, 0.045, 1)
    bg.inputs["Strength"].default_value = 0.6
    scene.world = world

    def light(name, kind, loc, rot, energy, color, size=1.0):
        data = bpy.data.lights.new(name, kind)
        data.energy, data.color = energy, color
        if kind == "AREA":
            data.size = size
        obj = bpy.data.objects.new(name, data)
        obj.location, obj.rotation_euler = loc, [math.radians(a) for a in rot]
        scene.collection.objects.link(obj)

    light("Luz Principal", "AREA", (-2.5, -3.0, 3.0), (55, 0, -40), 350, (1.0, 0.96, 0.9), 2.0)
    light("Contraluz Azul", "AREA", (2.0, 2.5, 2.2), (-60, 0, 140), 260, (0.55, 0.65, 1.0), 1.5)
    light("Preenchimento", "AREA", (3.0, -2.0, 0.8), (80, 0, 60), 60, (0.8, 0.8, 1.0), 2.0)

    cam_data = bpy.data.cameras.new("Camera")
    cam_data.lens = 45
    cam = bpy.data.objects.new("Camera", cam_data)
    cam.location = (-3.2, -3.4, 1.6)
    scene.collection.objects.link(cam)
    target = bpy.data.objects.new("AlvoCamera", None)
    target.location = (0, 0, 0.6)
    scene.collection.objects.link(target)
    c = cam.constraints.new("TRACK_TO")
    c.target, c.track_axis, c.up_axis = target, "TRACK_NEGATIVE_Z", "UP_Y"
    scene.camera = cam

    floor = bpy.data.meshes.new("Chao")
    floor.from_pydata([(-3, -3, 0), (3, -3, 0), (3, 3, 0), (-3, 3, 0)], [], [(0, 1, 2, 3)])
    fobj = bpy.data.objects.new("Chao", floor)
    scene.collection.objects.link(fobj)
    fm = bpy.data.materials.new("Chao")
    fm.use_nodes = True
    fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.05, 0.06, 1)
    floor.materials.append(fm)
    return fobj


def render_previews(scene, arm, out_dir):
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 480, 360
    scene.render.film_transparent = False
    for action in [a for a in bpy.data.actions]:
        arm.animation_data.action = action
        start, end = (int(f) for f in action.frame_range)
        for i, f in enumerate((start, (start + end) // 2, end)):
            scene.frame_set(f)
            scene.render.filepath = os.path.join(out_dir, "previa", f"{action.name}_{i}.png")
            bpy.ops.render.render(write_still=True)


# =============================================================================
# 9. Exportação
# =============================================================================

def export(out_dir, arm, mesh_obj, images, floor):
    os.makedirs(out_dir, exist_ok=True)
    for img in images:
        img.filepath_raw = os.path.join(out_dir, img.name + ".png")
        img.file_format = "PNG"
        img.save()
    arm.animation_data.action = None
    for obj in bpy.context.scene.objects:
        obj.select_set(obj in (arm, mesh_obj))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out_dir, NAME + ".fbx"),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=False,
        mesh_smooth_type="FACE",
        use_triangles=True,
        add_leaf_bones=False,
        primary_bone_axis="Y", secondary_bone_axis="X",
        armature_nodetype="NULL",
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="STRIP",
    )


# =============================================================================
# 10. Execução
# =============================================================================

def main():
    out_dir, do_render, save_blend = parse_args()
    scene = reset_scene()

    images = [texture_body(), texture_mask(), texture_spirit(), texture_glow()]
    mats = [
        make_material("Corpo", images[0]),
        make_material("Mascara", images[1]),
        make_material("Espirito", images[2], transparent=True),
        make_material("Brilho", images[3], transparent=True, emission=6.0),
    ]

    mb = build_wolf_mesh()
    mesh_obj = mb.build(NAME + "_Malha", mats)
    arm = build_armature()
    skin(mesh_obj, arm)

    an = Animator(arm)
    for make in ANIMATIONS:
        make(an)

    floor = setup_studio(scene)
    scene.frame_start, scene.frame_end = 0, 48
    print(f"[Lobo Espiritual] {mb.tri_count()} triângulos, {len(BONES)} ossos, "
          f"{len(bpy.data.actions)} animações: {', '.join(a.name for a in bpy.data.actions)}")

    export(out_dir, arm, mesh_obj, images, floor)
    if do_render:
        render_previews(scene, arm, out_dir)
    if save_blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, NAME + ".blend"))
    print("[Lobo Espiritual] exportado em", out_dir)


if __name__ == "__main__":
    main()
