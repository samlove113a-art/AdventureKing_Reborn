"""Writes image-to-3D requests for the approved town concepts (polycount budgets sized for PC + mobile)."""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REQ = os.path.join(ROOT, "ArtSource", "Meshy", "requests")

BUDGET = {
    "town_teahouse": 14000, "town_cottage": 10000, "town_forge": 12000, "town_market_stall": 9000,
    "town_bell_tower": 10000, "town_windmill": 9000, "town_banyan_tree": 14000, "town_bamboo": 6000,
    "town_stone_lantern": 4000, "town_lantern_post": 4000, "town_signboard": 5000, "town_hero_statue": 12000,
    "town_stone_bridge": 8000, "town_portal_gate": 9000, "town_crates": 5000, "town_planter": 7000,
}

if __name__ == "__main__":
    jobs = []
    for name, tris in BUDGET.items():
        body = {
            "image_url": f"@file:ArtSource/Meshy/raw/{name}_concept/image_0.png",
            "ai_model": "latest", "should_texture": True, "texture_resolution": "2k",
            "should_remesh": True, "target_polycount": tris, "remove_lighting": True, "target_formats": ["glb"],
        }
        with open(os.path.join(REQ, name + "_model.json"), "w", encoding="utf-8") as f:
            json.dump(body, f, indent=2)
        jobs.append({"kind": "image-to-3d", "name": name + "_model", "request": f"ArtSource/Meshy/requests/{name}_model.json"})
    # Backdrop painting (16:9 is the widest ratio the API accepts).
    with open(os.path.join(REQ, "town_backdrop_far.json"), encoding="utf-8") as f:
        bd = json.load(f)
    bd["aspect_ratio"] = "16:9"
    with open(os.path.join(REQ, "town_backdrop_far.json"), "w", encoding="utf-8") as f:
        json.dump(bd, f, ensure_ascii=False, indent=2)
    jobs.append({"kind": "text-to-image", "name": "town_backdrop_far", "request": "ArtSource/Meshy/requests/town_backdrop_far.json"})
    with open(os.path.join(REQ, "town_models.jobs.json"), "w", encoding="utf-8") as f:
        json.dump(jobs, f, indent=1)
    print(len(jobs), "jobs written")
