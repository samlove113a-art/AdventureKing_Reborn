# ADVENTURE KING REBORN — LỆNH TỔNG HỢP TỪ CÁC GÓP Ý KHOẢNG 17H, 09/11/2026

Bạn là Senior Unity 6 / C# / ASP.NET Core 8 engineer và technical artist. Tiếp tục trên PROJECT HIỆN CÓ, không tạo dự án mới. Nhánh làm việc: `feat/diamond-store-catalog-20261107`. GitHub mốc xác minh: `11dcce859c67a245da4b9db092ef5f42b2f93ab6`, nhưng **kiểm tra local status và mọi commit chưa push trước tiên**; không reset hoặc ghi đè phần đã làm. Đọc `Docs/SESSION_HANDOFF.md` và đúng những file liên quan, không quét cả project mỗi lượt. Đây là yêu cầu tích hợp vào game THẬT, không phải một ảnh tĩnh hoặc cửa sổ placeholder.

## 1. SỬ DỤNG HÌNH MẪU — PHONG CÁCH ĐÃ DUYỆT

Đọc ba ảnh được kèm trong gói ở `ArtSource/OwnerProvided/UIReferences/`:
- `AdventureKing_HUD_Approved.png`: giao diện sảnh + HUD chibi fantasy đã duyệt; phải bám sát bố cục, màu sắc, tỷ lệ và phong cách.
- `AdventureKing_Events_Icons_Approved.png`: bộ biểu tượng Sự kiện, Điểm danh, Nạp đầu, Phúc lợi, Thư, Giftcode, VIP, BXH, Thêm; cần tách/redraw thành **sprite trong suốt RIÊNG**, độ phân giải rõ ở kích thước sử dụng, không đem cả background poster vào game.
- `AdventureKing_Checkin_Events_VisualReference.png`: thiết kế minh họa các popup Điểm danh, Sự kiện/Phúc lợi, Nạp đầu, Giftcode, VIP, BXH, Kết hôn, icon và quà.

Ảnh AI là tài liệu **tham chiếu mỹ thuật**, không phải bản hoàn thiện, không phải chứng nhận bản quyền/quality. Tách hoặc vẽ lại các sprite bằng thiết kế nguyên bản và xác minh đọc được chữ; không dùng các biểu tượng UI từ game khác.

## 2. HUD VÀ NÚT THU GỌN/MỞ RỘNG

- **Trên trái:** avatar người chơi lấy từ album (có trình chọn ảnh, crop, giới hạn tệp, lưu theo account và kiểm duyệt khi công khai); tên nhân vật, Lv, HP đỏ, MP xanh; số Vàng/Kim Cương thật trong khung riêng đồng bộ với HUD, không dùng ô đen bầu dục.
- **Trên phải:** minimap và dãy icon Sự kiện / Điểm danh / Nạp đầu / Phúc lợi / Thư / Giftcode / VIP / BXH / Thêm. **Có mũi tên ngay cạnh cụm icon để THU GỌN/MỞ RỘNG trên CẢ PC VÀ MOBILE** (hiện code cũ chỉ đóng/mở một số icon trên mobile). Animate mượt, không che minimap, giữ trạng thái người chơi nếu phù hợp. Chấm đỏ chỉ hiển thị khi server xác nhận có nội dung có thể nhận.
- **Dưới:** chat có tab, nút túi đồ/nhân vật/pet/shop/bang/kết hôn/kỹ năng, joystick mobile và các nút skill + Auto Skill; không tự động auto-farm.
- Sửa lỗi tràn chữ, bảng đen, nút lặp, overlay chồng nhau, tiêu đề đè lên hoa văn ở login và các panel. Duy trì các họa tiết/khuôn khác nhau phù hợp từng nhóm, không copy một khung vàng cho cả game. Test PC 1920×1080, 1366×768 và mobile landscape 16:9, 19.5:9.

## 3. NHÂN VẬT HÒA HỢP VỚI MAP + NAMEPLATE TRÊN ĐẦU

