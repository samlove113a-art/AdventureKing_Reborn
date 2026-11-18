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
        "shadow. Strict side view facing right unless stated.")

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
        ref=("a lean ranger archer, forest-green and brown leather armor with a feathered cap, a hooded cloak with leaf trim, "
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
    print(f"{cls}: 1 reference (x2 tries) + {len(CLIPS)} clip sheets = {(2 + len(CLIPS)) * CREDIT} credits (+{CREDIT} per redo)")
    for n, f, lay, ar, fps, loop, tpl, wh in CLIPS:
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
    need_yes(len(CLIPS), "clip sheets")
    b64 = base64.b64encode(open(ref, "rb").read()).decode()
    c = CLASSES[cls]
    for n, f, lay, ar, fps, loop, tpl, wh in CLIPS:
        desc = tpl.format(**c)
        fr = f"{f} frames" if f else "6 or 7 frames"
        body = dict(prompt=f"{SAME} Create a sprite sheet of {fr} arranged in {lay}, {desc}. Feet on the same baseline in every cell.",
                    ai_model="nano-banana-2", reference_image_urls=["data:image/png;base64," + b64], aspect_ratio=ar)
        print(meshy_run("image-to-image", f"pixel_{cls.lower()}_{n.lower()}", body))


def cmd_process(cls):
    od = os.path.join(OUT, cls)
    os.makedirs(od, exist_ok=True)
    meta = {"cell": 192, "pixel": True, "frame_m": 1.9, "ground_from_bottom_m": 0.099, "clips": {}}
    for n, f, lay, ar, fps, loop, tpl, wh in CLIPS:
        src = os.path.join(RAW, f"pixel_{cls.lower()}_{n.lower()}", "image_0.png")
        cnt = f if f else 7
        args = [py(), os.path.join(HERE, "process_pixel_strip.py"), src, od, cls, n, str(cnt)]
        if wh >= 0:
            args += ["--whiten", str(wh)]
        args += EXTRA.get(n, [])
        # frames=0 -> try 7 then 6 (the generator draws either)
        for attempt in dict.fromkeys(([cnt] if f else []) + [8, 7, 6, 5, 4, 3, 2]):   # the generator sometimes draws a different count
            args[6] = str(attempt)
            if subprocess.call(args) == 0:
                cnt = attempt
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
    for n, *_ in CLIPS:
        shutil.copy(os.path.join(od, f"{cls}_{n}.png"), os.path.join(dd, f"{cls}_{n}.png"))
    shutil.copy(os.path.join(od, f"{cls}_frames.json"), os.path.join(dd, f"{cls}_frames.json"))
    print("installed ->", dd, "(git keeps the previous files); now run Chibi2DArt.BuildPixelClassesFromCommandLine")


if __name__ == "__main__":
    if len(sys.argv) < 3 or sys.argv[2] not in CLASSES:
        sys.exit(__doc__)
    {"plan": cmd_plan, "ref": cmd_ref, "gen": cmd_gen, "process": cmd_process, "install": cmd_install}[sys.argv[1]](sys.argv[2])


