"""Contact sheet of every action in a rigged GLB (one row per action, N evenly spaced frames).
blender -b --factory-startup -P action_sheet.py -- <in.glb> <out.png> [frames=8] [cell=180] [yaw=-60]
Used for animation review / choosing clip ranges. Not a shipped asset.
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
src, out = a[0], a[1]
N = int(a[2]) if len(a) > 2 else 8
CELL = int(a[3]) if len(a) > 3 else 180
YAW = math.radians(float(a[4]) if len(a) > 4 else -60)
ORTHO = float(a[5]) if len(a) > 5 else 2.2
ONLY = set(a[6].split(",")) if len(a) > 6 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
sc = bpy.context.scene
for o in list(sc.objects):
    if o.type == "MESH" and o.name.lower().startswith("icosphere"):
        bpy.data.objects.remove(o, do_unlink=True)
arm = next(o for o in sc.objects if o.type == "ARMATURE")
if arm.animation_data:
    for t in list(arm.animation_data.nla_tracks):
        arm.animation_data.nla_tracks.remove(t)
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x = sc.render.resolution_y = CELL
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[1].default_value = 0.6
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = ORTHO
cam.rotation_euler = (math.radians(90), 0, YAW)
cam.location = Vector((math.sin(YAW) * 10, -math.cos(YAW) * 10, 0.5))
sun = bpy.data.objects.new("s", bpy.data.lights.new("s", "SUN")); sun.data.energy = 3
sun.rotation_euler = (math.radians(50), 0, YAW - math.radians(30)); sc.collection.objects.link(sun)

rows = []
tmp = os.path.join(os.path.dirname(out), "_cell.png")
for act in sorted(bpy.data.actions, key=lambda x: x.name):
    if ONLY and act.name not in ONLY:
        continue
    arm.animation_data_create().action = act
    if hasattr(act, "slots") and len(act.slots):
        arm.animation_data.action_slot = act.slots[0]
    s, e = act.frame_range
    cells = []
    for i in range(N):
        f = s + (e - s) * i / max(1, N - 1)
        sc.frame_set(int(f), subframe=f - int(f))
        sc.render.filepath = tmp
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(tmp)
        px = np.array(img.pixels[:], np.float32).reshape(CELL, CELL, 4)[::-1]
        bpy.data.images.remove(img)
        bg = np.ones_like(px); bg[..., :3] = (0.82, 0.86, 0.9)
        cells.append(px * px[..., 3:4] + bg * (1 - px[..., 3:4]))
    row = np.concatenate(cells, 1)
    # label strip: encode action index as a dark bar length (names printed to stdout)
    rows.append(row)
    print("ROW", len(rows) - 1, act.name, f"{s:.0f}-{e:.0f}")
sheet = np.concatenate(rows, 0)
sheet[..., 3] = 1
img = bpy.data.images.new("sheet", sheet.shape[1], sheet.shape[0], alpha=True)
img.pixels[:] = sheet[::-1].ravel()
img.filepath_raw = out; img.file_format = "PNG"; img.save()