- Chỉnh toon shader, viền, ánh sáng, exposure, saturation, contact shadow, độ lớn nhân vật so với NPC, quái, mái nhà, bệ và cổng để nhân vật chibi 3D hòa vào bản đồ 2.5D; không có cảm giác sticker bị dán lên nền; giữ nguyên 4 class và animation face/hand/Run/Jump/Climb/Attack/Skill.
- Trên đầu player: **khung VIP hoặc Boss Hunter (nếu đã đạt đủ điều kiện và trang bị), bên dưới là TÊN NHÂN VẬT THẬT lấy từ server**. Không đặt avatar ảnh album trên đầu. Không giả VIP 5 như concept nếu người chơi chưa đạt quyền sở hữu.
- Thư viện khung tên nguồn đang ở `ArtSource/OwnerProvided/Nameplates_2026-11-08/`; ảnh hiện còn nhược điểm về mép, độ phân giải và ký tự nên phải QA hoặc vẽ lại asset gốc, không đưa nguyên bản kém chất lượng lên game. Các mốc VIP 1–14 chưa duyệt, VIP 15 = 50 triệu VND verified; Boss Hunter theo điểm boss server, cấp qua Thư một lần, không tăng sức mạnh.

## 4. CÁC NÚT PHẢI CÓ NỘI DUNG THẬT KHI BẤM

Hiện `HudFeatureButton.cs` chủ yếu mở một `HudPopup` thông báo, chưa phải hệ thống nhận quà. Không được ghi "hoàn thành" chỉ vì popup đã mở.

### Điểm danh 7 ngày
- Popup fantasy viền gỗ/đồng vàng; **6 ô đầu GridLayoutGroup 3×2; ngày 7 là ô MILESTONE lớn độc lập** với các loại quà được duyệt (chú ý ngày 7 có quyền chọn skin phù hợp).
- Mỗi ô: ngày, icon, số lượng, tooltip, trạng thái Khóa / Có thể nhận / Đã nhận; nút Nhận Quà có loading, xác nhận server và chống bấm trùng.
- Quy tắc đề xuất: tối đa 1 lần/account/ngày VN UTC+7, bỏ lỡ không reset, xong ngày 7 mới sang vòng sau trong ngày tiếp theo; thời gian tính trên server, không tin client. Server transaction nguyên tử qua `Wallet.Gold`, `Wallet.Diamonds`, cấp item đúng ownership và ledger, chống replay/request đồng thời.
- JSON mẫu có trong gói `Docs/ReferenceConfigs/AdventureKing_Welfare_Config_Example.json`: hiện `status=DRAFT_OWNER_APPROVAL_REQUIRED`, `enabled=false`. **KHÔNG kích hoạt cấp thưởng live khi chưa duyệt số Gold/KC, rương và skin.** Cho phép test với config dev riêng.

### Bảng Sự kiện và Phúc lợi
- Popup đẹp, bên trái vertical tabs **Sự Kiện Mới / Đăng Nhập / Nạp Đầu / Phúc Lợi Online**, bên phải banner, thông tin thời gian, danh sách nhiệm vụ, progress bars, quà, nút Nhận hoặc Đã Nhận.
- Sự kiện đầu tiên đề xuất **Săn Nấm Lễ Hội**, ví dụ tiêu diệt 30 Nấm Lùn, tiêu chí kill count từ server; chưa có hệ thống quái thì chưa được phép claim.
- Online welfare đề xuất các mốc 15/30/60 phút, tính presence server, chống farm offline/đồng hồ client. Có thể chỉnh lại trước khi chốt.
- Nạp đầu phải kiểm chứng giao dịch real money hợp lệ qua backend và tuân thủ luật nền tảng; **payments đang OFF, không bật và không tặng giả**.
- Sự kiện định kỳ và quà tân thủ phải có catalog version, thời gian start/end, whitelist item, idempotency, expiration và receipt.

