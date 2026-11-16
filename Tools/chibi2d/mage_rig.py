"""AdventureKing original 2D chibi Mage v2: hand-drawn style, layered, side view (faces right).

Run with the Python that ships with Blender (numpy only):
  "<Blender>/5.2/python/bin/python.exe" Tools/chibi2d/mage_rig.py [out_dir] [only_clip,...] [--preview]

Every body part is an analytic shape drawn back to front at 2x supersampling: hard two-tone cel shading (base / shadow / light),
a crisp ink line round every part and one bold silhouette outline, strand and fold lines drawn on top. Poses move the parts as a
skeleton (smooth at any frame count; outfit and weapon are separate layers). Proportions: head ~50 % of the height, stubby body,
big expressive eye. Original design, no outside art. Output: one PNG strip per clip + Mage_frames.json (Unity importer contract)
and, with --preview, a big review sheet (stand / run / jump / attack) on a neutral background.
"""
import json
import math
import os
import struct
import sys
import zlib

import numpy as np

PIXEL = "--pixel" in sys.argv     # premium pixel-art output: 96 px frames, fixed palette, hard 1 px outline, no anti-aliasing
LW = 3.2 if PIXEL else 1.0            # line-weight multiplier: ink lines must be a full pixel wide to survive the downsample
PX = 96                               # pixel-art frame size
S = 384                  # frame size (design px)
SS = 2                   # supersampling
CX, G = 150.0, 364.0     # root x (feet centre) and ground line
BASE = 0.9               # whole-character scale inside the frame
FRAME_M = 1.185          # metres covered by one frame side
GROUND_M = (S - G) / (S / FRAME_M)

_Y, _X = np.mgrid[0:S * SS, 0:S * SS].astype(np.float32)
X = (_X + 0.5) / SS
Y = (_Y + 0.5) / SS
INK = np.array([0.15, 0.08, 0.2], np.float32)


def rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], np.float32)


# (base, shadow, light, ink line)
PAL = {
    "skin": (rgb("#ffe0cc"), rgb("#f1b49a"), rgb("#fff4ea"), rgb("#7a3a3a")),
    "hair": (rgb("#cdb4ff"), rgb("#9d7fe6"), rgb("#f0e6ff"), rgb("#3f2a78")),
    "hat": (rgb("#6a48cc"), rgb("#43299a"), rgb("#9d84f2"), rgb("#1f1250")),
    "band": (rgb("#ff8fb8"), rgb("#d9588a"), rgb("#ffc2d8"), rgb("#5a1840")),
    "gold": (rgb("#ffd45c"), rgb("#d9922b"), rgb("#fff3b0"), rgb("#6a3d0c")),
    "dress": (rgb("#3f62d0"), rgb("#2b43a0"), rgb("#7a9cf4"), rgb("#14224f")),
    "cape": (rgb("#6a3fb0"), rgb("#47287f"), rgb("#9a74e0"), rgb("#1f1050")),
    "white": (rgb("#fbfbff"), rgb("#cdd0ec"), rgb("#ffffff"), rgb("#3a3f78")),
    "wood": (rgb("#b27a42"), rgb("#7a4c28"), rgb("#dfa664"), rgb("#3c2010")),
    "boot": (rgb("#8c5630"), rgb("#5c371c"), rgb("#bd7a46"), rgb("#2a1408")),
    "gem": (rgb("#b85cff"), rgb("#7a2cd4"), rgb("#f0c8ff"), rgb("#2c0a5c")),
    "sock": (rgb("#4d6ad0"), rgb("#33459e"), rgb("#8097ee"), rgb("#14224f")),
    "pink": (rgb("#ff8fb8"), rgb("#d9588a"), rgb("#ffc2d8"), rgb("#5a1840")),
}


# ------------------------------------------------------------------ shapes (signed distance in design px; < 0 inside)
def ellipse(x, y, cx, cy, rx, ry):
    return (np.sqrt(((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2) - 1.0) * min(rx, ry)


def capsule(x, y, ax, ay, bx, by, ra, rb):
    px, py = x - ax, y - ay
    vx, vy = bx - ax, by - ay
    h = np.clip((px * vx + py * vy) / (vx * vx + vy * vy + 1e-6), 0, 1)
    return np.sqrt((px - vx * h) ** 2 + (py - vy * h) ** 2) - (ra + (rb - ra) * h)


def box(x, y, cx, cy, hx, hy, r=0.0):
    qx, qy = np.abs(x - cx) - hx + r, np.abs(y - cy) - hy + r
    return np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2) + np.minimum(np.maximum(qx, qy), 0) - r


def star(x, y, cx, cy, ro, ri, rot=-90.0):
    ang = np.arctan2(y - cy, x - cx) - math.radians(rot)
    r = np.hypot(x - cx, y - cy)
    k = (np.cos(ang * 5) + 1) / 2
    return (r - (ri + (ro - ri) * k ** 1.5)) * 0.85


def union(*ds):
    out = ds[0]
    for d in ds[1:]:
        out = np.minimum(out, d)
    return out


def smin(a, b, k=6.0):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)


def ring(sd, w):
    """A line of width w centred on the zero set of sd."""
    return np.abs(sd) - w * LW * 0.5


def lock(x, y, pts, r0, r1):
    """Tapered hair lock along a polyline."""
    d = None
    n = len(pts) - 1
    for i in range(n):
        a, b = pts[i], pts[i + 1]
        ra = r0 + (r1 - r0) * i / n
        rb = r0 + (r1 - r0) * (i + 1) / n
        c = capsule(x, y, a[0], a[1], b[0], b[1], ra, rb)
        d = c if d is None else np.minimum(d, c)
    return d


# ------------------------------------------------------------------ transforms
class Xf:
    """Chain of (pivot, angle deg clockwise, translation, scale): screen -> rest coordinates of the last link."""

    def __init__(self, links):
        self.links = links

    def __call__(self, x, y):
        for (ox, oy), ang, (tx, ty), (sx, sy) in self.links:
            dx, dy = x - ox - tx, y - oy - ty
            c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
            x = ox + (c * dx + s * dy) / sx
            y = oy + (-s * dx + c * dy) / sy
        return x, y

    def child(self, pivot, ang=0.0, t=(0, 0), s=(1, 1)):
        return Xf(self.links + [(pivot, ang, t, s)])

    def forward(self, pt):
        x, y = pt
        for (ox, oy), ang, (tx, ty), (sx, sy) in reversed(self.links):
            dx, dy = (x - ox) * sx, (y - oy) * sy
            c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
            x = ox + tx + c * dx - s * dy
            y = oy + ty + s * dx + c * dy
        return x, y


