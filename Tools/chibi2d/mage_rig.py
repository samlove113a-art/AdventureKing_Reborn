"""AdventureKing original 2D chibi Mage: procedural, layered, side view (faces right).

Run with the Python that ships with Blender (numpy only):
  "<Blender>/5.2/python/bin/python.exe" Tools/chibi2d/mage_rig.py [out_dir] [only_clip,...]

Every body part is an analytic shape (signed distance field) in rest-pose coordinates, drawn back to front with cel shading
(base / shadow / light), a thin inner contour and one clean dark silhouette outline. Poses move the parts as a skeleton, so
animation is smooth at any frame count and outfits / weapons are separate layers (swap a layer, not the whole character).
Proportions: head ~ 45 % of the height, small body, big expressive eye, clear silhouette (original design, no outside art).
Output: one PNG strip per clip + Mage_frames.json (same contract as the Unity importer Chibi2DArt).
"""
import json
import math
import os
import struct
import sys
import zlib

import numpy as np

S = 384                  # frame size (px)
CX, G = 150.0, 364.0     # root x (feet centre) and ground line
BASE = 0.88              # whole-character scale inside the frame (room for the hat tip and the staff orb)
FRAME_M = 1.185          # metres covered by one frame side (the 340 px tall mage = 1.05 m like the 3D body)
GROUND_M = (S - G) / (S / FRAME_M)

Y, X = np.mgrid[0:S, 0:S].astype(np.float32)
X += 0.5
Y += 0.5

INK = np.array([0.17, 0.10, 0.24], np.float32)


def rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], np.float32)


PAL = {
    "skin": (rgb("#ffdcc6"), rgb("#f3b79f"), rgb("#fff1e6")),
    "hair": (rgb("#c7aeff"), rgb("#9c82e0"), rgb("#eadfff")),
    "hat": (rgb("#5d40b8"), rgb("#3e2a88"), rgb("#8c74e8")),
    "band": (rgb("#2e2c78"), rgb("#1f1d58"), rgb("#4a48a8")),
    "gold": (rgb("#ffd45c"), rgb("#d99a2b"), rgb("#fff1a8")),
    "dress": (rgb("#3b58bc"), rgb("#2a3e8c"), rgb("#6c8ae6")),
    "white": (rgb("#f6f6ff"), rgb("#c8cbe8"), rgb("#ffffff")),
    "wood": (rgb("#a06c3a"), rgb("#6d4626"), rgb("#cf955a")),
    "boot": (rgb("#80502c"), rgb("#55321b"), rgb("#ad6e3e")),
    "gem": (rgb("#b24dff"), rgb("#7c2cd4"), rgb("#ecc0ff")),
    "sockb": (rgb("#4b63c4"), rgb("#33459a"), rgb("#7d93e8")),
}


