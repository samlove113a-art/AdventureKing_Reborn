"""One Meshy image-to-image call per pet -> a 2x2 sheet of 4 animation frames (6 credits per pet). The API returns a square image whatever the reference shape.

  "<Blender>/5.2/python/bin/python.exe" Tools/costume/pet_sheet_gen.py <petId>        (needs MESHY_API_KEY from the User env; call one pet at a time and look at the result first)

Then:  copy ArtSource/Meshy/raw/sheet2x2_<petId>/image_0.png to ArtSource/Meshy/web_sheets/done/<petId>.png  (done by this script)
       python Tools/costume/pet_sheet_import.py <petId> --grid 2x2   and run Unity VisualDatabases.BuildFromCommandLine
"""
import base64
import io
import json
import os
import shutil
import subprocess
import sys

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "ArtSource", "Meshy", "raw")
REQ = os.path.join(ROOT, "ArtSource", "Meshy", "requests")
DONE = os.path.join(ROOT, "ArtSource", "Meshy", "web_sheets", "done")
MESHY = os.path.join(ROOT, "Tools", "meshy", "meshy.py")
SRC = {"pet_wind_unicorn": "mount2d_white_horse", "pet_ember_tiger": "mount2d_fire_tiger", "pet_night_horse": "mount2d_night_horse",
       "pet_rainbow_cloud": "mount2d_cloud", "pet_snow_wolf": "mount2d_snow_wolf"}

COMMON = ("Every cell: the same creature, same design, colours and size, centred, facing right, the whole tail and all limbs / both wings fully drawn and attached. "
          "Crisp pixel art, NO anti-aliasing, NO blur, dark outline, flat grey background in every cell, no text, no borders, no shadows. The output is a square image with exactly 4 equal cells in a 2x2 grid.")
PROMPTS = {
    "FLY": ("looping WING-BEAT cycle", "cell 2 = wings sweeping down halfway, cell 3 = wings fully down at the bottom of the beat with the body slightly lower and tail trailing up, cell 4 = wings rising halfway"),
    "WALK": ("looping WALK cycle", "cell 2 = front foot reaching far forward with the tail swinging back, cell 3 = legs passing under the body with the body slightly raised, cell 4 = back foot pushing off with the tail swinging up"),
    "WALKWING": ("looping WALK cycle with small wing flutter", "cell 2 = front foot reaching far forward, wings slightly raised, tail swinging back, cell 3 = legs passing under the body, wings lowered, body slightly raised, cell 4 = back foot pushing off, wings raised, tail swinging up"),
}
KIND = {"pet_phoenix": "FLY", "pet_wind_unicorn": "WALK", "pet_gold_dragon": "WALKWING", "pet_shadow_dragon": "WALKWING"}


def main(pid):
    name = "sheet2x2_" + pid
    out = os.path.join(RAW, name, "image_0.png")
    if os.path.exists(out):
        print("already generated:", name)
    else:
        ref = Image.open(os.path.join(RAW, SRC.get(pid, pid), "image_0.png")).convert("RGBA")
        ref = ref.crop(ref.getbbox())
        sc = 470 / max(ref.size)
        a = ref.resize((round(ref.width * sc), round(ref.height * sc)), Image.LANCZOS)
        sheet = Image.new("RGBA", (1024, 1024), (128, 128, 128, 255))
        sheet.alpha_composite(a, ((512 - a.width) // 2, 512 - 20 - a.height))          # frame 1 in the top-left cell, feet on the baseline
        b = io.BytesIO()
        sheet.convert("RGB").save(b, "PNG")
        uri = "data:image/png;base64," + base64.b64encode(b.getvalue()).decode()
        word, phases = PROMPTS[KIND[pid]]
        prompt = (f"This image is a 2x2 grid of 4 equal square cells on a flat grey background. The top-left cell already contains a pixel-art game creature. Keep that cell exactly as it is and fill the other 3 cells "
                  f"(top-right, bottom-left, bottom-right, in reading order) with the next 3 frames of a smooth {word} of the SAME creature: {phases}, ready to loop back to the first cell. " + COMMON)
        os.makedirs(REQ, exist_ok=True)
        req = os.path.join(REQ, name + ".json")
        with open(req, "w", encoding="utf-8") as fh:
            json.dump(dict(ai_model="nano-banana-2", remove_background=False, reference_image_urls=[uri], prompt=prompt), fh)
        r = subprocess.run([sys.executable, MESHY, "run", "image-to-image", name, req], capture_output=True, text=True)
        if not os.path.exists(out):
            print("FAILED", (r.stdout + r.stderr)[-300:])
            return
    os.makedirs(DONE, exist_ok=True)
    shutil.copy(out, os.path.join(DONE, pid + ".png"))
    print("sheet ready:", pid)


if __name__ == "__main__":
    main(sys.argv[1])
