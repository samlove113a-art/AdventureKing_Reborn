# -*- coding: utf-8 -*-
"""Chapter 1 · Maps 3-10: ONE source of truth for the data that the server and the Unity client must agree on.

  <blender python> Tools/server/ch1_maps.py          # idempotent: patches config JSON, (re)writes the two generated C# files

Writes / patches (all DRAFT balance, owner has not approved the numbers):
  Server/AdventureKing.Server/config/monsters.json   monster types + spawns (marker comments are not needed: ids are checked)
  Server/AdventureKing.Server/config/quests.json     main quests ch1_07 ...
  Server/AdventureKing.Server/config/items.json      junk + equipment drops
  Server/AdventureKing.Server/Domain/Ch1ServerData.cs   maps, portals, respawn points, safe zones, explore points (server)
  Assets/Scripts/Gameplay/Ch1MapData.cs                 decks, climbs, portals, explore points (client; scene builder + tests)
Map 3 keeps its hand-written class in TownLayout (it is already a built scene) but its extra decks / spawns come from here.
"""
import io
import json
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CFG = os.path.join(ROOT, "Server", "AdventureKing.Server", "config")

# ---------------------------------------------------------------- monster types
# id, name, tier, hp, atk, atkSec, reach, leash, goldMin, goldMax, respawn, drops[(item, chance, min, max)], slam(sec, dmg, reach) or None, enrageBelow
TYPES = [
    ("glow_moss_mushroom", "Nấm Rêu Lung Linh", "normal", 150, 17, 1.7, 1.3, 3.5, 10, 18, 55, [("junk_glow_spore", 0.65, 1, 3), ("gear_glow_charm", 0.05, 1, 1)], None, 0),
    ("mushroom_chief", "Nấm Thủ Lĩnh", "mini", 1400, 26, 1.8, 1.8, 5.0, 120, 180, 300, [("gear_chief_armor", 0.5, 1, 1), ("junk_glow_spore", 1.0, 3, 5)], (4.5, 40, 3.4), 0.5),
    ("flower_bee", "Ong Cánh Hoa", "normal", 170, 18, 1.5, 1.3, 3.5, 11, 19, 55, [("junk_bee_wing", 0.65, 1, 3), ("gear_bee_charm", 0.05, 1, 1)], None, 0),
    ("crystal_bat", "Dơi Pha Lê", "normal", 190, 20, 1.5, 1.3, 3.5, 12, 21, 55, [("junk_crystal_shard", 0.65, 1, 3), ("gear_crystal_ring", 0.05, 1, 1)], None, 0),
    ("jade_turtle", "Rùa Suối Ngọc", "normal", 230, 22, 1.9, 1.3, 3.0, 13, 23, 55, [("junk_jade_shell", 0.65, 1, 3), ("gear_jade_vest", 0.05, 1, 1)], None, 0),
    ("jade_guardian", "Hộ Vệ Lam Thạch", "mini", 2200, 32, 2.0, 1.9, 5.0, 180, 260, 360, [("gear_jade_guardian_charm", 0.5, 1, 1), ("junk_jade_shell", 1.0, 3, 5)], (4.8, 52, 3.6), 0.5),
    ("shadow_leaf", "Bóng Lá Tím", "normal", 250, 25, 1.5, 1.3, 4.0, 14, 25, 55, [("junk_shadow_leaf", 0.65, 1, 3), ("gear_shadow_cloak", 0.05, 1, 1)], None, 0),
    ("toxic_slime", "Slime Nấm Độc", "normal", 280, 27, 1.7, 1.4, 3.5, 15, 27, 60, [("junk_toxic_gel", 0.65, 1, 3), ("gear_toxic_ring", 0.05, 1, 1)], None, 0),
    ("toxic_elite", "Nấm Độc Tinh Anh", "elite", 900, 34, 1.7, 1.6, 4.0, 70, 110, 150, [("gear_toxic_mask", 0.35, 1, 1), ("junk_toxic_gel", 1.0, 2, 4)], (5.2, 44, 3.0), 0.4),
    ("stone_guard", "Vệ Binh Đá Mầm", "normal", 330, 30, 2.0, 1.4, 3.0, 17, 30, 55, [("junk_stone_chip", 0.65, 1, 3), ("gear_stone_vest", 0.05, 1, 1)], None, 0),
    ("dark_mushroom", "Nấm Hắc Ám", "mini", 3000, 38, 1.9, 1.9, 5.0, 260, 380, 420, [("gear_dark_charm", 0.5, 1, 1), ("junk_stone_chip", 1.0, 3, 5)], (4.6, 60, 3.8), 0.5),
    ("royal_guard", "Nấm Cận Vệ", "normal", 380, 33, 1.6, 1.4, 3.5, 20, 34, 55, [("junk_royal_cap", 0.65, 1, 3), ("gear_royal_vest", 0.05, 1, 1)], None, 0),
    ("mushroom_king", "Vua Nấm Quỷ", "boss", 5200, 46, 1.8, 2.2, 6.0, 600, 900, 600, [("gear_king_crown_charm", 1.0, 1, 1), ("junk_royal_cap", 1.0, 4, 6)], (4.2, 72, 4.4), 0.5),
]
BOSS_OF_CHAPTER = {"mushroom_king": 1}

