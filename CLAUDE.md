# AdventureKing_Reborn — hướng dẫn cho Claude (đọc trước mỗi phiên)

## Project
- Thư mục đúng: `C:\AdventureKing_Reborn` (KHÔNG phải "AdventureKing_Final" — đó chỉ là tên nhóm phiên cũ trên Claude Desktop).
- Repo: `https://github.com/samlove113a-art/AdventureKing_Reborn` (private). Nhánh làm việc: `feat/chapter1-mushroom-kingdom`. `main` chỉ có Initial commit — không merge vào main khi chưa được yêu cầu.
- `git` KHÔNG có trong PATH. Dùng: `C:\Users\PC\AppData\Local\GitHubDesktop\app-3.6.10\resources\app\git\cmd\git.exe`.
- Shell không có credential GitHub → `fetch/push` thất bại; chủ dự án push/publish bằng GitHub Desktop.
- Unity **6000.6.7f1** (URP). Server **ASP.NET 8 + SQLite** ở `Server/` (`dotnet test Server/AdventureKing.Server.Tests`).

## Đọc tiếp (theo thứ tự, chỉ khi cần)
1. `Docs/SESSION_HANDOFF.md` — mục "TRẠNG THÁI HIỆN TẠI" ở đầu file (phần dưới là lịch sử).
2. `Docs/CHAPTER01_PLAN.md` — kế hoạch Chương 1, quyết định đã chốt, ngân sách + nhật ký credit Meshy.
3. `Docs/PROJECT_MASTER_CONTEXT.md` — tầm nhìn, kiến trúc, bảng trạng thái mọi hệ thống.
4. `Docs/SKILL_VFX_REVIEW.md` — VFX 12 skill (bản nháp chờ duyệt), sửa vị trí phát skill.
5. `ArtSource/Concepts/Chapter01/README_FOR_CLAUDE_FIRST.md` — gói concept Chương 1.

## Game (không đổi)
MMORPG online, fantasy, nhân vật **3D chibi** anime, gameplay **2.5D cuộn ngang** (Rigidbody2D + model 3D, CameraFollow2D, joystick cảm ứng). Windows / Android / iOS. 4 nghề: Kiếm Sĩ, Pháp Sư, Sát Thủ, Cung Thủ (3 skill mỗi nghề). Server quyết định sát thương, phần thưởng, kinh tế, tiến trình.

## Quy tắc bất biến
- KHÔNG tạo project/game mới, KHÔNG reset/checkout đè/xóa thay đổi chưa commit, KHÔNG làm lại hệ thống đang chạy (nhân vật, hub Thị trấn Chuông Gió, Đồng Cỏ Gió, sảnh bang/cưới, shop, pet…).
- KHÔNG sửa lễ cưới 180 s (WIP `670f6f9`) khi chủ dự án chưa yêu cầu.
- KHÔNG tự merge nhánh/PR cũ hoặc thử nghiệm khi chưa kiểm tra.
- KHÔNG báo hoàn thành giả: phân biệt "có code" / "đã test" / "chủ dự án đã duyệt". Test pass ≠ đẹp. Không đếm scene rỗng là map.
- KHÔNG placeholder làm thành phẩm, KHÔNG copy tài sản MapleStory/game khác, không Missing Script, không nền đen/scene trống.
- Kinh tế: giá/tỉ lệ rơi/EXP/Gold mới = **DRAFT**. KHÔNG bật VietQR/thanh toán thật, KHÔNG phát Kim Cương.
- Meshy: KHÔNG tiêu credit khi chưa kiểm tra số dư + dự toán; trần Chương 1 = **2.000** (tính cả phần đã tiêu, xem nhật ký trong `CHAPTER01_PLAN.md` §6). Làm 1 mẫu → kiểm tra → mới làm loạt. Key bị limit thì không gọi lặp. Nạp key từ biến môi trường **User**. Ưu tiên Unity/Blender/tái dùng + recolor.
- KHÔNG in/commit API key, token, mật khẩu. KHÔNG mua/gia hạn dịch vụ.
- Tiết kiệm: không chạy full regression sau mỗi sửa nhỏ (chạy test mục tiêu; full suite cuối nhóm việc), không lặp vô hạn một lỗi — phân tích nguyên nhân (đo bằng số) trước khi sửa.
- Trước khi kết thúc nhóm việc / khi context gần đầy: cập nhật `Docs/SESSION_HANDOFF.md` (mục đầu) + commit theo nhóm đã test.

## Mẫu dựng map Chương 1 (Map 1 là chuẩn)
Client: `Assets/Scripts/Gameplay/TownLayout.cs` (`Ch1Village`: decks/climbs/portals/safe zone) + `MapInfo` registry; `Assets/Editor/ArtPipeline/VillageSceneBuilder.Chapter1.cs` (dựng scene bằng code; `ChapterScenePaths`); `AdventureWorldSession.cs` (map→scene). Server: `CharacterService.cs` (Maps/Portals), `FarmService.cs` (SafeZones/RespawnPoints), `config/monsters.json`, `config/quests.json`. Test: `Server/.../Chapter1Tests.cs`, PlayMode `Chapter1Tests`.
