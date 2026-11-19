"""Writes the fashion DATA only (no art is drawn here: every sprite is dragged into the Unity ScriptableObjects by the owner).

  "<Blender>/5.2/python/bin/python.exe" Tools/costume/make_costumes.py        (needs numpy + Pillow only to read the hero strips for the anchors)

Reads  Tools/costume/costume_data.py (single source of truth for names, prices, skills) and the existing hero strips Assets/Art/Chibi2D/<Class>/<Class>_<Clip>.png.
Writes
  Server/AdventureKing.Server/config/cosmetics.json   server catalogue (old 3D entries kept; 2D entries replaced). A set lists its pieces (top, bottom, head, mask).
  Assets/Resources/Fashion/catalog.json               client catalogue: id, slot, name, pieces of a set, run-speed bonus (no image data)
  Assets/Resources/Fashion/anchors.json               head / face / shoulder point of every body frame (pixels, y up), measured on the existing hero sprites
A set is worn as separate pieces: piece id = "<setId>.<slot>" (slot top / bottom / head / mask), each its own paper-doll layer.
"""
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
import costume_data as D  # noqa: E402

CELL = 192
CLIPS = ["Idle", "Run", "Jump", "Fall", "Climb", "Hit", "Death", "Attack", "Skill"]
ART = os.path.join(ROOT, "Assets", "Art", "Chibi2D")
RES = os.path.join(ROOT, "Assets", "Resources", "Fashion")
SERVER = os.path.join(ROOT, "Server", "AdventureKing.Server", "config", "cosmetics.json")
CLASS_ID = {v: k for k, v in D.HERO.items()}


