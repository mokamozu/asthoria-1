"""
ATHORIA — Espírito do Musgo (criatura que solta Musgo)
======================================================

Recria em 3D a criatura da imagem de referência (5 vistas), que é a fonte da verdade:

* A silhueta do manto de musgo, das pernas/botas e a posição dos braços, orelhas, olhos e
  galhos são MEDIDAS na imagem (frente, lado e costas), não inventadas.
* A pintura é a própria referência, projetada ortogonalmente: cada face recebe a cor da
  vista para a qual está virada (frente / costas / lado). A barra rasgada e a fenda das
  pernas são recortadas onde a imagem não tem musgo.
* Corpo, braços, mãos de dedos longos, orelhas, olhos com brilho, broto e galhos com
  folhas são geometria de verdade.
* Esqueleto com pesos rígidos e 4 Actions: Idle (1-48), Hiding (1-40), Running (1-20), Death (1-36).

Precisa do arquivo espirito_musgo_referencia.png na mesma pasta do script.

Como usar
---------
* Blender 4.0+ (testado no 5.3): aba Scripting > Abrir > Run Script.
  Tudo vai para a Collection "PS1_Moss_Spirit".
* Terminal, exportando FBX + texturas para o Unity:
      blender --background --python espirito_musgo.py -- --out ./EspiritoMusgo [--render] [--blend]

Convenções: metros, Z para cima, origem no chão, olhando para -Y; esquerda = +X (_L).
"""

import math
import os
import random
import sys

import bpy
import numpy as np
from mathutils import Quaternion, Vector

FPS = 24
NAME = "EspiritoMusgo"
COLLECTION = "PS1_Moss_Spirit"


# =============================================================================
# 1. Cena
# =============================================================================

def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[argv.index("--out") + 1] if "--out" in argv else None
    if out is None:
        base = bpy.path.abspath("//") or bpy.app.tempdir
        out = os.path.join(base, NAME)
    return os.path.abspath(out), "--render" in argv, "--blend" in argv


def clean_scene():
    """Remove o cubo/luz/câmera padrão e qualquer geração anterior (sem resetar a interface)."""
    scene = bpy.context.scene
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    doomed = [o for o in bpy.data.objects if o.name.split(".")[0] in ("Cube", "Light", "Camera")]
    old = bpy.data.collections.get(COLLECTION)
    if old:
        doomed += list(old.all_objects)
    for o in set(doomed):
        bpy.data.objects.remove(o, do_unlink=True)
    if old:
        bpy.data.collections.remove(old)
    for block in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images,
                  bpy.data.actions, bpy.data.lights, bpy.data.cameras):
        for item in list(block):
            if item.users == 0 or (block is bpy.data.actions and item.name in ACTION_NAMES):
                block.remove(item)
    coll = bpy.data.collections.new(COLLECTION)
    scene.collection.children.link(coll)
    scene.render.fps = FPS
    scene.unit_settings.system = "METRIC"
    return scene, coll


def link(obj, coll):
    coll.objects.link(obj)
    return obj


# =============================================================================
# 2. Referência: a imagem de 5 vistas é a fonte da verdade
# =============================================================================
#
# O modelo é reconstruído a partir das silhuetas medidas na imagem (frente, lado e
# costas) e pintado com ela mesma, por projeção ortográfica: cada face recebe a cor
# da vista para a qual está virada. Assim o musgo pintado à mão, a sombra, as folhas
# e o rosto ficam exatamente como na referência.

REF_FILE = "espirito_musgo_referencia.png"
HEIGHT = 1.80                     # do chão ao topo da cabeça (sem o broto), em metros

# Vistas da imagem (pixels): centro horizontal do corpo, linha da sola, topo da cabeça.
# sign = +1 se o eixo do mundo cresce para a direita da imagem.
VIEWS = {
    "front": dict(cx=192.0, sole=619.0, top=39.0, axis=0, sign=1.0, x0=0, x1=385),
    "side": dict(cx=463.0, sole=612.0, top=43.0, axis=1, sign=1.0, x0=385, x1=585),
    "back": dict(cx=198.0, sole=1231.0, top=670.0, axis=0, sign=-1.0, x0=0, x1=420),
}
BG = (41, 50, 52)
FLOOR = (84, 112, 123)


def vscale(view):
    v = VIEWS[view]
    return HEIGHT / (v["sole"] - v["top"])


def to_px(view, p):
    v = VIEWS[view]
    s = vscale(view)
    return v["cx"] + v["sign"] * p[v["axis"]] / s, v["sole"] - p[2] / s


def from_px(view, px, py):
    """(coordenada horizontal no mundo, z) de um pixel da vista."""
    v = VIEWS[view]
    s = vscale(view)
    return (px - v["cx"]) * v["sign"] * s, (v["sole"] - py) * s


def find_reference():
    here = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else ""
    for base in (here, bpy.path.abspath("//"), os.getcwd()):
        p = os.path.join(base, REF_FILE)
        if base and os.path.exists(p):
            return p
    raise FileNotFoundError(f"Coloque {REF_FILE} ao lado deste script.")


