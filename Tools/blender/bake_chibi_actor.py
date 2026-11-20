"""NPC / monster variant of bake_chibi_pixel.py (same chibi pixel look, whole-take clips, Meshy models that already exist: no credits).\n\nMeshy model -> Blender -> chibi pixel-art sprites (side view). No new Meshy credits: renders the hero models that already exist.

  blender -b --factory-startup -P bake_chibi_pixel.py -- <Class> [only_clip,...]

* the rigged Meshy FBX (Assets/Art/Characters/<Class>/<Class>.fbx, weapon skinned to the hand) is posed with the game's own clip table
* chibi proportions are made in Blender: a LIMIT_SCALE constraint enlarges the Head bone (hair / hat / face follow), the body stays small
* orthographic side camera, toon-lit transparent render at 512 px, then PIXEL conversion: one shared palette per class (k-means),
  majority colour per 4x4 block, hard alpha, 1 px dark outline  ->  128 px frames, feet on one baseline
* output: ArtSource/Chibi2D/<Class>/<Class>_<Clip>.png strips + <Class>_frames.json (the Unity importer Chibi2DArt reads these)
"""
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
CLS = ARGS[0]
ONLY = set(ARGS[1].split(",")) if len(ARGS) > 1 and ARGS[1] not in ("", "-") else None   # "-" = every clip
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "ArtSource", "Chibi2D", CLS)
os.makedirs(OUT, exist_ok=True)

HEAD_SCALE = {"NpcElder": 1.3, "NpcSmith": 1.3, "NpcStorekeeper": 1.3, "NpcCaptain": 1.3, "NpcOfficiant": 1.3, "NpcMusician": 1.3, "NpcMushroomElder": 1.3}.get(CLS, 1.0)
FRAME_M = 1.5
BASE = 0.07
RES, PX = 512, 128
OUTLINE = (0.12, 0.06, 0.2)
# name -> list of (clip, take, frames, fps, loop); frame range = the whole take
NPC_CLIPS = lambda wave: [("Idle", "Idle", 6, 8, True), ("Walk", "Casual_Walk", 8, 10, True), ("Wave", wave, 6, 10, False),
                          ("Talk", "Talk_with_Hands_Open", 6, 8, True), ("Bow", "Formal_Bow", 6, 8, False)]
TABLE = {
    "NpcElder": NPC_CLIPS("Big_Wave_Hello"), "NpcSmith": NPC_CLIPS("Wave_One_Hand"), "NpcStorekeeper": NPC_CLIPS("Wave_One_Hand"),
    "NpcCaptain": NPC_CLIPS("Wave_One_Hand"), "NpcOfficiant": NPC_CLIPS("Big_Wave_Hello"), "NpcMusician": NPC_CLIPS("Wave_One_Hand"),
    "NpcMushroomElder": NPC_CLIPS("Wave_One_Hand"),
    "MonsterNamNon": [("Idle", "Idle", 6, 8, True), ("Walk", "Casual_Walk", 8, 10, True), ("Attack", "Right_Jab_from_Guard", 6, 12, False),
                      ("Hit", "Hit_Reaction", 4, 12, False), ("Death", "Dead", 6, 8, False)],
    "PollenSlime": [("Idle", None, 1, 1, True)],      # a prop without a rig: one frame, the game squashes / hops it
}
CLIPS = [(n, t, 0, 0, fr, fps, lp, False) for n, t, fr, fps, lp in TABLE[CLS]]
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
FBX = os.path.join(ROOT, ARGS[2]) if len(ARGS) > 2 else os.path.join(ROOT, "Assets", "Art", "Characters", CLS, f"{CLS}.fbx")   # new Meshy model (from the owner references) or the old one
bpy.ops.import_scene.fbx(filepath=FBX)
if CLS == "PollenSlime":                         # a prop: turn its face about 3/4 toward the side camera
    for o in [o for o in sc.objects if o.type == "MESH" and o.parent is None]:
        o.rotation_euler.z += math.radians(-60)
arm = next((o for o in sc.objects if o.type == "ARMATURE"), None)
if arm and HEAD_SCALE != 1.0 and "Head" in arm.pose.bones:
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    con = arm.pose.bones["Head"].constraints.new("LIMIT_SCALE")
    for ax in "xyz":
        setattr(con, f"use_min_{ax}", True)
        setattr(con, f"use_max_{ax}", True)
        setattr(con, f"min_{ax}", HEAD_SCALE)
        setattr(con, f"max_{ax}", HEAD_SCALE)
    con.owner_space = "LOCAL"
    bpy.ops.object.mode_set(mode="OBJECT")

sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x = sc.render.resolution_y = RES
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
world = bpy.data.worlds.new("w"); sc.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.85, 0.88, 1.0, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
sun.data.energy = 2.4
sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
cd = bpy.data.cameras.new("cam"); cd.type = "ORTHO"; cd.ortho_scale = FRAME_M
cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
cam.rotation_euler = (math.radians(90), 0, math.radians(-90))
cam_z = -BASE + FRAME_M / 2


def action(name):
    for a in bpy.data.actions:
        if a.name.split("|")[-1] == name:
            return a
    raise KeyError(name)


