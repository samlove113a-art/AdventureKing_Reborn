"""Data-driven pipeline for the pixel-art heroes (Swordsman done by hand with the same steps).

  python pixel_class.py plan   <Class>            # clip table + credit estimate, spends nothing
  python pixel_class.py ref    <Class> N --yes    # N reference images (text-to-image, 6 credits each)
  python pixel_class.py gen    <Class> --yes      # 9 clip sheets via image-to-image from ArtSource/Meshy/ref/<Class>.png (6 each)
  python pixel_class.py process <Class>           # cut / key / align -> ArtSource/Chibi2D/_Meshy2D/<Class>/ + frames.json
  python pixel_class.py install <Class>           # copy to ArtSource/Chibi2D/<Class>/ (old files stay in git) -> then run Unity build

Run with Blender's python; PYTHONPATH must contain Pillow; MESHY_API_KEY from the User env var (never printed).
Credits are only spent by `ref` and `gen`, and only with --yes (project rule: ask the owner first).
"""
import base64
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
MESHY = os.path.join(ROOT, "Tools", "meshy", "meshy.py")
REQ = os.path.join(ROOT, "ArtSource", "Meshy", "requests")
RAW = os.path.join(ROOT, "ArtSource", "Meshy", "raw")
REFDIR = os.path.join(ROOT, "ArtSource", "Meshy", "ref")
OUT = os.path.join(ROOT, "ArtSource", "Chibi2D", "_Meshy2D")
FINAL = os.path.join(ROOT, "ArtSource", "Chibi2D")
CREDIT = 6

STYLE = ("high-detail 2D pixel art character sprite, dark fantasy action RPG, strict side view facing right, crisp discrete "
         "pixels with NO anti-aliasing, NO blur, NO smooth gradients, NO vector look, NO 3D render look, dark selective outline, "
         "hand-pixeled clustered shading with 3-4 tones per material, light from the top-left, rich but slightly dark palette, "
         "slender athletic heroic proportions (head about 1/6 of body height, NOT chibi, NOT a short stubby body), strong readable "
         "silhouette, consistent pixel size, weapon fully visible, flat solid magenta #FF00FF background, no text, no grid, no shadow")
SAME = ("Same character as the reference image, identical face, hair, outfit, colours and weapon in every frame. High-detail 2D pixel "
        "art, crisp pixels, dark selective outline, clustered shading, same pixel size and same character scale in every frame, "
        "character fully inside each cell, flat solid magenta #FF00FF background, thin black lines between cells, no text, no "
        "shadow. Strict side view facing right unless stated. EVERY cell shows the COMPLETE character from the top of the head to the soles of the boots at EXACTLY the same size and scale as the reference: no zoom-in, no close-ups, no cropped feet, no cell with a bigger or smaller character.")

CLASSES = {
    "Mage": dict(
        ref=("a young woman, a slender female battle mage with a clearly feminine face and a curvy hourglass figure with a fuller bust, fully clothed and tasteful (robe covers the chest, no cleavage), fitted layered midnight-purple robe "
             "with ivory and gold trim, a high collar and a long slit skirt over dark leggings and boots, wide-brimmed pointed "
             "hat with a gold crescent emblem, long silver-white hair, glowing violet eyes, leather belts with a spell-book and potion "
             "vials, a tall ornate staff topped by a glowing crystal held in the right hand. Palette: deep purple, ivory, gold, cyan crystal glow"),
        attack="STAFF ATTACK: raise the staff, thrust it forward casting a hard-edged pixel arcane bolt, follow-through, recovery",
        skill="SKILL: charge a pixel magic circle under the feet, raise the staff, release a large hard-edged pixel arcane blast / meteor of violet and cyan fire",
        stash="staff held in one hand"),
    "Assassin": dict(
        ref=("a lean agile assassin, hooded dark-teal leather armor with a scarf over the lower face, layered straps, bracers, light "
             "pauldron, dark cloth cape short and tattered, two curved daggers held in reverse grip, crouched ready stance, glowing "
             "teal eyes under the hood. Palette: teal, black, silver steel, a little dark red"),
        attack="DAGGER ATTACK: quick double slash with the two daggers, step forward, hard-edged pixel silver slash arcs, recovery",
        skill="SKILL: vanish-dash forward with afterimages, then a spinning multi-slash with hard-edged pixel teal and white slash arcs",
        stash="daggers sheathed at the hips"),
    "Archer": dict(
        ref=("a young woman, a slender female ranger archer with a beautiful, attractive, delicate face with large expressive eyes and a curvy hourglass figure with a fuller bust (fully clothed and tasteful, no cleavage), fitted forest-green and brown leather armor with a short skirt over leggings and tall boots, a feathered cap with long hair in a ponytail, a hooded cloak with leaf trim, "
             "quiver of fletched arrows on the back, leather bracers and boots, an ornate recurve bow held in the left hand, "
             "sharp focused eyes. Palette: forest green, leather brown, gold trim, cream feathers"),
        attack="BOW ATTACK: draw the string back with an arrow, aim, release the arrow (hard-edged pixel arrow in flight), recovery",
        skill="SKILL: draw three arrows at once, leap back, release a fan of glowing hard-edged pixel arrows with green and gold light trails",
        stash="bow slung over the shoulder"),
}