def _shift_max(a, dy, dx):
    out = a.copy()
    h, w = a.shape
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = np.maximum(out[yd, xd], a[ys, xs])
    return out


def dilate_octagon(a, r):
    """Round-ish dilation by r px: a square pass then a diamond pass (fast, separable shifts)."""
    sq = int(round(r * 0.62))
    dm = r - sq
    out = a
    for axis in (0, 1):
        acc = out
        for k in range(1, sq + 1):
            acc = np.maximum(acc, np.maximum(_shift_max(out, k if axis == 0 else 0, k if axis == 1 else 0),
                                             _shift_max(out, -k if axis == 0 else 0, -k if axis == 1 else 0)))
        out = acc
    for _ in range(dm):
        out = np.maximum.reduce([out, _shift_max(out, 1, 0), _shift_max(out, -1, 0), _shift_max(out, 0, 1), _shift_max(out, 0, -1)])
    return out


# ------------------------------------------------------------------ pixel-art conversion
def _palette():
    cols = [c for pal in PAL.values() for c in pal] + [INK] + [rgb(h) for h in (
        "#7a52f0", "#4b2fb8", "#a9c6ff", "#24146a", "#1c1052", "#ffffff", "#bfe0ff", "#ff8fa8", "#ff5f86", "#8a2f52", "#7a1f40",
        "#d98f78", "#6e4fc0", "#8fd0ff", "#241a52", "#e6dcff")]
    out = []
    for c in cols:
        if not any(np.abs(c - o).sum() < 0.02 for o in out):
            out.append(c)
    return np.array(out, np.float32)


PALETTE = None


def pixelate(rgb_hi, alpha_hi):
    """Hi-res render -> PX x PX pixel art: every sub-sample snaps to the fixed palette, each pixel takes the majority colour,
    alpha is 0 / 1 (no soft edges), orphan pixels are removed."""
    global PALETTE
    if PALETTE is None:
        PALETTE = _palette()
    n = len(PALETTE)
    flat = rgb_hi.reshape(-1, 3)
    idx = np.empty(flat.shape[0], np.int16)
    step = 1 << 17
    for i in range(0, flat.shape[0], step):
        d = ((flat[i:i + step, None, :] - PALETTE[None, :, :]) ** 2).sum(-1)
        idx[i:i + step] = d.argmin(1)
    idx = idx.reshape(S * SS, S * SS)
    opaque = alpha_hi > 0.5
    b = (S * SS) // PX
    counts = np.zeros((n, PX, PX), np.int32)
    for k in range(n):
        counts[k] = ((idx == k) & opaque).reshape(PX, b, PX, b).sum(axis=(1, 3))
    cover = opaque.reshape(PX, b, PX, b).sum(axis=(1, 3))
    best = counts.argmax(0)
    on = cover >= (b * b) * 0.45
    nb = np.zeros_like(on, np.int32)
    nb[1:] += on[:-1]; nb[:-1] += on[1:]; nb[:, 1:] += on[:, :-1]; nb[:, :-1] += on[:, 1:]
    on &= nb > 0
    out = np.zeros((PX, PX, 4), np.float32)
    out[..., :3] = PALETTE[best]
    out[..., 3] = on.astype(np.float32)
    out[~on, :3] = 0
    return out

# ------------------------------------------------------------------ painter
class Canvas:
    def __init__(self):
        self.rgb = np.zeros((S * SS, S * SS, 3), np.float32)
        self.a = np.zeros((S * SS, S * SS), np.float32)
        self.flash = 0.0
        self.glows = []

    def over(self, color, cov):
        cov = np.clip(cov, 0, 1)
        self.a = cov + self.a * (1 - cov)
        w = cov[..., None]
        self.rgb = color * w + self.rgb * (1 - w)

    def fill(self, sd, lx, ly, pal, center, r, line=2.0, tint=None, shade=True):
        """Hard two-tone cel fill: shadow on the lower right, light rim on the upper left, ink line round the part."""
        cov = np.clip(0.5 - sd * SS, 0, 1)
        if cov.max() <= 0:
            return
        base, shd, hi, ink = PAL[pal] if isinstance(pal, str) else pal
        t = ((lx - center[0]) * 0.5 + (ly - center[1]) * 0.86) / max(r, 1.0)
        sh = np.clip((t - 0.52) * 60, 0, 1) if shade else np.zeros_like(t)
        li = np.clip((-0.74 - t) * 60, 0, 1) * (1 - sh) if shade else np.zeros_like(t)
        col = base[None, None, :] * (1 - sh[..., None]) + shd[None, None, :] * sh[..., None]
        col = col * (1 - li[..., None]) + hi[None, None, :] * li[..., None]
        if tint is not None:
            col = col * (1 - tint[1]) + tint[0][None, None, :] * tint[1]
        if line > 0:
            e = np.clip((sd + line * LW) * SS * 0.8, 0, 1)          # 0 inside .. 1 at the edge (crisp ink line)
            col = col * (1 - e[..., None]) + ink[None, None, :] * e[..., None]
        self.over(col, cov)

    def solid(self, sd, color, alpha=1.0):
        cov = np.clip(0.5 - sd * SS, 0, 1) * alpha
        self.over(np.asarray(color, np.float32)[None, None, :], cov)

    def glow(self, cx, cy, r, color, k=1.0):
        d = np.hypot(X - cx, Y - cy)
        self.glows.append((np.clip(np.exp(-(d / r) ** 2 * 2.2) * 0.5 * k, 0, 0.8), np.asarray(color, np.float32)))

    def finish(self, ring_px=3.2):
        r = int(round(ring_px * (1.25 if PIXEL else 1.0) * SS))
        outline = dilate_octagon((self.a > 0.3).astype(np.float32), r)
        body = self.a[..., None]
        base = INK[None, None, :] * (1 - body) + self.rgb * body
        if self.flash > 0:
            base = base * (1 - self.flash * body) + self.flash * body
        alpha = np.maximum(outline, self.a)
        if PIXEL:
            return pixelate(base, alpha)
        for ga, gc in self.glows:
            w = ga[..., None]
            base = base * (1 - w) + gc[None, None, :] * w
            alpha = np.maximum(alpha, ga)
        out = np.concatenate([base, alpha[..., None]], axis=2)
        # box downsample (premultiplied)
        pm = out.copy()
        pm[..., :3] *= pm[..., 3:4]
        pm = pm.reshape(S, SS, S, SS, 4).mean(axis=(1, 3))
        a = pm[..., 3:4]
        res = np.zeros_like(pm)
        res[..., :3] = np.where(a > 1e-4, pm[..., :3] / np.maximum(a, 1e-4), 0)
        res[..., 3:4] = a
        return res


