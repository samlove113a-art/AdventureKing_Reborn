# ADVENTURE KING REBORN — BỘ KHUNG TÊN TRÊN ĐẦU NHÂN VẬT

Đây là bộ hình tham khảo do ChatGPT tạo cho chủ dự án, đã đóng gói và tách thành PNG:
- 01 khung cơ bản (Basic_Nameplate.png)
- 15 khung VIP (VIP_01.png … VIP_15.png)
- 10 khung Boss Hunter (BossHunter_01.png … BossHunter_10.png)
- 08 khung đặc biệt (tân thủ, sự kiện, lễ hội, thành tựu, cặp đôi, bang chủ, top server, đặc biệt)
- Ảnh tổng hợp gốc nằm trong References/ và manifest JSON.

## CÁCH SỬ DỤNG VỚI UNITY
1. Import Assets/Nameplates/**/*.png vào Unity, loại Sprite (2D and UI), giữ alpha.
2. Nameplate là **thanh ngang trên đầu nhân vật**, KHÔNG phải ô inventory. Render khung bằng Image/SpriteRenderer riêng và tên nhân vật bằng TextMeshPro riêng, lấy tên từ dữ liệu nhân vật đã xác thực của server.
3. Phần dưới của mỗi khung VIP/Boss Hunter là ô TỐI TRỐNG để viết TÊN NHÂN VẬT ĐỘNG. Dòng VIP/BOSS trên đầu là nhãn cấp, KHÔNG thay cho tên.
4. Giữ đúng aspect ratio. Không dùng 9-slice trên cánh, vương miện hoặc rồng vì sẽ méo. Tách lõi theo 9-slice riêng nếu cần hỗ trợ tên dài.
5. Giới hạn độ rộng tên và tự co font trong mức đọc được; không cho tên tràn ra viền; kiểm tra trên 16:9, 19.5:9, Android/iPhone.
6. Chỉ trang bị 01 khung tại một thời điểm. Chỉ cấp khung theo dữ liệu server hợp lệ.
7. Viền ngoài PNG đã được tách bằng phương pháp tự động từ poster concept; **đây là bộ hình khởi tạo, KHÔNG phải asset thương mại đã nghiệm thu**. Phải kiểm tra và chỉnh phần mép còn răng cưa, dải màu nền kẹt giữa cánh, độ phân giải, chữ nhãn AI, và hiệu ứng trước khi công bố hoàn thành.

## QUY TẮC REWARD ĐÃ CHỐT
- VIP 1–15 dựa trên tổng tiền VND nạp **được xác minh hợp lệ** bởi server; VIP 15 = 50.000.000 VND tích lũy. VIP 1–14 chưa chốt ngưỡng, cần chủ dự án duyệt.
- Boss Hunter 1–10 dựa trên tổng điểm đánh bại Boss hợp lệ; cấp cao cần điểm cao hơn nhiều. Các mốc điểm trước đây là ĐỀ XUẤT chờ cân bằng.
- Khi đạt mốc VIP/Boss Hunter, **server gửi đúng phần thưởng vào Hộp Thư** (mail), chống gửi trùng, người chơi nhận một lần rồi chọn trang bị.
- Giữ quyền sở hữu khung cũ; không cộng lực chiến chỉ vì khung VIP hoặc khung Boss.
- Không tự bật thanh toán thật khi chưa tích hợp nhà cung cấp hợp lệ.

## KỸ THUẬT ART
Nguồn: AI-generated original concept artwork theo yêu cầu người dùng; không phải tài nguyên từ game khác. Các tệp trong References/ là concept atlas và nguyên mẫu. Cần QA mỹ thuật đầu cuối; tên, viền sáng và màu hiển thị sẽ có giới hạn do tách từ ảnh poster.

Không có mã nguồn C# hoặc plugin Unity trong ZIP này; đây là tệp hình + quy ước cho Claude tích hợp.
