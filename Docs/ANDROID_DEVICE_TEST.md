# Kiểm thử Android trên máy thật — Adventure King Reborn

Mục này **cần chủ dự án** vì máy tính không có điện thoại Android nào được kết nối (`adb devices` trống, kiểm tra ngày 2026-11-10).
Bản APK dev được build tự động trên máy này (xem `Docs/SESSION_HANDOFF.md`, mục APK). Không ký bản phát hành, không đưa lên cửa hàng.

## Chuẩn bị (một lần)
1. Điện thoại: Cài đặt → Thông tin điện thoại → chạm 7 lần vào "Số hiệu bản tạo" để bật Tùy chọn nhà phát triển. Sau đó bật **Gỡ lỗi USB**.
2. Cắm cáp USB vào máy tính, trên điện thoại bấm **Cho phép** gỡ lỗi USB.
3. Điện thoại và máy tính dùng **cùng mạng Wi-Fi**: bản dev gọi máy chủ thử nghiệm trên máy tính qua mạng LAN.
4. Trên máy tính, chạy máy chủ thử nghiệm:
   ```
   dotnet run --project Server/AdventureKing.Server --environment Lan
   ```
   Windows Firewall có thể hỏi; chỉ cho phép trong **mạng riêng (Private)**.

## Cài bản dev
```
"C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" install -r Builds/Android/AdventureKingReborn-dev.apk
```
Hoặc chép file APK sang điện thoại rồi mở để cài (cần cho phép "cài ứng dụng không rõ nguồn" cho trình quản lý tệp).

## Danh sách kiểm tra (đánh dấu khi xong)
- [ ] Mở game: logo, nền đăng nhập, nhạc. Không văng app.
- [ ] Đăng ký / đăng nhập tài khoản thử, tạo nhân vật (đủ 4 nghề), vào Làng Chuông Gió.
- [ ] Màn hình ngang. Tai thỏ / góc bo không che nút, HP, minimap.
- [ ] Joystick trái và nút bên phải: đi, nhảy, leo thang, đánh, 3 kỹ năng, Auto, nhặt, thuốc HP/MP, túi đồ, Pet. Thử **nhấn nhiều ngón cùng lúc** (đi + đánh).
- [ ] Nói chuyện Trưởng Làng, mở shop Mộc Linh, mua thuốc. Nhập số lượng bằng bàn phím ảo.
- [ ] Nhiệm vụ: nhận thưởng, Chỉ đường.
- [ ] Cổng sang Đồng Cỏ Gió: đánh quái, nhặt Vàng, chết rồi hồi sinh.
- [ ] Pet: mua, triệu hồi, cho ăn, pet nhặt Vàng. Bật **Pet tự bơm thuốc HP** ở 90 %, để quái đánh: pet đưa thuốc, số thuốc giảm 1.
- [ ] Sự kiện / Phúc lợi / Thư / Giftcode (gõ mã bằng bàn phím ảo) / BXH / VIP: mở, cuộn, đóng.
- [ ] Cài đặt: đồ họa, âm thanh, nhạc nền.
- [ ] Ra màn hình chính rồi quay lại game (app chạy nền); tắt Wi-Fi vài giây rồi bật lại: thông báo lỗi rõ ràng, không treo.
- [ ] Hiệu năng: chơi 10 phút ở Đồng Cỏ Gió. Nếu bản dev có hiện FPS thì ghi lại; nóng máy hay giật lag thì ghi chú.
- [ ] Ghi lại: **tên máy, phiên bản Android, RAM**, và mọi lỗi kèm ảnh chụp màn hình.

## Gửi lại kết quả
Gửi danh sách trên (đã đánh dấu), ảnh chụp lỗi và tên máy. Nếu game văng, lấy log bằng lệnh:
```
adb logcat -d -s Unity > android_log.txt
```
rồi gửi file `android_log.txt`.
