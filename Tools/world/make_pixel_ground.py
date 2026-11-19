"""Pixel-art Dark Fantasy ground tiles for the side-scroller (same 101 px/metre as the heroes). Both tiles wrap horizontally (and the earth
one vertically), so they can be tiled by a SpriteRenderer in Tiled draw mode.

  python make_pixel_ground.py   ->  Assets/Art/World/PixelGround/ground_top.png (256x64)  ground_earth.png (256x128)
Needs numpy + Pillow (Blender's python with PYTHONPATH to Pillow).
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "World", "PixelGround")
C = 2  # px per art cell (coarse pixels, same chunkiness as the sprites)


def h(x, a=255):
    x = x.lstrip("#")
    return (int(x[0:2], 16), int(x[2:4], 16), int(x[4:6], 16), a)


OUTLINE = h("#0d1218")
GRASS = [h("#173a2c"), h("#1f5a3a"), h("#2f8147"), h("#5fb35a"), h("#a6e07a")]   # dark -> light
DIRT = [h("#2a1d24"), h("#3d2a2e"), h("#523a37"), h("#6b4b40")]
STONE = [h("#2b2f3a"), h("#444b5a"), h("#6a7385"), h("#9aa3b5")]
ROOT_C = h("#2a1a14")
MOSS = h("#2f6b3e")


def to_img(a):
    return Image.fromarray(a, "RGBA").resize((a.shape[1] * C, a.shape[0] * C), Image.NEAREST)


def periodic_noise(cw, ch, seed, scale):
    """value noise that wraps in both axes"""
    rng = np.random.RandomState(seed)
    gw, gh = max(2, cw // scale), max(2, ch // scale)
    grid = rng.rand(gh, gw)
    out = np.zeros((ch, cw))
    for y in range(ch):
        for x in range(cw):
            fx, fy = x / scale, y / scale
            x0, y0 = int(fx) % gw, int(fy) % gh
            x1, y1 = (x0 + 1) % gw, (y0 + 1) % gh
            tx, ty = fx - int(fx), fy - int(fy)
            tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
            out[y, x] = (grid[y0, x0] * (1 - tx) + grid[y0, x1] * tx) * (1 - ty) + (grid[y1, x0] * (1 - tx) + grid[y1, x1] * tx) * ty
    return out


def earth(cw=128, ch=64):
    a = np.zeros((ch, cw, 4), np.uint8)
    n = periodic_noise(cw, ch, 3, 8)
    n2 = periodic_noise(cw, ch, 9, 3)
    for y in range(ch):
        for x in range(cw):
            band = (y // 14 + int(n[y, x] * 2.2)) % 4          # soft strata
            v = min(3, max(0, band - 0 + (1 if n2[y, x] > 0.72 else 0) - (1 if n2[y, x] < 0.18 else 0)))
            a[y, x] = DIRT[v]
    rng = np.random.RandomState(21)
    # stones: rounded blobs with a light top edge and a dark outline, placed with wrap-around
    for _ in range(7):
        cx, cy = rng.randint(0, cw), rng.randint(4, ch - 4)
        rx, ry = rng.randint(3, 6), rng.randint(2, 4)
        for dy in range(-ry - 1, ry + 2):
            for dx in range(-rx - 1, rx + 2):
                d = (dx / (rx + 0.5)) ** 2 + (dy / (ry + 0.5)) ** 2
                x, y = (cx + dx) % cw, (cy + dy) % ch
                if d <= 1.0:
                    tone = 2 if (dy < -ry * 0.3 and dx < rx * 0.4) else (0 if dy > ry * 0.4 else 1)
                    a[y, x] = STONE[tone + (1 if dy < -ry * 0.6 else 0)]
                elif d <= 1.45:
                    a[y, x] = OUTLINE
    # a few dark roots / cracks
    for _ in range(4):
        x, y = rng.randint(0, cw), rng.randint(0, ch)
        for k in range(rng.randint(6, 12)):
            a[(y + k) % ch, (x + int(np.sin(k * 0.9) * 2)) % cw] = ROOT_C
    return a


def top(cw=128, ch=32):
    """grass lip: transparent above, bright blades on top, grass body, drooping lip with an irregular dripping bottom edge"""
    a = np.zeros((ch, cw, 4), np.uint8)
    rng = np.random.RandomState(5)
    base = 9                                           # grass surface (cells from the top) before the blades
    blade_h = np.zeros(cw, int)
    for x in range(cw):
        blade_h[x] = 0
    x = 0
    while x < cw:
        w = rng.randint(1, 3)
        hh = rng.randint(2, 7)
        for k in range(w):
            blade_h[(x + k) % cw] = max(blade_h[(x + k) % cw], hh - k)
        x += w + rng.randint(0, 3)
    lip = np.zeros(cw, int)
    n = periodic_noise(cw, 1, 4, 5)[0]
    for x in range(cw):
        lip[x] = 18 + int(n[x] * 9)                    # bottom edge of the grass lip
    for x in range(cw):
        top_y = base - blade_h[x]
        for y in range(top_y, lip[x] + 1):
            depth = y - top_y
            if y == top_y:
                c = GRASS[4]
            elif depth < 2:
                c = GRASS[3]
            elif y < base + 3:
                c = GRASS[2]
            elif y < lip[x] - 2:
                c = GRASS[1] if (x + y) % 7 else GRASS[2]
            else:
                c = GRASS[0]
            a[y, x] = c
        a[lip[x] + 1, x] = OUTLINE if lip[x] + 1 < ch else a[lip[x], x]
        if top_y - 1 >= 0:
            a[top_y - 1, x] = OUTLINE if blade_h[x] > 0 else a[top_y - 1, x]
    # moss/dirt peeking under the lip so it joins the earth tile
    for x in range(cw):
        for y in range(lip[x] + 2, ch):
            a[y, x] = DIRT[1] if (x * 3 + y) % 5 else DIRT[2]
    # clean any blade outline pixel that landed beside a taller neighbour (keeps silhouettes hard)
    return a


def main():
    os.makedirs(OUT, exist_ok=True)
    to_img(top()).save(os.path.join(OUT, "ground_top.png"))
    to_img(earth()).save(os.path.join(OUT, "ground_earth.png"))
    for n in ("ground_top", "ground_earth"):
        meta = os.path.join(OUT, n + ".png.meta")
        print("wrote", n, Image.open(os.path.join(OUT, n + ".png")).size)


if __name__ == "__main__":
    main()