ITEMS = [  # junk: (id, name, price, map label); gear: (id, name, slot, atk, def, desc)
    ("junk", "junk_glow_spore", "Bào Tử Sáng", 10, "Rừng Nấm Sáng"), ("junk", "junk_bee_wing", "Cánh Ong Hoa", 12, "Đồi Hoa"),
    ("junk", "junk_crystal_shard", "Mảnh Pha Lê", 14, "Hang Nấm"), ("junk", "junk_jade_shell", "Mai Rùa Ngọc", 16, "Thác Ngọc"),
    ("junk", "junk_shadow_leaf", "Lá Bóng Tím", 18, "Rừng U Tối"), ("junk", "junk_toxic_gel", "Thạch Độc", 20, "Đầm Lầy Nấm Độc"),
    ("junk", "junk_stone_chip", "Mảnh Đá Mầm", 22, "Khu Di Tích Cổ"), ("junk", "junk_royal_cap", "Mũ Nấm Cận Vệ", 26, "Đền Vua Nấm"),
    ("gear", "gear_glow_charm", "Bùa Nấm Sáng", "accessory", 3, 1, "DRAFT: rơi từ Nấm Rêu Lung Linh. +3 tấn công, +1 phòng thủ."),
    ("gear", "gear_chief_armor", "Giáp Thủ Lĩnh Nấm", "armor", 0, 10, "DRAFT: Mini Boss Nấm Thủ Lĩnh. +10 phòng thủ."),
    ("gear", "gear_bee_charm", "Bùa Cánh Ong", "accessory", 4, 1, "DRAFT: rơi từ Ong Cánh Hoa. +4 tấn công, +1 phòng thủ."),
    ("gear", "gear_crystal_ring", "Nhẫn Pha Lê", "accessory", 5, 0, "DRAFT: rơi từ Dơi Pha Lê. +5 tấn công."),
    ("gear", "gear_jade_vest", "Áo Rùa Ngọc", "armor", 0, 9, "DRAFT: rơi từ Rùa Suối Ngọc. +9 phòng thủ."),
    ("gear", "gear_jade_guardian_charm", "Bùa Lam Thạch", "accessory", 5, 4, "DRAFT: Mini Boss Hộ Vệ Lam Thạch. +5 tấn công, +4 phòng thủ."),
    ("gear", "gear_shadow_cloak", "Áo Choàng Bóng Tím", "armor", 0, 12, "DRAFT: rơi từ Bóng Lá Tím. +12 phòng thủ."),
    ("gear", "gear_toxic_ring", "Nhẫn Nấm Độc", "accessory", 6, 0, "DRAFT: rơi từ Slime Nấm Độc. +6 tấn công."),
    ("gear", "gear_toxic_mask", "Mặt Nạ Nấm Độc", "armor", 2, 12, "DRAFT: Tinh Anh Nấm Độc. +2 tấn công, +12 phòng thủ."),
    ("gear", "gear_stone_vest", "Giáp Đá Mầm", "armor", 0, 15, "DRAFT: rơi từ Vệ Binh Đá Mầm. +15 phòng thủ."),
    ("gear", "gear_dark_charm", "Bùa Nấm Hắc Ám", "accessory", 7, 5, "DRAFT: Mini Boss Nấm Hắc Ám. +7 tấn công, +5 phòng thủ."),
    ("gear", "gear_royal_vest", "Giáp Cận Vệ Nấm", "armor", 0, 18, "DRAFT: rơi từ Nấm Cận Vệ. +18 phòng thủ."),
    ("gear", "gear_king_crown_charm", "Vương Miện Nấm Quỷ", "accessory", 10, 8, "DRAFT: Boss Vua Nấm Quỷ. +10 tấn công, +8 phòng thủ."),
]

