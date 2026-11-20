"""Single source of truth for the 2D fashion catalogue (DRAFT balance, owner approves prices / skills).

Generates:
  Server/AdventureKing.Server/config/cosmetics2d.json   (server: ids, class, slot, price, currency, rarity, skill effect numbers)
  Assets/Resources/Fashion/catalog.json                 (client: names, icons, layer info, text of the skills)
Run through make_costumes.py (which also draws the art).

Slots: set (full outfit), top, bottom, head (hat), mask, wings, mount.  Classes: 0 Kiem Si, 1 Phap Su, 2 Sat Thu, 3 Cung Thu.
Skill effects the SERVER applies (all small, additive): damagePct, critPct (added to the 12% base), reducePct (damage taken), moveSpeedPct.
They are passive ("noi tai"): active with the set worn (the set IS the full outfit) / the mount or wings equipped.
"""

HERO = {0: "Swordsman", 1: "Mage", 2: "Assassin", 3: "Archer"}
# hue band of the main outfit colour of each class body (HSV hue 0..1; wraps for red) and its saturation floor
BAND = {
    "Swordsman": dict(lo=0.92, hi=0.05, smin=0.32),   # crimson tabard + cape
    "Mage": dict(lo=0.68, hi=0.88, smin=0.25),        # violet robe + hat
    "Assassin": dict(lo=0.40, hi=0.58, smin=0.18),    # teal leather + hood
    "Archer": dict(lo=0.17, hi=0.40, smin=0.22),      # forest green
}

