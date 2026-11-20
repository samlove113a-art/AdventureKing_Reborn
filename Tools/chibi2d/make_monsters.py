"""Original 2D chibi monsters for Chương 1 Map 3-10 (Elite, Mini Boss, Boss included): analytic shapes, hard cel shading,
1 px ink outline, hard alpha, facing right. Every monster gets the clips the game's Animator contract expects: Idle, Walk, Attack, Hit, Death.
No outside art, no Meshy credits.

  <blender python> Tools/chibi2d/make_monsters.py <out_dir=ArtSource/Chibi2D> [Name ...] [--preview]

Output per monster: <out>/<Name>/<Name>_<Clip>.png (frame strips) + <Name>_frames.json (the contract Chibi2DArt reads) [+ <Name>_preview.png]
"""
import json
import math
import os
import struct
import sys
import zlib

import numpy as np

SS = 3
INK = np.array([0.15, 0.07, 0.22], np.float32)


def rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], np.float32)


def write_png(path, arr):
    h, w = arr.shape[:2]
    raw = b"".join(b"\x00" + arr[y].tobytes() for y in range(h))

    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


class Cv:
    """Drawing surface in a 128-unit design space (scaled to `cell` px). Pose = inverse-mapped sampling grid."""

    def __init__(self, cell, sx=1.0, sy=1.0, rot=0.0, dx=0.0, dy=0.0, pivot=(64.0, 118.0)):
        self.cell, self.n = cell, cell * SS
        yy, xx = np.mgrid[0:self.n, 0:self.n].astype(np.float32)
        X, Y = (xx + 0.5) / (self.n / 128.0), (yy + 0.5) / (self.n / 128.0)
        px, py = pivot
        X, Y = X - dx - px, Y - dy - py                       # undo the translation, rotate / scale about the pivot (feet)
        c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        X, Y = c * X + s * Y, -s * X + c * Y
        self.X, self.Y = X / sx + px, Y / sy + py
        self.col = np.zeros((self.n, self.n, 3), np.float32)
        self.cov = np.zeros((self.n, self.n), bool)
        self.k = self.n / 128.0

    # shapes: signed distance (design units, < 0 inside)
    def ell(self, cx, cy, rx, ry, rot=0.0):
        c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        dx, dy = self.X - cx, self.Y - cy
        u, v = (c * dx + s * dy) / rx, (-s * dx + c * dy) / ry
        return (np.sqrt(u * u + v * v) - 1.0) * min(rx, ry)

    def box(self, cx, cy, hx, hy, r=3.0, rot=0.0):
        c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        dx, dy = self.X - cx, self.Y - cy
        u, v = c * dx + s * dy, -s * dx + c * dy
        qx, qy = np.abs(u) - hx + r, np.abs(v) - hy + r
        return np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2) + np.minimum(np.maximum(qx, qy), 0) - r

    def tri(self, a, b, c):
        pts = [a, b, c]
        sgn = 1 if ((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])) > 0 else -1
        inside = np.ones_like(self.X, bool)
        dist = np.full_like(self.X, 1e9)
        for i in range(3):
            p, q = pts[i], pts[(i + 1) % 3]
            ex, ey = q[0] - p[0], q[1] - p[1]
            L = math.hypot(ex, ey) + 1e-6
            inside &= sgn * ((self.X - p[0]) * ey - (self.Y - p[1]) * ex) / L <= 0
            t = np.clip(((self.X - p[0]) * ex + (self.Y - p[1]) * ey) / (L * L), 0, 1)
            dist = np.minimum(dist, np.hypot(self.X - p[0] - t * ex, self.Y - p[1] - t * ey))
        return np.where(inside, -dist, dist)

    def line(self, ax, ay, bx, by, r):
        px, py = self.X - ax, self.Y - ay
        vx, vy = bx - ax, by - ay
        h = np.clip((px * vx + py * vy) / (vx * vx + vy * vy + 1e-6), 0, 1)
        return np.hypot(px - vx * h, py - vy * h) - r

    def part(self, sd, base, shade=None, light=None, outline=True):
        m = sd < 0
        c = np.broadcast_to(base, (self.n, self.n, 3)).copy()
        k = self.k
        if shade is not None:
            off = np.roll(np.roll(sd, int(2.4 * k), axis=0), int(-2.4 * k), axis=1)
            c[m & (off >= 0)] = shade
        if light is not None:
            lo = np.roll(np.roll(sd, int(-2.6 * k), axis=0), int(2.6 * k), axis=1)
            sh = np.roll(np.roll(sd, int(2.4 * k), axis=0), int(-2.4 * k), axis=1)
            c[m & (lo >= 0) & ~(m & (sh >= 0))] = light
        if outline:
            ring = (sd >= 0) & (sd < 0.9)
            self.col[ring] = INK
            self.cov |= ring
        self.col[m] = c[m]
        self.cov |= m

    def eye(self, cx, cy, r=2.6, fierce=False, color="#241638", glow=None):
        if glow is not None:
            self.part(self.ell(cx, cy, r * 1.15, r * 1.5), glow, outline=False)
        else:
            self.part(self.ell(cx, cy, r, r * 1.3), rgb(color), outline=False)
            self.part(self.ell(cx - r * 0.3, cy - r * 0.45, r * 0.35, r * 0.4), rgb("#ffffff"), outline=False)
        if fierce:   # angry brow
            self.part(self.line(cx - r * 1.4, cy - r * 2.1, cx + r * 1.2, cy - r * 1.1, 0.9), INK, outline=False)

    def png(self, tint=None):
        k = SS
        cell = self.cell
        cov = self.cov.reshape(cell, k, cell, k).mean((1, 3))
        col = self.col.reshape(cell, k, cell, k, 3).mean((1, 3))
        if tint is not None:
            col = col * (1 - tint[1]) + tint[0] * tint[1]
        out = np.zeros((cell, cell, 4), np.float32)
        out[..., :3] = col
        a = cov > 0.5
        out[..., 3] = a
        edge = a & ~(np.roll(a, 1, 0) & np.roll(a, -1, 0) & np.roll(a, 1, 1) & np.roll(a, -1, 1))
        out[edge, :3] = INK
        out[~a, :3] = 0
        return (np.clip(out, 0, 1) * 255).astype(np.uint8)


