# Hướng dẫn: Firebase đăng nhập, nạp VietQR, Hot Update, build iOS (.ipa)

Kiến trúc đã chốt: **Firebase CHỈ dùng đăng nhập (Auth)**. Nhân vật, Kim Cương, skin, đơn nạp vẫn nằm ở server ASP.NET + SQLite hiện tại. Không dùng Firestore / Cloud Functions.

## 1. Đăng nhập Google / Facebook / Chơi nhanh (Firebase Auth)
Luồng: app lấy **Firebase ID token** → `POST /api/v1/auth/firebase {"idToken": "..."}` → server tự kiểm chữ ký RS256 (khoá công khai của Google), `iss`, `aud`, hạn dùng → cấp phiên game. Mỗi `uid` Firebase = 1 tài khoản game. Guest nâng cấp lên Google/Facebook bằng `LinkWithCredential` giữ nguyên `uid` nên **giữ toàn bộ dữ liệu**.

Việc bạn cần làm:
1. Tạo project Firebase → bật Authentication: Anonymous, Google, Facebook (Facebook cần App ID/Secret từ developers.facebook.com).
2. Thêm app iOS (bundle id `com.adventureking.reborn`) → tải **`GoogleService-Info.plist`** → bỏ vào `Assets/` (đúng tên file).
3. Tải Firebase Unity SDK, import `FirebaseAuth.unitypackage` (+ `FirebaseApp`). Cài thêm plugin Google Sign-In và Facebook SDK for Unity.
4. Project Settings → Player → Scripting Define Symbols (iOS): thêm **`FIREBASE_AUTH`**. (Code cầu nối đã viết sẵn trong `Assets/Scripts/Gameplay/FirebaseSignInBridge.cs`; plugin Google/Facebook gán token vào `ProviderTokens.Google` / `.Facebook` trước khi gọi.)
5. Server: trong `Server/AdventureKing.Server/appsettings.json` điền `"Firebase": { "ProjectId": "<id-project-firebase>" }`. Để trống = đăng nhập Firebase bị từ chối (an toàn).

Chưa có SDK thì nút Google/Facebook/Guest báo lỗi rõ ràng, không treo.

## 2. Nạp Kim Cương bằng VietQR
Luồng: chọn gói (cửa sổ **Nạp** ở HUD) → server tạo đơn + mã `AKR######` (hết hạn 30 phút) → app hiện thông tin + ảnh QR (`img.vietqr.io`) + nút **Copy Số Tài Khoản / Copy Nội Dung CK** → ngân hàng báo tiền vào → server khớp **mã + đúng số tiền** → cộng KC **đúng 1 lần** (chống trùng theo mã giao dịch ngân hàng).

Chống gian lận: client **không có route ghi số dư**. KC chỉ cộng bởi webhook có bí mật. Webhook sai bí mật → 401, sai số tiền / sai mã → không cộng, gọi lại cùng giao dịch → không cộng lần 2.

Bật thật (đây là quyết định của chủ dự án, ghi nhận 2026-11-20, thay cho dòng cấm trong CLAUDE.md):
```json
"VietQr": { "Enabled": true, "BankId": "MB", "AccountNo": "SỐ TÀI KHOẢN", "AccountName": "TÊN CHỦ TK", "WebhookSecret": "chuỗi-bí-mật-≥16-ký-tự", "OrderMinutes": 30 }
```
- `BankId`: mã VietQR của ngân hàng (MB, VCB, TCB...).
- Endpoint nhận biến động số dư: `POST https://<server>/api/v1/store/topup/webhook`, header `Authorization: <WebhookSecret>`, body `{"content":"<nội dung CK>","amount":10000,"transactionId":"<mã giao dịch>"}`. Cấu hình dịch vụ thông báo ngân hàng (SePay, Casso, PayOS...) gọi vào đây.
- Thiếu một trong các trường trên, hoặc secret < 16 ký tự → nạp tự tắt.
- **Server phải có địa chỉ công khai (HTTPS)** để ngân hàng gọi webhook được. Máy nhà không đủ: cần VPS / tunnel.

