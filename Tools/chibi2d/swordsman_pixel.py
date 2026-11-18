"""AdventureKing original PIXEL-ART Swordsman, heroic proportions (head ~ 1/5.5 of height), side view, faces right.
Same analytic engine as mage_rig.py in its pixel mode (128 px frames, fixed palette, hard 1 px outline, no anti-aliasing).

  "<Blender>/5.2/python/bin/python.exe" Tools/chibi2d/swordsman_pixel.py [out_dir] [only_clip,...] [--preview]

Output: ArtSource/Chibi2D/_Hand/SwordsmanPixel (never overwrites ArtSource/Chibi2D/Swordsman). Original design: silver spiky hair, teal tunic,
steel pauldron + bracers, long navy coat-tail with gold trim, brown boots, greatsword with a winged gold guard. Not based on any existing character.
"""
import json
import math
import os
import sys

if "--pixel" not in sys.argv:
    sys.argv.append("--pixel")          # must be set before mage_rig is imported (also in multiprocessing children)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402
import mage_rig as M  # noqa: E402
from mage_rig import (Canvas, Xf, X, Y, CX, G, S, FRAME_M, GROUND_M, ellipse, capsule, box, star, union, smin, ring, lock,  # noqa: E402
                      rgb, INK, sample, write_png, PX)

M.PAL.update({
    "shair": (rgb("#ffd54a"), rgb("#e0902a"), rgb("#fff3a8"), rgb("#5a2e0a")),
    "steel": (rgb("#d3dcea"), rgb("#8e9bb6"), rgb("#ffffff"), rgb("#1c2540")),
    "leather": (rgb("#9a6238"), rgb("#62401f"), rgb("#c98c52"), rgb("#24120a")),
    "coat": (rgb("#d8434e"), rgb("#9c2433"), rgb("#ff8088"), rgb("#3e0a14")),
    "tunic": (rgb("#3a63c8"), rgb("#243f94"), rgb("#80a6f2"), rgb("#0c1840")),
    "pants": (rgb("#3c4258"), rgb("#272b3c"), rgb("#5f6684"), rgb("#10121d")),
    "boot2": (rgb("#7a5234"), rgb("#4c301c"), rgb("#a87348"), rgb("#1f1008")),
    "eye": (rgb("#4fa8ff"), rgb("#2a64d0"), rgb("#bfe0ff"), rgb("#0c1f4a")),
    "fire": (rgb("#ff9a2e"), rgb("#d9481a"), rgb("#ffe27a"), rgb("#5a1408")),
})
PAL = M.PAL
SKIN_INK = PAL["skin"][3]
M.BASE = 0.86
PX = M.PX = 192                         # 192 px cell (4x4 supersample per pixel): detail level of the existing hero sprites
M.LW = 2.0                              # ink lines ~1 px at this cell size
HEAD_SCALE = (1.3, 1.3)                 # chibi: big head on a sturdy body (the doc forbids tall thin mannequins)
FRAME_OUT = 1.9                         # metres per frame side: the hero stands ~1.15 m like the existing sprites
GROUND_OUT = (S - G) / S * FRAME_OUT

HIP, SH, HEAD = (CX + 2, 208), (CX + 6, 134), (CX + 6, 92)


def pose_default():
    return dict(bob=0.0, lean=0.0, sx=1.0, sy=1.0, fall=0.0, fall_dx=0.0, head=0.0, hair=0.0, coat=0.0,
                arm_n=(-14.0, -50.0), arm_f=(10.0, 16.0), leg_n=(0.0, 0.0), leg_f=(0.0, 0.0), sword=34.0, eye="open", mouth="line",
                flash=0.0, lift=0.0, slash=0.0, fire=0.0, spark=0.0)


