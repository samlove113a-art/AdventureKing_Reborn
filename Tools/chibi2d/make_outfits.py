"""Colour-variant outfits for the 2D Mage (Cơ Bản / Hỏa Ma / Băng Sương of the design sheet): hue-shifts only the violet hat / coat pixels
of every body strip, keeps skin, hair, gold and outlines. Output: ArtSource/Chibi2D/Mage/Outfits/<set>/Mage_<Clip>.png (+ Mage_frames.json).

  "<Blender>/5.2/python/bin/python.exe" Tools/chibi2d/make_outfits.py
Needs the strips written by extract_mage_sheet.py. Pure numpy; PNG read through a tiny decoder (our own writer's format only).
"""
import colorsys
import json
import os
import struct
import sys
import zlib

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from mage_rig import write_png  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "ArtSource", "Chibi2D", "Mage")
SETS = {"fire_mage": (0.26, 1.35, 1.12), "frost_mage": (-0.16, 0.95, 1.05)}   # (hue turn, saturation, value) applied to violet pixels


def read_png(path):
    b = open(path, "rb").read()
    pos, idat, w = 8, b"", 0
    while pos < len(b):
        n, t = struct.unpack(">I4s", b[pos:pos + 8])
        d = b[pos + 8:pos + 8 + n]
        if t == b"IHDR":
            w, h = struct.unpack(">II", d[:8])
        elif t == b"IDAT":
            idat += d
        pos += 12 + n
    raw = zlib.decompress(idat)
    stride = w * 4 + 1
    arr = np.frombuffer(raw, np.uint8).reshape(h, stride)[:, 1:].reshape(h, w, 4).astype(np.float32) / 255.0
    return arr


def shift(arr, turn, s, v):
    out = arr.copy()
    rgb = arr[..., :3]
    mx, mn = rgb.max(-1), rgb.min(-1)
    d = mx - mn
    sat = np.where(mx > 0, d / np.maximum(mx, 1e-6), 0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.zeros_like(mx)
    m = d > 1e-6
    hue = np.where(m & (mx == r), ((g - b) / np.maximum(d, 1e-6)) % 6, hue)
    hue = np.where(m & (mx == g), (b - r) / np.maximum(d, 1e-6) + 2, hue)
    hue = np.where(m & (mx == b), (r - g) / np.maximum(d, 1e-6) + 4, hue)
    hue = hue / 6.0
    violet = (hue > 0.68) & (hue < 0.86) & (sat > 0.22)
    ys, xs = np.where(violet)
    for y, x in zip(ys, xs):
        h2 = (hue[y, x] + turn) % 1.0
        s2 = min(1.0, sat[y, x] * s)
        v2 = min(1.0, mx[y, x] * v)
        out[y, x, :3] = colorsys.hsv_to_rgb(h2, s2, v2)
    return out


def main():
    meta = json.load(open(os.path.join(SRC, "Mage_frames.json"), encoding="utf-8"))
    for name, (turn, s, v) in SETS.items():
        out = os.path.join(SRC, "Outfits", name)
        os.makedirs(out, exist_ok=True)
        for clip, info in meta["clips"].items():
            write_png(os.path.join(out, info["file"]), shift(read_png(os.path.join(SRC, info["file"])), turn, s, v))
        json.dump(meta, open(os.path.join(out, "Mage_frames.json"), "w", encoding="utf-8"), indent=2)
        print("OUTFIT", name, flush=True)


if __name__ == "__main__":
    main()