# name, frames (0 = whatever the sheet really has), layout, aspect ratio, fps, loop, template, whiten index
CLIPS = [
    ("Idle", 6, "a grid of 3 columns and 2 rows, equal cells", "16:9", 8, True, "IDLE breathing loop: standing ready, gentle body bob, cloak/hair swaying slightly between frames", -1),
    ("Run", 8, "a grid of 4 columns and 2 rows, equal cells", "16:9", 14, True, "RUN CYCLE: full sprint, cloak flowing behind, clear foot contact and passing poses", -1),
    ("Jump", 4, "a grid of 2 columns and 2 rows, equal cells", "1:1", 10, False, "JUMP: crouch to charge, push off, rising, apex of the jump", -1),
    ("Fall", 3, "one horizontal row of 3 equal cells", "16:9", 8, False, "FALLING: descending with arms spread, cloak flying upward, legs slightly bent", -1),
    ("Climb", 8, "a grid of 4 columns and 2 rows, equal cells", "16:9", 10, True,
     "CLIMBING seen from behind in three-quarter back view: arms reaching up and alternating, knees lifting alternately, {stash}. IMPORTANT: NO ladder, NO rope, NO wall, NO props, only the character", -1),
    ("Attack", 6, "a grid of 3 columns and 2 rows, equal cells", "16:9", 14, False, "{attack}", -1),
    ("Skill", 8, "a grid of 4 columns and 2 rows, equal cells", "16:9", 12, False, "{skill}", -1),
    ("Hit", 3, "one horizontal row of 3 equal cells", "16:9", 10, False, "HIT REACTION: knocked back, wincing face (the middle frame is replaced by a white flash in code)", 1),
    ("Death", 0, "a grid of 4 columns and 2 rows, equal cells (6 or 7 frames)", "16:9", 7, False,
     "DEATH: staggering, collapsing to the knees, falling backward and lying flat on the ground, weapon dropped beside", -1),
]


