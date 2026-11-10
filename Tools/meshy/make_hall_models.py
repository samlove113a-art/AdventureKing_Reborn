"""Image-to-3D requests for the reviewed hall concepts (polycount budgets sized for PC + mobile: repeated pieces
such as pillars, benches and wall modules are kept small). Writes hall_models.jobs.json (18 x 30 credits)."""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REQ = os.path.join(ROOT, "ArtSource", "Meshy", "requests")

BUDGET = {
    "gh_throne": 12000, "gh_council_set": 16000, "gh_pillar": 6000, "gh_wall_arch": 8000, "gh_banner": 6000,
    "gh_ranking_board": 8000, "gh_donation_altar": 14000, "gh_reception_counter": 14000, "gh_crystal_brazier": 7000,
    "hall_chandelier": 12000,
    "wh_flower_arch": 14000, "wh_altar_stage": 18000, "wh_guest_bench": 5000, "wh_banquet_table": 12000,
    "wh_wedding_cake": 9000, "wh_marble_pillar": 6000, "wh_wall_window": 8000, "wh_harp": 10000,
}

if __name__ == "__main__":
    jobs = []
    for name, tris in BUDGET.items():
        body = {
            "image_url": f"@file:ArtSource/Meshy/raw/{name}_concept/image_0.png",
            "ai_model": "latest", "should_texture": True, "texture_resolution": "2k",
            "should_remesh": True, "topology": "triangle", "target_polycount": tris, "remove_lighting": True, "target_formats": ["glb"],
        }
        with open(os.path.join(REQ, name + "_model.json"), "w", encoding="utf-8") as f:
            json.dump(body, f, indent=2)
        jobs.append({"kind": "image-to-3d", "name": name + "_model", "request": f"ArtSource/Meshy/requests/{name}_model.json"})
    with open(os.path.join(REQ, "hall_models.jobs.json"), "w", encoding="utf-8") as f:
        json.dump(jobs, f, indent=1)
    print(len(jobs), "model jobs written (", len(jobs) * 30, "credits )")
