"""Meshy model -> Blender -> chibi pixel-art sprites (side view). No new Meshy credits: renders the hero models that already exist.

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

HEAD_SCALE = {"Mage": 1.45, "Swordsman": 1.3, "Archer": 1.35, "Assassin": 1.4}.get(CLS, 1.2)
FRAME_M = {"Mage": 1.6, "Swordsman": 1.65, "Assassin": 1.75}.get(CLS, 1.5)   # metres per frame side (wider for the classes whose attack / skill poses touched the frame edge)
BASE = 0.07                    # ground line above the bottom edge (m)
RES, PX = 768, 192            # 192 px frames: faces, eyes and weapons stay readable (was 128)
OUTLINE = (0.12, 0.06, 0.2)

# follow: False = fixed camera, "x" = follow the hips sideways (root motion), "xz" = also keep the hips at the same height (the game moves the body:
# a jump or a dash must not move the sprite twice)
SHARED = [
    ("Run", "Run_02", 0, 21, 8, 14, True, "x"), ("Jump", "Regular_Jump", 12, 19, 4, 10, False, "xz"),
    ("Fall", "Regular_Jump", 19, 25, 3, 8, False, "xz"), ("Climb", "Ladder_Climb_Loop", 1, 48, 6, 8, True, False),
    ("Hit", "Hit_Reaction", 0, 30, 4, 12, False, False), ("Death", "dying_backwards", 0, 53, 6, 6, False, "x")]
SPEC = {   # class -> (idle take, idle last, attack, skill)
    "Mage": (("Idle_02", 56), ("mage_soell_cast", 0, 40), ("Charged_Spell_Cast", 0, 62)),
    "Swordsman": (("Idle_02", 56), ("Right_Hand_Sword_Slash", 0, 30), ("Triple_Combo_Attack", 0, 70)),
    "Assassin": (("Combat_Stance", 50), ("Double_Combo_Attack", 4, 36), ("Double_Blade_Spin", 0, 56)),
    "Archer": (("Idle_02", 56), ("Archery_Shot_3", 0, 16), ("Archery_Shot", 24, 76)),
}
idle, att, skl = SPEC[CLS]
CLIPS = ([("Idle", idle[0], 0, idle[1], 6, 8, True, False)] + SHARED +
         [("Attack", att[0], att[1], att[2], 6, 14, False, False), ("Skill", skl[0], skl[1], skl[2], 8, 12, False, "xz")])

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
FBX = os.path.join(ROOT, ARGS[2]) if len(ARGS) > 2 else os.path.join(ROOT, "Assets", "Art", "Characters", CLS, f"{CLS}.fbx")   # new Meshy model (from the owner references) or the old one
bpy.ops.import_scene.fbx(filepath=FBX)
arm = next(o for o in sc.objects if o.type == "ARMATURE")
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="POSE")
head = arm.pose.bones["Head"]
con = head.constraints.new("LIMIT_SCALE")
for ax in "xyz":
    setattr(con, f"use_min_{ax}", True)
    setattr(con, f"use_max_{ax}", True)
    setattr(con, f"min_{ax}", HEAD_SCALE)
    setattr(con, f"max_{ax}", HEAD_SCALE)
con.owner_space = "LOCAL"
bpy.ops.object.mode_set(mode="OBJECT")

# ---- Meshy dumps the reference picture's ground (grass base, stones, mushrooms) into the model: cut everything under / beside the feet
foot_bones = [b for b in arm.data.bones if b.name in ("LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase")]
pts = [arm.matrix_world @ p for b in foot_bones for p in (b.head_local, b.tail_local)]
sole = min(p.z for p in pts) - 0.035
fx0, fx1 = min(p.x for p in pts) - 0.14, max(p.x for p in pts) + 0.14
fy0, fy1 = min(p.y for p in pts) - 0.16, max(p.y for p in pts) + 0.16
removed = 0
for ob in [o for o in sc.objects if o.type == "MESH" and not o.name.startswith("Weapon_")]:
    bm = bmesh.new(); bm.from_mesh(ob.data)
    kill = []
    for v in bm.verts:
        w = ob.matrix_world @ v.co
        under = w.z < sole + 0.012
        beside = w.z < sole + 0.17 and not (fx0 <= w.x <= fx1 and fy0 <= w.y <= fy1)
        if under or beside:
            kill.append(v)
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    removed += len(kill)
    bm.to_mesh(ob.data); bm.free()
print("GROUND CUT", CLS, "sole z %.3f" % sole, "verts removed", removed, flush=True)

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


def hips_pos(frame):
    sc.frame_set(int(frame))
    h = arm.matrix_world @ arm.pose.bones["Hips"].head
    return h.y, h.z


def hips_y(frame):
    return hips_pos(frame)[0]


UPRIGHT = {"Idle"}      # standing clips: head must sit over the feet (Meshy's stance / idle takes lean the torso forward)
SPINE = next((n for n in ("Spine", "Spine01", "Spine02") if n in arm.pose.bones), None)


def lean_dy():
    """Horizontal offset (m, image x) of the head (top 35 % of the silhouette) from the feet (bottom 12 %), measured on a quick render:
    the bones are not enough - Meshy's heads sit forward of the neck bone."""
    old = (sc.render.resolution_x, sc.render.resolution_y)
    sc.render.resolution_x = sc.render.resolution_y = 256
    sc.render.filepath = os.path.join(OUT, "_l.png")
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(sc.render.filepath)
    a = np.array(img.pixels[:], np.float32).reshape(256, 256, 4)[::-1, :, 3] > 0.5
    bpy.data.images.remove(img)
    sc.render.resolution_x, sc.render.resolution_y = old
    ys, xs = np.nonzero(a)
    top, bot = ys.min(), ys.max()
    h = max(1, bot - top)
    px_m = FRAME_M / 256.0
    return float((xs[ys < top + h * 0.35].mean() - xs[ys > bot - h * 0.12].mean()) * px_m)


