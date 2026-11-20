"""Full animation cycles for the pets: 5 extra frames each (6-frame loop with the main picture), one Meshy image-to-image call per frame (6 credits).

  "<Blender>/5.2/python/bin/python.exe" Tools/costume/pet_frames_gen.py gen [petId ...] [--workers 6]
  "<Blender>/5.2/python/bin/python.exe" Tools/costume/pet_frames_gen.py fit [petId ...]

gen : reference = the pet's original high-resolution picture (ArtSource/Meshy/raw/...), prompt = the phase of the walk / flap / float / hop cycle. Needs MESHY_API_KEY (User env).
fit : cuts the creature out of every raw result, matches its area to the pet's main picture, aligns it on the feet line and writes Assets/Resources/Pets/<petId>_b.._f.png.
Then run Unity AdventureKing.ArtPipeline.VisualDatabases.BuildFromCommandLine to add the frames to the pet database. Nothing is drawn by code.
"""
import base64
import concurrent.futures as cf
import io
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "ArtSource", "Meshy", "raw")
REQ = os.path.join(ROOT, "ArtSource", "Meshy", "requests")
PETS = os.path.join(ROOT, "Assets", "Resources", "Pets")
MESHY = os.path.join(ROOT, "Tools", "meshy", "meshy.py")
SRC = {"pet_wind_unicorn": "mount2d_white_horse", "pet_ember_tiger": "mount2d_fire_tiger", "pet_night_horse": "mount2d_night_horse",
       "pet_rainbow_cloud": "mount2d_cloud", "pet_snow_wolf": "mount2d_snow_wolf", "pet_ink_dragon": "pet_ink_dragon_v2"}
FLY = {"pet_war_dragon", "pet_phoenix", "pet_night_owl"}
FLOAT = {"pet_lantern_wisp", "pet_rainbow_cloud"}
HOP = {"pet_slime_jelly", "pet_mushroom_walker", "pet_chick", "pet_radish_bunny"}
BIG = {"pet_war_dragon", "pet_ink_dragon"}

PHASES = {
    "WALK": ["the FRONT foot reaching far forward, body stretched, tail swinging back",
             "the body at its LOWEST, weight on the front foot, front leg bent",
             "the legs PASSING under the body, all four feet close together, body slightly raised",
             "the BACK foot pushing off behind, body at its HIGHEST, tail swinging up",
             "both legs gathering mid-stride, body level, ready to loop back to the first frame"],
    "FLY": ["the wings RISING, half raised, body slightly lifting",
            "the wings at the TOP of the beat, fully raised and spread wide, body highest",
            "the wings SWEEPING DOWN, half way down, body dropping slightly",
            "the wings at the BOTTOM of the beat, fully down, body lowest, tail trailing up",
            "the wings starting to RISE again, nearly the same as the first frame"],
    "FLOAT": ["drifting a few pixels UP, the body puffed slightly, tail / wisp waving to the left",
              "at the TOP of the drift, the body at its most puffed, tail / wisp flowing straight back",
              "drifting DOWN, the body relaxing, tail / wisp waving to the right",
              "at the BOTTOM of the drift, the body slightly squeezed, tail / wisp curling up",
              "drifting up again, nearly the same as the first frame"],
    "HOP": ["CROUCHING low, body squashed wide and short, ears / sprout / scarf pressed down",
            "PUSHING OFF, body stretching upward, leaving the ground",
            "IN THE AIR at the peak, body stretched tall, ears / sprout / scarf flying",
            "FALLING, body still stretched, ears / sprout / scarf lifting",
            "LANDING, body squashed wide again, then recovering to the first frame"],
}
KEEP = (" Keep EVERYTHING else identical: the same creature, the same design, colours, markings, the whole tail and all limbs fully drawn and attached, the same side view facing right, "
        "the same size and the same position in the picture. Crisp pixel art with NO anti-aliasing and NO blur, dark outline, transparent background, no stray fragments, no text.")


def kind(pid):
    return "FLY" if pid in FLY else "FLOAT" if pid in FLOAT else "HOP" if pid in HOP else "WALK"


def pet_ids():
    with open(os.path.join(ROOT, "Server", "AdventureKing.Server", "config", "pets.json"), encoding="utf-8-sig") as fh:
        return [p["id"] for p in json.load(fh)["pets"]]


def frame_name(pid, n):
    return f"frame_{pid}_{n}"


