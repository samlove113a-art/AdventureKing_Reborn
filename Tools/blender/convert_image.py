# Converts an image (e.g. .webp) to PNG with Blender's image IO (no extra Python packages).
# blender -b --python Tools/blender/convert_image.py -- <in> <out.png>
import sys
import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]
img = bpy.data.images.load(src)
w, h = img.size
px = list(img.pixels[:])  # forces the pixel buffer to load
out = bpy.data.images.new("converted", w, h, alpha=True)
out.pixels[:] = px
out.filepath_raw = dst
out.file_format = "PNG"
out.save()
print("converted", src, "->", dst, w, "x", h)