# ------------------------------------------------------------------ shapes (signed distance, px; < 0 inside)
def ellipse(x, y, cx, cy, rx, ry):
    return (np.sqrt(((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2) - 1.0) * min(rx, ry)


def capsule(x, y, ax, ay, bx, by, ra, rb):
    """Tapered capsule from a (radius ra) to b (radius rb)."""
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
    k = (np.cos(ang * 5 / 2 * 2) + 1) / 2          # 5 lobes
    edge = ri + (ro - ri) * k ** 1.6
    return (r - edge) * 0.9


def union(*ds):
    out = ds[0]
    for d in ds[1:]:
        out = np.minimum(out, d)
    return out


def smin(a, b, k=6.0):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)


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


# ------------------------------------------------------------------ painter
class Canvas:
    def __init__(self):
        self.rgb = np.zeros((S, S, 3), np.float32)
        self.a = np.zeros((S, S), np.float32)
        self.flash = 0.0
        self.glows = []

    def over(self, color, cov):
        cov = np.clip(cov, 0, 1)
        new_a = cov + self.a * (1 - cov)
        w = cov[..., None]
        self.rgb = color * w + self.rgb * (1 - w)
        self.a = new_a

    def fill(self, sd, lx, ly, pal, center, r, edge=0.62, tint=None):
        """Cel-shaded fill: base, a shadow crescent (lower right) and a light rim (upper left), thin inner contour."""
        cov = np.clip(0.5 - sd, 0, 1)
        if cov.max() <= 0:
            return
        base, shade, hi = PAL[pal] if isinstance(pal, str) else pal
        t = ((lx - center[0]) * 0.55 + (ly - center[1]) * 0.85) / max(r, 1.0)
        sh = np.clip((t - 0.30) * 14, 0, 1)
        li = np.clip((-0.62 - t) * 14, 0, 1) * (1 - sh)
        col = base[None, None, :] * (1 - sh[..., None]) + shade[None, None, :] * sh[..., None]
        col = col * (1 - li[..., None]) + hi[None, None, :] * li[..., None]
        if tint is not None:
            col = col * (1 - tint[1]) + tint[0][None, None, :] * tint[1]
        ring = np.clip((sd + 2.6) / 1.9, 0, 1)                       # 0 deep inside .. 1 at the edge
        col = col * (1 - ring[..., None] * (1 - edge))
        self.over(col, cov)

    def solid(self, sd, color, alpha=1.0):
        cov = np.clip(0.5 - sd, 0, 1) * alpha
        self.over(np.asarray(color, np.float32)[None, None, :], cov)

    def glow(self, cx, cy, r, color, k=1.0):
        d = np.hypot(X - cx, Y - cy)
        self.glows.append((np.clip(np.exp(-(d / r) ** 2 * 2.2) * 0.5 * k, 0, 0.8), np.asarray(color, np.float32)))

    def finish(self, ring=3):
        a = self.a
        grown = (a > 0.2).astype(np.float32)
        pad = ring + 1
        big = np.pad(grown, pad)
        acc = np.zeros_like(big)
        for dy in range(-ring, ring + 1):
            for dx in range(-ring, ring + 1):
                if dx * dx + dy * dy <= ring * ring + 1:
                    acc = np.maximum(acc, np.roll(np.roll(big, dy, 0), dx, 1))
        outline = acc[pad:-pad, pad:-pad]
        # soft edge on the outline from a blurred copy
        k = np.array([1, 2, 1], np.float32) / 4
        o = outline
        o = (np.roll(o, 1, 0) * k[0] + o * k[1] + np.roll(o, -1, 0) * k[2])
        o = (np.roll(o, 1, 1) * k[0] + o * k[1] + np.roll(o, -1, 1) * k[2])
        out = np.zeros((S, S, 4), np.float32)
        body = a[..., None]
        base = INK[None, None, :] * (1 - body) + self.rgb * body
        if self.flash > 0:
            base = base * (1 - self.flash * body) + np.array([1, 1, 1], np.float32)[None, None, :] * self.flash * body
        alpha = np.maximum(o, a)
        for ga, gc in self.glows:                       # soft magic glow, added after the outline so it is never inked
            w = ga[..., None]
            base = base * (1 - w) + gc[None, None, :] * w
            alpha = np.maximum(alpha, ga)
        out[..., :3] = base
        out[..., 3] = alpha
        return out


# ------------------------------------------------------------------ the character
EYES = ("open", "blink", "closed", "hurt", "dead", "happy", "cast")
MOUTHS = ("smile", "open", "small", "sad", "o", "grin")


def pose_default():
    return dict(
        bob=0.0, lean=0.0, sx=1.0, sy=1.0, fall=0.0, fall_dx=0.0,
        head=0.0, head_dx=0.0, head_dy=0.0, hat_lag=0.0, braid=0.0,
        arm_n=(-18.0, -35.0), arm_f=(12.0, 10.0), leg_n=(0.0, 0.0), leg_f=(0.0, 0.0),
        staff=0.0, eye="open", mouth="smile", flash=0.0, glow=0.35, lift=0.0, blush=1.0, staff_hand=True,
    )


def render(p):
    cv = Canvas()
    cv.flash = p["flash"]
    # root: squash / lean / fall about the feet, then bob
    root = Xf([((CX, G), p["lean"] + p["fall"], (p["fall_dx"], p["bob"] - p["lift"]), (p["sx"] * BASE, p["sy"] * BASE))])

    hip = (CX + 2, 296)
    sh_pivot = (CX + 6, 236)
    head_c = (CX + 12, 150)

    # --- back hair + braid
    hx = root.child(head_c, p["head"], (p["head_dx"], p["head_dy"]))
    lx, ly = hx(X, Y)
    back = union(ellipse(lx, ly, head_c[0] - 30, head_c[1] + 12, 62, 70), ellipse(lx, ly, head_c[0] - 56, head_c[1] + 52, 30, 34))
    cv.fill(back, lx, ly, "hair", (head_c[0] - 30, head_c[1]), 78)
    prev = hx
    px, py = head_c[0] - 62, head_c[1] + 44
    for i in range(5):                                   # braid: a chain of leaves, each swings a little more
        prev = prev.child((px, py), p["braid"] * (0.5 + i * 0.35))
        lx, ly = prev(X, Y)
        r = 17 - i * 2.2
        cv.fill(ellipse(lx, ly, px, py + 9, r * 0.8, r + 7), lx, ly, "hair", (px, py + 9), r + 8)
        py += 15

    # --- far arm (behind the body, darker)
    arm_dark = (rgb("#242a52"), 0.22)
    sf = root.child(sh_pivot, p["arm_f"][0], (-6, 2))
    ef = sf.child((sh_pivot[0], sh_pivot[1] + 30), p["arm_f"][1])
    lx, ly = sf(X, Y)
    cv.fill(capsule(lx, ly, sh_pivot[0], sh_pivot[1], sh_pivot[0], sh_pivot[1] + 32, 9.5, 8.5), lx, ly, "white", sh_pivot, 34, tint=arm_dark)
    lx, ly = ef(X, Y)
    cv.fill(capsule(lx, ly, sh_pivot[0], sh_pivot[1] + 30, sh_pivot[0], sh_pivot[1] + 58, 8.5, 7.5), lx, ly, "dress", (sh_pivot[0], sh_pivot[1] + 44), 30, tint=arm_dark)
    cv.fill(ellipse(lx, ly, sh_pivot[0], sh_pivot[1] + 64, 9, 9), lx, ly, "skin", (sh_pivot[0], sh_pivot[1] + 64), 10, tint=arm_dark)

    # --- legs (far then near): thigh + shin + striped sock + boot
    def leg(angles, dx, tintc):
        hp = root.child((hip[0] + dx, hip[1]), angles[0])
        kn = hp.child((hip[0] + dx, hip[1] + 30), angles[1])
        lx, ly = hp(X, Y)
        cv.fill(capsule(lx, ly, hip[0] + dx, hip[1], hip[0] + dx, hip[1] + 32, 11, 10), lx, ly, "white", (hip[0] + dx, hip[1] + 14), 20, tint=tintc)
        lx, ly = kn(X, Y)
        fx = hip[0] + dx
        shin = capsule(lx, ly, fx, hip[1] + 30, fx, hip[1] + 58, 10, 9)
        cv.fill(shin, lx, ly, "white", (fx, hip[1] + 44), 20, tint=tintc)
        stripe = (np.floor((ly - (hip[1] + 30)) / 7.0) % 2) == 1
        sock = np.where(stripe, shin, 99.0)
        cv.fill(sock, lx, ly, "sockb", (fx, hip[1] + 44), 20, tint=tintc)
        foot = union(ellipse(lx, ly, fx + 8, hip[1] + 62, 22, 12.5), ellipse(lx, ly, fx - 4, hip[1] + 58, 12, 11))
        cv.fill(foot, lx, ly, "boot", (fx + 6, hip[1] + 62), 24, tint=tintc)
        cv.fill(ellipse(lx, ly, fx + 20, hip[1] + 59, 9, 6), lx, ly, "boot", (fx + 20, hip[1] + 59), 8, tint=tintc)

    dim = (rgb("#242a52"), 0.18)
    leg(p["leg_f"], -9, dim)
    leg(p["leg_n"], 10, None)

    # --- dress + collar + belt
    bx = root.child(sh_pivot, p["lean"] * 0.0)
    lx, ly = bx(X, Y)
    dress = capsule(lx, ly, CX + 4, 244, CX + 2, 290, 30, 44)
    dress = smin(dress, ellipse(lx, ly, CX + 4, 252, 34, 30), 8)
    cv.fill(dress, lx, ly, "dress", (CX + 2, 262), 48)
    hem = np.where(np.abs(ly - 296) < 4.0, dress, 99.0)
    cv.fill(hem, lx, ly, "gold", (CX, 296), 30, edge=0.8)
    cv.fill(box(lx, ly, CX + 3, 266, 31, 5, 3), lx, ly, "boot", (CX, 266), 30)          # belt
    cv.fill(box(lx, ly, CX + 16, 266, 6, 7, 2), lx, ly, "gold", (CX + 16, 266), 8)       # buckle
    cv.fill(ellipse(lx, ly, CX + 6, 228, 24, 11), lx, ly, "white", (CX + 6, 228), 26)    # collar
    # --- head
    lx, ly = hx(X, Y)
    face = ellipse(lx, ly, head_c[0], head_c[1], 74, 66)
    cv.fill(face, lx, ly, "skin", head_c, 80)
    # blush + eye + brow + nose + mouth (right side of the face: the character looks to the right)
    ex, ey = head_c[0] + 34, head_c[1] + 6
    if p["blush"] > 0:
        cv.solid(ellipse(lx, ly, ex + 6, ey + 26, 15, 8), rgb("#ff8fa3"), 0.55 * p["blush"])
    eye = p["eye"]
    if eye in ("open", "cast", "happy"):
        ry = 21 if eye == "open" else 24 if eye == "cast" else 12
        if eye == "happy":                                   # ^ ^ happy arc
            arc = np.abs(ellipse(lx, ly, ex, ey + 8, 16, 14)) - 2.6
            arc = np.where(ly < ey + 8, arc, 99.0)
            cv.solid(arc, INK)
        else:
            cv.solid(ellipse(lx, ly, ex, ey, 17, ry), rgb("#ffffff"))
            iris = ellipse(lx, ly, ex + 2, ey + 1, 13.5, ry - 2.5)
            cv.fill(iris, lx, ly, (rgb("#6a4be0"), rgb("#4a31b0"), rgb("#9fc0ff")), (ex + 2, ey - 6), 22, edge=0.9)
            cv.solid(ellipse(lx, ly, ex + 3, ey + 3, 7.5, ry - 9), rgb("#1e1450"))
            cv.solid(ellipse(lx, ly, ex - 5, ey - 7, 5.5, 6.5), rgb("#ffffff"))                    # big sparkle
            cv.solid(ellipse(lx, ly, ex + 8, ey + 9, 3.0, 3.4), rgb("#ffffff"), 0.95)              # small sparkle
            lid = np.abs(ellipse(lx, ly, ex, ey, 17.5, ry + 0.5)) - 2.4
            lid = np.where(ly < ey - 2, lid, 99.0)
            cv.solid(lid, INK)                                                                       # thick upper lash
            cv.solid(box(lx, ly, ex - 15, ey - ry + 2, 5, 1.6, 1.0), INK)                           # lash flick
    elif eye == "blink" or eye == "closed":
        lid = np.abs(ellipse(lx, ly, ex, ey - 6, 16, 12)) - 2.4
        lid = np.where(ly > ey - 6, lid, 99.0)
        cv.solid(lid, INK)
    elif eye == "hurt":                                       # > <  squeezed
        cv.solid(capsule(lx, ly, ex - 14, ey - 12, ex + 12, ey, 2.6, 2.6), INK)
        cv.solid(capsule(lx, ly, ex - 14, ey + 12, ex + 12, ey, 2.6, 2.6), INK)
    elif eye == "dead":                                       # X
        cv.solid(capsule(lx, ly, ex - 12, ey - 12, ex + 12, ey + 12, 2.6, 2.6), INK)
        cv.solid(capsule(lx, ly, ex - 12, ey + 12, ex + 12, ey - 12, 2.6, 2.6), INK)
    brow_y = ey - (30 if eye in ("open", "cast") else 24)
    tilt = {"hurt": -8, "cast": 6, "dead": -6}.get(eye, 0)
    cv.solid(capsule(lx, ly, ex - 14, brow_y + tilt * 0.5, ex + 14, brow_y - tilt * 0.5, 2.4, 2.0), rgb("#7d63c4"))
    cv.solid(ellipse(lx, ly, head_c[0] + 62, head_c[1] + 26, 3.0, 2.2), rgb("#e8a58e"))        # tiny nose
    mx, my = head_c[0] + 40, head_c[1] + 40
    m = p["mouth"]
    if m == "smile":
        cv.solid(np.where(ly > my - 2, np.abs(ellipse(lx, ly, mx, my - 2, 8, 6)) - 1.8, 99.0), rgb("#9a3d5a"))
    elif m == "grin":
        cv.solid(np.where(ly > my - 3, ellipse(lx, ly, mx, my - 1, 10, 8), 99.0), rgb("#7a2a48"))
        cv.solid(np.where(ly > my + 2, ellipse(lx, ly, mx, my + 4, 5, 3), 99.0), rgb("#ff8fa3"))
    elif m == "open":
        cv.solid(ellipse(lx, ly, mx, my, 9, 10), rgb("#7a2a48"))
        cv.solid(ellipse(lx, ly, mx, my + 5, 6, 4), rgb("#ff8fa3"))
    elif m == "o":
        cv.solid(ellipse(lx, ly, mx, my, 5.5, 7), rgb("#7a2a48"))
    elif m == "sad":
        cv.solid(np.where(ly < my + 4, np.abs(ellipse(lx, ly, mx, my + 6, 8, 6)) - 1.8, 99.0), rgb("#9a3d5a"))
    else:
        cv.solid(capsule(lx, ly, mx - 5, my, mx + 5, my, 1.8, 1.8), rgb("#9a3d5a"))

    # --- front hair: bangs under the hat + a side lock in front of the ear
    bangs = union(ellipse(lx, ly, head_c[0] + 8, head_c[1] - 52, 70, 24), ellipse(lx, ly, head_c[0] + 52, head_c[1] - 34, 26, 22),
                  ellipse(lx, ly, head_c[0] - 36, head_c[1] - 28, 40, 40))
    cv.fill(bangs, lx, ly, "hair", (head_c[0] + 8, head_c[1] - 52), 58)
    lock = capsule(lx, ly, head_c[0] - 14, head_c[1] - 10, head_c[0] - 18, head_c[1] + 52, 12, 7)
    cv.fill(lock, lx, ly, "hair", (head_c[0] - 16, head_c[1] + 20), 40)

    # --- hat: bent cone (follows the head, tip lags), brim, band, star
    hat_x = root.child(head_c, p["head"], (p["head_dx"], p["head_dy"]))
    lx, ly = hat_x(X, Y)
    lag = p["hat_lag"] * 0.6
    brim_c = (head_c[0] + 2, head_c[1] - 62)
    tip1 = root.child((head_c[0], head_c[1] - 62), p["head"] + lag * 0.5, (p["head_dx"], p["head_dy"]))
    tip2 = tip1.child((head_c[0] - 22, head_c[1] - 104), lag * 0.9)
    tip3 = tip2.child((head_c[0] - 52, head_c[1] - 128), lag * 1.3)
    l1x, l1y = tip1(X, Y)
    cone1 = capsule(l1x, l1y, brim_c[0], brim_c[1], head_c[0] - 22, head_c[1] - 104, 60, 38)
    cv.fill(cone1, l1x, l1y, "hat", (head_c[0] - 10, head_c[1] - 90), 60)
    l2x, l2y = tip2(X, Y)
    cone2 = capsule(l2x, l2y, head_c[0] - 22, head_c[1] - 104, head_c[0] - 52, head_c[1] - 128, 38, 22)
    cv.fill(cone2, l2x, l2y, "hat", (head_c[0] - 40, head_c[1] - 118), 40)
    l3x, l3y = tip3(X, Y)
    cone3 = capsule(l3x, l3y, head_c[0] - 52, head_c[1] - 128, head_c[0] - 96, head_c[1] - 124, 22, 6)
    cv.fill(cone3, l3x, l3y, "hat", (head_c[0] - 76, head_c[1] - 126), 28)
    cv.fill(ellipse(l3x, l3y, head_c[0] - 98, head_c[1] - 124, 7, 7), l3x, l3y, "gold", (head_c[0] - 98, head_c[1] - 124), 8)   # tip charm
    band = np.where(np.abs(l1y - (brim_c[1] - 12)) < 8.5, cone1, 99.0)
    cv.fill(band, l1x, l1y, "band", (brim_c[0], brim_c[1] - 12), 40, edge=0.8)
    brim = ellipse(lx, ly, brim_c[0] + 4, brim_c[1] + 2, 104, 21)
    cv.fill(np.where(ly > brim_c[1] - 3, brim, 99.0), lx, ly, "hat", (brim_c[0], brim_c[1] + 6), 90)    # front half of the brim over the bangs
    cv.fill(star(l1x, l1y, brim_c[0] + 34, brim_c[1] - 14, 15, 7), l1x, l1y, "gold", (brim_c[0] + 34, brim_c[1] - 14), 16, edge=0.8)

    # --- near arm + staff (in front of everything)
    sn = root.child(sh_pivot, p["arm_n"][0])
    en = sn.child((sh_pivot[0], sh_pivot[1] + 30), p["arm_n"][1])
    hand = (sh_pivot[0], sh_pivot[1] + 62)
    stf = en.child(hand, p["staff"] + 26.0 - (p["arm_n"][0] + p["arm_n"][1]))   # staff angle is relative to upright, not to the arm
    lx, ly = stf(X, Y)
    # staff: pole behind the fist, orb on top
    top = (hand[0] + 4, hand[1] - 196)
    pole = capsule(lx, ly, hand[0] + 2, hand[1] + 44, top[0], top[1] + 14, 5.5, 5.0)
    cv.fill(pole, lx, ly, "wood", (hand[0], hand[1] - 70), 120)
    wrap = np.where(np.abs(ly - (hand[1] - 10)) < 12, pole, 99.0)
    cv.fill(wrap, lx, ly, "boot", (hand[0], hand[1] - 10), 14, edge=0.8)
    glow_pt = stf.child(hand, 0)
    # orb halo (additive-looking) then the cage + gem
    gx, gy = top
    ang = math.radians(p["staff"] - 8.0 + p["arm_n"][0] + p["arm_n"][1] + p["lean"] + p["fall"])
    ox = hand[0] + (gx - hand[0]) * math.cos(ang) - (gy - hand[1]) * math.sin(ang)
    oy = hand[1] + (gx - hand[0]) * math.sin(ang) + (gy - hand[1]) * math.cos(ang)
    # arm angles are applied about the shoulder, so approximate the orb centre through the real transform:
    px_, py_ = _forward(stf, top)
    cv.glow(px_, py_, 24 + 13 * p["glow"], (0.74, 0.42, 1.0), 0.35 + 0.45 * p["glow"])   # tight halo: reads as magic, never a smear
    cage = union(ellipse(lx, ly, top[0], top[1], 24, 24))
    cv.fill(cage, lx, ly, "gold", top, 26, edge=0.7)
    cv.fill(ellipse(lx, ly, top[0], top[1], 16.5, 16.5), lx, ly, "gem", top, 18, edge=0.7)
    cv.solid(ellipse(lx, ly, top[0] - 5, top[1] - 6, 4.8, 5.8), rgb("#ffffff"), 0.9)
    cv.solid(ellipse(lx, ly, top[0] + 6, top[1] + 6, 2.6, 2.6), rgb("#ffffff"), 0.7)
    cv.fill(star(lx, ly, top[0] - 24, top[1] + 10, 7, 3), lx, ly, "gold", (top[0] - 24, top[1] + 10), 8, edge=0.8)

    lx, ly = sn(X, Y)
    cv.fill(capsule(lx, ly, sh_pivot[0], sh_pivot[1], sh_pivot[0], sh_pivot[1] + 32, 10.5, 9.5), lx, ly, "white", sh_pivot, 36)
    lx, ly = en(X, Y)
    cv.fill(capsule(lx, ly, sh_pivot[0], sh_pivot[1] + 30, sh_pivot[0], sh_pivot[1] + 58, 9.5, 8.5), lx, ly, "dress", (sh_pivot[0], sh_pivot[1] + 44), 32)
    cuff = np.where(np.abs(ly - (sh_pivot[1] + 54)) < 4, capsule(lx, ly, sh_pivot[0], sh_pivot[1] + 30, sh_pivot[0], sh_pivot[1] + 58, 9.5, 8.5), 99.0)
    cv.fill(cuff, lx, ly, "gold", (sh_pivot[0], sh_pivot[1] + 54), 10, edge=0.8)
    cv.fill(ellipse(lx, ly, hand[0], hand[1] + 2, 10.5, 10.5), lx, ly, "skin", (hand[0], hand[1]), 12)
    lx, ly = stf(X, Y)
    cv.fill(box(lx, ly, hand[0] + 4, hand[1] + 2, 7, 10, 4), lx, ly, "skin", (hand[0], hand[1]), 12)   # fingers wrapped round the pole
    return cv.finish(3)


def _forward(chain, pt):
    """Rest point -> screen point through a chain (numeric forward transform)."""
    x, y = pt
    for (ox, oy), ang, (tx, ty), (sx, sy) in reversed(chain.links):
        dx, dy = (x - ox) * sx, (y - oy) * sy
        c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
        x = ox + tx + c * dx - s * dy
        y = oy + ty + s * dx + c * dy
    return x, y


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
    """keys: list of poses spread evenly over u in [0,1]."""
    n = len(keys) - 1
    f = min(max(u, 0), 1) * n
    i = min(int(f), n - 1)
    return mix(keys[i], keys[i + 1], ease(f - i))


def clip_idle(n):
    out = []
    for i in range(n):
        u = i / n
        s = math.sin(u * 2 * math.pi)
        out.append(P(bob=-2.5 * (0.5 + 0.5 * s), sy=1.0 + 0.012 * s, sx=1.0 - 0.008 * s, head=1.5 * s, hat_lag=-5 * s, braid=4 * s,
                     arm_n=(-18 + 2 * s, -35 + 3 * s), arm_f=(12 - 2 * s, 10), staff=1.2 * s, glow=0.35 + 0.15 * (0.5 + 0.5 * s),
                     eye="blink" if i == int(n * 0.72) else "open", mouth="smile"))
    return out


def clip_run(n):
    out = []
    for i in range(n):
        u = i / n
        a = math.sin(u * 2 * math.pi)
        b = math.cos(u * 2 * math.pi)
        bounce = abs(math.sin(u * 2 * math.pi))
        out.append(P(bob=-7 * bounce, lean=7, head=-2 + 2 * a, head_dy=1.5 * a, hat_lag=-16 - 6 * bounce, braid=-18 + 10 * a,
                     leg_n=(48 * a, -34 * max(0, -a) - 6), leg_f=(-48 * a, -34 * max(0, a) - 6),
                     arm_n=(-36 - 20 * b, -52), arm_f=(40 * a + 8, 22), staff=-14 + 4 * b, sx=1.0 + 0.02 * bounce, sy=1.0 - 0.02 * bounce,
                     eye="open", mouth="smile", glow=0.45))
    return out


def clip_jump(n):
    keys = [P(sy=0.9, sx=1.08, bob=4, leg_n=(-22, 30), leg_f=(-10, 24), arm_n=(-5, -30), eye="closed", mouth="small", hat_lag=-6),
            P(lift=24, sy=1.06, sx=0.96, leg_n=(20, 48), leg_f=(-14, 40), arm_n=(-60, -70), arm_f=(55, 40), eye="happy", mouth="o", hat_lag=-26, braid=-24, head=-4),
            P(lift=32, leg_n=(14, 40), leg_f=(-10, 36), arm_n=(-70, -50), arm_f=(60, 30), eye="open", mouth="o", hat_lag=-14, braid=-14)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_fall(n):
    keys = [P(lift=30, leg_n=(8, 20), leg_f=(-8, 16), arm_n=(-80, -40), arm_f=(70, 20), eye="open", mouth="o", hat_lag=14, braid=26, head=4),
            P(lift=16, leg_n=(14, 30), leg_f=(-12, 26), arm_n=(-88, -30), arm_f=(78, 20), eye="open", mouth="o", hat_lag=20, braid=34, head=5)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_climb(n):
    out = []
    for i in range(n):
        u = i / n
        a = math.sin(u * 2 * math.pi)
        out.append(P(bob=-3 * abs(a), lean=-3, leg_n=(-10 + 30 * a, 40 + 20 * max(0, a)), leg_f=(-10 - 30 * a, 40 + 20 * max(0, -a)),
                     arm_n=(-150 + 34 * a, -20), arm_f=(-150 - 34 * a, -20), staff=70, head=-6, hat_lag=6, braid=8, eye="open", mouth="small", glow=0.25,
                     head_dy=-2 * a))
    return out


def clip_hit(n):
    keys = [P(flash=0.9, lean=-12, sy=0.94, sx=1.06, head=-10, hat_lag=22, braid=30, arm_n=(8, -10), arm_f=(30, 8), eye="hurt", mouth="open", leg_n=(-6, 10), blush=0),
            P(flash=0.0, lean=-9, sy=0.97, sx=1.03, head=-7, hat_lag=14, braid=20, arm_n=(4, -16), arm_f=(24, 8), eye="hurt", mouth="open", blush=0),
            P(lean=-4, head=-3, hat_lag=6, braid=8, eye="hurt", mouth="small", blush=0.5)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_death(n):
    keys = [P(flash=0.8, lean=-10, head=-10, hat_lag=18, eye="hurt", mouth="open", arm_n=(10, -10), blush=0, glow=0.3),
            P(lean=-24, fall=-30, fall_dx=-8, head=-12, hat_lag=28, braid=30, eye="dead", mouth="o", arm_n=(30, 10), arm_f=(50, 20), glow=0.2, blush=0),
            P(fall=-72, fall_dx=-22, head=-6, hat_lag=18, eye="dead", mouth="o", arm_n=(40, 20), arm_f=(60, 20), glow=0.1, blush=0),
            P(fall=-84, fall_dx=-30, bob=4, head=-4, hat_lag=8, eye="dead", mouth="sad", arm_n=(48, 24), arm_f=(66, 20), glow=0.05, blush=0, staff=20)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_attack(n):
    keys = [P(lean=-9, sy=0.97, arm_n=(-40, -78), staff=-24, hat_lag=14, braid=16, leg_n=(-8, 14), leg_f=(10, 4), eye="open", mouth="small", glow=0.5),     # wind-up
            P(lean=-12, arm_n=(-70, -84), staff=-38, hat_lag=22, braid=24, eye="cast", mouth="grin", glow=0.9),
            P(lean=14, sx=1.05, sy=0.96, arm_n=(-8, -4), staff=18, hat_lag=-22, braid=-28, leg_n=(18, 4), leg_f=(-14, 16), eye="cast", mouth="open", glow=1.5, head=4),  # thrust
            P(lean=8, arm_n=(-14, -14), staff=10, hat_lag=-10, braid=-14, eye="open", mouth="smile", glow=0.9),
            P(lean=0, arm_n=(-18, -35), staff=0, hat_lag=0, eye="open", mouth="smile", glow=0.4)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_skill(n):
    keys = [P(sy=0.93, sx=1.06, bob=3, arm_n=(-20, -50), arm_f=(10, 10), staff=-6, leg_n=(-14, 28), leg_f=(8, 20), eye="closed", mouth="small", glow=0.6),
            P(lift=10, sy=1.05, arm_n=(-100, -70), arm_f=(80, 20), staff=-4, hat_lag=-12, braid=-14, eye="cast", mouth="grin", glow=1.4, head=-4),
            P(lift=16, sy=1.06, arm_n=(-130, -50), arm_f=(95, 24), staff=-2, hat_lag=-18, braid=-20, eye="cast", mouth="open", glow=2.0, head=-6),
            P(lift=6, lean=10, sx=1.05, sy=0.96, arm_n=(-30, -10), arm_f=(30, 10), staff=22, hat_lag=-24, braid=-30, eye="cast", mouth="open", glow=2.4, leg_n=(20, 6), leg_f=(-16, 18)),
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
    """arr: (H,W,4) float 0..1, straight alpha -> PNG (no dependencies)."""
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


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "ArtSource", "Chibi2D", "Mage")
    only = set(sys.argv[2].split(",")) if len(sys.argv) > 2 and sys.argv[2] else None
    os.makedirs(out, exist_ok=True)
    meta_path = os.path.join(out, "Mage_frames.json")
    meta = {"cell": S, "frame_m": FRAME_M, "ground_from_bottom_m": round(GROUND_M, 4), "clips": {}}
    if os.path.exists(meta_path):
        with open(meta_path, encoding="utf-8") as f:
            meta["clips"] = json.load(f).get("clips", {})
    for name, fn, n, fps, loop in CLIPS:
        if only and name not in only:
            continue
        frames = [render(p) for p in fn(n)]
        write_png(os.path.join(out, f"Mage_{name}.png"), np.concatenate(frames, axis=1))
        meta["clips"][name] = {"frames": n, "fps": fps, "loop": loop, "file": f"Mage_{name}.png"}
        print("BAKED", name, n)
    with open(meta_path, "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2)


if __name__ == "__main__":
    main()
