"""Per-action motion analysis for clip trimming and impact events.
blender -b --factory-startup -P analyze_actions.py -- <spec.json> <out.json>
For every action: frame range, frames where both feet are off the ground, and the frames of peak
right/left wrist speed (used as the weapon contact / release frame for animation events).
"""
import json
import os
import sys

import bpy

a = sys.argv[sys.argv.index("--") + 1:]
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
spec = json.load(open(a[0], encoding="utf-8-sig"))
bpy.ops.wm.read_factory_settings(use_empty=True)
for i, g in enumerate(spec["glbs"]):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(ROOT, g))
    if i > 0:
        for o in [o for o in bpy.data.objects if o not in before]:
            bpy.data.objects.remove(o, do_unlink=True)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
if arm.animation_data:
    for t in list(arm.animation_data.nla_tracks):
        arm.animation_data.nla_tracks.remove(t)
sc = bpy.context.scene


def wpos(name):
    return arm.matrix_world @ arm.pose.bones[name].head


out = {}
for act in bpy.data.actions:
    base = act.name.split(".")[0]
    if base in out:
        continue
    arm.animation_data_create().action = act
    if hasattr(act, "slots") and len(act.slots):
        arm.animation_data.action_slot = act.slots[0]
    s, e = int(act.frame_range[0]), int(round(act.frame_range[1]))
    feet, hips, rspd, lspd = [], [], [], []
    prev = None
    for f in range(s, e + 1):
        sc.frame_set(f)
        lt, rt = wpos("LeftToeBase"), wpos("RightToeBase")
        lf, rf = wpos("LeftFoot"), wpos("RightFoot")
        feet.append(min(lt.z, rt.z, lf.z, rf.z))
        hips.append(wpos("Hips").z)
        rh, lh = wpos("RightHand"), wpos("LeftHand")
        if prev:
            rspd.append((rh - prev[0]).length * 30)
            lspd.append((lh - prev[1]).length * 30)
        else:
            rspd.append(0.0); lspd.append(0.0)
        prev = (rh.copy(), lh.copy())
    def peaks(spd, frac=0.5, sep=8):
        top = max(spd) or 1
        cand = [i for i in range(1, len(spd) - 1)
                if spd[i] >= spd[i - 1] and spd[i] >= spd[i + 1] and spd[i] >= top * frac]
        cand.sort(key=lambda i: -spd[i])
        chosen = []
        for i in cand:
            if all(abs(i - j) >= sep for j in chosen):
                chosen.append(i)
        return sorted(s + i for i in chosen)

    ground = min(feet)
    air = [f for f, z in zip(range(s, e + 1), feet) if z - ground > 0.04]
    out[base] = {
        "range": [s, e],
        "airborne": [air[0], air[-1]] if air else None,
        "peak_hips_frame": s + max(range(len(hips)), key=lambda i: hips[i]),
        "peak_right_hand_frame": s + max(range(len(rspd)), key=lambda i: rspd[i]),
        "peak_left_hand_frame": s + max(range(len(lspd)), key=lambda i: lspd[i]),
        "peak_right_speed": round(max(rspd), 2), "peak_left_speed": round(max(lspd), 2),
        "hips_rise": round(max(hips) - hips[0], 3),
        "right_peaks": peaks(rspd), "left_peaks": peaks(lspd),
        "foot_contacts": [s + i for i in range(1, len(feet) - 1)
                          if feet[i] - ground < 0.02 and feet[i - 1] - ground >= 0.02],
    }
json.dump(out, open(a[1], "w"), indent=1)
for k, v in sorted(out.items()):
    print("ANALYSIS", k, json.dumps(v))