# NPCs: the game plays Idle / Walk / Wave / Talk / Bow (NpcActor, NpcTalkSpot). target = hero-relative size (136 = full hero cell).
NPC_CLIPS = [
    ("Idle", 6, "a grid of 3 columns and 2 rows, equal cells", "16:9", 8, True, "IDLE breathing loop: standing relaxed, subtle body bob, clothing and hair swaying slightly between frames", -1),
    ("Walk", 8, "a grid of 4 columns and 2 rows, equal cells", "16:9", 10, True, "WALK CYCLE: calm walking, clear foot contact and passing poses", -1),
    ("Wave", 6, "a grid of 3 columns and 2 rows, equal cells", "16:9", 10, False, "WAVE: raising one hand and waving a friendly greeting, then lowering it", -1),
    ("Talk", 8, "a grid of 4 columns and 2 rows, equal cells (a WIDE SHOT: small full-body figures with empty space around them)", "16:9", 8, True, "TALKING: animated speaking with expressive hand gestures, head nodding slightly. IMPORTANT: FULL BODY from head to boots always visible and uncropped, standing at the same scale as the reference, NOT a close-up or portrait", -1),
    ("Bow", 6, "a grid of 3 columns and 2 rows, equal cells", "16:9", 8, False, "POLITE BOW: bending forward at the waist in a respectful bow and rising back up", -1),
]
NPCS = {
    "NpcElder": dict(target=118, ref=("an elderly village elder, kind wise old man, bald head with a long flowing white beard and white eyebrows, small round "
                                       "glasses, slightly stooped, long teal-green robe with cream trim and a deep purple vest, brown belt and sturdy boots. "
                                       "Palette: teal, purple, cream, brown, white beard")),
    "NpcSmith": dict(target=126, ref=("a young village blacksmith, sturdy broad-shouldered young man with short messy auburn hair and a goggles headband on his "
                                       "forehead, soot-smudged face, rolled-up rust-brown shirt, thick brown leather apron, leather gloves and bracers, a "
                                       "smith's hammer in his right hand, dark heavy boots. Palette: rust brown, leather, dark iron gray, a little orange")),
    "NpcStorekeeper": dict(target=124, ref=("a friendly young village shopkeeper, slim man with black hair in a small topknot, a warm smile, olive-green "
                                             "linen shirt with rolled sleeves, a brown cloth apron with a small coin pouch at the belt, cream trousers, brown "
                                             "boots. Palette: olive green, cream, brown, black hair")),
    "NpcCaptain": dict(target=132, ref=("a guard captain, tall disciplined young soldier with silver-blue braided hair, blue-and-gold plate armor with steel "
                                         "pauldrons, a short blue cape with gold trim, leather gloves, a sheathed sword at the hip, upright proud posture, "
                                         "stern eyes. Palette: royal blue, gold, steel gray, silver hair")),
    "NpcOfficiant": dict(target=120, ref=("a gentle elderly wedding officiant, white hair in a neat bun, a short white beard, long cream-and-gold ceremonial "
                                           "robe with golden embroidery and a soft golden sash, a small holy book in one hand, kind serene face. Palette: "
                                           "cream white, soft gold, pale blue accents")),
    "NpcMusician": dict(target=122, ref=("a graceful young woman musician, a slender female bard with a beautiful delicate face, long wavy brown hair with a "
                                          "pink flower hair clip, teal short jacket over a pink layered skirt with leggings and brown boots, a small lute "
                                          "slung on her back, fully clothed and tasteful. Palette: teal, rose pink, brown, cream")),
    "NpcMushroomElder": dict(target=132, ref=("a mushroom-folk elder, a stout old forest sage wearing an enormous red mushroom-cap hat with cream spots, a "
                                               "long flowing white beard, teal tunic with a mossy green patchwork vest and cream hem, brown boots, a "
                                               "gnarled wooden staff. Palette: mushroom red, cream, teal, moss green, white beard")),
}
for _n, _d in NPCS.items():
    CLASSES[_n] = _d


# Monsters: FarmMonsterView CrossFades by state name: Idle / Walk / Attack / Hit / Death. target = size in "hero px" (82 = 0.82 m, the old in-game size).
MONSTER_CLIPS = [
    ("Idle", 6, "a grid of 3 columns and 2 rows, equal cells", "16:9", 8, True, "IDLE loop: the creature breathing / bobbing in place, subtle body movement between frames", -1),
    ("Walk", 6, "a grid of 3 columns and 2 rows, equal cells", "16:9", 10, True, "WALK / MOVE CYCLE: the creature moving forward, clear step or hop poses", -1),
    ("Attack", 6, "a grid of 3 columns and 2 rows, equal cells", "16:9", 12, False, "ATTACK: {attack}", -1),
    ("Hit", 3, "one horizontal row of 3 equal cells", "16:9", 10, False, "HIT REACTION: flinching backward in pain (the middle frame is replaced by a white flash in code)", 1),
    ("Death", 6, "a grid of 3 columns and 2 rows, equal cells", "16:9", 8, False, "DEATH: {death}", -1),
    ("Skill", 8, "a grid of 4 columns and 2 rows, equal cells", "16:9", 12, False, "BOSS SPECIAL SKILL: {skill}", -1),
]
MONSTERS = {
    "MonsterNamNon": dict(target=82, attack="lunges forward and head-butts with its mushroom cap, then recovers",
                          death="staggers, deflates, topples over and its cap pops off, ending on a small spore puff on the ground",
                          ref=("a young hostile forest mushroom creature (Nam Non): a short stocky mushroom monster with a large red cap with cream spots, "
                               "a pale cream stalk body with two small angry eyes, a small leather strap across the body, stubby brown feet, sharp little "
                               "fangs, slightly menacing but cute dark-fantasy forest monster, side view. Palette: red cap, cream, brown, dark outline")),
}
MONSTERS["PollenSlime"] = dict(target=52, clips=["Idle"], attack="", death="",
                               ref=("a cute but slightly sinister forest slime monster (Pollen Slime): a glossy translucent lime-green gelatinous dome-shaped "
                                    "blob lying low on the ground with a wide flat base, bright glossy highlights, a small white daisy flower with a yellow centre "
                                    "and a curly green sprout growing on top of its head, two small dark eyes with white sparkles and tiny pink cheeks, "
                                    "dark-fantasy pixel style, side view. Palette: lime green, darker green shade, white flower, pink cheeks, dark outline"))