### Thư / Giftcode / VIP / BXH
- **Thư:** thư hệ thống/phần thưởng, đính kèm và claim nhận 1 lần bằng server; phân biệt đã đọc/đã nhận.
- **Giftcode:** nhập mã, trả trạng thái dùng/hết hạn/không hợp lệ/hết lượt, kiểm tra server và chống dùng lại.
- **VIP:** hiển thị cấp thực; mốc chưa duyệt hiển thị 'chưa cấu hình', không tự cấp VIP hoặc thanh toán.
- **BXH:** cấp, sức mạnh và Bang, lấy dữ liệu đã xác thực, không cộng sức mạnh từ skin.
- **Phúc lợi / Điểm danh / Sự kiện:** red dot chỉ khi có claimable data thật; tình trạng server offline không hiện dot giả.
- Tất cả panel có nút Đóng, ô trạng thái lỗi, ScrollRect, safe area, input block khi mở popup và return focus khi đóng. Không báo nút có chức năng nếu chỉ dẫn sang popup placeholder.

## 5. TRANG PHỤC — THAY ÁO KHÔNG CHỒNG MESH

Đã kèm `Assets/Scripts/Gameplay/EquipmentManager.cs` (source C# thật) để tham khảo hoặc dùng cho **prefab quần áo skinned tách riêng**. Script xử lý theo slot, dựng instance mới ở trạng thái tắt, kiểm tra xương, ánh xạ SkinnedMeshRenderer tới skeleton nhân vật, vô hiệu hóa ngay quần áo cũ rồi mới hiển thị mới, dọn instance cũ; cho phép kéo rig/renderer/mount/legacy clothes trong Inspector.

**CỰC KỲ QUAN TRỌNG VỀ CODEBASE:** game ĐÃ có `HeroOutfit.cs`, `OutfitLoadout`, `CosmeticLibrary` và save server. `HeroOutfit.Apply()` đang Clear trang phục cũ rồi dựng mesh mới theo itemId. **KHÔNG attach song song hai renderer quản lý áo của cùng slot**. Ưu tiên tích hợp logic replace/bone mapping/validation/rollback vào `HeroOutfit` đang chạy nếu đó là đường mặc đồ chính, chỉ dùng `EquipmentManager` trực tiếp cho một luồng prefab độc lập. Không để client tự quyết quyền sở hữu, chỉ mặc sau khi server xác minh / ở preview miễn phí.

Ngoài ra: nếu áo gốc được **vẽ liền trên body mesh/texture**, code không thể chỉ 'xóa phần áo' mà vẫn giữ thân/da; cần body segmentation, mesh mask hoặc đúng base body model do pipeline tạo. SkinnedMeshRenderer remap xương không tự sửa được mesh xuyên cơ thể nếu bindposes, UV, trọng số, độ rộng hoặc silhouette sai. Cần QA mesh + animation tất cả 4 nghề và cả shop preview/map thật.

Đồ thời trang phải nhiều món rời, nhiều kiểu vui nhộn (mũ vịt, ếch, nấm, cá mập, thú bông...) nhưng **không chỉ đổi màu**. Tỷ lệ Gold/Diamonds khoảng 70/30 là hướng thiết kế, giá cụ thể phải dựa Gold kiếm/giờ và owner duyệt. Không tự tiêu Meshy credit (số dư trước đây 931, cần kiểm lại).

## 6. NHẠC VÀ SFX — KHÔNG GIẢ FILE

- Tạo và tích hợp nhạc nguyên bản hoặc nguồn đã kiểm chứng license thương mại: **BGM_Login, BGM_Lobby_Day, BGM_Lobby_Night, BGM_Map_Adventure, BGM_Boss, BGM_Wedding**; bổ sung SFX ClaimReward, ButtonClick, LevelUp, Mail, ShopPurchase khi cần. Tên liệt kê trong ảnh mẫu chỉ là LABEL, không phải âm thanh đã tạo.
- Hiện repo có SFX đánh, kỹ năng, bước chân, portal; **chưa có soundtrack BGM hoàn chỉnh đã duyệt**. Gói giao nhận có `Assets/Audio/AKOriginalDrafts/` gồm 6 BGM WAV vòng lặp ~24 giây và 2 SFX gốc tổng hợp thủ tục, chỉ để NGHE THỬ/kỹ thuật, không coi là chất lượng thương mại đã phê duyệt. Test nghe, loop/crossfade, mixer BGM/SFX riêng, volume saved, nguồn gốc và quyền sử dụng trước khi đề xuất đưa vào game.
- Nếu không có công cụ tạo nhạc chất lượng/phê duyệt nguồn, để trạng thái blocked và báo rõ thay vì làm file nhạc giả, không dùng nhạc game có bản quyền.

## 7. ROADMAP NỀN KINH TẾ VÀ GAMEPLAY

- Gold từ quái rơi xuống đất tự nhiên, không giới hạn farm/ngày, thường respawn 60 giây server; Pet tự nhặt (no power advantage), Pet đói sau 60 phút active summoned+online; food Gold/rare, trạng thái server.
- Chợ người chơi dùng Gold, giao dịch nguyên tử, chống dupe, server validates items, anti-bot/macro không quấy rầy dân cày bằng CAPTCHA liên tục; Auto Skill trong game được phép, không có Auto Farm chạy tìm quái.
- Giữ định hướng chat thế giới, bang, bạn bè; Bang hội, Kết hôn cinematic, VIP/Boss Hunter, Level 300, 30 channels và save account/character; chưa hoàn thành không dựng tính năng giả.

## 8. KẾ HOẠCH THỰC THI THÍCH HỢP CLAUDE PRO (3 PHIÊN/NGÀY)

Ưu tiên lần lượt:
1. Kiểm tra local state, bảo toàn code + hoàn thiện sửa HUD/UI chưa push.
2. Integrate/tách sprite độc lập từ reference, arrow mở/đóng đúng PC/mobile, cảnh nhân vật hòa hợp.
3. Hoàn thiện backend+UI **Điểm danh 7 ngày thật**, test claim và idempotency. JSON hiện là draft; xin duyệt reward trước khi live.
4. Event/Welfare/Nạp đầu, Mail/Giftcode/VIP/BXH theo server readiness, không giả.
5. Tích hợp áo mới không chồng trên `HeroOutfit`, test 4 class x Idle/Run/Jump/Climb/Attack/Skill.
6. BGM nguyên bản/licensed khi có file nguồn thật, playlist và mixing.
7. Roadmap kinh tế/social/pet/farm sau khi phần trước vững.

Trong mỗi phiên: đọc tài liệu bàn giao và file liên quan; làm một nhóm việc đủ hoàn thiện, chạy test liên quan, chụp **Unity Play Mode thật**, commit checkpoint và cập nhật `Docs/SESSION_HANDOFF.md` trước hết context. Chạy full tests trước khi bàn giao đợt, không che test fail bằng đổi tên/thứ tự. Không mua tool/asset/credit/thanh toán khi chưa có chủ dự án phê duyệt. Nếu thiếu device Android thật, ghi rõ chưa test. Không tuyên bố game production-ready khi chưa đủ bằng chứng.

## 9. ĐIỀU KIỆN ĐẦU RA

- Không có Missing Script, compile errors, mất data/skin/Gold, lỗi tràn chữ/khung, nút giả, icon mờ, bố cục không giống ảnh duyệt, nameplate VIP giả hay âm thanh không có file.
- Phải có ảnh trước/sau trong Unity PlayMode và danh sách test chính xác.
- Server xác minh mọi phần thưởng/giao dịch và giữ dữ liệu sau relog.
- Bảo toàn art source, source code, prefab, rig, animation, dữ liệu nhân vật và số dư.
- Ghi rõ mục hoàn thành / chưa hoàn thành / cần owner duyệt và commit SHA; chỉ push nhánh đúng khi an toàn.

BẮT ĐẦU NGAY: kiểm tra local, đọc tài liệu bàn giao, so sánh code `HeroOutfit` và `EquipmentManager`, đưa ra kế hoạch tích hợp nhỏ gọn rồi thực hiện phần ưu tiên cao nhất đang còn thiếu. KHÔNG HỎI LẠI NHỮNG ĐIỀU OWNER ĐÃ CHỐT.
