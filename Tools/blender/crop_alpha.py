# Crops a PNG to the bounding box of its opaque pixels (alpha > threshold), in place.
#   blender -b --python Tools/blender/crop_alpha.py -- <image.png> [padding_px]
import sys

import bpy
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
path = argv[0]
pad = int(argv[1]) if len(argv) > 1 else 2
img = bpy.data.images.load(path)
w, h = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
ys, xs = np.where(px[..., 3] > 0.12)
x0, x1 = max(0, xs.min() - pad), min(w, xs.max() + pad + 1)
y0, y1 = max(0, ys.min() - pad), min(h, ys.max() + pad + 1)
crop = px[y0:y1, x0:x1].copy()
out = bpy.data.images.new("crop", x1 - x0, y1 - y0, alpha=True)
out.pixels[:] = crop.ravel()
out.filepath_raw = path
out.file_format = "PNG"
out.save()
print(f"CROP {path}: {w}x{h} -> {x1 - x0}x{y1 - y0}")