def face(cv, lx, ly, p):
    ex, ey = HEAD[0] + 14, HEAD[1] + 2
    e = p["eye"]
    if e in ("open", "cast"):
        cv.solid(ellipse(lx, ly, ex, ey, 6.5, 8.5), rgb("#ffffff"))
        cv.solid(ellipse(lx, ly, ex + 1, ey + 1, 5, 7), PAL["eye"][0])
        cv.solid(ellipse(lx, ly, ex + 1.5, ey + 2, 3, 4.5), PAL["eye"][3])
        cv.solid(ellipse(lx, ly, ex - 1.5, ey - 3, 2.2, 2.4), rgb("#ffffff"))
        cv.solid(np.where(ly < ey - 2, ring(ellipse(lx, ly, ex, ey, 7, 9), 2.4), 99.0), INK)
    elif e == "happy":
        cv.solid(np.where(ly < ey + 4, ring(ellipse(lx, ly, ex, ey + 4, 6, 5), 2.6), 99.0), INK)
    elif e in ("blink", "closed"):
        cv.solid(capsule(lx, ly, ex - 6, ey, ex + 6, ey, 1.4, 1.4), INK)
    elif e == "hurt":
        cv.solid(capsule(lx, ly, ex - 5, ey - 5, ex + 5, ey, 1.6, 1.6), INK)
        cv.solid(capsule(lx, ly, ex - 5, ey + 5, ex + 5, ey, 1.6, 1.6), INK)
    elif e == "dead":
        cv.solid(capsule(lx, ly, ex - 5, ey - 5, ex + 5, ey + 5, 1.6, 1.6), INK)
        cv.solid(capsule(lx, ly, ex - 5, ey + 5, ex + 5, ey - 5, 1.6, 1.6), INK)
    tilt = {"hurt": -4, "cast": -5, "dead": -3}.get(e, -2)
    cv.solid(capsule(lx, ly, ex - 7, ey - 14 + tilt * 0.5, ex + 7, ey - 14 - tilt * 0.5, 1.7, 1.4), PAL["shair"][1])
    cv.solid(capsule(lx, ly, HEAD[0] + 23, ey + 8, HEAD[0] + 25, ey + 11, 1.2, 1.0), rgb("#d98f78"))
    mx, my = HEAD[0] + 16, HEAD[1] + 20
    m = p["mouth"]
    if m in ("open", "grin"):
        cv.solid(ellipse(lx, ly, mx, my, 5, 4.5 if m == "open" else 3.4), rgb("#7a1f40"))
    elif m == "o":
        cv.solid(ellipse(lx, ly, mx, my, 2.8, 3.4), rgb("#7a1f40"))
    elif m == "sad":
        cv.solid(np.where(ly < my + 2, ring(ellipse(lx, ly, mx, my + 3, 4, 3), 1.8), 99.0), rgb("#8a2f52"))
    else:
        cv.solid(capsule(lx, ly, mx - 3, my, mx + 3, my + (0.6 if m == "smile" else 0), 1.1, 1.1), rgb("#8a2f52"))


