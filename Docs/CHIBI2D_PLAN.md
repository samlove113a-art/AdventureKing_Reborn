# Chuyển đồ họa 3D → 2D chibi (bake từ model 3D hiện có)

**Hướng đã chọn:** bake ngang trực giao từ chính model 3D (Blender 5.2) thành sprite sheet; không tốn credit Meshy, giữ model 3D gốc, gameplay/server không đổi.

## Pipeline
1. `Tools/blender/bake_chibi2d.py` + `ArtSource/Specs/chibi2d_{Tên}.json` → `ArtSource/Chibi2D/{Tên}/{Tên}_{Clip}.png` (384 px/khung, viền mực 3 px, đáy chân cố định) + `_frames.json`.
2. `Chibi2DArt.Build(HeroClass)` (Editor) → import sprite, clip sprite theo từng trạng thái, `AnimatorOverrideController` phủ lên **controller 3D có sẵn** (cùng state/tham số/transition ⇒ `CharacterVisual3D`, người chơi khác, skill director chạy nguyên), prefab `Resources/Chibi2D/{Class}_Visual2D`. Animation event (`OnAttackImpact`, `OnSkillCast`, `OnSkillImpact`) giữ đúng nhịp gameplay.
3. `HeroVisualStyle` chọn thân: `ak_visual_style` = `2d`/`3d` (hoặc env `AK_VISUAL_STYLE`). Mặc định **3d** cho tới khi chủ dự án duyệt. Lớp chưa có thân 2D tự rơi về 3D, nên chuyển được từng nhóm.
4. 2D lật bằng scale.x (không xoay yaw).

## Tiến độ
| Nhóm | Trạng thái |
|---|---|
| Pháp Sư (Idle/Run/Jump/Fall/Climb/Hit/Death/Attack/Skill) | ✅ bake + chạy thật (QA/Chibi2D) · chờ duyệt mỹ thuật |
| Kiếm Sĩ, Sát Thủ, Cung Thủ | ⏳ cần spec + bake (cùng pipeline) |
| NPC (7), Nấm Non, Slime Phấn Hoa, Boss | ⏳ |
| Trang phục / vũ khí theo lớp (layer) | ⏳ chưa làm: bake riêng từng lớp (thân, vũ khí, nón/áo) cùng camera để xếp chồng |
| Đo FPS mobile, test điện thoại thật | ⏳ |

## Chưa làm / hạn chế
- Mage 2D nhỏ hơn thân 3D khoảng 1/3 (tỉ lệ khung); cần chỉnh `frame_m`/`heroScale` khi duyệt.
- `HeroOutfit` (đồ shop) chỉ tác dụng lên thân 3D; thân 2D chưa mặc đồ.
- Không dùng Unity 2D Animation/Skeletal Rig (gói chưa cài); dùng sprite-frame animation.