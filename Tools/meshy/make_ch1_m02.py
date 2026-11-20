"""Chương 1 Map 2 (Đồng Cỏ Xanh): one monster + two reusable environment modules. The windmill, banyan tree,
planter, crates and signboard are reused from the town (Assets/Art/World/Town/Props), Map 1 modules are recoloured.
Monster brief: ArtSource/Concepts/Chapter01/04_BRIEF_AND_PIPELINE/Meshy_14_Creature_Briefs.md (CH01_MAP02).

  python make_ch1_m02.py concepts   -> ch1_m02_concepts.jobs.json   (3 x 6 credits)
  python make_ch1_m02.py models     -> ch1_m02_models.jobs.json     (3 x 30 credits)
"""
import json
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REQ = os.path.join(ROOT, "ArtSource", "Meshy", "requests")

PROP_STYLE = ("Single stylized game environment prop for a cute chibi fantasy adventure game, chunky toy-like proportions, "
              "hand-painted look, clean smooth readable shapes, fresh meadow palette (grass green, sky blue, warm wood, "
              "white and yellow flowers), shown in three-quarter front view, full object visible and centered, plain flat "
              "light grey background, no text, no letters, no people, no animals, no ground scenery.")
MONSTER_STYLE = ("Full-body creature concept for a 3D game model, single creature, three-quarter front view, plain flat light "
                 "grey background, no text, no labels, no scenery, nothing cropped. Style: original stylized 3D chibi fantasy "
                 "MMORPG creature, hand-painted 3D toy look, smooth clean rounded surfaces, bright saturated palette, soft "
                 "even studio lighting, readable silhouette from the side.")

SUBJECTS = {
    # name: (style, description, target triangles)
    "ch1_pollen_slime": (MONSTER_STYLE,
        "Creature: Slime Phấn Hoa, a round bouncy pollen slime. Bright lime-green glossy translucent jelly body shaped like "
        "a soft dome, slightly wider than tall, glowing yellow pollen speckles floating inside the jelly, a small white "
        "daisy flower with a yellow centre growing on top of its head with two tiny curled leaf antennae, two big "
        "expressive black eyes with white highlights, rosy cheeks, a small happy open mouth. No arms, no legs.", 8000),
    "ch1_wood_fence": (PROP_STYLE,
        "A short rustic wooden meadow fence section: three rounded wooden posts with little pointed caps joined by two "
        "slightly curved horizontal rails, warm honey-brown wood with painted grain, a small green vine with tiny white "
        "flowers wrapped around one post, a tuft of grass at the foot of each post. Long and low, wider than tall.", 4000),
    "ch1_flower_bush": (PROP_STYLE,
        "A round chunky meadow flower bush: soft rounded clumps of bright green leaves, many white daisies, yellow "
        "buttercups and a few pink and light blue wildflowers poking out, two tall grass blades. Wider than tall.", 5000),
}

if __name__ == "__main__":
    stage = sys.argv[1] if len(sys.argv) > 1 else "concepts"
    only = sys.argv[2:]  # optional subset of names
    jobs = []
    for name, (style, desc, tris) in SUBJECTS.items():
        if only and name not in only:
            continue
        if stage == "concepts":
            body = {"prompt": style + " " + desc, "ai_model": "nano-banana-2", "remove_background": True}
            kind, suffix = "text-to-image", "_concept"
        else:
            body = {"image_url": f"@file:ArtSource/Meshy/raw/{name}_concept/image_0.png", "ai_model": "latest",
                    "should_texture": True, "texture_resolution": "2k", "should_remesh": True, "topology": "triangle",
                    "target_polycount": tris, "remove_lighting": True, "target_formats": ["glb"]}
            kind, suffix = "image-to-3d", "_model"
        with open(os.path.join(REQ, name + suffix + ".json"), "w", encoding="utf-8") as f:
            json.dump(body, f, ensure_ascii=False, indent=2)
        jobs.append({"kind": kind, "name": name + suffix, "request": f"ArtSource/Meshy/requests/{name}{suffix}.json"})
    out = os.path.join(REQ, f"ch1_m02_{stage}.jobs.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(jobs, f, indent=1)
    print(len(jobs), stage, "jobs ->", out)
