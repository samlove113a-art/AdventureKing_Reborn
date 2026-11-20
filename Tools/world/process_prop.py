"""Turn a Meshy pixel-art prop image (flat magenta background) into a transparent, tightly cropped sprite at the game scale.

  python process_prop.py <raw.png> <out.png> <height_m>
The prop is keyed (magenta -> alpha), cleaned (largest blob kept, pink fringe eroded), cropped to its box and resized (nearest) so it is
<height_m> metres tall at 101.05 px/m. Needs numpy + Pillow (Blender's python with PYTHONPATH to Pillow).
"""
import sys

import numpy as np
from PIL import Image

PPU = 101.05


def key(rgb):
    r, g, b = rgb[..., 0].astype(int), rgb[..., 1].astype(int), rgb[..., 2].astype(int)
    bg = (r > 110) & (b > 110) & (g < np.minimum(r, b) * 0.66) & (abs(r - b) < 100)
    return ~bg


def erode(m):
    p = np.pad(m, 1)
    e = m.copy()
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            e &= p[1 + dy:1 + dy + m.shape[0], 1 + dx:1 + dx + m.shape[1]]
    return e


def keep_big(m, frac=0.02):
    h, w = m.shape
    lab = np.zeros((h, w), np.int32)
    sizes = {}
    n = 0
    for y in range(h):
        for x in range(w):
            if m[y, x] and lab[y, x] == 0:
                n += 1
                st = [(y, x)]
                lab[y, x] = n
                c = 0
                while st:
                    cy, cx = st.pop()
                    c += 1
                    for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                        if 0 <= ny < h and 0 <= nx < w and m[ny, nx] and lab[ny, nx] == 0:
                            lab[ny, nx] = n
                            st.append((ny, nx))
                sizes[n] = c
    tot = max(sizes.values())
    out = np.zeros_like(m)
    for k, v in sizes.items():
        if v >= tot * frac:
            out |= lab == k
    return out


def main():
    raw, out, height_m = sys.argv[1], sys.argv[2], float(sys.argv[3])
    im = Image.open(raw).convert("RGB")
    a = np.asarray(im)
    m = keep_big(erode(key(a)))
    ys, xs = np.where(m)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    rgba = np.dstack([a, (m * 255).astype(np.uint8)])[y0:y1, x0:x1]
    rgba[rgba[..., 3] == 0, :3] = 0
    target_h = max(8, round(height_m * PPU))
    scale = target_h / float(rgba.shape[0])
    tw = max(4, round(rgba.shape[1] * scale))
    res = Image.fromarray(rgba, "RGBA").resize((tw, target_h), Image.NEAREST)
    res.save(out)
    print(out, res.size, "alpha ok", np.unique(np.asarray(res)[..., 3]).tolist())


if __name__ == "__main__":
    main()