# ------------------------------------------------------------------ the bodies. Each draws facing right at the pose already set on the canvas.
def mushroom(cv, p, st):
    """Walking mushroom: stem body, cap, face, feet; style flags add crown / spikes / helmet / cape / horns."""
    ph = p.get("phase", 0.0)
    cap, capsh, caphi = st["cap"]
    stem = st.get("stem", (rgb("#f6ecd6"), rgb("#d8c6a2"), rgb("#ffffff")))
    S = st.get("size", 1.0)                       # body size multiplier inside the design box
    cx, ground = 60.0, 116.0
    if st.get("aura") is not None:        # drifting motes BEHIND the body
        for i in range(4):
            a = ph + i * 1.6
            cv.part(cv.ell(cx + math.cos(a) * 30 * S, ground - 44 * S + math.sin(a * 1.3) * 18 * S, 3.5, 5.5, 20), st["aura"], outline=False)
    if st.get("cape"):
        c, cs = st["cape"]
        cv.part(cv.tri((cx - 10 * S, ground - 62 * S), (cx - 38 * S, ground - 4), (cx + 6 * S, ground - 8)), c, cs)
    legs = [(-9, 0), (9, 1)]
    for lx, i in legs:
        lift = max(0.0, math.sin(ph + i * math.pi)) * 5 if p.get("walk") else 0
        cv.part(cv.ell(cx + lx * S, ground - 4 - lift, 8 * S, 5 * S), st.get("feet", rgb("#7a4e2a")), rgb("#54331a"))
    bw, bh = 20 * S, 22 * S
    cv.part(cv.ell(cx, ground - 6 - bh, bw, bh), *stem)                          # body / stem
    if st.get("armor"):
        a, ash = st["armor"]
        cv.part(cv.box(cx, ground - 20 * S, 16 * S, 12 * S, 4), a, ash, outline=True)
    cy = ground - 6 - bh * 1.7 - 2
    cv.part(cv.ell(cx, cy, 36 * S, 24 * S), cap, capsh, caphi)                  # cap
    for (sx, sy, r) in st.get("spots", []):
        cv.part(cv.ell(cx + sx * S, cy + sy * S, r * S, r * S * 0.8), st.get("spot", rgb("#ffffff")), outline=False)
    if st.get("spikes") is not None:
        for i in range(-2, 3):
            cv.part(cv.tri((cx + i * 12 * S - 5, cy - 14 * S - abs(i) * 2), (cx + i * 12 * S, cy - 34 * S + abs(i) * 4), (cx + i * 12 * S + 5, cy - 14 * S - abs(i) * 2)), st["spikes"], st.get("spikesh"))
    if st.get("horns") is not None:
        for sgn in (-1, 1):
            cv.part(cv.tri((cx + sgn * 24 * S, cy - 8 * S), (cx + sgn * 40 * S, cy - 34 * S), (cx + sgn * 14 * S, cy - 16 * S)), st["horns"], st.get("hornsh"))
    if st.get("crown"):
        g, gs = st["crown"]
        cv.part(cv.box(cx, cy - 20 * S, 22 * S, 5 * S, 1.5), g, gs)
        for i in range(-2, 3):
            cv.part(cv.tri((cx + i * 9 * S - 4, cy - 24 * S), (cx + i * 9 * S, cy - 36 * S - (2 - abs(i)) * 3), (cx + i * 9 * S + 4, cy - 24 * S)), g, gs)
        cv.part(cv.ell(cx, cy - 22 * S, 3 * S, 3 * S), rgb("#e0405a"), outline=False)
    if st.get("leaf_crown"):
        for i in range(-2, 3):
            cv.part(cv.ell(cx + i * 9 * S, cy - 22 * S - (2 - abs(i)) * 2, 3 * S, 8 * S, i * 16), rgb("#58b84a"), rgb("#2f7f34"))
    if st.get("helmet"):
        h, hs = st["helmet"]
        cv.part(cv.ell(cx + 2, cy - 4 * S, 24 * S, 15 * S), h, hs, rgb("#e8ecf4"))
        cv.part(cv.tri((cx - 3 * S, cy - 16 * S), (cx, cy - 30 * S), (cx + 3 * S, cy - 16 * S)), rgb("#d0405a"))
    fy = ground - 6 - bh * 1.15
    fierce = st.get("fierce", False)
    eg = st.get("eyeglow")
    cv.eye(cx + 8 * S, fy, 2.8 * S, fierce, glow=eg)
    cv.eye(cx - 4 * S, fy, 2.6 * S, fierce, glow=eg)
    cv.part(cv.ell(cx + 4 * S, fy + 8 * S, 4 * S, 2 * S * (1.6 if p.get("mouth") else 1.0)), rgb("#5a1f2a"), outline=False)   # mouth
    if st.get("fangs"):
        for fx in (-1.5, 6.5):
            cv.part(cv.tri((cx + fx * S, fy + 6 * S), (cx + (fx + 1.5) * S, fy + 11 * S), (cx + (fx + 3) * S, fy + 6 * S)), rgb("#ffffff"), outline=False)
    if st.get("spear"):
        reach = p.get("reach", 0.0)
        cv.part(cv.line(cx + 6 * S + reach, ground - 22 * S, cx + 46 * S + reach, ground - 30 * S, 1.6), rgb("#8a6a3a"))
        cv.part(cv.tri((cx + 46 * S + reach, ground - 35 * S), (cx + 60 * S + reach, ground - 30 * S), (cx + 46 * S + reach, ground - 25 * S)), rgb("#e4ecf6"), rgb("#9aa8c0"))
    if st.get("scepter"):
        reach = p.get("reach", 0.0)
        cv.part(cv.line(cx + 22 * S + reach, ground - 4, cx + 30 * S + reach, ground - 70 * S, 2.2), rgb("#5a3a20"))
        cv.part(cv.ell(cx + 30 * S + reach, ground - 74 * S, 6 * S, 6 * S), rgb("#c040e0"), rgb("#7a1f9a"), rgb("#f0b0ff"))