class Reference:
    """Imagem de referência em numpy (linha 0 = topo) + máscaras de musgo/corpo/personagem."""

    def __init__(self, path):
        img = bpy.data.images.load(path, check_existing=True)
        w, h = img.size
        px = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        self.rgba = px.reshape(h, w, 4)[::-1].copy()      # Blender guarda de baixo para cima
        bpy.data.images.remove(img)
        self.w, self.h = w, h
        c = self.rgba[..., :3] * 255.0
        r, g, b = c[..., 0], c[..., 1], c[..., 2]
        d_bg = np.abs(c - np.array(BG)).sum(2)
        d_fl = np.abs(c - np.array(FLOOR)).sum(2)
        self.char = (d_bg > 22) & (d_fl > 40)
        self.moss = self.char & (g - r > 22) & (g >= b - 6)
        self.body = self.char & ~self.moss
        self.dark = self.char & (c.sum(2) < 110)          # sombra funda/corpo: vãos entre as mechas
        # versão "engordada" (1 px) para não abrir buracos por causa do pontilhado
        self.char_d = self.char.copy()
        self.moss_d = self.moss.copy()
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (-1, -1), (1, -1), (-1, 1)):
            self.char_d |= np.roll(np.roll(self.char, dy, 0), dx, 1)
            self.moss_d |= np.roll(np.roll(self.moss, dy, 0), dx, 1)

    def at(self, mask, px, py):
        x, y = int(round(px)), int(round(py))
        if 0 <= x < self.w and 0 <= y < self.h:
            return bool(mask[y, x])
        return False

    def extents(self, view, mask, py, band=4, lo=3, hi=97, xmin=None, xmax=None):
        v = VIEWS[view]
        x0, x1 = xmin if xmin is not None else v["x0"], xmax if xmax is not None else v["x1"]
        y0, y1 = int(py - band), int(py + band) + 1
        ys, xs = np.nonzero(mask[y0:y1, x0:x1])
        if len(xs) < 6:
            return None
        return np.percentile(xs, lo) + x0, np.percentile(xs, hi) + x0

    def runs(self, mask, py, x0, x1):
        row = mask[int(py), x0:x1]
        xs = np.nonzero(row)[0]
        out = []
        if len(xs) == 0:
            return out
        s = p = xs[0]
        for x in xs[1:]:
            if x > p + 2:
                out.append((s + x0, p + x0))
                s = x
            p = x
        out.append((s + x0, p + x0))
        return out

    def texture(self, name):
        """
        Textura = a própria referência, com o fundo e o chão trocados pela cor do
        personagem mais próxima (sem "auréola" cinza nas bordas) e o braço que aparece
        na vista de lado (por cima do manto) apagado com o musgo em volta.
        """
        rgb = self.rgba[..., :3].copy()
        char = self.char.copy()
        # vista de lado: braço/mão pretos dentro da silhueta do musgo -> musgo interpolado
        v = VIEWS["side"]
        for y in range(150, 470):
            row_m = self.moss[y, v["x0"]:v["x1"]]
            xs = np.nonzero(row_m)[0]
            if len(xs) < 4:
                continue
            a, b = xs.min(), xs.max()
            for x in range(a, b + 1):
                X = x + v["x0"]
                if self.body[y, X]:
                    lx = X - 1
                    while lx > a + v["x0"] and not self.moss[y, lx]:
                        lx -= 1
                    rx = X + 1
                    while rx < b + v["x0"] and not self.moss[y, rx]:
                        rx += 1
                    t = (X - lx) / max(1, rx - lx)
                    rgb[y, X] = rgb[y, lx] * (1 - t) + rgb[y, rx] * t
        # sangria: espalha a cor do personagem para fora (12 passos)
        filled = char.copy()
        for _ in range(12):
            acc = np.zeros_like(rgb)
            cnt = np.zeros(filled.shape, dtype=np.float32)
            for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
                sh = np.roll(np.roll(filled, dy, 0), dx, 1)
                acc += np.roll(np.roll(rgb, dy, 0), dx, 1) * sh[..., None]
                cnt += sh
            grow = (~filled) & (cnt > 0)
            rgb[grow] = acc[grow] / cnt[grow][:, None]
            filled |= grow
        out = np.concatenate([rgb, np.ones(rgb.shape[:2] + (1,), dtype=np.float32)], 2)[::-1]
        img = bpy.data.images.new(name, self.w, self.h, alpha=False)
        img.pixels.foreach_set(out.astype(np.float32).ravel())
        return img


# =============================================================================
# 3. Texturas pequenas e materiais
# =============================================================================

def hexc(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


SKIN = hexc("#081921")           # corpo: quase preto azul-esverdeado (medido na referência)
NEON = hexc("#C3EA5C")           # olhos (medido)
PUPIL = hexc("#1F4205")
LEAF_A, LEAF_B, STEM = hexc("#6FA24A"), hexc("#3E7A43"), hexc("#0B1D1E")


def make_image(name, w, h, pixel_fn):
    img = bpy.data.images.new(name, w, h, alpha=True)
    px = []
    for y in range(h):
        for x in range(w):
            px.extend(pixel_fn(x, y))
    img.pixels[:] = px
    return img


def texture_skin():
    return make_image("Musgo_Pele", 4, 4, lambda x, y: (*SKIN, 1.0))


def texture_eye():
    """Olho da referência: anel escuro, disco verde-amarelo neon, pupila escura pequena."""
    W = H = 32

    def px(x, y):
        d = math.hypot((x + 0.5) / W - 0.5, (y + 0.5) / H - 0.5)
        if d > 0.46:
            return (0.0, 0.07, 0.02, 1.0)
        if d < 0.10:
            return (*PUPIL, 1.0)
        return (*NEON, 1.0)

    return make_image("Musgo_Olho", W, H, px)


def texture_leaf():
    """Folha oval (u = largura, v = comprimento), duas metades de tom e nervura; recorte alfa."""
    W, H = 8, 16

    def px(x, y):
        u, v = (x + 0.5) / W, (y + 0.5) / H
        half = 0.5 * math.sin(math.pi * v) ** 0.7
        if abs(u - 0.5) > half:
            return (0, 0, 0, 0)
        c = LEAF_A if u > 0.5 else LEAF_B
        if abs(u - 0.5) < 0.07:
            c = STEM
        return (*c, 1.0)

    return make_image("Musgo_Folha", W, H, px)


def make_material(name, image, emission=0.0, alpha=False, closest=True, cull=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")
        out = next((n for n in nodes if n.type == "OUTPUT_MATERIAL"), None) or nodes.new("ShaderNodeOutputMaterial")
        links.new(bsdf.outputs[0], out.inputs[0])
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest" if closest else "Linear"
    tex.location = (-420, 200)
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 1.0
    for key in ("Specular IOR Level", "Specular"):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = 0.0
    if emission > 0.0:
        links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"] if "Emission Color" in bsdf.inputs else bsdf.inputs["Emission"])
        bsdf.inputs["Emission Strength"].default_value = emission
    if alpha:
        links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = "DITHERED"
        if hasattr(mat, "blend_method"):
            mat.blend_method = "CLIP"
    mat.use_backface_culling = cull
    return mat


# =============================================================================
# 4. Malha (um osso por vértice)
# =============================================================================

MATS = ["Musgo", "Pele", "Olhos", "Bota", "Folha"]   # "Bota" = forro escuro do manto (as botas vêm pintadas na projeção)


class MeshBuilder:
    def __init__(self):
        self.verts, self.bones = [], []
        self.faces, self.face_mats, self.face_uvs = [], [], []

    def v(self, co, bone):
        self.verts.append(Vector(co))
        self.bones.append(bone)
        return len(self.verts) - 1

    def face(self, idx, mat, uvs=None):
        if uvs is None:
            uvs = [(0.5, 0.5)] * len(idx)
        self.faces.append(list(idx))
        self.face_mats.append(MATS.index(mat))
        self.face_uvs.append(uvs)

    def tube(self, points, radii, bones, mat, sides=6, cap_end=True, cap_start=False):
        rings = []
        for i, (p, r, b) in enumerate(zip(points, radii, bones)):
            p = Vector(p)
            d = (Vector(points[min(i + 1, len(points) - 1)]) - Vector(points[max(i - 1, 0)])).normalized()
            ref = Vector((0, 1, 0)) if abs(d.y) < 0.9 else Vector((1, 0, 0))
            side = d.cross(ref).normalized()
            up = side.cross(d).normalized()
            rings.append([self.v(p + (side * math.cos(a) + up * math.sin(a)) * r, b)
                          for a in (k * 2 * math.pi / sides for k in range(sides))])
        for a, b in zip(rings, rings[1:]):
            for s in range(sides):
                self.face([a[s], a[(s + 1) % sides], b[(s + 1) % sides], b[s]], mat)
        if cap_end:
            self.face(rings[-1], mat)
        if cap_start:
            self.face(list(reversed(rings[0])), mat)
        return rings

    def blade(self, a, b, tip, bone, mat, uv=True):
        a, b, t = Vector(a), Vector(b), Vector(tip)
        ia, ib, it = self.v(a, bone), self.v(b, bone), self.v(t, bone)
        mid = self.v((a + b) * 0.5 + (t - (a + b) * 0.5) * 0.4, bone)
        self.face([ia, mid, it], mat, [(0.0, 0.0), (0.5, 0.4), (0.5, 1.0)])
        self.face([mid, ib, it], mat, [(0.5, 0.4), (1.0, 0.0), (0.5, 1.0)])

    def leaf(self, base, direction, length, width, bone):
        """Folha oval de verdade: losango com uma dobra na nervura (tem volume de perto)."""
        d = Vector(direction).normalized()
        side = d.cross(Vector((0, 0, 1)))
        if side.length < 0.1:
            side = d.cross(Vector((1, 0, 0)))
        side = side.normalized() * width
        fold = d.cross(side).normalized() * width * 0.35
        b = Vector(base)
        t = b + d * length
        m = b + d * length * 0.45
        ib, it = self.v(b, bone), self.v(t, bone)
        il, ir, im = self.v(m - side, bone), self.v(m + side, bone), self.v(m + fold, bone)
        self.face([ib, im, il], "Folha", [(0.5, 0.0), (0.5, 0.45), (0.0, 0.45)])
        self.face([im, it, il], "Folha", [(0.5, 0.45), (0.5, 1.0), (0.0, 0.45)])
        self.face([ib, ir, im], "Folha", [(0.5, 0.0), (1.0, 0.45), (0.5, 0.45)])
        self.face([im, ir, it], "Folha", [(0.5, 0.45), (1.0, 0.45), (0.5, 1.0)])

    def oval(self, center, rx, rz, bone, mat, sides=12):
        c = self.v(center, bone)
        rim = [self.v(Vector(center) + Vector((math.cos(a) * rx, 0.0, math.sin(a) * rz)), bone)
               for a in (i * 2 * math.pi / sides for i in range(sides))]
        uv = [(0.5 + 0.5 * math.cos(i * 2 * math.pi / sides), 0.5 + 0.5 * math.sin(i * 2 * math.pi / sides)) for i in range(sides)]
        for i in range(sides):
            j = (i + 1) % sides
            self.face([c, rim[j], rim[i]], mat, [(0.5, 0.5), uv[j], uv[i]])

    def tri_count(self):
        return sum(len(f) - 2 for f in self.faces)

    def build(self, name, materials, coll):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(v) for v in self.verts], [], self.faces)
        mesh.update()
        uv = mesh.uv_layers.new(name="UVMap")
        for poly, uvs, mi in zip(mesh.polygons, self.face_uvs, self.face_mats):
            poly.material_index = mi
            poly.use_smooth = False
            for li, loop in enumerate(poly.loop_indices):
                uv.data[loop].uv = uvs[li]
        for m in materials:
            mesh.materials.append(m)
        obj = link(bpy.data.objects.new(name, mesh), coll)
        for i, bone in enumerate(self.bones):
            vg = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
            vg.add([i], 1.0, "REPLACE")
        return obj


