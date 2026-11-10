"""Concept requests for Phase D: Sảnh Bang Hội (Guild Hall) and Sảnh Kết Hôn (Wedding Hall).
Same chibi style blocks as the town props and NPCs (consistency first). Original Adventure King designs:
Vietnamese-inspired fantasy — wind bells, lotus, peach blossom, jade crystals, curved roofs. No reference to other games.

Budget (2026-11-10, balance 931): 19 props x 36 + 3 NPCs x 56 + 2 textures x 6 = 828 credits; this script only writes
the concept stage (24 x 6 = 144). Models are requested only for concepts that pass review (make_hall_models.py)."""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REQ = os.path.join(ROOT, "ArtSource", "Meshy", "requests")

PROP_STYLE = ("Single stylized game environment prop for a cute chibi fantasy adventure game, chunky toy-like proportions, "
              "hand-painted look, clean smooth readable shapes, soft saturated colors, shown in three-quarter front view, "
              "full object visible and centered, plain flat light grey background, no text, no letters, no people, no animals.")

GUILD = ("Interior furniture of a grand fantasy guild hall: carved pale stone, deep teal and royal blue cloth, polished gold trim, "
         "glowing cyan jade crystals, wind-bell motifs.")
WEDDING = ("Decor of a romantic fantasy wedding chapel: white marble, blush pink and cream fabric, soft gold trim, "
           "peach blossoms and pink lotus flowers, little warm fairy lights.")

PROPS = {
    # ---- Guild Hall (Sảnh Bang Hội)
    "gh_throne": GUILD + " A grand guild master's throne on a two-step round stone dais: tall carved high back topped with a "
                 "golden wind-bell crest, deep teal velvet cushion, armrests ending in carved scroll curls, small cyan crystals set in the back.",
    "gh_council_set": GUILD + " A round wooden council table with a teal and gold table runner, a rolled map and a small crystal "
                      "centerpiece on top, surrounded by six matching chunky wooden chairs with teal cushions, tidy and symmetric.",
    "gh_pillar": GUILD + " One tall carved stone column for a great hall interior: square stepped base, round shaft with spiral "
                 "carved ribbons and wind-bell reliefs, gold band rings, wide carved capital. Just the column, straight and upright.",
    "gh_wall_arch": GUILD + " One straight modular interior wall section of a great hall seen from the front: pale carved stone "
                    "blocks, a tall pointed arch window with teal and gold stained glass in the middle, a carved stone ledge at the bottom. "
                    "Flat back, rectangular outline.",
    "gh_banner": GUILD + " A tall standing guild banner on an ornate gold pole with a stone base: long royal-blue cloth banner "
                 "with gold edging showing an original crest of a shield with a wind bell and two crossed swords, tassels at the bottom.",
    "gh_ranking_board": GUILD + " A large ornate standing ranking board: carved dark wood frame with a gold laurel crest on top, "
                        "a blank cream parchment panel with empty horizontal rows, three small gold, silver and bronze medallions on the left side.",
    "gh_donation_altar": GUILD + " A guild contribution altar: a carved stone pedestal holding a large open treasure chest with gold "
                         "coins and glowing cyan crystals, a floating crystal above it, small steps in front.",
    "gh_reception_counter": GUILD + " A curved wooden reception counter with a gold-trimmed teal cloth front, an open ledger book, "
                            "an inkwell and quill, and behind it a wooden quest notice board with pinned blank paper notes and a small roof.",
    "gh_crystal_brazier": GUILD + " A magic brazier torch: a tall slender gold and stone stand holding a shallow bowl with a cluster "
                          "of glowing cyan crystals and soft magic flame above.",
    "hall_chandelier": "Decor for a fantasy palace hall: a large round hanging crystal chandelier with three tiers of gold rings, "
                       "many clear teardrop crystals, small glowing candles, a short chain at the top. Seen slightly from below.",
    # ---- Wedding Hall (Sảnh Kết Hôn)
    "wh_flower_arch": WEDDING + " A tall rounded wedding arch made of white wood fully covered with pink peach blossoms, white roses, "
                      "green leaves and hanging sheer cream fabric drapes, small fairy lights.",
    "wh_altar_stage": WEDDING + " A round raised white marble ceremony stage with three wide front steps and gold trim, a small "
                      "marble pedestal in the middle holding a pink velvet cushion with two gold rings, flower bouquets on both front corners.",
    "wh_guest_bench": WEDDING + " A long white wooden guest bench with a curved backrest, a pink satin bow and a small flower bunch "
                      "tied on the aisle end, cream cushion.",
    "wh_banquet_table": WEDDING + " A long banquet table with a flowing cream tablecloth and pink runner, set with white plates, "
                        "crystal wine glasses, a small flower centerpiece and two gold candle holders.",
    "wh_wedding_cake": WEDDING + " A tall four-tier white wedding cake decorated with pink sugar peach blossoms, pearl beads and gold "
                       "ribbons, a small gold heart topper, on a silver cake stand.",
    "wh_marble_pillar": WEDDING + " One tall slender white marble column with a gold spiral garland of pink flowers and green vines "
                        "climbing around it, ornate capital and round base. Just the column, straight and upright.",
    "wh_wall_window": WEDDING + " One straight modular interior wall section seen from the front: smooth white marble with gold "
                      "trim, a large round rose window of pink and gold stained glass in the upper middle, soft pink draped curtain on each side. "
                      "Flat back, rectangular outline.",
    "wh_harp": WEDDING + " An elegant standing golden concert harp with a carved flower crown, many fine strings and a small "
               "round white stool beside it.",
}