def slime(cv, p, st):
    base, sh, hi = st["body"]
    cx, ground = 64.0, 116.0
    cv.part(cv.ell(cx, ground - 20, 40, 22 + p.get("puff", 0.0)), base, sh, hi)
    cv.part(cv.ell(cx - 18, ground - 30, 7, 4), rgb("#ffffff"), outline=False)
    cap, capsh, caphi = st["cap"]
    cv.part(cv.ell(cx + 14, ground - 46, 18, 11), cap, capsh, caphi)                 # a mushroom growing on its head
    cv.part(cv.ell(cx + 14, ground - 36, 6, 6), rgb("#f6ecd6"), rgb("#d8c6a2"))
    for sx, sy in ((6, -50), (20, -47), (14, -52)):
        cv.part(cv.ell(cx + sx, ground + sy, 2.4, 2), rgb("#ffffff"), outline=False)
    cv.eye(cx + 14, ground - 22, 3.2)
    cv.eye(cx - 2, ground - 22, 3.0)
    cv.part(cv.ell(cx + 6, ground - 12, 5, 2.4 * (1.6 if p.get("mouth") else 1)), rgb("#3d1a2a"), outline=False)
    for i, (bx, by, r) in enumerate(st.get("bubbles", [])):
        yo = (p.get("phase", 0.0) * 6 + i * 9) % 22
        cv.part(cv.ell(cx + bx, ground + by - yo, r, r), st.get("bubble", rgb("#c8ffb0")), outline=True)


