# WORK_PROGRESS — AdventureKing_Reborn 2D chibi (cập nhật 2026-11-17, nhánh `feat/chapter1-mushroom-kingdom`)

Quy tắc: không làm lại phần đã xong; phiên sau đọc file này trước.

## ĐÃ HOÀN THÀNH (giữ nguyên)
| Hạng mục | Bằng chứng |
|---|---|
| Server, quest, shop, pet, bang hội, multiplayer (không đổi) | server tests 175/175 (commit e86fb88) |
| Map 1 Làng Nấm, Map 2 Đồng Cỏ Xanh (thật) | `Chapter1Tests` PlayMode, ảnh `QA/Chapter1/` |
| Cổng Map 2 → Map 3, parallax riêng từng map, Quest UI mobile | 3055315, `Chapter1Tests` |
| Rớt đồ / túi đồ / bán đồ rác / mở rộng túi | e86fb88, `DropInventoryTests` + `DropInventoryUiTests` |
| Pipeline Meshy → Blender → sprite pixel cho 4 nghề | `Tools/blender/bake_chibi_pixel.py`, `ArtSource/Chibi2D/<Class>`, e1df899 |
| 4 nghề 2D vào Unity: Idle/Run/Jump/Fall/Climb/Attack/Skill/Hit/Death | `Chibi2DClassTests`, `Chibi2DMageTests` (QA/Chibi2D) |
| Paper-doll (Áo/Mũ/Vũ khí một renderer mỗi slot) | `Chibi2DMageTests.Pixel_Material...` |
| Shader sprite không phát sáng (`AdventureKing/PixelSprite`) | cùng test |
| NPC (7) + Nấm Non + Slime Phấn Hoa/Bông/Rêu thành 2D, runtime swap | `Chibi2DActorTests`, commit 2a976d9 |
| 2D là mặc định (`HeroVisualStyle.DefaultStyle="2d"`); 3D giữ nguyên làm dự phòng (`ak_visual_style=3d`) | `Default_Style_Is_2D...` |

## CẦN SỬA (có sản phẩm nhưng chưa đạt chuẩn "vẽ tay thương mại")
- **Mặt / mắt 4 nghề**: nhìn ngang nên mắt nhỏ, mất nét do pixel hóa 128 px. Hướng: bake 192–256 px cho phần đầu, hoặc vẽ lớp mặt 2D riêng (như `mage_rig.py` đã làm) ghép lên đầu; tăng HEAD_SCALE cho Cung Thủ (mũ trùm che mặt).
- **Vũ khí 4 nghề**: gậy Pháp Sư cầm như đeo lưng, kiếm Kiếm Sĩ thấp; cần chỉnh socket (`ArtSource/Specs/ref_*.json` weapons.tip_dir/roll) rồi bake lại clip Attack/Skill.
- **Sát Thủ**: vài khung Attack/Skill bị cắt mép khung; cần FRAME_M lớn hơn cho clip xoay.
- **Hiệu ứng skill**: chỉ có 3 hiệu ứng pixel tạm cho Pháp Sư (`Resources/Chibi2D/Fx`), chưa gắn vào `SkillVfxDirector`; Kiếm Sĩ/Cung Thủ/Sát Thủ chưa có.
- **Trang phục đổi được**: chỉ Pháp Sư có `fire_mage`/`frost_mage`; chưa có Mũ/Vũ khí art riêng, chưa nối id shop thật.
- Kích thước slime/Nấm Non mới chỉnh (0,42–0,8) chưa kiểm lại bằng ảnh sau chỉnh.
- Map 3 chỉ là khung (không quái, không Mini Boss).

## CHƯA LÀM
- Pet 2D (di chuyển, nhảy, biểu cảm đói).
- Elite, Mini-boss, Boss (Nấm Thủ Lĩnh, Vua Nấm Quỷ) — chưa có model/AI/kỹ năng/`chapter_progress`.
- Map 3 (đầy đủ) → Map 10; thiết kế địa hình nhiều tầng, thang/dây, đường bí mật.
- Packaging server/client, Tailscale, build Windows/Android.
- Đo FPS/điện thoại thật.

## NGÂN SÁCH MESHY
Dùng trong Chương 1: ≈ 687 / 2.000 (xem `Docs/CHAPTER01_PLAN.md`). Số dư API khoảng 2.315. Không tiêu thêm cho việc hiện tại.

## TIẾP THEO (thứ tự đề xuất)
1. Chạy lại hồi quy đầy đủ, sửa lỗi do mặc định 2D.
2. Sửa mặt/vũ khí 4 nghề (không tạo model mới).
3. VFX 4 nghề nối `SkillVfxDirector`.
4. Pet 2D → Map 3–10 + Mini-boss/Boss.

## HỒI QUY ĐẦY ĐỦ SAU KHI ĐẶT 2D MẶC ĐỊNH (2026-11-17, commit 2a976d9)
Server 175/175. PlayMode 71 test: 63 pass, 2 bỏ qua, 6 fail:
- 3 fail là phần đám cưới 180 giây đang tạm dừng (không đụng): `Two_Players_..._Marry_In_The_Wedding_Hall`, `Archer_And_Assassin_Key_Moments`, `Swordsman_And_Mage_Key_Moments`.
- `Hub_To_Meadow_To_Mushroom_Village_Fight...`: fail trong lần chạy đầy đủ nhưng **pass khi chạy riêng** → nhiễu thứ tự test, không phải lỗi 2D.
- **`Town_Portals_Lead_Into_Both_Halls_And_Back`** và **`Town_Screens_Pass_Ui_Audit_At_All_Resolutions`** fail cả khi chạy riêng — **CẦN KIỂM TRA ĐẦU TIÊN SÁNG MAI**: chữ "VIP" chồng "3›2›1" trên điện thoại 19.5:9 (UI audit), và "village BGM playing and audible". Chưa biết do 2D, do UiStandard (Match 0.5, e86fb88) hay do QuestPanel; so sánh với kết quả trước e86fb88 (bản đó pass: hall 8/8).
