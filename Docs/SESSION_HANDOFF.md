# Adventure King Reborn — Bàn giao phiên làm việc

**Ngày:** 2026-11-08 · **Repo:** `samlove113a-art/AdventureKing_Reborn` · **Nhánh:** `feat/diamond-store-catalog-20261107`
**Commit đã có trên GitHub trước phiên:** `cc3905a` · **Commit của phiên này:** xem `git log` (mục 9 ghi trạng thái push).

> Game **chưa đạt chuẩn thương mại**. Tài liệu này chỉ ghi điều đã chạy và kiểm thử thật; phần nào chưa kiểm chứng được ghi rõ.

---
## ★ Cập nhật mới nhất — 2026-11-08 chiều: Giai đoạn A, đợt 1 (theo "Yêu cầu tổng hợp phát triển")
**Commit:** "Phase A: sign/dialog text fit, round joystick, phone HUD, Meshy audit" trên `feat/diamond-store-catalog-20261107`, nối tiếp `8ff5bbb` (SHA: xem `git log -1`).

### A0. Kiểm tra đầu phiên
- Nhánh local = `origin` = `8ff5bbb`, **không có thay đổi chưa commit, không có stash** trước khi sửa.
- Chạy lại trên máy này (không dùng kết quả cũ): server **72/72**, EditMode **4/4**, PlayMode **26 đạt / 0 lỗi / 2 bỏ qua** → trùng báo cáo trước.
- Sao lưu hiện có: `C:\AdventureKing_ArtBackup\` (raw Meshy 11-07, 11-08, session, git bundle) + zip khung tên của chủ dự án.

### A1. Kiểm toán credit Meshy (chỉ gọi API đọc số dư, **không tạo tác vụ mới**)
- Số dư thực tế qua API lúc kiểm tra: **931** (681 tháng + 250 vĩnh viễn) — khớp báo cáo.
- `ArtSource/Meshy/manifest.jsonl` (79 tác vụ, 0 tác vụ lỗi bị tính tiền) cộng lại: 11-07 = **479**; 11-08 = **798** → 1729 − 798 = 931, **khớp tuyệt đối**.
- Chi tiết 798 credit ngày 11-08: đợt sáng 144 (19 ảnh concept = 114, 1 model tre = 30); đợt sau khi nâng giới hạn key 654 (19 model image-to-3D × 30 = **570** — 15 đạo cụ còn lại (tre đã làm buổi sáng), 1 khối đất, 3 NPC; 4 ảnh concept = 24; 3 rig = 15; 3 bộ animation NPC = 45).
- **Vượt ngân sách 600 credit đã duyệt khoảng 198 credit.** Nguyên nhân: ngân sách 600 được hiểu theo đợt sau khi chủ dự án nâng giới hạn key, nhưng đợt sáng (144) đã tiêu trước đó, và riêng 20 model 3D đã là 600. Đây là lỗi quản lý ngân sách của Claude, không phải phí ẩn. **Từ nay: không tạo tác vụ Meshy tốn credit nào cho đến khi chủ dự án duyệt ngân sách mới**; mọi đợt sau sẽ ghi dự toán trước khi chạy.

### A2. Đã sửa và có kiểm thử
| Lỗi | Sửa | Bằng chứng |
|---|---|---|
| Chữ bảng hiệu tràn khỏi bảng ("Phố Thợ Rèn & Chợ Ph…") | `SignPanelMeasure`: bắn lưới tia từ phía camera vào model bảng hiệu, đọc texture tại điểm chạm, lấy vùng giấy da liên thông lớn nhất ⇒ đo được mặt bảng thật **0.76 × 0.92 m**; chữ đặt đúng mặt phẳng đó, lề 12%/14%, tự co chữ; câu chữ xuống dòng theo nghĩa | `QA/WorldCaptures/hub_03_smithy_street.png`, `sign_zoom_smithy.png`, `QA/Logs/sign_panel_samples.png` (ảnh những gì tia "nhìn thấy") |
| Chữ hộp thoại NPC dính viền trái, sát nút | Hộp 1240×320, vùng chữ 940×160 nằm trong lòng khung (đo trên ảnh chụp), tự co 20–26, nút ở hàng riêng | `QA/HubTest/00_elder_greeting.png`, `00b_elder_greeting_phone.png` |
| Kiểm thử tự động chữ không tràn | `AssertTextFits`: dựng chữ hai lần (cắt / không cắt) — số ký tự phải bằng nhau; có đối chứng âm (văn bản quá dài phải bị phát hiện) | `TownHubTests`: hộp thoại 199/199 ký tự (PC + điện thoại), 3 bảng hiệu đủ ký tự |
| Điện thoại vẫn hiện hướng dẫn phím | `TouchControlsVisibility.hideOnTouch` | `QA/HubTest/01b_spawn_phone_19.5x9.png` |
| D-pad 4 nút, ▼ sát mép | **Joystick tròn** (`VirtualJoystick2D`): theo đúng 1 ngón (multi-touch), vùng chết 0.22, ngang ≥0.3 = chạy tối đa (tránh trượt chân), dọc cần ≥0.6 (chạy chéo không vô tình leo thang), thả/ẩn là về giữa. Nút phải: Đánh 200, Nhảy 160, Tương tác 130, cách mép ≥ 60 | `MovementTests.Joystick_Moves_Ignores_Jitter_Second_Finger_And_Recentres` |

**Kết quả cuối đợt:** server **72/72** · EditMode **4/4** · PlayMode **27 đạt / 0 lỗi / 2 bỏ qua** (thêm 1 test joystick) · log PlayMode không có Exception. Video Play Mode: `QA/Video/town_playmode_2026-11-08_phaseA.mp4`.

### A3. Gói khung tên của chủ dự án
Lưu nguyên vẹn ở `ArtSource/OwnerProvided/Nameplates_2026-11-08/`. **Chưa đưa vào game** vì: độ phân giải 181–399 px, `Basic` còn ký tự thừa ".t", `VIP_13`/`BossHunter_10` có vệt mép trái, `BossHunter_03` vạch mép trên, `BossHunter_05` mất cánh phải (chi tiết: `Docs/ART_ASSET_PROVENANCE.md`). Hệ thống khung tên (server cấp qua Thư, trang bị 1 khung) làm ở Giai đoạn F; cần bộ ảnh sạch ≥ 512 px.

### A4. Chưa làm được trong đợt này (vẫn thuộc Giai đoạn A)
- **Thử APK trên điện thoại Android thật:** không có thiết bị kết nối với máy → **chưa kiểm chứng**.
- Mối nối khối đất, mái ngói mép sàn, dây leo, đèn lồng phát sáng: chưa sửa.
- Biểu cảm khuôn mặt / ngón tay (rig Meshy không có xương mặt, ngón): chưa làm — cần rig mặt trong Blender, không tốn credit.
- Bộ nút skill 4–6 ô + Auto Skill: chờ hệ thống kỹ năng (Giai đoạn B/G).

### A5. Cần chủ dự án quyết định
1. Ngân sách Meshy cho đợt kế tiếp (đang **dừng hoàn toàn**).
2. Ngưỡng VIP 1–14 (VIP 15 = 50.000.000 VND đã chốt).
3. Bộ khung tên bản sạch, độ phân giải cao (hoặc cho phép Claude vẽ lại bằng công cụ miễn phí).
4. Nguồn nhạc nền (CC0 / thương mại đã xác minh).
5. Kết nối 1 điện thoại Android (USB debugging) để thử APK.
---

## 1. Yêu cầu phải giữ nguyên
- Nền tảng: **Windows PC (chuột + phím), Android và iPhone/iPad (ngang, cảm ứng)**, cùng tài khoản server.
- Đồ họa **chibi 3D trên gameplay 2.5D**, chất lượng thương mại, một art direction thống nhất (`Docs/ART_DIRECTION_TOWN.md`).
- Thiết kế **nguyên bản**: chỉ học cảm giác thị trấn nhiều tầng kiểu MMORPG cổ điển; không sao chép MapleStory (map, nhân vật, cổng, UI, nhạc).
- Server-authoritative; không lưu nhân vật bằng PlayerPrefs; Google/Facebook và thanh toán thật **tắt** đến khi chủ dự án duyệt.
- **Không mua credit, không thuê server, không thanh toán.** Không gửi bản build thô cho chủ dự án.

## 2. Đã hoàn thành và có kiểm thử
| Hạng mục | Trạng thái | Bằng chứng |
|---|---|---|
| Server local (ASP.NET 8 + SQLite) | Tài khoản, nhân vật (4 ô, tên tiếng Việt có lọc), vị trí/bản đồ, cổng dịch chuyển (đúng map, trong tầm, hồi 1.5 s), tutorial, ví có sổ cái, Shop KC (thanh toán tắt), VIP, backup/restore | **72/72** `dotnet test Server/AdventureKing.Server.Tests` |
| Luồng đầy đủ | Mở game → đăng ký → tạo nhân vật → vào làng → đi → Lưu & Thoát → tắt/bật server → đăng nhập → đúng vị trí | PlayMode `ServerEndToEndTests` |
| Thị trấn — di chuyển | Chạy, nhảy, leo 2 dây + 4 thang (lên/xuống xuyên sàn gỗ một chiều), cầu treo, rơi khỏi map được cứu, đi hết map 2 chiều không kẹt, layer/collider đúng | PlayMode `TownHubTests` + video |
| Cổng dịch chuyển | Vùng trigger, tên bản đồ đích, hộp xác nhận (khi rời thị trấn), chặn kích hoạt kép, hiệu ứng xoáy + âm thanh, màn tải thật (async + thanh tiến trình), server xác thực, xuất hiện đúng điểm ở Đồng Cỏ Gió và quay về | `TownHubTests`, ảnh `QA/HubTest/05*` |
| NPC Trưởng Làng | Người mới: ông đi tới (animation đi thật), quay mặt, vẫy tay, chào đúng tên nhân vật trong hộp thoại; đóng hộp thoại → server lưu tutorial bước 1 → cúi chào, đi về; không chào lại lần sau | `TownHubTests` (đoạn đầu), ảnh `00_elder_greeting` |
| 4 nhân vật | Vũ khí bám tay ≤ 5.2 cm; tốc độ phát Run khớp tốc độ đi (trượt chân 0%) | `QA/AnimAudit/` |
| Logo | Logo studio SAM của chủ dự án ở Splash | — |
| Mobile chuẩn bị | Android + iOS Build Support đã cài; **APK development build thành công (69.9 MB, 0 lỗi)**; nút cảm ứng, vùng an toàn tai thỏ, tùy chọn đồ họa Thấp/Vừa/Cao; server chế độ LAN | `QA/Logs/34_android_build.log`, `Docs/MOBILE_LAN_TESTING.md` |
| Lỗi treo PlayMode | Nguyên nhân: test/scene Single unload scene của Test Runner. Đã sửa bằng `TestRunnerGuard` | Bộ test chạy hết |

**Kết quả lần chạy cuối của phiên (2026-11-08 10:3x):** Server **72/72** · EditMode **4/4** · PlayMode **26 passed / 0 failed** (+2 test của gói Input System bị chính Unity bỏ qua). Video Play Mode: `QA/Video/town_playmode_3d_2026-11-08.mp4` (96.9 s, 30 fps, quay trực tiếp trong test, có UI).

## 3. Tình trạng từng khu vực
- **Thị trấn Chuông Gió (AK_Village):** đã chuyển sang **3D thống nhất**: 16 đạo cụ Meshy 3D (quán trà, nhà, lò rèn, sạp, tháp chuông, cối xay, cây đa, tre, đèn, bảng hiệu, tượng, cầu đá, cổng rune, thùng, chậu hoa), mặt đất bằng khối đất 3D Meshy lát nối, sàn gỗ/thang/cầu treo bằng hình học bo tròn cùng shader Toon + viền, nền tranh vẽ chỉ ở xa (parallax), post-processing chung (bloom nhẹ, chỉnh màu, vignette). Bố cục: mặt đất + 2 tầng sàn + đài cao nhất, 2 dây leo, 4 thang, cầu treo, suối + cầu gỗ, khu Thợ Rèn/Chợ (tây), Quảng trường + tượng (giữa), Quán Trà (đông), cổng sang Đồng Cỏ Gió (đông). Ảnh Play Mode: `QA/WorldCaptures/`, `QA/HubTest/`.
- **Đồng Cỏ Gió (AK_Meadow):** cùng hệ đồ họa, 3 bệ, dây/thang, cổng về. **Chưa có quái.**
- **Sảnh chọn tướng (AK_Entry):** giữ nguyên 4 model 3D, xoay, xem đòn đánh/kỹ năng. UI đã đổi: khung vàng chạm đá quý (art Meshy) cho bảng lớn, nút men bóng viền vàng, ô nhập chìm, thanh chỉ số khung vàng, font **Be Vietnam Pro**. **Chưa duyệt kỹ bằng mắt toàn bộ màn hình sau khi đổi** (xem ảnh `QA/LobbyCaptures/` của lần chạy cuối).
- **NPC:** Trưởng Làng (chào người mới), Thợ Rèn, Thủ Kho — model 3D rig + 5 animation, bảng tên. Thợ Rèn/Thủ Kho **chỉ đứng idle, chưa có hội thoại/cửa hàng**.

## 4. Meshy — credit và tác vụ
- Số dư đầu ngày 2026-11-08: **1729** → cuối phiên: **931** ⇒ dùng **≈ 798 credit** (tất cả từ credit sẵn có, không mua thêm).
- Đã tạo trong ngày: 16 concept + 16 model đạo cụ, 1 tranh nền, 1 khối đất (concept + model), 3 NPC (concept, model, rig, animation), 2 khung UI.
- Lỗi đã gặp: "API key credit limit reached" — chủ dự án đã nâng giới hạn API key.
- **Không còn tác vụ Meshy nào đang chạy.** File gốc sao lưu tại `C:\AdventureKing_ArtBackup\2026-11-08_session\` (79 thư mục tác vụ). Nhật ký: `ArtSource/Meshy/manifest.jsonl`.

## 5. Lỗi / hạn chế chưa xử lý
**Animation & nhân vật**
- Rig Meshy **không có xương ngón tay, xương mặt, blendshape** → không có biểu cảm khuôn mặt thật (nét mặt cố định), ngón tay không cử động. Cần: rig mặt trong Blender (shape keys mắt/miệng) hoặc đổi texture biểu cảm; thêm xương ngón.
- Leo thang: tốc độ hoạt ảnh khớp khoảng cách bậc, tay chưa bám chính xác từng bậc.
- NPC đi bằng tốc độ cố định (1.3 m/s), chưa đo stride như nhân vật chính.

**Đồ họa**
- Khối đất lát nối: có thể thấy mối nối/lặp lại ở một số vị trí; texture hơi kéo giãn khi scale.
- Mái ngói ở mép sàn còn thô so với đạo cụ Meshy; dây leo đơn giản.
- Đèn lồng chưa phát sáng thật (URP đang tắt đèn phụ để tiết kiệm mobile).
- ~~Bảng hiệu: chữ đặt theo ước lượng~~ → đã đo mặt bảng thật (mục A2).
- Chưa có LOD/tối ưu cho Android; chưa đo FPS trên điện thoại thật.

**UI**
- Mới đổi khung/nút/font; bố cục sảnh chưa được thiết kế lại hoàn toàn; chưa có hiệu ứng mở/đóng cửa sổ.
- Nhãn HUD/tên vẫn dùng panel tối đơn giản ở vài chỗ.
- ~~Hộp thoại NPC: chữ sát mép trái khung~~ → đã sửa (mục A2).
- ~~Bố cục điện thoại: hướng dẫn phím hiện trên điện thoại; nút ▼ sát mép; mũi tên nhỏ~~ → đã sửa, thay bằng joystick tròn (mục A2).

**Nền tảng**
- APK build được nhưng **chưa cài/kiểm thử trên điện thoại thật**; tường lửa Windows cho cổng 5080 do chủ dự án tự cho phép.
- iOS: mới có cấu hình + iOS Build Support; **cần máy Mac + Xcode** để build/ký/chạy.

**Âm thanh:** chưa có nhạc nền có bản quyền (chờ chủ dự án chọn nguồn).

**Unity log:** có cảnh báo licensing (`LicensingClient failed validation`) và "missing authored music" — không phải lỗi biên dịch.

## 6. Việc tiếp theo (thứ tự ưu tiên)
1. **Hoàn thiện map**: duyệt ảnh/video Play Mode, sửa mối nối đất, mái ngói, dây leo; thêm ánh sáng đèn lồng (light cookie/emissive), chi tiết khu Chợ/Quán Trà; LOD + đo hiệu năng Android.
2. **Quái ở Đồng Cỏ Gió**: 2–3 loại quái nguyên bản (Meshy concept → model → rig → idle/walk/attack/hit/death), AI tuần tra, va chạm đòn đánh với animation event, **sát thương & loot do server quyết định**.
3. **Boss**: Mini Boss đầu tiên với kỹ năng riêng, bảng loot server, điểm Boss Hunter.
4. **Gameplay chiến đấu**: kỹ năng theo nghề, EXP/level-up lưu server, nhặt Gold/vật phẩm.
5. Hội thoại + cửa hàng cho Thợ Rèn/Thủ Kho; túi đồ.
6. Thử APK trên điện thoại thật qua LAN; chuẩn bị build iOS trên Mac.
7. Nhạc nền (khi chủ dự án chọn nguồn).

## 7. Cách chạy
```
dotnet run --project Server/AdventureKing.Server              # PC
dotnet run --project Server/AdventureKing.Server --environment Lan   # thử điện thoại cùng Wi-Fi
```
Unity 6000.6.7f1: mở `Assets/Scenes/AK_Entry.unity` → Play. Dựng lại map: menu `AdventureKing/Scenes/Rebuild World Maps`; NPC: `AdventureKing/Art/Rebuild NPC Visuals`; build: `AdventureKing/Build/*`.

## 8. Sao lưu
- `C:\AdventureKing_ArtBackup\2026-11-08_session\` — ArtSource (Meshy raw, request, spec, logo), Tools.
- Git bundle toàn bộ lịch sử (kể cả commit chưa push): `C:\AdventureKing_ArtBackup\git\`.
- Database server thật: **chưa có dữ liệu người chơi thật** (chỉ database thử nghiệm tạm, tự xóa sau test).

## 9. Trạng thái lúc kết thúc phiên
- Toàn bộ thay đổi đã kiểm thử được commit trên nhánh `feat/diamond-store-catalog-20261107` (commit "Unified 3D town, real portal travel, Elder greeting, mobile prep, fantasy UI").
- Push: thử bằng git của GitHub Desktop; nếu báo cần đăng nhập thì **chủ dự án bấm Push trong GitHub Desktop** (Claude không đọc/nhập thông tin đăng nhập).
- Git bundle sao lưu: `C:\AdventureKing_ArtBackup\git\AdventureKing_Reborn_2026-11-08.bundle`.
- Không còn tiến trình Unity, server thử nghiệm hay tác vụ Meshy nào đang chạy.
- APK nội bộ ở `Builds/Android/` (không đưa lên Git, không gửi cho chủ dự án).