MONSTERS["PuffSlime"] = dict(target=43, clips=["Idle"], attack="", death="",
                             ref=("a cute but slightly sinister forest slime monster (Puff Slime): a glossy translucent periwinkle-blue gelatinous dome-shaped "
                                  "blob lying low on the ground with a wide flat base, bright glossy highlights, a small pale-blue puff flower with a white centre "
                                  "and a curly sprout growing on top of its head, two small dark eyes with white sparkles and tiny orange-pink cheeks, "
                                  "dark-fantasy pixel style, side view. Palette: periwinkle blue, darker blue shade, pale blue flower, orange cheeks, dark outline"))
MONSTERS["MossSlime"] = dict(target=52, clips=["Idle"], attack="", death="",
                             ref=("a cute but slightly sinister forest slime monster (Moss Slime): a glossy translucent deep-green gelatinous dome-shaped "
                                  "blob lying low on the ground with a wide flat base covered in a little moss, bright glossy highlights, a small flower with a "
                                  "green centre and a curly sprout growing on top of its head, two small dark eyes with white sparkles and tiny orange cheeks, "
                                  "dark-fantasy pixel style, side view. Palette: deep forest green, moss green, white flower, orange cheeks, dark outline"))
MONSTERS["GlowMoss"] = dict(target=85, attack="hops forward and bursts a small cloud of glowing cyan spores from its cap, then recovers",
                            death="stumbles, its glow flickers out, it sinks down and topples over, the cap dimming to dull gray-green",
                            ref=("a small hostile glowing forest mushroom creature (Glow Moss): a round bioluminescent turquoise-cyan mushroom cap with lighter "
                                 "pale-cyan spots that glows softly, a short pale mint-white stalk body with two small dark eyes and a tiny frown, stubby "
                                 "brown feet, slightly eerie cave-forest dark-fantasy monster, side view. Palette: turquoise cyan glow, pale mint, brown, dark outline"))
MONSTERS["FlowerBee"] = dict(target=82, attack="darts forward and jabs with its stinger, then buzzes back",
                             death="loses lift, its wings stop, it spins down and lands upside-down on the ground motionless",
                             ref=("a hostile giant forest bee monster (Flower Bee): a chubby round bee body with black and golden-yellow stripes, a pink flower-petal "
                                  "wing pair (translucent pink-white), a round head with an angry face, curled antennae, a sharp stinger at the tail, tiny "
                                  "legs, hovering in the air, side view facing right, dark-fantasy pixel style. Palette: golden yellow, black, pink wings, dark outline"))
MONSTERS["CrystalBat"] = dict(target=71, attack="swoops forward with fangs bared and slashes with its crystal wing tips, then flutters back",
                              death="its wings fold, crystals dim and crack, it tumbles down and lies motionless on the ground",
                              ref=("a hostile crystal cave bat monster (Crystal Bat): a plump indigo-purple bat body with a round head, two sharp pointed horns, big "
                                   "yellow glowing eyes, small fangs, and two wings made of pale cyan-blue crystal shards with glassy facets, hovering in the air, "
                                   "side view facing right, dark-fantasy pixel style. Palette: deep indigo purple, cyan crystal wings, yellow eyes, dark outline"))
