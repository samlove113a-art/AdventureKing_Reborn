# Pipeline sprite pixel Pure 2D — kế hoạch cắt frame + logic animation (DRAFT, chờ duyệt)

Nhánh: `feat/pixel-heroes-20261118`. Chuẩn: pixel-art Dark Fantasy / Action RPG, **thon gọn, đầu ≈ 1/6 chiều cao**, viền tối, tông hơi tối. Ảnh mẫu chỉ là mốc chất lượng, mỗi class thiết kế nguyên bản.

## 1. Lưới & khung (đã chạy thật với Kiếm Sĩ)
- Ô **192×192**, hero cao ≈ 125 px, chân ở hàng pixel 182 (`ground_from_bottom_m` 0,099 / `frame_m` 1,9 ⇒ ppu 101), pivot (0,5; 0,052). Mặt phải, lật bằng scale.x.
- Dải ngang `{Class}_{Clip}.png` = N khung 192×192, PNG RGBA, alpha chỉ 0/255, Point, không nén.
- Cắt: sheet AI (nền magenta, lưới 2×4 / 3×2 / 1×3 có vạch đen) → `Tools/chibi2d/process_pixel_strip.py`: tìm vạch đen theo từng hàng → khử magenta → giữ vùng chính → **một tỷ lệ chung = 136 / chiều cao ô** (mọi clip cùng cỡ hero) → vị trí **theo ô** (chân/trọng tâm = trung vị các khung, nên FX dưới chân không làm hero nhảy) → khung flash trắng của Hit tạo bằng code.

## 2. Bảng clip (mọi class giống nhau, ID class giữ nguyên)
| Clip | Khung | fps | Loop | Ghi chú |
|---|---|---|---|---|
| Idle | 6 | 8 | ✔ | |
| Run | 8 | 14 | ✔ | = "walk/move" |
| Jump | 4 | 10 | | |
| Fall | 3 | 8 | | |
| Climb | 8 | 10 | ✔ | quay lưng, **không vẽ thang** (thang là map) |
| Attack | 6 | 14 | | vệt chém/đạn tích hợp trong khung |
| Skill | 8 | 12 | | FX tích hợp trong khung (lửa/arcane/vệt) |
| Hit | 3 | 10 | | khung giữa = nháy trắng (code) |
| Death | 6–7 | 7 | | tự nhận số khung |

## 3. Logic animation (không viết lại, dùng hệ có sẵn)
`Chibi2DArt.Build(HeroClass)` → sprite Multiple + `.anim` theo clip + `AnimatorOverrideController` phủ lên controller sẵn có `Assets/Art/Characters/{Class}/{Class}.controller` (cùng state/tham số/transition) + prefab `Resources/Chibi2D/{Class}_Visual2D`. Animation event giữ nhịp gameplay: `OnAttackImpact` 0,35 · `OnSkillCast` 0,1 · `OnSkillImpact` 0,68 (theo % thời lượng clip, nên đổi số khung không làm lệch). Chỉ cần thêm: `Chibi2DArt.BuildPixelClassesFromCommandLine` (Mage/Assassin/Archer).
Cung Thủ: mũi tên bay là VFX có sẵn của skill director; sprite chỉ cần tư thế giương/bắn.

## 4. Quy trình mỗi class (`Tools/chibi2d/pixel_class.py`)
`plan` (miễn phí) → `ref N --yes` (ảnh chuẩn, 6 credit/ảnh) → **chủ dự án duyệt ảnh chuẩn**, chép vào `ArtSource/Meshy/ref/{Class}.png` → `gen --yes` (9 sheet × 6 credit) → `process` → xem contact sheet → `install` → Unity build + test PlayMode → so cạnh asset cũ → commit. Giữ asset cũ trong git tới khi class pass.
Ước tính credit: **66/class** (1 ảnh chuẩn + dự phòng 1 + 9 clip), +6 mỗi lần làm lại một dải. 3 class ≈ **200**.

## 5. NPC (7: Captain, Elder, MushroomElder, Musician, Officiant, Smith, Storekeeper)
NPC chỉ cần Idle (6) + Talk/Gesture (4–6), cùng lưới 192 và cùng phong cách; mỗi NPC ≈ 1 ảnh chuẩn + 1–2 sheet ≈ 18 credit ⇒ ≈ **126**. Quái/boss/pet/tile làm sau, cùng mật độ pixel.

## 6. Rủi ro đã biết
- Meshy không đảm bảo nhất quán tuyệt đối giữa các sheet → luôn xem contact sheet, tạo lại dải hỏng.
- FX tích hợp trong khung trùng với VFX 2D riêng của skill director → cần quyết định giữ cái nào khi duyệt.
- Đồ shop (`Paperdoll2D`/outfit) chưa mặc được lên thân pixel mới.