# ---------------------------------------------------------------- anchors (measured on the existing hero sprites)
def load_strip(cls, clip):
    path = os.path.join(ART, cls, f"{cls}_{clip}.png")
    if not os.path.exists(path):
        return None
    im = Image.open(path).convert("RGBA")
    return [np.array(im.crop((i * CELL, 0, (i + 1) * CELL, CELL))) for i in range(im.width // CELL)]


def anchors_of(frame):
    a = frame[..., 3] > 0
    ys, xs = np.nonzero(a)
    if len(ys) == 0:
        return None
    rows = np.nonzero(a.sum(1) >= 6)[0]
    return dict(top=int(rows[0]) if len(rows) else int(ys.min()), bottom=int(ys.max()))


def smooth(v):
    return [sorted(v[max(0, i - 1):i + 2])[len(v[max(0, i - 1):i + 2]) // 2] for i in range(len(v))]


def build_anchors(cls, strips):
    table = {}
    idle = [anchors_of(f) for f in strips.get("Idle", [])]
    h0 = float(np.median([a["bottom"] - a["top"] for a in idle if a])) if idle else 120.0
    for clip, frames in strips.items():
        if clip in ("Climb", "Death"):                      # back view / lying down: no reliable head point, overlays hide there
            continue
        raw = [anchors_of(f) for f in frames]
        if any(r is None for r in raw):
            continue
        tops = [int(round(r["bottom"] - h0)) if r["bottom"] - r["top"] > h0 * 1.04 else r["top"] for r in raw]
        hxs = []
        for f, top in zip(frames, tops):
            a = f[..., 3] > 0
            centers = []
            for yy in range(top + 2, top + 6):
                row = np.nonzero(a[min(yy, CELL - 1)])[0]
                if len(row):
                    runs = np.split(row, np.nonzero(np.diff(row) > 1)[0] + 1)
                    centers.append(float(max(runs, key=len).mean()))
            hxs.append(float(np.mean(centers)) if centers else float(np.nonzero(a)[1].mean()))
        hx, top = smooth(hxs), smooth(tops)
        table[clip] = [[round(hx[i], 1), round(CELL - top[i], 1), round(hx[i] + 4, 1), round(CELL - top[i] - 11, 1), round(hx[i] - 4, 1), round(CELL - top[i] - 27, 1)]
                       for i in range(len(frames))]
    return table


# ---------------------------------------------------------------- catalogue
def main():
    client, server, anchors_file = [], [], {}

    def add(item_id, name, slot, kind, rarity, currency, price, classes, desc, skill=None, effect=None, pieces=None):
        server.append(dict(id=item_id, name=name, description=desc, slot=slot, kind=kind, currency=currency, price=price, classes=classes, listed=True,
                           rarity=rarity, pieces=pieces or [],
                           skill=dict(name=skill[0], description=skill[1], **(effect or {})) if skill else None, art="2d"))
        client.append(dict(id=item_id, slot=slot, name=name, moveSpeedPct=(effect or {}).get("moveSpeedPct", 0), pieces=pieces or []))

    for cls in D.HERO.values():
        anchors_file[cls] = build_anchors(cls, {c: f for c in CLIPS if (f := load_strip(cls, c))})
        for (key, name, hue, sm, vm, hshape, mshape, rarity, cur, price, sname, sdesc, eff) in D.SETS[cls]:
            pieces = ["top", "bottom"] + (["head"] if hshape else []) + (["mask"] if mshape else [])
            add(f"set2d_{cls.lower()}_{key}", name, "set", "cosmetic_set", rarity, cur, price, [CLASS_ID[cls]],
                f"Bộ trang phục 2D: {name} ({', '.join(pieces)}). {sdesc}", (sname, sdesc), eff, pieces)
    for (key, name, shape, tint, rarity, cur, price) in D.HATS:
        add("hat2d_" + key, name, "head", "cosmetic", rarity, cur, price, [1, 3], f"Mũ cho nhân vật nữ: {name}.")
    for (key, name, shape, tint, rarity, cur, price) in D.MASKS:
        add("mask2d_" + key, name, "mask", "cosmetic", rarity, cur, price, [1, 3], f"Mặt nạ cho nhân vật nữ: {name}.")
    for (key, name, shape, tint, rarity, cur, price, eff) in D.WINGS:
        sk = ("Cánh " + name.split("Cánh ")[-1], "Nội tại: " + ", ".join(
            k.replace("moveSpeedPct", "+%d%% tốc chạy").replace("critPct", "+%d%% chí mạng") % v for k, v in eff.items())) if eff else None
        add("wings2d_" + key, name, "wings", "cosmetic", rarity, cur, price, [1, 3], f"Cánh cho nhân vật nữ: {name}.", sk, eff or None)
    for (key, name, shape, tint, rarity, cur, price, eff) in D.MOUNTS:
        add("mount2d_" + key, name, "mount", "cosmetic", rarity, cur, price, [0, 1, 2, 3], f"Thú cưỡi: {name}, chạy nhanh hơn {eff['moveSpeedPct']}%.",
            ("Tăng tốc", f"Nội tại: +{eff['moveSpeedPct']}% tốc chạy khi cưỡi."), eff)
    for (key, name, hue, sm, vm, rarity, price) in D.TOPS + D.BOTTOMS:
        top = key.startswith("t_")
        add(("top2d_" if top else "bottom2d_") + key[2:], name, "top" if top else "bottom", "cosmetic", rarity, "gold", price, [0, 1, 2, 3],
            f"{'Áo' if top else 'Quần'}: {name}.")

    os.makedirs(RES, exist_ok=True)
    with open(os.path.join(RES, "catalog.json"), "w", encoding="utf-8") as fh:
        json.dump(dict(version=2, items=client), fh, ensure_ascii=False, indent=1)
    with open(os.path.join(RES, "anchors.json"), "w", encoding="utf-8") as fh:
        json.dump(dict(cell=CELL, e=[dict(key=f"{c}_{k}", v=[x for ent in ents for x in ent]) for c, tb in anchors_file.items() for k, ents in tb.items()]),
                  fh, separators=(",", ":"))
    with open(SERVER, "r", encoding="utf-8-sig") as fh:
        cat = json.load(fh)
    cat["items"] = [i for i in cat["items"] if i.get("art") != "2d"] + server
    cat["version"] = 4
    cat["_note"] = ("Server-owned cosmetic catalog. Prices are PROPOSALS pending owner approval (DRAFT); change them here, never in code. Entries with art '2d' "
                    "(sets per class, hats, masks, tops, bottoms, wings, mounts) come from Tools/costume/costume_data.py via make_costumes.py; their images are "
                    "assigned by the owner in the Fashion Database asset. A set is worn as separate pieces (id '<setId>.<slot>'). Old 3D entries stay (owned items keep working). "
                    "Classes: 0 Kiếm Sĩ, 1 Pháp Sư, 2 Sát Thủ, 3 Cung Thủ. skill = passive effect numbers the farm combat reads.")
    with open(SERVER, "w", encoding="utf-8") as fh:
        json.dump(cat, fh, ensure_ascii=False, indent=2)
    print("items:", len(client))


if __name__ == "__main__":
    main()
