# Thử Adventure King trên điện thoại (Android / iPhone / iPad) với server trên PC

Server hiện chạy trên PC của chủ dự án. Địa chỉ `127.0.0.1` chỉ dùng được **trên chính PC đó**, điện thoại không truy cập được. Để thử trên điện thoại cần chạy server ở **chế độ LAN** trong mạng Wi-Fi nhà, **không** thuê VPS.

## 1. Chạy server ở chế độ LAN (trên PC)

```
dotnet run --project Server/AdventureKing.Server --environment Lan
```

- Lắng nghe `http://0.0.0.0:5080` (mọi card mạng của PC). Cấu hình: `Server/AdventureKing.Server/appsettings.Lan.json`.
- Windows sẽ hỏi cho phép ứng dụng qua tường lửa — **chủ dự án tự bấm "Cho phép" chỉ với mạng Riêng tư (Private)**. Claude không tự thay đổi tường lửa.
- Địa chỉ LAN của PC lúc kiểm tra: `192.168.1.46` (có thể đổi khi router cấp lại IP).
- Chế độ LAN dùng HTTP thường: chỉ dùng trong Wi-Fi nhà tin cậy, **không** mở cổng ra Internet. Bản phát hành bắt buộc HTTPS (client từ chối HTTP ở bản release).

## 2. Android

- Unity 6000.6.7f1 đã cài **Android Build Support** (SDK, NDK, OpenJDK) — ngày 2026-11-08.
- Build: menu `AdventureKing/Build/Android APK (development, LAN server)` hoặc
  `Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod AdventureKing.ArtPipeline.PlatformBuilds.BuildAndroidFromCommandLine`
- Bản build tự ghi địa chỉ LAN của PC vào `Resources/ServerEndpoint.txt` (chỉ trong bản development), APK ở `Builds/Android/AdventureKingReborn-dev.apk` (không đưa lên Git).
- Cài lên máy: bật *Tùy chọn nhà phát triển → Gỡ lỗi USB*, rồi `adb install -r Builds/Android/AdventureKingReborn-dev.apk` (adb nằm trong Android SDK của Unity).
- Màn hình ngang, nút cảm ứng tự hiện trên điện thoại, UI nằm trong vùng an toàn (tai thỏ).

## 3. iPhone / iPad

- Đã cài **iOS Build Support** trên Windows: Unity có thể xuất **dự án Xcode** (`AdventureKing/Build/iOS Xcode project`), nhưng **biên dịch, ký và cài lên iPhone bắt buộc cần máy Mac + Xcode** và Apple ID (tài khoản Apple Developer nếu phát hành).
- Các bước trên Mac: mở `Builds/iOS/Unity-iPhone.xcodeproj` → chọn Team (Apple ID) → cắm iPhone → Run. Cho phép "Local Network" khi app hỏi để kết nối server LAN.
- Chưa kiểm thử trên thiết bị iOS thật (không có Mac trong môi trường hiện tại).

## 4. Dữ liệu đồng bộ

Cùng một tài khoản đăng nhập từ PC, Android hay iOS đều dùng chung database của server → nhân vật, bản đồ, vị trí giống nhau (đã kiểm thử tự động trên PC: đăng xuất, tắt/bật server, đăng nhập lại giữ nguyên).
