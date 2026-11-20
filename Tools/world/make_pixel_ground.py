"""Pixel-art Dark Fantasy ground tiles for the side-scroller (same 101 px/metre as the heroes), one palette per Chapter 1 map.
Both tiles wrap horizontally (the earth one vertically too) so a SpriteRenderer in Tiled mode can repeat them.

  python make_pixel_ground.py   ->  Assets/Art/World/PixelGround/ground_top_mNN.png (256x64)  ground_earth_mNN.png (256x128)   NN = 01..10
Map 1 also writes the unsuffixed ground_top.png / ground_earth.png (the first pilot). Needs numpy + Pillow (Blender's python, PYTHONPATH to Pillow).
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "World", "PixelGround")
C = 2  # px per art cell


def h(x, a=255):
    x = x.lstrip("#")
    return (int(x[0:2], 16), int(x[2:4], 16), int(x[4:6], 16), a)


OUTLINE = h("#0d1218")

# surface = 5 tones dark->light of the top layer; dirt = 4 tones of the earth; stone = 4 tones of embedded stones; kind: grass | slab
THEMES = {
    1: dict(kind="grass", surface=["#173a2c", "#1f5a3a", "#2f8147", "#5fb35a", "#a6e07a"], dirt=["#2a1d24", "#3d2a2e", "#523a37", "#6b4b40"], stone=["#2b2f3a", "#444b5a", "#6a7385", "#9aa3b5"]),
    2: dict(kind="grass", surface=["#1d4a2a", "#2b7a34", "#4aa845", "#86d35a", "#d3f58a"], dirt=["#33241f", "#4b352b", "#654a38", "#82603f"], stone=["#3a3f4a", "#5a6270", "#8590a0", "#b8c2d2"]),
    3: dict(kind="grass", surface=["#0f3a3a", "#126a5e", "#1fa58a", "#52dcb0", "#b8ffe0"], dirt=["#1f1a2e", "#2c2440", "#3d3258", "#52447a"], stone=["#262c44", "#3d4768", "#5f6fa0", "#8fa6d8"]),
    4: dict(kind="grass", surface=["#2b4a2c", "#3f7a3a", "#6fae55", "#b8dc7a", "#f5ffb0"], dirt=["#3a2a2c", "#563e40", "#74524f", "#966a5e"], stone=["#4a3e4c", "#6e5f72", "#9c8aa0", "#d0bfd6"]),
    5: dict(kind="slab", surface=["#14141f", "#242638", "#3a3e58", "#5a6188", "#8d9be0"], dirt=["#10101a", "#1a1a28", "#272a40", "#383d5c"], stone=["#0e1220", "#1d2b48", "#2f5a8a", "#58c8ff"]),
    6: dict(kind="grass", surface=["#0f3340", "#16606a", "#28969a", "#5fd0c0", "#b0fff0"], dirt=["#1a2a34", "#26404c", "#375a68", "#4c7a8a"], stone=["#1e3a3a", "#2f6a60", "#4fa58a", "#8ee0b8"]),
    7: dict(kind="grass", surface=["#14102a", "#241a46", "#3a2a6a", "#5b3fa0", "#9a78e0"], dirt=["#120e1a", "#1c1428", "#2a1e3c", "#3a2a52"], stone=["#1a1626", "#2c2540", "#4a3e70", "#7a68b0"]),
    8: dict(kind="grass", surface=["#232a10", "#3a4a14", "#617a1c", "#9ab82a", "#d6f04a"], dirt=["#1e1a12", "#2f291a", "#46402a", "#625a3a"], stone=["#2a3020", "#434c30", "#6a7648", "#9aaa66"]),
    9: dict(kind="slab", surface=["#262a2a", "#3e4442", "#5c6560", "#8a9488", "#bcc8b4"], dirt=["#1c1e1e", "#2a2e2c", "#3c423e", "#545c54"], stone=["#202426", "#363c3e", "#566064", "#8a989c"]),
    10: dict(kind="slab", surface=["#1a0c14", "#35121f", "#5a1c2c", "#8a2c3c", "#d8a64a"], dirt=["#0e0810", "#1a0e18", "#2a1524", "#3e1e34"], stone=["#1c1218", "#33202c", "#583a48", "#8a5a6c"]),
}
ROOT_C = h("#2a1a14")


def pal(lst):
    return [h(c) for c in lst]


def to_img(a):
    return Image.fromarray(a, "RGBA").resize((a.shape[1] * C, a.shape[0] * C), Image.NEAREST)


def periodic_noise(cw, ch, seed, scale):
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


def earth(t, seed, cw=128, ch=64):
    DIRT, STONE = pal(t["dirt"]), pal(t["stone"])
    a = np.zeros((ch, cw, 4), np.uint8)
    n = periodic_noise(cw, ch, 3 + seed, 8)
    n2 = periodic_noise(cw, ch, 9 + seed, 3)
    for y in range(ch):
        for x in range(cw):
            band = (y // 14 + int(n[y, x] * 2.2)) % 4
            v = min(3, max(0, band + (1 if n2[y, x] > 0.72 else 0) - (1 if n2[y, x] < 0.18 else 0)))
            a[y, x] = DIRT[v]
    rng = np.random.RandomState(21 + seed)
    for _ in range(7):
        cx, cy = rng.randint(0, cw), rng.randint(4, ch - 4)
        rx, ry = rng.randint(3, 6), rng.randint(2, 4)
        for dy in range(-ry - 1, ry + 2):
            for dx in range(-rx - 1, rx + 2):
                d = (dx / (rx + 0.5)) ** 2 + (dy / (ry + 0.5)) ** 2
                x, y = (cx + dx) % cw, (cy + dy) % ch
                if d <= 1.0:
                    tone = 2 if (dy < -ry * 0.3 and dx < rx * 0.4) else (0 if dy > ry * 0.4 else 1)
                    a[y, x] = STONE[min(3, tone + (1 if dy < -ry * 0.6 else 0))]
                elif d <= 1.45:
                    a[y, x] = OUTLINE
    for _ in range(4):
        x, y = rng.randint(0, cw), rng.randint(0, ch)
        for k in range(rng.randint(6, 12)):
            a[(y + k) % ch, (x + int(np.sin(k * 0.9) * 2)) % cw] = ROOT_C
    return a


def top_grass(t, seed, cw=128, ch=32):
    GRASS, DIRT = pal(t["surface"]), pal(t["dirt"])
    a = np.zeros((ch, cw, 4), np.uint8)
    rng = np.random.RandomState(5 + seed)
    base = 9
    blade_h = np.zeros(cw, int)
    x = 0
    while x < cw:
        w = rng.randint(1, 3)
        hh = rng.randint(2, 7)
        for k in range(w):
            blade_h[(x + k) % cw] = max(blade_h[(x + k) % cw], hh - k)
        x += w + rng.randint(0, 3)
    n = periodic_noise(cw, 1, 4 + seed, 5)[0]
    lip = np.array([18 + int(v * 9) for v in n])
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
        a[lip[x] + 1, x] = OUTLINE
        if top_y - 1 >= 0 and blade_h[x] > 0:
            a[top_y - 1, x] = OUTLINE
    for x in range(cw):
        for y in range(lip[x] + 2, ch):
            a[y, x] = DIRT[1] if (x * 3 + y) % 5 else DIRT[2]
    return a


def top_slab(t, seed, cw=128, ch=32):
    """stone floor: a flat top face of paving slabs (cracks, moss, a few glowing bits), a dark course below, ragged bottom edge"""
    S, DIRT = pal(t["surface"]), pal(t["dirt"])
    a = np.zeros((ch, cw, 4), np.uint8)
    rng = np.random.RandomState(7 + seed)
    top_y = 9
    n = periodic_noise(cw, 1, 6 + seed, 6)[0]
    bottom = np.array([20 + int(v * 6) for v in n])
    slab = 16
    for x in range(cw):
        for y in range(top_y, bottom[x] + 1):
            k = y - top_y
            if k == 0:
                c = OUTLINE
            elif k == 1:
                c = S[4]
            elif k < 6:
                c = S[3] if ((x // slab) % 2 == 0) else S[2]
                if (x % slab) == 0:
                    c = OUTLINE                                   # joint between slabs
            elif k < 10:
                c = S[2] if ((x // slab) % 2 == 0) else S[1]
                if (x % slab) == 0:
                    c = OUTLINE
            else:
                c = S[1] if (x + y) % 6 else S[0]
            a[y, x] = c
        a[bottom[x] + 1, x] = OUTLINE
    for _ in range(6):                                           # hairline cracks and a speck of moss / glow on the lit face
        x = rng.randint(0, cw)
        for k in range(rng.randint(3, 7)):
            a[top_y + 3 + k % 5, (x + k) % cw] = S[0]
        a[top_y + 1, rng.randint(0, cw)] = h("#4aa860") if t["kind"] == "slab" and rng.rand() < 0.5 else S[4]
    for x in range(cw):
        for y in range(bottom[x] + 2, ch):
            a[y, x] = DIRT[1] if (x * 3 + y) % 5 else DIRT[2]
    return a


def main():
    os.makedirs(OUT, exist_ok=True)
    for idx, t in THEMES.items():
        topf = top_grass if t["kind"] == "grass" else top_slab
        ti, ei = to_img(topf(t, idx)), to_img(earth(t, idx))
        ti.save(os.path.join(OUT, f"ground_top_m{idx:02d}.png"))
        ei.save(os.path.join(OUT, f"ground_earth_m{idx:02d}.png"))
        if idx == 1:
            ti.save(os.path.join(OUT, "ground_top.png"))
            ei.save(os.path.join(OUT, "ground_earth.png"))
        print("wrote theme", idx, t["kind"])


if __name__ == "__main__":
    main()