# (key, name, hue, sat_mul, val_mul, hat, mask, rarity, currency, price, skill_name, skill_desc, effect)
# hat / mask keys are shapes from make_costumes.py; None = no extra piece.
SETS = {
    "Assassin": [
        ("void", "Bóng Đêm Hư Vô", 0.68, 0.9, 0.72, None, "half_white", "epic", "gold", 42000, "Lướt Bóng", "Nội tại: di chuyển nhanh hơn như một bóng đêm.", dict(moveSpeedPct=10)),
        ("blood", "Huyết Nguyệt", 0.99, 1.1, 0.9, "horns", "oni", "epic", "gold", 46000, "Khát Máu", "Nội tại: đòn đánh gây thêm 8% sát thương, như trăng máu thôi thúc.", dict(damagePct=8)),
        ("viper", "Độc Xà Tím", 0.78, 1.0, 0.85, None, "snake", "rare", "gold", 26000, "Độc Tố", "Nội tại: đòn đánh tẩm độc, gây thêm 6% sát thương.", dict(damagePct=6)),
        ("frost", "Tuyết Ảnh", 0.55, 0.35, 1.25, "frost_crown", None, "rare", "gold", 28000, "Băng Bước", "Nội tại: chân nhẹ như tuyết (+8% tốc chạy) và giảm 3% sát thương nhận.", dict(moveSpeedPct=8, reducePct=3)),
        ("aurum", "Hoàng Kim Ám Vệ", 0.12, 1.0, 1.15, "circlet", None, "epic", "gold", 52000, "Chí Mạng Vàng", "Nội tại: +8% tỉ lệ chí mạng.", dict(critPct=8)),
        ("ghost", "Hắc Phong Quỷ", 0.60, 0.08, 0.55, None, "kitsune", "legendary", "diamonds", 320, "Tàng Hình Chớp", "Nội tại: ra đòn từ bóng tối, +10% chí mạng.", dict(critPct=10)),
        ("tide", "Lam Thủy Ảnh", 0.60, 1.0, 1.0, "ribbon", None, "common", "gold", 12000, "Thủy Ảnh", "Nội tại: bóng nước làm giảm 8% sát thương nhận.", dict(reducePct=8)),
        ("ember", "Lửa Ma Trơi", 0.06, 1.05, 1.1, "flame", None, "epic", "diamonds", 180, "Hỏa Ảnh", "Nội tại: lửa ma trơi tăng 10% sát thương.", dict(damagePct=10)),
        ("sakura", "Sương Mai Ninja", 0.93, 0.6, 1.15, "petal", None, "rare", "gold", 30000, "Hoa Anh Đào", "Nội tại: nhẹ như cánh hoa, +12% tốc chạy.", dict(moveSpeedPct=12)),
        ("star", "Thiên Ảnh Tinh", 0.45, 0.9, 1.35, "halo", "half_white", "legendary", "diamonds", 380, "Ảnh Tinh Tú", "Nội tại: +12% chí mạng và +4% sát thương.", dict(critPct=12, damagePct=4)),
    ],
    "Swordsman": [
        ("crimson", "Hồng Kỵ Sĩ Cận Vệ", 0.99, 1.0, 1.0, "plume", None, "common", "gold", 12000, "Ý Chí Thép", "Nội tại: giảm 6% sát thương nhận.", dict(reducePct=6)),
        ("azure", "Thanh Long Kiếm Khách", 0.60, 0.9, 1.0, "kabuto", None, "rare", "gold", 26000, "Long Ngâm", "Nội tại: +6% sát thương.", dict(damagePct=6)),
        ("aurum", "Hoàng Kim Thánh Kiếm", 0.12, 1.0, 1.2, "circlet", None, "epic", "gold", 48000, "Thánh Quang", "Nội tại: +8% sát thương, giảm 4% sát thương nhận.", dict(damagePct=8, reducePct=4)),
        ("ronin", "Lãng Nhân Đêm", 0.70, 0.5, 0.6, "kabuto", "visor", "rare", "gold", 30000, "Nhất Đao", "Nội tại: +8% chí mạng.", dict(critPct=8)),
        ("verdant", "Lục Lâm Hiệp Sĩ", 0.30, 0.9, 0.9, "plume", None, "common", "gold", 13000, "Rừng Che Chở", "Nội tại: giảm 7% sát thương nhận.", dict(reducePct=7)),
        ("frost", "Băng Thiết Giáp", 0.54, 0.35, 1.25, "frost_crown", "visor", "epic", "gold", 50000, "Băng Giáp", "Nội tại: giảm 10% sát thương nhận.", dict(reducePct=10)),
        ("ember", "Hỏa Diệm Chiến Thần", 0.04, 1.1, 1.1, "horns", None, "epic", "diamonds", 190, "Liệt Hỏa", "Nội tại: +11% sát thương.", dict(damagePct=11)),
        ("shadow", "Hắc Ám Vương", 0.78, 0.45, 0.5, "horns", "oni", "legendary", "diamonds", 340, "Vương Uy", "Nội tại: +9% sát thương, +6% chí mạng.", dict(damagePct=9, critPct=6)),
        ("jade", "Ngọc Bích Tướng Quân", 0.38, 0.8, 1.0, "kabuto", None, "rare", "gold", 29000, "Phong Tốc", "Nội tại: +10% tốc chạy.", dict(moveSpeedPct=10)),
        ("sun", "Thái Dương Thiên Tướng", 0.10, 1.1, 1.35, "halo", None, "legendary", "diamonds", 400, "Nhật Quang", "Nội tại: +10% sát thương, +6% tốc chạy.", dict(damagePct=10, moveSpeedPct=6)),
    ],
    "Mage": [
        ("violet", "Tím Mộng Pháp Sư", 0.78, 1.0, 1.0, "wizard", None, "common", "gold", 12000, "Ma Lực Tuôn", "Nội tại: +5% sát thương.", dict(damagePct=5)),
        ("ember", "Hỏa Ma Điện", 0.04, 1.2, 1.1, "flame", None, "epic", "gold", 46000, "Hỏa Ma", "Nội tại: +10% sát thương.", dict(damagePct=10)),
        ("frost", "Băng Sương Nữ Hoàng", 0.55, 0.5, 1.2, "frost_crown", None, "epic", "gold", 48000, "Sương Giá", "Nội tại: +6% sát thương, giảm 4% sát thương nhận.", dict(damagePct=6, reducePct=4)),
        ("sakura", "Anh Đào Phù Thủy", 0.93, 0.6, 1.2, "ribbon", None, "rare", "gold", 28000, "Hoa Rơi", "Nội tại: +9% tốc chạy.", dict(moveSpeedPct=9)),
        ("night", "Dạ Nguyệt Tiên Tri", 0.66, 0.9, 0.6, "wizard", "half_white", "rare", "gold", 30000, "Nguyệt Nhãn", "Nội tại: +8% chí mạng.", dict(critPct=8)),
        ("verdant", "Lục Diệp Hiền Giả", 0.32, 0.9, 0.95, "feather", None, "common", "gold", 13000, "Mộc Hộ", "Nội tại: giảm 7% sát thương nhận.", dict(reducePct=7)),
        ("aurum", "Kim Quang Đại Pháp", 0.12, 1.0, 1.25, "halo", None, "legendary", "diamonds", 360, "Kim Quang", "Nội tại: +9% sát thương, +6% chí mạng.", dict(damagePct=9, critPct=6)),
        ("abyss", "Hư Không Huyền Thuật", 0.82, 0.8, 0.45, "horns", "kitsune", "legendary", "diamonds", 380, "Hư Không", "Nội tại: +12% chí mạng.", dict(critPct=12)),
        ("ocean", "Hải Triều Pháp Sư", 0.58, 1.0, 1.05, "circlet", None, "rare", "gold", 27000, "Triều Dâng", "Nội tại: giảm 8% sát thương nhận.", dict(reducePct=8)),
        ("crimson", "Huyết Ma Nữ Hoàng", 0.98, 1.1, 0.85, "horns", "half_white", "epic", "diamonds", 200, "Huyết Chú", "Nội tại: +11% sát thương.", dict(damagePct=11)),
    ],
    "Archer": [
        ("forest", "Lục Lâm Xạ Thủ", 0.28, 1.0, 1.0, "feather", None, "common", "gold", 12000, "Mắt Ưng", "Nội tại: +5% chí mạng.", dict(critPct=5)),
        ("autumn", "Thu Phong Cung Nữ", 0.07, 1.0, 1.05, "feather", None, "common", "gold", 13000, "Gió Thu", "Nội tại: +8% tốc chạy.", dict(moveSpeedPct=8)),
        ("frost", "Tuyết Nguyên Thợ Săn", 0.54, 0.4, 1.2, "frost_crown", None, "rare", "gold", 28000, "Đạn Băng", "Nội tại: +6% sát thương, giảm 3% sát thương nhận.", dict(damagePct=6, reducePct=3)),
        ("moon", "Nguyệt Thần Xạ Thủ", 0.64, 0.8, 0.8, "circlet", "half_white", "epic", "gold", 46000, "Tên Nguyệt", "Nội tại: +9% chí mạng.", dict(critPct=9)),
        ("sakura", "Anh Hoa Cung Thủ", 0.93, 0.6, 1.2, "ribbon", None, "rare", "gold", 30000, "Mưa Hoa", "Nội tại: +10% tốc chạy.", dict(moveSpeedPct=10)),
        ("ember", "Hỏa Vũ Xạ Thủ", 0.04, 1.15, 1.1, "flame", None, "epic", "gold", 48000, "Tên Lửa", "Nội tại: +10% sát thương.", dict(damagePct=10)),
        ("shadow", "Dạ Hành Săn Đêm", 0.70, 0.5, 0.5, None, "visor", "rare", "gold", 29000, "Săn Đêm", "Nội tại: +8% chí mạng.", dict(critPct=8)),
        ("aurum", "Kim Ưng Hoàng Gia", 0.12, 1.0, 1.25, "circlet", None, "epic", "diamonds", 190, "Ưng Vương", "Nội tại: +8% sát thương, +4% chí mạng.", dict(damagePct=8, critPct=4)),
        ("azure", "Thiên Không Phong Hầu", 0.55, 1.0, 1.15, "halo", None, "legendary", "diamonds", 340, "Phong Hầu", "Nội tại: +12% tốc chạy, +4% sát thương.", dict(moveSpeedPct=12, damagePct=4)),
        ("fae", "Tiên Rừng Huyền Ảo", 0.45, 0.85, 1.3, "petal", "half_white", "legendary", "diamonds", 380, "Tiên Lực", "Nội tại: +8% sát thương, giảm 6% sát thương nhận.", dict(damagePct=8, reducePct=6)),
    ],
}

