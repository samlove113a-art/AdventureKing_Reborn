# AdventureKing_Reborn — PROJECT MASTER CONTEXT

> Kiến thức tổng hợp, cập nhật **2026-11-11** từ code + git log + tài liệu trên nhánh `feat/chapter1-mushroom-kingdom` (HEAD `7e9c174`).
> Chi tiết lịch sử từng đợt: `Docs/SESSION_HANDOFF.md`. Kế hoạch Chương 1: `Docs/CHAPTER01_PLAN.md`.
>
> **Chú giải trạng thái:** ✅T = có code + có test tự động pass (theo commit/handoff) · ✅C = có code, chưa test đủ / chưa nghiệm thu · 👁 = chờ chủ dự án duyệt hình ảnh/cân bằng · ⏸ = tạm dừng theo lệnh · 📐 = mới thiết kế · ⬜ = chưa bắt đầu.
> "Test pass" chỉ chứng minh chức năng chạy, **không** chứng minh đạt chất lượng thương mại.

## 1. Tầm nhìn
- MMORPG online, thế giới fantasy nguyên bản, nhân vật **3D chibi** phong cách anime vui nhộn.
- Gameplay **2.5D cuộn ngang**: vật lý Rigidbody2D trên mặt phẳng z=0 + model 3D, `CameraFollow2D`, joystick cảm ứng. Không đổi thể loại/góc camera/công nghệ.
- Nền tảng: Windows PC, Android, iOS (cùng tài khoản server).
- Chất lượng mục tiêu: thương mại; không placeholder làm thành phẩm; không copy MapleStory/game khác; không Missing Script, nền đen, scene trống; tối ưu PC tầm trung + điện thoại.
- Phạm vi dài hạn: **20 chương × 10 map = 200 map**, giới hạn cấp **300**, mục tiêu **30 kênh** server.

## 2. Công nghệ & kiến trúc
| Phần | Công nghệ / vị trí |
|---|---|
| Client | Unity **6000.6.7f1**, URP, toon shader + viền, font Be Vietnam Pro. Code: `Assets/Scripts/Gameplay/` (namespace `AdventureKing.Gameplay`). Editor/pipeline: `Assets/Editor/ArtPipeline/`. |
| Scene | `AK_Entry` (đăng nhập/chọn tướng), `AK_Village` (hub Thị trấn Chuông Gió), `AK_Meadow` (Đồng Cỏ Gió), `AK_GuildHall`, `AK_WeddingHall`, `Chapter1/AK_C1_M01` (Làng Nấm). Scene dựng **bằng code** (`VillageSceneBuilder*.cs`) — sửa builder rồi dựng lại, không sửa tay scene. |
| Server | ASP.NET Core **8** + **SQLite** (`Microsoft.Data.Sqlite`), `Server/AdventureKing.Server`; DB schema **v11** có migration + backup/restore. Domain: Account, Character (maps/portals), Farm (quái, sát thương, skill), Quest, Inventory, Shop, Pet, Welfare (sự kiện/thư/giftcode), RankingTitle, Social (bạn/chat), GuildMarriage, CosmeticShop, DiamondStore, Wallet (sổ cái), Checkin. Config JSON ở `config/` (`*.dev.json` chỉ cho test). |
| Multiplayer | HTTP polling `POST /sync` ~6 Hz (kiểm tốc độ/biên, peers cùng map, chat, badge). Chưa có WebSocket, chưa test >2 client. |
| Test | Server: `dotnet test Server/AdventureKing.Server.Tests`. Unity: EditMode + PlayMode (`Assets/Tests/PlayMode`, nhiều test dùng server thật; `SecondClient.cs` giả lập người chơi thứ 2). Test cần hình ảnh/LateUpdate phải chạy **có cửa sổ** (batchmode không thể hiện tư thế thủ tục). |
| Công cụ | `Tools/meshy/` (meshy.py balance/library/run → `ArtSource/Meshy/manifest.jsonl`), `Tools/blender/` (export nhân vật/đạo cụ, encode video), `Tools/audio/` (nhạc/SFX tổng hợp nguyên bản). |
| Git | `git` không có trong PATH → dùng git của GitHub Desktop (xem `CLAUDE.md`). Shell không có quyền GitHub: chủ dự án push bằng GitHub Desktop. |