MONSTERS["JadeTurtle"] = dict(target=75, attack="snaps forward with an extended neck and bites, then pulls its head back",
                              death="retreats into its shell, the jade glow dims, the shell tips over and lies still",
                              ref=("a hostile jade turtle monster (Jade Turtle): a stout teal-green turtle with a domed jade-green shell studded with four pale-mint "
                                   "jade gems, a round green head with a grumpy face and small pink cheeks, four stubby green feet, walking on the ground, "
                                   "side view facing right, slightly menacing dark-fantasy pixel style. Palette: teal green, mint jade, dark green, dark outline"))
MONSTERS["ShadowLeaf"] = dict(target=60, attack="darts forward and slashes with its sharp leaf-blade tip, trailing a dark shadow smear, then glides back",
                              death="its leaf body curls and withers, the shadow dissolves into dark wisps, and it falls to the ground as a dry crumpled leaf",
                              ref=("a hostile shadow leaf creature (Shadow Leaf): a sleek elongated dark-purple leaf-shaped body like a floating blade, with a glowing "
                                   "narrow pale slit eye, a sharp pointed leaf-tip snout pointing right, a clearly visible face with a large glowing pale-yellow angry eye and a row of tiny sharp teeth, a pale vein line along its body, small dripping shadow "
                                   "droplets underneath, gliding just above the ground, side view facing right, eerie dark-fantasy pixel style. "
                                   "Palette: deep violet purple, lilac highlights, pale eye, dark outline"))
MONSTERS["ToxicSlime"] = dict(target=72, attack="lunges forward and spits a glob of toxic green goo, then wobbles back",
                              death="bubbles and melts into a flat puddle of toxic goo with the little purple mushroom cap left floating on top",
                              ref=("a hostile toxic slime monster (Toxic Slime): a wide low blob of glossy poison-green gelatinous goo with a few bubbles and "
                                   "darker green spots inside, a small purple spotted mushroom growing on top of its head, two small dark eyes with a mean "
                                   "frown and a tiny dark mouth, dripping toxic green goo at the edges, sitting on the ground, side view facing right, "
                                   "dark-fantasy pixel style. Palette: toxic lime green, darker green, purple mushroom cap with cream spots, dark outline"))
MONSTERS["ToxicElite"] = dict(target=149, attack="rears up and slams down with its spiked cap, releasing a puff of toxic green spores, then recovers",
                              death="roars, its spikes droop and the purple cap cracks, it topples over and lies still releasing a last green spore cloud",
                              ref=("an elite hostile poisonous mushroom monster (Toxic Elite): a stout muscular mushroom brute with a big dark-purple cap covered in "
                                   "sharp toxic-green spikes along its top and glowing lime spots, a pale lavender stalk body with two glowing yellow-green angry "
                                   "eyes, a fanged frown, thick arms, and heavy brown feet, menacing dark-fantasy pixel style, side view facing right. "
                                   "Palette: dark purple, toxic lime green spikes, pale lavender, brown, dark outline"))
MONSTERS["StoneGuard"] = dict(target=127, attack="raises a heavy stone fist and slams it down in front of itself, then straightens up",
                              death="its glowing green core flickers out, the stone body cracks, crumbles and collapses into a heap of rubble with a small sprout left on top",
                              ref=("an ancient stone golem guardian (Stone Guard): a blocky humanoid golem built from rough gray-brown stone slabs with moss in the "
                                   "cracks, a rectangular head with two glowing orange-red angry slit eyes under a heavy scowling brow, a jagged snarling mouth crack with stone fangs, jagged stone spikes on its shoulders and knuckles, deep cracks glowing faintly red, a glowing green gem in the center of its chest, thick heavy "
                                   "arms and short legs, a tiny green sprout growing on top of its head, standing upright, side view facing right, "
                                   "dark-fantasy pixel style. Palette: warm gray stone, moss green, glowing green gem, yellow eyes, dark outline"))
