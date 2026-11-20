"""Slime Bông / Slime Rêu of the meadow: recolours of the Meshy-made Slime Phấn Hoa sprite (same body, new hue), so every monster type has 2D art.
  "<Blender>/5.2/python/bin/python.exe" Tools/chibi2d/make_slime_variants.py     (after bake_chibi_actor.py PollenSlime)
"""
import colorsys
import json
import os
import shutil
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from make_outfits import read_png  # noqa: E402
from mage_rig import write_png  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "ArtSource", "Chibi2D", "PollenSlime")
VARIANTS = {"PuffSlime": (0.40, 0.55, 1.10), "MossSlime": (0.10, 1.0, 0.62)}   # (hue turn, saturation x, value x) on the green body


def recolour(a, turn, s, v):
    out = a.copy()
    rgb = a[..., :3]
    for y in range(a.shape[0]):
        for x in range(a.shape[1]):
            if a[y, x, 3] < 0.5:
                continue
            h, sat, val = colorsys.rgb_to_hsv(*rgb[y, x])
            if 0.15 < h < 0.35 and sat > 0.25:             # the green body only; eyes, flower and outline keep their colours
                out[y, x, :3] = colorsys.hsv_to_rgb((h + turn) % 1.0, min(1.0, sat * s), min(1.0, val * v))
    return out


meta = json.load(open(os.path.join(SRC, "PollenSlime_frames.json"), encoding="utf-8"))
for name, (turn, s, v) in VARIANTS.items():
    dst = os.path.join(ROOT, "ArtSource", "Chibi2D", name)
    os.makedirs(dst, exist_ok=True)
    m = json.loads(json.dumps(meta))
    for clip, info in m["clips"].items():
        info["file"] = f"{name}_{clip}.png"
        write_png(os.path.join(dst, info["file"]), recolour(read_png(os.path.join(SRC, f"PollenSlime_{clip}.png")), turn, s, v))
    json.dump(m, open(os.path.join(dst, f"{name}_frames.json"), "w", encoding="utf-8"), indent=2)
    print("VARIANT", name, flush=True)