def job(pid, n):
    name = frame_name(pid, n)
    out = os.path.join(RAW, name, "image_0.png")
    if os.path.exists(out):
        return name, "skip"
    ref = os.path.join(RAW, SRC.get(pid, pid), "image_0.png")
    b = io.BytesIO()
    Image.open(ref).convert("RGBA").save(b, "PNG")
    uri = "data:image/png;base64," + base64.b64encode(b.getvalue()).decode()
    k = kind(pid)
    word = {"WALK": "walk cycle", "FLY": "wing-beat cycle", "FLOAT": "floating cycle", "HOP": "hopping cycle"}[k]
    prompt = f"Edit this exact pixel-art creature: show it in frame {n + 1} of 6 of a smooth looping {word}: {PHASES[k][n - 1]}." + KEEP
    req = os.path.join(REQ, name + ".json")
    with open(req, "w", encoding="utf-8") as fh:
        json.dump(dict(ai_model="nano-banana-2", remove_background=True, reference_image_urls=[uri], prompt=prompt), fh)
    r = subprocess.run([sys.executable, MESHY, "run", "image-to-image", name, req], capture_output=True, text=True)
    return name, "ok" if os.path.exists(out) else "FAILED " + (r.stdout + r.stderr)[-160:].replace("\n", " ")


SEL = [1, 3, 4]          # economical default: 3 key phases per pet (4-frame loop with the main picture); pass --full for all 5


def gen(ids, workers):
    jobs = [(p, n) for p in ids for n in SEL]
    print(len(jobs), "frames, at most", len(jobs) * 6, "credits", flush=True)
    with cf.ThreadPoolExecutor(workers) as ex:
        for name, st in ex.map(lambda a: job(*a), jobs):
            print(name, st, flush=True)


def load(raw):
    im = Image.open(raw).convert("RGBA")
    im.putalpha(im.getchannel("A").point(lambda v: 255 if v > 40 else 0))
    return im.crop(im.getbbox())


def area(im):
    return int((np.array(im)[..., 3] > 0).sum())


def drop_specks(im, minpx=14):
    a = np.array(im)
    al = a[..., 3] > 0
    lab = np.zeros(al.shape, int)
    sizes = {}
    n = 0
    for y, x in zip(*np.nonzero(al)):
        if lab[y, x]:
            continue
        n += 1
        st = [(y, x)]
        lab[y, x] = n
        c = 0
        while st:
            cy, cx = st.pop()
            c += 1
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < al.shape[0] and 0 <= nx < al.shape[1] and al[ny, nx] and not lab[ny, nx]:
                        lab[ny, nx] = n
                        st.append((ny, nx))
        sizes[n] = c
    big = max(sizes.values()) if sizes else 0
    for k, c in sizes.items():
        if c < minpx and c < big * 0.02:
            a[lab == k, 3] = 0
    out = Image.fromarray(a)
    bb = out.getbbox()
    return out.crop(bb) if bb else out


def fit(ids):
    for pid in ids:
        main_path = os.path.join(PETS, pid + ".png")
        if not os.path.exists(main_path):
            continue
        main = Image.open(main_path).convert("RGBA")
        ref = load(os.path.join(RAW, SRC.get(pid, pid), "image_0.png"))
        scale = (area(main) / max(1, area(ref))) ** 0.5               # the reference's own scale: all frames share it (same canvas, same size)
        big = pid in BIG
        done = 0
        for slot, n in enumerate(SEL):
            p = os.path.join(RAW, frame_name(pid, n), "image_0.png")
            if not os.path.exists(p):
                continue
            im = drop_specks(load(p))
            im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS if big else Image.BOX)
            im.putalpha(im.getchannel("A").point(lambda v: 255 if v > (70 if big else 110) else 0))
            bb = im.getbbox()
            im.crop(bb).save(os.path.join(PETS, f"{pid}_{'bcdef'[slot]}.png"))
            done += 1
        print(pid, done, "frames")


if __name__ == "__main__":
    cmd = sys.argv[1]
    if "--full" in sys.argv:
        SEL[:] = [1, 2, 3, 4, 5]
    rest = [a for a in sys.argv[2:] if not a.startswith("--") and not a.isdigit()]
    ids = rest or [p for p in pet_ids() if p != "pet_ink_dragon"]
    if cmd == "gen":
        gen(ids, int(sys.argv[sys.argv.index("--workers") + 1]) if "--workers" in sys.argv else 6)
    else:
        fit(ids)
