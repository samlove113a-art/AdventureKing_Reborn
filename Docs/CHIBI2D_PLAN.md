# Chuyển đồ họa 3D → 2D chibi (bake từ model 3D hiện có)

**Cập nhật (chủ dự án chưa duyệt bản bake 3D):** bake từ model 3D ra hình 3/4, không đạt "2D chibi side-view". Đã làm lại Pháp Sư bằng **rig 2D procedural gốc** `Tools/chibi2d/mage_rig.py` (Python của Blender + numpy): đầu to ~45% chiều cao, mắt to biểu cảm (mở/chớp/cười/đau/X), viền mực dày, tô cel, các lớp tách rời (tóc, nón, thân, tay, chân, gậy) để sau này thay trang phục/vũ khí theo lớp. Không dùng Meshy, không tải asset ngoài. Pipeline Unity (Chibi2DArt, HeroVisualStyle) giữ nguyên.

**Hướng cũ (đã bỏ cho Pháp Sư):** bake ngang trực giao từ chính model 3D (Blender 5.2).

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
## 2026-11-18 — Pure 2D (chủ dự án): mẫu Kiếm Sĩ vẽ tay 2D (CHỜ DUYỆT)
- Hướng mới: 100% Pure 2D, cấm sprite bake từ 3D. Sprite Meshy-bake/pixel ở trên bị coi là **tạm**, sẽ thay dần.
- `Tools/chibi2d/swordsman_rig.py` (dùng chung engine `mage_rig.py`): rig 2D thủ công — tóc bạc gai, giáp xanh, khăn/áo choàng đỏ, khiên tròn, kiếm thép; viền mực cứng + cel 2 tông, không blur/glow. 9 clip: Idle 8, Run 8, Jump 4, Fall 3, Climb 8, Hit 4, Death 6, Attack 8 (có vệt chém), Skill 10; khung 384 px, mặt phải.
- Xuất ra `ArtSource/Chibi2D/_Hand/Swordsman/` (kèm `Swordsman_preview.png`), **chưa** vào Unity, **không** ghi đè `ArtSource/Chibi2D/Swordsman` hay prefab `Swordsman_Visual2D`.
- Lệnh: `"C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe" Tools/chibi2d/swordsman_rig.py "" "" --preview`
- Việc kế sau khi duyệt: đưa vào Unity bằng `Chibi2DArt.Build` (đổi nguồn sang `_Hand`), kiểm gameplay/hitbox, rồi làm Cung Thủ, Sát Thủ, Pháp Sư (đã có v2 thủ công), NPC, quái.

## 2026-11-17 — Pipeline Meshy → Blender → sprite pixel (theo yêu cầu "không cắt ảnh")
- `Tools/meshy/character_pipeline.py` tạo model + rig + animation từ **chân dung** trong 4 ảnh tham chiếu (`ArtSource/OwnerProvided/Portraits/`), tên `ref_<class>`. **Credit: 287** (3 model × 30 mới + Kiếm Sĩ 30, rig 4 × 5, animation ~147). Số dư API sau đó: 2.555. Đã ghi `ArtSource/Meshy/manifest.jsonl`.
- `Tools/blender/export_character.py` + `ArtSource/Specs/ref_<class>.json` → FBX (`ArtSource/Chibi2D/_meshy/`, không ghi đè model 3D đang chạy trong game).
- `Tools/blender/bake_chibi_pixel.py` → camera ngang, đầu phóng 1,2×, màu 30 màu/class, viền 1 px, khung 128 px → `ArtSource/Chibi2D/_MeshyPixel/<Class>/` (+ `<Class>_preview.png`).
- **Kết quả thật:** Kiếm Sĩ đọc được (tóc bạc, giáp xanh, kiếm). **Pháp Sư bị dính cả bệ cỏ/nấm** của ảnh tham chiếu vào model; **Cung Thủ chỉ thấy mũ trùm + tóc, thân nhỏ xíu**; Sát Thủ chưa xem kỹ. Nguyên nhân: Meshy dựng luôn nền / khối tóc từ ảnh có nền rừng. Cần ảnh nhân vật **tách nền** (không có bệ đất) cho mỗi class rồi tạo lại (~35 credit/class + animation ~40).
- **Chưa đưa vào Unity:** `Assets/Resources/Chibi2D/*_Visual2D.prefab` hiện vẫn là bản cắt từ ảnh (tạm, trái yêu cầu mới) cho tới khi có model Meshy sạch.

## 2026-11-17 (tiếp) — Meshy v2 sạch nền, đã vào Unity
- Pháp Sư / Cung Thủ / Sát Thủ: tạo concept mới **không nền** bằng Meshy text-to-image (6 credit), rồi `v2_<class>` model + rig + animation. Credit thêm ≈ 240; số dư API 2.315. Kiếm Sĩ dùng `ref_swordsman`.
- Bake lại bằng `bake_chibi_pixel.py` (đầu 1,3–1,45×, cắt sạch phần đất dưới chân, 48 màu, khung 128 px, FRAME_M 1,5) → `ArtSource/Chibi2D/<Class>/` → `Chibi2DArt.BuildAllFromCommandLine` → prefab `*_Visual2D`. Bản cắt từ ảnh đã bị thay thế (còn trong git).
- Test thật: Chibi2DClassTests (3 nghề) + Chibi2DMageTests (3) + SkillOrigin + SkillSync = 8/8 pass.
- **Hạn chế:** pixel hóa từ model 3D nên chi tiết mặt/mắt nhỏ và mất nét; vũ khí Meshy cũ gắn tạm (Pháp Sư cầm gậy đeo lưng); Skill/Attack chưa có VFX riêng; Mage giữ hiệu ứng pixel cũ; chưa có NPC/quái/boss 2D.
