"""Meshy text-to-image requests for the Adventure King fantasy UI kit (original design: antique gold filigree,
teal gemstones, navy enamel). All pieces are front-on, empty (no text) and on transparent backgrounds so they
can be 9-sliced in Unity."""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REQ = os.path.join(ROOT, "ArtSource", "Meshy", "requests")
COMMON = ("for a cute fantasy adventure MMORPG, perfectly front-on flat orthographic view, symmetrical, polished antique "
          "gold metal filigree trim with curling vine motifs and teal gemstones, crisp clean edges, high detail, soft rim light, "
          "no text, no letters, no icons, no characters, plain transparent background")
KIT = {
    "ui_button_primary": ("Ornate horizontal game UI button: rounded rectangle with a glossy teal-blue enamel face and soft top highlight, "
                          "thin gold rim, small round teal gem caps on the left and right ends, empty centre", "16:9"),
    "ui_button_secondary": ("Ornate horizontal game UI button: rounded rectangle with a deep navy-indigo enamel face and soft top highlight, "
                            "thin gold rim, small gold rivet caps on the left and right ends, empty centre", "16:9"),
    "ui_card_vertical": ("Ornate vertical game UI card frame (portrait rectangle) for choosing a hero class: gold filigree border, "
                         "teal gem at the top centre, deep navy inner panel with subtle gradient, empty centre", "2:3"),
    "ui_slot_frame": ("Ornate square game UI slot frame for a character save slot: gold rim with four small teal corner gems, "
                      "dark navy recessed inner square, empty centre", "1:1"),
    "ui_title_banner": ("Ornate wide decorative title cartouche banner: gold filigree scroll ends curling outward, teal enamel band in "
                        "the middle, a small teal gem at the top centre, empty centre", "16:9"),
    "ui_bar_frame": ("Very wide thin ornate horizontal progress bar frame: slim gold rim, small teal gem caps at both ends, "
                     "empty dark inner groove", "16:9"),
    "ui_input_frame": ("Wide ornate text input field frame: dark navy recessed inset with a thin double gold border and tiny gold corner "
                       "flourishes, empty centre", "16:9"),
}

if __name__ == "__main__":
    jobs = []
    for name, (desc, ratio) in KIT.items():
        with open(os.path.join(REQ, name + ".json"), "w", encoding="utf-8") as f:
            json.dump({"prompt": desc + " " + COMMON, "aspect_ratio": ratio, "ai_model": "nano-banana-2",
                       "remove_background": True}, f, ensure_ascii=False, indent=2)
        jobs.append({"kind": "text-to-image", "name": name, "request": f"ArtSource/Meshy/requests/{name}.json"})
    with open(os.path.join(REQ, "ui_kit.jobs.json"), "w", encoding="utf-8") as f:
        json.dump(jobs, f, indent=1)
    print(len(jobs), "ui jobs")
