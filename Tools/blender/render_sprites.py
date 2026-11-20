"""Adventure King sprite renderer: rigged 3D (Meshy) -> pixel-art sprite strips.

blender -b --factory-startup -P render_sprites.py -- <spec.json> [only_anim,...]

Pipeline per frame: cel-shaded EEVEE render at SS x resolution -> alpha-weighted box downsample ->
hard alpha -> shared per-character palette (k-means) -> 1 px selective dark outline.
Every character uses the same camera angle, light rig, ramp and outline rule so the cast reads as
one art style. Root motion is removed (gameplay physics moves the sprite).
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
SPEC_PATH = ARGS[0]
ONLY = set(ARGS[1].split(",")) if len(ARGS) > 1 and ARGS[1] else None
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
with open(SPEC_PATH, encoding="utf-8-sig") as f:
    SPEC = json.load(f)

SS = SPEC.get("supersample", 4)
W, H = SPEC["canvas"]
PIVOT = SPEC["pivot"]                  # pixel (x from left, y from bottom) where the root sits
PX_PER_M = SPEC["px_per_m"]
OUT = os.path.join(ROOT, SPEC["out"])
TMP = os.path.join(ROOT, "ArtSource", "_render_tmp", SPEC["name"])
os.makedirs(OUT, exist_ok=True)
os.makedirs(TMP, exist_ok=True)


def p(path):
    return path if os.path.isabs(path) else os.path.join(ROOT, path)


# ---------------------------------------------------------------- scene
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
bpy.ops.import_scene.gltf(filepath=p(SPEC["glb"]))
for o in list(scene.objects):
    if o.type == "MESH" and o.name.lower().startswith("icosphere"):
        bpy.data.objects.remove(o, do_unlink=True)
arm = next((o for o in scene.objects if o.type == "ARMATURE"), None)
meshes = [o for o in scene.objects if o.type == "MESH"]


def make_toon(mat, rim=True):
    """Replace a glTF material with a stepped (cel) shader that keeps the painted base colour."""
    nt = mat.node_tree
    tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None)
    base_rgb = None
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf and not tex:
        base_rgb = tuple(bsdf.inputs["Base Color"].default_value)
    for n in list(nt.nodes):
        if n != tex:
            nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
    diff.inputs["Color"].default_value = (1, 1, 1, 1)
    s2r = nt.nodes.new("ShaderNodeShaderToRGB")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    els = ramp.color_ramp.elements
    steps = SPEC.get("ramp", [[0.0, 0.50], [0.18, 0.74], [0.45, 1.0]])
    els[0].position, els[0].color = steps[0][0], (steps[0][1],) * 3 + (1,)
    els[1].position, els[1].color = steps[-1][0], (steps[-1][1],) * 3 + (1,)
    for pos, v in steps[1:-1]:
        e = els.new(pos)
        e.color = (v, v, v, 1)
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = 1.0
    nt.links.new(diff.outputs[0], s2r.inputs[0])
    nt.links.new(s2r.outputs["Color"], ramp.inputs["Fac"])
    if tex:
        nt.links.new(tex.outputs["Color"], mul.inputs["A"])
    else:
        mul.inputs["A"].default_value = base_rgb or (0.8, 0.8, 0.8, 1)
    nt.links.new(ramp.outputs["Color"], mul.inputs["B"])
    color = mul.outputs["Result"]
    if rim:
        lw = nt.nodes.new("ShaderNodeLayerWeight")
        lw.inputs["Blend"].default_value = 0.35
        rr = nt.nodes.new("ShaderNodeMath")
        rr.operation = "GREATER_THAN"
        rr.inputs[1].default_value = SPEC.get("rim_threshold", 0.72)
        rs = nt.nodes.new("ShaderNodeMath")
        rs.operation = "MULTIPLY"
        rs.inputs[1].default_value = SPEC.get("rim_strength", 0.10)
        add = nt.nodes.new("ShaderNodeMix")
        add.data_type = "RGBA"
        add.blend_type = "ADD"
        nt.links.new(lw.outputs["Facing"], rr.inputs[0])
        nt.links.new(rr.outputs[0], rs.inputs[0])
        nt.links.new(rs.outputs[0], add.inputs["Factor"])
        nt.links.new(color, add.inputs["A"])
        add.inputs["B"].default_value = (1.0, 0.95, 0.85, 1)
        color = add.outputs["Result"]
    emit = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(color, emit.inputs["Color"])
    nt.links.new(emit.outputs[0], out.inputs["Surface"])


# ---------------------------------------------------------------- attachments (e.g. sword)
for att in SPEC.get("attach", []):
    before = set(scene.objects)
    bpy.ops.import_scene.gltf(filepath=p(att["glb"]))
    new = [o for o in scene.objects if o not in before]
    parts = [o for o in new if o.type == "MESH"]
    for o in new:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # normalise: longest axis -> +Y (blade direction), grip end at origin
    vs = np.array([v.co[:] for v in obj.data.vertices])
    mn, mx = vs.min(0), vs.max(0)
    axis = int(np.argmax(mx - mn))
    length = (mx - mn)[axis]
    s = att["length_m"] / length
    grip_frac = att.get("grip_frac", 0.86)      # position of the grip centre along the long axis
    sign = -1 if att.get("blade_negative", True) else 1
    M = Matrix.Identity(4)
    # map chosen axis to +Y with blade pointing +Y
    if axis == 0:
        M = Matrix.Rotation(math.radians(90 * (1 if sign < 0 else -1)), 4, "Z")
    elif axis == 2:
        M = Matrix.Rotation(math.radians(-90 * (1 if sign < 0 else -1)), 4, "X")
    elif sign > 0:
        M = Matrix.Rotation(math.radians(180), 4, "Z")
    centre = (mn + mx) / 2
    grip = centre.copy()
    grip[axis] = mn[axis] + (mx - mn)[axis] * grip_frac if sign < 0 else mx[axis] - (mx - mn)[axis] * grip_frac
    obj.data.transform(Matrix.Translation(Vector(-grip)))
    obj.data.transform(M)
    obj.data.transform(Matrix.Scale(s, 4))
    if att.get("flip_roll"):
        obj.data.transform(Matrix.Rotation(math.radians(att["flip_roll"]), 4, "Y"))
    obj.parent = arm
    obj.parent_type = "BONE"
    obj.parent_bone = att["bone"]
    bone = arm.data.bones[att["bone"]]
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.location = Vector(att.get("loc", [0, -bone.length * 0.5, 0]))
    obj.rotation_euler = [math.radians(a) for a in att.get("rot", [0, 0, 0])]
    meshes.append(obj)

for o in meshes:
    for slot in o.material_slots:
        if slot.material and slot.material.use_nodes:
            slot.material = slot.material.copy()
            make_toon(slot.material, rim=SPEC.get("rim", True))
    for mod in list(o.modifiers):
        if mod.type == "SUBSURF":
            o.modifiers.remove(mod)

# ---------------------------------------------------------------- render settings
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x, scene.render.resolution_y = W * SS, H * SS
scene.render.film_transparent = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"
scene.render.filter_size = 0.5
world = bpy.data.worlds.new("w")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[1].default_value = 0.0

yaw = math.radians(SPEC.get("yaw", -62))          # -90 = pure side view facing screen-right
pitch = math.radians(SPEC.get("pitch", 4))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
scene.collection.objects.link(cam)
scene.camera = cam
cam.data.type = "ORTHO"
cam.data.ortho_scale = (W * SS) / (PX_PER_M * SS)  # metres across the frame width
dist = 20
cam_dir = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
cam.rotation_euler = (math.radians(90) - pitch, 0, yaw)
# frame so that world origin (character root on the ground) lands on PIVOT
right = Vector((math.cos(yaw), math.sin(yaw), 0))
up = cam.rotation_euler.to_matrix() @ Vector((0, 1, 0))
off_x = (W / 2 - PIVOT[0]) / PX_PER_M
off_y = (H / 2 - PIVOT[1]) / PX_PER_M
cam.location = cam_dir * dist + right * off_x + up * off_y

def sun(name, energy, rot, color=(1, 1, 1)):
    l = bpy.data.objects.new(name, bpy.data.lights.new(name, "SUN"))
    l.data.energy = energy
    l.data.color = color
    l.rotation_euler = [math.radians(a) for a in rot]
    scene.collection.objects.link(l)

# key light from upper-left-front relative to the camera, so every sprite shares lighting
sun("key", SPEC.get("key_energy", 4.0), (52, 0, math.degrees(yaw) - 38), (1.0, 0.97, 0.9))
sun("fill", SPEC.get("fill_energy", 1.2), (75, 0, math.degrees(yaw) + 95), (0.85, 0.9, 1.0))

if arm and arm.animation_data:
    for t in list(arm.animation_data.nla_tracks):
        arm.animation_data.nla_tracks.remove(t)
elif arm:
    arm.animation_data_create()


def set_action(name):
    act = bpy.data.actions[name]
    arm.animation_data.action = act
    if hasattr(act, "slots") and len(act.slots) and hasattr(arm.animation_data, "action_slot"):
        arm.animation_data.action_slot = act.slots[0]


def hips_world():
    pb = arm.pose.bones[SPEC.get("root_bone", "Hips")]
    return arm.matrix_world @ pb.head


# ---------------------------------------------------------------- render + post-process
def render_png(path):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    a = np.array(img.pixels[:], dtype=np.float32).reshape(img.size[1], img.size[0], 4)
    bpy.data.images.remove(img)
    return a[::-1]  # top row first


def downsample(a):
    h, w = a.shape[0] // SS, a.shape[1] // SS
    blk = a.reshape(h, SS, w, SS, 4)
    alpha = blk[..., 3]
    wsum = alpha.sum(axis=(1, 3))
    rgb = (blk[..., :3] * alpha[..., None]).sum(axis=(1, 3)) / np.maximum(wsum[..., None], 1e-6)
    out = np.zeros((h, w, 4), np.float32)
    out[..., :3] = rgb
    out[..., 3] = (wsum / (SS * SS) >= SPEC.get("alpha_cut", 0.45)).astype(np.float32)
    return out


frames = {}  # anim -> list of (h,w,4)
anims = SPEC["anims"]
for name, a in anims.items():
    if ONLY and name not in ONLY:
        continue
    set_action(a["action"])
    act = bpy.data.actions[a["action"]]
    start = a.get("start", act.frame_range[0])
    end = a.get("end", act.frame_range[1])
    n = a["frames"]
    loop = a.get("loop", True)
    times = [start + (end - start) * i / (n if loop else max(1, n - 1)) for i in range(n)]
    lock = a.get("lock", "xy")
    arm.location = (0, 0, 0)
    scene.frame_set(int(start), subframe=start - int(start))
    ref = hips_world()
    ground_ref = a.get("ground", None)
    out_frames = []
    for i, t in enumerate(times):
        arm.location = (0, 0, 0)
        scene.frame_set(int(t), subframe=t - int(t))
        cur = hips_world()
        d = cur - ref
        comp = Vector((d.x if "x" in lock else 0, d.y if "y" in lock else 0, d.z if "z" in lock else 0))
        arm.location = -comp + Vector(a.get("offset", [0, 0, 0]))
        scene.frame_set(int(t), subframe=t - int(t))
        raw = render_png(os.path.join(TMP, f"{name}_{i:02d}.png"))
        out_frames.append(downsample(raw))
    frames[name] = out_frames
    print("RENDERED", name, len(out_frames), flush=True)


# shared palette across every frame of this character
def kmeans(x, k, iters=18, seed=3):
    rng = np.random.default_rng(seed)
    c = x[rng.choice(len(x), size=min(k, len(x)), replace=False)]
    for _ in range(iters):
        d = ((x[:, None, :] - c[None, :, :]) ** 2).sum(-1)
        lab = d.argmin(1)
        for j in range(len(c)):
            m = lab == j
            if m.any():
                c[j] = x[m].mean(0)
    return c


pal_path = os.path.join(OUT, f"{SPEC['name']}_palette.npy")
allpx = np.concatenate([f[f[..., 3] > 0][:, :3] for fl in frames.values() for f in fl])
if SPEC.get("reuse_palette") and os.path.exists(pal_path):
    palette = np.load(pal_path)
else:
    sample = allpx[np.random.default_rng(1).choice(len(allpx), size=min(60000, len(allpx)), replace=False)]
    palette = kmeans(sample.copy(), SPEC.get("colors", 40))
    np.save(pal_path, palette)

OUTLINE_TINT = np.array(SPEC.get("outline_tint", [0.16, 0.10, 0.16]), np.float32)


def stylise(f):
    f = f.copy()
    m = f[..., 3] > 0
    px = f[m][:, :3]
    d = ((px[:, None, :] - palette[None, :, :]) ** 2).sum(-1)
    f[m, :3] = palette[d.argmin(1)]
    # selective outline: transparent pixels touching the body take a darkened neighbour colour
    op = m
    pad = np.pad(op, 1)
    neigh = pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]
    edge = neigh & ~op
    if edge.any():
        cols = np.zeros_like(f[..., :3]); cnt = np.zeros(f.shape[:2], np.float32)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            sh = np.roll(np.roll(f, dy, 0), dx, 1)
            mm = sh[..., 3] > 0
            cols += sh[..., :3] * mm[..., None]; cnt += mm
        nc = cols / np.maximum(cnt[..., None], 1)
        f[edge, :3] = nc[edge] * 0.28 + OUTLINE_TINT
        f[edge, 3] = 1
    return f


def save_png(arr, path):
    h, w = arr.shape[:2]
    img = bpy.data.images.new("o", w, h, alpha=True)
    img.pixels[:] = arr[::-1].ravel()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


meta = {"name": SPEC["name"], "frame_size": [W, H], "pivot_px": PIVOT, "px_per_unit": PX_PER_M, "anims": {}}
meta_path = os.path.join(OUT, f"{SPEC['name']}_anims.json")
if os.path.exists(meta_path):
    with open(meta_path, encoding="utf-8") as f:
        meta["anims"] = json.load(f).get("anims", {})
for name, fl in frames.items():
    strip = np.concatenate([stylise(f) for f in fl], axis=1)
    save_png(strip, os.path.join(OUT, f"{SPEC['name']}_{name}.png"))
    a = anims[name]
    meta["anims"][name] = {"frames": len(fl), "fps": a.get("fps", 12), "loop": a.get("loop", True),
                           "events": a.get("events", {})}
with open(meta_path, "w", encoding="utf-8") as f:
    json.dump(meta, f, indent=1)

# review sheet (x3 nearest, dark/light checker) - not shipped
rows = []
for name, fl in frames.items():
    strip = np.concatenate([stylise(f) for f in fl], axis=1)
    rows.append(strip)
maxw = max(r.shape[1] for r in rows)
sheet = np.concatenate([np.pad(r, ((0, 0), (0, maxw - r.shape[1]), (0, 0))) for r in rows], axis=0)
bg = np.zeros_like(sheet); bg[..., :3] = (0.55, 0.72, 0.85); bg[..., 3] = 1
comp = sheet * sheet[..., 3:4] + bg * (1 - sheet[..., 3:4]); comp[..., 3] = 1
comp = comp.repeat(SPEC.get("review_zoom", 2), 0).repeat(SPEC.get("review_zoom", 2), 1)
save_png(comp, os.path.join(TMP, "review_sheet.png"))
print("DONE", OUT, flush=True)
