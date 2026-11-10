"""Chapter 1 (Vương Quốc Nấm) concept requests: one clean single-subject view per creature / NPC, from the
owner's briefs (ArtSource/Concepts/Chapter01/04_BRIEF_AND_PIPELINE/Meshy_14_Creature_Briefs.md), adapted so a
humanoid rig works (stubby but visible arms and legs, A-pose). Original Adventure King designs.

python make_ch1_requests.py            -> writes ArtSource/Meshy/requests/ch1_*_concept.json
"""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REQ = os.path.join(ROOT, "ArtSource", "Meshy", "requests")
STYLE = ("Full-body character concept for a 3D game model, single character, front view, A-pose, standing straight, "
         "arms slightly away from body, plain flat light grey background, no text, no labels, no scenery, nothing cropped. "
         "Style: original stylized 3D chibi fantasy MMORPG creature, hand-painted 3D toy look, smooth clean rounded "
         "surfaces, bright saturated palette, soft even studio lighting, readable silhouette from the side.")
SUBJECTS = {
    # Map 1 normal monster
    "ch1_nam_non": "Character: Nấm Non, a tiny mushroom foot soldier. Big soft red mushroom cap with a scalloped rim and "
                   "cream-white spots, squat round ivory body with a cute face under the cap (big black eyes, small angry "
                   "eyebrows, tiny mouth), two short stubby ivory arms with little round hands, two short stubby legs with "
                   "small brown root-like feet, a tiny green leaf pouch on a vine strap across the body. Empty hands.",
    # Map 1 quest NPC (chapter guide)
    "ch1_npc_mushroom_elder": "Character: Trưởng Làng Nấm, the kind old elder of a mushroom village, super-deformed chibi "
                   "proportions (2.5 heads tall), wide orange-red mushroom cap worn like a hat with pale spots, long soft "
                   "white beard, bushy white eyebrows, warm smiling eyes, turquoise and cream layered robe with a short "
                   "moss-green vest embroidered with small golden mushroom motifs, a small glowing crystal pendant, robe "
                   "ends above the knees so short legs and soft brown boots are visible. Empty hands, nothing held.",
}

if __name__ == "__main__":
    for name, desc in SUBJECTS.items():
        with open(os.path.join(REQ, name + "_concept.json"), "w", encoding="utf-8") as f:
            json.dump({"pose_mode": "a-pose", "remove_background": True, "prompt": STYLE + " " + desc,
                       "ai_model": "nano-banana-2"}, f, ensure_ascii=False, indent=2)
    print(len(SUBJECTS), "chapter 1 concept requests")
