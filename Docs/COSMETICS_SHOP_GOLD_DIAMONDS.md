# Adventure King Reborn — Shop thời trang, Gold và Diamonds
Approved by project owner 2026-11-07. This is a design/integration specification, not an implemented or tested shop.

## Chính sách tiền tệ — BẮT BUỘC
- **Trang phục/phụ kiện bán từng món**: mua bằng **Gold (Vàng)**, loại tiền kiếm được khi chơi. Danh mục phải phong phú, có thể phối nhiều món với nhau. Không ép mua Diamonds để mặc đẹp.
- **Skin đặc biệt nguyên bộ (Premium / Legendary / Event)**: mua bằng **Diamonds (Kim Cương)**; có thiết kế riêng nổi bật, có thể kèm hiệu ứng kỹ năng/cổng xuất hiện/Idle/âm thanh riêng ở mức thẩm mỹ. Không cộng sát thương, HP, phòng thủ, tốc độ, tỷ lệ rơi đồ hay buff PVP chỉ vì mua bằng tiền.
- Tuyệt đối không tự ý chuyển sản phẩm Gold sang Diamonds khi chưa được chủ dự án duyệt. Không tự phát sinh thanh toán tiền thật hoặc tích hợp nhà cung cấp thanh toán khi chưa được phép.
- **Giá cụ thể chưa được chủ dự án duyệt**. Dùng data-driven catalog để thay đổi giá trên server; không tuyên bố mức giá giả định là cuối cùng.

## Hệ thống skin từng món (Gold)
- Danh mục phối đồ 3D: tóc, mũ, khuôn mặt/phụ kiện mặt, áo, quần hoặc váy, găng tay, giày, áo choàng/cánh thời trang, hiệu ứng ngoại hình nhỏ, vũ khí hiển thị/thời trang. Các item phải có slot rõ ràng và tương thích nghề (nếu hạn chế, phải hiển thị).
- 4 nghề Kiếm Sĩ / Pháp Sư / Sát Thủ / Cung Thủ đều phải có nhiều lựa chọn đáng mua bằng Gold. Khi chọn món, màn hình hiển thị model 3D đang mặc thử với **đúng rig, trang bị hiện tại và tất cả món khác đã phối**.
- Không có lỗi xuyên tóc, xuyên áo, rách mesh, vũ khí lệch tay hay cánh xuyên người trong chuyển động Idle/Run/Jump/Climb/Attack/Skill.
- Hỗ trợ thử đồ, xoay model bằng chuột/cảm ứng, xem giá, mua, mặc, tháo, phối, xem bộ đang mặc và đồ đã sở hữu.
- Mục tiêu là **catalog nhiều lựa chọn và mở rộng được lâu dài**, không dùng danh sách cứng chắp vá. Không coi text/icon placeholder là skin đã hoàn thành.

## Skin đặc biệt nguyên bộ (Diamonds)
- Bộ sưu tập fantasy/anime-inspired **thiết kế gốc** cho mỗi nghề, có chủ đề và ngôn ngữ thiết kế riêng Adventure King (không sao chép Naruto/One Piece/MapleStory/nhân vật phim).
- Skin đặc biệt có thể gồm đầu/tóc, body/costume, phụ kiện, vũ khí riêng, màu VFX skill, Idle và âm thanh riêng (nếu tạo đủ), nhưng tất cả **chỉ mỹ thuật**.
- Skin premium có thể chọn mặc cả bộ, xem trước đủ hoạt ảnh, tháo để trở về trang phục phối bằng Gold, giữ quyền sở hữu vĩnh viễn; có lựa chọn ẩn các phần không tương thích để tránh clipping.
- Skin đặc biệt phải đáng giá về chất lượng thiết kế/animation/VFX, không chỉ đổi màu vật liệu rồi bán bằng Diamonds.
- Nếu nội dung premium nào chưa có model, animation, VFX hoặc quyền sử dụng thương mại được kiểm chứng thì không niêm yết như vật phẩm hoàn thiện.