# =============================================================================
# 5. Esqueleto (medido na referência)
# =============================================================================

BONES = {
    "Root": ((0, 0, 0), (0, 0.25, 0), None),
    "Pelvis": ((0, 0.0, 0.84), (0, 0.0, 1.00), "Root"),
    "Spine": ((0, 0.0, 1.00), (0, -0.01, 1.18), "Pelvis"),
    "Chest": ((0, -0.01, 1.18), (0, -0.03, 1.38), "Spine"),
    "Neck": ((0, -0.03, 1.38), (0, -0.05, 1.47), "Chest"),
    "Head": ((0, -0.05, 1.47), (0, -0.07, 1.80), "Neck"),
    "Sprout_Top": ((0, -0.07, 1.80), (0, -0.07, 1.90), "Head"),
    "Cloak_Back": ((0, 0.10, 1.32), (0, 0.18, 0.42), "Chest"),
    "Sprout_Back": ((0.08, 0.12, 1.20), (0.10, 0.30, 1.30), "Chest"),
}
for _s, _x in (("L", 1.0), ("R", -1.0)):
    BONES.update({
        f"Ear_{_s}": ((0.12 * _x, -0.05, 1.63), (0.22 * _x, -0.02, 1.68), "Head"),
        f"Eye_{_s}": ((0.05 * _x, -0.17, 1.618), (0.05 * _x, -0.24, 1.618), "Head"),
        f"Cloak_Front_{_s}": ((0.10 * _x, -0.14, 1.32), (0.18 * _x, -0.18, 0.42), "Chest"),
        f"Shoulder_{_s}": ((0.05 * _x, -0.03, 1.37), (0.15 * _x, -0.03, 1.37), "Chest"),
        f"UpperArm_{_s}": ((0.15 * _x, -0.03, 1.37), (0.36 * _x, -0.035, 1.11), f"Shoulder_{_s}"),
        f"Forearm_{_s}": ((0.36 * _x, -0.035, 1.11), (0.50 * _x, -0.04, 0.90), f"UpperArm_{_s}"),
        f"Hand_{_s}": ((0.50 * _x, -0.04, 0.90), (0.56 * _x, -0.045, 0.77), f"Forearm_{_s}"),
        f"Thigh_{_s}": ((0.095 * _x, 0, 0.84), (0.095 * _x, 0, 0.47), "Pelvis"),
        f"Shin_{_s}": ((0.095 * _x, 0, 0.47), (0.095 * _x, 0.01, 0.10), f"Thigh_{_s}"),
        f"Foot_{_s}": ((0.095 * _x, 0.01, 0.10), (0.095 * _x, -0.19, 0.03), f"Shin_{_s}"),
    })


def B(name):
    return Vector(BONES[name][0]), Vector(BONES[name][1])


def build_armature(coll):
    data = bpy.data.armatures.new(NAME + "_Rig")
    arm = link(bpy.data.objects.new(NAME, data), coll)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (head, tail, _) in BONES.items():
        eb = data.edit_bones.new(name)
        eb.head, eb.tail, eb.roll = Vector(head), Vector(tail), 0.0
    for name, (_, _, parent) in BONES.items():
        if parent:
            data.edit_bones[name].parent = data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return arm


# =============================================================================
# 6. Geometria reconstruída das silhuetas
# =============================================================================

def cloak_bone(p):
    if p.z > 1.46:
        return "Head"
    if p.z > 1.22:
        return "Chest"
    if p.y > 0.03:
        return "Cloak_Back"
    return "Cloak_Front_L" if p.x >= 0 else "Cloak_Front_R"


