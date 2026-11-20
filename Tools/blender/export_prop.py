# Static prop export for Unity: GLB (Meshy) -> FBX + PNG texture.
#   blender -b --python Tools/blender/export_prop.py -- <in.glb> <out_dir> <Name> <height_m> [yaw_deg]
# - joins all meshes, applies transforms, pivot at bottom-centre, uniform scale to the target height,
# - optional yaw so the prop's front faces the side-view camera (-Y in Blender = towards camera after FBX axis conversion),
# - unique material/texture names (<Name>_Mat, <Name>_baseColor.png) to avoid collisions in Unity,
# - prints vertex/triangle counts for the performance budget.
import math
import os
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, out_dir, name, height = argv[0], argv[1], argv[2], float(argv[3])
yaw = float(argv[4]) if len(argv) > 4 else 0.0
os.makedirs(out_dir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
for o in bpy.context.scene.objects:
    o.select_set(o in meshes)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
# Unparent (GLB root empties) keeping world transform, then bake transforms into the mesh.
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Yaw around Z, then normalise size and pivot.
obj.rotation_euler = (0.0, 0.0, math.radians(yaw))
bpy.ops.object.transform_apply(rotation=True)
xs = [v.co.x for v in obj.data.vertices]
ys = [v.co.y for v in obj.data.vertices]
zs = [v.co.z for v in obj.data.vertices]
size_z = max(zs) - min(zs)
s = height / max(size_z, 1e-6)
centre = Vector(((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, min(zs)))
for v in obj.data.vertices:
    v.co = (v.co - centre) * s
obj.name = name
obj.data.name = name + "_Mesh"

# Unique material/texture names.
for i, slot in enumerate(obj.material_slots):
    m = slot.material
    if not m:
        continue
    m.name = f"{name}_Mat" if i == 0 else f"{name}_Mat{i}"
    if m.use_nodes:
        for n in m.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image:
                n.image.name = f"{name}_baseColor" if "base" in n.image.name.lower() or i == 0 else f"{name}_{n.image.name}"
                path = os.path.join(out_dir, n.image.name + ".png")
                n.image.filepath_raw = path
                n.image.file_format = "PNG"
                n.image.save()

tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
dims = obj.dimensions
bpy.ops.export_scene.fbx(
    filepath=os.path.join(out_dir, name + ".fbx"), use_selection=False, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y", path_mode="COPY", embed_textures=False, mesh_smooth_type="FACE",
    add_leaf_bones=False, bake_anim=False,
    # Bake the Z-up -> Y-up conversion into the vertex data: static props then import upright with an identity
    # root in Unity (no reliance on the importer's axis conversion, which left props lying on their backs).
    bake_space_transform=True,
)
print(f"PROP {name}: tris={tris} size=({dims.x:.2f}, {dims.y:.2f}, {dims.z:.2f}) m")
