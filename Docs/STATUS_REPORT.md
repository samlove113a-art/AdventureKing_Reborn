# Adventure King Reborn — Báo cáo tiến độ & bàn giao

**Cập nhật:** 2026-11-08 · **Nhánh:** `feat/diamond-store-catalog-20261107`

Chỉ ghi những gì **đã chạy và kiểm thử được** trên PC. Game **chưa đạt chuẩn thương mại**; đây không phải bản phát hành.

## 1. Đã hoàn thành (có bằng chứng kiểm thử)

| Hạng mục | Kết quả | Bằng chứng |
|---|---|---|
| Biên dịch Unity 6000.6.7f1 | Sạch, không lỗi | `QA/Logs/2x_*.log` |
| 4 nhân vật chibi 3D | Kiếm Sĩ, Pháp Sư, Sát Thủ (song đao), Cung Thủ; đủ Idle/Run/Jump/Fall/Climb/Attack/Skill/Hit/Death; Animation Event đúng khung chạm | `QA/AnimAudit/*_clips.png` |
| Vũ khí bám tay | Mọi khung hình kiểm tra: vũ khí cách khớp tay ≤ 5,2 cm | `QA/AnimAudit/audit.md` |
| Chân không trượt | Tốc độ phát Run/Climb tự khớp tốc độ di chuyển thật (đo từ chính clip của từng nghề); trượt chân còn lại 0% ở tốc độ chạy 4,6 m/s | `audit.md`, `HeroMotionProfile` |
| Logo | Logo studio **SAM** do chủ dự án cung cấp, hiện ở màn Splash (nền ngà, hiệu ứng hiện dần) | `Assets/Art/Branding/SAM_Logo.png` |
| Sảnh chọn tướng 3D | Model thật, xoay chuột/cảm ứng, xem đòn đánh & kỹ năng có VFX/SFX riêng, 4 ô, đặt tên | `EntryLobbyTests`, `QA/LobbyCaptures/` |
| Đăng ký / đăng nhập thật | Qua server local; Google/Facebook **bị từ chối** (chưa cấu hình OAuth), không giả lập | `HttpApiTests`, `Login_Fails_Closed…` |
| Bản đồ đầu tiên | `AK_Village` (Làng Chuông Gió): mặt đất, 2 tầng, 3 thang, cầu treo, nhà, cây, đồi, cổng; nhân vật 3D đúng nghề đã chọn; Build Settings: AK_Entry (0), AK_Village (1) | `QA/WorldCaptures/` |
| Lưu thật trên server | Tài khoản, nhân vật, tên, nghề, cấp, bản đồ + vị trí (kiểm tra bản đồ & biên), tutorial, ví Gold/KC có sổ cái chống cộng trùng, sao lưu/khôi phục | 71/71 test server |
| Luồng đầy đủ | Mở game → đăng ký → chọn nghề & tên → vào map → đi → **Lưu & Thoát** → tắt/bật lại server → đăng nhập → vào map **đúng vị trí cũ** | `ServerEndToEndTests` (xem mục 5) |
| Đặt tên | Tiếng Việt, 3–16 ký tự, không trùng (kể cả `B0ss`/`Boss`), chặn GM/Admin, tục tĩu, ký tự Unicode giả dạng | `NameRulesTests` (50 trường hợp) |
| Shop Kim Cương (catalog) | Đúng bảng giá; Thẻ Tháng 15 + 3×30 = 105 KC theo giờ VN; một biên lai không cộng hai lần | `StoreAndWalletTests` |

## 2. Khóa có chủ đích — chờ chủ dự án quyết định

