"""Writes the Meshy concept requests for the central town 'Thị trấn Chuông Gió' (original Adventure King design:
Vietnamese-inspired fantasy — curved tile roofs, wind chimes, lanterns, lotus, bamboo, banyan, rune crystals).
No reference to any existing game. Outputs ArtSource/Meshy/requests/town_*.json and town_concepts.jobs.json."""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REQ = os.path.join(ROOT, "ArtSource", "Meshy", "requests")

STYLE = ("Single stylized game environment prop for a cute chibi fantasy adventure game, chunky toy-like proportions, "
         "hand-painted look, clean smooth readable shapes, soft saturated colors, shown in three-quarter front view, "
         "full object visible and centered, plain flat light grey background, no text, no letters, no people, no animals.")

PROPS = {
    "town_teahouse": "A two-storey fantasy teahouse with gently upturned curved teal glazed-tile roofs, cream plaster walls, "
                     "warm wooden beams and balcony, a round moon-gate doorway, red paper lanterns hanging under the eaves, small brass wind chimes.",
    "town_cottage": "A small cozy cottage with a sweeping upturned terracotta-orange tile roof, white plaster walls, wooden lattice windows, "
                    "a short brick chimney and clay flower pots beside the door.",
    "town_forge": "A blacksmith workshop booth with a slate-blue curved tile roof on wooden pillars, open front showing an anvil, "
                  "a stone forge with warm orange glow and a wall rack of hammers and tongs.",
    "town_market_stall": "A market stall with a striped red-and-cream cloth awning, wooden counter with woven baskets of fruit, "
                         "rolled scrolls and small jars, a hanging lantern.",
    "town_bell_tower": "A tall slender wooden bell tower with three stacked small curved teal roofs, a large bronze bell visible in the open "
                       "top level, wind chimes at the roof corners, square carved stone base.",
    "town_windmill": "A small fantasy windmill with four fabric sails painted with swirl wind patterns, round white stone body, "
                     "teal conical tile roof, little wooden door.",
    "town_banyan_tree": "A large stylized banyan tree with a big round lush green canopy, hanging aerial roots and a thick twisting trunk "
                        "with a few small hanging lanterns.",
    "town_bamboo": "A cluster of tall bright green bamboo stalks with segmented joints and leafy tops, growing from a small mossy mound.",
    "town_stone_lantern": "A carved stone garden lantern on a short pedestal with a warm glowing window and patches of moss.",
    "town_lantern_post": "A wooden lamp post with a curved arm holding a round red silk paper lantern with a gold tassel.",
    "town_signboard": "A wooden fantasy signboard on two posts with a large blank cream-coloured panel and carved curling scroll ends, "
                      "small roof of curved tiles on top.",
    "town_hero_statue": "A weathered white marble statue on a round stone pedestal of a heroic chibi adventurer raising a sword to the sky, "
                        "cape flowing, gold trim accents, ivy at the base.",
    "town_stone_bridge": "A small arched stone footbridge with low carved railings and mossy edges, seen from the side.",
    "town_portal_gate": "An original magic portal gate: a tall ring of pale violet carved stone covered with glowing cyan runes, "
                        "three floating teal crystals around the ring, standing on a hexagonal stone base with steps. "
                        "The inside of the ring is empty and open.",
    "town_crates": "A tidy pile of wooden crates, two barrels and a cloth sack tied with rope.",
    "town_planter": "A long wooden planter box overflowing with colorful flowers, small lotus leaves and ferns.",
}

BACKDROPS = {
    "town_backdrop_far": "Wide panoramic background painting for a side-scrolling fantasy game, no characters, no text, no buildings in the "
                         "foreground: soft pastel blue sky with large fluffy clouds, distant layered blue-green mountains with thin waterfalls, "
                         "a few small floating grassy islands with tiny curved-roof pavilions, terraced rice fields far away, hand-painted, "
                         "bright cheerful morning light, wide horizontal composition, lower third fades to pale haze.",
}


def write(name, body):
    with open(os.path.join(REQ, name + ".json"), "w", encoding="utf-8") as f:
        json.dump(body, f, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    os.makedirs(REQ, exist_ok=True)
    jobs = []
    for name, desc in PROPS.items():
        write(name + "_concept", {"prompt": STYLE + " " + desc, "aspect_ratio": "1:1", "ai_model": "nano-banana-2",
                                  "remove_background": True})
        jobs.append({"kind": "text-to-image", "name": name + "_concept", "request": f"ArtSource/Meshy/requests/{name}_concept.json"})
    for name, desc in BACKDROPS.items():
        write(name, {"prompt": desc, "aspect_ratio": "21:9", "ai_model": "nano-banana-2"})
        jobs.append({"kind": "text-to-image", "name": name, "request": f"ArtSource/Meshy/requests/{name}.json"})
    with open(os.path.join(REQ, "town_concepts.jobs.json"), "w", encoding="utf-8") as f:
        json.dump(jobs, f, indent=1)
    print(len(jobs), "concept jobs written")