# ---------------------------------------------------------------- maps (maps 4-10 fully generated; map 3 only contributes spawns / explore point)
# decks (name, x0, x1, top); climbs (name, x, bottom, top, rope); spawns (type, x, y); explore (id, label, x, y)
MAPS = [
    dict(n=3, id="ch1_glow_forest", name="Rừng Nấm Sáng", gen_layout=False, mon="glow_moss_mushroom", boss="mushroom_chief", boss_x=90,
         decks=[(10, 22, 4.5), (16, 30, 8.5), (46, 58, 6.0), (64, 76, 5.0)],
         spawns=[(-6, 0), (4, 0), (14, 4.5), (24, 8.5), (36, 0), (50, 6.0), (60, 0), (70, 5.0), (78, 0)],
         explore=("ch1_m03_glow_pond", "Hồ Nấm Phát Sáng", 52, 6.0)),
    dict(n=4, id="ch1_flower_hill", name="Đồi Hoa", scene="AK_C1_M04", mon="flower_bee",
         decks=[("BacHoaThap", 6, 18, 4.5), ("CauAnhDao", 14, 28, 8.5), ("BacDaHoa", 40, 52, 6.0), ("DinhDoiHoa", 56, 70, 9.5), ("ThemHoaDong", 76, 88, 5.5)],
         climbs=[("ThangBacHoa", 7, 0, 5.8, False), ("DayCauAnhDao", 15, 4.5, 9.8, True), ("ThangBacDa", 41, 0, 7.3, False), ("DayDinhDoi", 57, 0, 10.8, True), ("ThangThemHoa", 77, 0, 6.8, False)],
         spawns=[(-6, 0), (10, 4.5), (22, 8.5), (34, 0), (46, 6.0), (62, 9.5), (66, 0), (82, 5.5), (94, 0)],
         explore=("ch1_m04_cherry_falls", "Thác Anh Đào", 62, 9.5)),
    dict(n=5, id="ch1_mushroom_cave", name="Hang Nấm", scene="AK_C1_M05", mon="crystal_bat",
         decks=[("BacDaHang", 8, 20, 5.0), ("GacMoThap", 26, 38, 8.0), ("BacXeMo", 44, 56, 5.0), ("GacPhaLe", 52, 66, 9.5), ("TrangThoHang", 72, 86, 6.5)],
         climbs=[("ThangDaHang", 9, 0, 6.3, False), ("DayGacMo", 27, 0, 9.3, True), ("ThangXeMo", 45, 0, 6.3, False), ("DayGacPhaLe", 53, 5.0, 10.8, True), ("ThangTrangTho", 73, 0, 7.8, False)],
         spawns=[(-8, 0), (14, 5.0), (32, 8.0), (40, 0), (50, 5.0), (60, 9.5), (68, 0), (80, 6.5), (94, 0)],
         explore=("ch1_m05_crystal_vein", "Mạch Pha Lê", 60, 9.5)),
    dict(n=6, id="ch1_jade_falls", name="Thác Ngọc", scene="AK_C1_M06", mon="jade_turtle", boss="jade_guardian", boss_x=92,
         decks=[("BacRe", 6, 16, 4.0), ("DaThac", 22, 34, 7.0), ("DinhThac", 40, 52, 10.0), ("BoHo", 58, 70, 5.5)],
         climbs=[("ThangRe", 7, 0, 5.3, False), ("DayDaThac", 23, 0, 8.3, True), ("DayDinhThac", 41, 0, 11.3, True), ("ThangBoHo", 59, 0, 6.8, False)],
         spawns=[(-6, 0), (10, 4.0), (28, 7.0), (36, 0), (46, 10.0), (54, 0), (64, 5.5), (76, 0)],
         explore=("ch1_m06_jade_lake", "Hồ Ngọc", 46, 10.0)),
    dict(n=7, id="ch1_dark_forest", name="Rừng U Tối", scene="AK_C1_M07", mon="shadow_leaf",
         decks=[("ReXoan", 8, 20, 5.0), ("CauTreoDen", 26, 40, 8.5), ("GocCayMuc", 46, 58, 5.5), ("TanCayCao", 52, 66, 10.0), ("DoiSuong", 72, 84, 7.0)],
         climbs=[("ThangReXoan", 9, 0, 6.3, False), ("DayCauTreo", 27, 0, 9.8, True), ("ThangGocCay", 47, 0, 6.8, False), ("DayTanCay", 53, 5.5, 11.3, True), ("ThangDoiSuong", 73, 0, 8.3, False)],
         spawns=[(-8, 0), (14, 5.0), (32, 8.5), (42, 0), (52, 5.5), (60, 10.0), (68, 0), (78, 7.0), (92, 0)],
         explore=("ch1_m07_lantern_grove", "Lùm Đèn Lồng", 33, 8.5)),
    dict(n=8, id="ch1_poison_marsh", name="Đầm Lầy Nấm Độc", scene="AK_C1_M08", mon="toxic_slime", elite="toxic_elite", elite_x=90,
         decks=[("BaiBun", 10, 22, 4.0), ("GoLay", 30, 44, 7.0), ("GoNamDoc", 50, 62, 5.0), ("DinhNamDoc", 68, 82, 8.5)],
         climbs=[("ThangBaiBun", 11, 0, 5.3, False), ("DayGoLay", 31, 0, 8.3, True), ("ThangNamDoc", 51, 0, 6.3, False), ("DayDinhNamDoc", 69, 0, 9.8, True)],
         spawns=[(-8, 0), (16, 4.0), (26, 0), (36, 7.0), (46, 0), (56, 5.0), (64, 0), (76, 8.5)],
         explore=("ch1_m08_glow_bog", "Bãi Nấm Phát Sáng", 75, 8.5)),
    dict(n=9, id="ch1_ancient_ruins", name="Khu Di Tích Cổ", scene="AK_C1_M09", mon="stone_guard", boss="dark_mushroom", boss_x=92,
         decks=[("BacDaCo", 8, 20, 5.5), ("TruDo", 14, 28, 9.5), ("CongPhaLe", 40, 54, 7.0), ("DinhDiTich", 60, 72, 11.0)],
         climbs=[("ThangDaCo", 9, 0, 6.8, False), ("DayTruDo", 15, 5.5, 10.8, True), ("ThangCongPhaLe", 41, 0, 8.3, False), ("DayDinhDiTich", 61, 0, 12.3, True)],
         spawns=[(-8, 0), (12, 5.5), (22, 9.5), (34, 0), (46, 7.0), (56, 0), (66, 11.0), (78, 0)],
         explore=("ch1_m09_crystal_gate", "Cổng Pha Lê Cổ", 66, 11.0)),
    dict(n=10, id="ch1_king_temple", name="Đền Vua Nấm", scene="AK_C1_M10", mon="royal_guard", boss="mushroom_king", boss_x=60, east_portal=False,
         decks=[("BacDaDen", 8, 20, 4.5), ("HanhLangDen", 26, 38, 7.5), ("ThemNgai", 78, 92, 7.5), ("ThemDen", 94, 104, 4.5)],
         climbs=[("ThangBacDen", 9, 0, 5.8, False), ("DayHanhLang", 27, 0, 8.8, True), ("DayThemNgai", 79, 0, 8.8, True), ("ThangThemDen", 95, 0, 5.8, False)],
         spawns=[(-6, 0), (14, 4.5), (32, 7.5), (42, 0), (84, 7.5), (98, 4.5)],
         explore=("ch1_m10_throne_steps", "Bậc Thềm Ngai", 85, 7.5)),
]
MIN_X, MAX_X = -46.0, 106.0
SAFE = (-46.0, -20.0)
EAST_X, WEST_X, ARRIVE_W, ARRIVE_E = 102.0, -40.0, -36.0, 99.0


