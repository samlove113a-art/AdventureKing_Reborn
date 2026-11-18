"""Turn an AI-generated sprite sheet (magenta background, grid of frames) into a Unity-ready strip.

Usage (Blender python + Pillow on PYTHONPATH):
  python process_pixel_strip.py <sheet.png> <out_dir> <Class> <Clip> <frames> [--whiten i] [--ref-scale S]
Output: <out_dir>/<Class>_<Clip>.png  (frames * 192 x 192, transparent, feet on one ground line)
        <out_dir>/<Class>_<Clip>_preview.png (checkerboard preview, 2x)
Steps: detect dark grid lines (per row) -> crop cells -> key magenta -> keep main components ->
scale = 136 / cell height (the generator draws the hero at ~92% of the cell, so every clip gets the same
hero size ~125 px) -> premultiplied downscale -> alpha threshold -> place with CELL-RELATIVE coordinates
(median foot line / median body centroid across frames) so poses keep their motion and fx do not move the feet.
--whiten i : replace frame i by a white-flash copy of frame i-1 (used for Hit).
"""
import os
import sys

import numpy as np
from PIL import Image

CELL = 192
GROUND_PX = 10  # ground_from_bottom_m 0.099 / frame_m 1.9 * 192
TARGET = 136.0


def groups(idx):
    out = []
    for i in idx:
        if out and i - out[-1][-1] <= 2:
            out[-1].append(i)
        else:
            out.append([i])
    return [(g[0], g[-1]) for g in out]


def spans(lines, size, min_len=100):
    res = []
    cur = 0
    for l in lines:
        if l[0] - cur > min_len:
            res.append((cur, l[0]))
        cur = l[1] + 1
    if size - cur > min_len:
        res.append((cur, size))
    return res


def split_cells(img):
    a = np.asarray(img.convert("RGB")).astype(int)
    dark = (a.sum(axis=2) < 300)
    h, w = dark.shape
    ys = spans(groups(np.where(dark.mean(axis=1) > 0.9)[0]), h)
    cells = []
    for (y0, y1) in ys:
        row = dark[y0:y1]
        xs = spans(groups(np.where(row.mean(axis=0) > 0.9)[0]), w)
        for (x0, x1) in xs:
            cells.append((x0, y0, x1, y1))
    return cells


def single_hero(mask):
    """Keep the biggest blob plus small blobs touching its (slightly grown) box; drops a second hero the generator drew in the same cell."""
    h, w = mask.shape
    lab = np.zeros((h, w), np.int32)
    boxes = {}
    n = 0
    for y in range(h):
        for x in range(w):
            if mask[y, x] and lab[y, x] == 0:
                n += 1
                stack = [(y, x)]
                lab[y, x] = n
                x0 = x1 = x
                y0 = y1 = y
                cnt = 0
                while stack:
                    cy, cx = stack.pop()
                    cnt += 1
                    x0, x1, y0, y1 = min(x0, cx), max(x1, cx), min(y0, cy), max(y1, cy)
                    for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                        if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and lab[ny, nx] == 0:
                            lab[ny, nx] = n
                            stack.append((ny, nx))
                boxes[n] = [cnt, x0, y0, x1, y1]
    if not boxes:
        return mask
    big = max(boxes, key=lambda k: boxes[k][0])
    _, bx0, by0, bx1, by1 = boxes[big]
    keep = np.zeros_like(mask)
    for k, (cnt, x0, y0, x1, y1) in boxes.items():
        if k == big or (x1 >= bx0 - 6 and x0 <= bx1 + 6 and y1 >= by0 - 6 and y0 <= by1 + 6 and cnt < boxes[big][0] * 0.3):
            keep |= lab == k
    return keep


def key_magenta(rgb):
    r, g, b = rgb[..., 0].astype(int), rgb[..., 1].astype(int), rgb[..., 2].astype(int)
    bg = (r > 120) & (b > 120) & (g < np.minimum(r, b) * 0.62) & (abs(r - b) < 90)
    fg = ~bg
    pad = np.pad(fg, 1)
    er = fg.copy()
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            er &= pad[1 + dy:1 + dy + fg.shape[0], 1 + dx:1 + dx + fg.shape[1]]
    return er


