"""Cuts the 3x2 animation sheets made on the Meshy web app into pet frames.

  "<Blender>/5.2/python/bin/python.exe" Tools/costume/pet_sheet_import.py [petId ...]    (default: every sheet in ArtSource/Meshy/web_sheets/done)

Input : ArtSource/Meshy/web_sheets/done/<petId>.png   3 columns x 2 rows, flat grey background, frame 1 = the pet's current picture
Output: Assets/Resources/Pets/<petId>_b.png .. _f.png (frames 2..6), same scale as the pet's main picture (area matched to frame 1 of the sheet), feet on the same line.
Then run Unity: AdventureKing.ArtPipeline.VisualDatabases.BuildFromCommandLine (it adds the frames to the pet's idleFrames; the game loops 6 frames, or ping-pongs 3).
Needs numpy + Pillow. Nothing is drawn here: cells are cut, the grey background is keyed out, small stray fragments are dropped.
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DONE = os.path.join(ROOT, "ArtSource", "Meshy", "web_sheets", "done")
PETS = os.path.join(ROOT, "Assets", "Resources", "Pets")
BIG = {"pet_war_dragon", "pet_ink_dragon"}


def key_background(cell):
    """Flat background -> transparent: the colour of the cell corners, with a tolerance, flood-filled from the border."""
    a = np.array(cell.convert("RGBA")).astype(int)
    h, w = a.shape[:2]
    corners = np.array([a[2, 2, :3], a[2, w - 3, :3], a[h - 3, 2, :3], a[h - 3, w - 3, :3]])
    bg = np.median(corners, axis=0)
    near = (np.abs(a[..., :3] - bg).sum(axis=2) < 54)
    outside = np.zeros((h, w), bool)
    stack = [(y, x) for y in (0, h - 1) for x in range(w)] + [(y, x) for x in (0, w - 1) for y in range(h)]
    while stack:
        y, x = stack.pop()
        if 0 <= y < h and 0 <= x < w and near[y, x] and not outside[y, x]:
            outside[y, x] = True
            stack += [(y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)]
    a[outside, 3] = 0
    return Image.fromarray(a.astype("uint8"), "RGBA")


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
    return Image.fromarray(a)


def area(im):
    return int((np.array(im)[..., 3] > 0).sum())


def run(pid):
    path = os.path.join(DONE, pid + ".png")
    if not os.path.exists(path):
        print("no sheet for", pid)
        return
    sheet = Image.open(path).convert("RGBA")
    cw, ch = sheet.width // 3, sheet.height // 2
    cells = [key_background(sheet.crop((c * cw, r * ch, (c + 1) * cw, (r + 1) * ch))) for r in range(2) for c in range(3)]
    boxes = [im.getbbox() for im in cells]
    if any(b is None for b in boxes):
        print(pid, "has an empty cell: regenerate this sheet")
        return
    main = Image.open(os.path.join(PETS, pid + ".png")).convert("RGBA")
    s = (area(main) / max(1, area(cells[0].crop(boxes[0])))) ** 0.5            # sheet scale -> game scale, from frame 1
    big = pid in BIG
    base_bottom = boxes[0][3]
    for i in range(1, 6):
        im = cells[i]
        # keep the position of the creature inside its cell (feet line) by cropping to the union height of frame 1 and this frame
        bb = boxes[i]
        fr = drop_specks(im.crop(bb))
        fr = fr.resize((max(1, round(fr.width * s)), max(1, round(fr.height * s))), Image.LANCZOS if big else Image.BOX)
        fr.putalpha(fr.getchannel("A").point(lambda v: 255 if v > (70 if big else 110) else 0))
        bb2 = fr.getbbox()
        if bb2:
            fr = fr.crop(bb2)
        fr.save(os.path.join(PETS, f"{pid}_{'bcdef'[i - 1]}.png"))
    print(pid, "ok, scale %.3f" % s)


if __name__ == "__main__":
    ids = sys.argv[1:] or [f[:-4] for f in sorted(os.listdir(DONE)) if f.endswith(".png")]
    for p in ids:
        run(p)
