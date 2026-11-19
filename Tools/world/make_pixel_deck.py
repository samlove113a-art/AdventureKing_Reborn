"""Pixel-art Dark Fantasy wooden deck (platform) tile: a tileable plank strip, 256x48 px (2.5 m x 0.47 m at 101 px/m), plus a post tile.
  python make_pixel_deck.py -> Assets/Art/World/PixelGround/deck_planks.png  deck_post.png"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "World", "PixelGround")
C = 2


def h(x, a=255):
    x = x.lstrip("#")
    return (int(x[0:2], 16), int(x[2:4], 16), int(x[4:6], 16), a)


OUTLINE = h("#0d0a10")
WOOD = [h("#2a1b17"), h("#3e2a22"), h("#58392a"), h("#7a5236"), h("#a06f45")]
IRON = [h("#23202b"), h("#3d394a"), h("#5c566b")]
MOSS = h("#2f6b3e")


def planks(cw=128, ch=24):
    a = np.zeros((ch, cw, 4), np.uint8)
    rng = np.random.RandomState(11)
    seg = 16                                   # plank length in cells (128 divisible)
    for y in range(ch):
        for x in range(cw):
            if y < 2:
                a[y, x] = OUTLINE if y == 0 else WOOD[4]                  # top edge: outline + lit edge
            elif y >= ch - 2:
                a[y, x] = OUTLINE if y == ch - 1 else WOOD[0]
            else:
                px = x % seg
                px_id = x // seg
                tone = 2 + (px_id * 5 % 3 == 0) - (px_id * 7 % 4 == 0)
                grain = rng.rand()
                v = tone + (1 if y < 5 else 0) - (1 if y > ch - 6 else 0)
                if grain < 0.07:
                    v -= 1
                a[y, x] = WOOD[int(min(4, max(0, v)))]
                if px == 0:
                    a[y, x] = OUTLINE                                      # gap between planks
    # iron straps with rivets every other plank joint
    for x0 in range(2, cw, 32):
        for y in range(2, ch - 2):
            for dx in (0, 1, 2):
                a[y, (x0 + dx) % cw] = IRON[1 + (dx == 0)] if dx != 2 else IRON[0]
        a[4, (x0 + 1) % cw] = IRON[2]; a[ch - 6, (x0 + 1) % cw] = IRON[2]
    # a little moss on the lit edge
    for x in range(cw):
        if rng.rand() < 0.07:
            a[1, x] = MOSS
            if rng.rand() < 0.5:
                a[2, x] = MOSS
    return a


def post(cw=12, ch=64):
    a = np.zeros((ch, cw, 4), np.uint8)
    for y in range(ch):
        for x in range(cw):
            if x in (0, cw - 1):
                a[y, x] = OUTLINE
            else:
                v = 3 if x < 4 else (2 if x < cw - 4 else 1)
                if (y + x * 2) % 13 == 0:
                    v -= 1
                a[y, x] = WOOD[v]
        if y % 16 in (0, 1):
            for x in range(1, cw - 1):
                a[y, x] = IRON[1 if y % 16 == 0 else 0]
    return a


def up(a):
    return Image.fromarray(a, "RGBA").resize((a.shape[1] * C, a.shape[0] * C), Image.NEAREST)


def main():
    os.makedirs(OUT, exist_ok=True)
    up(planks()).save(os.path.join(OUT, "deck_planks.png"))
    up(post()).save(os.path.join(OUT, "deck_post.png"))
    print("wrote deck_planks 256x48, deck_post 24x128")


if __name__ == "__main__":
    main()
