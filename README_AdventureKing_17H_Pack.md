# ADVENTURE KING REBORN — Gói tài nguyên & bàn giao 17h

Gói này chứa mã nguồn C# mẫu, prompt tổng hợp, ba ảnh tham khảo mỹ thuật, cấu hình phần thưởng mẫu và 8 file âm thanh WAV gốc dạng **BẢN NHÁP ĐỂ NGHE THỬ**.

## Cách dùng

1. Giải nén gói này vào **thư mục gốc của project Unity hiện có** `AdventureKing_Reborn`, sau khi sao lưu và kiểm tra Git Status.
2. Không ghi đè bất kỳ file hiện có nào mà không kiểm tra. `EquipmentManager.cs` trong gói là **script mới, chưa được kết nối trực tiếp với Shop**; Unity sẽ biên dịch nó khi nằm trong `Assets/Scripts/Gameplay/`.
3. Trong Claude Code mở đúng thư mục project và yêu cầu nó đọc `Docs/CLAUDE_TONG_HOP_17H.md`; kèm ảnh trong `ArtSource/OwnerProvided/UIReferences/` để nhận biết hướng mỹ thuật.
4. **QUAN TRỌNG:** Game hiện dùng `HeroOutfit.cs` + `CosmeticLibrary`; không gắn đồng thời `HeroOutfit` và `EquipmentManager` để vẽ áo ở cùng một slot. Claude phải so sánh và tích hợp cách thay áo vào đúng hệ thống rồi mới bật runtime.
5. `Docs/ReferenceConfigs/AdventureKing_Welfare_Config_Example.json` để **DRAFT** và `enabled=false`. Các mức quà là gợi ý, không copy sang server live trước khi duyệt.
6. Ảnh minh họa là reference; nếu cần sprite trong suốt cho nút/icon thì phải tách/redraw cẩn thận, kiểm tra chất lượng và quyền sử dụng. Không dùng một poster lớn làm UI tĩnh.
7. Gói có `Assets/Audio/AKOriginalDrafts/`: 6 vòng lặp BGM WAV 24 giây + 2 SFX tự tổng hợp bằng âm thanh số nguyên bản, **chưa phải nhạc đã master hoặc được owner duyệt**. Có thể dùng nghe thử kỹ thuật trong Unity; đừng đưa vào bản thương mại khi chưa kiểm định chất lượng. Không có file MP3 và không dùng nhạc lấy từ game khác.

## Setup Inspector cho prefab-based EquipmentManager (nếu chọn dùng riêng)

- `Rig Root`: transform chứa xương nhân vật.
- `Reference Body Renderer`: SkinnedMeshRenderer của thân nhân vật THẬT, không phải vũ khí hay áo khoác.
- `Slots[0].Slot = Top`; `Mount`: một `GameObject` con tên `TopSlot` dành riêng áo, không chứa body, animator hay root skeleton.
- `Legacy Visuals`: kéo từng GameObject áo cũ dạng mesh riêng đang nằm ngoài `TopSlot` (nếu có). KHÔNG kéo cả nhân vật.
- Gọi `EquipTop(prefabAoMoi)` để thay áo, `UnequipTop()` để tháo và khôi phục áo mặc định.
- Prefab áo cần `SkinnedMeshRenderer` với mesh, bones và bindposes xuất từ đúng rig; việc khớp tên bone **không bảo đảm** 100% không clipping nếu chất liệu/model/body chưa cùng kích thước.

### Cảnh báo quan trọng về áo dính body

Nếu áo cũ chỉ là texture trên cùng body mesh, không thể tắt riêng áo mà không làm mất da; cần thiết kế lại mesh base/segmentation, body mask hoặc shader mask do artist/Claude kiểm tra trong Unity.

## Những gì chưa được xác minh

- File C# đã kiểm tra cú pháp/tham chiếu thủ công nhưng **chưa biên dịch/test trong Unity thực tế** tại máy bạn.
- Audio BGM chưa được sản xuất.
- Nội dung Sự kiện, Điểm danh chỉ là yêu cầu để Claude tích hợp server; **chưa phải tính năng đã tồn tại trong project**.
