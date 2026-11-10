# Chương 1 — Làng Khởi Đầu · Vương Quốc Nấm — Kế hoạch triển khai

> Tài liệu sống: phiên Claude mới đọc file này + `Docs/SESSION_HANDOFF.md` rồi làm tiếp, KHÔNG cần lịch sử hội thoại.
> Nhánh: `feat/chapter1-mushroom-kingdom` (tách từ `670f6f9`). Sao lưu: nhánh `backup/pre-chapter1-20261110` (local, cần publish bằng GitHub Desktop) + `C:\AdventureKing_ArtBackup\git\AdventureKing_Reborn_pre-chapter1_2026-11-10.bundle` + `C:\AdventureKing_ArtBackup\2026-11-10_pre-chapter1\` (database, concept, SkillButtons).

## 0. Trạng thái
| Hạng mục | Trạng thái |
|---|---|
| Sao lưu + nhánh Chương 1 | ✅ (push nhánh backup: chủ dự án bấm Publish trong GitHub Desktop — shell không có quyền GitHub) |
| Đọc concept Chapter01 | ✅ (master board, 10 map, 9 creature crop, brief 14 quái) |
| Số dư Meshy key đang cấu hình | ✅ 3.242 (3.232 monthly + 10 permanent), trần giai đoạn này **2.000** |
| Map 1 chuẩn | ⏳ chưa bắt đầu |
| Map 2–10, quest, Mini Boss, Boss, unlock Ch2 | ⏳ |
| Server đóng gói + client Windows + Tailscale | ⏳ |
| Lễ cưới 180 s | ⏸ WIP (`670f6f9`), tạm dừng theo lệnh chủ dự án — không sửa tiếp |

## 1. Quyết định đã chốt (đừng hỏi lại)
1. **Không thay nội dung đã duyệt**: Thị trấn Chuông Gió (`village_wind_bell`, AK_Village) giữ nguyên làm **hub chính** (Trưởng Làng chào người mới, cửa hàng, Sảnh Bang Hội, Sảnh Kết Hôn). Đồng Cỏ Gió (`meadow_breeze`, AK_Meadow, slime) giữ nguyên làm **bãi luyện trước chương**.
2. **Chuỗi bản đồ**: Hub ⇄ Đồng Cỏ Gió ⇄ **Map 1 Làng Nấm** ⇄ Map 2 ⇄ … ⇄ Map 10. Map 1 nối từ đầu đông của Đồng Cỏ Gió (cổng mới). Map 1 có NPC riêng **Trưởng Làng Nấm** giao chuỗi nhiệm vụ chương (Trưởng Làng ở hub giới thiệu sang).
3. **Mini Boss ở Map 3, 6, 9** (theo lệnh chủ dự án; concept ghi 3/6/8 → lệnh thắng). Nấm Hắc Ám (concept map 8) chuyển thành Mini Boss **Map 9** (Khu Di Tích Cổ); Map 8 giữ quái độc thường.
4. **Boss chương chỉ ở Map 10** (Vua Nấm Quỷ). Server ghi `chapter_progress` khi xác nhận hạ Boss → mới mở Chương 2. Client không thể tự mở.
5. Góc nhìn/điều khiển giữ nguyên: gameplay 2.5D cuộn ngang (Rigidbody2D + model 3D chibi), CameraFollow2D, joystick cảm ứng.
6. Không dùng ảnh concept 2D làm nền phẳng thay môi trường 3D. Nền xa (parallax) được phép như AK_Village, nhưng mặt đất, bệ, cầu, đạo cụ gần là 3D.
7. Mọi giá/tỉ lệ rơi/EXP/Gold mới = **DRAFT** trong config, không mở thanh toán, không phát Kim Cương.
8. Không tiêu credit Meshy cho việc làm được bằng Unity/Blender (IK, collider, terrain, LOD, recolor).

## 2. Hệ thống có sẵn sẽ tận dụng (đã xác minh)
- **Chiến đấu server**: `Server/.../Domain/FarmService.cs` + `config/monsters.json` (types: maxHp, gold, respawn, attack, reach, leash; spawns theo `mapId`) — server quyết định HP, sát thương, rơi Gold, hồi sinh, quái đánh trả, HP anh hùng. Client: `FarmField.cs`, `FarmMonsterView.cs`, `EnemyChase2D.cs`, `HeroCombatPresentation.cs`.
- **Kỹ năng/MP**: `config/skills.json`; nhiệm vụ: `QuestService.cs` + `config/quests.json` + `QuestPanel/QuestUi/NpcQuestMarker`; túi đồ/thuốc: `InventoryService` + `items.json`; Pet nhặt Gold/tự dùng thuốc: `PetService` + `pets.json`.
- **Bản đồ/cổng**: `CharacterService.cs` (bảng WorldMap + Portal, server kiểm tra cổng), client `TownLayout` + `VillageSceneBuilder*.cs` (dựng scene bằng code: sàn, bệ, thang, dây, cổng, đạo cụ Meshy, ánh sáng, parallax).
- **Pipeline art**: `Tools/meshy/meshy.py` (balance/library/run, ghi `ArtSource/Meshy/manifest.jsonl`), `Tools/blender/export_character.py`, `HeroArtPipeline`/`NpcArtPipeline` (import, rig, clip, Animator).
- Cần thêm: **EXP theo quái** (kiểm tra `monsters.json` chưa có trường exp), **trạng thái độc/bẫy**, **Boss phase/kỹ năng/telegraph**, **chapter_progress** (DB migration mới).

## 3. Bảng thiết kế 10 map
| # | map_id (server) | Scene | Tên | Môi trường (concept) | Quái thường | Đặc biệt |
|---|---|---|---|---|---|---|
| 1 | `ch1_mushroom_village` | AK_C1_M01 | Làng Nấm | nhà nấm, quảng trường, đài phun pha lê; ấm đỏ-cam-ngọc | Nấm Non (ngoài làng) | vùng an toàn trong làng, NPC Trưởng Làng Nấm, hướng dẫn |
| 2 | `ch1_green_meadow` | AK_C1_M02 | Đồng Cỏ Xanh | bệ cỏ nhiều tầng, cối xay gió, rào gỗ, hoa | Slime Phấn Hoa (+Nấm Non) | farm cấp thấp |
| 3 | `ch1_glow_forest` | AK_C1_M03 | Rừng Nấm Sáng | nấm phát quang, gỗ mục, suối; chàm-cyan-tím | Nấm Rêu Lung Linh | **Mini Boss Nấm Thủ Lĩnh** |
| 4 | `ch1_flower_hill` | AK_C1_M04 | Đồi Hoa | cầu gỗ, anh đào, bậc đá, thác nhỏ | Ong Cánh Hoa | nhảy/leo nhiều tầng |
| 5 | `ch1_mushroom_cave` | AK_C1_M05 | Hang Nấm | hầm đá, xe mỏ, mạch pha lê | Dơi Pha Lê | hang tối, đèn |
| 6 | `ch1_jade_falls` | AK_C1_M06 | Thác Ngọc | thác rộng, rễ cây, hồ | Rùa Suối Ngọc | **Mini Boss Hộ Vệ Lam Thạch** |
| 7 | `ch1_dark_forest` | AK_C1_M07 | Rừng U Tối | cây xoắn, đèn treo, sương tím | Bóng Lá Tím | bẫy (gai/rễ), sương |
| 8 | `ch1_poison_marsh` | AK_C1_M08 | Đầm Lầy Nấm Độc | bùn độc, nấm phát sáng | Slime Nấm Độc (+Nấm Độc) | vùng độc (trạng thái) |
| 9 | `ch1_ancient_ruins` | AK_C1_M09 | Khu Di Tích Cổ | cổng pha lê, đá rêu, trụ đổ | Vệ Binh Đá Mầm | **Mini Boss Nấm Hắc Ám** |
| 10 | `ch1_king_temple` | AK_C1_M10 | Đền Vua Nấm | đấu trường, cột, ngai, đỏ-tím | Nấm Cận Vệ (triệu hồi) | **Boss Vua Nấm Quỷ**, mở Chương 2 |

Mỗi map: sàn + ≥2 tầng bệ có collider thật, thang/dây, cổng vào/ra (server), điểm hồi sinh quái, vùng an toàn quanh cổng/NPC, nhạc nền + âm thanh môi trường nguyên bản (Tools/audio), tên khu vực + minimap, ánh sáng/palette theo concept, không Missing Script, tối ưu mobile.

## 4. Quái & ngân sách Meshy (dự toán, trần 2.000)
Giá tham chiếu từ manifest: concept text-to-image 6 · image-to-3D 30 · rig 5 · animation ~3/clip.
| Nhóm | Số | Cách làm | Dự toán |
|---|---|---|---|
| Quái hai chân (Nấm Non, Nấm Rêu, Bóng Lá, Vệ Binh Đá Mầm, Nấm Cận Vệ) | 5 | concept → model → rig → Idle/Walk/Attack/Hit/Death | ~62 × 5 = 310 |
| Quái không hai chân (Slime Phấn Hoa, Ong, Dơi, Rùa, Slime Độc) | 5 | concept → model; animation thủ tục trong Unity (nhún, bay, vỗ cánh, bò) | ~36 × 5 = 180 |
| Mini Boss (Nấm Thủ Lĩnh, Hộ Vệ Lam Thạch, Nấm Hắc Ám) | 3 | concept 2 góc → model → rig → 7–8 clip | ~85 × 3 = 255 |
| Boss Vua Nấm Quỷ | 1 | concept 2 góc → model → rig → 9–10 clip (+1 lần làm lại) | ~140 |
| Đạo cụ mốc theo map (nhà nấm, cối xay, nấm phát sáng, xe mỏ, cổng pha lê, ngai…) | ~25 | concept → model; tái dùng nhiều lần, recolor bằng material | ~900 |
| **Tổng** | | | **~1.785–1.885** |
Quy tắc: làm **1 mẫu, kiểm tra, rồi mới làm loạt**; ghi credit từng tác vụ vào `ArtSource/Meshy/manifest.jsonl` và bảng §6. Luôn đặt key từ biến môi trường **User** trước khi gọi (`[Environment]::GetEnvironmentVariable('MESHY_API_KEY','User')`) — tiến trình shell cũ có thể giữ key tài khoản cũ.

## 5. Thứ tự thực hiện (nhóm có kiểm thử rõ)
1. **Map 1 chuẩn**: kiến trúc map dữ liệu (`ChapterMaps` layout + builder), server map/portal/spawn, Nấm Non (model+anim), NPC Trưởng Làng Nấm + quest đầu, nhạc/âm thanh, ảnh Game View. Kiểm thử PlayMode: đi từ hub → Đồng Cỏ Gió → Map 1, đánh quái, nhận quest, lưu tiến trình.
2. Map 2–9 lần lượt (mỗi map một commit + ảnh), Mini Boss 3/6/9 (telegraph, thanh máu, thưởng server).
3. Map 10 + Boss (intro camera, phase, triệu hồi) + `chapter_progress` + khóa Chương 2 (server test chống nhân đôi).
4. Đóng gói server (self-contained, start/stop, autostart tùy chọn, backup/restore, log, số online) + client Windows (.exe, shortcut, server offline, `server.json` cấu hình) + hướng dẫn Tailscale.
5. Kiểm thử 2 → 3–5 → 10 client, tối ưu, regression cuối giai đoạn.

## 6. Nhật ký credit Meshy (key đang cấu hình)
| Ngày | Tác vụ | Credit | Số dư sau |
|---|---|---|---|
| 2026-11-10 | (kiểm tra số dư) | 0 | 3.242 |

## 7. Nhật ký quyết định / lỗi
- 2026-11-10: Mini Boss 3/6/9 (lệnh chủ dự án) thay vì 3/6/8 (concept).
- 2026-11-10: hub + Đồng Cỏ Gió giữ nguyên; Chương 1 nối từ đông Đồng Cỏ Gió.
- 2026-11-10: tiến trình shell giữ MESHY_API_KEY cũ (khác key User) → luôn nạp key từ biến môi trường User.