def leg_bone(side, z):
    return (f"Thigh_{side}" if z > 0.47 else f"Shin_{side}" if z > 0.11 else f"Foot_{side}")


def face_view(n):
    """Vista que pinta a face, pela direção da normal (frente/costas/lado)."""
    if abs(n.y) >= abs(n.x) * 0.85:
        return "front" if n.y < 0 else "back"
    return "side"


def projected_uvs(ref, mb, idx, view):
    return [((px + 0.0) / ref.w, 1.0 - (py + 0.0) / ref.h) for px, py in (to_px(view, mb.verts[i]) for i in idx)]


def cloak_profile(ref):
    """Para cada altura: limites esquerdo/direito (frente) e frente/costas (lado), em metros."""
    sf, ss = vscale("front"), vscale("side")
    rows = []
    z = HEIGHT - 0.02
    while z > 0.36:
        fy = VIEWS["front"]["sole"] - z / sf
        sy = VIEWS["side"]["sole"] - z / ss
        head = z > 1.45
        fe = ref.extents("front", ref.moss | (ref.body if head else ref.moss), fy, band=3,
                         xmin=100 if z > 1.2 else 90, xmax=290 if z > 1.2 else 300)
        se = ref.extents("side", ref.char if head else ref.moss, sy, band=3, xmax=527)
        if fe is None or se is None:
            break
        if head and fe:                      # a cabeça não inclui as orelhas (pretas): usa só o musgo
            fm = ref.extents("front", ref.moss, fy, band=3, xmin=120, xmax=270)
            if fm:
                fe = fm
        xl, _ = from_px("front", fe[0], fy)
        xr, _ = from_px("front", fe[1], fy)
        yf, _ = from_px("side", se[0], sy)
        yb, _ = from_px("side", se[1], sy)
        rows.append([z, xl, xr, yf, yb])
        z -= 0.02 if z < 0.85 else 0.035
    # suaviza (média móvel 3) para não serrilhar a superfície
    sm = []
    for i in range(len(rows)):
        win = rows[max(0, i - 1):i + 2]
        sm.append([rows[i][0]] + [sum(r[k] for r in win) / len(win) for k in range(1, 5)])
    return sm