def flyer(cv, p, st):
    ph = p.get("phase", 0.0)
    flap = math.sin(ph * 2.0) * 22 if not p.get("still") else 0
    cx, cy = 60.0, 66.0
    kind = st["kind"]
    if kind == "bee":
        cv.part(cv.ell(cx - 24, cy + 6, 8, 3.5, 20), rgb("#3a2a1a"))               # stinger
        for sgn in (-1, 1):
            cv.part(cv.ell(cx - 6 + sgn * 4, cy - 28, 9, 16, 28 * sgn + flap * 0.6), rgb("#ffd6ec"), rgb("#f0a0c8"), rgb("#ffffff"))   # petal wings
        cv.part(cv.ell(cx, cy, 30, 24), rgb("#ffd94a"), rgb("#e6a820"), rgb("#fff3a0"))
        for bx in (-12, 0, 12):
            cv.part(cv.box(cx + bx, cy, 4, 20, 2), rgb("#4a2f1a"), outline=False)
        cv.part(cv.ell(cx + 26, cy - 2, 16, 14), rgb("#ffd94a"), rgb("#e6a820"), rgb("#fff3a0"))
        cv.part(cv.line(cx + 30, cy - 14, cx + 38, cy - 28, 1.0), INK); cv.part(cv.ell(cx + 38, cy - 29, 2.4, 2.4), rgb("#e0405a"), outline=False)
        cv.eye(cx + 32, cy - 4, 2.8, True)
        cv.part(cv.ell(cx + 36, cy + 6, 3, 1.6 * (1.8 if p.get("mouth") else 1)), rgb("#5a1f2a"), outline=False)
    elif kind == "bat":
        for sgn, ox in ((1, -2), (-1, 6)):
            cv.part(cv.tri((cx + ox, cy - 6), (cx - 34 + ox * 0 - flap * 0.4, cy - 36 - flap), (cx - 8 + ox, cy + 12)), rgb("#b490ff"), rgb("#7a52d8"), rgb("#e0d0ff"))
            cv.part(cv.tri((cx + ox - 26, cy - 22 - flap * 0.7), (cx + ox - 34, cy - 36 - flap), (cx + ox - 16, cy - 16 - flap * 0.5)), rgb("#7ee8ff"), rgb("#34a8d0"), rgb("#d8fbff"))
        cv.part(cv.ell(cx + 2, cy + 2, 22, 20), rgb("#5a3aa0"), rgb("#3a2278"), rgb("#8a6ad8"))
        for sgn in (-1, 1):
            cv.part(cv.tri((cx + 10 + sgn * 8 - 4, cy - 14), (cx + 10 + sgn * 9, cy - 32), (cx + 10 + sgn * 8 + 5, cy - 14)), rgb("#5a3aa0"), rgb("#3a2278"))
        cv.part(cv.ell(cx + 22, cy, 15, 13), rgb("#6e4cc0"), rgb("#4a2c96"), rgb("#a088f0"))
        cv.eye(cx + 28, cy - 3, 2.8, True, glow=rgb("#ffe85a"))
        cv.part(cv.tri((cx + 24, cy + 7), (cx + 26, cy + 14), (cx + 29, cy + 7)), rgb("#ffffff"), outline=False)
        cv.part(cv.tri((cx + 30, cy + 6), (cx + 32, cy + 12), (cx + 34, cy + 6)), rgb("#ffffff"), outline=False)
        cv.part(cv.tri((cx - 26, cy + 12), (cx - 38, cy + 8), (cx - 24, cy + 20)), rgb("#7ee8ff"), rgb("#34a8d0"))      # crystal tail
    else:   # shadow leaf spirit
        sway = math.sin(ph) * 3
        cv.part(cv.tri((cx - 30, cy + 22), (cx - 8, cy + 6 + sway), (cx - 6, cy + 20)), rgb("#4a2a78"), rgb("#2e1a54"))
        cv.part(cv.ell(cx - 4, cy, 18, 32, 70), rgb("#7a4cc0"), rgb("#4e2a8a"), rgb("#b090f0"))      # pointed leaf body
        cv.part(cv.tri((cx + 24, cy - 6), (cx + 44, cy + 2), (cx + 24, cy + 10)), rgb("#7a4cc0"), rgb("#4e2a8a"))
        cv.part(cv.line(cx - 28, cy + sway, cx + 24, cy, 0.9), rgb("#c8a8ff"), outline=False)        # leaf vein
        cv.eye(cx + 16, cy - 4, 3.0, True, glow=rgb("#ffe0ff"))
        cv.eye(cx + 4, cy - 4, 2.6, True, glow=rgb("#ffe0ff"))
        for i in range(3):
            a = ph + i * 2.1
            cv.part(cv.ell(cx - 18 + math.cos(a) * 10, cy + 24 + math.sin(a) * 4, 3, 3), rgb("#a888e8"), outline=False)


