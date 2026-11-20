"""Full-body class SKINS through Meshy image-to-image, ONE call per clip (a whole animation as a grid; 6 credits per call; nothing is drawn by code).

  python Tools/costume/skin_sheet.py gen  <Class|all> <skinKey|all> [--clips Idle,Run,Attack] [--workers 3] [--max-credits N]
  python Tools/costume/skin_sheet.py fit  <Class|all> <skinKey|all>

gen : builds a grid of the hero's original frames (Assets/Art/Chibi2D/<Class>/<Class>_<Clip>.png, 192 px each), asks Meshy to redesign ONLY the outfit in the skin's style in every cell.
fit : cuts the result into cells and puts every frame back on the original 192 px frame (feet line + lower-body width) ->
      Assets/Resources/Fashion/Skins/<Class>/<key>/<Class>_<Clip>_<n>.png
Raw results: ArtSource/Meshy/raw/skinsheet_<Class>_<key>_<Clip>/image_0.png. Credits are logged by meshy.py in ArtSource/Meshy/manifest.jsonl.
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
UP = 384
CLIPS = ["Idle", "Run", "Attack"]
GRID = {6: (3, 3), 7: (3, 3), 8: (3, 3)}          # frames -> (columns, rows): the model always answers with a SQUARE picture, so we send a 3x3 grid (spare cells repeat the first frames, ignored on fit)

STYLE = ("High-detail 2D pixel art, crisp discrete pixels with NO anti-aliasing and NO blur, dark selective outline, hand-pixeled clustered shading, "
         "premium fantasy MMORPG style, light from the top-left, transparent background, no text.")

C, K, A = "CUTE", "COOL", "ANIME"
SKINS = {
    "Swordsman": {
        "crimson": (K, "a crimson royal-guard knight outfit: red and gold plate armour with a short red cape, a plumed helmet-less look, polished pauldrons, noble guard look"),
        "verdant": (K, "a forest-ranger knight outfit: green leather and steel armour with a hooded green mantle, leaf motifs, brown belts, woodland hero look"),
        "azure": (A, "an anime azure-dragon swordsman outfit: blue and white long coat with silver dragon embroidery, flowing blue scarf, glowing cyan trim, stylish hero look"),
        "jade": (K, "a jade general outfit: deep green lacquered armour with gold trim, jade shoulder guards and a long tabard, commanding general look"),
        "ronin": (K, "a night ronin outfit: black and indigo kimono-style coat with a straw-tied sash, tattered dark cape, silver accents, lone wanderer look"),
        "aurum": (A, "an anime holy-sword paladin outfit: white and gold ornate armour with a golden winged pauldron and a flowing white cape, glowing gold trim, radiant hero look"),
        "frost": (K, "a frost-steel armour outfit: pale blue ice-crystal plate armour with white fur collar, icy glowing edges, heavy frozen knight look"),
        "ember": (K, "a flame war-god outfit: black armour with glowing orange lava cracks, red flame cape, horned pauldrons, burning fury look"),
        "shadow": (K, "a dark king outfit: black and violet spiked armour with a tall collar, tattered purple cape, glowing violet eyes slits on the pauldrons, menacing dark lord look"),
        "sun": (A, "an anime sun-general outfit: radiant gold and orange divine armour with a big sun-disc emblem, flowing red-gold cape, glowing halo trim, legendary hero look"),
    },
    "Mage": {
        "sakura": (C, "a cute sakura witch outfit: soft pink and white short frilly dress with big ribbons and a bow, cherry blossom petals, a small pink witch hat with a flower, pink thigh-high stockings, sweet kawaii look"),
        "crimson": (K, "a cool dark gothic blood-queen outfit: black and crimson armoured corset robe with a high collar, spiked shoulder pauldrons, a tall black crown-like horned hat with red gems, edgy badass look"),
        "aurum": (A, "an anime magical-girl idol outfit: white and gold layered dress with a big gold bow, star motifs, glowing gold trim, a fancy white and gold star hat, sparkling heroine look"),
        "violet": (C, "a cute violet dream-witch outfit: purple and white puffy short dress with star and moon charms, a pointy purple hat with a big bow, lavender stockings, sweet look"),
        "verdant": (C, "a cute forest-sage outfit: soft green and cream dress with leaf and mushroom charms, a flower-crowned small hat, brown boots, gentle nature-girl look"),
        "ocean": (A, "an anime ocean-priestess outfit: blue and white flowing dress with a pearl and shell tiara, translucent aqua scarves, glowing blue trim, graceful look"),
        "night": (K, "a night-moon seer outfit: deep navy and silver robe with a crescent-moon hood and star-dust cape, silver trim, mysterious oracle look"),
        "ember": (K, "a fire-mage outfit: black and orange robe with flame embroidery, a tall pointed hat with an ember gem, ember-glow hem, fierce pyromancer look"),
        "frost": (A, "an anime ice-queen outfit: white and ice-blue elegant gown with snowflake crown and crystal shoulder pieces, glowing cyan trim, regal frost look"),
        "abyss": (K, "a void-magic outfit: black and deep-purple robe with floating dark crystals and a hooded tall hat, glowing magenta runes, forbidden arcanist look"),
    },
    "Assassin": {
        "tide": (K, "a tide-assassin outfit: dark blue and teal fitted ninja suit with a wave-pattern scarf and a face-half mask, light armour plates, swift look"),
        "viper": (K, "a viper assassin outfit: dark purple and toxic green scaled bodysuit with a snake-head shoulder piece and a hooded cowl, venomous look"),
        "frost": (A, "an anime snow-shadow outfit: white and pale blue ninja outfit with a fluffy white scarf and ice-crystal kunai pouches, glowing icy trim, elegant look"),
        "sakura": (C, "a cute sakura kunoichi outfit: pink and white short ninja dress with a big bow sash, cherry blossom hair pins, pink arm warmers, sweet kawaii look"),
        "void": (K, "a void-shadow outfit: pure black bodysuit with violet glowing seams, torn dark cloak, a half face-mask, ghostly look"),
        "blood": (K, "a blood-moon assassin outfit: black and crimson leather with a red crescent emblem, spiked bracers, red scarf, lethal look"),
        "aurum": (A, "an anime golden-shadow outfit: black and gold ninja outfit with golden shoulder guards and a long gold-trimmed scarf, glowing gold trim, premium look"),
        "ember": (K, "an ember-phantom outfit: charcoal black suit with glowing orange fire seams and wisps of flame trailing from the cloak, fiery look"),
        "ghost": (K, "a ghost-wind assassin outfit: tattered grey and teal hooded cloak with a skull half-mask and chains, spectral look"),
        "star": (A, "an anime star-shadow outfit: midnight blue ninja outfit with silver star patterns, a flowing starry scarf and glowing star-shaped clasps, celestial look"),
    },
    "Archer": {
        "forest": (K, "a forest-hunter outfit: green and brown leather tunic with a feathered hood, quiver strap, bracers and boots, woodland archer look"),
        "autumn": (C, "a cute autumn-archer outfit: orange and cream short dress with a maple-leaf cape and a little hat with a feather, warm brown boots, sweet look"),
        "frost": (K, "a frost-hunter outfit: white fur-trimmed pale blue coat with a fur hood, ice-crystal quiver, snow-trapper look"),
        "shadow": (K, "a night-stalker outfit: black and dark green fitted leather armour with a hood and a half cape, silver clasps, stealthy look"),
        "sakura": (C, "a cute cherry-blossom archer outfit: pink and white short dress with a big ribbon, flower hair accessories, pink gloves and boots, sweet kawaii look"),
        "moon": (A, "an anime moon-goddess archer outfit: white and silver-blue flowing outfit with a crescent-moon crown and a long glowing scarf, graceful divine look"),
        "ember": (K, "a flame-archer outfit: red and black leather armour with phoenix-feather shoulder pieces and glowing ember trim, fiery look"),
        "aurum": (A, "an anime golden-eagle archer outfit: white and gold ornate outfit with golden eagle-wing pauldrons and a long cape, glowing gold trim, royal look"),
        "azure": (A, "an anime sky-marquis archer outfit: sky blue and white elegant coat with silver wind patterns, a long flowing scarf and a feathered hat, noble look"),
        "fae": (C, "a cute forest-fairy archer outfit: pastel green and lilac petal dress with small translucent wings, flower crown, glittering sparkles, enchanting kawaii look"),
    },
}


def frames(cls, clip):
    im = Image.open(os.path.join(ART, cls, f"{cls}_{clip}.png")).convert("RGBA")
    return [im.crop((i * CELL, 0, (i + 1) * CELL, CELL)) for i in range(im.width // CELL)]


def name_of(cls, key, clip):
    return f"skinsheet_{cls}_{key}_{clip}"


def sheet_of(cls, clip):
    fr = frames(cls, clip)
    cols, rows = GRID[len(fr)]
    sheet = Image.new("RGBA", (cols * UP, rows * UP), (0, 0, 0, 0))
    pad = fr + fr[: cols * rows - len(fr)]
    for i, f in enumerate(pad):
        sheet.alpha_composite(f.resize((UP, UP), Image.NEAREST), ((i % cols) * UP, (i // cols) * UP))
    return sheet, cols, rows, len(fr)


def job(cls, key, clip):
    name = name_of(cls, key, clip)
    if os.path.exists(os.path.join(RAW, name, "image_0.png")):
        return name, "skip"
    style, desc = SKINS[cls][key]
    sheet, cols, rows, n = sheet_of(cls, clip)
    b = io.BytesIO()
    sheet.save(b, "PNG")
    uri = "data:image/png;base64," + base64.b64encode(b.getvalue()).decode()
    prompt = (f"This image is a {cols} by {rows} grid of {cols * rows} equal square cells, each holding the SAME pixel-art game character in one frame of a looping {clip.lower()} animation. "
              f"Edit every cell: REDESIGN ONLY THE OUTFIT AND HEADWEAR in a {style} style: {desc}. Use the identical new outfit in all {cols * rows} cells. "
              "Keep EVERYTHING else identical in every cell: same pose, same face, same hair colour and hairstyle, same weapon, same body proportions, same size, same position inside its cell, "
              "same camera angle facing right, the grid layout unchanged with no gaps or borders. " + STYLE)
    req = os.path.join(REQ, name + ".json")
    with open(req, "w", encoding="utf-8") as fh:
        json.dump(dict(ai_model="nano-banana-2", remove_background=True, reference_image_urls=[uri], prompt=prompt), fh)
    r = subprocess.run([sys.executable, MESHY, "run", "image-to-image", name, req], capture_output=True, text=True)
    return name, "ok" if os.path.exists(os.path.join(RAW, name, "image_0.png")) else "FAILED " + r.stdout[-200:] + r.stderr[-200:]


def targets(cls, key):
    for c in (SKINS if cls == "all" else [cls]):
        for k in (SKINS[c] if key == "all" else [key]):
            if k in SKINS[c]:
                yield c, k


def gen(cls, key, clips, workers, max_credits):
    jobs = [(c, k, clip) for c, k in targets(cls, key) for clip in clips if not os.path.exists(os.path.join(RAW, name_of(c, k, clip), "image_0.png"))]
    if max_credits is not None:
        jobs = jobs[: max_credits // 6]
    print(len(jobs), "calls,", len(jobs) * 6, "credits at most", flush=True)
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


def clean(img, min_area=60):
    """Drops tiny detached specks / hairlines (model artefacts) before measuring: keeps components with >= min_area opaque pixels."""
    a = np.array(img)
    solid = a[..., 3] > 40
    h, w = solid.shape
    lab = np.zeros((h, w), np.int32)
    n = 0
    sizes = [0]
    for y in range(h):
        for x in range(w):
            if solid[y, x] and not lab[y, x]:
                n += 1
                stack = [(y, x)]
                lab[y, x] = n
                cnt = 0
                while stack:
                    cy, cx = stack.pop()
                    cnt += 1
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            ny, nx = cy + dy, cx + dx
                            if 0 <= ny < h and 0 <= nx < w and solid[ny, nx] and not lab[ny, nx]:
                                lab[ny, nx] = n
                                stack.append((ny, nx))
                sizes.append(cnt)
    keep = np.array([s >= min_area for s in sizes])
    for c in range(1, n + 1):                          # hairlines / rules the model sometimes draws: thin AND long = not part of the character
        if keep[c]:
            ys, xs = np.nonzero(lab == c)
            w, hh = xs.max() - xs.min() + 1, ys.max() - ys.min() + 1
            if (min(w, hh) <= 4 and max(w, hh) > 30) or (w > 100 and hh > 100 and len(ys) < 0.06 * w * hh):   # hairline, or an L-shaped border / frame fragment
                keep[c] = False
    a[~keep[lab]] = 0
    return Image.fromarray(a)


def fit_cell(orig, new):
    o = np.array(orig)
    ob = bbox(o)
    oh = ob[3] - ob[1]
    obl = bbox(o, (ob[1] + int(oh * 0.45), ob[3]))
    new = clean(new)
    na = np.array(new)
    nb = bbox(na)
    if nb is None:
        return None
    nh = nb[3] - nb[1]
    nbl = bbox(na, (nb[1] + int(nh * 0.45), nb[3]))
    s = FIT_REF[0] if FIT_REF[0] else ((obl[3] - obl[1]) / (nbl[3] - nbl[1]) * (oh / nh)) ** 0.5   # one scale for the whole clip (see fit)
    new = new.resize((max(1, int(round(new.width * s))), max(1, int(round(new.height * s)))), Image.NEAREST)
    nbl = tuple(int(round(v * s)) for v in nbl)
    dx = int(round((obl[0] + obl[2]) / 2 - (nbl[0] + nbl[2]) / 2))
    dy = obl[3] - nbl[3]
    canvas = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    canvas.paste(new, (dx, dy), new)
    return canvas


FIT_REF = [None]

def heights(img):
    ys = np.nonzero(np.array(img)[..., 3] > 40)[0]
    return (ys.max() - ys.min() + 1) if len(ys) else 0


def cells_of(res, cols, rows, n):
    cw, ch = res.width / cols, res.height / rows
    return [res.crop((int(round((i % cols) * cw)), int(round((i // cols) * ch)), int(round((i % cols + 1) * cw)), int(round((i // cols + 1) * ch)))) for i in range(n)]


def estimate(orig, cells):
    ests = []
    for of, cell in zip(orig, cells):
        na, o = np.array(clean(cell)), np.array(of)
        nb, ob = bbox(na), bbox(o)
        if nb is None:
            continue
        nbl = bbox(na, (nb[1] + int((nb[3] - nb[1]) * 0.45), nb[3]))
        obl = bbox(o, (ob[1] + int((ob[3] - ob[1]) * 0.45), ob[3]))
        ests.append((((obl[3] - obl[1]) / (nbl[3] - nbl[1])) * ((ob[3] - ob[1]) / (nb[3] - nb[1]))) ** 0.5)
    return float(np.median(ests)) if ests else None


def fit(cls, key):
    for c, k in targets(cls, key):
        d = os.path.join(OUT, c, k)
        for clip in CLIPS:
            p = os.path.join(RAW, name_of(c, k, clip), "image_0.png")
            if not os.path.exists(p):
                continue
            os.makedirs(d, exist_ok=True)
            res = Image.open(p).convert("RGBA")
            orig = frames(c, clip)
            cols, rows = GRID[len(orig)]
            cells = cells_of(res, cols, rows, len(orig))
            FIT_REF[0] = estimate(orig, cells)
            outs = []
            for attempt in (0, 1):                       # attempt 1 rescales a clip that came out at the wrong size as a whole
                outs = [fit_cell(of, cell) for of, cell in zip(orig, cells)]
                ratios = [heights(o) / max(1, heights(of)) for o, of in zip(outs, orig) if o is not None]
                med = float(np.median(ratios)) if ratios else 1.0
                if attempt == 0 and FIT_REF[0] and (med < 0.88 or med > 1.2):
                    FIT_REF[0] = FIT_REF[0] / med
                    continue
                break
            ratio = [(heights(o) / max(1, heights(of))) if o is not None else 0 for o, of in zip(outs, orig)]
            good = [0.8 <= r_ <= 1.3 for r_ in ratio]
            for i in range(len(orig)):
                if not good[i] and any(good):            # a glitched cell repeats the nearest good neighbour of the same clip
                    j = min((x for x in range(len(orig)) if good[x]), key=lambda x: (abs(x - i), x))
                    outs[i] = outs[j]
                    print("repaired", c, k, clip, i, "from", j, round(float(ratio[i]), 2))
            for i, o in enumerate(outs):
                if o is None:
                    print("EMPTY", c, k, clip, i)
                    continue
                o.save(os.path.join(d, f"{c}_{clip}_{i}.png"))
            print("fit", c, k, clip, len(orig), "frames, clip scale", round(FIT_REF[0] or 0, 3))

if __name__ == "__main__":
    cmd, cls, key = sys.argv[1], sys.argv[2], sys.argv[3]
    if cmd == "gen":
        clips = sys.argv[sys.argv.index("--clips") + 1].split(",") if "--clips" in sys.argv else CLIPS
        mc = int(sys.argv[sys.argv.index("--max-credits") + 1]) if "--max-credits" in sys.argv else None
        gen(cls, key, clips, int(sys.argv[sys.argv.index("--workers") + 1]) if "--workers" in sys.argv else 3, mc)
    else:
        fit(cls, key)