def build_creature(ref):
    mb = MeshBuilder()
    rng = random.Random(3)

    # ---- manto/cabelo de musgo: anéis com a silhueta exata das vistas
    S = 32
    prof = cloak_profile(ref)
    P = 2.0 / 2.6                                   # superelipse (preenche os "cantos" da silhueta)
    rings = []
    for z, xl, xr, yf, yb in prof:
        ring = []
        for k in range(S):
            a = -math.pi / 2 + k * 2 * math.pi / S
            c, s = math.cos(a), math.sin(a)
            x = (xr if c >= 0 else -xl) * math.copysign(abs(c) ** P, c)
            y = (yb if s >= 0 else -yf) * math.copysign(abs(s) ** P, s)
            n = 1.0 + 0.025 * (rng.random() - 0.5)  # superfície orgânica
            p = Vector((x * n, y * n, z + 0.008 * (rng.random() - 0.5)))
            ring.append(mb.v(p, cloak_bone(p)))
        rings.append(ring)
    top_z = prof[0][0] + 0.02
    cy = (prof[0][3] + prof[0][4]) * 0.5
    top = mb.v((0.0, cy, top_z), "Head")
    for k in range(S):
        idx = [rings[0][k], rings[0][(k + 1) % S], top]
        pa, pb, pt = (mb.verts[j] for j in idx)
        if (pb - pa).cross(pt - pa).z < 0:
            idx = idx[::-1]
        mb.face(idx, "Musgo", projected_uvs(ref, mb, idx, "front" if math.sin(-math.pi / 2 + k * 2 * math.pi / S) < 0 else "back"))

    kept = removed = 0
    lining = []
    for i in range(len(rings) - 1):
        hi, lo = rings[i], rings[i + 1]
        for k in range(S):
            idx = [lo[k], lo[(k + 1) % S], hi[(k + 1) % S], hi[k]]
            pts = [mb.verts[j] for j in idx]
            c = sum(pts, Vector()) / 4
            n = (pts[1] - pts[0]).cross(pts[3] - pts[0])
            if n.length < 1e-9:
                continue
            n.normalize()
            if n.dot(Vector((c.x, c.y - cy, 0))) < 0:      # garante a face virada para fora
                n = -n
                idx = idx[::-1]
            view = face_view(n)
            # recorte pela imagem (5 amostras por face): fora do personagem -> barra rasgada;
            # vão escuro na parte de baixo da frente/costas -> fenda das pernas
            samples = [c] + [c.lerp(q, 0.6) for q in pts]
            def frac(mask):
                return sum(ref.at(mask, *to_px(view, q)) for q in samples) / len(samples)
            inside = frac(ref.char_d) >= 0.5
            if view == "side" and c.z < 1.42:       # contorno rasgado também visto de frente/costas
                fv = "front" if c.y < 0.04 else "back"
                inside = inside and sum(ref.at(ref.char_d, *to_px(fv, q)) for q in samples) / len(samples) >= 0.5
            if c.z < 0.66:
                inside = frac(ref.moss_d) >= 0.5 and frac(ref.dark) < 0.6
            if view == "front" and c.z < 0.98 and abs(c.x) < 0.10 and (frac(ref.dark) >= 0.6 or frac(ref.body) >= 0.4):
                inside = False
            if not inside:
                removed += 1
                continue
            kept += 1
            mb.face(idx, "Musgo", projected_uvs(ref, mb, idx, view))
            lining.append(idx)

    # ---- forro do manto: o lado de dentro é sombra escura (como nos vãos da referência)
    inner = {}
    for idx in lining:
        ids = []
        for j in idx:
            if j not in inner:
                p = mb.verts[j]
                d = Vector((p.x, p.y - cy, 0))
                off = d.normalized() * 0.006 if d.length > 1e-6 else Vector()
                inner[j] = mb.v(p - off, mb.bones[j])
            ids.append(inner[j])
        mb.face(list(reversed(ids)), "Bota")

    # ---- pernas (com as botas): silhueta da frente por perna + do lado, pintadas pela projeção
    sf, ss = vscale("front"), vscale("side")
    for side, sgn in (("L", 1.0), ("R", -1.0)):
        L = 10
        rings = []
        z = 0.86
        while z >= 0.0:
            fy = VIEWS["front"]["sole"] - max(z, 0.004) / sf
            sy = VIEWS["side"]["sole"] - max(z, 0.004) / ss
            fyy = min(fy, VIEWS["front"]["sole"] - 0.44 / sf) if z > 0.44 else fy   # escondido no musgo: usa a coxa visível
            syy = min(sy, VIEWS["side"]["sole"] - 0.40 / ss) if z > 0.40 else sy
            runs = [r for r in ref.runs(ref.char, fyy, 120, 270) if r[1] - r[0] >= 6]
            mine = [r for r in runs if ((r[0] + r[1]) / 2 - VIEWS["front"]["cx"]) * sgn > 0]
            if not mine:
                z -= 0.04
                continue
            r = max(mine, key=lambda q: q[1] - q[0])
            xl, _ = from_px("front", r[0], fyy)
            xr, _ = from_px("front", r[1], fyy)
            sruns = [q for q in ref.runs(ref.char, syy, 395, 500) if q[1] >= 445 and q[0] <= 480]
            if not sruns:
                z -= 0.04
                continue
            q = (min(s[0] for s in sruns), max(s[1] for s in sruns))
            yf, _ = from_px("side", q[0], syy)
            yb, _ = from_px("side", q[1], syy)
            cxm, cym = (xl + xr) / 2, (yf + yb) / 2
            ax, ay = max(0.018, (xr - xl) / 2), max(0.018, (yb - yf) / 2)
            ring = []
            for k in range(L):
                a = -math.pi / 2 + k * 2 * math.pi / L
                p = Vector((cxm + ax * math.cos(a), cym + ay * math.sin(a), z))
                ring.append(mb.v(p, leg_bone(side, z)))
            rings.append(ring)
            z -= 0.05 if z > 0.5 else 0.03 if z > 0.12 else 0.02
            if z < 0.0 and z > -0.02:
                z = 0.0
        for hi, lo in zip(rings, rings[1:]):
            for k in range(L):
                idx = [lo[k], lo[(k + 1) % L], hi[(k + 1) % L], hi[k]]
                pts = [mb.verts[j] for j in idx]
                n = (pts[1] - pts[0]).cross(pts[3] - pts[0])
                c = sum(pts, Vector()) / 4
                cen = sum((mb.verts[j] for j in hi), Vector()) / L
                if n.dot(c - cen) < 0:
                    n = -n
                    idx = idx[::-1]
                mb.face(idx, "Musgo", projected_uvs(ref, mb, idx, face_view(n.normalized())))
        bottom = rings[-1]
        bp = [mb.verts[j] for j in bottom]
        if (bp[1] - bp[0]).cross(bp[2] - bp[0]).z > 0:
            bottom = list(reversed(bottom))
        mb.face(list(bottom), "Musgo", projected_uvs(ref, mb, list(bottom), "side"))

    # ---- braços (medidos na vista de frente): finos, longos, em A
    for s, x in (("L", 1), ("R", -1)):
        sh, el = B(f"UpperArm_{s}")
        _, wr = B(f"Forearm_{s}")
        _, hd = B(f"Hand_{s}")
        root = sh - Vector((0.06 * x, 0, -0.01))
        mb.tube([root, sh, el, wr], [0.040, 0.042, 0.034, 0.026],
                [f"Shoulder_{s}", f"UpperArm_{s}", f"Forearm_{s}", f"Hand_{s}"], "Pele", 7, False)
        # mão: palma + 4 dedos longos e finos com ponta, abertos em leque, e o polegar
        d = (hd - wr).normalized()
        out = Vector((x, 0, 0))
        palm = wr + d * 0.055
        mb.tube([wr, palm], [0.027, 0.032], [f"Hand_{s}"] * 2, "Pele", 6, False)
        for k, spread in enumerate((-0.6, -0.2, 0.2, 0.6)):
            base = palm + out * 0.016 * spread * 1.5 + Vector((0, -0.006 * k, 0))
            dirv = (d + out * 0.35 * spread).normalized()
            length = 0.085 if k in (1, 2) else 0.07
            mid = base + dirv * length * 0.5
            tip = base + dirv * length + Vector((0, -0.012, 0))
            mb.tube([base, mid, tip], [0.009, 0.007, 0.0015], [f"Hand_{s}"] * 3, "Pele", 4, False)
        tb = wr + d * 0.03 + Vector((0, -0.02, 0))
        mb.tube([tb, tb + (d * 0.6 - out * 0.5 + Vector((0, -0.4, 0))).normalized() * 0.05], [0.009, 0.0015],
                [f"Hand_{s}"] * 2, "Pele", 4, False)

    # ---- orelhas: pretas, longas e pontudas, para os lados (prisma fino = tem volume)
    for s, x in (("L", 1), ("R", -1)):
        a = Vector((0.125 * x, -0.07, 1.655))
        b = Vector((0.118 * x, -0.035, 1.585))
        t = Vector((0.222 * x, -0.03, 1.678))
        th = Vector((0, 0.012, 0))
        ids = [mb.v(p, f"Ear_{s}") for p in (a - th, b - th, t, a + th, b + th)]
        mb.face([ids[0], ids[1], ids[2]] if x > 0 else [ids[1], ids[0], ids[2]], "Pele")
        mb.face([ids[4], ids[3], ids[2]] if x > 0 else [ids[3], ids[4], ids[2]], "Pele")
        mb.face([ids[0], ids[3], ids[4], ids[1]], "Pele")

    # ---- olhos: discos com brilho exatamente onde estão pintados (centros medidos)
    for s, x, z in (("L", 0.051, 1.618), ("R", -0.048, 1.618)):
        fy = VIEWS["front"]["sole"] - z / sf
        # profundidade: frente da cabeça nessa altura (vista de lado)
        se = ref.extents("side", ref.char, VIEWS["side"]["sole"] - z / ss, band=2, lo=1, hi=99)
        yf = from_px("side", se[0], VIEWS["side"]["sole"] - z / ss)[0] if se else -0.19
        mb.oval((x, yf - 0.006, z), 0.034, 0.037, f"Eye_{s}", "Olhos")

    # ---- broto curvo no topo da cabeça (a "antena")
    pts = [(0.0, -0.07, 1.79), (-0.004, -0.075, 1.83), (0.003, -0.078, 1.86), (0.018, -0.078, 1.877),
           (0.032, -0.075, 1.873), (0.035, -0.07, 1.86), (0.027, -0.066, 1.853)]
    mb.tube(pts, [0.008, 0.007, 0.006, 0.005, 0.005, 0.004, 0.003], ["Sprout_Top"] * len(pts), "Folha", 4, True)

    # ---- galhinhos com folhas: costas/lados (posições das vistas de lado e de frente)
    twigs = [((0.10, 0.05, 1.55), (0.10, 0.18, 1.60), "Head"),
             ((0.12, 0.10, 1.33), (0.14, 0.27, 1.38), "Sprout_Back"),
             ((0.14, 0.14, 1.02), (0.16, 0.31, 1.06), "Cloak_Back"),
             ((0.13, 0.14, 0.75), (0.15, 0.26, 0.72), "Cloak_Back"),
             ((-0.22, -0.02, 1.45), (-0.30, -0.03, 1.43), "Chest"),
             ((0.22, 0.02, 1.33), (0.29, 0.03, 1.35), "Chest"),
             ((-0.12, 0.14, 1.20), (-0.14, 0.26, 1.24), "Cloak_Back")]
    for base, tip, bone in twigs:
        base, tip = Vector(base), Vector(tip)
        mb.tube([base, tip], [0.006, 0.003], [bone, bone], "Pele", 4, False)
        d = (tip - base).normalized()
        for t, up in ((0.45, 1), (0.7, -1), (0.95, 1)):
            p = base.lerp(tip, t)
            mb.leaf(p, (d * 0.6 + Vector((0, 0, 0.8 * up))).normalized(), 0.055, 0.02, bone)
        mb.leaf(tip, d, 0.055, 0.02, bone)
    print(f"[Espírito do Musgo] manto: {kept} faces mantidas, {removed} recortadas pela silhueta")
    return mb


