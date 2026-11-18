"""AdventureKing original 2D chibi Swordsman (hand-built, layered, side view, faces right). Same engine as mage_rig.py.

Run with the Python that ships with Blender (numpy only):
  "<Blender>/5.2/python/bin/python.exe" Tools/chibi2d/swordsman_rig.py [out_dir] [only_clip,...] [--preview]

Default output: ArtSource/Chibi2D/_Hand/Swordsman (never overwrites ArtSource/Chibi2D/Swordsman). Pure 2D: analytic shapes drawn back to
front, hard two-tone cel shading, ink line round every part + bold silhouette outline. Silver spiky hair, blue plate armour, red cape +
headband, round shield on the far arm, steel sword on the near arm. Original design, no outside art.
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402
import mage_rig as M  # noqa: E402
from mage_rig import (Canvas, Xf, X, Y, CX, G, BASE, S, FRAME_M, GROUND_M, ellipse, capsule, box, star, union, smin, ring, lock,  # noqa: E402
                      rgb, INK, sample, write_png, PX)

M.PAL.update({
    "shair": (rgb("#e8eef9"), rgb("#a9b8d8"), rgb("#ffffff"), rgb("#3a4673")),
    "armor": (rgb("#3f7bdc"), rgb("#2650a8"), rgb("#86b6ff"), rgb("#0f2458")),
    "steel": (rgb("#d3dcea"), rgb("#8e9bb6"), rgb("#ffffff"), rgb("#28324f")),
    "leather": (rgb("#9a6238"), rgb("#62401f"), rgb("#c98c52"), rgb("#2a160a")),
    "cloth": (rgb("#e0454f"), rgb("#a02536"), rgb("#ff8088"), rgb("#4a0f18")),
    "under": (rgb("#34408a"), rgb("#222c66"), rgb("#5766c4"), rgb("#0e1440")),
})
PAL = M.PAL


def pose_default():
    return dict(bob=0.0, lean=0.0, sx=1.0, sy=1.0, fall=0.0, fall_dx=0.0, head=0.0, head_dx=0.0, head_dy=0.0, hair=0.0, cape=0.0,
                arm_n=(-18.0, -35.0), arm_f=(12.0, 10.0), leg_n=(0.0, 0.0), leg_f=(0.0, 0.0), sword=28.0, eye="open", mouth="smile",
                flash=0.0, glow=0.0, lift=0.0, blush=1.0, slash=0.0)


def draw_face(cv, lx, ly, hc, p):
    ex, ey = hc[0] + 38, hc[1] + 10
    if p["blush"] > 0:
        cv.solid(ellipse(lx, ly, ex + 10, ey + 28, 15, 8.5), rgb("#ff8fa8"), 0.65 * p["blush"])
        for i in range(3):
            cv.solid(capsule(lx, ly, ex + 3 + i * 7, ey + 32, ex + 6 + i * 7, ey + 26, 0.9, 0.9), rgb("#ff5f86"), 0.6 * p["blush"])
    eye = p["eye"]
    if eye in ("open", "cast"):
        ry = 25 if eye == "open" else 28
        cv.solid(ellipse(lx, ly, ex, ey, 19, ry), rgb("#ffffff"))
        cv.fill(ellipse(lx, ly, ex + 2, ey + 2, 15.5, ry - 2.5), lx, ly, (rgb("#4fa8ff"), rgb("#2a64d0"), rgb("#b8dcff"), rgb("#12306e")), (ex + 2, ey - 4), 24, line=1.0)
        cv.solid(ellipse(lx, ly, ex + 3, ey + 5, 9, ry - 12), rgb("#0c1f4a"))
        cv.solid(ellipse(lx, ly, ex - 6, ey - 9, 6.4, 7.6), rgb("#ffffff"))
        cv.solid(ellipse(lx, ly, ex + 9, ey + 11, 3.2, 3.6), rgb("#ffffff"), 0.95)
        cv.solid(np.where(ly < ey - 3, ring(ellipse(lx, ly, ex, ey, 19.5, ry + 0.5), 3.6), 99.0), INK)
        cv.solid(capsule(lx, ly, ex - 17, ey - ry + 6, ex - 24, ey - ry + 1, 2.6, 0.6), INK)
    elif eye == "happy":
        cv.solid(np.where(ly < ey + 8, ring(ellipse(lx, ly, ex, ey + 8, 17, 15), 4.0), 99.0), INK)
    elif eye in ("blink", "closed"):
        cv.solid(np.where(ly > ey - 4, ring(ellipse(lx, ly, ex, ey - 4, 17, 11), 3.8), 99.0), INK)
        cv.solid(capsule(lx, ly, ex - 17, ey - 3, ex - 24, ey - 9, 2.4, 0.6), INK)
    elif eye == "hurt":
        cv.solid(capsule(lx, ly, ex - 15, ey - 14, ex + 13, ey, 3, 3), INK)
        cv.solid(capsule(lx, ly, ex - 15, ey + 14, ex + 13, ey, 3, 3), INK)
        cv.solid(ellipse(lx, ly, ex + 24, ey - 20, 3, 5), rgb("#8fd0ff"))
    elif eye == "dead":
        cv.solid(capsule(lx, ly, ex - 13, ey - 13, ex + 13, ey + 13, 3, 3), INK)
        cv.solid(capsule(lx, ly, ex - 13, ey + 13, ex + 13, ey - 13, 3, 3), INK)
    by = ey - (34 if eye in ("open", "cast") else 26)
    tilt = {"hurt": -9, "cast": -8, "dead": -7}.get(eye, -3)                     # brave, slightly angled brows
    cv.solid(capsule(lx, ly, ex - 16, by + tilt * 0.5, ex + 15, by - tilt * 0.5, 3.4, 2.4), rgb("#5a6a98"))
    cv.solid(capsule(lx, ly, hc[0] + 70, hc[1] + 26, hc[0] + 72, hc[1] + 29, 1.6, 1.2), rgb("#d98f78"))
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


def render(p):
    cv = Canvas()
    cv.flash = p["flash"]
    root = Xf([((CX, G), p["lean"] + p["fall"], (p["fall_dx"], p["bob"] - p["lift"]), (p["sx"] * BASE, p["sy"] * BASE))])
    hip, sh, hc = (CX + 2, 300), (CX + 8, 236), (CX + 14, 148)

    # ---- cape (behind everything)
    cx_ = root.child((CX - 2, 232), p["cape"])
    lx, ly = cx_(X, Y)
    cape = smin(capsule(lx, ly, CX - 4, 238, CX - 34, 314, 18, 30), ellipse(lx, ly, CX - 8, 246, 24, 20), 8)
    cv.fill(cape, lx, ly, "cloth", (CX - 14, 282), 50)
    cv.solid(np.where(ly < 300, ring(capsule(lx, ly, CX - 10, 252, CX - 28, 304, 3, 5) - 7, 1.4), 99.0), rgb("#ff8088"), 0.6)

    # ---- back hair: spiky silver tufts + headband tails
    ht = root.child(hc, p["head"] + p["hair"] * 0.35, (p["head_dx"], p["head_dy"]))
    lx, ly = ht(X, Y)
    cv.fill(ellipse(lx, ly, hc[0] - 30, hc[1] - 4, 64, 70), lx, ly, "shair", (hc[0] - 30, hc[1]), 80)
    for i, pts in enumerate(([(hc[0] - 40, hc[1] - 36), (hc[0] - 84, hc[1] - 56), (hc[0] - 114, hc[1] - 50)],
                             [(hc[0] - 44, hc[1] - 2), (hc[0] - 90, hc[1] + 6), (hc[0] - 120, hc[1] + 24)],
                             [(hc[0] - 40, hc[1] + 30), (hc[0] - 76, hc[1] + 54), (hc[0] - 96, hc[1] + 86)])):
        cv.fill(lock(lx, ly, pts, 17, 2.2), lx, ly, "shair", pts[1], 44, line=1.8)
    tail_a = root.child((hc[0] - 46, hc[1] - 40), p["hair"] * 0.9 + 8)
    lx, ly = tail_a(X, Y)
    for k, off in enumerate((0, 16)):
        cv.fill(lock(lx, ly, [(hc[0] - 46, hc[1] - 40 + off), (hc[0] - 80, hc[1] - 34 + off), (hc[0] - 106, hc[1] - 24 + off)], 9, 3), lx, ly, "cloth", (hc[0] - 70, hc[1] - 34), 40, line=1.6)

    # ---- far arm + round shield (dimmed: it is behind the body)
    dim = (rgb("#241a52"), 0.26)
    sf = root.child(sh, p["arm_f"][0], (-6, 2))
    ef = sf.child((sh[0], sh[1] + 28), p["arm_f"][1])
    lx, ly = sf(X, Y)
    cv.fill(capsule(lx, ly, sh[0], sh[1], sh[0], sh[1] + 30, 11, 9), lx, ly, "under", sh, 34, tint=dim)
    lx, ly = ef(X, Y)
    cv.fill(capsule(lx, ly, sh[0], sh[1] + 28, sh[0], sh[1] + 52, 10, 9), lx, ly, "armor", (sh[0], sh[1] + 40), 30, tint=dim)
    cv.fill(ellipse(lx, ly, sh[0], sh[1] + 58, 9, 9), lx, ly, "skin", (sh[0], sh[1] + 58), 10, tint=dim)
    cv.fill(ellipse(lx, ly, sh[0] + 6, sh[1] + 44, 24, 28), lx, ly, "steel", (sh[0] + 6, sh[1] + 44), 30, line=2.2, tint=dim)
    cv.fill(ellipse(lx, ly, sh[0] + 6, sh[1] + 44, 15, 19), lx, ly, "armor", (sh[0] + 6, sh[1] + 44), 20, line=1.4, tint=dim)
    cv.fill(star(lx, ly, sh[0] + 6, sh[1] + 44, 9, 4), lx, ly, "gold", (sh[0] + 6, sh[1] + 44), 9, line=1.0, shade=False, tint=dim)

    # ---- legs: navy trousers, blue greaves with steel knee guard and gold cuff
    def leg(angles, dx, tint):
        hp = root.child((hip[0] + dx, hip[1]), angles[0])
        kn = hp.child((hip[0] + dx, hip[1] + 24), angles[1])
        fx = hip[0] + dx
        lx, ly = hp(X, Y)
        cv.fill(capsule(lx, ly, fx, hip[1], fx, hip[1] + 26, 10.5, 10), lx, ly, "under", (fx, hip[1] + 12), 22, tint=tint)
        lx, ly = kn(X, Y)
        cv.fill(capsule(lx, ly, fx, hip[1] + 24, fx, hip[1] + 48, 10, 9.5), lx, ly, "armor", (fx, hip[1] + 32), 22, tint=tint)
        cv.fill(ellipse(lx, ly, fx + 3, hip[1] + 24, 8, 7), lx, ly, "steel", (fx + 3, hip[1] + 24), 9, line=1.4, tint=tint)
        foot = union(ellipse(lx, ly, fx + 10, hip[1] + 54, 23, 11), ellipse(lx, ly, fx - 2, hip[1] + 46, 12, 14))
        cv.fill(foot, lx, ly, "armor", (fx + 8, hip[1] + 46), 26, tint=tint)
        cv.fill(ellipse(lx, ly, fx + 22, hip[1] + 54, 11, 9), lx, ly, "steel", (fx + 22, hip[1] + 54), 11, line=1.4, tint=tint)
        cv.fill(box(lx, ly, fx - 1, hip[1] + 36, 13, 4.5, 2.5), lx, ly, "gold", (fx, hip[1] + 36), 14, line=1.2, shade=False, tint=tint)

    leg(p["leg_f"], -9, dim)
    leg(p["leg_n"], 11, None)

    # ---- body: plate cuirass, tabard, belt, gorget
    lx, ly = root(X, Y)
    torso = smin(capsule(lx, ly, CX + 4, 246, CX + 2, 280, 29, 35), ellipse(lx, ly, CX + 4, 254, 32, 27), 8)
    cv.fill(torso, lx, ly, "armor", (CX + 2, 270), 48)
    cv.solid(np.where(torso < -1, ring(ellipse(lx, ly, CX + 14, 258, 20, 22), 1.6) + np.where(lx < CX + 6, 99.0, 0.0), 99.0), PAL["armor"][3], 0.6)
    cv.fill(box(lx, ly, CX + 17, 306, 11, 17, 3), lx, ly, "cloth", (CX + 17, 306), 16, line=1.6)                   # tabard flap
    cv.fill(box(lx, ly, CX + 4, 281, 33, 6, 3), lx, ly, "leather", (CX + 4, 281), 30)                                # belt
    cv.fill(box(lx, ly, CX + 20, 281, 8, 9, 2), lx, ly, "gold", (CX + 20, 281), 9, line=1.2)
    cv.fill(star(lx, ly, CX + 18, 258, 10, 4.5), lx, ly, "gold", (CX + 18, 258), 10, line=1.2, shade=False)         # crest
    cv.fill(ellipse(lx, ly, CX + 6, 232, 27, 11), lx, ly, "steel", (CX + 6, 232), 30, line=1.8)                       # gorget

    # ---- head: face, then front hair + headband
    hx = root.child(hc, p["head"], (p["head_dx"], p["head_dy"]))
    lx, ly = hx(X, Y)
    cv.fill(ellipse(lx, ly, hc[0], hc[1], 82, 74), lx, ly, "skin", hc, 86, line=2.4)
    draw_face(cv, lx, ly, hc, p)
    hh = root.child(hc, p["head"] + p["hair"] * 0.25, (p["head_dx"], p["head_dy"]))
    lx, ly = hh(X, Y)
    cv.fill(ellipse(lx, ly, hc[0] + 8, hc[1] - 58, 78, 22), lx, ly, "shair", (hc[0] + 8, hc[1] - 58), 70, line=1.6)
    for i, pts in enumerate(([(hc[0] - 12, hc[1] - 62), (hc[0] + 2, hc[1] - 34), (hc[0] + 6, hc[1] - 8)],
                             [(hc[0] + 18, hc[1] - 66), (hc[0] + 36, hc[1] - 40), (hc[0] + 42, hc[1] - 20)],
                             [(hc[0] + 46, hc[1] - 60), (hc[0] + 64, hc[1] - 42), (hc[0] + 76, hc[1] - 26)],
                             [(hc[0] - 52, hc[1] - 46), (hc[0] - 58, hc[1] - 20), (hc[0] - 64, hc[1] + 4)])):
        cv.fill(lock(lx, ly, pts, 16 - i, 2.0), lx, ly, "shair", pts[1], 40, line=1.8)
    cv.fill(lock(lx, ly, [(hc[0] + 2, hc[1] - 74), (hc[0] + 6, hc[1] - 90), (hc[0] - 10, hc[1] - 100)], 8, 2.0), lx, ly, "shair", (hc[0] + 4, hc[1] - 88), 36, line=1.6)  # ahoge
    band = np.where(np.abs(ly - (hc[1] - 40)) < 8, ellipse(lx, ly, hc[0], hc[1], 82.5, 74.5), 99.0)
    cv.fill(band, lx, ly, "cloth", (hc[0], hc[1] - 40), 30, line=1.6)
    cv.fill(ellipse(lx, ly, hc[0] + 46, hc[1] - 40, 9, 9), lx, ly, "gold", (hc[0] + 46, hc[1] - 40), 9, line=1.4, shade=False)
    cv.fill(ellipse(lx, ly, hc[0] + 46, hc[1] - 40, 4, 4), lx, ly, "gem2" if "gem2" in PAL else "armor", (hc[0] + 46, hc[1] - 40), 5, line=0, shade=False)

    # ---- sword (behind the hand), then the near arm
    sn = root.child(sh, p["arm_n"][0])
    en = sn.child((sh[0], sh[1] + 28), p["arm_n"][1])
    hand = (sh[0], sh[1] + 58)
    stf = en.child(hand, p["sword"] - (p["arm_n"][0] + p["arm_n"][1]))
    lx, ly = stf(X, Y)
    bx, by_ = hand[0], hand[1]
    tip = (bx, by_ - 122)
    blade = union(capsule(lx, ly, bx, by_ - 18, bx, by_ - 112, 8.0, 3.2), capsule(lx, ly, bx, by_ - 108, bx + 1, by_ - 124, 3.4, 0.8))
    cv.fill(blade, lx, ly, "steel", (bx, by_ - 70), 60, line=1.8)
    cv.solid(ring(capsule(lx, ly, bx + 1, by_ - 30, bx + 1, by_ - 100, 0.5, 0.5), 1.4), PAL["steel"][1], 0.9)           # fuller
    cv.fill(box(lx, ly, bx, by_ - 14, 15, 4.6, 2), lx, ly, "gold", (bx, by_ - 14), 14, line=1.4)                          # guard
    cv.fill(ellipse(lx, ly, bx - 15, by_ - 14, 4.5, 4.5), lx, ly, "gold", (bx - 15, by_ - 14), 5, line=1.0, shade=False)
    cv.fill(ellipse(lx, ly, bx + 15, by_ - 14, 4.5, 4.5), lx, ly, "gold", (bx + 15, by_ - 14), 5, line=1.0, shade=False)
    cv.fill(capsule(lx, ly, bx, by_ + 4, bx, by_ - 10, 4.6, 4.6), lx, ly, "leather", (bx, by_ - 2), 10, line=1.4)
    cv.fill(ellipse(lx, ly, bx, by_ + 14, 6, 6), lx, ly, "gold", (bx, by_ + 14), 6, line=1.2, shade=False)

    lx, ly = sn(X, Y)
    cv.fill(capsule(lx, ly, sh[0], sh[1], sh[0], sh[1] + 30, 12, 10), lx, ly, "under", sh, 36, line=2.0)
    cv.fill(ellipse(lx, ly, sh[0] + 1, sh[1] + 2, 20, 17), lx, ly, "steel", (sh[0], sh[1]), 22, line=2.0)             # pauldron
    cv.fill(ellipse(lx, ly, sh[0] + 1, sh[1] + 2, 12, 9), lx, ly, "armor", (sh[0], sh[1]), 12, line=1.2)
    lx, ly = en(X, Y)
    cv.fill(capsule(lx, ly, sh[0], sh[1] + 28, sh[0], sh[1] + 52, 11, 9.5), lx, ly, "armor", (sh[0], sh[1] + 40), 32, line=2.0)
    cv.fill(box(lx, ly, sh[0], sh[1] + 48, 11, 4.5, 2), lx, ly, "gold", (sh[0], sh[1] + 48), 12, line=1.2, shade=False)
    lx, ly = stf(X, Y)
    cv.fill(box(lx, ly, hand[0] + 1, hand[1] - 2, 11, 9, 4), lx, ly, "skin", hand, 13, line=2.0)                        # fist round the grip
    for k in range(3):
        cv.solid(ring(capsule(lx, ly, hand[0] - 3, hand[1] - 7 + k * 5, hand[0] + 9, hand[1] - 7 + k * 5, 0.6, 0.6), 0.9), PAL["skin"][3], 0.8)

    # ---- flat sparkle at the blade tip while charging (no blur / glow: hard-edged star with ink line)
    if p["glow"] >= 0.8:
        tx, ty = stf.forward(tip)
        cv.fill(star(X, Y, tx, ty - 6, 16 + 5 * p["glow"], 5), X, Y, "steel", (tx, ty - 6), 20, line=1.6, shade=False)
    # ---- sword-swing crescent (screen space, in front): opaque white with an ink line; thinner as it fades
    if p["slash"] > 0:
        a = min(1.0, p["slash"])
        outer = ellipse(X, Y, CX + 66, 212, 126, 118)
        inner = ellipse(X, Y, CX + 34 + (1 - a) * 22, 218, 118, 112)
        cres = np.where(X > CX + 14, np.maximum(outer, -inner), 99.0)
        cv.fill(cres, X, Y, "steel", (CX + 120, 212), 120, line=1.8, shade=False)
    return cv.finish(3.2)


# ------------------------------------------------------------------ animation
def P(**kw):
    d = pose_default()
    d.update(kw)
    return d


def clip_idle(n):
    out = []
    for i in range(n):
        s = math.sin(i / n * 2 * math.pi)
        out.append(P(bob=-2.5 * (0.5 + 0.5 * s), sy=1.0 + 0.012 * s, sx=1.0 - 0.008 * s, head=1.2 * s, hair=-5 * s, cape=3 * s,
                     arm_n=(-18 + 2 * s, -35 + 3 * s), arm_f=(12 - 2 * s, 10), sword=28 + 1.5 * s,
                     eye="blink" if i == int(n * 0.72) else "open", mouth="smile"))
    return out


def clip_run(n):
    out = []
    for i in range(n):
        u = i / n
        a, b = math.sin(u * 2 * math.pi), math.cos(u * 2 * math.pi)
        bounce = abs(math.sin(u * 2 * math.pi))
        out.append(P(bob=-7 * bounce, lean=8, head=-2 + 2 * a, head_dy=1.5 * a, hair=-18 - 6 * bounce, cape=-18 + 8 * a,
                     leg_n=(48 * a, -34 * max(0, -a) - 6), leg_f=(-48 * a, -34 * max(0, a) - 6),
                     arm_n=(-30 - 18 * b, -55), arm_f=(40 * a + 8, 22), sword=58 + 6 * b, sx=1.0 + 0.02 * bounce, sy=1.0 - 0.02 * bounce))
    return out


def clip_jump(n):
    keys = [P(sy=0.9, sx=1.08, bob=4, leg_n=(-22, 30), leg_f=(-10, 24), arm_n=(-5, -30), eye="closed", mouth="small", hair=-6),
            P(lift=24, sy=1.06, sx=0.96, leg_n=(20, 48), leg_f=(-14, 40), arm_n=(-70, -50), arm_f=(55, 40), sword=70, eye="happy", mouth="o", hair=-26, cape=-22, head=-4),
            P(lift=32, leg_n=(14, 40), leg_f=(-10, 36), arm_n=(-80, -40), arm_f=(60, 30), sword=60, mouth="o", hair=-14, cape=-12)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_fall(n):
    keys = [P(lift=30, leg_n=(8, 20), leg_f=(-8, 16), arm_n=(-80, -40), arm_f=(70, 20), sword=40, mouth="o", hair=14, cape=24, head=4),
            P(lift=16, leg_n=(14, 30), leg_f=(-12, 26), arm_n=(-88, -30), arm_f=(78, 20), sword=36, mouth="o", hair=20, cape=30, head=5)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_climb(n):
    out = []
    for i in range(n):
        a = math.sin(i / n * 2 * math.pi)
        out.append(P(bob=-3 * abs(a), lean=-3, leg_n=(-10 + 30 * a, 40 + 20 * max(0, a)), leg_f=(-10 - 30 * a, 40 + 20 * max(0, -a)),
                     arm_n=(-150 + 34 * a, -20), arm_f=(-150 - 34 * a, -20), sword=-20, head=-6, hair=6, cape=6, mouth="small", head_dy=-2 * a))
    return out


def clip_hit(n):
    keys = [P(flash=0.9, lean=-12, sy=0.94, sx=1.06, head=-10, hair=22, cape=24, arm_n=(8, -10), arm_f=(30, 8), sword=-10, eye="hurt", mouth="open", leg_n=(-6, 10), blush=0),
            P(lean=-9, sy=0.97, sx=1.03, head=-7, hair=14, cape=16, arm_n=(4, -16), arm_f=(24, 8), sword=0, eye="hurt", mouth="open", blush=0),
            P(lean=-4, head=-3, hair=6, cape=6, eye="hurt", mouth="small", blush=0.5)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_death(n):
    keys = [P(flash=0.8, lean=-10, head=-10, hair=18, eye="hurt", mouth="open", arm_n=(10, -10), blush=0, sword=0),
            P(lean=-24, fall=-30, fall_dx=-8, head=-12, hair=28, eye="dead", mouth="o", arm_n=(30, 10), arm_f=(50, 20), sword=-30, blush=0),
            P(fall=-72, fall_dx=-22, head=-6, hair=18, eye="dead", mouth="o", arm_n=(40, 20), arm_f=(60, 20), sword=-50, blush=0),
            P(fall=-84, fall_dx=-30, bob=4, head=-4, hair=8, eye="dead", mouth="sad", arm_n=(48, 24), arm_f=(66, 20), sword=-60, blush=0)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_attack(n):
    keys = [P(lean=-8, sy=0.97, arm_n=(60, -30), sword=-40, hair=12, cape=12, leg_n=(-8, 14), leg_f=(10, 4), mouth="small"),
            P(lean=-12, arm_n=(125, -34), sword=-18, hair=20, cape=20, eye="cast", mouth="grin", glow=0.5),
            P(lean=14, sx=1.05, sy=0.96, arm_n=(-70, -10), sword=62, slash=1.0, hair=-22, cape=-26, leg_n=(18, 4), leg_f=(-14, 16), eye="cast", mouth="open", glow=1.0, head=4),
            P(lean=8, arm_n=(-42, -14), sword=96, slash=0.5, hair=-10, cape=-12, glow=0.6),
            P()]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_skill(n):
    keys = [P(sy=0.93, sx=1.06, bob=3, arm_n=(-20, -50), sword=10, leg_n=(-14, 28), leg_f=(8, 20), eye="closed", mouth="small", glow=0.6),
            P(lift=14, sy=1.05, arm_n=(-160, -12), arm_f=(80, 20), sword=6, hair=-12, cape=-12, eye="cast", mouth="grin", glow=1.6, head=-4),
            P(lift=22, sy=1.06, arm_n=(-172, -6), arm_f=(95, 24), sword=2, hair=-18, cape=-18, eye="cast", mouth="open", glow=2.2, head=-6),
            P(lift=6, lean=12, sx=1.05, sy=0.96, arm_n=(-30, -10), arm_f=(30, 10), sword=100, slash=1.0, hair=-24, cape=-28, eye="cast", mouth="open", glow=2.4, leg_n=(20, 6), leg_f=(-16, 18)),
            P(lean=3, arm_n=(-20, -30), sword=60, slash=0.4, eye="happy", mouth="grin", glow=0.9),
            P()]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


CLIPS = [
    ("Idle", clip_idle, 8, 8, True), ("Run", clip_run, 8, 14, True), ("Jump", clip_jump, 4, 10, False), ("Fall", clip_fall, 3, 8, False),
    ("Climb", clip_climb, 8, 10, True), ("Hit", clip_hit, 4, 12, False), ("Death", clip_death, 6, 8, False),
    ("Attack", clip_attack, 8, 14, False), ("Skill", clip_skill, 10, 12, False),
]


def preview(out_dir, frames):
    picks = [("Idle", 0), ("Run", 1), ("Run", 5), ("Jump", 1), ("Attack", 1), ("Attack", 2), ("Skill", 3), ("Hit", 0)]
    cells = []
    for clip, i in picks:
        f = frames[clip][i]
        n_ = f.shape[0]
        bg = np.zeros((n_, n_, 3), np.float32) + np.array([0.86, 0.9, 0.84], np.float32)
        bg[int((G + 2) * n_ / S):, :, :] = np.array([0.55, 0.7, 0.4], np.float32)
        a = f[..., 3:4]
        cells.append(np.concatenate([f[..., :3] * a + bg * (1 - a), np.ones((n_, n_, 1), np.float32)], axis=2))
    sheet = np.concatenate([np.concatenate(cells[:4], axis=1), np.concatenate(cells[4:], axis=1)], axis=0)
    write_png(os.path.join(out_dir, "Swordsman_preview.png"), sheet)


def _task(job):
    name, i = job
    for cname, fn, n, fps, loop in CLIPS:
        if cname == name:
            return render(fn(n)[i])


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    out = args[0] if args else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "ArtSource", "Chibi2D", "_Hand", "Swordsman")
    only = set(args[1].split(",")) if len(args) > 1 and args[1] else None
    os.makedirs(out, exist_ok=True)
    meta_path = os.path.join(out, "Swordsman_frames.json")
    meta = {"cell": S, "pixel": False, "frame_m": FRAME_M, "ground_from_bottom_m": round(GROUND_M, 4), "clips": {}}
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
        write_png(os.path.join(out, f"Swordsman_{name}.png"), np.concatenate(frames[name], axis=1))
        meta["clips"][name] = {"frames": n, "fps": fps, "loop": loop, "file": f"Swordsman_{name}.png"}
        print("BAKED", name, n, flush=True)
    with open(meta_path, "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2)
    if "--preview" in sys.argv and all(k in frames for k in ("Idle", "Run", "Jump", "Attack", "Skill", "Hit")):
        preview(out, frames)


if __name__ == "__main__":
    main()