- **Thanh toán tiền thật: TẮT** (`payments_disabled`). Chưa có giao dịch nào.
- **Gói Tân Thủ**: chưa bán được cho tới khi chọn "1 vật phẩm hiếm".
- **Ngưỡng VIP 1–10**: chưa đặt (`VipThresholdsVnd`).
- **Google/Facebook**: cần OAuth Client ID của chủ dự án.
- **Nhạc nền có quyền thương mại**: chưa có. Cần chọn 1 trong 3: (a) Claude tự soạn nhạc gốc bằng code (thuộc sở hữu dự án, cần chủ dự án nghe duyệt), (b) chủ dự án mua/đặt nhạc, (c) Claude đề xuất danh sách nhạc CC0/royalty-free để duyệt trước khi tải.
- **Logo tựa game "Adventure King"**: hiện mới có logo studio SAM.

## 3. Còn thiếu / giới hạn đã biết (chưa sửa)

| Vấn đề | Chi tiết |
|---|---|
| Biểu cảm khuôn mặt | Rig Meshy **không có xương mặt, không có blendshape** → nét mặt cố định, không chớp mắt/cười. Cần rig mặt (Blender shape keys) hoặc đổi texture mắt/miệng |
| Ngón tay | Không có xương ngón; tay là nắm đấm/găng điêu khắc sẵn (cầm vũ khí trông ổn, không cử động ngón) |
| Leo thang | Tốc độ hoạt ảnh khớp theo khoảng cách bậc thang, nhưng tay chưa bám chính xác từng bậc |
| Môi trường làng | Dựng bằng khối cơ bản + toon shader: chơi được, dễ nhìn, **chưa phải mỹ thuật thương mại**; cần model nhà/cây/đá thật (Meshy/Blender) |
| UI | Sảnh và HUD dùng khung bo góc tạm + font mặc định Unity; cần bộ UI fantasy thống nhất và font tiếng Việt có giấy phép |
| Nhân vật trên map | Chiếm ~10–12% chiều cao màn hình; có thể cần zoom gần hơn sau khi có môi trường đẹp |
| Chưa có | NPC Trưởng Làng & tutorial trong game, quái/boss/loot, túi đồ, kho, shop NPC, pet, thời trang/skin, đấu giá, bang hội, kết hôn, kênh, bạn bè, thư, nhiệm vụ, Settings đầy đủ |
| Android | Chưa cài Android Build Support |
| Âm thanh | SFX tự tổng hợp mới kiểm tra theo thời điểm, chưa nghe duyệt bằng tai |
| Dữ liệu SQLite thật | Chưa có database người chơi thật (server mới chạy với database thử nghiệm tạm) |

## 4. Bước tiếp theo (theo thứ tự)

1. Chủ dự án quyết định nguồn **nhạc nền** (mục 2) → tích hợp BGM cho Splash/Đăng nhập/Sảnh/Làng.
2. **NPC Trưởng Làng**: model Meshy + rig + animation đi/vẫy tay; đi đến, chào bằng tên, mở tutorial bước 1–10 (server đã lưu tiến độ).
3. **Nâng cấp môi trường làng**: thay khối cơ bản bằng model nhà/cây/đá/cầu thật, giữ nguyên bố cục và va chạm.
4. **Bộ UI fantasy thống nhất** + font tiếng Việt có giấy phép cho sảnh, đăng nhập, HUD.
5. Quái thường đầu tiên + rơi Gold do server quyết định.
6. Cài Android Build Support và kiểm thử trên điện thoại.

## 5. Cách chạy & kiểm thử

```
dotnet run --project Server/AdventureKing.Server
```
Mở `Assets/Scenes/AK_Entry.unity` trong Unity → Play. Server: `Server/README.md`.

Kiểm thử: `dotnet test Server/AdventureKing.Server.Tests`; Unity Test Runner (EditMode + PlayMode). Test end-to-end cần `dotnet build Server/AdventureKing.Server` trước.

## 6. Sao lưu

- Meshy raw, ArtSource, Tools: `C:\AdventureKing_ArtBackup\` (2026-11-07, 2026-11-08). Nên chép thêm ra ổ ngoài/cloud.
- Toàn bộ lịch sử git (kể cả commit chưa push): `C:\AdventureKing_ArtBackup\git\` (git bundle).