def skin(mesh_obj, arm):
    mesh_obj.parent = arm
    mod = mesh_obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm


# =============================================================================
# 7. Animações
# =============================================================================

ACTION_NAMES = ("Idle", "Hiding", "Running", "Death")


class Animator:
    """Cria Actions e grava poses com rotações em eixos do esqueleto (graus). Quadros começam em 1."""

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
        self.keys = {}

    def key(self, frame, bone, rot=None, loc=None, scale=None):
        self.keys.setdefault((bone, frame), {}).update(
            {k: v for k, v in (("rot", rot), ("loc", loc), ("scale", scale)) if v is not None})

    def _local_rot(self, bone, euler_deg):
        q = Quaternion((1, 0, 0, 0))
        for axis, deg in zip("XYZ", euler_deg):
            if deg:
                q = Quaternion(Vector(tuple(1.0 if a == axis else 0.0 for a in "XYZ")), math.radians(deg)) @ q
        b = self.rest[bone]
        return b.inverted() @ q @ b

    def _local_vec(self, bone, v):
        return self.rest[bone].inverted() @ Vector(v)

    def commit(self):
        for b in self.rest:                               # todo osso tem chave: nada "vaza" de outra Action
            self.keys.setdefault((b, 0), {})
        frames_of = {}
        for (b, f), k in self.keys.items():
            frames_of.setdefault(b, set()).update(k.keys())
        for b, channels in frames_of.items():
            start = self.keys.setdefault((b, 0), {})
            start.setdefault("rot", (0, 0, 0))
            if "loc" in channels:
                start.setdefault("loc", (0, 0, 0))
            if "scale" in channels:
                start.setdefault("scale", 1.0)
            if self.loop:
                self.keys.setdefault((b, self.length), {}).update(self.keys[(b, 0)])
        for (b, f), k in sorted(self.keys.items(), key=lambda i: i[0][1]):
            pb = self.arm.pose.bones[b]
            frame = f + 1
            pb.rotation_quaternion = self._local_rot(b, k.get("rot", (0, 0, 0)))
            pb.keyframe_insert("rotation_quaternion", frame=frame)
            if "loc" in k:
                pb.location = self._local_vec(b, k["loc"])
                pb.keyframe_insert("location", frame=frame)
            if "scale" in k:
                s = k["scale"]
                pb.scale = (s, s, s) if isinstance(s, (int, float)) else s
                pb.keyframe_insert("scale", frame=frame)
        for fc in iter_fcurves(self.action):
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
            if self.loop:
                fc.modifiers.new("CYCLES")
        self.action.frame_range = (1, self.length + 1)
        for pb in self.arm.pose.bones:
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.location = (0, 0, 0)
            pb.scale = (1, 1, 1)


def iter_fcurves(action):
    if hasattr(action, "layers") and len(action.layers):
        for layer in action.layers:
            for strip in layer.strips:
                for bag in getattr(strip, "channelbags", []):
                    yield from bag.fcurves
    elif hasattr(action, "fcurves"):
        yield from action.fcurves


SIDES = (("L", 1), ("R", -1))
ARM_HANG = 0            # 0 = A-pose do modelo (braços para fora do manto)


def arms_hang(an, f, extra_x=0.0, extra_in=0.0, elbow=0.0):
    for s, x in SIDES:
        an.key(f, f"UpperArm_{s}", rot=(extra_x, x * (ARM_HANG + extra_in), 0))
        an.key(f, f"Forearm_{s}", rot=(-elbow, -x * 4, 0))


def anim_idle(an):
    """Respiração tímida, ombros encolhidos, cabeça curiosa de um lado ao outro, orelhas com espasmos."""
    L = 48
    an.begin("Idle", L, True)
    for f in range(0, L + 1, 6):
        t = 2 * math.pi * f / L
        b = math.sin(t * 2)
        an.key(f, "Chest", rot=(3 - 1.5 * b, 0, 0), scale=(1 + 0.02 * b, 1.0, 1 + 0.02 * b))
        an.key(f, "Spine", rot=(4, 0, 0), loc=(0, 0, 0.006 * b))
        an.key(f, "Head", rot=(-6, 9 * math.sin(t), 4 * math.sin(t)))
        an.key(f, "Neck", rot=(6, 0, 0))
        for s, x in SIDES:
            an.key(f, f"Shoulder_{s}", rot=(-4, x * 7, x * 6))          # ombros encolhidos para dentro
            an.key(f, f"Cloak_Front_{s}", rot=(-2 * b, 0, 0))
        arms_hang(an, f, extra_x=-4 + 2 * b, elbow=10)
        an.key(f, "Cloak_Back", rot=(1.5 * b, 0, 0))
        an.key(f, "Sprout_Top", rot=(8 * math.sin(t * 2 + 1), 10 * math.sin(t + 0.5), 0))
        an.key(f, "Sprout_Back", rot=(0, 0, 8 * math.sin(t * 2)))
    for f in (0, 12, 13, 15, 36, 37, 39, 48):                            # espasmos das orelhas
        tw = 18 if f in (13, 37) else 0
        an.key(f, "Ear_L", rot=(0, 0, tw if f < 30 else 0))
        an.key(f, "Ear_R", rot=(0, 0, -tw if f > 30 else 0))
    for f, s in ((0, 1.0), (28, 1.0), (30, 0.15), (32, 1.0)):            # piscada
        for e in ("Eye_L", "Eye_R"):
            an.key(f, e, scale=(1.0, 1.0, s))
    an.commit()


