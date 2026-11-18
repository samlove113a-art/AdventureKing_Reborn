"""Original 2D pixel skill effects for the four classes (hard-edged, flat-shaded, no outline), flipbook strips of 128 px frames.

  <blender python> Tools/chibi2d/make_fx.py [out_dir=ArtSource/Chibi2D/Fx] [--preview]

Output: <out>/<name>.png (one strip) + <out>/fx_frames.json (frames / fps / loop per effect). Chibi2DArt.BuildFx2 turns them into
Resources/Chibi2D/Fx2/<name> sprite sets that Pixel2DSkills plays. Colours are tinted at runtime (SpriteRenderer.color), so one
white-ish effect serves several skills. Drawn for the reference sheets of the owner: mage = purple magic / orb / ice / lightning /
summon circle; swordsman = sword aura / light slash / shield / ground slam; archer = multi-arrow / arrow rain / leaf wind / pierce;
assassin = clone / dash / dark slash / smoke / finisher.
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from make_monsters import Cv, rgb, write_png, SS  # noqa: E402

W = rgb("#ffffff")


def paint(cv, mask, color):
    cv.col[mask] = color
    cv.cov |= mask


def polar(cv, cx=64.0, cy=64.0):
    dx, dy = cv.X - cx, cv.Y - cy
    return np.hypot(dx, dy), np.arctan2(dy, dx), dx, dy


def to_png(cv):
    k = SS
    cell = cv.cell
    cov = cv.cov.reshape(cell, k, cell, k).mean((1, 3))
    col = cv.col.reshape(cell, k, cell, k, 3).mean((1, 3))
    out = np.zeros((cell, cell, 4), np.float32)
    out[..., :3] = col
    out[..., 3] = (cov > 0.5)
    out[out[..., 3] == 0, :3] = 0
    return (np.clip(out, 0, 1) * 255).astype(np.uint8)


# ------------------------------------------------------------------ effects: each is f(i, n) -> Cv
def slash(i, n, edge, core, mirror=False):
    """A swept crescent: an arc of radius 56 centred left of the frame middle, thick in the middle and sharp at both ends, revealed from the
    hero side outward with a fading tail."""
    cv = Cv(128)
    p = (i + 1) / n
    cx, cy, R = 24.0, 64.0, 62.0
    dx, dy = cv.X - cx, cv.Y - cy
    r = np.hypot(dx, dy)
    ang = np.degrees(np.arctan2(dy, dx))
    u = (ang + 68.0) / 136.0                            # 0..1 along the arc
    head = min(1.0, p * 1.35)
    tail = max(0.0, head - 0.75) if p > 0.7 else 0.0
    thick = 15.0 * np.clip(np.sin(np.pi * np.clip(u, 0, 1)), 0, 1) ** 0.8
    visible = (u >= tail) & (u <= head) & (u >= 0) & (u <= 1)
    m = visible & (np.abs(r - R) < thick)
    paint(cv, m, edge)
    paint(cv, visible & (np.abs(r - R - 2) < thick * 0.42), core)
    return cv

def ground_slam(i, n):
    cv = Cv(128)
    t = (i + 1) / n
    rx, ry = 12 + 50 * t, 3 + 8 * t
    ring = np.abs(np.hypot((cv.X - 64) / rx, (cv.Y - 104) / ry) - 1.0) < 0.2 * (1.1 - t)
    paint(cv, ring, rgb("#ffd45c"))
    inner = np.abs(np.hypot((cv.X - 64) / (rx * 0.7), (cv.Y - 104) / (ry * 0.7)) - 1.0) < 0.14 * (1.1 - t)
    paint(cv, inner, W)
    for k in range(7):                                  # rock chunks flying up and out
        a = (k - 3) * 0.42
        px, py = 64 + math.sin(a) * 46 * t, 104 - (math.cos(a) * 40 * t - 30 * t * t)
        r, _, _, _ = polar(cv, px, py)
        paint(cv, r < 3.4 * (1.15 - t * 0.6), rgb("#b79a6a") if k % 2 else rgb("#8a7048"))
    return cv


def pillar(i, n):
    cv = Cv(128)
    t = (i + 1) / n
    half = 5 + 14 * math.sin(min(1.0, t * 1.3) * math.pi * 0.5) * (1.0 - 0.6 * max(0.0, t - 0.6))
    m = (np.abs(cv.X - 64) < half) & (cv.Y < 118) & (cv.Y > 2 + 40 * (1 - t))
    paint(cv, m, rgb("#ffe48a"))
    paint(cv, m & (np.abs(cv.X - 64) < half * 0.5), W)
    r, _, _, _ = polar(cv, 64, 114)
    paint(cv, r < 8 + 26 * t, rgb("#fff1b8"))
    for k in range(5):
        r2, _, _, _ = polar(cv, 64 + (k - 2) * 12, 100 - 80 * ((t * 1.4 + k * 0.17) % 1.0))
        paint(cv, r2 < 2.2, W)
    return cv


def whirl(i, n):
    cv = Cv(128)
    t = i / n
    r, a, dx, dy = polar(cv)
    ang = np.degrees(a) % 360
    for k in range(2):
        start = (t * 360 + k * 180) % 360
        d = (ang - start) % 360
        m = (np.abs(r - 44) < 5 * (1 - d / 180.0 + 0.2)) & (d < 130)
        paint(cv, m, rgb("#7af0ff") if k == 0 else rgb("#d8fbff"))
    paint(cv, (np.abs(r - 44) < 1.2), rgb("#bfffff"))
    return cv


def arrow(i, n, edge=rgb("#d8f0a8"), head=rgb("#e8eef8")):
    cv = Cv(128)
    paint(cv, (np.abs(cv.Y - 64) < 1.7) & (cv.X > 18) & (cv.X < 104), rgb("#8a6a3a"))
    tri = cv.tri((100, 56), (122, 64), (100, 72)) < 0
    paint(cv, tri, head)
    for dy in (-1, 1):                                  # fletching
        paint(cv, cv.tri((18, 64), (34, 64 + dy * 9), (26, 64)) < 0, edge)
    paint(cv, (np.abs(cv.Y - 64) < 0.6) & (cv.X > 28) & (cv.X < 98), rgb("#d8b074"))
    return cv


def leaf_wind(i, n):
    cv = Cv(128)
    t = i / n
    for k in range(9):
        a = t * 2 * math.pi + k * 0.7
        rad = 18 + k * 4.2
        px, py = 64 + math.cos(a) * rad * 1.45, 64 + math.sin(a) * rad * 0.8 - k * 1.2
        leaf = cv.ell(px, py, 10, 4.2, math.degrees(a) + 90) < 0
        paint(cv, leaf, rgb("#6fd04f") if k % 2 else rgb("#b8f070"))
        paint(cv, cv.ell(px, py, 6.5, 1.1, math.degrees(a) + 90) < 0, rgb("#3f8f30"))
    return cv


def ice_shard(i, n):
    cv = Cv(128)
    body = cv.tri((54, 8), (64, 120), (74, 8)) < 0
    paint(cv, body, rgb("#9fe6ff"))
    paint(cv, cv.tri((60, 14), (64, 110), (68, 14)) < 0, W)
    paint(cv, cv.tri((46, 20), (52, 70), (58, 18)) < 0, rgb("#c8f4ff"))
    return cv


def ice_burst(i, n):
    cv = Cv(128)
    t = (i + 1) / n
    for k in range(10):
        a = k * 0.628 + 0.2
        r0, r1 = 8 + 36 * t, 20 + 42 * t
        ax, ay = 64 + math.cos(a) * r0, 80 + math.sin(a) * r0 * 0.7
        bx, by = 64 + math.cos(a) * r1, 80 + math.sin(a) * r1 * 0.7
        paint(cv, cv.line(ax, ay, bx, by, 3.4 * (1.15 - t)) < 0, rgb("#9fe6ff") if k % 2 else W)
    r, _, _, _ = polar(cv, 64, 80)
    paint(cv, r < 12 * (1.1 - t), W)
    return cv


def lightning(i, n):
    cv = Cv(128)
    rng = np.random.default_rng(100 + i)
    x, y = 64.0 + rng.uniform(-8, 8), 2.0
    pts = [(x, y)]
    while y < 116:
        y += rng.uniform(10, 18)
        x += rng.uniform(-14, 14)
        x = min(max(x, 30), 98)
        pts.append((x, y))
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        paint(cv, cv.line(ax, ay, bx, by, 5.2) < 0, rgb("#b46bff"))
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        paint(cv, cv.line(ax, ay, bx, by, 2.2) < 0, W)
    r, _, _, _ = polar(cv, pts[-1][0], 116)
    paint(cv, r < 9, rgb("#d9a8ff"))
    return cv


def smoke(i, n, color=rgb("#463860"), light=rgb("#6e5a94")):
    cv = Cv(128)
    t = (i + 1) / n
    rng = np.random.default_rng(7)
    for k in range(7):
        a = k * 0.9
        px, py = 64 + math.cos(a) * 26 * t, 84 - 14 * t + math.sin(a) * 12 * t
        rad = (10 + rng.uniform(0, 5)) * (0.5 + t) * (1.25 - t * 0.7)
        r, _, _, _ = polar(cv, px, py)
        paint(cv, r < rad, color)
        r2, _, _, _ = polar(cv, px - 2, py - 2)
        paint(cv, r2 < rad * 0.6, light)
    return cv


def explosion(i, n):
    cv = Cv(128)
    t = (i + 1) / n
    r, a, dx, dy = polar(cv, 64, 84)
    bump = 1 + 0.18 * np.sin(a * 7 + i)
    paint(cv, r < 14 + 46 * t * bump, rgb("#b04cff"))
    paint(cv, r < (10 + 34 * t) * bump, rgb("#e0a0ff"))
    paint(cv, r < (4 + 20 * t) * bump * (1.1 - t * 0.5), W)
    for k in range(8):
        ang = k * 0.785 + 0.3
        rr = 30 + 50 * t
        r2, _, _, _ = polar(cv, 64 + math.cos(ang) * rr, 84 + math.sin(ang) * rr * 0.8)
        paint(cv, r2 < 3.5 * (1.2 - t), rgb("#ffd9ff"))
    return cv


def meteor(i, n):
    cv = Cv(128)
    for k in range(9):                                  # trail up-left, head down-right
        t = k / 8
        px, py = 100 - 78 * t, 100 - 78 * t
        r, _, _, _ = polar(cv, px, py)
        paint(cv, r < 20 * (1.1 - t * 0.9), rgb("#7a2cd0") if k > 2 else rgb("#b04cff"))
    r, _, _, _ = polar(cv, 100, 100)
    paint(cv, r < 20, rgb("#5a3a78"))
    paint(cv, polar(cv, 97, 97)[0] < 12, rgb("#d0a0ff"))
    paint(cv, polar(cv, 94, 94)[0] < 5, W)
    return cv


def dagger(i, n):
    cv = Cv(128)
    paint(cv, cv.tri((96, 58), (122, 64), (96, 70)) < 0, rgb("#e8eef8"))
    paint(cv, (np.abs(cv.Y - 64) < 5) & (cv.X > 52) & (cv.X < 98), rgb("#c8d0e0"))
    paint(cv, (np.abs(cv.Y - 64) < 1.6) & (cv.X > 54) & (cv.X < 118), W)
    paint(cv, (np.abs(cv.X - 50) < 3) & (np.abs(cv.Y - 64) < 11), rgb("#6a4a8a"))
    paint(cv, (np.abs(cv.Y - 64) < 3) & (cv.X > 22) & (cv.X < 48), rgb("#3a2a4a"))
    return cv


def orb(i, n):
    cv = Cv(128)
    t = i / n
    r, _, _, _ = polar(cv)
    pulse = 1 + 0.08 * math.sin(t * 2 * math.pi)
    paint(cv, r < 30 * pulse, rgb("#8a3cf0"))
    paint(cv, r < 22 * pulse, rgb("#c07cff"))
    paint(cv, polar(cv, 58, 58)[0] < 12 * pulse, rgb("#f0d8ff"))
    paint(cv, polar(cv, 55, 55)[0] < 5, W)
    for k in range(5):
        a = t * 2 * math.pi + k * 1.26
        paint(cv, polar(cv, 64 + math.cos(a) * 40, 64 + math.sin(a) * 40)[0] < 3, rgb("#d9a8ff"))
    return cv


def shield(i, n):
    cv = Cv(128)
    t = (i + 1) / n
    r, a, dx, dy = polar(cv, 64, 70)
    rim = np.abs(r - 46) < 3.4
    paint(cv, rim & (np.cos(a * 6 + t * 3) > -0.2), rgb("#8fe8ff"))
    paint(cv, (r < 46) & (r > 40), rgb("#bff6ff"))
    paint(cv, np.abs(r - 46) < 1.2, W)
    return cv


EFFECTS = {   # name -> (builder, frames, fps, loop)
    "slash_light": (lambda i, n: slash(i, n, rgb("#7af0ff"), rgb("#ffffff")), 6, 22, False),
    "slash_dark": (lambda i, n: slash(i, n, rgb("#7a2cd0"), rgb("#e8b8ff")), 6, 22, False),
    "slam": (ground_slam, 6, 16, False),
    "pillar": (pillar, 6, 14, False),
    "whirl": (whirl, 8, 20, True),
    "arrow": (arrow, 1, 1, True),
    "leaf_wind": (leaf_wind, 8, 14, True),
    "ice_shard": (ice_shard, 1, 1, True),
    "ice_burst": (ice_burst, 6, 18, False),
    "lightning": (lightning, 4, 24, False),
    "smoke": (smoke, 6, 12, False),
    "explosion": (explosion, 6, 16, False),
    "meteor": (meteor, 1, 1, True),
    "dagger": (dagger, 1, 1, True),
    "orb": (orb, 6, 12, True),
    "shield": (shield, 6, 12, True),
}


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    out = args[0] if args else os.path.join(os.path.dirname(__file__), "..", "..", "ArtSource", "Chibi2D", "Fx")
    os.makedirs(out, exist_ok=True)
    meta = {"cell": 128, "effects": {}}
    rows = []
    for name, (fn, n, fps, loop) in EFFECTS.items():
        frames = [to_png(fn(i, n)) for i in range(n)]
        strip = np.concatenate(frames, axis=1)
        write_png(os.path.join(out, name + ".png"), strip)
        meta["effects"][name] = {"frames": n, "fps": fps, "loop": loop}
        rows.append(strip)
        print("wrote", name, flush=True)
    json.dump(meta, open(os.path.join(out, "fx_frames.json"), "w", encoding="utf-8"), indent=2)
    if "--preview" in sys.argv:
        w = max(r.shape[1] for r in rows)
        sheet = np.concatenate([np.pad(r, ((0, 0), (0, w - r.shape[1]), (0, 0))) for r in rows], axis=0).astype(np.float32) / 255.0
        bg = np.zeros(sheet.shape[:2] + (3,), np.float32) + np.array([0.22, 0.26, 0.3], np.float32)
        al = sheet[..., 3:4]
        img = np.concatenate([sheet[..., :3] * al + bg * (1 - al), np.ones(sheet.shape[:2] + (1,), np.float32)], axis=2)
        write_png(os.path.join(out, "fx_preview.png"), (np.clip(img, 0, 1) * 255).astype(np.uint8))


if __name__ == "__main__":
    main()