TEXTURES = {
    "gh_floor_tex": "Seamless tileable square floor texture, top-down orthographic, flat even lighting, no shadows, no perspective: "
                    "hand-painted stylized pale stone tiles in a large diamond pattern with thin gold inlay lines and small teal "
                    "jade rosettes at the corners, soft painterly game texture, no text.",
    "wh_carpet_tex": "Seamless tileable square texture, top-down orthographic, flat even lighting, no shadows: a soft blush pink "
                     "wedding aisle carpet with a delicate cream peach-blossom pattern and thin gold border lines running along "
                     "both sides, hand-painted stylized game texture, no text.",
}

NPC_STYLE = ("Full-body character concept for a 3D game model, single character, front view, A-pose, standing straight, arms "
             "slightly away from body, plain flat light grey background, no text. Style: super-deformed chibi proportions, total "
             "height only 2.5 heads, very large round head, short stubby limbs, small hands and big chunky boots, stylized "
             "hand-painted 3D toy look, smooth clean surfaces, bright saturated palette, soft even lighting. Clothing ends above "
             "the knees so legs are clearly visible. Empty hands, nothing held.")
NPCS = {
    "npc_captain": "Character: confident friendly alliance captain woman, short silver-blue hair with a side braid, determined "
                   "smiling eyes, light polished steel shoulder armor with gold trim over a royal-blue tabard showing an original "
                   "crest of a shield with a wind bell, short teal cape, brown leather belt with a small brass badge, sturdy boots.",
    "npc_officiant": "Character: gentle elderly wedding officiant man, neat white hair and a short white beard, kind closed-eye "
                     "smile, long-sleeved cream and soft gold ceremonial robe with pink lotus embroidery and a pale pink sash, a small "
                     "gold blossom pendant, soft cream shoes.",
    "npc_musician": "Character: cheerful young bard musician girl, wavy chestnut hair with a pink peach-blossom hair clip, "
                    "rosy cheeks, cream blouse with puffy sleeves, short teal vest with gold buttons, short pink skirt over leggings, "
                    "a small music-note brooch, soft brown boots.",
}


def write(name, body):
    with open(os.path.join(REQ, name + ".json"), "w", encoding="utf-8") as f:
        json.dump(body, f, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    os.makedirs(REQ, exist_ok=True)
    jobs = []
    for name, desc in PROPS.items():
        write(name + "_concept", {"prompt": PROP_STYLE + " " + desc, "aspect_ratio": "1:1", "ai_model": "nano-banana-2",
                                  "remove_background": True})
        jobs.append({"kind": "text-to-image", "name": name + "_concept", "request": f"ArtSource/Meshy/requests/{name}_concept.json"})
    for name, desc in TEXTURES.items():
        write(name, {"prompt": desc, "aspect_ratio": "1:1", "ai_model": "nano-banana-2"})
        jobs.append({"kind": "text-to-image", "name": name, "request": f"ArtSource/Meshy/requests/{name}.json"})
    for name, desc in NPCS.items():
        write(name + "_concept", {"pose_mode": "a-pose", "remove_background": True, "prompt": NPC_STYLE + " " + desc, "ai_model": "nano-banana-2"})
        jobs.append({"kind": "text-to-image", "name": name + "_concept", "request": f"ArtSource/Meshy/requests/{name}_concept.json"})
    with open(os.path.join(REQ, "hall_concepts.jobs.json"), "w", encoding="utf-8") as f:
        json.dump(jobs, f, indent=1)
    print(len(jobs), "concept jobs written (", len(jobs) * 6, "credits )")