def hips_y(frame):
    sc.frame_set(int(frame))
    return 0.0


def render_frame():
    path = os.path.join(OUT, "_t.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    a = np.array(img.pixels[:], np.float32).reshape(RES, RES, 4)[::-1].copy()
    bpy.data.images.remove(img)
    return a


raw = {}
for name, take, f0, f1, n, fps, loop, follow in CLIPS:
    if ONLY and name not in ONLY:
        continue
    if take and arm:
        act = action(take)
        arm.animation_data_create().action = act
        if hasattr(act, "slots") and len(act.slots):
            arm.animation_data.action_slot = act.slots[0]
        a0, a1 = act.frame_range
    else:
        a0 = a1 = 1
    base_h = hips_y(a0)
    frames = []
    for i in range(n):
        fr = a0 + (a1 - a0) * i / (n if loop else max(1, n - 1))
        sc.frame_set(int(fr), subframe=fr - int(fr))
        cx = hips_y(fr) if follow else base_h
        cam.location = Vector((-10, cx, cam_z))
        sun.location = Vector((-5, cx, 3))
        frames.append(render_frame())
    raw[name] = (frames, fps, loop)
    print("RENDERED", name, n, flush=True)

# ---- one shared palette for the class (k-means over opaque pixels of every frame)
samples = np.concatenate([f[::4, ::4][f[::4, ::4, 3] > 0.5][:, :3] for fr, _, _ in raw.values() for f in fr])
rng = np.random.default_rng(5)
samples = samples[rng.choice(len(samples), min(50000, len(samples)), replace=False)]
cen = samples[rng.choice(len(samples), 48, replace=False)].copy()
for _ in range(12):
    idx = ((samples[:, None] - cen[None]) ** 2).sum(-1).argmin(1)
    for j in range(len(cen)):
        m = idx == j
        if m.any():
            cen[j] = samples[m].mean(0)
PAL = cen
ink = np.array(OUTLINE, np.float32)


def shift(m, dy, dx):
    out = np.zeros_like(m)
    h, w = m.shape
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = m[ys, xs]
    return out


def pixelate(f):
    a = f[..., 3] > 0.5
    rgb = f[..., :3].reshape(-1, 3)
    idx = np.empty(len(rgb), np.int16)
    for i in range(0, len(rgb), 1 << 17):
        idx[i:i + (1 << 17)] = ((rgb[i:i + (1 << 17), None] - PAL[None]) ** 2).sum(-1).argmin(1)
    idx = idx.reshape(RES, RES)
    b = RES // PX
    counts = np.stack([((idx == k) & a).reshape(PX, b, PX, b).sum(axis=(1, 3)) for k in range(len(PAL))])
    cover = a.reshape(PX, b, PX, b).sum(axis=(1, 3))
    on = cover >= (b * b) * 0.45
    nb = np.zeros_like(on, np.int32)
    nb[1:] += on[:-1]; nb[:-1] += on[1:]; nb[:, 1:] += on[:, :-1]; nb[:, :-1] += on[:, 1:]
    on &= nb > 0
    img = np.zeros((PX, PX, 4), np.float32)
    img[..., :3] = PAL[counts.argmax(0)]
    ring = (on | shift(on, 1, 0) | shift(on, -1, 0) | shift(on, 0, 1) | shift(on, 0, -1)) & ~on
    img[ring, :3] = ink
    img[..., 3] = (on | ring).astype(np.float32)
    img[~(on | ring), :3] = 0
    return img


sys.path.insert(0, os.path.join(ROOT, "Tools", "chibi2d"))
from mage_rig import write_png  # noqa: E402

meta_path = os.path.join(OUT, f"{CLS}_frames.json")
meta = {"cell": PX, "pixel": True, "frame_m": FRAME_M, "ground_from_bottom_m": BASE, "clips": {}}
if os.path.exists(meta_path) and ONLY:
    meta["clips"] = json.load(open(meta_path, encoding="utf-8")).get("clips", {})
prev = []
for name, (frames, fps, loop) in raw.items():
    px = [pixelate(f) for f in frames]
    write_png(os.path.join(OUT, f"{CLS}_{name}.png"), np.concatenate(px, axis=1))
    meta["clips"][name] = {"frames": len(px), "fps": fps, "loop": loop, "file": f"{CLS}_{name}.png"}
    prev.append(np.concatenate(px, axis=1))
json.dump(meta, open(meta_path, "w", encoding="utf-8"), indent=2)
w = max(p.shape[1] for p in prev)
sheet = np.concatenate([np.pad(p, ((0, 0), (0, w - p.shape[1]), (0, 0))) for p in prev], axis=0)
bg = np.zeros(sheet.shape[:2] + (3,), np.float32) + np.array([0.82, 0.88, 0.8], np.float32)
al = sheet[..., 3:4]
write_png(os.path.join(OUT, f"{CLS}_preview.png"), np.repeat(np.repeat(np.concatenate([sheet[..., :3] * al + bg * (1 - al), np.ones(sheet.shape[:2] + (1,), np.float32)], axis=2), 3, 0), 3, 1))
try:
    os.remove(os.path.join(OUT, "_t.png"))
except OSError:
    pass
print("DONE", CLS)
