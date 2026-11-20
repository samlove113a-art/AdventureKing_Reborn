"""Pixel-art skill effects for the 2D Mage (separate assets, transparent, fixed palette).

  "<Blender>/5.2/python/bin/python.exe" Tools/chibi2d/mage_fx.py [out_dir]

Writes one strip per effect + fx_frames.json: cast_circle (rotating rune ring), arcane_orb (pulsing bolt), hit_spark (burst).
Drawn analytically at 4x and snapped to a 9-colour violet / white / gold palette with hard alpha, like the character.
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from mage_rig import write_png  # noqa: E402  (PNG writer, no dependencies)

PAL = np.array([
    (0.09, 0.05, 0.25), (0.29, 0.18, 0.62), (0.48, 0.33, 0.82), (0.69, 0.55, 0.96), (0.86, 0.78, 1.0),
    (1.0, 1.0, 1.0), (1.0, 0.83, 0.36), (0.85, 0.57, 0.17), (0.62, 0.9, 1.0)], np.float32)
SSF = 4


def grid(n):
    y, x = np.mgrid[0:n * SSF, 0:n * SSF].astype(np.float32)
    return (x + 0.5) / SSF - n / 2, (y + 0.5) / SSF - n / 2


def to_pixels(level, alpha, n):
    """level: 0..1 brightness index into the violet ramp (0..4), alpha 0/1 at hi-res -> n x n pixel frame."""
    idx = np.clip(np.round(level * 4), 0, 4).astype(np.int16)
    out = np.zeros((n, n, 4), np.float32)
    on = alpha.reshape(n, SSF, n, SSF).mean(axis=(1, 3)) > 0.5
    mode = np.zeros((n, n), np.int16)
    counts = [((idx == k) & (alpha > 0.5)).reshape(n, SSF, n, SSF).sum(axis=(1, 3)) for k in range(9)]
    mode = np.argmax(np.stack(counts), axis=0)
    out[..., :3] = PAL[mode]
    out[..., 3] = on
    out[~on, :3] = 0
    return out


def cast_circle(i, n=64, frames=8):
    x, y = grid(n)
    r = np.hypot(x, y)
    a = math.atan2
    ang = np.arctan2(y, x) + i / frames * 2 * math.pi / 6
    squash = 0.42                                           # seen from the side: a flat ellipse on the ground
    ry = np.hypot(x, y / squash)
    ring = (np.abs(ry - 28) < 1.3) | (np.abs(ry - 21) < 1.0)
    runes = (np.abs(ry - 24.5) < 2.2) & (np.cos(ang * 6) > 0.35)
    star = (np.abs(ry - 14) < 1.0) & (np.cos(ang * 5 + 0.0) > -2)
    tri = (np.abs(np.cos(ang * 3) * ry - 12) < 1.0) & (ry < 20)
    pulse = 0.5 + 0.5 * math.sin(i / frames * 2 * math.pi)
    glow = np.clip(1 - ry / 30, 0, 1)
    alpha = (ring | runes | star | tri | (ry < 4 + 2 * pulse)).astype(np.float32)
    level = np.where(ring, 3, np.where(runes, 4, np.where(tri | star, 2, 4))) / 4.0
    level = np.where(ry < 4 + 2 * pulse, 1.0, level)
    return to_pixels(level, alpha, n)


def arcane_orb(i, n=32, frames=6):
    x, y = grid(n)
    r = np.hypot(x, y)
    pulse = 0.5 + 0.5 * math.sin(i / frames * 2 * math.pi)
    R = 9 + 2 * pulse
    core = r < R * 0.45
    mid = r < R * 0.8
    body = r < R
    tail = (x < -2) & (np.abs(y) < (np.clip(-x - 2, 0, 14) * -0.5 + 7) * (1 - np.clip((-x - 2) / 14, 0, 1)) + 0.5) & (x > -15)
    sparks = (np.abs(np.hypot(x + 6 + (i % 3) * 3, y - 7 * (-1) ** i) - 1.1) < 1.0)
    alpha = (body | tail | sparks).astype(np.float32)
    level = np.where(core, 1.0, np.where(mid, 0.75, np.where(body, 0.5, 0.25)))
    return to_pixels(level, alpha, n)


def hit_spark(i, n=48, frames=5):
    x, y = grid(n)
    r = np.hypot(x, y)
    ang = np.arctan2(y, x)
    t = i / (frames - 1)
    R = 6 + 16 * t
    spikes = r < R * (0.35 + 0.65 * np.abs(np.cos(ang * 4)) ** 1.6)
    hole = r < R * 0.28 * (1 - t) + 1
    ringw = (np.abs(r - (R + 4)) < 1.6 * (1 - t) + 0.6) & (t > 0.25)
    alpha = ((spikes & ~(t > 0.7) | ringw | (spikes & (r > R * 0.55))) & (t < 0.95)).astype(np.float32)
    level = np.where(hole, 1.0, np.where(ringw, 0.75, 0.5 + 0.5 * (1 - r / (R + 1))))
    return to_pixels(level, alpha, n)


EFFECTS = [("cast_circle", cast_circle, 8, 12, True, 64), ("arcane_orb", arcane_orb, 6, 14, True, 32), ("hit_spark", hit_spark, 5, 16, False, 48)]


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "ArtSource", "Chibi2D", "Mage", "Fx")
    os.makedirs(out, exist_ok=True)
    meta = {"pixel": True, "effects": {}}
    for name, fn, n, fps, loop, size in EFFECTS:
        frames = [fn(i, size, n) if name != "hit_spark" else fn(i, size, n) for i in range(n)]
        write_png(os.path.join(out, f"{name}.png"), np.concatenate(frames, axis=1))
        meta["effects"][name] = {"frames": n, "fps": fps, "loop": loop, "size": size, "file": f"{name}.png"}
        print("FX", name, n, flush=True)
    with open(os.path.join(out, "fx_frames.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2)


if __name__ == "__main__":
    main()
