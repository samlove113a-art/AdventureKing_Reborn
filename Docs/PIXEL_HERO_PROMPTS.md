# Prompt tạo sprite nhân vật Pixel-RPG chi tiết cao (DRAFT — chờ chủ dự án duyệt)

Chuẩn bắt buộc (chủ dự án, 2026-11-18): high-detail 2D pixel-art kiểu sprite RPG cổ điển cao cấp; giáp/áo choàng/tóc sắc sảo, có **khối (shading theo cụm pixel)** và **viền tối** rõ; tỉ lệ cân đối, thân chắc. **CẤM** vector đơn giản, tay chân gầy, hình lỏng/mờ/gradient.
Ảnh mẫu chỉ là mốc chất lượng + bố cục sheet. Prompt dưới đây tạo hiệp sĩ **nguyên bản** (không ghi tên game/nhân vật nào, không bảo AI "giống hệt" ảnh mẫu).

> Vì sao không vẽ bằng code nữa: `Tools/chibi2d/swordsman_pixel.py` (hình học + shader) chỉ ra mức "vector tô màu", không có tay-vẽ-pixel (cụm pixel, dithering, viền thay đổi nét). Chất lượng này cần nguồn tạo ảnh pixel thật (AI ảnh hoặc họa sĩ). Script chỉ còn dùng làm bộ cắt/kiểm tra khung.

## 0. Quy trình (để sprite dùng được trong Unity)
1. Tạo **1 ảnh chuẩn nhân vật** (mục 2) → duyệt.
2. Dùng ảnh đó làm *reference/image-prompt* cho từng hành động (mục 3), mỗi lần **1 dải ngang** 6–8 khung.
3. Nền **magenta phẳng #FF00FF** (nhiều công cụ không xuất alpha) → mình khử nền thành PNG trong suốt thật, kiểm kích thước/alpha, căn chân, cắt khung, import (Sprite Multiple, Point, không nén).
4. Mỗi class xong → so cạnh ảnh mẫu → mới thay vào game. Giữ asset cũ tới khi pass.

Quy cách mỗi khung: **192×192 px**, nhân vật cao ~120–130 px (đầu ≈ 1/4–1/5 chiều cao: chibi chắc, không cao gầy), chân chạm cùng một đường nền, xoay **sang phải**.

## 1. STYLE LOCK (dán đầu mọi prompt)
```
high-detail 2D pixel art character sprite, classic premium RPG sprite style, strict side view facing right,
crisp discrete pixels with NO anti-aliasing, NO blur, NO smooth gradients, NO vector look, NO 3D render look,
dark selective outline (1 px, darker shade of each material, not pure black everywhere), hand-pixeled clustered shading
with 3-4 tones per material and a clear light source from the top-left, a few dithered highlights on metal,
rich saturated colors, strong readable silhouette, slender athletic heroic proportions (head about 1/6 of body height, broad shoulders,
solid legs, NOT chibi, NOT a short stubby body, NOT mannequin) [owner 2026-11-19: not dwarf-chibi], consistent pixel size across the whole sprite (no mixed-resolution pixels),
fully visible weapon never cropped, flat solid magenta #FF00FF background, no text, no labels, no grid, no checkerboard,
no frame, no shadow on the ground, no extra characters
```

## 2. Hiệp sĩ (Swordsman) — ảnh chuẩn
```
[STYLE LOCK]
single character reference, a young heroic armored knight, spiky swept-back golden blond hair with a long fringe and
two longer back locks, determined blue eyes, battle-worn steel-gray plate armor (layered pauldrons with a crimson trim,
breastplate with a small gold emblem, riveted bracers, tassets, segmented greaves, dark leather boots), long crimson
cloak with tattered lower edge flowing behind him, dark leather belts and a small pouch, a large ornate longsword with
a winged gold cross-guard, a blue gem in the pommel and engraved fuller, held low in the right hand. Colour plan:
steel gray armor, deep crimson cloak and accents, gold trim, warm golden hair, brown leather. Standing idle pose, slight
breathing stance, full body, feet on a flat baseline, canvas 192x192, character about 125 px tall.
```

## 3. Hành động (mỗi dải = 1 prompt; luôn đính kèm ảnh chuẩn)
Thêm sau STYLE LOCK + mô tả ngắn: *"Same character as the attached reference, identical hair, face, armor, cloak and sword in every frame, same pixel size."*

| Clip | Khung | Mô tả pose |
| --- | --- | --- |
| Idle | 6 | thở nhẹ, áo choàng và tóc đung đưa, kiếm hạ thấp |
| Run | 8 | chu kỳ chạy hết cỡ, áo choàng bay ra sau, kiếm xách thấp, chân tiếp đất rõ |
| Jump | 4 | ngồi nén → bật lên → đỉnh nhảy, kiếm giơ chéo |
| Fall | 3 | rơi, tay dang, áo choàng bay lên |
| Climb | 8 | leo thang dây, quay lưng ~3/4 nhưng vẫn cùng thiết kế, kiếm đeo lưng |
| Attack | 6 | gồng kiếm ra sau → chém ngang mạnh → đà chém → thu thế; vệt chém cong **pixel cứng** màu trắng-xanh |
| Skill | 8 | tụ lực (kiếm sáng) → bật lên → bổ xuống từ trên, vệt/sóng lửa đỏ-cam pixel cứng |
| Hit | 3 | giật lùi, nhăn mặt, nháy trắng 1 khung |
| Death | 6 | gục quỵ → ngã ngửa nằm sấp xuống nền, kiếm rơi cạnh bên |

Mẫu prompt dải: `[STYLE LOCK] sprite strip of N frames in one horizontal row, equal 192x192 cells, [CLIP] animation of the character in the attached reference: [mô tả pose]. Feet aligned to the same baseline in every cell, character centered in each cell, weapon and cloak fully inside the cell.`

## 4. Biến thể class khác (cùng STYLE LOCK + cùng chuẩn chi tiết, giữ ID class)
- **Mage:** long layered robe with ornate trim, wide brimmed hat with gold crescent emblem, tall ornate staff with glowing crystal, spell-book at the belt; accent royal purple/ivory/gold. VFX: pixel arcane circles, orbs.
- **Assassin:** hooded lightweight dark-teal leather armor, scarf, dual curved daggers with reverse grip, crouched agile stance; accent teal/black/silver. VFX: sharp pixel slash arcs.
- **Archer:** green and brown ranger gear, feathered cap, quiver, ornate recurve bow; clear draw / release frames and a separate arrow projectile strip. Accent forest green/leather/gold.

## 5. Negative (nếu công cụ hỗ trợ)
```
vector art, flat clip-art, smooth shading, gradients, blur, anti-aliased edges, 3D render, thin limbs, tall slim proportions,
mannequin, chibi baby proportions with tiny body, inconsistent pixel size, text, watermark, UI frame, checkerboard, shadow,
cropped weapon, multiple characters, photo, painterly
```
