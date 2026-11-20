"""Clean the Meshy pixel panoramas (pixel_bg_mNN) into wrap-around 1024x1024 textures: crop the white frame, resize (nearest), crossfade the seam.
  python process_panoramas.py   -> Assets/Art/World/Generated/c1_mNN_pixel_panorama_v2.png for every pixel_bg_mNN that has an image (m02..m10)
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "ArtSource", "Meshy", "raw")
OUT = os.path.join(ROOT, "Assets", "Art", "World", "Generated")
F = 96


def clean(src, dst):
    a = np.asarray(Image.open(src).convert("RGB")).astype(int)
    nw = a.sum(axis=2) < 740
    ys = np.where(nw.mean(axis=1) > 0.5)[0]
    xs = np.where(nw.mean(axis=0) > 0.5)[0]
    x0, x1, y0, y1 = xs.min() + 2, xs.max() - 2, ys.min() + 2, ys.max() - 2
    # also trim solid black margins (some generations come with a black frame on one or two sides)
    lum = a.sum(axis=2) / 3.0
    dark = lambda line: line.mean() < 14 and line.std() < 10
    while y0 < y1 - 200 and dark(lum[y0, x0:x1]): y0 += 1
    while y1 > y0 + 200 and dark(lum[y1, x0:x1]): y1 -= 1
    while x0 < x1 - 200 and dark(lum[y0:y1, x0]): x0 += 1
    while x1 > x0 + 200 and dark(lum[y0:y1, x1]): x1 -= 1
    c = Image.fromarray(a.astype("uint8")).crop((x0, y0, x1 + 1, y1 + 1)).resize((1024, 1024), Image.NEAREST)
    arr = np.asarray(c).astype(float)
    W = arr.shape[1]
    out = arr[:, :W - F].copy()
    for i in range(F):
        t = i / float(F)
        out[:, i] = arr[:, W - F + i] * (1 - t) + arr[:, i] * t
    Image.fromarray(out.clip(0, 255).astype("uint8")).resize((1024, 1024), Image.NEAREST).save(dst)
    return (x0, x1, y0, y1)


def main():
    for n in range(2, 11):
        src = os.path.join(RAW, f"pixel_bg_m{n:02d}", "image_0.png")
        if not os.path.exists(src):
            print("missing", n)
            continue
        box = clean(src, os.path.join(OUT, f"c1_m{n:02d}_pixel_panorama_v2.png"))
        print("m%02d" % n, "content box", box)


if __name__ == "__main__":
    main()