MONSTERS["RoyalGuard"] = dict(target=92, attack="thrusts its long spear forward in a sharp stab, then pulls it back",
                              death="drops its spear, the shield-cap cracks, it falls to its knees and collapses face-down, the spear clattering beside it",
                              ref=("a majestic, dignified elite royal guard mushroom knight (Royal Guard), standing tall and upright with a proud commanding stance: a mushroom warrior whose cap is a round crimson-red shield with gold trim and a "
                                   "silver-gray center boss and a sharp red spike on top, a grim armored face with two small angry eyes under the rim, a pale "
                                   "gray ornate plate-armored body with engraved gold-trimmed steel pauldrons and a short crimson tabard, tall greaves and boots, a plumed crimson crest, and a long gold-ornamented steel-tipped spear held upright-forward in its right hand, "
                                   "side view facing right, menacing dark-fantasy pixel style. Palette: crimson red, steel gray, dark gray, dark outline"))
MONSTERS["DarkMushroom"] = dict(target=204, cell=384, attack="rears back then slams forward with its horned cap in a violent head-butt, dark spores bursting out, then recovers",
                                death="roars, its horns crack and the cap sags, it keels over backward and lies still while a cloud of dark spores rises",
                                skill="DARK SPORE ERUPTION: it raises both huge arms overhead while dark crimson energy gathers glowing inside its cap, then slams both fists into the ground and a hard-edged pixel ring shockwave of dark crimson spores and jagged spiked roots erupts across the ground in front of it",
                                ref=("a COLOSSAL, hulking, extremely powerful dark mushroom boss (Dark Mushroom): a gigantic muscular corrupted mushroom monster, very broad and heavy, "
                                     "with an enormous black-purple cap studded with glowing crimson-red spots and three huge curved pale-bone horns, a massive "
                                     "bulging-muscled pale lavender body with arms thick as tree trunks and giant clawed fists, glaring blazing crimson eyes, a "
                                     "huge mouth full of sharp fangs, a faint crimson aura around it, cracked ground under its heavy feet, a wide menacing "
                                     "power stance, overwhelming and terrifying dark-fantasy pixel style, side view facing right. "
                                     "Palette: black-purple, crimson red glow, bone white horns, pale lavender, dark outline"))
MONSTERS["MushroomChief"] = dict(target=340, cell=384, attack="swings its heavy gnarled wooden staff in a wide crushing overhead strike, then recovers",
                                 death="its staff drops, the leaf crown wilts, the great cap sags and it collapses to its knees then falls face-forward, spores drifting up",
                                 skill="WAR CALL: it raises its glowing purple-orb staff high and slams it into the ground, a hard-edged pixel ring of purple energy and thorny green roots bursts outward while the leaf crown flares",
                                 ref=("a COLOSSAL, hulking, extremely powerful mushroom tribe chieftain boss (Mushroom Chief): a gigantic broad-shouldered muscular "
                                      "mushroom warlord with an enormous rust-brown cap spotted with cream, a crown of sharp green leaves on top of the cap, a "
                                      "heavy tattered dark-green war cape, a thick bulging-muscled cream body with deep battle scars, massive tree-trunk arms and giant fists, an immense, monstrous, towering hulking silhouette that fills the whole canvas and dwarfs everything, a swirling aura of dark purple energy and drifting glowing green toxic spores and floating embers surrounding its whole body, cracks of purple light in the ground beneath its giant feet, glowing purple veins pulsing across its muscles, glaring blood-red menacing eyes, a snarling mouth with huge tusks, a crown of iron spikes and bones tied to the cap, "
                                      "tusked mouth, wielding a huge gnarled wooden staff topped by a glowing purple orb, a wide commanding power stance, "
                                      "overwhelming dark-fantasy pixel style, side view facing right. "
                                      "Palette: rust brown, cream, leaf green, dark green cape, glowing purple, dark outline"))