def turtle(cv, p, st):
    ph = p.get("phase", 0.0)
    cx, ground = 60.0, 116.0
    for lx, i in ((-18, 0), (16, 1)):
        lift = max(0.0, math.sin(ph + i * math.pi)) * 5 if p.get("walk") else 0
        cv.part(cv.ell(cx + lx, ground - 6 - lift, 9, 7), rgb("#7fcf6a"), rgb("#4f9f48"))
    cv.part(cv.ell(cx - 30, ground - 22, 7, 5, -20), rgb("#7fcf6a"), rgb("#4f9f48"))                 # tail
    cv.part(cv.ell(cx, ground - 32, 34, 28), rgb("#2fb090"), rgb("#1c7a6a"), rgb("#7ae8c8"))          # shell
    for hx, hy in ((0, -48), (-16, -34), (16, -34), (0, -22)):
        cv.part(cv.ell(cx + hx, ground + hy, 7, 6), rgb("#90f0d8"), rgb("#4cc0a4"), outline=True)      # jade plates
    cv.part(cv.ell(cx + 36, ground - 36, 14, 13), rgb("#7fcf6a"), rgb("#4f9f48"), rgb("#b8f0a0"))     # head
    cv.eye(cx + 42, ground - 39, 2.6)
    cv.part(cv.ell(cx + 46, ground - 31, 3.2, 1.6 * (1.8 if p.get("mouth") else 1)), rgb("#5a1f2a"), outline=False)
    cv.part(cv.ell(cx + 36, ground - 30, 4, 2.4), rgb("#ff9a80"), outline=False)


