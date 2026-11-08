# Adventure King Reborn — Nguồn gốc & quyền sử dụng tài sản (cập nhật 2026-11-07)

Mọi tài sản dưới đây được tạo **mới** cho Adventure King. Không dùng, không trích xuất và không mô phỏng tài sản của MapleStory hay trò chơi khác. Prompt thiết kế chỉ mô tả nhân vật/vũ khí gốc, không nhắc tên tác phẩm khác.

## 1. Model 3D (Meshy, gói trả phí của chủ dự án)
- Giấy phép: gói trả phí Meshy = **private license, dùng thương mại đầy đủ**, không bắt buộc ghi công (docs.meshy.ai/en/webapp/pricing, kiểm tra ngày 2026-11-07). Cần kiểm tra lại điều khoản trước khi phát hành.
- Nhật ký từng tác vụ (prompt, task id, credit, file): `ArtSource/Meshy/manifest.jsonl`; request gốc: `ArtSource/Meshy/requests/`.
- File tải về gốc (GLB/FBX) **không** lưu trong git (dung lượng lớn; Meshy xoá sau 3 ngày) → sao lưu tại `C:\AdventureKing_ArtBackup\` trên PC. Cần sao lưu thêm ra ổ ngoài/cloud của chủ dự án.

| Tài sản | Quy trình | Ghi chú |
|---|---|---|
| Kiếm Sĩ (tóc vàng mật ong, khăn đỏ, áo gile xanh) | text-to-image → image-to-3D → rig → 14 animation | thiết kế gốc |
| Pháp Sư (tóc oải hương tết đôi, mũ phù thuỷ chàm) | như trên, 13 animation | thiết kế gốc |
| Sát Thủ (tóc đen vệt bạc, khăn đỏ thẫm, áo mận) | như trên, 13 animation | thiết kế gốc |
| Cung Thủ (tóc nâu đỏ, mũ xanh lông đỏ, ống tên) | như trên, 13 animation | thiết kế gốc |
| Kiếm, pháp trượng, dao găm, cung (khởi đầu) | text-to-image → image-to-3D | thiết kế gốc |

Tổng: 30 tác vụ, 479 credit (60 ảnh concept, 240 model, 20 rig, 159 animation). Số dư sau phiên: 1729 credit. Không mua thêm credit.

### Thị trấn Chuông Gió & Đồng Cỏ Gió (2026-11-08)
Thiết kế gốc của Adventure King — fantasy lấy cảm hứng Việt Nam (mái ngói cong, chuông gió, đèn lồng, đa, tre, sen, cổng rune pha lê). Prompt không nhắc tác phẩm khác (`Tools/meshy/make_town_requests.py`).

| Tài sản | Quy trình | Dùng trong game |
|---|---|---|
| 16 tranh đạo cụ: quán trà, nhà nhỏ, lò rèn, sạp chợ, tháp chuông, cối xay gió, cây đa, tre, đèn đá, cột đèn lồng, bảng hiệu, tượng anh hùng, cầu đá, cổng dịch chuyển, thùng hàng, chậu hoa | text-to-image (nền trong suốt) | Lớp cảnh vẽ có chiều sâu (shader `AdventureKing/Cutout`) |
| Tranh nền xa (núi, ruộng bậc thang, đảo bay, thác) | text-to-image 16:9 | Nền parallax |
| Bụi tre 3D | image-to-3D → Blender (`Tools/blender/export_prop.py`) | Model 3D trong cảnh |

Credit phiên này: 17 ảnh + 1 model ≈ 132 credit. Số dư tài khoản sau đó: **1597**. 15 model 3D còn lại bị Meshy từ chối với lỗi **"API key credit limit reached"** (giới hạn chi tiêu riêng của API key, không phải hết credit tài khoản) — chủ dự án đã nâng giới hạn (2026-11-08), sau đó mới tạo tiếp.

### Đồng nhất 3D (2026-11-08, sau khi nâng giới hạn API key)
| Tài sản | Quy trình | Ghi chú |
|---|---|---|
| 16 đạo cụ thị trấn 3D (quán trà, nhà, lò rèn, sạp, tháp chuông, cối xay, đa, tre, đèn đá, cột đèn, bảng hiệu, tượng, cầu đá, cổng rune, thùng hàng, chậu hoa) | image-to-3D từ các concept trên → Blender `export_prop.py` (chuẩn tỷ lệ, gốc ở đáy, trục bake sẵn) | shader Toon + viền chung |
| Khối đất 3D (cỏ rủ, đất nhiều lớp, đá) | text-to-image → image-to-3D → lát nối dọc map | thay mặt đất hộp phẳng |
| NPC Trưởng Làng, Thợ Rèn, Thủ Kho | concept cùng khối style với 4 nhân vật → image-to-3D → rig → 5 animation (Idle, Walk, Wave, Talk, Bow) | thiết kế gốc |
| Khung UI vàng chạm + thanh chỉ số | text-to-image (nền trong suốt), cắt bằng `crop_alpha.py`, 9-slice | khung bảng/hộp thoại |

Số dư sau toàn bộ phiên 2026-11-08: **931 credit** (không mua thêm).

## Khung tên trên đầu nhân vật (chủ dự án cung cấp, 2026-11-08)
- Nguồn: gói `AdventureKing_KhungTen_VIP1-15_Boss1-10.zip` do chủ dự án gửi; theo README trong gói là ảnh **AI-generated (ChatGPT) theo yêu cầu của chủ dự án**, không lấy từ game khác. Lưu nguyên vẹn tại `ArtSource/OwnerProvided/Nameplates_2026-11-08/` (+ bản zip ở `C:\AdventureKing_ArtBackup\`).
- 34 khung: 1 cơ bản, VIP 1–15, Boss Hunter 1–10, 8 khung đặc biệt.
- **Chưa đưa vào game.** Kiểm tra kỹ thuật (2026-11-08) cho thấy đây là bộ khởi tạo, chưa đạt chuẩn thương mại — đúng như README của gói ghi:
  - Độ phân giải thấp: VIP/Boss/Special chỉ 181–399 px chiều ngang (VIP_01 182×85) → mờ trên màn hình 1440p và điện thoại mật độ cao.
  - Lỗi cắt viền: `Basic_Nameplate` còn ký tự thừa ".t" trong ô tên; `VIP_13` và `BossHunter_10` có vệt màu ở mép trái; `BossHunter_03` có vạch sáng ở mép trên; `BossHunter_05` bị cắt cánh bên phải.
  - Chữ nhãn ("VIP 1", "BOSS HUNTER 3"…) là chữ AI vẽ sẵn trong ảnh, độ nét không đều.
- Việc cần làm trước khi dùng: vẽ lại/upscale từng khung ở ≥ 512 px chiều ngang, sửa mép, tách nhãn cấp thành chữ động hoặc kiểm tra từng nhãn; giữ tên nhân vật là chữ động.

## Font
`Be Vietnam Pro` (Regular/SemiBold/Bold/ExtraBold) và `Nunito` — tải từ kho chính thức google/fonts, giấy phép **SIL Open Font License 1.1** (cho phép nhúng vào game thương mại); file `OFL.txt` đặt cạnh font trong `Assets/Art/Fonts/`. Tải theo phê duyệt của chủ dự án ngày 2026-11-08.

## 2. Animation
Thư viện animation có sẵn của Meshy (Idle, Run, Jump, Ladder Climb, Sword Slash, Spell Cast, Archery, Hit, Death…) áp lên rig của chính các model trên, cùng giấy phép gói trả phí. Cắt clip, gắn Animation Event va chạm và bake root trong Unity — xem `Assets/Editor/ArtPipeline/HeroArtDefinitions.cs`.

## 3. Hiệu ứng kỹ năng (VFX)
Tự tạo hoàn toàn bằng code trong `Assets/Editor/ArtPipeline/HeroVfxFactory.cs`: texture thủ tục (chấm mềm, vòng, vệt chém, sao lấp lánh, vòng phép rune, khói), mesh vệt chém và mũi tên dựng thủ tục, shader `AdventureKing/VFX`. Không dùng asset bên ngoài.

## 4. Âm thanh
- 17 hiệu ứng âm thanh chiến đấu **tổng hợp từ đầu** bằng DSP (`Tools/audio/synth_sfx.py`): không sample, không thư viện âm thanh bên thứ ba → thuộc sở hữu dự án.
- 3 âm thanh cổng dịch chuyển (tiếng ngân lặp liền mạch `Portal_Hum`, `Portal_Warp`, `Portal_Arrive`) tổng hợp cùng cách (`synth_sfx.py <out> world`).
- **Nhạc nền: CHƯA CÓ.** Không có nhạc gốc hoặc nhạc có giấy phép thương mại; không gắn nhạc tạm để tránh coi placeholder là hoàn thành.

## 5. Shader & UI
`AdventureKing/Toon`, `AdventureKing/VFX`, `AdventureKing/Cutout`, `AdventureKing/BlobShadow`, sprite bo góc và gradient của sảnh, texture thủ tục (cỏ, đất, ván gỗ, ngói cong, nước, xoáy cổng): tự viết/tự tạo trong repo.

## 6. Logo
- **Logo studio "SAM"** do chủ dự án cung cấp và phê duyệt (2026-11-08). File gốc: `ArtSource/Branding/SAM_logo_original.webp` (1254×1254, nền trong suốt); bản dùng trong Unity: `Assets/Art/Branding/SAM_Logo.png` (chuyển định dạng bằng `Tools/blender/convert_image.py`, không chỉnh sửa nội dung). Hiển thị ở màn Splash trên nền ngà sáng vì chữ "SAM" màu đen.
- **Logo tựa game "Adventure King"**: chưa có. Không tự tạo; chờ chủ dự án.

## 7. Bên thứ ba khác
Unity packages (URP, Input System, Test Framework, uGUI) theo giấy phép Unity Companion License của editor 6000.6.7f1. Font UI tạm dùng `LegacyRuntime.ttf` có sẵn của Unity — cần chọn font thương mại có giấy phép hỗ trợ tiếng Việt trước khi phát hành.