# ------------------------------------------------------------------ the character
def pose_default():
    return dict(
        bob=0.0, lean=0.0, sx=1.0, sy=1.0, fall=0.0, fall_dx=0.0,
        head=0.0, head_dx=0.0, head_dy=0.0, hat_lag=0.0, braid=0.0, cape=0.0,
        arm_n=(-18.0, -35.0), arm_f=(12.0, 10.0), leg_n=(0.0, 0.0), leg_f=(0.0, 0.0),
        staff=0.0, eye="open", mouth="smile", flash=0.0, glow=0.35, lift=0.0, blush=1.0,
    )


def render(p):
    cv = Canvas()
    cv.flash = p["flash"]
    root = Xf([((CX, G), p["lean"] + p["fall"], (p["fall_dx"], p["bob"] - p["lift"]), (p["sx"] * BASE, p["sy"] * BASE))])
    hip = (CX + 2, 314)
    sh = (CX + 8, 236)
    hc = (CX + 14, 148)          # head centre

    # ---------------------------------------------------------------- cape (behind everything)
    cx_ = root.child((CX - 2, 232), p["cape"])
    lx, ly = cx_(X, Y)
    cape = smin(capsule(lx, ly, CX - 4, 236, CX - 30, 318, 20, 34), ellipse(lx, ly, CX - 8, 244, 26, 22), 8)
    cv.fill(cape, lx, ly, "cape", (CX - 14, 280), 50)
    cv.fill(np.where(ly > 326, cape, 99.0), lx, ly, "gold", (CX - 20, 330), 20, line=1.4, shade=False)   # hem trim
    cv.solid(np.where(ly < 300, ring(capsule(lx, ly, CX - 8, 250, CX - 26, 300, 3, 5) - 8, 1.4), 99.0), rgb("#9a74e0"), 0.7)

    # ---------------------------------------------------------------- back hair, long locks, braid with a ribbon
    hx = root.child(hc, p["head"], (p["head_dx"], p["head_dy"]))
    lx, ly = hx(X, Y)
    back = union(ellipse(lx, ly, hc[0] - 34, hc[1] + 8, 66, 74), ellipse(lx, ly, hc[0] - 60, hc[1] + 54, 28, 40))
    cv.fill(back, lx, ly, "hair", (hc[0] - 34, hc[1]), 80)
    for k, (dx, dy, rr) in enumerate(((-18, 0, 15), (-46, 8, 14))):         # hanging lock tips behind the shoulders
        tip = lock(lx, ly, [(hc[0] + dx, hc[1] + 56), (hc[0] + dx - 16, hc[1] + 92 + dy), (hc[0] + dx - 8, hc[1] + 120 + dy)], rr, 3)
        cv.fill(tip, lx, ly, "hair", (hc[0] + dx, hc[1] + 90), 50)
    prev = hx
    px, py = hc[0] - 64, hc[1] + 40
    for i in range(5):                                                        # braid
        prev = prev.child((px, py), p["braid"] * (0.5 + i * 0.35))
        lx, ly = prev(X, Y)
        r = 15 - i * 1.8
        cv.fill(ellipse(lx, ly, px, py + 8, r * 0.85, r + 6), lx, ly, "hair", (px, py + 8), r + 8)
        cv.solid(ring(capsule(lx, ly, px - 3, py, px + 3, py + 16, 1.5, 1.5), 1.2), PAL["hair"][3], 0.8)
        py += 14
    lx, ly = prev(X, Y)
    cv.fill(ellipse(lx, ly, px - 7, py + 2, 8, 6), lx, ly, "pink", (px - 7, py + 2), 8, line=1.4)
    cv.fill(ellipse(lx, ly, px + 7, py + 2, 8, 6), lx, ly, "pink", (px + 7, py + 2), 8, line=1.4)

    # ---------------------------------------------------------------- far arm
    dim = (rgb("#241a52"), 0.26)
    sf = root.child(sh, p["arm_f"][0], (-6, 2))
    ef = sf.child((sh[0], sh[1] + 28), p["arm_f"][1])
    lx, ly = sf(X, Y)
    cv.fill(capsule(lx, ly, sh[0], sh[1], sh[0], sh[1] + 30, 11, 9), lx, ly, "white", sh, 34, tint=dim)
    lx, ly = ef(X, Y)
    cv.fill(capsule(lx, ly, sh[0], sh[1] + 28, sh[0], sh[1] + 52, 9, 8), lx, ly, "dress", (sh[0], sh[1] + 40), 30, tint=dim)
    cv.fill(ellipse(lx, ly, sh[0], sh[1] + 58, 9, 9), lx, ly, "skin", (sh[0], sh[1] + 58), 10, tint=dim)

    # ---------------------------------------------------------------- legs: striped sock, boot with cuff and buckle
    def leg(angles, dx, tint):
        hp = root.child((hip[0] + dx, hip[1]), angles[0])
        kn = hp.child((hip[0] + dx, hip[1] + 20), angles[1])
        fx = hip[0] + dx
        lx, ly = hp(X, Y)
        cv.fill(capsule(lx, ly, fx, hip[1], fx, hip[1] + 22, 12, 11), lx, ly, "white", (fx, hip[1] + 12), 22, tint=tint)
        lx, ly = kn(X, Y)
        shin = capsule(lx, ly, fx, hip[1] + 20, fx, hip[1] + 40, 11, 10)
        cv.fill(shin, lx, ly, "white", (fx, hip[1] + 30), 22, tint=tint)
        stripe = (np.floor((ly - (hip[1] + 20)) / 6.0) % 2) == 1
        cv.fill(np.where(stripe, shin, 99.0), lx, ly, "sock", (fx, hip[1] + 30), 22, line=0, tint=tint)
        foot = union(ellipse(lx, ly, fx + 10, hip[1] + 46, 25, 13), ellipse(lx, ly, fx - 2, hip[1] + 40, 14, 14))
        cv.fill(foot, lx, ly, "boot", (fx + 8, hip[1] + 46), 26, tint=tint)
        cv.fill(box(lx, ly, fx - 1, hip[1] + 34, 13, 5, 3), lx, ly, "boot", (fx, hip[1] + 34), 14, tint=tint)          # cuff
        cv.fill(ellipse(lx, ly, fx + 2, hip[1] + 34, 3.5, 3.5), lx, ly, "gold", (fx + 2, hip[1] + 34), 4, line=1.0, tint=tint)  # buckle
        cv.solid(ring(ellipse(lx, ly, fx + 22, hip[1] + 46, 9, 7), 1.4), PAL["boot"][3], 0.7)                            # toe seam

    leg(p["leg_f"], -9, dim)
    leg(p["leg_n"], 11, None)

    # ---------------------------------------------------------------- body: tabard dress, trim, crest, belt, pouch, ruff
    lx, ly = root(X, Y)
    dress = smin(capsule(lx, ly, CX + 4, 246, CX + 2, 290, 30, 38), ellipse(lx, ly, CX + 4, 254, 33, 28), 8)
    cv.fill(dress, lx, ly, "dress", (CX + 2, 270), 48)
    # gold trim: hem with scallops, centre stripe and the star crest
    hem = np.where(ly > 319, dress, 99.0)
    cv.fill(hem, lx, ly, "gold", (CX + 2, 322), 34, line=1.4, shade=False)
    scal = np.where(ly > 319, ring(ellipse(np.mod(lx + 6, 12) - 6, ly, 0, 316, 5, 5), 1.2), 99.0)
    cv.solid(np.where(dress < 0, scal, 99.0), PAL["gold"][3], 0.8)
    cv.solid(np.where(ly < 318, box(lx, ly, CX + 14, 284, 2.2, 26), 99.0), PAL["gold"][0])
    cv.fill(star(lx, ly, CX + 18, 262, 10, 4.5), lx, ly, "gold", (CX + 18, 262), 10, line=1.2, shade=False)
    cv.fill(box(lx, ly, CX + 4, 278, 32, 5.5, 3), lx, ly, "boot", (CX + 4, 278), 30)                       # belt
    cv.fill(box(lx, ly, CX + 20, 278, 7, 8, 2), lx, ly, "gold", (CX + 20, 278), 8, line=1.2)                # buckle
    cv.fill(box(lx, ly, CX - 18, 290, 10, 11, 4), lx, ly, "boot", (CX - 18, 290), 12, line=1.6)             # pouch
    cv.fill(ellipse(lx, ly, CX - 18, 281, 5, 3.5), lx, ly, "gold", (CX - 18, 281), 5, line=1.0, shade=False)
    ruff = union(ellipse(lx, ly, CX + 6, 232, 28, 12), *[ellipse(lx, ly, CX - 16 + i * 11.5, 238, 7.5, 7) for i in range(6)])
    cv.fill(ruff, lx, ly, "white", (CX + 6, 232), 30, line=1.8)
    cv.fill(ellipse(lx, ly, CX + 10, 240, 5.2, 5.2), lx, ly, "pink", (CX + 10, 240), 6, line=1.2)            # ruff brooch
    cv.fill(ellipse(lx, ly, CX + 10, 240, 2.2, 2.2), lx, ly, "gold", (CX + 10, 240), 3, line=0, shade=False)

    # ---------------------------------------------------------------- head and face
    lx, ly = hx(X, Y)
    cv.fill(ellipse(lx, ly, hc[0], hc[1], 82, 74), lx, ly, "skin", hc, 86, line=2.4)
    ex, ey = hc[0] + 38, hc[1] + 10
    if p["blush"] > 0:
        bl = ellipse(lx, ly, ex + 10, ey + 28, 15, 8.5)
        cv.solid(bl, rgb("#ff8fa8"), 0.65 * p["blush"])
        for i in range(3):
            cv.solid(capsule(lx, ly, ex + 3 + i * 7, ey + 32, ex + 6 + i * 7, ey + 26, 0.9, 0.9), rgb("#ff5f86"), 0.6 * p["blush"])
    eye = p["eye"]
    if eye in ("open", "cast"):
        ry = 25 if eye == "open" else 28
        cv.solid(ellipse(lx, ly, ex, ey, 19, ry), rgb("#ffffff"))
        iris = ellipse(lx, ly, ex + 2, ey + 2, 15.5, ry - 2.5)
        cv.fill(iris, lx, ly, (rgb("#7a52f0"), rgb("#4b2fb8"), rgb("#a9c6ff"), rgb("#24146a")), (ex + 2, ey - 4), 24, line=1.0)
        cv.solid(ellipse(lx, ly, ex + 3, ey + 5, 9, ry - 12), rgb("#1c1052"))                    # pupil
        cv.solid(ellipse(lx, ly, ex - 6, ey - 9, 6.4, 7.6), rgb("#ffffff"))                       # big highlight
        cv.solid(ellipse(lx, ly, ex + 9, ey + 11, 3.2, 3.6), rgb("#ffffff"), 0.95)               # small highlight
        cv.solid(ellipse(lx, ly, ex - 8, ey + 12, 2.0, 2.0), rgb("#bfe0ff"), 0.9)                # sparkle
        lid = np.where(ly < ey - 3, ring(ellipse(lx, ly, ex, ey, 19.5, ry + 0.5), 3.6), 99.0)
        cv.solid(lid, INK)                                                                       # bold upper lash
        cv.solid(capsule(lx, ly, ex - 17, ey - ry + 6, ex - 24, ey - ry + 1, 2.6, 0.6), INK)    # lash flick
        cv.solid(capsule(lx, ly, ex + 15, ey - ry + 4, ex + 22, ey - ry + 3, 2.2, 0.6), INK)
    elif eye == "happy":
        arc = np.where(ly < ey + 8, ring(ellipse(lx, ly, ex, ey + 8, 17, 15), 4.0), 99.0)
        cv.solid(arc, INK)
    elif eye in ("blink", "closed"):
        lid = np.where(ly > ey - 4, ring(ellipse(lx, ly, ex, ey - 4, 17, 11), 3.8), 99.0)
        cv.solid(lid, INK)
        cv.solid(capsule(lx, ly, ex - 17, ey - 3, ex - 24, ey - 9, 2.4, 0.6), INK)
    elif eye == "hurt":
        cv.solid(capsule(lx, ly, ex - 15, ey - 14, ex + 13, ey, 3, 3), INK)
        cv.solid(capsule(lx, ly, ex - 15, ey + 14, ex + 13, ey, 3, 3), INK)
        cv.solid(ellipse(lx, ly, ex + 24, ey - 20, 3, 5), rgb("#8fd0ff"))                          # sweat drop
    elif eye == "dead":
        cv.solid(capsule(lx, ly, ex - 13, ey - 13, ex + 13, ey + 13, 3, 3), INK)
        cv.solid(capsule(lx, ly, ex - 13, ey + 13, ex + 13, ey - 13, 3, 3), INK)
    by = ey - (34 if eye in ("open", "cast") else 26)
    tilt = {"hurt": -9, "cast": 7, "dead": -7}.get(eye, 0)
    cv.solid(capsule(lx, ly, ex - 16, by + tilt * 0.5, ex + 15, by - tilt * 0.5, 3.0, 2.2), rgb("#6e4fc0"))
    cv.solid(capsule(lx, ly, hc[0] + 70, hc[1] + 26, hc[0] + 72, hc[1] + 29, 1.6, 1.2), rgb("#d98f78"))   # nose tick
    mx, my = hc[0] + 44, hc[1] + 46
    m = p["mouth"]
    if m == "smile":
        cv.solid(np.where(ly > my - 2, ring(ellipse(lx, ly, mx, my - 3, 9, 7), 2.6), 99.0), rgb("#8a2f52"))
    elif m == "grin":
        cv.solid(np.where(ly > my - 4, ellipse(lx, ly, mx, my - 2, 12, 10), 99.0), rgb("#7a1f40"))
        cv.solid(np.where(ly > my + 3, ellipse(lx, ly, mx, my + 5, 6, 4), 99.0), rgb("#ff8fa8"))
        cv.solid(box(lx, ly, mx, my - 6, 8, 2.2, 1), rgb("#ffffff"))
    elif m == "open":
        cv.solid(ellipse(lx, ly, mx, my, 10, 12), rgb("#7a1f40"))
        cv.solid(ellipse(lx, ly, mx, my + 6, 7, 5), rgb("#ff8fa8"))
    elif m == "o":
        cv.solid(ellipse(lx, ly, mx, my, 6, 8), rgb("#7a1f40"))
    elif m == "sad":
        cv.solid(np.where(ly < my + 5, ring(ellipse(lx, ly, mx, my + 7, 9, 7), 2.6), 99.0), rgb("#8a2f52"))
    else:
        cv.solid(capsule(lx, ly, mx - 6, my, mx + 6, my, 1.8, 1.8), rgb("#8a2f52"))

    # ---------------------------------------------------------------- front hair: pointed bangs, side lock, pink bow
    bangs_pts = [
        [(hc[0] - 6, hc[1] - 56), (hc[0] + 2, hc[1] - 36), (hc[0] + 4, hc[1] - 14)],
        [(hc[0] + 22, hc[1] - 58), (hc[0] + 32, hc[1] - 40), (hc[0] + 38, hc[1] - 26)],
        [(hc[0] + 48, hc[1] - 54), (hc[0] + 60, hc[1] - 42), (hc[0] + 70, hc[1] - 30)],
        [(hc[0] - 50, hc[1] - 50), (hc[0] - 52, hc[1] - 26), (hc[0] - 56, hc[1] - 4)],
    ]
    cap = union(ellipse(lx, ly, hc[0] + 8, hc[1] - 60, 78, 20))
    cv.fill(cap, lx, ly, "hair", (hc[0] + 8, hc[1] - 60), 70, line=1.6)
    for i, pts in enumerate(bangs_pts):
        cv.fill(lock(lx, ly, pts, 15 - i * 1.0, 2.2), lx, ly, "hair", pts[1], 36, line=1.8)
        cv.solid(ring(capsule(lx, ly, pts[0][0] + 3, pts[0][1] + 4, pts[1][0] + 3, pts[1][1], 1, 1), 1.1), PAL["hair"][3], 0.7)
    side = [(hc[0] - 46, hc[1] - 20), (hc[0] - 48, hc[1] + 26), (hc[0] - 56, hc[1] + 64), (hc[0] - 46, hc[1] + 86)]
    cv.fill(lock(lx, ly, side, 15, 3.2), lx, ly, "hair", (hc[0] - 50, hc[1] + 30), 56, line=2.0)
    for i in range(2):
        cv.solid(ring(capsule(lx, ly, hc[0] - 42 + i * 5, hc[1] - 6, hc[0] - 48 + i * 4, hc[1] + 52, 1, 1), 1.2), PAL["hair"][3], 0.75)
    bow = (hc[0] - 42, hc[1] - 36)
    cv.fill(ellipse(lx, ly, bow[0] - 11, bow[1], 11, 8), lx, ly, "pink", bow, 12, line=1.6)
    cv.fill(ellipse(lx, ly, bow[0] + 11, bow[1], 11, 8), lx, ly, "pink", bow, 12, line=1.6)
    cv.fill(ellipse(lx, ly, bow[0], bow[1], 5.5, 5.5), lx, ly, "pink", bow, 6, line=1.4)

    # ---------------------------------------------------------------- hat: tilted brim, bent cone, ribbon, buckle, moon, star
    hat_c = (hc[0] + 2, hc[1] - 64)
    lag = p["hat_lag"] * 0.6
    t1 = root.child(hat_c, p["head"] + lag * 0.5, (p["head_dx"], p["head_dy"]))
    t2 = t1.child((hc[0] - 22, hc[1] - 108), lag * 0.9)
    t3 = t2.child((hc[0] - 54, hc[1] - 132), lag * 1.3)
    l1x, l1y = t1(X, Y)
    cone1 = capsule(l1x, l1y, hat_c[0], hat_c[1], hc[0] - 22, hc[1] - 108, 62, 38)
    cv.fill(cone1, l1x, l1y, "hat", (hc[0] - 10, hc[1] - 92), 62, line=2.2)
    l2x, l2y = t2(X, Y)
    cv.fill(capsule(l2x, l2y, hc[0] - 22, hc[1] - 108, hc[0] - 54, hc[1] - 132, 38, 22), l2x, l2y, "hat", (hc[0] - 40, hc[1] - 120), 40, line=2.2)
    l3x, l3y = t3(X, Y)
    cv.fill(capsule(l3x, l3y, hc[0] - 54, hc[1] - 132, hc[0] - 98, hc[1] - 124, 22, 6), l3x, l3y, "hat", (hc[0] - 76, hc[1] - 128), 28, line=2.2)
    cv.fill(star(l3x, l3y, hc[0] - 102, hc[1] - 124, 11, 5), l3x, l3y, "gold", (hc[0] - 102, hc[1] - 124), 12, line=1.4, shade=False)
    # fabric folds (hand-drawn lines)
    cv.solid(np.where(cone1 < 0, ring(ellipse(l1x, l1y, hc[0] + 6, hc[1] - 84, 40, 36), 1.6) + np.where(l1x < hc[0] - 6, 99.0, 0.0), 99.0), PAL["hat"][3], 0.55)
    cv.solid(np.where(cone1 < 0, ring(ellipse(l1x, l1y, hc[0] - 6, hc[1] - 112, 26, 16), 1.5) + np.where(l1y > hc[1] - 108, 99.0, 0.0), 99.0), PAL["hat"][3], 0.5)
    band = np.where(np.abs(l1y - (hat_c[1] - 12)) < 10, cone1, 99.0)
    cv.fill(band, l1x, l1y, "band", (hat_c[0], hat_c[1] - 12), 44, line=1.8)
    cv.fill(box(l1x, l1y, hat_c[0] + 38, hat_c[1] - 12, 8, 11, 2), l1x, l1y, "gold", (hat_c[0] + 38, hat_c[1] - 12), 12, line=1.6)   # buckle
    cv.solid(box(l1x, l1y, hat_c[0] + 38, hat_c[1] - 12, 3.4, 6, 1), PAL["band"][0])
    moon = np.maximum(ellipse(l1x, l1y, hat_c[0] + 8, hat_c[1] - 52, 14, 14), -ellipse(l1x, l1y, hat_c[0] + 14, hat_c[1] - 55, 12, 12))
    cv.fill(moon, l1x, l1y, "gold", (hat_c[0] + 4, hat_c[1] - 52), 14, line=1.4, shade=False)
    cv.fill(star(l1x, l1y, hat_c[0] + 30, hat_c[1] - 42, 8, 3.6), l1x, l1y, "gold", (hat_c[0] + 30, hat_c[1] - 42), 8, line=1.2, shade=False)
    lx, ly = t1(X, Y)
    brim = ellipse(lx, ly, hat_c[0] + 4, hat_c[1] + 4, 108, 22)
    cv.fill(np.where(ly > hat_c[1] - 2, brim, 99.0), lx, ly, "hat", (hat_c[0], hat_c[1] + 8), 96, line=2.4)
    cv.solid(np.where(brim < -2, ring(ellipse(lx, ly, hat_c[0] + 4, hat_c[1] + 2, 98, 16), 1.5) + np.where(ly < hat_c[1] + 6, 99.0, 0.0), 99.0), PAL["hat"][3], 0.55)

    # ---------------------------------------------------------------- near arm: puffy sleeve, cuff, glove, then the staff
    sn = root.child(sh, p["arm_n"][0])
    en = sn.child((sh[0], sh[1] + 28), p["arm_n"][1])
    hand = (sh[0], sh[1] + 58)
    stf = en.child(hand, p["staff"] + 26.0 - (p["arm_n"][0] + p["arm_n"][1]))
    lx, ly = stf(X, Y)
    top = (hand[0] + 4, hand[1] - 190)
    pole = capsule(lx, ly, hand[0] + 2, hand[1] + 42, top[0], top[1] + 14, 5.6, 5.0)
    cv.fill(pole, lx, ly, "wood", (hand[0], hand[1] - 70), 120, line=1.8)
    for k in range(5):                                           # twisted grain lines
        yy = hand[1] - 20 - k * 28
        cv.solid(ring(capsule(lx, ly, hand[0] - 5, yy, hand[0] + 6, yy - 14, 1, 1), 1.0), PAL["wood"][3], 0.7)
    sx_, sy_ = stf.forward(top)
    cv.glow(sx_, sy_, 24 + 13 * p["glow"], (0.74, 0.42, 1.0), 0.35 + 0.45 * p["glow"])
    cage = smin(ellipse(lx, ly, top[0], top[1], 23, 23), capsule(lx, ly, top[0] - 8, top[1] + 18, top[0] + 8, top[1] + 30, 8, 4), 6)
    cv.fill(cage, lx, ly, "gold", top, 26, line=1.8)
    cv.fill(ellipse(lx, ly, top[0], top[1], 15.5, 15.5), lx, ly, "gem", top, 18, line=1.8)
    cv.solid(ellipse(lx, ly, top[0] - 5, top[1] - 6, 4.8, 5.8), rgb("#ffffff"), 0.95)
    cv.solid(ellipse(lx, ly, top[0] + 6, top[1] + 6, 2.4, 2.4), rgb("#ffffff"), 0.8)
    cres = np.maximum(ellipse(lx, ly, top[0] - 22, top[1] + 2, 9, 15), -ellipse(lx, ly, top[0] - 17, top[1] + 2, 8, 13))
    cv.fill(cres, lx, ly, "gold", (top[0] - 22, top[1]), 14, line=1.4, shade=False)
    cv.fill(ellipse(lx, ly, top[0], top[1] - 26, 4.5, 4.5), lx, ly, "gold", (top[0], top[1] - 26), 5, line=1.2, shade=False)
    cv.fill(ellipse(lx, ly, top[0] + 5, top[1] + 34, 5.5, 5.5), lx, ly, "pink", (top[0] + 5, top[1] + 34), 6, line=1.3)  # ribbon knot

    lx, ly = sn(X, Y)
    cv.fill(smin(capsule(lx, ly, sh[0], sh[1], sh[0], sh[1] + 30, 12, 10), ellipse(lx, ly, sh[0], sh[1] + 8, 15, 13), 6), lx, ly, "white", sh, 36, line=2.0)
    lx, ly = en(X, Y)
    fore = capsule(lx, ly, sh[0], sh[1] + 28, sh[0], sh[1] + 52, 10, 9)
    cv.fill(fore, lx, ly, "dress", (sh[0], sh[1] + 40), 32, line=2.0)
    cv.fill(np.where(np.abs(ly - (sh[1] + 48)) < 4.5, fore, 99.0), lx, ly, "gold", (sh[0], sh[1] + 48), 10, line=1.2, shade=False)
    cv.fill(ellipse(lx, ly, hand[0], hand[1] + 2, 11, 11), lx, ly, "skin", hand, 13, line=2.0)
    lx, ly = stf(X, Y)
    cv.fill(box(lx, ly, hand[0] + 5, hand[1] + 2, 7.5, 11, 4), lx, ly, "skin", hand, 13, line=1.8)
    for k in range(3):
        cv.solid(ring(capsule(lx, ly, hand[0] + 1, hand[1] - 5 + k * 6, hand[0] + 11, hand[1] - 5 + k * 6, 0.6, 0.6), 0.9), PAL["skin"][3], 0.8)
    return cv.finish(3.2)