def golem(cv, p, st):
    ph = p.get("phase", 0.0)
    cx, ground = 60.0, 116.0
    b, bs, bh = st["stone"]
    g, gs = st["gem"]
    for lx, i in ((-12, 0), (12, 1)):
        lift = max(0.0, math.sin(ph + i * math.pi)) * 4 if p.get("walk") else 0
        cv.part(cv.box(cx + lx, ground - 12 - lift, 9, 12, 3), b, bs, bh)
    arm = p.get("arm", 0.0)
    cv.part(cv.box(cx - 30, ground - 44, 7, 16, 3, -8), b, bs, bh)                                      # back arm
    cv.part(cv.box(cx, ground - 46, 24, 24, 6), b, bs, bh)                                              # torso
    cv.part(cv.ell(cx + 4, ground - 46, 7, 9), g, gs, rgb("#ffffff"))                                  # chest gem
    cv.part(cv.box(cx + 2, ground - 76 + arm * 0.0, 15, 13, 5), b, bs, bh)                              # head
    cv.eye(cx + 10, ground - 78, 2.8, True, glow=st.get("eyeglow", rgb("#ffe85a")))
    cv.eye(cx - 3, ground - 78, 2.6, True, glow=st.get("eyeglow", rgb("#ffe85a")))
    cv.part(cv.box(cx + 4, ground - 68, 6, 1.6, 0.8), rgb("#2a1a2a"), outline=False)
    if st.get("sprout"):
        cv.part(cv.line(cx + 2, ground - 89, cx + 4, ground - 100, 1.4), rgb("#3a8a38"))
        cv.part(cv.ell(cx + 10, ground - 101, 7, 3.5, -20), rgb("#7ad060"), rgb("#3f9a3f"))
        cv.part(cv.ell(cx - 4, ground - 100, 7, 3.5, 20), rgb("#7ad060"), rgb("#3f9a3f"))
    if st.get("shield"):
        cv.part(cv.box(cx + 30 + arm * 0.5, ground - 46, 6, 20, 3), rgb("#8a6a4a"), rgb("#5a4028"), rgb("#b89a74"))
    cv.part(cv.box(cx + 26 + arm, ground - 52, 8, 14, 3, 6), b, bs, bh)                                 # front arm / fist
    if st.get("crystals"):
        for i, (sx, sy) in enumerate(((-18, -70), (-8, -76), (8, -72))):
            cv.part(cv.tri((cx + sx - 4, ground + sy + 14), (cx + sx, ground + sy), (cx + sx + 4, ground + sy + 14)), g, gs)


def slimeish(cv, p, st):
    slime(cv, p, st)


BUILDERS = {"mushroom": mushroom, "slime": slime, "flyer": flyer, "turtle": turtle, "golem": golem}

