"""Meshy rigged character (+ animation batches) -> Unity-ready FBX with weapon sockets.

blender -b --factory-startup -P export_character.py -- <spec.json> [qa]

* merges every action from all animation GLBs onto one armature
* creates Socket_R / Socket_L empties on the hand bones (grip point, +Y = weapon tip direction)
* exports the character FBX (mesh + armature + sockets + all takes, textures embedded)
* exports each weapon as its own FBX normalised to: grip at origin, tip along +Y (swappable gear)
* "qa" renders close-up frames of selected actions WITH weapons attached for review
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
with open(ARGS[0], encoding="utf-8-sig") as f:
    SPEC = json.load(f)
QA = len(ARGS) > 1 and ARGS[1] == "qa"


def p(x):
    return x if os.path.isabs(x) else os.path.join(ROOT, x)


bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.fps = 30          # Meshy clips are sampled at 30 fps (post_process change_fps=30)
sc.render.fps_base = 1.0


def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=p(path))
    return [o for o in bpy.data.objects if o not in before]


objs = import_glb(SPEC["glbs"][0])
for o in list(objs):
    if o.type == "MESH" and o.name.lower().startswith("icosphere"):
        bpy.data.objects.remove(o, do_unlink=True)
arm = next(o for o in sc.objects if o.type == "ARMATURE")
arm.name = SPEC["name"] + "_Rig"
body = next(o for o in sc.objects if o.type == "MESH" and o.parent == arm)
body.name = SPEC["name"] + "_Body"

for extra in SPEC["glbs"][1:]:
    for o in import_glb(extra):
        bpy.data.objects.remove(o, do_unlink=True)

# de-duplicate actions imported twice (same name with .001 suffix)
seen = {}
for act in list(bpy.data.actions):
    base = act.name.split(".")[0]
    if base in seen:
        bpy.data.actions.remove(act)
    else:
        seen[base] = act
        act.name = base
        act.use_fake_user = True
if arm.animation_data:
    for t in list(arm.animation_data.nla_tracks):
        arm.animation_data.nla_tracks.remove(t)
arm.animation_data_create().action = None
print("ACTIONS", sorted(a.name for a in bpy.data.actions))


# ------------------------------------------------------------------ weapons
def load_weapon(w):
    """Import a weapon GLB and normalise it: grip at origin, tip along +Y, scaled to length_m."""
    parts = [o for o in import_glb(w["glb"]) if o.type == "MESH"]
    for o in [o for o in bpy.data.objects if o.type != "MESH" and o.parent is None and o not in (arm,)]:
        if o.name.startswith(("Socket_",)):
            continue
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    wo = bpy.context.view_layer.objects.active
    wo.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    vs = np.array([v.co[:] for v in wo.data.vertices])
    mn, mx = vs.min(0), vs.max(0)
    ax = "xyz".index(w["axis"])
    tip_neg = w["tip"] == "-"
    length = mx[ax] - mn[ax]
    grip = (mn + mx) / 2
    # grip fraction is measured from the tip end
    grip[ax] = (mn[ax] + length * w["grip"]) if tip_neg else (mx[ax] - length * w["grip"])
    wo.data.transform(Matrix.Translation(Vector(-grip)))
    # rotate so the tip direction becomes +Y
    tip_dir = Vector([0, 0, 0]); tip_dir[ax] = -1 if tip_neg else 1
    rot = tip_dir.rotation_difference(Vector((0, 1, 0))).to_matrix().to_4x4()
    wo.data.transform(rot)
    if w.get("roll"):
        wo.data.transform(Matrix.Rotation(math.radians(w["roll"]), 4, "Y"))
    s = w["length_m"] / length
    wo.data.transform(Matrix.Scale(s, 4))
    wo.name = w["name"]
    return wo


def socket_world(bone_name, w):
    """Rest-pose grip frame: in the palm, weapon tip pointing forward (-Y) and slightly up."""
    b = arm.data.bones[bone_name]
    # Meshy hand bones have unreliable (very long) tails: derive the palm from forearm->wrist.
    wrist = arm.matrix_world @ b.head_local
    elbow = arm.matrix_world @ b.parent.head_local
    pos = wrist + (wrist - elbow).normalized() * w.get("palm", 0.045)
    side = 1 if pos.x > 0 else -1
    pos += Vector((-side * w.get("inset", 0.0), 0, 0))
    tip = Vector(w.get("tip_dir", (0, -1, 0.35))).normalized()
    fd = w.get("face_dir", (side, 0, 0))
    face = (elbow - wrist).normalized() if fd == "shoulder" else Vector(fd).normalized()  # weapon +Z
    z = (face - tip * face.dot(tip)).normalized()
    x = tip.cross(z)
    m = Matrix((x, tip, z)).transposed().to_4x4()
    m.translation = pos
    return m


sockets = {}
weapons = []
# sockets are authored against the REST pose
for pb in arm.pose.bones:
    pb.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()
for w in SPEC.get("weapons", []):
    sname = w["socket"]
    if sname not in sockets:
        e = bpy.data.objects.new(sname, None)
        e.empty_display_type = "ARROWS"
        e.empty_display_size = 0.08
        sc.collection.objects.link(e)
        m = socket_world(w["bone"], w)
        e.parent = arm
        e.parent_type = "BONE"
        e.parent_bone = w["bone"]
        bpy.context.view_layer.update()
        e.matrix_world = m
        bpy.context.view_layer.update()
        print("SOCKET", sname, tuple(round(v, 3) for v in e.matrix_world.translation),
              "target", tuple(round(v, 3) for v in m.translation))
        sockets[sname] = e
    wo = load_weapon(w)
    weapons.append((w, wo))

# ------------------------------------------------------------------ QA renders (weapons attached)
if QA:
    for w, wo in weapons:
        wo.parent = sockets[w["socket"]]
        wo.matrix_parent_inverse = Matrix.Identity(4)
        wo.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    for w, wo in weapons:
        bb = [wo.matrix_world @ Vector(c) for c in wo.bound_box]
        print("DEBUG weapon", wo.name, "hide", wo.hide_render, "verts", len(wo.data.vertices),
              "socket", tuple(round(v, 3) for v in sockets[w["socket"]].matrix_world.translation),
              "bbmin", tuple(round(min(b[i] for b in bb), 3) for i in range(3)),
              "bbmax", tuple(round(max(b[i] for b in bb), 3) for i in range(3)))
    sc.render.engine = "BLENDER_EEVEE"
    cell = 300
    sc.render.resolution_x = sc.render.resolution_y = cell
    sc.render.film_transparent = True
    sc.view_settings.view_transform = "Standard"
    wd = bpy.data.worlds.new("w"); sc.world = wd; wd.use_nodes = True
    wd.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = 1.5
    yaw = math.radians(SPEC.get("qa_yaw", -45))
    cam.rotation_euler = (math.radians(90), 0, yaw)
    cam.location = Vector((math.sin(yaw) * 10, -math.cos(yaw) * 10, 0.55))
    sun = bpy.data.objects.new("s", bpy.data.lights.new("s", "SUN")); sun.data.energy = 3
    sun.rotation_euler = (math.radians(50), 0, yaw - math.radians(30)); sc.collection.objects.link(sun)
    rows = []
    tmp = os.path.join(ROOT, "ArtSource", "_render_tmp", "_qa_cell.png")
    for name in SPEC["qa_actions"]:
        act = bpy.data.actions[name]
        arm.animation_data.action = act
        if hasattr(act, "slots") and len(act.slots):
            arm.animation_data.action_slot = act.slots[0]
        s, e = act.frame_range
        cells = []
        for i in range(5):
            fr = s + (e - s) * i / 4
            sc.frame_set(int(fr), subframe=fr - int(fr))
            sc.render.filepath = tmp
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(tmp)
            px = np.array(img.pixels[:], np.float32).reshape(cell, cell, 4)[::-1]
            bpy.data.images.remove(img)
            bg = np.ones_like(px); bg[..., :3] = (0.82, 0.86, 0.9)
            cells.append(px * px[..., 3:4] + bg * (1 - px[..., 3:4]))
        rows.append(np.concatenate(cells, 1))
    sheet = np.concatenate(rows, 0); sheet[..., 3] = 1
    img = bpy.data.images.new("q", sheet.shape[1], sheet.shape[0], alpha=True)
    img.pixels[:] = sheet[::-1].ravel()
    img.filepath_raw = os.path.join(ROOT, "ArtSource", "_render_tmp", f"qa_{SPEC['name']}.png")
    img.file_format = "PNG"; img.save()
    print("QA SHEET", img.filepath_raw)
    sys.exit(0)

# ------------------------------------------------------------------ export
FBX = dict(use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
           axis_forward="-Z", axis_up="Y", add_leaf_bones=False, path_mode="COPY", embed_textures=True,
           mesh_smooth_type="FACE", use_armature_deform_only=False)


def export(objs, path, anim):
    os.makedirs(os.path.dirname(p(path)), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=p(path), object_types={"ARMATURE", "MESH", "EMPTY"},
                             bake_anim=anim, bake_anim_use_all_actions=anim, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, **FBX)
    print("EXPORTED", path)


# Unique image / material names: every Meshy asset ships "Material_0" / "Material_0_baseColor",
# which collide inside one FBX. Names get a .png extension so Unity imports extracted files.
def rename_materials(obj, base):
    for slot in obj.material_slots:
        m = slot.material
        if not m:
            continue
        m.name = base
        for n in m.node_tree.nodes if m.use_nodes else []:
            if n.type == "TEX_IMAGE" and n.image:
                n.image.name = f"{base}_baseColor.png"
                n.image.filepath_raw = f"//{base}_baseColor.png"
                n.image.file_format = "PNG"


rename_materials(body, f"{SPEC['name']}_Body")

# Weapons are skinned 100% to their hand bone (robust in FBX, unlike bone-parented objects, whose
# Blender tail-relative offsets break with Meshy's very long hand bones). Each weapon stays a separate
# SkinnedMeshRenderer, so Unity can hide/swap gear or rebind other gear to the same bones by name.
rigid = []
for w, wo in weapons:
    sock = sockets[w["socket"]]
    wo.parent = None
    wo.matrix_world = sock.matrix_world.copy()
    bpy.ops.object.select_all(action="DESELECT")
    wo.select_set(True)
    bpy.context.view_layer.objects.active = wo
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    vg = wo.vertex_groups.new(name=w["bone"])
    vg.add(list(range(len(wo.data.vertices))), 1.0, "REPLACE")
    wo.parent = arm
    wo.matrix_parent_inverse = arm.matrix_world.inverted()
    mod = wo.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    rename_materials(wo, w["name"].replace("_L", "") if w["name"].endswith("_L") else w["name"])
    wo.name = "Weapon_" + w["name"]
    rigid.append(wo)

export([arm, body] + rigid, SPEC["out"], True)
