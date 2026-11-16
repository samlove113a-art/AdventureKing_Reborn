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
SHEET = np.load(os.path.join(ROOT, "ArtSource", "_render_tmp", "sheet.npy"))
CELL = 128
BASELINE = 122
FRAME_M = 1.16
OUT = os.path.join(ROOT, "ArtSource", "Chibi2D", "Mage")

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


def main():
    items = []                                         # (clip, idx, rgb, alpha)
    counts = {}
    for clip, (panel, expected, fps, loop) in PANELS.items():
        boxes = frames_in(panel, expected) if clip not in MANUAL else [(cx - hw, y0_, cx + hw, y1_) for cx, y0_, y1_, hw in MANUAL[clip]]
        counts[clip] = len(boxes)
        print("PANEL", clip, "frames found", len(boxes), "expected", expected, flush=True)
        for i, b in enumerate(boxes[:expected]):
            rgb, alpha = cutout(b, ladder=(clip == "Climb"))
            items.append((clip, i, rgb, keep_main(alpha)))
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
        write_png(os.path.join(OUT, f"Mage_{clip}.png"), np.concatenate(fr, axis=1))
        meta["clips"][clip] = {"frames": len(fr), "fps": fps, "loop": loop, "file": f"Mage_{clip}.png"}
    with open(os.path.join(OUT, "Mage_frames.json"), "w", encoding="utf-8") as f:
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
    write_png(os.path.join(OUT, "Mage_preview.png"), np.repeat(np.repeat(prev, 3, 0), 3, 1))
    print("DONE", {k: len(v) for k, v in frames.items()})


if __name__ == "__main__":
    main()