R = rgb
# name -> (builder, style, cell, frame_m, display scale in Unity)
MONSTERS = {
    "GlowMoss": ("mushroom", dict(cap=(R("#3ac8c0"), R("#1f8a96"), R("#a8fff0")), spots=[(-14, -6, 5), (8, -12, 4), (20, 0, 3.5)], spot=R("#e8fffa"),
                                    stem=(R("#d8f8e8"), R("#98d0b8"), R("#ffffff")), aura=R("#9ffff0")), 128, 1.5),
    "MushroomChief": ("mushroom", dict(cap=(R("#a8442f"), R("#6e2418"), R("#e07a58")), spots=[(-16, -8, 6), (6, -14, 5), (22, -2, 4)], spot=R("#ffe8c0"), size=1.05,
                                       leaf_crown=True, cape=(R("#3c7a3a"), R("#245024")), scepter=True, fierce=True, stem=(R("#f0e2c4"), R("#cfb98c"), R("#ffffff"))), 256, 3.0),
    "FlowerBee": ("flyer", dict(kind="bee"), 128, 1.5),
    "CrystalBat": ("flyer", dict(kind="bat"), 128, 1.5),
    "JadeTurtle": ("turtle", dict(), 128, 1.5),
    "JadeGuardian": ("golem", dict(stone=(R("#5a86c8"), R("#34568f"), R("#a8c8f4")), gem=(R("#46e0a8"), R("#1fa07a")), crystals=True), 256, 3.0),
    "ShadowLeaf": ("flyer", dict(kind="leaf"), 128, 1.5),
    "ToxicSlime": ("slime", dict(body=(R("#8ad050"), R("#4f9a2e"), R("#d0ff9a")), cap=(R("#9a48c8"), R("#62288a"), R("#d890f4")),
                                 bubbles=[(-22, -10, 3), (24, -4, 2.4), (4, -2, 3.4)]), 128, 1.5),
    "ToxicElite": ("mushroom", dict(cap=(R("#7a3aa8"), R("#4a1f72"), R("#b878e0")), spots=[(-14, -6, 5), (10, -12, 4.5), (22, 0, 3.5)], spot=R("#d6ff66"), size=1.0,
                                    spikes=R("#d6ff66"), spikesh=R("#8ab820"), fierce=True, fangs=True, eyeglow=R("#d6ff66"), aura=R("#a0e830"),
                                    stem=(R("#c8b8e0"), R("#8c78b0"), R("#f0e8ff"))), 192, 2.3),
    "StoneGuard": ("golem", dict(stone=(R("#9a9488"), R("#66604f"), R("#cfc8b8")), gem=(R("#7ad060"), R("#3f9a3f")), sprout=True, shield=True), 128, 1.5),
    "DarkMushroom": ("mushroom", dict(cap=(R("#3a2058"), R("#1c0e34"), R("#7a52a8")), spots=[(-16, -8, 5), (8, -14, 4.5), (22, -2, 4)], spot=R("#ff4a6a"), size=1.05,
                                     horns=R("#e8e0f0"), hornsh=R("#9a90b0"), fierce=True, fangs=True, eyeglow=R("#ff3a52"), aura=R("#8a4ad8"),
                                     stem=(R("#6a5a88"), R("#3e3258"), R("#9a8ac0"))), 256, 3.0),
    "RoyalGuard": ("mushroom", dict(cap=(R("#a02850"), R("#661632"), R("#e06a8a")), spots=[(-14, -6, 4), (10, -12, 3.5)], spot=R("#ffe8f0"), size=1.0,
                                    helmet=(R("#aab4c8"), R("#6e7a94")), armor=(R("#aab4c8"), R("#6e7a94")), spear=True, fierce=True), 128, 1.5),
    "MushroomKing": ("mushroom", dict(cap=(R("#c01838"), R("#7a0c26"), R("#f0587a")), spots=[(-20, -8, 7), (4, -16, 6), (24, -4, 5)], spot=R("#ffe0a0"), size=1.15,
                                      crown=(R("#ffd45c"), R("#d98a1c")), horns=R("#2a1a3a"), hornsh=R("#120a20"), cape=(R("#2a1040"), R("#14082a")), fierce=True,
                                      fangs=True, scepter=True, eyeglow=R("#ffe05a"), aura=R("#d83a8a"), stem=(R("#e8d8c8"), R("#b49c88"), R("#ffffff"))), 256, 3.6),
}
SCALE = {"GlowMoss": 1.0}   # (display scale lives in Chibi2DArt.Actors; frame_m already sets the real size)

CLIPS = {"Idle": (6, 8, True), "Walk": (6, 10, True), "Attack": (6, 12, False), "Hit": (3, 12, False), "Death": (5, 8, False)}