def fmt(v):
    return ("%g" % v)


def jtext(obj):
    return json.dumps(obj, ensure_ascii=False)


def patch_after(path, anchor_regex, new_lines, probe):
    """Insert new_lines after the first line matching anchor_regex, unless `probe` (a string) is already in the file."""
    text = io.open(path, encoding="utf-8").read()
    if probe in text:
        return False
    lines = text.split("\n")
    for i, ln in enumerate(lines):
        if re.search(anchor_regex, ln):
            nxt = lines[i + 1].strip() if i + 1 < len(lines) else ""
            if nxt.startswith("]") and new_lines and new_lines != ["__Q__"]:   # anchor was the last array element: fix the commas
                if not ln.rstrip().endswith(","):
                    lines[i] = ln.rstrip() + ","
                new_lines = list(new_lines)
                new_lines[-1] = new_lines[-1].rstrip().rstrip(",")
            lines[i + 1:i + 1] = new_lines
            io.open(path, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
            return True
    raise SystemExit("anchor not found: " + anchor_regex + " in " + path)


def monster_lines():
    out = []
    for t in TYPES:
        (tid, name, tier, hp, atk, sec, reach, leash, gmin, gmax, resp, drops, slam, enrage) = t
        d = ", ".join('{ "itemId": "%s", "chance": %s, "min": %d, "max": %d }' % (i, fmt(c), a, b) for i, c, a, b in drops)
        extra = ""
        if slam:
            extra += ', "slamSeconds": %s, "slamDamage": %d, "slamReach": %s, "enrageBelow": %s, "enrageMultiplier": 1.4' % (fmt(slam[0]), slam[1], fmt(slam[2]), fmt(enrage))
        if tid in BOSS_OF_CHAPTER:
            extra += ', "chapterBossOf": %d' % BOSS_OF_CHAPTER[tid]
        out.append('    { "id": "%s", "tier": "%s", "drops": [ %s ], "_draft": "Chương 1 Map 3-10 — DRAFT balance (chờ chủ dự án duyệt)", "name": "%s", "maxHp": %d, "goldMin": %d, "goldMax": %d, "respawnSeconds": %d, "attack": %d, "attackSeconds": %s, "reach": %s, "leash": %s%s },'
                   % (tid, tier, d, name, hp, gmin, gmax, resp, atk, fmt(sec), fmt(reach), fmt(leash), extra))
    return out


def spawn_lines():
    out = []
    for m in MAPS:
        k = 0
        for (x, y) in m["spawns"]:
            k += 1
            out.append('    { "id": "c1m%d_%d", "mapId": "%s", "type": "%s", "x": %s, "y": %s },' % (m["n"], k, m["id"], m["mon"], fmt(x), fmt(y)))
        if m.get("boss"):
            out.append('    { "id": "c1m%d_boss", "mapId": "%s", "type": "%s", "x": %s, "y": 0 },' % (m["n"], m["id"], m["boss"], fmt(m["boss_x"])))
        if m.get("elite"):
            out.append('    { "id": "c1m%d_elite", "mapId": "%s", "type": "%s", "x": %s, "y": 0 },' % (m["n"], m["id"], m["elite"], fmt(m["elite_x"])))
    return out


def item_lines():
    out = []
    for it in ITEMS:
        if it[0] == "junk":
            _, iid, name, price, label = it
            out.append('    { "id": "%s", "name": "%s", "kind": "junk", "icon": "item_junk", "priceGold": %d, "desc": "Đồ rác của %s. Bán cho NPC lấy Vàng." },' % (iid, name, price, label))
        else:
            _, iid, name, slot, atk, df, desc = it
            icon = "item_armor_vest" if slot == "armor" else "item_charm"
            out.append('    { "id": "%s", "name": "%s", "kind": "equipment", "icon": "%s", "equipSlot": "%s", "classes": [0, 1, 2, 3], "attack": %d, "defense": %d, "desc": "%s Không cộng dồn." },' % (iid, name, icon, slot, atk, df, desc))
    return out


def quest_lines():
    out, n = [], 7
    name_of = {t[0]: t[1] for t in TYPES}
    for m in MAPS:
        def q(title, desc, event, need, text, exp, gold, items=None, npc=""):
            nonlocal n
            qid = "ch1_%02d" % n
            n += 1
            ri = ""
            if items:
                ri = ', "rewardItems": [ %s ]' % ", ".join('{ "itemId": "%s", "qty": %d }' % (a, b) for a, b in items)
            out.append('    { "id": "%s", "type": "main", "title": "Chương 1 · %s", "_draft": "DRAFT reward", "desc": "%s", "map": "%s", "npc": "%s",' % (qid, title, desc, m["id"], npc))
            out.append('      "objectives": [ { "event": "%s", "need": %d, "text": "%s" } ], "rewardExp": %d, "rewardGold": %d%s },' % (event, need, text, exp, gold, ri))
        lvl = m["n"]
        q("Tới %s" % m["name"], "Đi qua cổng pha lê để tới %s." % m["name"], "travel:" + m["id"], 1, "Tới %s" % m["name"], 60 + lvl * 10, 0)
        mn = name_of[m["mon"]]
        q("Dọn dẹp %s" % m["name"], "%s đang quấy phá khu vực. Hạ 10 con." % mn, "kill:" + m["mon"], 10, "Hạ 10 %s" % mn, 200 + lvl * 40, 60 + lvl * 15, [("potion_hp_m", 2), ("potion_mp_m", 2)])
        label = m["explore"][1]
        q("Khám phá %s" % label, "Tìm tới %s, nơi có ánh sáng lạ." % label, "explore:" + m["explore"][0], 1, "Tới %s" % label, 90 + lvl * 15, 30 + lvl * 8)
        for key, tag in (("boss", None), ("elite", "Tinh Anh")):
            if m.get(key):
                bn = name_of[m[key]]
                exp = {"mushroom_chief": 700, "jade_guardian": 1000, "toxic_elite": 600, "dark_mushroom": 1400, "mushroom_king": 3000}[m[key]]
                gold = {"mushroom_chief": 300, "jade_guardian": 420, "toxic_elite": 220, "dark_mushroom": 600, "mushroom_king": 1500}[m[key]]
                kind = "Boss" if m[key] == "mushroom_king" else ("Mini Boss" if key == "boss" else "Tinh Anh")
                q("%s %s" % (kind, bn), "Hạ %s (%s) ở cuối %s." % (bn, kind, m["name"]), "kill:" + m[key], 1, "Hạ %s" % bn, exp, gold,
                  [("potion_hp_l", 2), ("potion_mp_l", 2)] if m[key] != "toxic_elite" else [("potion_hp_m", 3)])
    return out


# ---------------------------------------------------------------- generated C#
def cs_server_text():
    maps = []
    for m in MAPS:
        if m["n"] >= 4:
            maps.append('        new("%s", "%s", -50f, 110f, -8f, 30f),' % (m["id"], m["name"]))
    portals = []
    for m in MAPS:
        if m["n"] >= 4:
            prev = "ch1_glow_forest" if m["n"] == 4 else [x for x in MAPS if x["n"] == m["n"] - 1][0]["id"]
            east_prev = "ch1_m%02d_east_m%02d" % (m["n"] - 1, m["n"])
            west = "ch1_m%02d_west_m%02d" % (m["n"], m["n"] - 1)
            portals.append('        new("%s", "%s", %sf, 0f, "%s", %sf, 0.9f),' % (east_prev, prev, fmt(EAST_X), m["id"], fmt(ARRIVE_W)))
            portals.append('        new("%s", "%s", %sf, 0f, "%s", %sf, 0.9f),' % (west, m["id"], fmt(WEST_X), prev, fmt(ARRIVE_E)))
    # map 3 east gate to map 4
    portals.insert(0, '        // (Map 3 east gate and Map 4 west gate are in the list below with the others)')
    rows_ids = [m["id"] for m in MAPS]
    spots = ",\n".join('        ["%s"] = (-36f, 1f)' % i for i in rows_ids if i != "ch1_glow_forest")
    safes = ",\n".join('        ["%s"] = (-46f, -20f)' % i for i in rows_ids if i != "ch1_glow_forest")
    expl = ",\n".join('        new("%s", "%s", %sf, %sf, 3.5f)' % (m["explore"][0], m["id"], fmt(m["explore"][2]), fmt(m["explore"][3])) for m in MAPS)
    return """// <auto-generated> by Tools/server/ch1_maps.py - edit the script, not this file. DRAFT layout/balance (owner has not approved). </auto-generated>
using System.Collections.Generic;

namespace AdventureKing.Server.Domain;

/// <summary>Chapter 1 maps 4-10 (+ Map 3's east gate) as server data: maps, portals, respawn points, safe zones, explore points.</summary>
public static class Ch1ServerData
{
    public sealed record ExplorePoint(string Id, string MapId, float X, float Y, float Radius);

    public static readonly CharacterService.WorldMap[] Maps =
    {
%s
    };

    public static readonly CharacterService.Portal[] Portals =
    {
%s
    };

    public static readonly Dictionary<string, (float x, float y)> RespawnPoints = new()
    {
%s
    };

    public static readonly Dictionary<string, (float minX, float maxX)> SafeZones = new()
    {
%s
    };

    /// <summary>Walking into one of these (server-checked from the hero's reported position) records the quest event "explore:{Id}".</summary>
    public static readonly ExplorePoint[] ExplorePoints =
    {
%s
    };
}
""" % ("\n".join(maps), "\n".join(portals), spots, safes, expl)


def cs_client_text():
    maps = []
    for m in MAPS:
        if m["n"] < 4:
            continue
        decks = ", ".join('new TownLayout.Deck("%s", %sf, %sf, %sf)' % (d[0], fmt(d[1]), fmt(d[2]), fmt(d[3])) for d in m["decks"])
        climbs = ", ".join('new TownLayout.Climb("%s", %sf, %sf, %sf, %s)' % (c[0], fmt(c[1]), fmt(c[2]), fmt(c[3]), "true" if c[4] else "false") for c in m["climbs"])
        prev_scene = "AK_C1_M%02d" % (m["n"] - 1)
        prev_name = [x for x in MAPS if x["n"] == m["n"] - 1][0]["name"]
        next_m = [x for x in MAPS if x["n"] == m["n"] + 1]
        portals = ['new TownLayout.PortalSpot("ch1_m%02d_west_m%02d", %sf, "%s", "%s")' % (m["n"], m["n"] - 1, fmt(WEST_X), prev_scene, prev_name)]
        if next_m:
            portals.append('new TownLayout.PortalSpot("ch1_m%02d_east_m%02d", %sf, "%s", "%s")' % (m["n"], m["n"] + 1, fmt(EAST_X), next_m[0]["scene"] if "scene" in next_m[0] else "", next_m[0]["name"]))
        boss = ""
        if m.get("boss"):
            boss = 'BossType = "%s", BossX = %sf, ' % (m["boss"], fmt(m["boss_x"]))
        if m.get("elite"):
            boss = 'BossType = "%s", BossX = %sf, ' % (m["elite"], fmt(m["elite_x"]))
        maps.append("""            new Ch1Map
            {
                Index = %d, MapId = "%s", Scene = "%s", DisplayName = "%s", Monster = "%s", %sExploreId = "%s", ExploreLabel = "%s", ExploreX = %sf, ExploreY = %sf,
                Decks = new[] { %s },
                Climbs = new[] { %s },
                Portals = new[] { %s },
                Spawns = new[] { %s },
            },""" % (m["n"], m["id"], m["scene"], m["name"], m["mon"], boss, m["explore"][0], m["explore"][1], fmt(m["explore"][2]), fmt(m["explore"][3]),
                     decks, climbs, ", ".join(portals), ", ".join("new Vector2(%sf, %sf)" % (x, y) for x, y in m["spawns"])))
    m3 = [m for m in MAPS if m["n"] == 3][0]
    return """// <auto-generated> by Tools/server/ch1_maps.py - edit the script, not this file. DRAFT layout (owner has not approved). </auto-generated>
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Chapter 1 · Maps 4-10: decks, ladders / ropes, gates, spawn spots, explore point. Map 3 stays hand-written in TownLayout.Ch1GlowForest.</summary>
    public static class Ch1MapData
    {
        public sealed class Ch1Map
        {
            public int Index; public string MapId, Scene, DisplayName, Monster, BossType = "", ExploreId, ExploreLabel;
            public float BossX, ExploreX, ExploreY;
            public TownLayout.Deck[] Decks; public TownLayout.Climb[] Climbs; public TownLayout.PortalSpot[] Portals; public Vector2[] Spawns;
            public const float MinX = %sf, MaxX = %sf;
            public static readonly Vector2 Spawn = new Vector2(-36f, 1f), SafeZone = new Vector2(%sf, %sf);
        }

        public static readonly Ch1Map[] All =
        {
%s
        };

        /// <summary>Map 3 additions (the shell class keeps its first two decks): explore point and the Mini Boss spot.</summary>
        public const string Map3ExploreId = "%s", Map3ExploreLabel = "%s"; public const float Map3ExploreX = %sf, Map3ExploreY = %sf, Map3BossX = %sf;
    }
}
""" % (fmt(MIN_X), fmt(MAX_X), fmt(SAFE[0]), fmt(SAFE[1]), "\n".join(maps), m3["explore"][0], m3["explore"][1], fmt(m3["explore"][2]), fmt(m3["explore"][3]), fmt(m3["boss_x"]))


def main():
    # scene names for chaining
    for m in MAPS:
        m.setdefault("scene", "AK_C1_M%02d" % m["n"])
    changed = []
    if patch_after(os.path.join(CFG, "monsters.json"), r'"id": "pollen_slime"', monster_lines(), '"id": "glow_moss_mushroom"'): changed.append("types")
    if patch_after(os.path.join(CFG, "monsters.json"), r'"id": "c1m2_p8"', spawn_lines(), '"id": "c1m3_1"'): changed.append("spawns")
    if patch_after(os.path.join(CFG, "items.json"), r'"id": "junk_pollen_dust"', item_lines(), '"id": "junk_glow_spore"'): changed.append("items")
    if patch_after(os.path.join(CFG, "quests.json"), r'"id": "ch1_06"', None or ["__Q__"], "\"id\": \"ch1_07\""):
        # the anchor line is the first line of the ch1_06 object (2 lines): move the marker after its 2nd line
        p = os.path.join(CFG, "quests.json")
        t = io.open(p, encoding="utf-8").read().split("\n")
        i = t.index("__Q__")
        del t[i]
        # ch1_06 spans the anchor line + the next line (objectives ... },)
        t[i + 1:i + 1] = quest_lines()
        io.open(p, "w", encoding="utf-8", newline="\n").write("\n".join(t))
        changed.append("quests")
    io.open(os.path.join(ROOT, "Server", "AdventureKing.Server", "Domain", "Ch1ServerData.cs"), "w", encoding="utf-8", newline="\n").write(cs_server_text())
    io.open(os.path.join(ROOT, "Assets", "Scripts", "Gameplay", "Ch1MapData.cs"), "w", encoding="utf-8", newline="\n").write(cs_client_text())
    print("patched:", changed or "nothing (already applied)", "- C# regenerated")


if __name__ == "__main__":
    main()