Lưu ý đã gặp: trong môi trường thử, `img.vietqr.io` trả ảnh **SVG** thay vì PNG nên Unity không hiển thị được; app khi đó vẫn hiện đủ thông tin chuyển khoản. Hãy thử với tài khoản thật; nếu vẫn ra SVG thì cần chuyển sang API tạo QR PNG của VietQR hoặc tự sinh QR trong app.
Gói **Thẻ Tháng / Tân Thủ** có thể chưa mua được (Tân Thủ chờ vật phẩm hiếm được cấu hình).

## 3. Hot Update (cập nhật không cần cài lại .ipa)
Vì Unity Package Manager không tải được gói Addressables trong máy hiện tại, hệ thống dùng **AssetBundle có sẵn của Unity** (không cần gói ngoài).
1. Đặt asset của Map/Sự kiện/Skin/VFX mới vào một thư mục, trong Inspector gán **AssetBundle name** (vd `map_c1_m11`, `skin_dream`, `event_tet`, `vfx_...`).
2. Menu **AdventureKing → Build → Hot Update Bundles (iOS)** → ra thư mục `HotUpdate/iOS/` gồm các bundle + `catalog.json` (tự tăng version).
3. Upload cả thư mục lên Firebase Storage (hoặc VPS/CDN, đặt công khai). Địa chỉ gốc, ví dụ `https://firebasestorage.googleapis.com/v0/b/<bucket>/o/hotupdate%2F...` hoặc `https://cdn.../hotupdate`, ghi **một dòng** vào `Assets/Resources/HotUpdateUrl.txt` (trước khi build app).
4. Mở app ở màn Đăng nhập: nếu `catalog.json` có version mới hơn thì hiện bảng **Cập nhật** + thanh tiến trình; tải xong lưu trên máy (`persistentDataPath/hotupdate`). Đọc bundle bằng `HotUpdateManager.Load("map_c1_m11")`.
Giới hạn: iOS cấm tải **code C#** mới; hot update chỉ gồm dữ liệu/asset. Map mới cần scene/dữ liệu dựng sẵn trong bundle và code đã có trong app. Hiện hệ thống tải + lưu bundle; việc **gắn nội dung bundle vào từng tính năng** (vd mở map từ bundle) làm khi có nội dung thật.

## 4. App Icon + Launch Screen + iOS
Chạy menu **AdventureKing → Build → Apply iOS player settings** (đã chạy sẵn). Đã đặt: landscape trái/phải, iOS tối thiểu 15.0, bundle `com.adventureking.reborn`, App Icon 1024 (`Assets/Art/Branding/AppIcon_1024.png`, nền đặc, không bo góc), launch screen (`LaunchScreen.png`), tắt splash Unity.
Logo dùng bản logo có sẵn của game (không phải pixel art thuần).

## 5. Địa chỉ server + build .ipa
- Địa chỉ server: một dòng trong `Assets/Resources/ServerEndpoint.txt` (ví dụ `https://api.tenmien.vn/api/v1`). Đổi sang VPS chỉ sửa dòng này. File này **không commit** (theo quy ước dự án), nhớ điền trước khi build.
- iOS chỉ cho HTTPS (App Transport Security): server production cần chứng chỉ HTTPS. Địa chỉ `http://IP:5080` chỉ chạy ở bản debug/Editor.
- Build `.ipa`: cần máy Mac + Xcode **hoặc** Unity Cloud Build có tài khoản Apple Developer (chữ ký). Ở Unity: File → Build Settings → iOS (cần cài module iOS Build Support) → Build → mở Xcode → Archive → xuất `.ipa`; cài lên iPhone bằng Sideloadly. Máy Windows hiện tại **không tạo được .ipa trực tiếp**.