def pose(clip, i, n, kind, st):
    t = i / n
    p = dict(phase=t * math.tau)
    sx = sy = 1.0; rot = 0.0; dx = dy = 0.0
    fly = kind == "flyer"
    if clip == "Idle":
        sy = 1 + 0.04 * math.sin(t * math.tau); sx = 1 / math.sqrt(sy)
        if fly: dy = -4 * math.sin(t * math.tau)
    elif clip == "Walk":
        p["walk"] = True
        dy = -abs(math.sin(t * math.tau)) * (5 if not fly else 3)
        sy = 1 + 0.03 * math.cos(t * math.tau * 2); sx = 1 / math.sqrt(sy)
        rot = math.sin(t * math.tau) * 2
    elif clip == "Attack":
        k = i / max(1, n - 1)
        if k < 0.35: dx, rot, sy = -8 * (k / 0.35), -6 * (k / 0.35), 1.06
        elif k < 0.65: dx, rot, sy = -8 + 26 * ((k - 0.35) / 0.3), 8, 0.94
        else: dx, rot = 18 * (1 - (k - 0.65) / 0.35), 4 * (1 - (k - 0.65) / 0.35)
        sx = 1 / math.sqrt(sy)
        p["mouth"] = 0.35 < k < 0.7
        p["reach"] = 14 * max(0.0, math.sin(min(1.0, max(0.0, (k - 0.3) / 0.4)) * math.pi))
        p["arm"] = p["reach"] * 0.8
        p["puff"] = 6 * math.sin(k * math.pi)
    elif clip == "Hit":
        dx, rot = -7 * (1 if i < 2 else 0.4), -10 * (1 if i < 2 else 0.3)
        sy = 0.9 if i == 1 else 1.0; sx = 1 / math.sqrt(sy)
        p["mouth"] = True
    elif clip == "Death":
        k = i / max(1, n - 1)
        rot = -14 * min(1.0, k * 1.4) if kind != "slime" else 0
        sy = max(0.16, 1 - 0.84 * k); sx = 1 + 0.45 * k
        dx = -6 * k
        if fly: dy = 30 * k
        p["mouth"] = True
    return p, dict(sx=sx, sy=sy, rot=rot, dx=dx, dy=dy)


def render_actor(name, outdir, preview=False):
    kind, st, cell, frame_m = MONSTERS[name]
    folder = os.path.join(outdir, name)
    os.makedirs(folder, exist_ok=True)
    meta = {"cell": cell, "pixel": True, "frame_m": frame_m, "ground_from_bottom_m": round(frame_m * 10.0 / 128.0, 4), "clips": {}}
    prev = []
    for clip, (n, fps, loop) in CLIPS.items():
        frames = []
        for i in range(n):
            p, xf = pose(clip, i, n, kind, st)
            cv = Cv(cell, pivot=(60.0, 118.0), **xf)
            BUILDERS[kind](cv, p, st)
            tint = (np.array([1, 1, 1], np.float32), 0.55) if clip == "Hit" and i == 0 else None
            frames.append(cv.png(tint))
        strip = np.concatenate(frames, axis=1)
        write_png(os.path.join(folder, f"{name}_{clip}.png"), strip)
        meta["clips"][clip] = {"frames": n, "fps": fps, "loop": loop, "file": f"{name}_{clip}.png"}
        prev.append(strip)
    json.dump(meta, open(os.path.join(folder, f"{name}_frames.json"), "w", encoding="utf-8"), indent=2)
    if preview:
        w = max(p.shape[1] for p in prev)
        sheet = np.concatenate([np.pad(p, ((0, 0), (0, w - p.shape[1]), (0, 0))) for p in prev], axis=0).astype(np.float32) / 255.0
        bg = np.zeros(sheet.shape[:2] + (3,), np.float32) + np.array([0.7, 0.8, 0.7], np.float32)
        al = sheet[..., 3:4]
        img = np.concatenate([sheet[..., :3] * al + bg * (1 - al), np.ones(sheet.shape[:2] + (1,), np.float32)], axis=2)
        write_png(os.path.join(folder, f"{name}_preview.png"), (np.clip(img, 0, 1) * 255).astype(np.uint8))
    print("wrote", name, flush=True)


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    out = args[0] if args else os.path.join(os.path.dirname(__file__), "..", "..", "ArtSource", "Chibi2D")
    names = args[1:] or list(MONSTERS)
    for n in names:
        render_actor(n, out, "--preview" in sys.argv)