def remove_ground_lines(c, fg):
    dark = c.astype(int).sum(axis=2) < 90
    out = fg.copy()
    h, w = fg.shape
    for y in range(h):
        row = dark[y] & fg[y]
        run = 0
        for x in range(w + 1):
            if x < w and row[x]:
                run += 1
            else:
                if run >= 70:
                    out[y, x - run:x] = False
                run = 0
    return out


def largest_components(mask, min_area=150):
    h, w = mask.shape
    lab = np.zeros((h, w), np.int32)
    n = 0
    areas = {}
    for y in range(h):
        for x in range(w):
            if mask[y, x] and lab[y, x] == 0:
                n += 1
                stack = [(y, x)]
                lab[y, x] = n
                cnt = 0
                while stack:
                    cy, cx = stack.pop()
                    cnt += 1
                    for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                        if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and lab[ny, nx] == 0:
                            lab[ny, nx] = n
                            stack.append((ny, nx))
                areas[n] = cnt
    keep = np.zeros_like(mask)
    for k, v in areas.items():
        if v >= min_area:
            keep |= lab == k
    return keep


def scale_tile(c, m, b, scale):
    x0, y0, x1, y1 = b
    rgb = c[y0:y1, x0:x1].astype(np.float32)
    a = m[y0:y1, x0:x1].astype(np.float32)
    pm = np.dstack([rgb * a[..., None], a])
    tw, th = max(1, round((x1 - x0) * scale)), max(1, round((y1 - y0) * scale))
    chans = [Image.fromarray(pm[..., k]).resize((tw, th), Image.BOX) for k in range(4)]
    arr = np.dstack([np.asarray(ch) for ch in chans])
    al = arr[..., 3]
    col = np.where(al[..., None] > 0.01, arr[..., :3] / np.maximum(al[..., None], 0.01), 0)
    mask = al > 0.5
    out = np.zeros((th, tw, 4), np.uint8)
    out[..., :3] = np.clip(col, 0, 255).astype(np.uint8)
    out[..., 3] = np.where(mask, 255, 0)
    out[~mask, :3] = 0
    return out