def anim_hiding(an):
    """Agachado, manto aberto cobrindo tudo: vira uma moita; só os olhos espiam para os lados."""
    L = 40
    an.begin("Hiding", L, True)
    for f in range(0, L + 1, 4):
        t = 2 * math.pi * f / L
        b = math.sin(t * 2)
        an.key(f, "Pelvis", loc=(0, 0.06, -0.50 + 0.008 * b), rot=(20, 0, 0))
        an.key(f, "Spine", rot=(28, 0, 0))
        an.key(f, "Chest", rot=(16 - b, 0, 0), scale=(1.0 + 0.015 * b, 1.0, 1.0))
        an.key(f, "Neck", rot=(-12, 0, 0), loc=(0, 0.03, -0.07))   # cabeça afunda entre os ombros
        for s, x in SIDES:
            an.key(f, f"Thigh_{s}", rot=(-105, -x * 10, 0))                # joelhos um pouco para fora
            an.key(f, f"Shin_{s}", rot=(138, 0, 0))
            an.key(f, f"Foot_{s}", rot=(-20, 0, 0))
            an.key(f, f"UpperArm_{s}", rot=(25, x * 16, 0))                 # braços recolhidos no manto
            an.key(f, f"Forearm_{s}", rot=(-100, 0, 0))
            an.key(f, f"Shoulder_{s}", rot=(0, x * 8, x * 10))
            # manto se espalha e cobre o corpo (abre para os lados e cai até o chão)
            an.key(f, f"Cloak_Front_{s}", rot=(-22, -x * 16, 0), scale=(1.5, 1.0, 1.5))
            an.key(f, f"Ear_{s}", rot=(0, x * 10, -x * 25))                  # orelhas baixas
        an.key(f, "Cloak_Back", rot=(10, 0, 0), scale=(1.6, 1.0, 1.6))
        an.key(f, "Sprout_Top", rot=(4 * b, 0, 0))
    # olhos espiam: cabeça parada, depois desliza para um lado, volta, depois o outro
    for f, yaw, roll in ((0, 0, 0), (8, 0, 0), (11, 26, 4), (17, 26, 4), (20, 0, 0), (27, 0, 0), (30, -26, -4), (36, -26, -4), (39, 0, 0)):
        an.key(f, "Head", rot=(-14, roll, yaw))
    for f, s in ((0, 0.75), (8, 0.75), (11, 1.0), (36, 1.0), (39, 0.75)):
        for e in ("Eye_L", "Eye_R"):
            an.key(f, e, scale=(1.0, 1.0, s))
    an.commit()


def anim_running(an):
    """Corrida furtiva, curvada para a frente; braços em oposição; manto e orelhas para trás."""
    L = 20
    an.begin("Running", L, True)
    for f in range(0, L + 1, 2):
        t = 2 * math.pi * f / L
        bob = abs(math.sin(t))
        an.key(f, "Pelvis", loc=(0, 0, -0.06 + 0.05 * bob), rot=(10, 0, 6 * math.sin(t)))
        an.key(f, "Spine", rot=(22, 0, -4 * math.sin(t)))
        an.key(f, "Chest", rot=(8, 0, -6 * math.sin(t)))
        an.key(f, "Neck", rot=(-16, 0, 0))
        an.key(f, "Head", rot=(-14 + 4 * bob, 0, 0))
        for s, x in SIDES:
            ph = 0.0 if s == "L" else math.pi
            sw = math.sin(t + ph)
            an.key(f, f"Thigh_{s}", rot=(-48 * sw - 12, 0, 0))
            bend = 70 * max(0.0, -math.cos(t + ph)) + 18
            an.key(f, f"Shin_{s}", rot=(bend, 0, 0))
            an.key(f, f"Foot_{s}", rot=(-0.3 * bend, 0, 0))
            an.key(f, f"UpperArm_{s}", rot=(42 * sw - 6, x * (ARM_HANG - 4), 0))      # oposição à perna
            an.key(f, f"Forearm_{s}", rot=(-45, 0, 0))
            an.key(f, f"Ear_{s}", rot=(-10, 0, x * 38 + x * 4 * bob))                # orelhas para trás
            an.key(f, f"Cloak_Front_{s}", rot=(22 + 6 * math.sin(t * 2 + x), 0, 0))
        an.key(f, "Cloak_Back", rot=(28 + 7 * math.sin(t * 2), 0, 4 * math.sin(t)))
        an.key(f, "Sprout_Top", rot=(18 + 6 * math.sin(t * 2), 0, 0))
        an.key(f, "Sprout_Back", rot=(20, 0, 6 * math.sin(t * 2)))
    an.commit()


def anim_death(an):
    """Recua com o impacto, cai de joelhos e tomba de bruços; o manto cobre o corpo como um monte de folhas; olhos se apagam."""
    L = 36
    an.begin("Death", L, False)
    poses = {
        0: dict(pel=(0, 0, 0), pelr=(0, 0, 0), spine=(4, 0, 0), chest=(3, 0, 0), head=(-6, 0, 0), thigh=0, shin=0, root=(0, 0, 0), arm=(0, 10), cloak=0, cloakb=0, cs=1.0),
        4: dict(pel=(0, 0.03, -0.02), pelr=(-8, 0, 0), spine=(-14, 0, 0), chest=(-12, 0, 0), head=(-32, 0, 6), thigh=-6, shin=6, root=(0, 0, 0), arm=(28, 20), cloak=-10, cloakb=-12, cs=1.0),
        8: dict(pel=(0, 0.02, -0.04), pelr=(-6, 0, 0), spine=(-10, 0, 0), chest=(-8, 0, 0), head=(-26, 4, 8), thigh=-10, shin=14, root=(0, 0, 0), arm=(22, 18), cloak=-6, cloakb=-8, cs=1.0),
        16: dict(pel=(0, 0.10, -0.42), pelr=(6, 0, 0), spine=(22, 0, 0), chest=(16, 0, 0), head=(18, 8, 0), thigh=-40, shin=125, root=(0, 0, 0), arm=(-6, 8), cloak=4, cloakb=6, cs=1.1),
        22: dict(pel=(0, 0.10, -0.42), pelr=(10, 0, 0), spine=(34, 0, 0), chest=(22, 0, 0), head=(30, 10, 0), thigh=-40, shin=125, root=(0, 0, 0), arm=(-10, 6), cloak=6, cloakb=10, cs=1.15),
        30: dict(pel=(0, 0.16, -0.56), pelr=(48, 0, 6), spine=(24, 0, 0), chest=(12, 0, 0), head=(4, 14, 0), thigh=-55, shin=80, root=(0, 0, 0), arm=(-40, 24), cloak=10, cloakb=8, cs=1.2),
        36: dict(pel=(0, 0.30, -0.70), pelr=(84, 0, 10), spine=(6, 0, 0), chest=(2, 0, 0), head=(-18, 26, 0), thigh=0, shin=8, root=(0, 0, 0), arm=(-25, 30), cloak=16, cloakb=4, cs=1.35),
    }
    for f, p in poses.items():
        an.key(f, "Pelvis", loc=p["pel"], rot=p["pelr"])
        an.key(f, "Spine", rot=p["spine"])
        an.key(f, "Chest", rot=p["chest"])
        an.key(f, "Head", rot=p["head"])
        for s, x in SIDES:
            an.key(f, f"Thigh_{s}", rot=(p["thigh"], -x * 6, 0))
            an.key(f, f"Shin_{s}", rot=(p["shin"], 0, 0))
            an.key(f, f"Foot_{s}", rot=(-0.4 * p["shin"] if p["shin"] > 60 else 30 if f == 36 else 0, 0, 0))
            an.key(f, f"UpperArm_{s}", rot=(p["arm"][0], -x * p["arm"][1], 0))
            an.key(f, f"Forearm_{s}", rot=(-8, 0, 0))
            an.key(f, f"Cloak_Front_{s}", rot=(-0.6 * p["cloak"], -x * p["cloak"], 0), scale=(p["cs"], 1.0, p["cs"]))
            an.key(f, f"Ear_{s}", rot=(0, 0, -x * min(30, p["cloak"] * 1.5)))
        an.key(f, "Cloak_Back", rot=(-0.5 * p["cloakb"], 0, 0), scale=(p["cs"], 0.9, p["cs"]))
    for f, s in ((0, 1.0), (22, 1.0), (30, 0.45), (36, 0.05)):            # o brilho estreita até sumir
        for e in ("Eye_L", "Eye_R"):
            an.key(f, e, scale=(0.9 + 0.1 * s, 1.0, s))
    an.commit()


