# Adventure King Reborn — Báo cáo tiến độ thực tế

**Cập nhật:** 2026-11-08 · **Nhánh:** `feat/diamond-store-catalog-20261107`

Báo cáo này chỉ ghi những gì **đã chạy và kiểm thử được** trên PC, kèm phần còn thiếu. Đây **chưa phải** bản phát hành và chưa đạt chuẩn thương mại toàn bộ.

## 1. Đã hoàn thành và có bằng chứng kiểm thử

| Hạng mục (thứ tự ưu tiên) | Trạng thái | Bằng chứng |
|---|---|---|
| (1) Source an toàn, Unity biên dịch | Biên dịch sạch trên Unity 6000.6.7f1 · Meshy raw sao lưu tại `C:\AdventureKing_ArtBackup\` | `QA/Logs/16–18_*.log`, `QA/Results/*.xml` |
| (2) 4 nhân vật chibi 3D | Kiếm Sĩ, Pháp Sư, Sát Thủ, Cung Thủ: model Meshy đã rig, vũ khí gắn xương tay, đủ Idle/Run/Jump/Fall/Climb/Attack/Skill/Hit/Death, có Animation Event đúng khung chạm | `QA/ArtCaptures/`, `QA/LobbyCaptures/` |
| (4) Sảnh chọn tướng 3D | Model 3D thật chuyển động, xoay bằng chuột/cảm ứng, xem đòn đánh và kỹ năng (nhân vật xoay sang góc nghiêng như trong game), VFX và SFX riêng từng nghề, chỉ số, 4 ô nhân vật, ô đặt tên | Test `EntryLobbyTests` + 12 ảnh chụp |
| (4) Đăng nhập | Đăng ký/đăng nhập tài khoản thật qua server. **Google/Facebook bị từ chối** (chưa cấu hình OAuth), không giả lập thành công | Test `Login_Fails_Closed…`, `HttpApiTests` |
| (5) Server local + lưu thật | ASP.NET Core 8 + SQLite tại `Server/`. Tài khoản, nhân vật, tên, tiến độ tutorial, ví Gold/KC có sổ cái chống nhân đôi, sao lưu và khôi phục | **70/70 test server**; test end-to-end Unity ↔ server (tạo nhân vật → tắt server → bật lại → đăng nhập → nhân vật còn nguyên) |
| (5) Đặt tên nhân vật | Kiểm tra trên server: tiếng Việt có dấu, 3–16 ký tự, không trùng (kể cả tên na ná như `B0ss`/`Boss`), chặn GM/Admin/nhân viên, từ tục tĩu và thù ghét, chặn ký tự Unicode giả dạng (Cyrillic, Hy Lạp, fullwidth, ký tự ẩn) | `NameRulesTests` (50 trường hợp) |
| (8) Shop Kim Cương — catalog | Đúng bảng giá đã duyệt; tag "+40%", "Lần đầu x2" được tính từ số liệu; Thẻ Tháng 15 + 3/ngày × 30 = 105 theo ngày giờ Việt Nam; ưu đãi lần đầu lưu trên server; một biên lai không thể cộng hai lần; VIP chỉ tính tiền nạp đã xác minh | `StoreAndWalletTests` |

## 2. Đã khóa có chủ đích (chờ chủ dự án quyết định)

- **Thanh toán tiền thật: TẮT.** `/store/purchase` luôn trả `payments_disabled`; không có giao dịch nào được thực hiện. Khi được duyệt cần chọn cổng (Google Play Billing / App Store / cổng PC) và xác minh biên lai phía server.
- **Gói Tân Thủ không bán được** cho tới khi chủ dự án chọn "1 vật phẩm hiếm" cụ thể.
- **Ngưỡng VIP 1–10 chưa đặt** (`VipThresholdsVnd` trong `appsettings.json`), nên VIP hiện luôn là 0.
- **Google/Facebook:** cần chủ dự án tạo OAuth Client ID (Google Cloud Console, Meta for Developers) rồi mới tích hợp xác minh ID token trên server.

## 3. Còn thiếu — chưa được tuyên bố hoàn thành

| Thiếu | Lý do / việc cần làm |
|---|---|
| Logo chính thức | Repo chưa có file logo; màn Splash để trống, không tự thay logo khác |
| Nhạc nền (BGM) | Chưa có nhạc gốc hoặc nhạc có giấy phép thương mại; mới có SFX tự tổng hợp |
| Làng 2.5D, NPC Trưởng Làng, tutorial trong game | Chưa xây; server đã sẵn sàng lưu tiến độ tutorial |
| Scene gameplay | "Vào game" dừng ở thông báo "Scene gameplay chưa được cấu hình" (đúng thiết kế fail-closed) |
| UI thương mại | Sảnh hoạt động nhưng khung/nút vẫn là UI tạm (bo góc phẳng, font Unity mặc định); cần bộ khung fantasy thống nhất và font tiếng Việt có giấy phép |
| Ngón tay | Rig Meshy không có xương ngón; tay cầm vũ khí ở dạng mở |
| Quái, boss, loot, túi đồ, kho, pet, shop NPC, thời trang/skin, đấu giá, bang hội, kết hôn, kênh, bạn bè, thư, nhiệm vụ, Settings đầy đủ | Chưa làm (theo thứ tự ưu tiên 3, 6, 7, 9, 10) |
| Android | Chưa cài Android Build Support cho Unity 6000.6.7f1 |
| Kiểm tra âm thanh bằng tai người | SFX mới được kiểm tra bằng thời điểm sự kiện, chưa nghe duyệt |

## 4. Cách chạy

```
dotnet run --project Server/AdventureKing.Server
```
Mở `Assets/Scenes/AK_Entry.unity` trong Unity và bấm Play. Chi tiết server: `Server/README.md`.