## 3. Bảng trạng thái hệ thống (theo bằng chứng trong repo)
### Nền tảng & nhân vật
| Hệ thống | TT | Ghi chú |
|---|---|---|
| Tài khoản, đăng nhập, 4 slot nhân vật, tên tiếng Việt có lọc | ✅T | lưu server; đăng nhập lại về hub an toàn |
| 4 nghề 3D chibi (Kiếm Sĩ, Pháp Sư, Sát Thủ, Cung Thủ), rig + clip Meshy, mặt động | ✅T 👁 | rig Meshy không có xương ngón |
| Cấp/EXP | ✅C | EXP từ nhiệm vụ, đường cong DRAFT 100×cấp; **chưa có EXP theo quái**; giới hạn 300 chưa áp dụng/kiểm |
| Trang bị, túi đồ, thuốc HP/MP | ✅T | |
| Shop thời trang 3D (Gold) + Shop Kim Cương | ✅T | **thanh toán thật tắt**, không phát KC |
| VIP, khung tên VIP/Boss Hunter | ✅C | không ai sở hữu khung; bộ PNG cần QA art |
### Chiến đấu
| Hệ thống | TT | Ghi chú |
|---|---|---|
| Đánh thường, quái, Gold rơi — server quyết định | ✅T | `FarmService` + `monsters.json` |
| 12 skill (3/nghề), MP, cooldown 5/15/20 s, Auto Skill | ✅T | hệ số sát thương DRAFT |
| VFX 12 skill (shader VFXFlow, biên đạo 4 pha, hit-stop, rung, 29 SFX, bản mobile) | ✅C 👁 | **bản nháp 1 chờ duyệt** — `Docs/SKILL_VFX_REVIEW.md` liệt kê điểm chưa đạt |
| Sửa vị trí phát skill (đo bằng probe) | ✅T | `7336e1e`; số đo `QA/SkillOrigin/report.txt`: điểm phát thấp hơn đỉnh đầu 1,03 m, bay ±X |
| Đồng bộ skill cho người chơi khác | ✅T | `7e9c174`; server chỉ ghi skill nó chấp nhận |
| Mini Boss / Boss (phase, telegraph, triệu hồi) | ⬜ | |
| Trạng thái độc/bẫy | ⬜ | cần cho Map 7–8 |
### Thế giới
| Hệ thống | TT | Ghi chú |
|---|---|---|
| Hub Thị trấn Chuông Gió (`village_wind_bell`) | ✅T 👁 | **giữ nguyên** |
| Đồng Cỏ Gió (`meadow_breeze`, slime) | ✅T | **giữ nguyên**; có cổng đông x=78 sang Làng Nấm |
| Cổng dịch chuyển server-validated, màn tải, minimap, vé dịch chuyển | ✅T | |
| Nhiệm vụ chính/phụ/hằng ngày (reset 00:00 VN) | ✅T | |
| Chương 1 Map 1 Làng Nấm | ✅T 👁 | chủ dự án nhận xét "tạm ổn" |
| Chương 1 Map 2–10, `chapter_progress`, mở Chương 2 | ⬜ (📐 trong `CHAPTER01_PLAN.md`) | |
| Chương 2–20 | 📐 | chỉ có phạm vi |
### Online / xã hội / kinh tế
| Hệ thống | TT | Ghi chú |
|---|---|---|
| Bạn bè, chặn, chat thế giới/bang/riêng (lọc, giới hạn tốc độ) | ✅T | test 2 client |
| Bang hội (lập/duyệt/chức vụ/quỹ/cống hiến/nhiệm vụ bang/BXH), Sảnh Bang Hội riêng | ✅T | phí/đóng góp DRAFT, `economyEnabled` tắt ở production |
| Kết hôn (cầu hôn, lễ 2 người, quà cưới), Sảnh Kết Hôn | ✅T | |
| **Lễ cưới cinematic 180 s** | ⏸ WIP `670f6f9` | **không sửa khi chưa có lệnh**; `MultiplayerSocialTests` đang fail ở phần này |
| Pet (2 Gold + 2 VIP), no/đói, nhặt Gold, tự uống thuốc | ✅T | model pet dựng trong engine — **cần art thương mại** |
| Sự kiện/Phúc lợi, hộp thư, giftcode, BXH | ✅T | `events.json` DRAFT `enabled:false` |
| Kênh server (mục tiêu 30), WebSocket | ⬜ | |
| VietQR / thanh toán thật | ⬜ **cấm tự bật** | |
| Đóng gói server + client Windows + Tailscale | ⬜ | `CHAPTER01_PLAN.md` §5.4 |
| Thiết bị Android thật | ⬜ | APK dev build đã build được (11-08); chưa thử máy thật. iOS: chưa build (cần Mac) |

