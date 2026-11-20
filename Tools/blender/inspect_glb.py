"""blender -b -P inspect_glb.py -- <file.glb>  : list objects, armature bones count and actions."""
import sys
import bpy

src = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
for o in bpy.context.scene.objects:
    extra = ""
    if o.type == "ARMATURE":
        extra = f"bones={len(o.data.bones)} names={[b.name for b in o.data.bones][:60]}"
    if o.type == "MESH":
        extra = f"verts={len(o.data.vertices)} mats={[m.name for m in o.data.materials]}"
    print("OBJ", o.name, o.type, "parent=", o.parent.name if o.parent else None, extra)
for a in bpy.data.actions:
    print("ACTION", a.name, "frames", tuple(a.frame_range))