MONSTERS["JadeGuardian"] = dict(target=340, cell=384, gen_extra="Keep the same glowing green jade aura, floating jade shards and energy sparks around the body in EVERY frame of EVERY animation, including the idle.",
                                attack="rears back both massive jade fists then smashes them down in a devastating double hammer blow, green shockwave sparks at the impact",
                                death="its glowing core flickers and dies, the jade body cracks along glowing fault lines, shatters into huge chunks and collapses into a heap of broken jade rubble",
                                skill="JADE QUAKE: it raises both enormous fists overhead as the glowing jade core in its chest blazes, then slams the ground and a hard-edged pixel ring of green shockwave and jagged jade crystal spikes erupts outward across the ground",
                                ref=("a COLOSSAL, towering, monstrous ancient jade golem boss guardian (Jade Guardian): a gigantic hulking blocky stone-and-jade titan, "
                                     "extremely broad and heavy, built from massive carved teal-blue jade slabs with glowing green runes and cracks, a huge "
                                     "glowing green gem core blazing in its chest, jagged jade crystal spikes on its shoulders, back and knuckles, enormous "
                                     "fists like boulders, a blocky scowling head with two blazing yellow-green eyes and a jagged fanged mouth slit, a "
                                     "swirling aura of green energy, floating jade shards and glowing sparks surrounding its whole body, cracked glowing "
                                     "ground under its giant feet, an immense menacing power stance that fills the whole canvas, "
                                     "overwhelming dark-fantasy pixel style, side view facing right. "
                                     "Palette: teal-blue jade, glowing emerald green, dark stone, yellow-green eyes, dark outline"))
for _n, _d in MONSTERS.items():
    CLASSES[_n] = _d


# frames to keep when the generator drew junk at the end of a sheet: (actor, clip) -> indexes
KEEP = {("MonsterNamNon", "Death"): "0,1,2,3,3", ("PollenSlime", "Idle"): "0,1,2,4,5,4", ("JadeTurtle", "Death"): "0,1,2,3,5", ("ToxicSlime", "Hit"): "0,1,2"}


# extra processor flags per (actor, clip), e.g. drop the ground line the generator drew under the creature
ACTOR_FLAGS = {("FlowerBee", "Walk"): ["--deline"]}


def clips_for(cls):
    if cls in MONSTERS:
        only = MONSTERS[cls].get("clips")
        return [c for c in MONSTER_CLIPS if (c[0] in only if only else (c[0] != "Skill" or MONSTERS[cls].get("skill")))]
    return NPC_CLIPS if cls in NPCS else CLIPS


# per-clip processor flags: drop ground lines the generator draws, keep only the hero (projectile is separate VFX), anchor on the body
EXTRA = {"Jump": ["--deline"], "Attack": ["--hero-only", "--anchor-body"], "Skill": ["--anchor-body", "--copy", "6:5"]}


def py():
    return sys.executable


def env():
    e = dict(os.environ)
    if not e.get("MESHY_API_KEY"):
        sys.exit("MESHY_API_KEY not set (load it from the User environment variable)")
    return e


def meshy_run(kind, name, body):
    os.makedirs(REQ, exist_ok=True)
    p = os.path.join(REQ, name + ".json")
    with open(p, "w", encoding="utf8") as f:
        json.dump(body, f)
    subprocess.check_call([py(), MESHY, "run", kind, name, p], env=env())
    return os.path.join(RAW, name, "image_0.png")


def need_yes(n, what):
    print(f"{what}: {n} call(s) x {CREDIT} = {n * CREDIT} credits")
    if "--yes" not in sys.argv:
        sys.exit("refused: add --yes only after the owner approved this spend and the balance was checked")


def cmd_plan(cls):
    c = CLASSES[cls]
    cl = clips_for(cls)
    print(f"{cls}: 1 reference (x2 tries) + {len(cl)} clip sheets = {(2 + len(cl)) * CREDIT} credits (+{CREDIT} per redo)")
    for n, f, lay, ar, fps, loop, tpl, wh in cl:
        print(f"  {n:7s} frames {f or 'auto'}  {lay}  {ar}  {fps} fps  loop={loop}")


def cmd_ref(cls):
    n = int(sys.argv[3]) if len(sys.argv) > 3 and sys.argv[3].isdigit() else 1
    need_yes(n, "reference images")
    for i in range(1, n + 1):
        body = dict(prompt=f"{STYLE}. Single character reference, standing idle pose, full body, canvas square, character about 80% of the height. "
                           f"{CLASSES[cls]['ref']}.", ai_model="nano-banana-2", remove_background=False)
        print(meshy_run("text-to-image", f"pixel_{cls.lower()}_ref{i}", body))
    print("pick one and copy it to", os.path.join(REFDIR, cls + ".png"))