## Online save / giao dịch
- Server lưu catalog phiên bản, item definition, inventory instance, quyền sở hữu cosmetic, slot đang mặc và quyền sở hữu premium skin theo account/character như quy tắc sản phẩm; **không bao giờ tin Gold/Diamonds gửi từ client**.
- Purchase endpoint kiểm tra phiên đăng nhập, quyền sở hữu character, sản phẩm còn bán, loại tiền, giá server, số dư; thực hiện **một transaction nguyên tử**: trừ tiền, ghi ledger, cấp quyền sở hữu. Có idempotency key để bấm nhiều lần/lag mạng không trừ hai lần.
- Equip endpoint kiểm tra item đã sở hữu, điều kiện nghề, slot và xung đột; lưu appearance loadout và cập nhật revision. Không mất đồ khi logout, khởi động lại, đổi PC/điện thoại hoặc chuyển scene.
- Sau mua thành công: làm mới wallet và inventory bằng snapshot đã xác nhận trên server; không báo thành công chỉ từ hiệu ứng client.
- Giữ định hướng nguồn server có thể chạy **local PC để thử nghiệm**, không cần thuê VPS ngay.

## UI/UX cần đạt
- Shop riêng rõ 2 tab **Thời trang (Gold)** và **Skin đặc biệt (Diamonds)**.
- Danh mục có lọc theo nghề/slot/phong cách, xem trước chibi 3D có xoay, phối món, nút Mặc thử / Mua / Trang bị / Tháo / Đã sở hữu, hiển thị đơn vị tiền và giá rõ ràng.
- Khi thử đồ không trừ tiền; có xác nhận khi mua bằng Diamonds, thông báo số dư không đủ và bảo vệ thao tác double-click.
- Mọi màn hình tối ưu Windows và điện thoại; hiệu ứng preview/try-on không làm FPS giảm không kiểm soát.

## Điều kiện nghiệm thu
1. Mỗi nghề có nhiều món thời trang Gold **thật**, có mesh/material/rig đúng và mặc phối cùng nhau được.
2. Mỗi nghề có skin đặc biệt Diamonds **thật**, phong cách gốc, VFX/animation phù hợp và đáng mua; không tăng sức mạnh.
3. Thử và xoay hero xem trước hoạt động bằng chuột và cảm ứng; trước/sau khi mua hiển thị đúng model.
4. Mua Gold chỉ trừ Gold; mua premium chỉ trừ Diamonds; đúng số dư server và không mua lặp gây mất tiền.
5. Sau logout/relogin và dùng máy khác vẫn sở hữu và đang mặc chính xác từng item/skin, kể cả vật phẩm mua từ trước.
6. Bảo đảm license thương mại cho **tất cả** model/texture/SFX; không sao chép thương hiệu game/anime.
7. Test Unity, backend persistence, cửa hàng, ngoại hình hero đang chuyển động và PC/Android. Chưa qua thử nghiệm thì phải báo là WIP.

## Tình trạng hiện tại (cập nhật 2026-11-08 tối)
Đã triển khai và kiểm thử: server tủ đồ/giao dịch (V3), 10 món Gold + 1 skin KC thật trên cả 4 nghề, NPC Chủ Tiệm Lụa, shop + phòng thử đồ 3D, khôi phục sau relog/khởi động lại server. Chi tiết, kết quả test và phần còn thiếu (tóc, vũ khí, giá chờ duyệt): `Docs/SESSION_HANDOFF.md` mục ★★★.

### Ghi chú lịch sử
Trước đây đây chỉ là **yêu cầu và hướng tích hợp**. Code nền AdventurePlayerSaveContracts có inventory, ownedSkinIds và equipped appearance, nhưng chưa đầy đủ hệ thống slot mỹ phẩm + transaction server. Không có cam kết rằng vật phẩm/shop/art đã được dựng trong Unity. Cần Claude Code xây, tích hợp, kiểm thử thật và ghi bằng chứng.
