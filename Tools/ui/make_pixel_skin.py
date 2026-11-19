"""Dark-fantasy PIXEL UI frames, drawn on a coarse grid (4 px cells) and scaled with nearest neighbour, so edges stay crisp and the 9-slice
borders (taken from each sprite's .meta) stretch only flat colour. Same file names / sizes / borders as the old smooth skin: no code change.

  python make_pixel_skin.py            # writes Assets/Art/UI/Skin/<name>.png and sets filterMode Point in the .meta
Needs numpy + Pillow (run with Blender's python and PYTHONPATH to Pillow).
"""
import os
import re
import sys

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SKIN = os.path.join(ROOT, "Assets", "Art", "UI", "Skin")
S = 4  # px per cell


def hexc(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


PAL = {
    "outline": hexc("#0b0810"),
    "iron_hi": hexc("#6b6178"), "iron": hexc("#463e53"), "iron_lo": hexc("#2a2433"),
    "gold_hi": hexc("#f3d98a"), "gold": hexc("#c9a24a"), "gold_lo": hexc("#7a5a22"),
    "steel_hi": hexc("#aab4c4"), "steel": hexc("#6f7a8e"), "steel_lo": hexc("#3b4252"),
}
FILLS = {
    "velvet": (hexc("#1a1226"), hexc("#221733")),
    "navy": (hexc("#101826"), hexc("#16202f")),
    "slate": (hexc("#1a1d24"), hexc("#21252e")),
    "slot": (hexc("#14101c"), hexc("#1b1626")),
}


def borders(name):
    m = open(os.path.join(SKIN, name + ".png.meta"), encoding="utf-8").read()
    mm = re.search(r"spriteBorder: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+), w: ([\d.]+)\}", m)
    return [int(float(v)) for v in mm.groups()]  # left, bottom, right, top (px)


def frame(w, h, fill, metal="iron", trim="gold", ornament="gems", header=0, inset=False, glow=None):
    """w,h in px. Returns an RGBA image. fill = key of FILLS."""
    cw, ch = w // S, h // S
    img = np.zeros((ch, cw, 4), np.uint8)
    f0, f1 = FILLS[fill]
    rng = np.random.RandomState(7)
    # inner fill with sparse dither
    for y in range(ch):
        for x in range(cw):
            img[y, x] = f1 if (rng.rand() < 0.06 or (x + y) % 11 == 0 and rng.rand() < 0.3) else f0
    P = PAL
    mhi, mid, mlo = P[metal + "_hi"], P[metal], P[metal + "_lo"]
    thi, tmid, tlo = P["gold_hi"], P["gold"], P["gold_lo"]
    if trim == "steel":
        thi, tmid, tlo = P["steel_hi"], P["steel"], P["steel_lo"]

    def px(x, y, c):
        if 0 <= x < cw and 0 <= y < ch:
            img[y, x] = c

    # rings (outer -> inner): 0 outline, 1-2 metal (hi top/left, lo bottom/right), 3 trim, 4 dark inner line
    def ring(i, hi, mid_, lo):
        for x in range(i, cw - i):
            px(x, i, hi); px(x, ch - 1 - i, lo)
        for y in range(i, ch - i):
            px(i, y, hi); px(cw - 1 - i, y, lo)
        px(cw - 1 - i, i, mid_); px(i, ch - 1 - i, mid_)

    if inset:   # slot: dark outline, inner shadow top/left, light bottom/right (looks pressed in)
        ring(0, P["outline"], P["outline"], P["outline"])
        ring(1, mlo, mlo, mid)
        ring(2, P["outline"], P["outline"], P["outline"])
    else:
        ring(0, P["outline"], P["outline"], P["outline"])
        ring(1, mhi, mid, mlo)
        ring(2, mid, mid, mlo)
        ring(3, tlo if trim else mlo, tlo if trim else mlo, tlo if trim else mlo)
        if trim:
            ring(3, thi, tmid, tlo)
        ring(4, P["outline"], P["outline"], P["outline"])
    # chamfer the outer corners (pixel rounding)
    for (cx, cy) in ((0, 0), (cw - 1, 0), (0, ch - 1), (cw - 1, ch - 1)):
        img[cy, cx] = (0, 0, 0, 0)
    for (cx, cy, dx, dy) in ((1, 0, 1, 0), (0, 1, 0, 1)):
        pass
    img[0, 1] = P["outline"]; img[1, 0] = P["outline"]
    img[0, cw - 2] = P["outline"]; img[1, cw - 1] = P["outline"]
    img[ch - 1, 1] = P["outline"]; img[ch - 2, 0] = P["outline"]
    img[ch - 1, cw - 2] = P["outline"]; img[ch - 2, cw - 1] = P["outline"]

    if ornament == "gems" and not inset:   # gold corner studs with a gem, small side studs
        for (ox, oy, sx, sy) in ((5, 5, 1, 1), (cw - 6, 5, -1, 1), (5, ch - 6, 1, -1), (cw - 6, ch - 6, -1, -1)):
            for (dx, dy, c) in ((0, 0, tmid), (1, 0, thi), (0, 1, thi), (-1, 0, tlo), (0, -1, tlo), (1, 1, tmid), (-1, -1, tlo), (1, -1, tmid), (-1, 1, tmid)):
                px(ox + dx * sx * -1 if False else ox + dx, oy + dy, c)
            px(ox, oy, hexc("#7be0c0"))
            for k in (2, 3):
                px(ox + sx * k, oy, tmid); px(ox, oy + sy * k, tmid)
    if header:   # horizontal divider under a title band, with diamond ends
        y = header // S
        for x in range(6, cw - 6):
            px(x, y, tmid); px(x, y + 1, tlo)
        for x in (5, cw - 6):
            for (dx, dy) in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
                px(x + dx, y + dy, thi if (dx == 0 and dy == 0) else tmid)
    if glow:   # selected state: gold outer glow ring on top of the metal
        for i in (1, 2):
            for x in range(i, cw - i):
                px(x, i, glow); px(x, ch - 1 - i, glow)
            for y in range(i, ch - i):
                px(i, y, glow); px(cw - 1 - i, y, glow)
    return Image.fromarray(img, "RGBA").resize((w, h), Image.NEAREST)


def tab(w, h, on):
    """Shop tab: a pixel trapezoid-ish plate; on = gold-lit."""
    cw, ch = w // S, h // S
    img = np.zeros((ch, cw, 4), np.uint8)
    P = PAL
    base = (P["gold"], P["gold_hi"], P["gold_lo"]) if on else (P["iron"], P["iron_hi"], P["iron_lo"])
    fill = hexc("#2b1f12") if on else hexc("#17121f")
    for y in range(2, ch):
        for x in range(2, cw - 2):
            img[y, x] = fill
    for x in range(3, cw - 3):
        img[1, x] = P["outline"]; img[2, x] = base[1]; img[3, x] = base[0]
    for y in range(3, ch):
        img[y, 2] = P["outline"]; img[y, cw - 3] = P["outline"]
        img[y, 3] = base[1]; img[y, cw - 4] = base[2]
    img[2, 2] = (0, 0, 0, 0); img[2, cw - 3] = (0, 0, 0, 0)
    img[0:2, :, :] = np.where(img[0:2, :, 3:4] > 0, img[0:2, :, :], 0)
    return Image.fromarray(img, "RGBA").resize((w, h), Image.NEAREST)


def save(name, im):
    path = os.path.join(SKIN, name + ".png")
    im.save(path)
    meta = path + ".meta"
    t = open(meta, encoding="utf-8").read()
    t = re.sub(r"filterMode: \d", "filterMode: 0", t)
    open(meta, "w", encoding="utf-8", newline="\n").write(t)
    print("wrote", name, im.size)


def main():
    save("shop_window", frame(256, 256, "velvet", metal="iron", trim="gold", ornament="gems"))
    save("popup_card", frame(256, 256, "navy", metal="iron", trim="gold", ornament="gems", header=72))
    save("settings_card", frame(256, 256, "slate", metal="steel", trim="steel", ornament="gems"))
    save("hall_panel", frame(256, 256, "velvet", metal="iron", trim="gold", ornament="gems"))
    save("item_cell", frame(128, 160, "slot", metal="iron", trim="", ornament="", inset=True))
    save("item_cell_on", frame(128, 160, "slot", metal="iron", trim="", ornament="", inset=True, glow=PAL["gold"]))
    save("item_cell_set", frame(128, 160, "slot", metal="iron", trim="", ornament="", inset=True, glow=hexc("#7be0c0")))
    save("shop_tab", tab(128, 64, False))
    save("shop_tab_on", tab(128, 64, True))


if __name__ == "__main__":
    main()
