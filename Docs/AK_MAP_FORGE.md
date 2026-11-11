# AK Map Forge v1 — Chương 1 Map 2 · Đồng Cỏ Xanh

> **Trạng thái:** Đã commit code trên nhánh `feat/ak-map-forge-m02`. **Chưa chạy Unity Editor, PlayMode hoặc dotnet test trên máy chủ dự án.** Đây là công cụ sản xuất và bản triển khai Map 2, **chưa được duyệt mỹ thuật thương mại**.

## Cách sử dụng (không dùng Claude, không cần Meshy)

1. Trong GitHub Desktop, lưu/commit thay đổi đang làm trước khi đổi nhánh; **KHÔNG Discard**. Fetch origin, chọn nhánh `feat/ak-map-forge-m02` (nhánh riêng, không ghi đè nhánh Chương 1 gốc).
2. Mở **Unity Hub → AdventureKing_Reborn** (Unity 6000.6.7f1), để Unity import và biên dịch. Nếu Console báo lỗi đỏ, **không chạy generator**; giữ nguyên các tệp và kiểm tra lỗi.
3. Menu **AdventureKing → Map Forge → Open AK Map Forge**. Chọn **Decoration Seed** (chỉ đổi vị trí hoa) và **Chi tiết hoa / Mobile** (1–3). Nhấn **TẠO MAP 2 + NỐI CỔNG MAP 1**. Unity sẽ hỏi lưu scene đang mở nếu có thay đổi.
4. Công cụ dựng `Assets/Scenes/Chapter1/AK_C1_M02.unity`, thêm cổng đông vào **scene Map 1 đang có**, đổi biển chỉ đường, không xây lại Map 1. Bản sao Map 1 trước chỉnh lưu tại `AKMapForgeBackups/AK_C1_M01_before_mapforge.unity` ở gốc project.
5. Nhấn **KIỂM TRA CẤU HÌNH MAP 2**. Nếu thông báo OK, vẫn cần kiểm thử gameplay thật với server.
6. Mở **PowerShell** ở `C:\AdventureKing_Reborn`, chạy:
   ```powershell
   dotnet test Server/AdventureKing.Server.Tests --filter FullyQualifiedName~MapForgeTests
   dotnet run --project Server/AdventureKing.Server
   ```
   Server local mặc định chạy `http://127.0.0.1:5080`. Khi đã chạy server mới, mở `Assets/Scenes/AK_Entry.unity` và Play, đăng nhập, đi `Thị trấn Chuông Gió → Đồng Cỏ Gió → Làng Nấm (Map 1) → Đồng Cỏ Xanh (Map 2)`; xác nhận cổng trở lại và đánh quái.
7. Xong và đạt yêu cầu mới commit scene / generated textures / meshes / .meta trên GitHub Desktop. **Không merge sang nhánh Chương 1 chính trước khi test.**

## Phạm vi đã lập trình

- Gameplay 2.5D với **4 bệ nhiều tầng**, thang/dây leo, collider thực từ pipeline có sẵn; không tạo project mới.
- 3D props hiện hữu: cối xay gió, cây, nấm, đèn; hàng rào + hoa gộp mesh, trang trí thay đổi bằng seed (không ngẫu nhiên hóa collision và vị trí spawn).
- Panorama đồng cỏ riêng, hệ thống parallax/camera/HUD/minimap/âm thanh dùng lại từ builder đã có.
- Server map `ch1_green_meadow`, hai cổng, vùng an toàn tại cửa tây, respawn, **8 vị trí quái** và quái Slime Phấn Hoa DRAFT (kinh tế do server xác nhận).
- Slime Phấn Hoa hiện sử dụng **mẫu 3D toon thủ tục** riêng để thử hệ thống farm, không phải model Meshy hoàn thiện. Nấm Non dùng prefab rigged hiện có.
- **Không sử dụng token Claude, credit Meshy, không đổi Gold/KC hiện có**, các con số Gold quái mới là DRAFT.

## Những điều chưa làm / chưa xác minh

- Chưa chạy Unity thực tế, chưa có ảnh QA từ scene mới, chưa đo hiệu năng Android/iOS, chưa nghiệm thu mỹ thuật.
- Chưa có nhiệm vụ `ch1_04`, nhạc riêng, Meshy Slime Phấn Hoa, VFX theo biome, cổng mở Map 3.
- Công cụ v1 dựng **Map 2** với các biến thể trang trí, **không tự động dựng hoàn chỉnh 200 map**. Map 3–200 cần thêm layout/data/server/kiểm thử riêng.
- Nếu thiếu prefab/model hoặc lỗi đỏ trong Console, **dừng** và đối chiếu thông báo lỗi; không xóa dữ liệu/scene hay chạy Rebuild World Maps vô điều kiện.

## File thay đổi

- `Assets/Editor/ArtPipeline/AKMapForgeWindow.cs` — UI công cụ.
- `Assets/Editor/ArtPipeline/VillageSceneBuilder.MapForge.cs` — factory Map 2 và patch Map 1 không phá scene.
- `Assets/Editor/ArtPipeline/VillageSceneBuilder.Chapter1.cs` — đăng ký scene / cổng khi batch build.
- `Assets/Scripts/Gameplay/TownLayout.cs`, `AdventureWorldSession.cs` — map và chuyển cảnh.
- `Server/AdventureKing.Server/Domain/CharacterService.cs`, `FarmService.cs`, `config/monsters.json` — server.
- `Server/AdventureKing.Server.Tests/MapForgeTests.cs` — test kiểm soát vị trí quái/cổng (chưa chạy).
