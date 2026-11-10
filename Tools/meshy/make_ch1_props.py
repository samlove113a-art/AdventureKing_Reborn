"""Chương 1 Map 1 (Làng Nấm) environment props: one clean single-object concept each (never a whole scene in one
Meshy call), then image-to-3D for the concepts that pass review. Modules are reused and recoloured in Unity.

  python make_ch1_props.py concepts   -> ch1_props_concepts.jobs.json   (N x 6 credits)
  python make_ch1_props.py models     -> ch1_props_models.jobs.json     (N x 30 credits)
"""
import json
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REQ = os.path.join(ROOT, "ArtSource", "Meshy", "requests")

STYLE = ("Single stylized game environment prop for a cute chibi fantasy adventure game, chunky toy-like proportions, "
         "hand-painted look, clean smooth readable shapes, warm saturated colors (red, orange, cream, turquoise), shown in "
         "three-quarter front view, full object visible and centered, plain flat light grey background, no text, no letters, "
         "no people, no animals, no ground scenery.")

PROPS = {
    "ch1_mushroom_house": ("A cozy two-storey house shaped like a giant mushroom: big glossy red cap roof with cream-white "
                           "spots and a scalloped rim, round cream stucco walls with exposed wooden beams, an arched wooden "
                           "front door with a small round window above it, two round windows with warm light, a few stone "
                           "steps and tiny flower pots at the door.", 14000),
    "ch1_crystal_fountain": ("A round village square fountain: two-tier grey stone basin with mossy rim and small carved "
                             "mushroom reliefs, clear turquoise water, a tall glowing sky-blue teardrop crystal standing on a "
                             "carved pedestal in the middle with little water jets around it.", 12000),
    "ch1_mushroom_lamp": ("A street lamp post for a mushroom village: slim curved wooden post wrapped with a green vine, "
                          "topped by a small glowing orange mushroom cap lantern with cream spots, a little round stone base.", 5000),
    "ch1_mushroom_stall": ("A small village market stall: wooden counter with a red and cream striped cloth awning, wicker "
                           "baskets full of colorful mushrooms and berries, hanging little lanterns, a small chalk board.", 12000),
    "ch1_giant_mushroom_tree": ("A huge fantasy mushroom tree: thick twisted ivory stem with roots, wide orange-red cap "
                                "with pale spots, a few smaller mushrooms growing from the stem, hanging glowing vines.", 10000),
}

if __name__ == "__main__":
    stage = sys.argv[1] if len(sys.argv) > 1 else "concepts"
    jobs = []
    for name, (desc, tris) in PROPS.items():
        if stage == "concepts":
            body = {"prompt": STYLE + " " + desc, "ai_model": "nano-banana-2", "remove_background": True}
            kind, suffix = "text-to-image", "_concept"
        else:
            body = {"image_url": f"@file:ArtSource/Meshy/raw/{name}_concept/image_0.png", "ai_model": "latest",
                    "should_texture": True, "texture_resolution": "2k", "should_remesh": True, "topology": "triangle",
                    "target_polycount": tris, "remove_lighting": True, "target_formats": ["glb"]}
            kind, suffix = "image-to-3d", "_model"
        with open(os.path.join(REQ, name + suffix + ".json"), "w", encoding="utf-8") as f:
            json.dump(body, f, ensure_ascii=False, indent=2)
        jobs.append({"kind": kind, "name": name + suffix, "request": f"ArtSource/Meshy/requests/{name}{suffix}.json"})
    out = os.path.join(REQ, f"ch1_props_{stage}.jobs.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(jobs, f, indent=1)
    print(len(jobs), stage, "jobs ->", out)
