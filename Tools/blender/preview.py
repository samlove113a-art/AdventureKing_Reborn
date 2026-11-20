"""Render turnaround previews of a GLB for quality review.
blender -b -P preview.py -- <in.glb> <out_prefix> [size]
"""
import math
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, out = argv[0], argv[1]
size = int(argv[2]) if len(argv) > 2 else 768

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
mn = Vector((1e9, 1e9, 1e9)); mx = Vector((-1e9, -1e9, -1e9))
for o in meshes:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
center = (mn + mx) / 2
height = max(mx.z - mn.z, mx.x - mn.x, mx.y - mn.y)
print("BOUNDS", tuple(round(v, 3) for v in mn), tuple(round(v, 3) for v in mx), "tris",
      sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes))

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = scene.render.resolution_y = size
scene.render.film_transparent = True
world = bpy.data.worlds.new("w"); scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
scene.collection.objects.link(cam); scene.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = height * 1.15
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 3; sun.rotation_euler = (math.radians(50), 0, math.radians(30))
scene.collection.objects.link(sun)
for name, ang in (("front", 0), ("side", 90), ("back", 180), ("q", 35)):
    a = math.radians(ang)
    cam.location = center + Vector((math.sin(a) * 10, -math.cos(a) * 10, 0))
    cam.rotation_euler = (math.radians(90), 0, a)
    scene.render.filepath = f"{out}_{name}.png"
    bpy.ops.render.render(write_still=True)
