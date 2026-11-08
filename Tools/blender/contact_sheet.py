# Builds a labelled-by-order contact sheet from several images (Blender image IO + numpy, no extra packages).
# blender -b --python Tools/blender/contact_sheet.py -- <out.png> <cols> <cell_px> <img1> <img2> ...
import sys

import bpy
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
out, cols, cell = argv[0], int(argv[1]), int(argv[2])
paths = argv[3:]
rows = (len(paths) + cols - 1) // cols
sheet = np.ones((rows * cell, cols * cell, 4), dtype=np.float32) * np.array([0.85, 0.86, 0.88, 1.0], dtype=np.float32)

for i, p in enumerate(paths):
    img = bpy.data.images.load(p)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    # Composite over the grey cell (images may have alpha).
    a = px[..., 3:4]
    px = px * a + np.array([0.85, 0.86, 0.88, 1.0], dtype=np.float32) * (1 - a)
    s = cell / max(w, h)
    nw, nh = max(1, int(w * s)), max(1, int(h * s))
    ys = (np.arange(nh) / s).astype(int).clip(0, h - 1)
    xs = (np.arange(nw) / s).astype(int).clip(0, w - 1)
    small = px[ys][:, xs]
    r, c = i // cols, i % cols
    # Blender images are bottom-up: place row 0 at the top of the sheet.
    y0 = (rows - 1 - r) * cell + (cell - nh) // 2
    x0 = c * cell + (cell - nw) // 2
    sheet[y0:y0 + nh, x0:x0 + nw] = small
    bpy.data.images.remove(img)

o = bpy.data.images.new("sheet", cols * cell, rows * cell, alpha=True)
o.pixels[:] = sheet.ravel()
o.filepath_raw = out
o.file_format = "PNG"
o.save()
print("sheet", out, len(paths))