# ------------------------------------------------------------------ animation
def lerp(a, b, t):
    return a + (b - a) * t


def ease(t):
    return t * t * (3 - 2 * t)


def mix(a, b, t):
    out = {}
    for k in a:
        va, vb = a[k], b[k]
        if isinstance(va, (int, float)):
            out[k] = lerp(va, vb, t)
        elif isinstance(va, tuple):
            out[k] = tuple(lerp(x, y, t) for x, y in zip(va, vb))
        else:
            out[k] = va if t < 0.5 else vb
    return out


def P(**kw):
    d = pose_default()
    d.update(kw)
    return d


def sample(keys, u):
    n = len(keys) - 1
    f = min(max(u, 0), 1) * n
    i = min(int(f), n - 1)
    return mix(keys[i], keys[i + 1], ease(f - i))


def clip_idle(n):
    out = []
    for i in range(n):
        s = math.sin(i / n * 2 * math.pi)
        out.append(P(bob=-2.5 * (0.5 + 0.5 * s), sy=1.0 + 0.012 * s, sx=1.0 - 0.008 * s, head=1.5 * s, hat_lag=-5 * s, braid=4 * s, cape=3 * s,
                     arm_n=(-18 + 2 * s, -35 + 3 * s), arm_f=(12 - 2 * s, 10), staff=1.2 * s, glow=0.35 + 0.15 * (0.5 + 0.5 * s),
                     eye="blink" if i == int(n * 0.72) else "open", mouth="smile"))
    return out