def cmd_gen(cls):
    ref = os.path.join(REFDIR, cls + ".png")
    if not os.path.exists(ref):
        sys.exit("missing " + ref)
    only = sys.argv[sys.argv.index("--only") + 1].split(",") if "--only" in sys.argv else None
    todo = [cl for cl in clips_for(cls) if not only or cl[0] in only]
    need_yes(len(todo), "clip sheets")
    b64 = base64.b64encode(open(ref, "rb").read()).decode()
    c = CLASSES[cls]
    for n, f, lay, ar, fps, loop, tpl, wh in todo:
        desc = tpl.format(**{"attack": "", "skill": "", "stash": "", "death": "", **c})
        fr = f"{f} frames" if f else "6 or 7 frames"
        body = dict(prompt=f"{SAME} Create a flat 2D sprite sheet image that fills the ENTIRE canvas edge to edge (not a tilted screen, no perspective, no mockup, no grey surround) of {fr} arranged in {lay}, {desc}. Feet on the same baseline in every cell. {(MONSTERS.get(cls) or {}).get('gen_extra', '')}",
                    ai_model="nano-banana-2", reference_image_urls=["data:image/png;base64," + b64], aspect_ratio=ar)
        print(meshy_run("image-to-image", f"pixel_{cls.lower()}_{n.lower()}", body))


def cmd_process(cls):
    od = os.path.join(OUT, cls)
    os.makedirs(od, exist_ok=True)
    cellpx = int((MONSTERS.get(cls) or {}).get("cell", 192))   # bosses: 384 (same 101 px per metre)
    meta = {"cell": cellpx, "pixel": True, "frame_m": round(cellpx / 101.05, 3), "ground_from_bottom_m": round(0.099 * cellpx / 192, 4), "clips": {}}
    for n, f, lay, ar, fps, loop, tpl, wh in clips_for(cls):
        src = os.path.join(RAW, f"pixel_{cls.lower()}_{n.lower()}", "image_0.png")
        cnt = f if f else 7
        args = [py(), os.path.join(HERE, "process_pixel_strip.py"), src, od, cls, n, str(cnt)]
        if wh >= 0:
            args += ["--whiten", str(wh)]
        args += ACTOR_FLAGS.get((cls, n), [])
        if cellpx != 192:
            args += ["--cell", str(cellpx)]
        if (cls, n) in KEEP:
            args += ["--keep", KEEP[(cls, n)]]
        if cls in NPCS or cls in MONSTERS:
            args += ["--target", str((NPCS.get(cls) or MONSTERS[cls])["target"]), "--single"] + (["--fit-first"] if cls in MONSTERS else [])
        else:
            args += EXTRA.get(n, [])
        # frames=0 -> try 7 then 6 (the generator draws either)
        for attempt in dict.fromkeys(([cnt] if f else []) + [12, 11, 9, 10, 8, 7, 6, 5, 4, 3, 2]):   # the generator sometimes draws a different count
            args[6] = str(attempt)
            if subprocess.call(args) == 0:
                cnt = int(open(os.path.join(od, f"{cls}_{n}.count")).read())   # real frame count (empty cells dropped)
                break
        else:
            sys.exit("could not cut " + n)
        meta["clips"][n] = {"frames": cnt, "fps": fps, "loop": loop, "file": f"{cls}_{n}.png"}
    with open(os.path.join(od, f"{cls}_frames.json"), "w", encoding="utf8") as fh:
        json.dump(meta, fh, indent=2)
    print("processed ->", od)


def cmd_install(cls):
    od, dd = os.path.join(OUT, cls), os.path.join(FINAL, cls)
    os.makedirs(dd, exist_ok=True)
    for n, *_ in clips_for(cls):
        shutil.copy(os.path.join(od, f"{cls}_{n}.png"), os.path.join(dd, f"{cls}_{n}.png"))
    shutil.copy(os.path.join(od, f"{cls}_frames.json"), os.path.join(dd, f"{cls}_frames.json"))
    print("installed ->", dd, "(git keeps the previous files); now run Chibi2DArt.BuildPixelClassesFromCommandLine")


if __name__ == "__main__":
    if len(sys.argv) < 3 or sys.argv[2] not in CLASSES:
        sys.exit(__doc__)
    {"plan": cmd_plan, "ref": cmd_ref, "gen": cmd_gen, "process": cmd_process, "install": cmd_install}[sys.argv[1]](sys.argv[2])

