def main():
    argv = sys.argv
    args = [a for i, a in enumerate(argv[1:], 1) if not a.startswith("--") and not argv[i - 1] in ("--whiten", "--ref-scale", "--copy", "--target", "--keep")]
    sheet, out_dir, cls, clip, frames = args[0], args[1], args[2], args[3], int(args[4])
    whiten = int(argv[argv.index("--whiten") + 1]) if "--whiten" in argv else -1
    img = Image.open(sheet).convert("RGB")
    cells = split_cells(img)
    if len(cells) != frames:
        sys.exit(f"found {len(cells)} cells, expected {frames}: {cells}")
    target = float(argv[argv.index("--target") + 1]) if "--target" in argv else TARGET
    scale = target / float(np.median([c[3] - c[1] - 6 for c in cells]))
    if "--fit-first" in argv:   # monsters: every clip gets the same body size, measured on its first (standing) frame
        x0, y0, x1, y1 = cells[0]
        c0 = np.asarray(img.crop((x0 + 3, y0 + 3, x1 - 3, y1 - 3)))
        m0 = single_hero(largest_components(key_magenta(c0), min_area=150))
        ys0, _ = np.where(m0)
        scale = target / float(ys0.max() - ys0.min() + 1)
    if "--ref-scale" in argv:
        scale = float(argv[argv.index("--ref-scale") + 1])
    print("scale", round(scale, 4))

    for _try in range(3):   # shrink the whole clip when a pose (lying death, long thrust) does not fit one cell
        items = []
        for i, (x0, y0, x1, y1) in enumerate(cells):
            if i == whiten:
                items.append(None)
                continue
            c = np.asarray(img.crop((x0 + 3, y0 + 3, x1 - 3, y1 - 3)))
            k = key_magenta(c)
            if '--deline' in argv:
                k = remove_ground_lines(c, k)
            m = largest_components(k, min_area=150)
            if '--single' in argv:
                m = single_hero(m)
            if '--hero-only' in argv:
                m = largest_components(m, min_area=int(m.sum() * 0.35))
            ys, xs = np.where(m)
            if len(ys) == 0:
                print(f"empty cell {i} dropped")
                continue
            tile = scale_tile(c, m, (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1), scale)
            lo = ys >= ys.max() - 0.3 * (ys.max() - ys.min())
            cxb = float(xs[lo].mean()) if '--anchor-body' in argv else float(xs.mean())
            items.append(dict(tile=tile, bx0=xs.min() * scale, bottom=(ys.max() + 1) * scale, top=ys.min() * scale,
                              cx=cxb * scale))
        real = [it for it in items if it]
        base = float(np.median([it["bottom"] for it in real]))
        anchor = float(np.median([it["cx"] for it in real]))
    
        def place(it):
            h, w = it["tile"].shape[:2]
            a = it["cx"] if '--anchor-body' in argv else anchor
            return CELL // 2 - 20 * ('--anchor-body' in argv) + (it["bx0"] - a), (CELL - GROUND_PX) - (base - it["top"]), w, h
    
        # uniform shift so every frame fits inside the cell
        minx = min(place(it)[0] for it in real)
        maxx = max(place(it)[0] + place(it)[2] for it in real)
        miny = min(place(it)[1] for it in real)
        maxy = max(place(it)[1] + place(it)[3] for it in real)
        sx = 0.0
        if minx < 0:
            sx = -minx
        elif maxx > CELL:
            sx = CELL - maxx
        sy = 0.0
        if miny < 0:
            sy = -miny
        elif maxy > CELL:
            sy = CELL - maxy
        print("fit", round(minx, 1), round(maxx, 1), round(miny, 1), round(maxy, 1), "shift", round(sx, 1), round(sy, 1))
        span_x, span_y = maxx - minx, maxy - miny
        if max(span_x, span_y) > CELL - 2 and "--ref-scale" not in argv:
            scale *= (CELL - 2) / max(span_x, span_y)
            print("too wide, rescale", round(scale, 4))
            continue
        break

    if '--keep' in argv:   # keep only these frames, in this order (a sheet whose last frames are junk)
        items = [items[int(v)] for v in argv[argv.index('--keep') + 1].split(',')]
    frames = len(items)   # empty cells were dropped
    strip = Image.new("RGBA", (CELL * frames, CELL), (0, 0, 0, 0))
    for i, it in enumerate(items):
        if it is None:
            prev = items[i - 1]
            t = prev["tile"].copy()
            a = t[..., 3] > 0
            t[a, :3] = (t[a, :3] * 0.15 + 255 * 0.85).astype(np.uint8)
            it = dict(prev, tile=t)
        px, py, w, h = place(it)
        strip.alpha_composite(Image.fromarray(it["tile"], "RGBA"), (int(round(px + sx)) + i * CELL, int(round(py + sy))))

    if '--copy' in argv:  # dst:src, replace a frame that lost the hero (e.g. a fx-only frame) by another one
        d, s = [int(v) for v in argv[argv.index('--copy') + 1].split(':')]
        tile = strip.crop((s * CELL, 0, (s + 1) * CELL, CELL))
        strip.paste((0, 0, 0, 0), (d * CELL, 0, (d + 1) * CELL, CELL))
        strip.alpha_composite(tile, (d * CELL, 0))
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, f"{cls}_{clip}.png")
    strip.save(path)
    z = 2
    cb = Image.new("RGBA", (strip.width * z, strip.height * z))
    pxs = cb.load()
    for y in range(cb.height):
        for x in range(cb.width):
            pxs[x, y] = (70, 70, 80, 255) if ((x // 16 + y // 16) & 1) else (110, 110, 120, 255)
    cb.alpha_composite(strip.resize((strip.width * z, strip.height * z), Image.NEAREST))
    cb.convert("RGB").save(os.path.join(out_dir, f"{cls}_{clip}_preview.png"))
    with open(os.path.join(out_dir, f"{cls}_{clip}.count"), "w") as cf:
        cf.write(str(frames))
    print("wrote", path, strip.size)


if __name__ == "__main__":
    main()