ANIMATIONS = [anim_idle, anim_hiding, anim_running, anim_death]


# =============================================================================
# 8. Estúdio (luz e câmera de prévia) e exportação
# =============================================================================

def setup_studio(scene, coll):
    world = bpy.data.worlds.new("Estudio_Musgo")
    world.use_nodes = True
    nt = world.node_tree
    bg = nt.nodes.get("Background") or nt.nodes.new("ShaderNodeBackground")
    out = next((n for n in nt.nodes if n.type == "OUTPUT_WORLD"), None) or nt.nodes.new("ShaderNodeOutputWorld")
    if not bg.outputs[0].links:
        nt.links.new(bg.outputs[0], out.inputs[0])
    bg.inputs["Color"].default_value = (0.03, 0.04, 0.04, 1)
    bg.inputs["Strength"].default_value = 0.7
    scene.world = world

    def light(name, loc, rot, energy, color, size):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.color, data.size = energy, color, size
        obj = link(bpy.data.objects.new(name, data), coll)
        obj.location, obj.rotation_euler = loc, [math.radians(a) for a in rot]

    light("Luz Principal", (-2.2, -3.0, 3.2), (50, 0, -36), 420, (1.0, 0.97, 0.9), 2.0)
    light("Contraluz", (2.0, 2.6, 2.4), (-60, 0, 140), 300, (0.6, 0.85, 0.7), 1.5)
    light("Preenchimento", (3.0, -2.0, 1.0), (80, 0, 60), 90, (0.8, 0.9, 1.0), 2.0)

    cam = link(bpy.data.objects.new("Camera_Previa", bpy.data.cameras.new("Camera_Previa")), coll)
    cam.data.lens = 50
    cam.location = (2.6, -3.6, 1.5)
    target = link(bpy.data.objects.new("Alvo_Camera", None), coll)
    target.location = (0, 0, 0.85)
    c = cam.constraints.new("TRACK_TO")
    c.target, c.track_axis, c.up_axis = target, "TRACK_NEGATIVE_Z", "UP_Y"
    scene.camera = cam

    floor = bpy.data.meshes.new("Chao_Previa")
    floor.from_pydata([(-3, -3, 0), (3, -3, 0), (3, 3, 0), (-3, 3, 0)], [], [(0, 1, 2, 3)])
    fobj = link(bpy.data.objects.new("Chao_Previa", floor), coll)
    fm = bpy.data.materials.new("Chao_Previa")
    fm.use_nodes = True
    fb = next((n for n in fm.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if fb:
        fb.inputs["Base Color"].default_value = (0.25, 0.3, 0.33, 1)
    floor.materials.append(fm)
    return cam, target


def render_previews(scene, arm, out_dir, cam):
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 16
    scene.render.resolution_x, scene.render.resolution_y = 360, 420
    views = {"f": (1.3, -3.4, 1.15), "l": (3.5, 0.4, 1.0), "c": (-2.4, 2.6, 1.3)}
    for action in [a for a in bpy.data.actions if a.name in ACTION_NAMES]:
        arm.animation_data.action = action
        start, end = (int(f) for f in action.frame_range)
        for i, f in enumerate((start, (start + end) // 2, end)):
            scene.frame_set(f)
            for v, loc in views.items():
                if v != "f" and i != 1 and not (action.name == "Death" and v == "l"):
                    continue
                cam.location = loc
                scene.render.filepath = os.path.join(out_dir, "previa", f"{action.name}_{i}_{v}.png")
                bpy.ops.render.render(write_still=True)


def export(out_dir, arm, mesh_obj, images):
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
        use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        use_mesh_modifiers=False, mesh_smooth_type="FACE", use_triangles=True,
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X", armature_nodetype="NULL",
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
        bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode="STRIP",
    )


# =============================================================================
# 9. Execução
# =============================================================================

def main():
    out_dir, do_render, save_blend = parse_args()
    background = bpy.app.background
    scene, coll = clean_scene()

    ref = Reference(find_reference())
    skin_img = texture_skin()
    boot_img = make_image("Musgo_Bota", 4, 4, lambda x, y: (0.035, 0.10, 0.095, 1.0))   # forro: sombra dentro do manto
    images = [ref.texture("Musgo_Manto"), skin_img, texture_eye(), boot_img, texture_leaf()]
    mats = [
        make_material("Musgo", images[0], emission=0.45, closest=False, cull=True),   # pintura da referência
        make_material("Pele", images[1]),
        make_material("Olhos", images[2], emission=1.0),
        make_material("Bota", images[3], cull=True),
        make_material("Folha", images[4], alpha=True, emission=0.3),
    ]
    mb = build_creature(ref)
    mesh_obj = mb.build(NAME + "_Malha", mats, coll)
    arm = build_armature(coll)
    skin(mesh_obj, arm)

    an = Animator(arm)
    for make in ANIMATIONS:
        make(an)
    arm.animation_data.action = bpy.data.actions["Idle"]
    scene.frame_start, scene.frame_end = 1, 49
    scene.frame_set(1)
    print(f"[Espírito do Musgo] {mb.tri_count()} triângulos, {len(BONES)} ossos, animações: {', '.join(ACTION_NAMES)}")

    if background or "--out" in sys.argv:
        export(out_dir, arm, mesh_obj, images)
        cam, _ = setup_studio(scene, coll)
        arm.animation_data.action = bpy.data.actions["Idle"]
        if do_render:
            render_previews(scene, arm, out_dir, cam)
        if save_blend:
            bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, NAME + ".blend"))
        print("[Espírito do Musgo] exportado em", out_dir)
    else:
        setup_studio(scene, coll)


if __name__ == "__main__":
    main()