# Layer items (any class): key, name, shape, tint (hex), rarity, currency, price, extra effect
HATS = [   # female heroes only (Pháp Sư, Cung Thủ): cute / cool / anime headwear, art from Meshy
    ("f_bunny", "Băng Đô Tai Thỏ", "bunny", "#f6a8c8", "common", "gold", 3200),
    ("f_gothic", "Mũ Chóp Gothic", "gothic", "#1b1424", "rare", "gold", 9500),
    ("f_valkyrie", "Vương Miện Valkyrie", "valkyrie", "#cfd8e6", "epic", "gold", 24000),
    ("f_kunoichi", "Băng Đô Kunoichi", "kunoichi", "#2f8a8a", "common", "gold", 4200),
    ("f_magical", "Cài Tóc Ma Pháp Thiếu Nữ", "magical", "#f06ca8", "rare", "gold", 10500),
    ("f_kitsune", "Cài Tóc Cáo Kitsune", "kitsune", "#f08a2a", "epic", "gold", 22000),
    ("f_angel", "Băng Đô Thiên Thần", "angel", "#fff0a0", "rare", "gold", 11000),
    ("f_darkqueen", "Vương Miện Nữ Hoàng Bóng Tối", "darkqueen", "#8a1f2a", "epic", "diamonds", 160),]