## 4. Chương 1 — Vương Quốc Nấm
Chuỗi: Hub ⇄ Đồng Cỏ Gió ⇄ Map 1 ⇄ … ⇄ Map 10. Mini Boss ở **Map 3, 6, 9** (lệnh chủ dự án thắng concept 3/6/8). Boss **Vua Nấm Quỷ** chỉ ở Map 10; server ghi `chapter_progress` → mở Chương 2 (client không tự mở).
| # | map_id | Tên | Quái | Đặc biệt | TT |
|---|---|---|---|---|---|
| 1 | `ch1_mushroom_village` | Làng Nấm | Nấm Non | vùng an toàn, Trưởng Làng Nấm, 3 nhiệm vụ | ✅T 👁 |
| 2 | `ch1_green_meadow` | Đồng Cỏ Xanh | Slime Phấn Hoa (+Nấm Non) | farm cấp thấp, cối xay, rào gỗ | ⬜ tiếp theo |
| 3 | `ch1_glow_forest` | Rừng Nấm Sáng | Nấm Rêu Lung Linh | Mini Boss Nấm Thủ Lĩnh | ⬜ |
| 4 | `ch1_flower_hill` | Đồi Hoa | Ong Cánh Hoa | nhảy/leo nhiều tầng | ⬜ |
| 5 | `ch1_mushroom_cave` | Hang Nấm | Dơi Pha Lê | hang tối, đèn | ⬜ |
| 6 | `ch1_jade_falls` | Thác Ngọc | Rùa Suối Ngọc | Mini Boss Hộ Vệ Lam Thạch | ⬜ |
| 7 | `ch1_dark_forest` | Rừng U Tối | Bóng Lá Tím | bẫy, sương | ⬜ |
| 8 | `ch1_poison_marsh` | Đầm Lầy Nấm Độc | Slime Nấm Độc | vùng độc | ⬜ |
| 9 | `ch1_ancient_ruins` | Khu Di Tích Cổ | Vệ Binh Đá Mầm | Mini Boss Nấm Hắc Ám | ⬜ |
| 10 | `ch1_king_temple` | Đền Vua Nấm | Nấm Cận Vệ | Boss Vua Nấm Quỷ, mở Ch2 | ⬜ |

Tiêu chuẩn mỗi map: sàn + ≥2 tầng bệ có collider, thang/dây, cổng vào/ra trên server, điểm hồi sinh quái, vùng an toàn quanh cổng/NPC, nhạc + âm môi trường nguyên bản, tên khu vực + minimap, palette theo concept, không clone đổi màu, tối ưu mobile. Concept: `ArtSource/Concepts/Chapter01/` (chỉ tham khảo, không dùng làm texture/nền phẳng).

## 5. Art pipeline
- Meshy: concept text-to-image (6) → image-to-3D (30) → rig (5) → clip thư viện (~3/clip). Blender export (`Tools/blender/export_character.py`, `export_prop.py`) → Unity (`HeroArtPipeline`, `NpcArtPipeline`, prefab đạo cụ `Assets/Art/World/...`). File gốc `ArtSource/Meshy/raw/` **không trong git** — cần sao lưu (`C:\AdventureKing_ArtBackup\`).
- Đạo cụ: tạo mô-đun riêng lẻ, tái dùng + **recolor bằng material** (`C1Prop(... tint:)`), không tạo lại.
- Quái không hai chân: animation thủ tục trong Unity (không rig Meshy).
- Ngân sách Chương 1: trần **2.000** credit trên key hiện tại (số dư đầu 3.242); đã tiêu **292** → còn trong trần **1.708**; số dư API xác nhận lần cuối **2.950** (2026-11-10). Luôn kiểm tra lại số dư trước khi tiêu.
- VFX/âm thanh/nhạc: tự sinh trong Unity/Python (0 credit).

## 6. Quyết định đã được chủ dự án duyệt / ra lệnh
- Chuyển hướng 2D sprite → 3D chibi trên 2.5D (nhánh backup cũ giữ lại).
- Hub + Đồng Cỏ Gió giữ nguyên; Chương 1 nối từ đông Đồng Cỏ Gió; Map 1 có NPC riêng Trưởng Làng Nấm.
- Mini Boss 3/6/9; Boss chỉ Map 10; server mở Chương 2.
- Bảng giá shop + 5.000 Gold khởi đầu (Đợt 5, 11-09) đã duyệt; mọi giá/thưởng/tỉ lệ mới khác = DRAFT.
- Lễ cưới 180 s tạm dừng.
- Map 1: đồ họa "tạm ổn".

## 7. Vấn đề kỹ thuật còn tồn tại
- `FarmTests.Monster_Knocks_Out_Hero...` và `Chapter1Tests` (khi chạy sau FarmTests) **phụ thuộc thứ tự**; pass khi chạy riêng.
- `MultiplayerSocialTests` fail ở phần lễ cưới 180 s (WIP, không sửa).
- Map 1: nền parallax vẫn dùng tranh ruộng bậc thang của thị trấn; chưa có âm môi trường riêng; thanh máu Nấm Non bị bảng tên che khi đứng sát; cổng sang Map 2 chưa có (biển "sắp mở").
- VFX skill: danh sách chưa đạt ở `SKILL_VFX_REVIEW.md` (quầng trắng skill 3, lửa phẳng…).
- Nhánh `origin/fix/skill-vfx-origin-20261110` (`18502d8`, PR #5) **chưa merge** và **không cần merge**: nó sửa cùng lỗi bằng điểm cố định theo collider; HEAD đã sửa tận gốc bằng đo xương tay (`7336e1e`). Merge sẽ xung đột `HeroCombatPresentation.cs`.
- Multiplayer polling chưa thử tải; chưa có kênh.
- Chưa test thiết bị Android/iOS thật, chưa đo FPS mobile.