def render(p):
    cv = Canvas()
    cv.flash = p["flash"]
    root = Xf([((CX, G), p["lean"] + p["fall"], (p["fall_dx"], p["bob"] - p["lift"]), (p["sx"] * M.BASE, p["sy"] * M.BASE))])
    dim = (rgb("#1c1442"), 0.28)

    # ---- long coat tail behind the legs
    ct = root.child((HIP[0] - 2, HIP[1] - 8), p["coat"])
    lx, ly = ct(X, Y)
    tail = union(capsule(lx, ly, HIP[0] - 4, HIP[1] - 4, HIP[0] - 16, HIP[1] + 92, 17, 20), capsule(lx, ly, HIP[0] - 10, HIP[1] + 40, HIP[0] - 26, HIP[1] + 100, 14, 12))
    cv.fill(tail, lx, ly, "coat", (HIP[0] - 10, HIP[1] + 50), 60, line=1.0)
    cv.fill(np.where((ly > HIP[1] + 79) & (ly < HIP[1] + 85), tail, 99.0), lx, ly, "gold", (HIP[0] - 14, HIP[1] + 82), 20, line=0.5, shade=False)

    # ---- back hair
    ht = root.child(HEAD, p["head"] + p["hair"] * 0.3, (0, 0), HEAD_SCALE)
    lx, ly = ht(X, Y)
    cv.fill(ellipse(lx, ly, HEAD[0] - 12, HEAD[1] - 4, 26, 26), lx, ly, "shair", (HEAD[0] - 12, HEAD[1]), 28, line=1.0)
    for pts in ([(HEAD[0] - 14, HEAD[1] - 14), (HEAD[0] - 36, HEAD[1] - 18), (HEAD[0] - 50, HEAD[1] - 10)],
                [(HEAD[0] - 16, HEAD[1] + 4), (HEAD[0] - 40, HEAD[1] + 10), (HEAD[0] - 54, HEAD[1] + 24)],
                [(HEAD[0] - 14, HEAD[1] + 16), (HEAD[0] - 32, HEAD[1] + 30), (HEAD[0] - 40, HEAD[1] + 48)]):
        cv.fill(lock(lx, ly, pts, 8, 1.6), lx, ly, "shair", pts[1], 22, line=0.8)

    # ---- far arm (dim) + legs
    sf = root.child(SH, p["arm_f"][0], (-5, 2))
    ef = sf.child((SH[0], SH[1] + 44), p["arm_f"][1])
    lx, ly = sf(X, Y)
    cv.fill(capsule(lx, ly, SH[0], SH[1], SH[0], SH[1] + 46, 10.5, 8.5), lx, ly, "tunic", SH, 24, line=0.8, tint=dim)
    lx, ly = ef(X, Y)
    cv.fill(capsule(lx, ly, SH[0], SH[1] + 44, SH[0], SH[1] + 84, 9.5, 8), lx, ly, "steel", (SH[0], SH[1] + 64), 22, line=0.8, tint=dim)
    cv.fill(ellipse(lx, ly, SH[0], SH[1] + 90, 8.5, 8.5), lx, ly, "skin", (SH[0], SH[1] + 90), 7, line=0.8, tint=dim)

    def leg(angles, dx, tint):
        hp = root.child((HIP[0] + dx, HIP[1]), angles[0])
        kn = hp.child((HIP[0] + dx, HIP[1] + 78), angles[1])
        fx = HIP[0] + dx
        lx, ly = hp(X, Y)
        cv.fill(capsule(lx, ly, fx, HIP[1], fx, HIP[1] + 80, 18.5, 14), lx, ly, "pants", (fx, HIP[1] + 40), 44, line=1.0, tint=tint)
        lx, ly = kn(X, Y)
        cv.fill(capsule(lx, ly, fx, HIP[1] + 76, fx, HIP[1] + 140, 14.5, 11.5), lx, ly, "boot2", (fx, HIP[1] + 108), 40, line=1.0, tint=tint)
        cv.fill(ellipse(lx, ly, fx + 2, HIP[1] + 78, 11.5, 10), lx, ly, "steel", (fx + 2, HIP[1] + 78), 10, line=0.8, tint=tint)         # knee guard
        cv.fill(union(ellipse(lx, ly, fx + 10, HIP[1] + 148, 25, 10), ellipse(lx, ly, fx - 1, HIP[1] + 140, 11, 14)), lx, ly, "boot2", (fx + 6, HIP[1] + 144), 22, line=1.0, tint=tint)
        cv.fill(box(lx, ly, fx, HIP[1] + 118, 10, 3, 1.5), lx, ly, "gold", (fx, HIP[1] + 118), 10, line=0.6, shade=False, tint=tint)

    leg(p["leg_f"], -8, dim)
    leg(p["leg_n"], 8, None)

    # ---- torso: tunic + chest plate + belt, coat front flap
    lx, ly = root(X, Y)
    torso = smin(capsule(lx, ly, CX + 2, 152, CX + 2, 198, 28, 23), ellipse(lx, ly, CX + 4, 150, 31, 21), 6)
    cv.fill(torso, lx, ly, "tunic", (CX + 2, 175), 36, line=1.0)
    cv.fill(np.where(ly < 182, smin(capsule(lx, ly, CX + 7, 146, CX + 7, 180, 21, 17), ellipse(lx, ly, CX + 9, 148, 23, 16), 5), 99.0), lx, ly, "steel", (CX + 6, 160), 26, line=0.9)
    cv.fill(box(lx, ly, CX + 2, 201, 24, 5.5, 2), lx, ly, "leather", (CX + 2, 200), 20, line=0.8)
    cv.fill(box(lx, ly, CX + 12, 200, 4.5, 5.5, 1), lx, ly, "gold", (CX + 12, 200), 6, line=0.6, shade=False)
    cv.fill(box(lx, ly, CX + 14, 226, 7, 22, 3), lx, ly, "coat", (CX + 14, 226), 24, line=0.8)
    cv.fill(box(lx, ly, CX + 2, 130, 7, 6, 3), lx, ly, "skin", (CX + 2, 130), 8, line=0.8)                      # neck

    # ---- head, face, front hair
    hx = root.child(HEAD, p["head"], (0, 0), HEAD_SCALE)
    lx, ly = hx(X, Y)
    cv.fill(ellipse(lx, ly, HEAD[0], HEAD[1], 26, 29), lx, ly, "skin", HEAD, 30, line=1.2)
    face(cv, lx, ly, p)
    hh = root.child(HEAD, p["head"] + p["hair"] * 0.15, (0, 0), HEAD_SCALE)
    lx, ly = hh(X, Y)
    cv.fill(ellipse(lx, ly, HEAD[0] + 2, HEAD[1] - 18, 26, 12), lx, ly, "shair", (HEAD[0], HEAD[1] - 18), 28, line=0.9)
    for i, pts in enumerate(([(HEAD[0] - 6, HEAD[1] - 22), (HEAD[0] + 2, HEAD[1] - 8), (HEAD[0] + 3, HEAD[1] + 4)],
                             [(HEAD[0] + 8, HEAD[1] - 24), (HEAD[0] + 16, HEAD[1] - 12), (HEAD[0] + 18, HEAD[1] - 2)],
                             [(HEAD[0] + 18, HEAD[1] - 22), (HEAD[0] + 26, HEAD[1] - 14), (HEAD[0] + 30, HEAD[1] - 6)],
                             [(HEAD[0] - 20, HEAD[1] - 16), (HEAD[0] - 24, HEAD[1] - 4), (HEAD[0] - 26, HEAD[1] + 10)])):
        cv.fill(lock(lx, ly, pts, 7 - i * 0.6, 1.2), lx, ly, "shair", pts[1], 16, line=0.8)
    cv.fill(lock(lx, ly, [(HEAD[0] + 2, HEAD[1] - 30), (HEAD[0] + 8, HEAD[1] - 42), (HEAD[0] - 2, HEAD[1] - 50)], 5, 1.2), lx, ly, "shair", (HEAD[0] + 4, HEAD[1] - 40), 18, line=0.8)
    cv.fill(np.where(np.abs(ly - (HEAD[1] - 12)) < 3.6, ellipse(lx, ly, HEAD[0], HEAD[1], 24.5, 27.5), 99.0), lx, ly, "coat", (HEAD[0], HEAD[1] - 12), 12, line=0.6)  # headband

    # ---- sword (behind the hand), near arm in front
    sn = root.child(SH, p["arm_n"][0])
    en = sn.child((SH[0], SH[1] + 44), p["arm_n"][1])
    hand = (SH[0], SH[1] + 90)
    stf = en.child(hand, p["sword"] - (p["arm_n"][0] + p["arm_n"][1]))
    lx, ly = stf(X, Y)
    bx, by_ = hand
    blade = union(box(lx, ly, bx, by_ - 64, 10.5, 46, 1.5), capsule(lx, ly, bx, by_ - 100, bx, by_ - 128, 10.5, 0.8))
    cv.fill(blade, lx, ly, "steel", (bx, by_ - 60), 56, line=0.9)
    cv.solid(ring(capsule(lx, ly, bx + 0.5, by_ - 26, bx + 0.5, by_ - 104, 0.5, 0.5), 1.3), PAL["steel"][1], 0.9)
    cv.fill(box(lx, ly, bx, by_ - 17, 16, 4.2, 1.5), lx, ly, "gold", (bx, by_ - 17), 12, line=0.7)
    for sgn in (-1, 1):
        cv.fill(capsule(lx, ly, bx + sgn * 12, by_ - 17, bx + sgn * 18, by_ - 26, 3, 1.5), lx, ly, "gold", (bx + sgn * 14, by_ - 20), 8, line=0.6, shade=False)
    cv.fill(capsule(lx, ly, bx, by_ + 4, bx, by_ - 13, 4.6, 4.6), lx, ly, "leather", (bx, by_ - 4), 10, line=0.7)
    cv.fill(ellipse(lx, ly, bx, by_ + 10, 4.6, 4.6), lx, ly, "gold", (bx, by_ + 10), 5, line=0.6, shade=False)

    lx, ly = sn(X, Y)
    cv.fill(capsule(lx, ly, SH[0], SH[1], SH[0], SH[1] + 46, 13.5, 11), lx, ly, "tunic", SH, 26, line=1.0)
    cv.fill(ellipse(lx, ly, SH[0] + 1, SH[1] + 2, 19, 15), lx, ly, "steel", SH, 17, line=1.0)                    # pauldron
    cv.fill(ellipse(lx, ly, SH[0] + 2, SH[1] + 1, 8, 6), lx, ly, "gold", SH, 8, line=0.6, shade=False)
    lx, ly = en(X, Y)
    cv.fill(capsule(lx, ly, SH[0], SH[1] + 44, SH[0], SH[1] + 84, 12, 10), lx, ly, "steel", (SH[0], SH[1] + 64), 24, line=1.0)    # bracer
    lx, ly = stf(X, Y)
    cv.fill(box(lx, ly, hand[0], hand[1] - 3, 9.5, 9, 3.5), lx, ly, "skin", hand, 9, line=0.9)

    # ---- tip sparkle while charging + slash crescent (flat, hard-edged)
    if p["spark"] > 0:
        tx, ty = stf.forward((bx, by_ - 128))
        cv.fill(star(X, Y, tx, ty - 4, 11 + 5 * p["spark"], 3.5), X, Y, "fire" if p["fire"] else "steel", (tx, ty - 4), 14, line=1.0, shade=False)
    if p["slash"] > 0:
        a = min(1.0, p["slash"])
        outer = ellipse(X, Y, CX + 46, 152, 150, 138)
        inner = ellipse(X, Y, CX + 2 + (1 - a) * 36, 158, 132, 120)
        ang = np.degrees(np.arctan2(Y - 152, X - (CX + 46)))
        cres = np.where((ang > -72) & (ang < 68) & (X > CX + 12), np.maximum(outer, -inner), 99.0)
        cv.fill(cres, X, Y, "fire" if p["fire"] else "steel", (CX + 150, 152), 140, line=1.0, shade=False)
    return cv.finish(2.2)


