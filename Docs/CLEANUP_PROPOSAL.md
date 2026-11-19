# Đề xuất dọn file thừa (CHỜ CHỦ DỰ ÁN DUYỆT — chưa xóa gì)

Ngày: 2026-11-19 · nhánh `feat/pixel-heroes-20261118`. Mọi mục dưới đây đã kiểm: thư mục/tệp nào đã nằm trong git thì xóa vẫn lấy lại được từ lịch sử; tệp bị `.gitignore` thì xóa là mất hẳn (nhưng đều sinh lại được).

## A. An toàn, đề nghị xóa (không ảnh hưởng game)
| Mục | Dung lượng | Vì sao |
|---|---|---|
| `Assets/InitTestScene*.unity` (+ `.meta`, 36 tệp) | nhỏ | Scene tạm do test PlayMode đẻ ra, đã bị `.gitignore`, chưa từng vào git |
| `Server/**/bin`, `Server/**/obj` | ~70 MB | Đầu ra build .NET, bị ignore, `dotnet build` sinh lại |
| `Builds/` | 813 MB | Bản build Windows thử, bị ignore, build lại được |
| `QA/Logs/*.log`, `QA/Results/*.xml`, `QA/*.log|*.ps1|*.xml` (~185 tệp) | ~22 MB | Log/kết quả test cũ của các phiên trước; kết quả mới nằm trong `QA/PixelActors`, `QA/Chibi2D`… |
| `ICON_CROPS_PREVIEW.png` ở thư mục gốc | nhỏ | Ảnh xem trước tạm |

## B. Nên xóa sau khi bạn xác nhận (đã bị thay bằng bản pixel, nhưng còn là bản gốc/dự phòng)
| Mục | Dung lượng | Ghi chú |
|---|---|---|
| `ArtSource/Chibi2D/_meshy`, `_meshy2` (FBX Meshy 3D các class) | ~86 MB | Model 3D cũ đã bake; CLAUDE.md nói **giữ model 3D làm dự phòng** → chỉ xóa nếu bạn bỏ hẳn hướng 3D |
| `ArtSource/Chibi2D/_MeshyPixel`, `_Hand` | 2,4 MB | Sprite bake/vẽ bằng code đã bị bạn loại |
| `Tools/chibi2d/swordsman_pixel.py`, `swordsman_rig.py` | nhỏ | Sprite vẽ bằng code đã bị loại (nhưng còn dùng làm tham chiếu kỹ thuật) |
| `ArtSource/Chibi2D/_Meshy2D` | 87 MB | Bản xử lý trung gian của sprite pixel; nguồn cuối nằm ở `ArtSource/Chibi2D/{Tên}` |
| `ArtSource/Backup` | 47 MB | Bản sao lưu cũ, cần bạn xem lại trước |

## C. Thư mục QA lớn (ảnh chụp các phiên trước, đã nằm trong git)
`QA/UI` 225 MB · `QA/SkillVfx` 156 MB · `QA/Video` 94 MB · `QA/Halls` 77 · `QA/Multiplayer` 77 · `QA/Wedding180` 50 · `QA/Before_PhaseA2` 42 · `QA/HubTest` 23 · `QA/WorldCaptures` 21 MB.
Đây là bằng chứng kiểm thử cũ, không game nào đọc. Đề nghị: giữ `QA/Chibi2D`, `QA/PixelActors`, `QA/Chapter1`, `QA/Chapter1Maps`, `QA/Shop`; bỏ phần còn lại khỏi cây làm việc (lịch sử git vẫn giữ).

## D. KHÔNG xóa
- `Assets/Art/Characters`, `Assets/Art/Outfits`, `Assets/Resources/Chibi2D/*` (hệ nhân vật, trang phục, prefab đang dùng; model 3D còn là dự phòng).
- Prop 3D `Assets/Art/World/**/Props` của Hub, Đồng Cỏ, hai sảnh (các scene đó chưa đổi sang pixel) và model 3D Chương 1 (còn là dự phòng khi thiếu sprite).
- `ArtSource/Meshy/raw` (1,7 GB, đã `.gitignore`): chứa mọi ảnh/model gốc Meshy (Meshy xóa sau 3 ngày). Chỉ xóa khi bạn đã sao lưu ra nơi khác.
- Thư mục `Library` (7,9 GB): bộ nhớ đệm Unity, sinh lại được nhưng build lại rất lâu.

## E. Đã làm sẵn (không xóa dữ liệu)
- Lược ảnh base64 khỏi ~60 tệp `ArtSource/Meshy/requests/*.json` (còn ~100 MB → vài trăm KB); ảnh tham chiếu thật vẫn ở `ArtSource/Meshy/ref/`.

Ghi chú: xóa khỏi cây làm việc không rút nhỏ lịch sử git; muốn nhẹ kho thật cần viết lại lịch sử (chưa làm, cần bạn đồng ý riêng).
