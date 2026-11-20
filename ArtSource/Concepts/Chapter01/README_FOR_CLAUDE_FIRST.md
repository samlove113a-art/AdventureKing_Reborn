# ADVENTUREKING_REBORN — CHƯƠNG 1: GÓI ẢNH THAM CHIẾU 3D (CONCEPT)

## Đọc trước

Đây là **ảnh concept dựng theo phong cách 3D**, KHÔNG PHẢI model 3D GLB/FBX hay scene Unity hoàn chỉnh.
Bộ gói gồm 01 bảng thiết kế tổng quan, 10 ảnh crop tham chiếu map, 9 ảnh crop minh họa quái/Boss, 02 ảnh concept nhân vật nấm kích thước lớn và brief JSON/Markdown để Claude Pro tiếp tục dựng.
Các ảnh crop từ bảng tổng quan có chất lượng tham khảo bố cục, KHÔNG đủ để dùng làm texture gốc hoặc nguồn Image-to-3D chuẩn nếu chưa tái thiết kế ảnh riêng.

## Luật đã chốt với chủ dự án
- Game có 20 chương, mỗi chương 10 map (tổng 200 map).
- Chương 1 gồm 10 map. Chỉ có **Boss mạnh nhất chương tại map 10**.
- Phải **hạ Boss map 10** thì server mới cho mở chương 2.
- Map 1–9 có quái thường và Mini Boss tùy map; tham khảo Mini Boss map 3, 6, 8 trong concept.
- Mỗi map có bản sắc môi trường, bộ quái, kỹ năng và silhouette khác nhau. Không tạo 9 map bằng cách clone đổi màu.
- Model phải chơi được trên PC/Android, art style 3D chibi fantasy nguyên gốc.

## Cấu trúc tập tin
- `00_OVERVIEW/Chapter01_Master_3D_Concept.png` — BẢNG CONCEPT CHÍNH, tham khảo bố cục và tổng thể.
- `01_MAP_SCENE_PREVIEWS/` — 10 crop khung cảnh (phóng to để tiện xem, không được hiểu nhầm là ảnh gốc full-res riêng lẻ).
- `02_CREATURE_REFERENCES/` — 5 quái mẫu, 3 Mini Boss, 1 Boss cuối chương, crop để tham khảo, cần concept riêng trước khi gọi Meshy.
- `03_MESHY_SOURCE_REFERENCES/` — 2 ảnh Nấm Chiến Binh full-scene, **concept**, cần tách phông trước khi đưa sang Meshy.
- `04_BRIEF_AND_PIPELINE/Chapter01_MapManifest_CONCEPT.json` — map ID, mô tả, quái, màu, tiến trình.
- `04_BRIEF_AND_PIPELINE/Meshy_14_Creature_Briefs.md` — 14 mô tả tạo concept đơn nhân vật độc đáo.

## CLAUDE PRO — CÁCH LÀM TIẾP
1. Kiểm tra project Unity hiện tại **AdventureKing_Reborn**, branch, remote và các thay đổi chưa commit; **KHÔNG tạo project mới**.
2. Đọc Master Board và bản manifest, so với mã nguồn **hiện có** (nhất là AK_Village / AK_Meadow và map_id server). Không giả định các tên đề xuất đang tồn tại trong Unity.
3. Đưa bản gói concept vào `ArtSource/Concepts/Chapter01/` hoặc một thư mục nguồn ngoài `Assets/` tùy cấu trúc dự án; đừng nhập tất cả ảnh tham chiếu như texture production.
4. Làm một map chuẩn đẹp + tối ưu, rồi mở rộng 10 map theo concept; mỗi map có platform/collider thật, quái và tiến trình server. Không tạo 10 scene rỗng.
5. Với từng nhân vật/quái: chuẩn hóa 1–4 ảnh cùng nhân vật rõ phông, chọn model tạo mới bằng Meshy chỉ khi cần; Blender sửa chi tiết, rig/tạo animation, tối ưu LOD, kiểm tra poly/UV/material/collider và tải trên mobile.
6. Với nền/bố cục: **không dùng Meshy để dựng cả ảnh cảnh có nhiều vật thể trong một lần**; tách ra cây, nhà nấm, cổng, cầu, đá, pha lê, đèn; tạo mô-đun dùng lại và bố trí bản đồ có chủ đích.
7. Xây dựng Chương 1 map 1–10; Boss cuối map10 có tối thiểu tấn công thường, 2 kỹ năng boss, triệu hồi, phase 2 và animation chết. Server xác thực kill và unlock chương 2. Chỉ thêm phần thưởng sau khi chủ duyệt cân bằng.
8. Kiểm thử Unity EditMode/PlayMode và server; ảnh chụp Game View, lag/fps trên thiết bị phù hợp; commit + push **nhánh phát triển đúng repo** sau nghiệm thu.
9. Tôn trọng ngân sách: không tự mua credit; API key bị limit thì không cố gọi lại; luôn tái sử dụng model đạt yêu cầu và ghi log số credit tiêu.
10. Bản pack này là hướng mỹ thuật để giúp dựng nhanh hơn, **không đủ để tự động tạo 3D không cần thao tác**. Phải có Meshy/Blender/Unity truy cập thật và các bước QA.

## Lưu ý về tính nhất quán
Chỉ dùng **Master Board 2026-11-10 trong gói này làm chuẩn concept của Chương 1**; tên map/quái/Boss trong file JSON là ĐỀ XUẤT có thể điều chỉnh. Không thay thế NPC hay model/scene nào đã được duyệt trong GitHub.
