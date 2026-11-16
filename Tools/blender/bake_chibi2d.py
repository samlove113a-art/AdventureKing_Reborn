"""AdventureKing 2D chibi baker: the rigged 3D hero / NPC / monster FBX -> side-view sprite strips (HD, not pixel art).

blender -b --factory-startup -P bake_chibi2d.py -- <spec.json> [only_clip,...]

* orthographic SIDE camera, one fixed framing for the whole cast (feet sit on the same baseline, same px per metre)
* toon-lit render with transparent background, then a 3 px dark outline + soft inner rim in numpy
* one PNG strip per clip (frames side by side) + <name>_frames.json (fps, loop, frame count) for the Unity importer
* locomotion loops follow the hips horizontally (no sliding feet; gameplay physics moves the sprite)
* never touches the source FBX / GLB: the 3D model stays the master, 2D is generated from it
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
with open(ARGS[0], encoding="utf-8-sig") as f:
    SPEC = json.load(f)
ONLY = set(ARGS[1].split(",")) if len(ARGS) > 1 and ARGS[1] else None


def p(x):
    return x if os.path.isabs(x) else os.path.join(ROOT, x)


CELL = SPEC.get("cell", 384)            # px per frame (square)
SCALE_M = SPEC.get("frame_m", 1.9)      # metres covered by one frame side
BASE_M = SPEC.get("ground_from_bottom_m", 0.12)
OUTLINE = SPEC.get("outline_px", 3)
OUT = p(SPEC["out"])
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
bpy.ops.import_scene.fbx(filepath=p(SPEC["fbx"]))
arm = next(o for o in sc.objects if o.type == "ARMATURE")
meshes = [o for o in sc.objects if o.type == "MESH"]
for o in meshes:
    if o.name in SPEC.get("hide", []):
        o.hide_render = True

# --- toon look: keep the painted base colour, step the light into 3 bands
for mat in {s.material for o in meshes for s in o.material_slots if s.material}:
    if not mat.use_nodes:
        continue
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf:
        bsdf.inputs["Roughness"].default_value = 1.0
        for name in ("Specular IOR Level", "Specular"):
            if name in bsdf.inputs:
                bsdf.inputs[name].default_value = 0.0

sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x = sc.render.resolution_y = CELL
sc.render.resolution_percentage = 100
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
world = bpy.data.worlds.new("w"); sc.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.82, 0.86, 1.0, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.85
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
sun.data.energy = 2.6
sun.rotation_euler = (math.radians(48), math.radians(8), math.radians(SPEC.get("sun_yaw", 35)))
cam_d = bpy.data.cameras.new("cam"); cam_d.type = "ORTHO"; cam_d.ortho_scale = SCALE_M
cam = bpy.data.objects.new("cam", cam_d); sc.collection.objects.link(cam); sc.camera = cam
# camera on +X looking at -X: the model's right side faces the camera; SPEC["flip"] mirrors if it faces left
SIDE = SPEC.get("cam_side", 1)
cam.rotation_euler = (math.radians(90), 0, math.radians(90 * SIDE))
cam_z = -BASE_M + SCALE_M / 2

hips_name = SPEC.get("hips", "Hips")


def action_by_name(name):
    for a in bpy.data.actions:
        if a.name.split("|")[-1] == name or a.name == name:
            return a
    raise KeyError(name)


def hips_y(frame):
    sc.frame_set(int(frame))
    b = arm.pose.bones.get(hips_name)
    if not b:
        return 0.0
    return (arm.matrix_world @ b.head).y


def outline(px, ring):
    """px: (H,W,4) float. Adds a dark outline `ring` px wide behind the sprite and lifts the inner colour slightly."""
    a = px[..., 3] > 0.5
    pad = np.pad(a, ring)
    grown = pad.copy()
    for dy in range(-ring, ring + 1):
        for dx in range(-ring, ring + 1):
            if dx * dx + dy * dy <= ring * ring:
                grown |= np.roll(np.roll(pad, dy, 0), dx, 1)
    grown = grown[ring:-ring, ring:-ring]
    out = np.zeros_like(px)
    ink = np.array(SPEC.get("ink", [0.16, 0.1, 0.22]), np.float32)
    out[grown, :3] = ink
    out[grown, 3] = 1.0
    src = px[..., 3:4]
    out[..., :3] = out[..., :3] * (1 - src) + px[..., :3] * src
    out[..., 3] = np.maximum(out[..., 3], px[..., 3])
    return out


frames_meta = {}
for clip in SPEC["clips"]:
    name = clip["name"]
    if ONLY and name not in ONLY:
        continue
    act = action_by_name(clip["action"])
    arm.animation_data_create().action = act
    if hasattr(act, "slots") and len(act.slots):
        arm.animation_data.action_slot = act.slots[0]
    f0, f1 = clip["from"] + 1, clip["to"] + 1          # Unity clip ranges are 0-based
    n = clip["frames"]
    follow = clip.get("follow", False)
    base_h = hips_y(f0)
    strip = []
    for i in range(n):
        fr = f0 + (f1 - f0) * i / max(1, (n if clip.get("loop") else n - 1))
        sc.frame_set(int(fr), subframe=fr - int(fr))
        cx = hips_y(fr) if follow else base_h
        cam.location = Vector((10 * SIDE, cx, cam_z))
        sun.location = Vector((5 * SIDE, cx, 3))
        tmp = os.path.join(OUT, "_tmp.png")
        sc.render.filepath = tmp
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(tmp)
        px = np.array(img.pixels[:], np.float32).reshape(CELL, CELL, 4)[::-1].copy()   # to top-left origin
        bpy.data.images.remove(img)
        if SPEC.get("flip"):
            px = px[:, ::-1].copy()
        strip.append(outline(px, OUTLINE))
    sheet = np.concatenate(strip, 1)
    out_img = bpy.data.images.new(name, sheet.shape[1], sheet.shape[0], alpha=True)
    out_img.pixels[:] = sheet[::-1].ravel()
    path = os.path.join(OUT, f"{SPEC['name']}_{name}.png")
    out_img.filepath_raw = path
    out_img.file_format = "PNG"
    out_img.save()
    frames_meta[name] = {"frames": n, "fps": clip.get("fps", 12), "loop": bool(clip.get("loop")), "file": os.path.basename(path)}
    print("BAKED", name, n, path)

meta_path = os.path.join(OUT, f"{SPEC['name']}_frames.json")
old = {}
if os.path.exists(meta_path):
    with open(meta_path, encoding="utf-8") as f:
        old = json.load(f)
old.update({"cell": CELL, "frame_m": SCALE_M, "ground_from_bottom_m": BASE_M, "clips": {**old.get("clips", {}), **frames_meta}})
with open(meta_path, "w", encoding="utf-8") as f:
    json.dump(old, f, indent=2)
try:
    os.remove(os.path.join(OUT, "_tmp.png"))
except OSError:
    pass