FIX = None


def straighten():
    """Rotate the spine about the camera axis until the head is over the feet. The correction is a constraint on the spine bone
    (copy-rotation from a helper empty, added after the animation, world space): the animation channels would overwrite direct edits."""
    global FIX
    if not SPINE:
        return
    if FIX is None:
        FIX = bpy.data.objects.new("lean_fix", None)
        sc.collection.objects.link(FIX)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.mode_set(mode="POSE")
        con = arm.pose.bones[SPINE].constraints.new("COPY_ROTATION")
        con.target = FIX
        con.mix_mode = "AFTER"
        con.target_space = "WORLD"
        con.owner_space = "WORLD"
        bpy.ops.object.mode_set(mode="OBJECT")

    def put(a):
        FIX.rotation_euler = (a, 0.0, 0.0)
        bpy.context.view_layer.update()
        return lean_dy()
    ang, cur = 0.0, put(0.0)
    for _ in range(6):
        if abs(cur) < 0.012:
            break
        step = math.atan2(cur, 0.45)
        cands = [(abs(put(ang + s * step)), ang + s * step) for s in (1, -1)]
        d, ang = min(cands)
        cur = put(ang)
        print("  spine", SPINE, "angle %.3f lean %.3f" % (ang, cur), flush=True)

def render_frame():
    path = os.path.join(OUT, "_t.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    a = np.array(img.pixels[:], np.float32).reshape(RES, RES, 4)[::-1].copy()
    bpy.data.images.remove(img)
    return a


def standing_z():
    """Hips height of the standing idle pose: "xz" clips keep the hips at exactly this height (the game moves the body, the sprite stays put)."""
    act = action(idle[0])
    arm.animation_data_create().action = act
    if hasattr(act, "slots") and len(act.slots):
        arm.animation_data.action_slot = act.slots[0]
    return hips_pos(1)[1]


STAND_Z = standing_z()
raw = {}
for name, take, f0, f1, n, fps, loop, follow in CLIPS:
    if ONLY and name not in ONLY:
        continue
    act = action(take)
    arm.animation_data_create().action = act
    if hasattr(act, "slots") and len(act.slots):
        arm.animation_data.action_slot = act.slots[0]
    a0, a1 = f0 + 1, f1 + 1
    base_h, base_z = hips_pos(a0)[0], STAND_Z
    frames = []
    for i in range(n):
        fr = a0 + (a1 - a0) * i / (n if loop else max(1, n - 1))
        sc.frame_set(int(fr), subframe=fr - int(fr))
        if name in UPRIGHT:
            before = lean_dy()
            straighten()
            print("UPRIGHT", CLS, name, i, "lean m %.3f -> %.3f" % (before, lean_dy()), flush=True)
        hy, hz = hips_pos(fr) if follow else (base_h, base_z)
        cx = hy if follow else base_h
        dz = (hz - base_z) if follow == "xz" else 0.0
        cam.location = Vector((-10, cx, cam_z + dz))
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