def clip_run(n):
    out = []
    for i in range(n):
        u = i / n
        a, b = math.sin(u * 2 * math.pi), math.cos(u * 2 * math.pi)
        bounce = abs(math.sin(u * 2 * math.pi))
        out.append(P(bob=-7 * bounce, lean=7, head=-2 + 2 * a, head_dy=1.5 * a, hat_lag=-16 - 6 * bounce, braid=-18 + 10 * a, cape=-16 + 8 * a,
                     leg_n=(48 * a, -34 * max(0, -a) - 6), leg_f=(-48 * a, -34 * max(0, a) - 6),
                     arm_n=(-36 - 20 * b, -52), arm_f=(40 * a + 8, 22), staff=-14 + 4 * b, sx=1.0 + 0.02 * bounce, sy=1.0 - 0.02 * bounce,
                     eye="open", mouth="smile", glow=0.45))
    return out


def clip_jump(n):
    keys = [P(sy=0.9, sx=1.08, bob=4, leg_n=(-22, 30), leg_f=(-10, 24), arm_n=(-5, -30), eye="closed", mouth="small", hat_lag=-6),
            P(lift=24, sy=1.06, sx=0.96, leg_n=(20, 48), leg_f=(-14, 40), arm_n=(-60, -70), arm_f=(55, 40), eye="happy", mouth="o", hat_lag=-26, braid=-24, cape=-22, head=-4),
            P(lift=32, leg_n=(14, 40), leg_f=(-10, 36), arm_n=(-70, -50), arm_f=(60, 30), eye="open", mouth="o", hat_lag=-14, braid=-14, cape=-12)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_fall(n):
    keys = [P(lift=30, leg_n=(8, 20), leg_f=(-8, 16), arm_n=(-80, -40), arm_f=(70, 20), eye="open", mouth="o", hat_lag=14, braid=26, cape=24, head=4),
            P(lift=16, leg_n=(14, 30), leg_f=(-12, 26), arm_n=(-88, -30), arm_f=(78, 20), eye="open", mouth="o", hat_lag=20, braid=34, cape=30, head=5)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_climb(n):
    out = []
    for i in range(n):
        a = math.sin(i / n * 2 * math.pi)
        out.append(P(bob=-3 * abs(a), lean=-3, leg_n=(-10 + 30 * a, 40 + 20 * max(0, a)), leg_f=(-10 - 30 * a, 40 + 20 * max(0, -a)),
                     arm_n=(-150 + 34 * a, -20), arm_f=(-150 - 34 * a, -20), staff=70, head=-6, hat_lag=6, braid=8, cape=6, eye="open", mouth="small", glow=0.25, head_dy=-2 * a))
    return out


def clip_hit(n):
    keys = [P(flash=0.9, lean=-12, sy=0.94, sx=1.06, head=-10, hat_lag=22, braid=30, cape=24, arm_n=(8, -10), arm_f=(30, 8), eye="hurt", mouth="open", leg_n=(-6, 10), blush=0),
            P(flash=0.0, lean=-9, sy=0.97, sx=1.03, head=-7, hat_lag=14, braid=20, cape=16, arm_n=(4, -16), arm_f=(24, 8), eye="hurt", mouth="open", blush=0),
            P(lean=-4, head=-3, hat_lag=6, braid=8, cape=6, eye="hurt", mouth="small", blush=0.5)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_death(n):
    keys = [P(flash=0.8, lean=-10, head=-10, hat_lag=18, eye="hurt", mouth="open", arm_n=(10, -10), blush=0, glow=0.3),
            P(lean=-24, fall=-30, fall_dx=-8, head=-12, hat_lag=28, braid=30, eye="dead", mouth="o", arm_n=(30, 10), arm_f=(50, 20), glow=0.2, blush=0),
            P(fall=-72, fall_dx=-22, head=-6, hat_lag=18, eye="dead", mouth="o", arm_n=(40, 20), arm_f=(60, 20), glow=0.1, blush=0),
            P(fall=-84, fall_dx=-30, bob=4, head=-4, hat_lag=8, eye="dead", mouth="sad", arm_n=(48, 24), arm_f=(66, 20), glow=0.05, blush=0, staff=20)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_attack(n):
    keys = [P(lean=-9, sy=0.97, arm_n=(-40, -78), staff=-24, hat_lag=14, braid=16, cape=14, leg_n=(-8, 14), leg_f=(10, 4), eye="open", mouth="small", glow=0.5),
            P(lean=-12, arm_n=(-70, -84), staff=-38, hat_lag=22, braid=24, cape=20, eye="cast", mouth="grin", glow=0.9),
            P(lean=14, sx=1.05, sy=0.96, arm_n=(-8, -4), staff=18, hat_lag=-22, braid=-28, cape=-26, leg_n=(18, 4), leg_f=(-14, 16), eye="cast", mouth="open", glow=1.5, head=4),
            P(lean=8, arm_n=(-14, -14), staff=10, hat_lag=-10, braid=-14, cape=-12, eye="open", mouth="smile", glow=0.9),
            P(lean=0, arm_n=(-18, -35), staff=0, hat_lag=0, eye="open", mouth="smile", glow=0.4)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_skill(n):
    keys = [P(sy=0.93, sx=1.06, bob=3, arm_n=(-20, -50), arm_f=(10, 10), staff=-6, leg_n=(-14, 28), leg_f=(8, 20), eye="closed", mouth="small", glow=0.6),
            P(lift=10, sy=1.05, arm_n=(-100, -70), arm_f=(80, 20), staff=-4, hat_lag=-12, braid=-14, cape=-12, eye="cast", mouth="grin", glow=1.4, head=-4),
            P(lift=16, sy=1.06, arm_n=(-130, -50), arm_f=(95, 24), staff=-2, hat_lag=-18, braid=-20, cape=-18, eye="cast", mouth="open", glow=2.0, head=-6),
            P(lift=6, lean=10, sx=1.05, sy=0.96, arm_n=(-30, -10), arm_f=(30, 10), staff=22, hat_lag=-24, braid=-30, cape=-28, eye="cast", mouth="open", glow=2.4, leg_n=(20, 6), leg_f=(-16, 18)),
            P(lean=3, arm_n=(-20, -30), staff=6, hat_lag=-6, eye="happy", mouth="grin", glow=0.9),
            P(arm_n=(-18, -35), eye="open", mouth="smile", glow=0.4)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


CLIPS = [
    ("Idle", clip_idle, 8, 8, True),
    ("Run", clip_run, 8, 14, True),
    ("Jump", clip_jump, 4, 10, False),
    ("Fall", clip_fall, 3, 8, False),
    ("Climb", clip_climb, 8, 10, True),
    ("Hit", clip_hit, 4, 12, False),
    ("Death", clip_death, 6, 8, False),
    ("Attack", clip_attack, 8, 14, False),
    ("Skill", clip_skill, 10, 12, False),
]


def write_png(path, arr):
    a = np.clip(arr, 0, 1)
    rgb8 = (a[..., :3] * 255 + 0.5).astype(np.uint8)
    a8 = (a[..., 3] * 255 + 0.5).astype(np.uint8)
    h, w = a8.shape
    raw = np.concatenate([rgb8, a8[..., None]], axis=2).reshape(h, w * 4)
    data = b"".join(b"\x00" + raw[y].tobytes() for y in range(h))

    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(data, 6)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def preview(out_dir, frames):
    """Review sheet: stand / run / jump / attack at 2x on a soft neutral background."""
    picks = [("Đứng", "Idle", 0), ("Chạy", "Run", 1), ("Chạy", "Run", 5), ("Nhảy", "Jump", 1), ("Đánh", "Attack", 2), ("Skill", "Skill", 3)]
    cells = []
    for _, clip, i in picks:
        f = frames[clip][i]
        n_ = f.shape[0]
        bg = np.zeros((n_, n_, 3), np.float32) + np.array([0.86, 0.9, 0.84], np.float32)
        bg[int((G + 2) * n_ / S):, :, :] = np.array([0.55, 0.7, 0.4], np.float32)
        a = f[..., 3:4]
        cells.append(np.concatenate([f[..., :3] * a + bg * (1 - a), np.ones((n_, n_, 1), np.float32)], axis=2))
    sheet = np.concatenate(cells, axis=1)
    big = np.repeat(np.repeat(sheet, 6 if PIXEL else 2, 0), 6 if PIXEL else 2, 1)       # pixel art: nearest-neighbour zoom
    write_png(os.path.join(out_dir, "Mage_preview.png"), big)


def _task(job):
    name, i = job
    for cname, fn, n, fps, loop in CLIPS:
        if cname == name:
            return render(fn(n)[i])


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    out = args[0] if args else os.path.join(os.path.dirname(__file__), "..", "..", "ArtSource", "Chibi2D", "Mage")
    only = set(args[1].split(",")) if len(args) > 1 and args[1] else None
    os.makedirs(out, exist_ok=True)
    meta_path = os.path.join(out, "Mage_frames.json")
    meta = {"cell": PX if PIXEL else S, "pixel": PIXEL, "frame_m": FRAME_M, "ground_from_bottom_m": round(GROUND_M, 4), "clips": {}}
    if os.path.exists(meta_path):
        with open(meta_path, encoding="utf-8") as f:
            meta["clips"] = json.load(f).get("clips", {})
    frames = {}
    import multiprocessing as mp
    todo = [(name, i) for name, fn, n, fps, loop in CLIPS if not only or name in only for i in range(n)]
    with mp.Pool(min(4, mp.cpu_count())) as pool:
        done = pool.map(_task, todo, chunksize=1)
    for (name, i), arr in zip(todo, done):
        frames.setdefault(name, []).append(arr)
    for name, fn, n, fps, loop in CLIPS:
        if name not in frames:
            continue
        write_png(os.path.join(out, f"Mage_{name}.png"), np.concatenate(frames[name], axis=1))
        meta["clips"][name] = {"frames": n, "fps": fps, "loop": loop, "file": f"Mage_{name}.png"}
        print("BAKED", name, n, flush=True)
    with open(meta_path, "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2)
    if "--preview" in sys.argv and all(k in frames for k in ("Idle", "Run", "Jump", "Attack", "Skill")):
        preview(out, frames)


if __name__ == "__main__":
    main()
