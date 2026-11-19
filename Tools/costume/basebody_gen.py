"""Bare-head BaseBody frames through Meshy image-to-image (6 credits per frame, nothing is drawn by code).

  "<Blender>/5.2/python/bin/python.exe" Tools/costume/basebody_gen.py gen      [--workers 3]   crops each hero frame, asks Meshy to remove the default hat
  "<Blender>/5.2/python/bin/python.exe" Tools/costume/basebody_gen.py fit                      aligns the raw results onto the original 192 px frames -> Assets/Resources/Fashion/BaseBody/<Class>/<Class>_<Clip>_<n>.png

Needs Pillow + numpy. Raw results: ArtSource/Meshy/raw/basebody_<Class>_<Clip>_<n>/image_0.png (git-ignored). Credits are logged by meshy.py in ArtSource/Meshy/manifest.jsonl.
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
OUT = os.path.join(ROOT, "Assets", "Resources", "Fashion", "BaseBody")
MESHY = os.path.join(ROOT, "Tools", "meshy", "meshy.py")
CELL = 192
CLIPS = ["Idle", "Run", "Jump", "Attack"]
HAT = {"Mage": "pointed purple wizard hat with a wide brim", "Archer": "green feathered archer cap"}


def frames(cls, clip):
    im = Image.open(os.path.join(ART, cls, f"{cls}_{clip}.png")).convert("RGBA")
    return [im.crop((i * CELL, 0, (i + 1) * CELL, CELL)) for i in range(im.width // CELL)]


def job(cls, clip, i, fr):
    name = f"basebody_{cls}_{clip}_{i}"
    if os.path.exists(os.path.join(RAW, name, "image_0.png")):
        return name, "skip"
    b = io.BytesIO()
    fr.resize((768, 768), Image.NEAREST).save(b, "PNG")
    uri = "data:image/png;base64," + base64.b64encode(b.getvalue()).decode()
    prompt = (f"Edit this exact pixel-art character: REMOVE the {HAT[cls]} completely, showing the bare head with full face and natural hair "
              "(finish the top of the hair naturally, same hair colour and hairstyle). Keep EVERYTHING else identical: same pose, same body, same clothes, "
              "same weapon, same size, same position in the picture, same pixel-art style, crisp pixels, dark outline, transparent background, no text.")
    req = os.path.join(REQ, name + ".json")
    with open(req, "w", encoding="utf-8") as fh:
        json.dump(dict(ai_model="nano-banana-2", remove_background=True, reference_image_urls=[uri], prompt=prompt), fh)
    r = subprocess.run([sys.executable, MESHY, "run", "image-to-image", name, req], capture_output=True, text=True)
    return name, "ok" if os.path.exists(os.path.join(RAW, name, "image_0.png")) else "FAILED " + r.stdout[-200:] + r.stderr[-200:]


def gen(workers):
    jobs = [(c, k, i, f) for c in HAT for k in CLIPS for i, f in enumerate(frames(c, k))]
    print(len(jobs), "frames")
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


def fit():
    """Scale + move every raw result so its body below the head sits exactly on the original frame (same feet line, same width, same centre)."""
    for cls in HAT:
        for clip in CLIPS:
            os.makedirs(os.path.join(OUT, cls), exist_ok=True)
            for i, orig in enumerate(frames(cls, clip)):
                p = os.path.join(RAW, f"basebody_{cls}_{clip}_{i}", "image_0.png")
                if not os.path.exists(p):
                    continue
                new = Image.open(p).convert("RGBA")
                o = np.array(orig)
                ob = bbox(o)
                oh = ob[3] - ob[1]
                lo = (ob[1] + int(oh * 0.45), ob[3])             # body below the head (hat, hair) : the part that must line up
                obl = bbox(o, lo)
                nb_full = bbox(np.array(new))
                nh = nb_full[3] - nb_full[1]
                nlo = (nb_full[1] + int(nh * 0.45), nb_full[3])
                nbl = bbox(np.array(new), nlo)
                s = (obl[3] - obl[1]) / (nbl[3] - nbl[1])        # scale so the lower body has the original height
                w, h = int(round(new.width * s)), int(round(new.height * s))
                new = new.resize((w, h), Image.NEAREST)
                nbl = tuple(int(round(v * s)) for v in nbl)
                dx = int(round((obl[0] + obl[2]) / 2 - (nbl[0] + nbl[2]) / 2))
                dy = obl[3] - nbl[3]
                canvas = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
                canvas.alpha_composite(new, (dx, dy)) if dx >= 0 and dy >= 0 else canvas.paste(new, (dx, dy))
                canvas.save(os.path.join(OUT, cls, f"{cls}_{clip}_{i}.png"))
                print("fit", cls, clip, i, "scale %.3f" % s)


if __name__ == "__main__":
    if sys.argv[1] == "gen":
        gen(int(sys.argv[sys.argv.index("--workers") + 1]) if "--workers" in sys.argv else 3)
    else:
        fit()