MASKS = [   # female heroes only (Pháp Sư, Cung Thủ): art from Meshy
    ("f_cat", "Mặt Nạ Mèo Con", "cat", "#f4ecd8", "common", "gold", 4500),
    ("f_masquerade", "Mặt Nạ Dạ Hội Ren Vàng", "masquerade", "#2a2030", "rare", "gold", 12000),
    ("f_kitsune", "Mặt Nạ Hồ Ly Kitsune", "kitsune", "#f4ecd8", "epic", "gold", 24000),
    ("f_oni", "Mặt Nạ Quỷ Oni", "oni", "#b82a2a", "rare", "gold", 13000),
    ("f_ninja", "Khăn Che Mặt Kunoichi", "ninja", "#2f8a8a", "common", "gold", 5000),
    ("f_goggles", "Kính Gió Hồng Steampunk", "goggles", "#c9922a", "rare", "gold", 9500),]
WINGS = [   # art from Meshy (pixel, glowing); a few carry a small speed / crit passive
    ("f_phoenix", "Cánh Phượng Hoàng Lửa", "phoenix", "#f08a2a", "legendary", "diamonds", 280, dict(moveSpeedPct=4)),
    ("f_fairy", "Cánh Bướm Tiên Pastel", "fairy", "#c8a8f0", "common", "gold", 9000, dict()),
    ("f_seraph", "Cánh Thiên Thần Seraph", "seraph", "#fff6dc", "epic", "gold", 40000, dict()),
    ("f_demon", "Cánh Dơi Quỷ Đỏ Thẫm", "demon", "#8a1f2a", "epic", "gold", 34000, dict(critPct=3)),
    ("f_ice", "Cánh Pha Lê Băng", "ice", "#7ad8f6", "epic", "gold", 36000, dict()),
    ("f_galaxy", "Cánh Ngân Hà Trăng Sao", "galaxy", "#5a3aa0", "legendary", "diamonds", 300, dict(critPct=3)),
    ("f_cherry", "Cánh Hoa Anh Đào", "cherry", "#f6a8c8", "rare", "gold", 15000, dict()),
    ("f_thunder", "Cánh Sấm Sét", "thunder", "#f6e84a", "epic", "diamonds", 240, dict(moveSpeedPct=5)),
]
MOUNTS = [
    ("white_horse", "Bạch Mã Gió", "horse", "#f1ede2", "rare", "gold", 60000, dict(moveSpeedPct=15)),
    ("night_horse", "Dạ Mã Lửa Xanh", "horse_dark", "#2a2442", "epic", "diamonds", 240, dict(moveSpeedPct=20)),
    ("snow_wolf", "Sói Tuyết Cưỡi", "wolf", "#dfe9f6", "rare", "gold", 64000, dict(moveSpeedPct=16)),
    ("fire_tiger", "Hổ Lửa", "tiger", "#e88a3a", "epic", "diamonds", 280, dict(moveSpeedPct=22)),
    ("carpet", "Thảm Bay Hoa Văn", "carpet", "#b8323a", "epic", "gold", 90000, dict(moveSpeedPct=18)),
    ("cloud", "Mây Cưỡi Cầu Vồng", "cloud", "#f4f8ff", "legendary", "diamonds", 360, dict(moveSpeedPct=25)),
]
TOPS = [("t_azure", "Áo Lam Thêu Mây", 0.58, 1.0, 1.0, "common", 5000), ("t_rose", "Áo Hồng Thêu Hoa", 0.93, 0.7, 1.15, "common", 5200),
        ("t_gold", "Áo Gấm Vàng", 0.12, 1.0, 1.2, "rare", 11000)]
BOTTOMS = [("b_azure", "Quần Lam Thêu Mây", 0.58, 1.0, 1.0, "common", 4500), ("b_rose", "Váy Hồng Thêu Hoa", 0.93, 0.7, 1.15, "common", 4800),
           ("b_gold", "Quần Gấm Vàng", 0.12, 1.0, 1.2, "rare", 10000)]
