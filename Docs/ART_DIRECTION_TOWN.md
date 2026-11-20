# Thị trấn Chuông Gió — Art Direction thống nhất (2026-11-08)

Mục tiêu: nhân vật, mặt đất, nhà cửa, NPC, cổng và UI trông như **cùng một game**. Nguyên bản Adventure King:
fantasy lấy cảm hứng Việt Nam (mái ngói cong, chuông gió, đèn lồng lụa, đa, tre, sen, cổng rune pha lê).
Chỉ học cảm giác "thị trấn nhiều tầng" của MMORPG cổ điển; không sao chép map, nhà, cây, NPC, cổng, UI hay nhạc của game nào.

## 1. Một hệ vật liệu cho mọi thứ trong vùng chơi
- **Mọi vật thể trong vùng chơi là 3D thật** dùng shader `AdventureKing/Toon` (2 dải sáng/tối, rim light, viền).
  Nhân vật, NPC, nhà, cây, cầu, thang, bệ, mặt đất, cổng — không còn tranh 2D đặt cạnh nhân vật.
- **Viền (outline)**: màu chung `(0.13, 0.09, 0.12)`; độ dày theo đơn vị thế giới: nhân vật 0.0065, đạo cụ 0.010, địa hình 0.012.
- **Shade color** chung `(0.62, 0.58, 0.72)` pha theo màu gốc; rim `(1, 0.94, 0.82)` 0.28.
- **Tranh vẽ** chỉ dùng làm **nền xa** (z ≥ 60, sau sương) — không bao giờ cạnh lối đi.

## 2. Ánh sáng & hậu kỳ
- Một nguồn sáng chính ấm (nắng sáng, 44°/-32°), ambient 3 tầng trời/chân trời/đất, sương tuyến tính xanh nhạt.
- Post-processing chung: bloom nhẹ cho đèn/pha lê, chỉnh màu (+độ bão hòa nhẹ), vignette rất nhẹ. Cùng một profile cho mọi map.

## 3. Tỷ lệ (chibi)
| Đối tượng | Chiều cao |
|---|---|
| Nhân vật, NPC | 1.6 m (đầu ≈ 40%) |
| Cửa ra vào | 2.2 m |
| Nhà 1 tầng / 2 tầng | 6 m / 8–9 m |
| Cây lớn | 9–11 m |
| Đèn lồng, đèn đá | 1.8–3.5 m |
| Bậc thang giữa các tầng | 5 m |

## 4. Địa hình
- Mặt đất là **khối điêu khắc**: mép cỏ rủ xuống, lớp đất, các khối đá lồi — hình học thật (không phải hộp nâu phẳng), màu theo đỉnh (vertex color) để đổ bóng toon tự nhiên.
- Lối đi lát đá/đất nén chạy dọc đường chơi; vùng xa có gò cỏ, hoa.
- Bệ gỗ: ván rời có lệch nhẹ, cột tròn, mái ngói cong ở mép — cùng vật liệu toon.

## 5. Camera & bố cục
- Nhân vật chiếm ~20–22% chiều cao màn hình (PC 16:9 và điện thoại 19.5:9), luôn nằm 55–62% từ trên xuống.
- Tiền cảnh không che nhân vật; UI trong vùng an toàn, không che vùng giữa màn hình.

## 6. UI
- Khung kim loại vàng chạm hoa văn + đá quý xanh ngọc (art Meshy), nút men bóng có viền vàng, font **Be Vietnam Pro** (tiêu đề/nội dung) và **Nunito** (nhãn tròn) — SIL OFL 1.1.
- Hộp thoại đặt ở nửa dưới/bên cạnh, không che nhân vật; mở/đóng có hiệu ứng.
