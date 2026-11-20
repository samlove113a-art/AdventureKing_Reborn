"""Cuts the owner-provided Mage design sheet into CLEAN sprite strips (no text, frames, panels, backgrounds).

  "<Blender>/5.2/python/bin/python.exe" Tools/chibi2d/extract_mage_sheet.py   (needs ArtSource/_render_tmp/sheet.npy, see README in Docs/CHIBI2D_PLAN.md)

For every animation panel of the sheet: find the frames, remove the dark panel background (flood fill from the border, so interior
dark outlines survive), key out the ladder in the Climb frames, snap all colours to one shared 40-colour palette (kills the JPEG /
upscale blur and makes every frame use the same colours), add a crisp 1 px dark outline, then place each frame on a 128 px canvas
with the feet on one baseline and the body centred. Output strips + Mage_frames.json (same contract as the Unity importer).
The sheet is the owner's own design reference; only the character frames are used, never the whole image.
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from mage_rig import write_png  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CLS = next((a for a in sys.argv[1:] if not a.startswith("--")), "Mage")
SHEET = None
CELL = 128
BASELINE = 122
FRAME_M = 1.16
OUT = None

# frames whose neighbours overlap (airborne poses, ladder, glowing circles) are cut by hand: (centre x, top, bottom, half width)
MANUAL = {
    "Death": [(1122, 628, 738, 54), (1238, 628, 738, 56), (1372, 640, 738, 62)],
    "Jump": [(494, 255, 362, 46), (585, 252, 362, 52), (668, 214, 312, 52), (770, 212, 308, 52), (878, 262, 364, 52)],
    "Climb": [(1267, 259, 360, 56), (1365, 240, 347, 56), (1465, 228, 338, 56)],
    "Skill": [(488, 612, 738, 58), (610, 612, 738, 58), (720, 612, 738, 58), (842, 612, 738, 58), (975, 606, 740, 62)],
}

# clip -> (panel x0,y0,x1,y1, expected frames, fps, loop)
PANELS = {
    "Idle": ((425, 48, 988, 176), 6, 8, True),
    "Run": ((1010, 48, 1522, 176), 5, 12, True),
    "Jump": ((425, 214, 928, 368), 5, 10, False),
    "Fall": ((950, 234, 1188, 368), 2, 8, False),
    "Climb": ((1210, 232, 1522, 372), 3, 8, True),
    "Attack": ((425, 425, 1070, 552), 5, 12, False),
    "Hit": ((1092, 425, 1522, 552), 4, 12, False),
    "Skill": ((425, 608, 1052, 740), 5, 10, False),
    "Death": ((1075, 608, 1522, 740), 3, 6, False),
}


def configure(cls):
    """Pick the sheet + panel layout of a class (the owner's design sheets share the layout but not the exact pixel positions)."""
    global SHEET, OUT, PANELS, MANUAL
    name = "sheet.npy" if cls == "Mage" else f"sheet_{cls}.npy"
    SHEET = np.load(os.path.join(ROOT, "ArtSource", "_render_tmp", name))
    OUT = os.path.join(ROOT, "ArtSource", "Chibi2D", cls)
    PANELS, MANUAL = CONFIGS[cls]


def value(a):
    return a[..., :3].max(-1)


def sat(a):
    mx, mn = a[..., :3].max(-1), a[..., :3].min(-1)
    return (mx - mn) / np.maximum(mx, 1e-4)


def shift(m, dy, dx):
    out = np.zeros_like(m)
    h, w = m.shape
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = m[ys, xs]
    return out


def dilate4(m):
    return m | shift(m, 1, 0) | shift(m, -1, 0) | shift(m, 0, 1) | shift(m, 0, -1)


def frames_in(panel, expected):
    x0, y0, x1, y1 = panel
    a = SHEET[y0:y1, x0:x1]
    fg = (value(a) >= 0.27) | ((sat(a) > 0.5) & (value(a) > 0.2))
    cols = fg.sum(0) > 0
    segs, start, gap = [], None, 0
    for x, on in enumerate(cols):
        if on:
            if start is None:
                start = x
            gap = 0
        elif start is not None:
            gap += 1
            if gap >= 6:
                segs.append((start, x - gap))
                start = None
    if start is not None:
        segs.append((start, len(cols) - 1))
    if len(segs) != expected:                               # frames touch (capes, swooshes): split the span at the emptiest columns
        prof = np.convolve(fg.sum(0).astype(np.float32), np.ones(7, np.float32) / 7, mode="same")
        on = np.where(cols)[0]
        xa, xb = on.min(), on.max()
        cuts = [xa]
        for k in range(1, expected):
            ideal = int(xa + (xb - xa + 1) * k / expected)
            lo, hi = max(cuts[-1] + 30, ideal - 22), min(xb - 30, ideal + 22)
            cuts.append(int(lo + np.argmin(prof[lo:hi + 1])) if hi > lo else ideal)
        cuts.append(xb + 1)
        segs = [(cuts[i], cuts[i + 1] - 1) for i in range(expected)]
    boxes = []
    for s0, s1 in segs:
        rows = np.where(fg[:, s0:s1 + 1].sum(1) > 0)[0]
        if s1 - s0 < 30 or len(rows) == 0 or rows[-1] - rows[0] < 45:
            continue                                   # label plates and stray sparkles
        boxes.append((x0 + s0, y0 + rows[0], x0 + s1 + 1, y0 + rows[-1] + 1))
    return boxes


def cutout(box, ladder=False):
    x0, y0, x1, y1 = box
    pad = 3
    a = SHEET[max(0, y0 - pad):y1 + pad, max(0, x0 - pad):x1 + pad].copy()
    # the sheet's panel border runs along the crop as a thin light-violet line (full width / height): erase it first
    lc = (value(a) > 0.33) & (value(a) < 0.85) & (sat(a) > 0.22) & (sat(a) < 0.7) & (a[..., 2] > a[..., 0]) & (a[..., 0] > a[..., 1] * 0.95)
    for yy in range(lc.shape[0]):
        if lc[yy].mean() > 0.5:
            a[yy, :, :3] = (0.06, 0.04, 0.13)
    for xx in range(lc.shape[1]):
        if lc[:, xx].mean() > 0.5:
            a[:, xx, :3] = (0.06, 0.04, 0.13)
    v = value(a)
    vs = (v + shift(v, 1, 0) + shift(v, -1, 0) + shift(v, 0, 1) + shift(v, 0, -1)) / 5       # light blur: ignore the JPEG noise
    # the dark violet panel is almost as dark as the robe, so do not try to tell them apart pixel by pixel:
    # keep everything within 4 px of a light pixel, fill the enclosed dark parts (robe, boots), drop the rest, shave 2 px
    fg = vs >= 0.235
    grow = fg
    for _ in range(3):
        grow = dilate4(grow)
    outside = np.zeros_like(grow)
    outside[0, :] = outside[-1, :] = outside[:, 0] = outside[:, -1] = True
    outside &= ~grow
    for _ in range(600):
        nxt = dilate4(outside) & ~grow
        if (nxt == outside).all():
            break
        outside = nxt
    alpha = ~outside
    shave = ~alpha
    for _ in range(2):
        shave = dilate4(shave)
    alpha = ~shave
    if ladder:                                         # brown wood rungs behind the character
        r, g, b = a[..., 0], a[..., 1], a[..., 2]
        wood = (r > g * 1.18) & (g > b * 1.15) & (r > 0.38) & (r < 0.85) & (sat(a) > 0.35) & (sat(a) < 0.75)
        alpha &= ~wood
    # keep the biggest blob, drop specks
    return a[..., :3], alpha


def strip_lines(alpha, min_run=26, thick=3):
    """Removes the leftovers of the sheet's panel borders: long, thin straight runs of pixels (<= thick px) in either direction."""
    out = alpha.copy()
    for transpose in (False, True):
        a = out.T.copy() if transpose else out
        h, w = a.shape
        for y in range(h):
            x = 0
            while x < w:
                if not a[y, x]:
                    x += 1
                    continue
                x0 = x
                while x < w and a[y, x]:
                    x += 1
                if x - x0 < min_run:
                    continue
                above = a[max(0, y - thick - 1):max(0, y - 1), x0:x]
                below = a[min(h, y + 2):min(h, y + thick + 2), x0:x]
                if (above.size == 0 or above.mean() < 0.2) and (below.size == 0 or below.mean() < 0.2):
                    a[y, x0:x] = False
        out = a.T.copy() if transpose else a
    return out


def keep_main(alpha):
    lab = np.zeros(alpha.shape, np.int32)
    n = 0
    sizes = {}
    for y, x in zip(*np.where(alpha)):
        if lab[y, x]:
            continue
        n += 1
        stack = [(y, x)]
        lab[y, x] = n
        cnt = 0
        while stack:
            cy, cx = stack.pop()
            cnt += 1
            for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                if 0 <= ny < alpha.shape[0] and 0 <= nx < alpha.shape[1] and alpha[ny, nx] and not lab[ny, nx]:
                    lab[ny, nx] = n
                    stack.append((ny, nx))
        sizes[n] = cnt
    if not sizes:
        return alpha
    keep = [k for k, v in sizes.items() if v >= max(120, 0.30 * max(sizes.values()))]
    return np.isin(lab, keep)


def kmeans(px, k=40, it=12, seed=3):
    rng = np.random.default_rng(seed)
    cen = px[rng.choice(len(px), k, replace=False)]
    for _ in range(it):
        d = ((px[:, None, :] - cen[None]) ** 2).sum(-1)
        idx = d.argmin(1)
        for j in range(k):
            m = idx == j
            if m.any():
                cen[j] = px[m].mean(0)
    return cen


def _panels(rows):
    """rows: {clip: (x0, y0, x1, y1)} -> the PANELS structure (expected frames, fps, loop are fixed by clip)."""
    spec = {"Idle": (6, 8, True), "Run": (5, 12, True), "Jump": (5, 10, False), "Fall": (2, 8, False), "Climb": (3, 8, True),
            "Attack": (5, 12, False), "Hit": (4, 12, False), "Skill": (5, 10, False), "Death": (3, 6, False)}
    return {c: (rows[c],) + spec[c] for c in spec}


def _manual(jump, climb, skill, death=None, hw=(50, 56, 58), idle=None, attack=None):
    """Frames whose neighbours overlap are cut by hand: lists of (centre x, top, bottom)."""
    out = {"Jump": [(cx, t, b, hw[0]) for cx, t, b in jump], "Climb": [(cx, t, b, hw[1]) for cx, t, b in climb],
           "Skill": [(cx, t, b, hw[2]) for cx, t, b in skill]}
    if death:
        out["Death"] = [(cx, t, b, h) for cx, t, b, h in death]
    if idle:
        out["Idle"] = [(cx, t, b, 46) for cx, t, b in idle]
    if attack:
        out["Attack"] = [(cx, t, b, 60) for cx, t, b in attack]
    return out


CONFIGS = {
    "Mage": (PANELS, MANUAL),
    "Swordsman": (_panels({
        "Idle": (400, 45, 932, 168), "Run": (950, 45, 1436, 168), "Jump": (402, 205, 876, 348), "Fall": (891, 220, 1120, 348),
        "Climb": (1137, 218, 1436, 350), "Attack": (400, 404, 1008, 518), "Hit": (1024, 385, 1436, 518), "Skill": (400, 560, 996, 698),
        "Death": (1012, 560, 1436, 698)}),
        _manual([(455, 255, 340), (553, 238, 325), (640, 210, 292), (726, 208, 290), (818, 255, 338)],
                [(1185, 252, 348), (1285, 214, 345), (1380, 214, 345)],
                [(450, 575, 690), (557, 575, 690), (668, 570, 690), (790, 568, 690), (920, 560, 692)],
                [(1062, 600, 690, 48), (1160, 610, 690, 50), (1270, 640, 690, 56)])),
    "Archer": (_panels({
        "Idle": (400, 45, 932, 166), "Run": (950, 45, 1436, 166), "Jump": (402, 203, 876, 346), "Fall": (895, 220, 1118, 346),
        "Climb": (1139, 218, 1436, 348), "Attack": (400, 402, 1010, 520), "Hit": (1028, 383, 1436, 520), "Skill": (400, 560, 996, 698),
        "Death": (1014, 560, 1436, 698)}),
        _manual([(453, 255, 340), (540, 240, 322), (630, 212, 295), (722, 210, 292), (825, 258, 342)],
                [(1185, 250, 346), (1280, 214, 344), (1378, 214, 344)],
                [(450, 575, 692), (560, 575, 692), (675, 572, 692), (785, 570, 692), (915, 565, 694)],
                [(1058, 600, 690, 48), (1160, 610, 690, 52), (1265, 640, 690, 58)],
                idle=[(440, 55, 166), (527, 55, 166), (617, 55, 166), (705, 55, 166), (795, 55, 166), (885, 55, 166)],
                attack=[(462, 404, 518), (575, 404, 518), (690, 404, 518), (805, 404, 518), (930, 404, 518)])),
    "Assassin": (_panels({
        "Idle": (400, 58, 932, 182), "Run": (950, 58, 1436, 182), "Jump": (402, 222, 876, 362), "Fall": (893, 236, 1120, 362),
        "Climb": (1139, 236, 1436, 364), "Attack": (400, 418, 1008, 528), "Hit": (1028, 402, 1436, 528), "Skill": (400, 568, 994, 708),
        "Death": (1012, 568, 1436, 708)}),
        _manual([(462, 270, 352), (548, 258, 340), (632, 230, 312), (718, 225, 308), (815, 268, 352)],
                [(1190, 262, 362), (1285, 228, 360), (1380, 228, 360)],
                [(455, 580, 695), (560, 580, 695), (670, 575, 695), (785, 575, 695), (915, 570, 697)],
                [(1058, 612, 700, 48), (1162, 620, 700, 52), (1270, 650, 700, 58)])),
}

def main():
    configure(CLS)
    items = []                                         # (clip, idx, rgb, alpha)
    counts = {}
    for clip, (panel, expected, fps, loop) in PANELS.items():
        boxes = frames_in(panel, expected) if clip not in MANUAL else [(cx - hw, y0_, cx + hw, y1_) for cx, y0_, y1_, hw in MANUAL[clip]]
        counts[clip] = len(boxes)
        print("PANEL", clip, "frames found", len(boxes), "expected", expected, flush=True)
        for i, b in enumerate(boxes[:expected]):
            rgb, alpha = cutout(b, ladder=(clip == "Climb"))
            items.append((clip, i, rgb, keep_main(strip_lines(alpha))))
    # one shared palette
    samples = np.concatenate([rgb[al] for _, _, rgb, al in items])
    samples = samples[np.random.default_rng(1).choice(len(samples), min(60000, len(samples)), replace=False)]
    pal = kmeans(samples.astype(np.float32), 40)
    ink = np.array([0.10, 0.05, 0.2], np.float32)
    frames = {}
    for clip, i, rgb, al in items:
        d = ((rgb.reshape(-1, 1, 3) - pal[None]) ** 2).sum(-1).argmin(1).reshape(rgb.shape[:2])
        q = pal[d]
        out_a = al | (dilate4(al) & False)
        ring = dilate4(al) & ~al                       # crisp 1 px outline round the silhouette
        img = np.zeros(rgb.shape[:2] + (4,), np.float32)
        img[..., :3] = np.where(al[..., None], q, ink)
        img[..., 3] = (al | ring).astype(np.float32)
        ys, xs = np.where(img[..., 3] > 0)
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        spr = img[y0:y1, x0:x1]
        h, w = spr.shape[:2]
        low = spr[int(h * 0.55):, :, 3] > 0
        cx = np.where(low.any(0))[0]
        body_cx = (cx.min() + cx.max()) / 2 if len(cx) else w / 2
        canvas = np.zeros((CELL, CELL, 4), np.float32)
        ox = int(round(CELL / 2 - body_cx))
        oy = BASELINE - h
        for yy in range(h):
            for xx in range(w):
                ty, tx = oy + yy, ox + xx
                if 0 <= ty < CELL and 0 <= tx < CELL and spr[yy, xx, 3] > 0:
                    canvas[ty, tx] = spr[yy, xx]
        frames.setdefault(clip, []).append(canvas)
    os.makedirs(OUT, exist_ok=True)
    meta = {"cell": CELL, "pixel": True, "frame_m": FRAME_M, "ground_from_bottom_m": round((CELL - BASELINE) / (CELL / FRAME_M), 4), "clips": {}}
    for clip, (panel, expected, fps, loop) in PANELS.items():
        fr = frames.get(clip, [])
        if not fr:
            continue
        write_png(os.path.join(OUT, f"{CLS}_{clip}.png"), np.concatenate(fr, axis=1))
        meta["clips"][clip] = {"frames": len(fr), "fps": fps, "loop": loop, "file": f"{CLS}_{clip}.png"}
    with open(os.path.join(OUT, f"{CLS}_frames.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2)
    # review sheet
    rows = []
    for clip in PANELS:
        fr = frames.get(clip, [])
        if fr:
            row = np.concatenate(fr + [np.zeros((CELL, CELL, 4), np.float32)] * (6 - len(fr)), axis=1)[:, :CELL * 6]
            rows.append(row)
    sheet = np.concatenate(rows, axis=0)
    bg = np.zeros(sheet.shape[:2] + (3,), np.float32) + np.array([0.82, 0.88, 0.8], np.float32)
    a = sheet[..., 3:4]
    prev = np.concatenate([sheet[..., :3] * a + bg * (1 - a), np.ones(sheet.shape[:2] + (1,), np.float32)], axis=2)
    write_png(os.path.join(OUT, f"{CLS}_preview.png"), np.repeat(np.repeat(prev, 3, 0), 3, 1))
    print("DONE", {k: len(v) for k, v in frames.items()})


if __name__ == "__main__":
    main()
