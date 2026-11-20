"""Concept image -> textured 3D model -> humanoid rig -> animation clips (one merged GLB per batch).

python character_pipeline.py <name> <concept_png_rel> <height_m> <ids_batch1> [<ids_batch2> ...]
  ids_batch: comma separated Meshy action ids (max 10 per batch)
Outputs land in ArtSource/Meshy/raw/<name>_*; every step is logged to the manifest by meshy.run().
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import meshy  # noqa: E402

name, concept, height = sys.argv[1], sys.argv[2], float(sys.argv[3])
batches = [[int(x) for x in b.split(",")] for b in sys.argv[4:]]

def _done(step):
    """Reuse a SUCCEEDED task from the manifest so a resumed run never pays twice."""
    import json
    if not os.path.exists(meshy.MANIFEST):
        return None
    hit = None
    with open(meshy.MANIFEST, encoding="utf-8") as f:
        for line in f:
            r = json.loads(line)
            if r["name"] == step and r["status"] == "SUCCEEDED":
                hit = r
    return {"status": "SUCCEEDED", "id": hit["task_id"]} if hit else None


model = _done(f"{name}_model") or meshy.run("image-to-3d", f"{name}_model", {
    "image_url": meshy.data_uri(os.path.join(meshy.ROOT, concept)),
    "ai_model": "latest", "should_texture": True, "texture_resolution": "2k",
    "should_remesh": True, "topology": "triangle", "target_polycount": 40000,
    "pose_mode": "a-pose", "remove_lighting": True, "target_formats": ["glb"],
})
if model.get("status") != "SUCCEEDED":
    sys.exit(f"{name}: model failed")
rig = _done(f"{name}_rig") or meshy.run("rigging", f"{name}_rig", {"input_task_id": model["id"], "height_meters": height})
if rig.get("status") != "SUCCEEDED":
    sys.exit(f"{name}: rig failed {rig.get('task_error')}")
for i, ids in enumerate(batches):
    if _done(f"{name}_anim_{chr(97 + i)}"):
        continue
    meshy.run("animations", f"{name}_anim_{chr(97 + i)}", {
        "rig_task_id": rig["id"], "action_ids": ids,
        "post_process": {"operation_type": "change_fps", "fps": 30},
    })
print("PIPELINE DONE", name)
