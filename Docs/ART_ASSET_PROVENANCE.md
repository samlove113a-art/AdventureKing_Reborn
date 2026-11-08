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

## 2. Animation
Thư viện animation có sẵn của Meshy (Idle, Run, Jump, Ladder Climb, Sword Slash, Spell Cast, Archery, Hit, Death…) áp lên rig của chính các model trên, cùng giấy phép gói trả phí. Cắt clip, gắn Animation Event va chạm và bake root trong Unity — xem `Assets/Editor/ArtPipeline/HeroArtDefinitions.cs`.

## 3. Hiệu ứng kỹ năng (VFX)
Tự tạo hoàn toàn bằng code trong `Assets/Editor/ArtPipeline/HeroVfxFactory.cs`: texture thủ tục (chấm mềm, vòng, vệt chém, sao lấp lánh, vòng phép rune, khói), mesh vệt chém và mũi tên dựng thủ tục, shader `AdventureKing/VFX`. Không dùng asset bên ngoài.

## 4. Âm thanh
- 17 hiệu ứng âm thanh chiến đấu **tổng hợp từ đầu** bằng DSP (`Tools/audio/synth_sfx.py`): không sample, không thư viện âm thanh bên thứ ba → thuộc sở hữu dự án.
- **Nhạc nền: CHƯA CÓ.** Không có nhạc gốc hoặc nhạc có giấy phép thương mại; không gắn nhạc tạm để tránh coi placeholder là hoàn thành.

## 5. Shader & UI
`AdventureKing/Toon`, `AdventureKing/VFX`, sprite bo góc và gradient của sảnh: tự viết/tự tạo trong repo.

## 6. Logo
- **Logo studio "SAM"** do chủ dự án cung cấp và phê duyệt (2026-11-08). File gốc: `ArtSource/Branding/SAM_logo_original.webp` (1254×1254, nền trong suốt); bản dùng trong Unity: `Assets/Art/Branding/SAM_Logo.png` (chuyển định dạng bằng `Tools/blender/convert_image.py`, không chỉnh sửa nội dung). Hiển thị ở màn Splash trên nền ngà sáng vì chữ "SAM" màu đen.
- **Logo tựa game "Adventure King"**: chưa có. Không tự tạo; chờ chủ dự án.

## 7. Bên thứ ba khác
Unity packages (URP, Input System, Test Framework, uGUI) theo giấy phép Unity Companion License của editor 6000.6.7f1. Font UI tạm dùng `LegacyRuntime.ttf` có sẵn của Unity — cần chọn font thương mại có giấy phép hỗ trợ tiếng Việt trước khi phát hành.