# ------------------------------------------------------------------ animation
def P(**kw):
    d = pose_default()
    d.update(kw)
    return d


def clip_idle(n):
    out = []
    for i in range(n):
        s = math.sin(i / n * 2 * math.pi)
        out.append(P(bob=-1.5 * (0.5 + 0.5 * s), sy=1.0 + 0.01 * s, head=1.0 * s, hair=-4 * s, coat=3 * s, arm_n=(-14 + 2 * s, -50 + 3 * s),
                     arm_f=(10 - 2 * s, 16), sword=34 + 1.5 * s, eye="blink" if i == int(n * 0.72) else "open"))
    return out


def clip_run(n):
    out = []
    for i in range(n):
        u = i / n
        a, b = math.sin(u * 2 * math.pi), math.cos(u * 2 * math.pi)
        bounce = abs(math.sin(u * 2 * math.pi))
        out.append(P(bob=-6 * bounce, lean=10, head=-3 + 2 * a, hair=-16 - 5 * bounce, coat=-22 + 8 * a,
                     leg_n=(46 * a, -50 * max(0, -a) - 8), leg_f=(-46 * a, -50 * max(0, a) - 8),
                     arm_n=(-28 - 20 * b, -70), arm_f=(40 * a + 8, 30), sword=66 + 6 * b))
    return out


