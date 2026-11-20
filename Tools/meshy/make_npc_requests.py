"""Concept requests for the town NPCs — same chibi style block as the four heroes (consistency first).
Original Adventure King characters, Vietnamese-inspired fantasy village; no reference to other games."""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REQ = os.path.join(ROOT, "ArtSource", "Meshy", "requests")
STYLE = ("Full-body character concept for a 3D game model, single character, front view, A-pose, standing straight, arms "
         "slightly away from body, plain flat light grey background, no text. Style: super-deformed chibi proportions, total "
         "height only 2.5 heads, very large round head, short stubby limbs, small hands and big chunky boots, stylized "
         "hand-painted 3D toy look, smooth clean surfaces, bright saturated palette, soft even lighting. Clothing ends above "
         "the knees so legs are clearly visible. Empty hands, nothing held.")
NPCS = {
    "npc_elder": "Character: kind old village elder of a wind-bell town, long white beard tied with a small jade bead, bushy white "
                 "eyebrows, warm smiling eyes, round wooden-rimmed spectacles, teal and cream layered robe with a short indigo "
                 "vest embroidered with small golden wind-bell motifs, a tiny brass wind chime hanging at the belt, soft cloth "
                 "shoes with straw sandals.",
    "npc_smith": "Character: cheerful sturdy blacksmith woman, short copper-red hair under a rolled cloth headband, freckles, "
                 "rolled-up sleeves showing strong arms, thick leather apron with brass rivets over a rust-orange tunic, heavy "
                 "work gloves tucked in the belt, soot smudge on one cheek, chunky dark boots.",
    "npc_storekeeper": "Character: friendly round-faced storekeeper man, neat black hair in a short topknot, small moustache, "
                       "olive-green merchant jacket with big pockets and a ledger tucked in the sash, a ring of brass keys on "
                       "the belt, cream trousers, soft brown shoes.",
}

if __name__ == "__main__":
    jobs = []
    for name, desc in NPCS.items():
        with open(os.path.join(REQ, name + "_concept.json"), "w", encoding="utf-8") as f:
            json.dump({"pose_mode": "a-pose", "remove_background": True, "prompt": STYLE + " " + desc, "ai_model": "nano-banana-2"},
                      f, ensure_ascii=False, indent=2)
        jobs.append({"kind": "text-to-image", "name": name + "_concept", "request": f"ArtSource/Meshy/requests/{name}_concept.json"})
    with open(os.path.join(REQ, "npc_concepts.jobs.json"), "w", encoding="utf-8") as f:
        json.dump(jobs, f, indent=1)
    print(len(jobs), "npc jobs")
