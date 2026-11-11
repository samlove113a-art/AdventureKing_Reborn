# Numeric concept check when images can't be eyeballed: alpha bbox/coverage, separate blobs, mean colour of the subject.
# blender -b --python Tools/blender/image_stats.py -- <img1> <img2> ...
import sys

import bpy
import numpy as np

for p in sys.argv[sys.argv.index("--") + 1:]:
    img = bpy.data.images.load(p)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    a = px[..., 3] > 0.5
    if not a.any():
        print("STATS", p, "no opaque pixels")
        continue
    ys, xs = np.nonzero(a)
    # coarse blob count on a 64x64 grid (4-neighbour flood fill)
    g = 64
    grid = np.zeros((g, g), bool)
    grid[(ys * g // h), (xs * g // w)] = True
    seen = np.zeros_like(grid)
    blobs = []
    for sy, sx in zip(*np.nonzero(grid)):
        if seen[sy, sx]:
            continue
        stack, n = [(sy, sx)], 0
        seen[sy, sx] = True
        while stack:
            y, x = stack.pop()
            n += 1
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = y + dy, x + dx
                if 0 <= yy < g and 0 <= xx < g and grid[yy, xx] and not seen[yy, xx]:
                    seen[yy, xx] = True
                    stack.append((yy, xx))
        blobs.append(n)
    rgb = px[..., :3][a]
    hsv_g = rgb[:, 1] - np.maximum(rgb[:, 0], rgb[:, 2])
    print("STATS", p.split("raw")[-1], f"size {w}x{h}", f"coverage {a.mean():.2f}",
          f"bbox x {xs.min()/w:.2f}-{xs.max()/w:.2f} y {1-ys.max()/h:.2f}-{1-ys.min()/h:.2f}",
          f"blobs {sorted(blobs, reverse=True)[:4]}", f"mean rgb {rgb.mean(0).round(2).tolist()}",
          f"green-dominant {np.mean(hsv_g > 0.08):.2f}", f"yellowish {np.mean((rgb[:,0]>0.7)&(rgb[:,1]>0.6)&(rgb[:,2]<0.4)):.2f}",
          f"white {np.mean(rgb.min(1) > 0.85):.2f}", f"dark {np.mean(rgb.max(1) < 0.15):.2f}", sep=" | ")