def clip_jump(n):
    keys = [P(sy=0.9, sx=1.06, bob=4, leg_n=(-26, 40), leg_f=(-10, 30), arm_n=(-5, -40), eye="closed", hair=-6),
            P(lift=30, sy=1.05, leg_n=(24, 70), leg_f=(-10, 50), arm_n=(-70, -50), arm_f=(55, 40), sword=70, eye="open", mouth="o", hair=-24, coat=-26, head=-4),
            P(lift=40, leg_n=(18, 60), leg_f=(-10, 46), arm_n=(-80, -44), arm_f=(60, 34), sword=64, mouth="o", hair=-14, coat=-18)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_fall(n):
    keys = [P(lift=36, leg_n=(10, 30), leg_f=(-8, 26), arm_n=(-80, -40), arm_f=(70, 24), sword=44, mouth="o", hair=14, coat=24, head=4),
            P(lift=20, leg_n=(16, 40), leg_f=(-12, 34), arm_n=(-88, -34), arm_f=(78, 24), sword=40, mouth="o", hair=20, coat=30, head=5)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_climb(n):
    out = []
    for i in range(n):
        a = math.sin(i / n * 2 * math.pi)
        out.append(P(bob=-2 * abs(a), lean=-3, leg_n=(-10 + 30 * a, 50 + 20 * max(0, a)), leg_f=(-10 - 30 * a, 50 + 20 * max(0, -a)),
                     arm_n=(-150 + 30 * a, -20), arm_f=(-150 - 30 * a, -20), sword=-24, head=-5, hair=6, coat=6))
    return out


def clip_hit(n):
    keys = [P(flash=0.9, lean=-12, sy=0.95, head=-10, hair=22, coat=26, arm_n=(8, -14), arm_f=(30, 12), sword=-10, eye="hurt", mouth="open", leg_n=(-8, 14)),
            P(lean=-9, sy=0.97, head=-7, hair=14, coat=18, arm_n=(4, -20), arm_f=(24, 12), sword=0, eye="hurt", mouth="open"),
            P(lean=-4, head=-3, hair=6, coat=6, eye="hurt")]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_death(n):
    keys = [P(flash=0.8, lean=-10, head=-10, hair=18, eye="hurt", mouth="open", arm_n=(10, -14), sword=0),
            P(lean=-26, fall=-30, fall_dx=30, bob=-10, head=-12, hair=28, eye="dead", mouth="o", arm_n=(30, 10), arm_f=(50, 20), sword=-30),
            P(fall=-72, fall_dx=95, bob=-22, head=-6, hair=18, eye="dead", mouth="o", arm_n=(40, 20), arm_f=(60, 20), sword=-50),
            P(fall=-86, fall_dx=125, bob=-26, head=-4, hair=8, eye="dead", mouth="sad", arm_n=(48, 24), arm_f=(66, 20), sword=-60)]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_attack(n):
    keys = [P(lean=-8, arm_n=(60, -30), sword=-40, hair=12, coat=12, leg_n=(-10, 20), leg_f=(12, 6)),
            P(lean=-12, arm_n=(120, -34), sword=-22, hair=20, coat=20, eye="cast", mouth="grin", spark=0.6),
            P(lean=14, sx=1.04, sy=0.97, arm_n=(-70, -12), sword=62, slash=1.0, hair=-22, coat=-28, leg_n=(22, 8), leg_f=(-16, 20), eye="cast", mouth="open", head=4),
            P(lean=9, arm_n=(-42, -16), sword=98, slash=0.5, hair=-10, coat=-14),
            P()]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


def clip_skill(n):
    keys = [P(sy=0.92, sx=1.05, bob=4, arm_n=(-20, -50), sword=10, leg_n=(-14, 36), leg_f=(8, 26), eye="closed", spark=0.5, fire=1),
            P(lift=10, sy=1.04, arm_n=(-112, -8), arm_f=(80, 20), sword=30, hair=-12, coat=-12, eye="cast", mouth="grin", spark=1.0, fire=1, head=-4),
            P(lift=16, sy=1.05, arm_n=(-120, -4), arm_f=(95, 24), sword=24, hair=-18, coat=-18, eye="cast", mouth="open", spark=1.5, fire=1, head=-6),
            P(lift=6, lean=14, sx=1.05, sy=0.96, arm_n=(-30, -10), arm_f=(30, 10), sword=104, slash=1.0, fire=1, hair=-24, coat=-30, eye="cast", mouth="open", leg_n=(22, 8), leg_f=(-18, 20)),
            P(lean=4, arm_n=(-20, -30), sword=64, slash=0.4, fire=1, eye="happy", mouth="grin"),
            P()]
    return [sample(keys, i / max(1, n - 1)) for i in range(n)]


CLIPS = [
    ("Idle", clip_idle, 8, 8, True), ("Run", clip_run, 8, 14, True), ("Jump", clip_jump, 4, 10, False), ("Fall", clip_fall, 3, 8, False),
    ("Climb", clip_climb, 8, 10, True), ("Hit", clip_hit, 4, 12, False), ("Death", clip_death, 6, 8, False),
    ("Attack", clip_attack, 8, 14, False), ("Skill", clip_skill, 10, 12, False),
]


def preview(out_dir, frames):
    picks = [("Idle", 0), ("Run", 1), ("Run", 5), ("Jump", 1), ("Hit", 1), ("Attack", 1), ("Attack", 2), ("Death", 5),
             ("Skill", 1), ("Skill", 2), ("Skill", 3), ("Climb", 2)]
    cells = []
    for clip, i in picks:
        f = frames[clip][i]
        n_ = f.shape[0]
        bg = np.zeros((n_, n_, 3), np.float32) + np.array([0.86, 0.9, 0.84], np.float32)
        bg[int((G + 2) * n_ / S):, :, :] = np.array([0.55, 0.7, 0.4], np.float32)
        a = f[..., 3:4]
        cells.append(np.concatenate([f[..., :3] * a + bg * (1 - a), np.ones((n_, n_, 1), np.float32)], axis=2))
    rows = [np.concatenate(cells[r * 4:(r + 1) * 4], axis=1) for r in range(3)]
    sheet = np.concatenate(rows, axis=0)
    write_png(os.path.join(out_dir, "Swordsman_preview.png"), np.repeat(np.repeat(sheet, 2, 0), 2, 1))     # nearest-neighbour zoom


def _task(job):
    name, i = job
    for cname, fn, n, fps, loop in CLIPS:
        if cname == name:
            return render(fn(n)[i])


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    out = args[0] if args else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "ArtSource", "Chibi2D", "_Hand", "SwordsmanPixel")
    only = set(args[1].split(",")) if len(args) > 1 and args[1] else None
    os.makedirs(out, exist_ok=True)
    meta_path = os.path.join(out, "Swordsman_frames.json")
    meta = {"cell": PX, "pixel": True, "frame_m": FRAME_OUT, "ground_from_bottom_m": round(GROUND_OUT, 4), "clips": {}}
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
    if "--preview" in sys.argv and all(k in frames for k in ("Idle", "Run", "Jump", "Attack", "Skill", "Hit", "Death", "Climb")):
        preview(out, frames)


if __name__ == "__main__":
    main()

