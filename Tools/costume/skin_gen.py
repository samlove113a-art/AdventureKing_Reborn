"""Full-body class SKINS through Meshy image-to-image (6 credits per frame; nothing is drawn by code).

  "<Blender>/5.2/python/bin/python.exe" Tools/costume/skin_gen.py gen <Class> <skinKey|all> [--frames Idle:0,Run:2]  [--workers 3]
  "<Blender>/5.2/python/bin/python.exe" Tools/costume/skin_gen.py fit <Class> <skinKey|all>

gen : crops each hero frame (Idle / Run / Jump / Attack, or only --frames), asks Meshy to redesign the OUTFIT in the skin's style, same pose / face / size.
fit : scales + moves every raw result onto the original 192 px frame (feet line + lower-body width) -> Assets/Resources/Fashion/Skins/<Class>/<skinKey>/<Class>_<Clip>_<n>.png
Raw results: ArtSource/Meshy/raw/skin_<Class>_<key>_<Clip>_<n>/image_0.png (git-ignored). Credits are logged by meshy.py in ArtSource/Meshy/manifest.jsonl.
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
ART = os.path.join(ROOT, "Assets", "Art", "Chibi2D")
RAW = os.path.join(ROOT, "ArtSource", "Meshy", "raw")
REQ = os.path.join(ROOT, "ArtSource", "Meshy", "requests")
OUT = os.path.join(ROOT, "Assets", "Resources", "Fashion", "Skins")
MESHY = os.path.join(ROOT, "Tools", "meshy", "meshy.py")
CELL = 192
CLIPS = ["Idle", "Run", "Jump", "Attack"]

STYLE = ("High-detail 2D pixel art, crisp discrete pixels with NO anti-aliasing and NO blur, dark selective outline, hand-pixeled clustered shading, "
         "premium fantasy MMORPG style, light from the top-left, transparent background, no text.")

# skin key -> (set id suffix, style, outfit description). Same order / names as Tools/costume/costume_data.py SETS["Mage"].
SKINS = {
    "Mage": {
        "sakura": ("CUTE", "a cute sakura witch outfit: soft pink and white short frilly dress with big ribbons and a bow, cherry blossom petals, a small pink witch hat with a flower, pink thigh-high stockings, sweet kawaii look"),
        "crimson": ("COOL", "a cool dark gothic blood-queen outfit: black and crimson armoured corset robe with a high collar, spiked shoulder pauldrons, a tall black crown-like horned hat with red gems, edgy badass look"),
        "aurum": ("ANIME", "an anime magical-girl idol outfit: white and gold layered dress with a big gold bow, star motifs, glowing gold trim, a fancy white and gold star hat, sparkling heroine look"),
    },
}


def frames(cls, clip):
    im = Image.open(os.path.join(ART, cls, f"{cls}_{clip}.png")).convert("RGBA")
    return [im.crop((i * CELL, 0, (i + 1) * CELL, CELL)) for i in range(im.width // CELL)]


def name_of(cls, key, clip, i):
    return f"skin_{cls}_{key}_{clip}_{i}"


def job(cls, key, clip, i, fr):
    name = name_of(cls, key, clip, i)
    if os.path.exists(os.path.join(RAW, name, "image_0.png")):
        return name, "skip"
    style, desc = SKINS[cls][key][0], SKINS[cls][key][1]
    b = io.BytesIO()
    fr.resize((768, 768), Image.NEAREST).save(b, "PNG")
    uri = "data:image/png;base64," + base64.b64encode(b.getvalue()).decode()
    prompt = (f"Edit this exact pixel-art character: REDESIGN ONLY HER OUTFIT AND HEADWEAR in a {style} style: {desc}. "
              "Keep EVERYTHING else identical: same pose, same face, same hair colour and hairstyle, same staff, same body proportions, same size, same position in the picture, "
              "same camera angle facing right. " + STYLE)
    req = os.path.join(REQ, name + ".json")
    with open(req, "w", encoding="utf-8") as fh:
        json.dump(dict(ai_model="nano-banana-2", remove_background=True, reference_image_urls=[uri], prompt=prompt), fh)
    r = subprocess.run([sys.executable, MESHY, "run", "image-to-image", name, req], capture_output=True, text=True)
    return name, "ok" if os.path.exists(os.path.join(RAW, name, "image_0.png")) else "FAILED " + r.stdout[-200:] + r.stderr[-200:]


def keys(cls, which):
    return list(SKINS[cls]) if which == "all" else [which]


def gen(cls, which, only, workers):
    jobs = []
    for key in keys(cls, which):
        for clip in CLIPS:
            for i, f in enumerate(frames(cls, clip)):
                if only is None or (clip, i) in only:
                    jobs.append((cls, key, clip, i, f))
    print(len(jobs), "frames,", len(jobs) * 6, "credits at most")
    with cf.ThreadPoolExecutor(workers) as ex:
        for name, st in ex.map(lambda a: job(*a), jobs):
            print(name, st, flush=True)


def bbox(a, rows=None):
    al = a[..., 3] > 40
    if rows is not None:
        m = np.zeros_like(al)
        m[rows[0]:rows[1]] = True
        al = al & m
    ys, xs = np.nonzero(al)
    return (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1) if len(ys) else None


def fit(cls, which):
    for key in keys(cls, which):
        for clip in CLIPS:
            d = os.path.join(OUT, cls, key)
            os.makedirs(d, exist_ok=True)
            for i, orig in enumerate(frames(cls, clip)):
                p = os.path.join(RAW, name_of(cls, key, clip, i), "image_0.png")
                if not os.path.exists(p):
                    continue
                new = Image.open(p).convert("RGBA")
                o = np.array(orig)
                ob = bbox(o)
                oh = ob[3] - ob[1]
                obl = bbox(o, (ob[1] + int(oh * 0.45), ob[3]))
                na = np.array(new)
                nb = bbox(na)
                nh = nb[3] - nb[1]
                nbl = bbox(na, (nb[1] + int(nh * 0.45), nb[3]))
                s = (obl[3] - obl[1]) / (nbl[3] - nbl[1])
                new = new.resize((int(round(new.width * s)), int(round(new.height * s))), Image.NEAREST)
                nbl = tuple(int(round(v * s)) for v in nbl)
                dx = int(round((obl[0] + obl[2]) / 2 - (nbl[0] + nbl[2]) / 2))
                dy = obl[3] - nbl[3]
                canvas = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
                canvas.paste(new, (dx, dy), new)
                canvas.save(os.path.join(d, f"{cls}_{clip}_{i}.png"))
                print("fit", cls, key, clip, i, "scale %.3f" % s)


if __name__ == "__main__":
    cmd, cls, which = sys.argv[1], sys.argv[2], sys.argv[3]
    if cmd == "gen":
        only = None
        if "--frames" in sys.argv:
            only = {(c, int(n)) for c, n in (x.split(":") for x in sys.argv[sys.argv.index("--frames") + 1].split(","))}
        gen(cls, which, only, int(sys.argv[sys.argv.index("--workers") + 1]) if "--workers" in sys.argv else 3)
    else:
        fit(cls, which)
