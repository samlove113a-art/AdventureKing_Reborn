# Encodes a numbered JPG/PNG frame folder into an H.264 MP4 with Blender's bundled FFmpeg.
#   blender -b --python Tools/blender/encode_video.py -- <frames_dir> <out.mp4> [fps] [kbps]
import os
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
frames_dir, out = argv[0], argv[1]
fps = int(argv[2]) if len(argv) > 2 else 30
kbps = int(argv[3]) if len(argv) > 3 else 1800
files = sorted(f for f in os.listdir(frames_dir) if f.lower().endswith((".jpg", ".png")))
if not files:
    sys.exit("no frames in " + frames_dir)

scene = bpy.context.scene
scene.sequence_editor_create()
strip = scene.sequence_editor.strips.new_image("frames", os.path.join(frames_dir, files[0]), 1, 1) \
    if hasattr(scene.sequence_editor, "strips") else \
    scene.sequence_editor.sequences.new_image("frames", os.path.join(frames_dir, files[0]), 1, 1)
for f in files[1:]:
    strip.elements.append(f)
first = bpy.data.images.load(os.path.join(frames_dir, files[0]))
w, h = first.size
scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = w, h, 100
scene.render.fps = fps
scene.frame_start, scene.frame_end = 1, len(files)
r = scene.render
if hasattr(r.image_settings, "media_type"):  # Blender 5.x: video output is a separate media type
    r.image_settings.media_type = "VIDEO"
r.image_settings.file_format = "FFMPEG"
r.ffmpeg.format = "MPEG4"
r.ffmpeg.codec = "H264"
r.ffmpeg.constant_rate_factor = "NONE"
r.ffmpeg.video_bitrate = kbps
r.ffmpeg.maxrate = int(kbps * 1.5)
r.ffmpeg.gopsize = fps
r.ffmpeg.audio_codec = "NONE"
r.filepath = out
os.makedirs(os.path.dirname(out), exist_ok=True)
bpy.ops.render.render(animation=True)
print(f"VIDEO {out}: {len(files)} frames @ {fps} fps = {len(files) / fps:.1f}s, {w}x{h}")
