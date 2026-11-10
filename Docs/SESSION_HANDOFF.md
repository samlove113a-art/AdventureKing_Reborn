# Adventure King Reborn — Bàn giao phiên làm việc

**Ngày:** 2026-11-08 · **Repo:** `samlove113a-art/AdventureKing_Reborn` · **Nhánh:** `feat/diamond-store-catalog-20261107`
**Commit đã có trên GitHub trước phiên:** `cc3905a` · **Commit của phiên này:** xem `git log` (mục 9 ghi trạng thái push).

> Game **chưa đạt chuẩn thương mại**. Tài liệu này chỉ ghi điều đã chạy và kiểm thử thật; phần nào chưa kiểm chứng được ghi rõ.

---
## ▶ ĐANG LÀM: CHƯƠNG 1 — xem `Docs/CHAPTER01_PLAN.md` (kế hoạch, quyết định, ngân sách Meshy, nhật ký)
- Nhánh: `feat/chapter1-mushroom-kingdom`. Sao lưu trước Chương 1: nhánh `backup/pre-chapter1-20261110` (= `670f6f9`, đã có trên GitHub qua nhánh `feat/diamond-store-catalog-20261107`) + git bundle + database trong `C:\AdventureKing_ArtBackup\`.
- **Lễ cưới 180 s = WIP, TẠM DỪNG** (`670f6f9`): server 8 cảnh/180 s + chống mất kết nối (158/158 test) đã xong; client timeline/IK/đạo cụ/âm thanh có nhưng **chưa nghiệm thu** (test xem trước còn lỗi tầm với khi Sát Thủ uống ly; chưa chạy bản 2 client 180 s; chưa có video). Không sửa tiếp khi chưa có lệnh.
- Meshy: key đang cấu hình (biến môi trường User) 3.242 credit; key cũ còn 32 (không dùng).

---
## ★★★★★★★★★★★★★★★★ GIAI ĐOẠN D — HOÀN THIỆN: Bang Hội, Kết Hôn, Bạn bè/Chat, Multiplayer (2026-11-10)
**Chỉ ghi điều đã chạy và kiểm thử thật.**

### Kiểm thử nhiều client trên cùng một server (đã chạy thật)
`Assets/Tests/PlayMode/MultiplayerSocialTests.cs`: **2 tài khoản thật** trên cùng server thử nghiệm — người chơi A là game Unity (bấm UI thật), người chơi B là client thứ hai độc lập (`SecondClient.cs`: phiên đăng nhập riêng, cùng các endpoint, nhịp đồng bộ 6 Hz riêng). Kết quả: **PASS**. Ảnh: `QA/Multiplayer/*.png`.
1. Hai người thấy nhau trên cùng map; B di chuyển → A thấy B chạy mượt (nội suy 150 ms); server chặn dịch chuyển tức thời (kiểm tra tốc độ — đã xác nhận: B "nhảy" 4.9 m trong 1 nhịp bị server kéo lại).
2. Chat Thế giới (B → A, hiện ở dòng chạy chữ), chat Riêng (A → B qua cửa sổ Xã hội).
3. Đứng cạnh B → "E · Tương tác" → mở cửa sổ Xã hội cho B → **Kết bạn** (UI) → B đồng ý → trạng thái online lấy từ server.
4. A **lập bang "Gió Chuông"** bằng UI (phí theo cấu hình dev) → B xin vào → A **Duyệt** trong tab Đơn xin vào → 2 thành viên → chat Bang.
5. Cả hai vào **Sảnh Bang Hội — phiên bản riêng của bang** (người ngoài bang không thấy), tên bang hiện trên bảng tên.
6. A **cầu hôn** (UI) → B đồng ý → cả hai sang **Sảnh Kết Hôn** → A bấm "Tổ chức lễ cưới" → B xác nhận → **lễ cưới hai người** chạy theo đồng hồ server: dẫn nhập, lời thề (cúi chào), trao nhẫn (quỳ + nhẫn), cắt bánh (dao + động tác cắt + miếng bánh), nâng ly (ly trên tay), **hôn** (nghiêng người vào nhau, mắt nhắm + má hồng, tim bay) → sau nụ hôn: cô dâu **làm hình trái tim**, chú rể **lắc lư vui** → kết (ăn mừng, cánh hoa).
7. Server ghi **đã kết hôn**, tặng 3 món quà cưới; bảng tên hiện "♥ tên vợ/chồng".
8. **Đăng nhập lại** (phiên mới) → vẫn đã kết hôn, đúng tên bạn đời, vẫn trong bang, vẫn sở hữu quà cưới.

### Meshy — đợt này (chủ dự án cho phép tối đa 77 credit của tài khoản cũ)
- Số dư thực tế kiểm tra trước khi dùng: **77**. Đã tiêu **27**: Swordsman 9 (3 clip) + Archer/Assassin/Mage 6 mỗi lớp (2 clip). **Số dư còn lại: 50** (kiểm tra lại qua API sau khi chạy).
- Clip mới (thư viện animation Meshy, không tạo lại model): **Big Heart Gesture** (sau nụ hôn), **Happy Sway Standing** (vui mừng khi cắt bánh / sau nụ hôn). Thư viện **không có** clip hôn hay cắt bánh → nụ hôn và nhát cắt làm bằng tư thế thủ tục (`PoseOverlay`) + biểu cảm khuôn mặt (`HeroFace`). Đã thử "Charged Axe Chop" cho Swordsman (nằm trong 9 credit) → **không dùng** (bắt đầu bằng tư thế cúi rạp sát đất).
- **Không dùng tài khoản Meshy mới (3.842 credit)** — dành cho Chương 1.

### Đã sửa trong đợt này
- Camera lễ cưới luôn theo đúng bước của server (trước đây trễ dần → cảnh hôn bị lệch khung).
- Nhắc "E · Nói chuyện" của NPC không còn hiện trong lúc chiếu cinematic.
- Động tác cắt bánh: vung tay trong mặt phẳng màn hình (trước đây xoay về phía ống kính nên gần như không thấy); chú rể đứng đúng cạnh bánh.
- Cửa sổ Xã hội: mọi nút/ô nhập ≥ 40 px trên điện thoại và PC (UiAudit sạch); danh sách thành viên bang có phân trang.
- Quà cưới (vương miện ngọc trai, găng lụa, áo choàng trắng) trước đây **không có hình 3D** → đã dựng (biến thể thủ tục từ đồ thời trang có sẵn, 0 credit), mặc được trên cả 4 lớp nhân vật.
- Ghi chú kỹ thuật: ảnh chụp trong chế độ `-batchmode` **không thể hiện** tư thế thủ tục (LateUpdate) — ảnh lễ cưới cuối cùng được chụp bằng trình chạy test có cửa sổ (`run_tests_windowed`), đúng như khung hình game thật.

### Kết quả bộ test (lần chạy cuối)
Server **155/155** · EditMode **4/4** · PlayMode **48 passed / 0 failed / 2 skipped** (2 test của gói Input System do Unity bỏ qua) · 0 exception. Gồm cả test đa client `MultiplayerSocialTests`, test sảnh `HallTravelTests`, thú cưng / sự kiện / thư / giftcode / VIP / BXH / cửa hàng thời trang (hồi quy A/B/C không lỗi). Server: migration v10→v11 + backup/restore + chống trùng (idempotency) có test riêng. Meshy: số dư xác nhận qua API sau đợt = **50**.

### Còn lại / chưa kiểm chứng
- **Thiết bị Android thật**: chưa có máy kết nối → chưa thử multiplayer trên điện thoại (bố cục cảm ứng đã kiểm bằng UiAudit ở tỉ lệ điện thoại).
- Multiplayer dùng HTTP polling 6 Hz (đủ cho thị trấn/sảnh); chưa có WebSocket, chưa thử tải nhiều người (>2 client).
- Giá/phí bang, nhẫn, quà, mức đóng góp vẫn là **DRAFT** (`config/social.json`: chưa duyệt → không thu phí, không mở đóng góp). Test dùng `social.dev.json`.
- Động tác cắt bánh / nụ hôn là tư thế thủ tục, không phải clip motion-capture (thư viện Meshy không có).

---
## ★★★★★★★★★★★★★★★ GIAI ĐOẠN D: Sảnh Bang Hội & Sảnh Kết Hôn bằng Meshy (2026-11-10)
**Trạng thái:** hai sảnh đã có scene 3D thật, đi lại và tương tác được, có kiểm thử. **Chưa phải bản nghiệm thu cuối**: chủ dự án cần duyệt đồ họa; một số hạng mục còn thiếu (xem cuối mục).

### Meshy (chủ dự án cho phép; không mua credit, không nâng gói, không tự đổi hạn mức)
- **Credit:** 931 → 137 cho hai sảnh, rồi **−60 cho động tác nghi lễ (chủ dự án duyệt) → còn 77**. Chi tiết hai sảnh (794 credit):

  | Hạng mục | Credit |
  |---|---|
  | 24 concept (1 vẽ lại vì Chủ Hôn nhắm mắt) | 144 |
  | 16 model đạo cụ | 480 |
  | 2 retexture | 20 |
  | 3 NPC (model + rig + 5 animation) | 150 |

- **Giữ 137 credit cho quái/Boss Chương 1** theo yêu cầu.
- **Tiết kiệm:**
  - Cột cẩm thạch và tường cửa sổ hoa hồng của Sảnh Kết Hôn là **retexture** từ cột/tường Sảnh Bang Hội: 10 credit thay vì 30.
  - Nhẫn cưới, ánh sáng và cánh hoa làm bằng Unity, không dùng Meshy.
- Có lúc API key bị chặn ("API key credit limit reached"). Khi bị chặn không gọi lặp; chỉ kiểm tra lại sau khi chủ dự án báo.
- Mọi task ghi trong `ArtSource/Meshy/manifest.jsonl`. File gốc ở `ArtSource/Meshy/raw/`: thư mục này **không nằm trong git**, cần sao lưu.

### Tài nguyên (Meshy → Blender → Unity, toon shader chung)
- **Sảnh Bang Hội:** ngai Bang Chủ, bàn họp 6 ghế, cột chạm, tường kính màu, cờ hiệu có huy hiệu, bảng xếp hạng, bàn thờ cống hiến, quầy tiếp tân kèm bảng nhiệm vụ, lò lửa pha lê ×2, đèn chùm ×2, sàn đá Meshy.
- **Sảnh Kết Hôn:**
  - lễ đường riêng (scene riêng);
  - cổng hoa ở lối vào, ghế khách 2 hàng ×3;
  - sân khấu tròn có mái hoa, gối nhẫn và 2 nhẫn vàng;
  - đàn hạc, 2 bàn tiệc, bánh cưới 4 tầng, đèn chùm ×2;
  - thảm cưới (texture Meshy), cánh hoa rơi.
- **Số tam giác:** mỗi model 5k–18k tam giác.
  - Tường và cột lặp lại dùng model nhẹ (6–8k).
  - Tầng tường phía trên chỉ để che trời trong cảnh quay cinematic. Camera chơi không thấy, nên bị cull khi chơi.
- **NPC:**
  - Đội Trưởng Liên Minh, Chủ Hôn, Nhạc Công: Idle / Walk / Wave / Talk / Bow.
  - **Mặt động** (chớp mắt, biểu cảm) như anh hùng: Đội Trưởng đã có.
  - Chủ Hôn và Nhạc Công **giữ mặt vẽ**: đo mặt tự động không ra kết quả hợp lệ (râu che miệng; mắt bị nhận sai). Công cụ có bước kiểm tra nên không áp mặt hỏng.
- **Sửa chất lượng:**
  - Sàn sân khấu Meshy bị tô gần đen → vẽ lại thành cẩm thạch (bản gốc giữ trong `ArtSource/Meshy/raw/wh_altar_stage_model/`).
  - Lỗi viền đen trên mặt anh hùng đã sửa từ trước.

### Chức năng
- **Server:** 2 bản đồ an toàn (không quái), 4 cổng Thị trấn ↔ Sảnh, cổng kiểm tra vị trí. Hai cổng mới trong làng có biển chỉ đường; vào sảnh không hỏi xác nhận.
- **Nói chuyện NPC:** E / ▲ hiện bong bóng thoại. Đội Trưởng nói rõ hệ thống bang hội **đang được xây dựng**.
- **Diễn tập nghi thức cưới:** nói chuyện với Chủ Hôn sẽ chạy cinematic 7 cảnh:
  - cảnh toàn → lối vào → lời thề (Chủ Hôn nói) → trao nhẫn (cận gối nhẫn) → cắt bánh → nâng ly → kết, có mưa cánh hoa;
  - có thanh letterbox, phụ đề, nút Bỏ qua; ẩn HUD và nhãn nổi;
  - trả camera và điều khiển khi xong.

### Kiểm thử
- Server: `HallTravelTests` (2): vào/ra hai sảnh, đứng xa bị từ chối, mọi cổng có cổng về.
- Play Mode `HallTravelTests`:
  - đi Thị trấn → Sảnh Bang Hội → Thị trấn → Sảnh Kết Hôn → Thị trấn qua server thật;
  - không còn chỗ thiếu model, không Missing Script; HUD audit 5 độ phân giải;
  - nói chuyện Đội Trưởng (có mặt động);
  - chạy trọn nghi thức: camera chiếm quyền rồi trả lại, input khóa rồi mở, ≥3 lần mưa cánh hoa, chụp đủ 7 cảnh.
- Ảnh: `QA/Halls/*.png`.
- **Chạy toàn bộ:** server 146/146; EditMode 4/4; **PlayMode 49 test, 47 qua, 2 bỏ qua (như trước), 0 lỗi, 0 exception.**
- Lỗi thật đã sửa nhờ xem ảnh:
  - HUD và nhãn nổi vẫn hiện trong cinematic;
  - sàn sân khấu đen;
  - Chủ Hôn đứng sai chỗ;
  - lộ bầu trời phía trên tường trong cảnh rộng;
  - tên Đội Trưởng xuống 2 dòng đè nhãn "E · Nói chuyện";
  - đèn điểm không có tác dụng: URP của dự án tắt đèn phụ ở mọi mức đồ họa, nên đã gỡ.

### CHƯA ĐẠT / CẦN DUYỆT (không tự nhận là hoàn thiện)
- **Động tác nghi lễ cho nhân vật người chơi (chủ dự án duyệt 60 credit, đã dùng đúng 60; còn 77):**
  - 4 nghề có thêm 5 clip Meshy: **Kneel** (quỳ một gối trao nhẫn), **Toast** (nâng ly), **Cheer**, **Celebrate**, **Bow**.
  - Trong buổi diễn tập, nhân vật của người chơi:
    - cúi chào khi Chủ Hôn đọc lời thề;
    - quỳ trao nhẫn trước sân khấu;
    - ăn mừng bên bánh cưới;
    - nâng ly ở bàn tiệc (cầm ly vàng ở tay trái, đúng tay mà clip đưa lên miệng);
    - cổ vũ ở cảnh cuối.
  - Vũ khí được cất khi diễn tập và trả lại khi xong. Clip nâng ly được cắt bỏ đoạn đứng yên đầu (đo độ cao tay trong Blender).
  - Bản FBX cũ của 4 anh hùng được sao lưu ở `ArtSource/Backup/heroes_before_ceremony_2026-11-10/`.
  - **Còn thiếu:**
    - **hôn** và **cắt bánh bằng dao**: thư viện Meshy không có, cần một cặp người chơi (hệ thống kết hôn);
    - ly nâng còn nhỏ, hơi khuất trong bàn tay chibi.
- **Lễ cưới thật cho 2 người chơi:** cần hệ thống kết hôn trên server (cầu hôn, bạn đời, nhẫn là vật phẩm). Chưa làm; hiện chỉ có **diễn tập**, phụ đề nói rõ.
- **Hệ thống bang hội** (lập bang, cống hiến, nhiệm vụ bang, BXH bang): chưa làm. Sảnh và NPC đã sẵn.
- **Chất lượng:**
  - Cận cảnh rất gần lộ giới hạn texture Meshy 2k; các cảnh quay đã đặt camera ở khoảng cách đẹp.
  - Cột Sảnh Kết Hôn (retexture) không có vòng hoa như concept.
  - Chủ Hôn và Nhạc Công chưa có mặt động.
- **Android:** chưa thử trên máy thật (không có thiết bị kết nối). Xem `Docs/ANDROID_DEVICE_TEST.md`.
- **Đồ họa cần chủ dự án xem và duyệt.**

---
## ★★★★★★★★★★★★★★ GIAI ĐOẠN C: Pet, sự kiện / phúc lợi, hộp thư, giftcode, BXH, khung tên (2026-11-10)
**Trạng thái:** đã làm và kiểm thử (chi tiết test bên dưới). Commit: pet server `5fb42c3`, pet client `780655d`, sự kiện/thư/giftcode server `ce69124`, BXH/khung tên server `8e49dda`, client: xem `git log`.
Không tiêu Meshy (số dư vẫn 931), không mua gì, không bật thanh toán, **không phát Kim Cương hay vật phẩm cao cấp ở bất kỳ đâu**. Mọi giá, phần thưởng và mốc là ĐỀ XUẤT.

### Pet
- 4 pet dựng trong engine (0 credit): Mèo Mây 3.000 Vàng, Gà Bông 2.500 Vàng, Rồng Ngọc (VIP) 180 KC, Cáo Lửa (VIP) 240 KC.
  - Mua một lần, chỉ triệu hồi một con. Pet VIP cần Kim Cương, mà thanh toán đang tắt, nên server từ chối và nói rõ lý do.
- No 60 phút. Chỉ giảm khi pet đang đi theo và người chơi đang farm thật (giới hạn 5 giây mỗi lần cập nhật, nên AFK hay offline không bị trừ).
- Thức ăn bán ở tab "Thức Ăn Pet" của Mộc Linh: 20 và 150 Vàng (đề xuất). Pet đã no thì từ chối ăn, không mất thức ăn.
- **Pet nhặt Vàng:** chỉ nhặt drop của chính chủ, trong tầm 8 m, khi pet còn no. Server trả tiền một lần cho drop đó, giống nhặt tay.
- Cửa sổ Pet (phím P hoặc nút Pet): mua (có xác nhận), triệu hồi / thu hồi, cho ăn, thanh no.
- Test: `PetServiceTests` (server), `PetTests` (Play Mode, ảnh `QA/Pets/*`).
- ⚠ Model pet là khối cơ bản dựng trong engine, **cần art thương mại** (cần duyệt).

### Sự Kiện / Phúc Lợi (server `EventService`, DB v8)
- Cửa sổ lớn, tab bên trái lấy từ server:
  - **Sự Kiện Mới** "Săn Nấm Lễ Hội" (01/11 → 31/12/2026, giờ VN): hạ 30 quái, nhặt Vàng 20 lần, dùng kỹ năng 25 lần. Tiến độ đếm từ **sự kiện server** (cùng nguồn với nhiệm vụ), chỉ trong thời gian diễn ra.
  - **Đăng Nhập:** mở bảng Điểm Danh 7 ngày đã có.
  - **Nạp Đầu:** **khóa**, vì thanh toán thật chưa bật.
  - **Phúc Lợi Online:** 10 / 30 / 60 phút mỗi ngày. Client gửi heartbeat 30 giây một lần; server chỉ cộng tối đa 40 giây mỗi lần (thoát game hay AFK ngoài sảnh không được cộng). Làm mới lúc 00:00 giờ VN.
  - **Quà Tân Thủ:** hiển thị "Đã nhận" sau khi gói tân thủ được phát lần đầu mở túi.
  - **Quà Lên Cấp:** cấp 2 / 5 / 10.
- Mỗi phần thưởng có một dòng claim (khóa chính), nên **chỉ trả một lần**. Server kiểm tra túi đủ chỗ trước khi trả; túi đầy thì từ chối và không trừ gì.
- **Chỉ Vàng và vật phẩm thường** (thuốc, thức ăn pet, vé). Server **từ chối khởi động** nếu cấu hình có trang bị hay vật phẩm khác.
- **Production:** `config/events.json` là **DRAFT `enabled:false`**. Nhận thưởng trả về "Chờ duyệt"; nút hiện "Chờ duyệt" khi đã đủ điều kiện. Server **từ chối khởi động** nếu ai đó bật một bản DRAFT.
  - Chỉ test dùng `events.dev.json` (`DRAFT_DEV_TEST_ONLY`, có thêm quà cấp 1 làm fixture cho test client).
- Chấm đỏ trên icon Sự kiện / Phúc lợi chỉ hiện khi server trả `anyClaimable`.

### Hộp Thư (`MailService`)
- Loại thư: hệ thống / phần thưởng / thông báo (thư bang hội khi có bang hội).
- Thư **chỉ do code server tạo**, không có API cho client tạo thư. Thư chào mừng được gửi một lần khi mở hộp thư lần đầu.
- Mở thư thì đánh dấu đã đọc. Vật phẩm đính kèm (Vàng và đồ thường) **nhận một lần**, cùng transaction với việc trả thưởng. Túi đầy thì thư vẫn còn quà để nhận sau.
- Chấm đỏ chỉ hiện khi có thư chưa đọc hoặc quà chưa nhận.
- **Phần thưởng VIP / Boss Hunter gửi vào thư:** đường ống đã sẵn (`MailService.Send` từ server), nhưng **chưa có gì gửi**, vì chưa có VIP hợp lệ và chưa có Boss.

### Giftcode (`GiftcodeService`)
- Không có endpoint tạo mã; chỉ code server hoặc người vận hành tạo được. **Production không có mã nào.**
- Khi đổi, server kiểm tra lần lượt: mã tồn tại → chưa hết hạn → còn lượt → đủ cấp → **mỗi tài khoản một lần** (mọi nhân vật trong tài khoản dùng chung một lượt).
- Mã không phân biệt hoa thường; khoảng trắng và gạch ngang được bỏ qua. Giới hạn 20 lần thử trong 10 phút để chống đoán mã.
- Cửa sổ có ô nhập và nút Đổi Quà; kết quả là câu trả lời của server.

### Bảng Xếp Hạng (`RankingService`) — chỉ dữ liệu thật
- **Cấp Độ:** xếp theo cấp, rồi EXP, rồi ai đạt trước.
- **Lực Chiến:** tính từ chính chỉ số chiến đấu của server: sát thương/giây (nghề cộng trang bị), HP, phòng thủ, cấp. **Công thức là đề xuất.** Khung tên không cộng lực chiến.
- **Bang Hội** và **Boss Hunter:** trống, có ghi rõ lý do (chưa có bang hội, chưa có Boss thế giới). Không có tên giả hay bot.
- Hiện top 10 và dòng "hạng của bạn".

### Khung tên VIP / Boss Hunter / đặc biệt (`TitleService`, DB v9)
- Danh mục: 15 VIP, 10 Boss Hunter, 8 đặc biệt, đúng tên file của bộ PNG chủ dự án gửi.
- Quyền sở hữu **chỉ do code server cấp** từ dữ liệu hợp lệ; không có API cấp khung. Mỗi lúc trang bị một khung. Bỏ trang bị vẫn giữ quyền sở hữu.
- **Hiện không ai sở hữu khung nào**, nên trên đầu nhân vật chỉ có tên. Popup VIP nói rõ điều này.
- 33 PNG đã chép vào `Assets/Art/Nameplates/` và gắn vào nameplate.
  - Ảnh QA "nếu trang bị thì trông thế nào" (chỉ xem trước phía client, không cấp gì): `QA/UI/welfare/qa_frame_preview_*.png`.
  - ⚠ Đây là bộ hình khởi tạo (theo README của gói): ảnh VIP nhỏ (~180×85), một số khung còn lỗi cắt viền (đã ghi trong `ART_ASSET_PROVENANCE.md`). **Cần art QA hoặc vẽ lại trước khi phát hành.**

### Kiểm thử
- Server: `WelfareServicesTests` (7), `RankingTitleTests` (2). **Server 140/140.**
- Play Mode `WelfareMailTests` (server thật, qua icon HUD thật):
  - chấm đỏ theo server, heartbeat online đã gửi;
  - Sự kiện có 3 nhiệm vụ ở 0/…; Nạp Đầu khóa; nhận quà cấp 1 qua nút (bấm đúp chỉ trả một lần, Vàng HUD khớp server), sau đó chấm đỏ tắt;
  - thư chào mừng được đánh dấu đã đọc trên server;
  - giftcode lạ bị từ chối;
  - BXH có nhân vật của mình, Lực Chiến > 0, Boss trống có lý do;
  - nameplate không hiện khung.
  - UI audit 5 độ phân giải → `QA/UI/welfare/*`.
- `UiTownScreensTests` cập nhật: popup VIP lấy dữ liệu server và khung tên; nameplate không hiện khung.
- **Chạy toàn bộ:** server 140/140; EditMode 4/4; **PlayMode 48 test, 46 qua, 2 bỏ qua (như trước), 0 lỗi, 0 exception.** UI audit `QA/UI/welfare/report.txt`: không có vấn đề.
- Lỗi thật đã sửa nhờ xem ảnh (audit tự động không bắt được): chữ thư nâu đậm trên nền tối không đọc được → đổi sang chữ sáng; tiêu đề cột BXH bị lệch → dựng lại thành hàng tiêu đề thật.

### Chờ chủ dự án duyệt
- Giá pet và thức ăn; mọi phần thưởng sự kiện, online, lên cấp (đang DRAFT tắt); công thức Lực Chiến; ngưỡng VIP 1–14; mốc điểm Boss Hunter.
- Art: model pet thương mại, QA bộ khung tên.

---
## ★★★★★★★★★★★★★ GIAI ĐOẠN B — đợt 2: Dược Sư Mộc Linh, vé dịch chuyển, nhiệm vụ, đăng nhập về sảnh (2026-11-10)
**Trạng thái:** đã làm và kiểm thử. Server commit `fa31b37`, client: xem `git log`. Với phần này, **Giai đoạn B đã đủ hạng mục** theo chỉ thị.
Không tiêu Meshy, không mua gì, không bật thanh toán. Giá, quà và chỉ số vẫn là ĐỀ XUẤT.

### Server
- **Cửa hàng Mộc Linh** (`items.json` → `shops`):
  - 2 tab: Dược Phẩm (thuốc HP/MP nhỏ, vừa, lớn) và Vé Dịch Chuyển.
  - Mỗi lần mua **1–99**. Kiểm tra theo thứ tự:
    1. mặt hàng có trong danh sách của shop;
    2. nhân vật đang ở bản đồ của shop;
    3. **túi đồ đủ chỗ, trước khi trừ Gold**;
    4. đủ Gold.
  - Gold và vật phẩm thay đổi trong cùng một transaction. Khóa ledger `shop:{nhân vật}:{mã yêu cầu}`: bấm hai lần hoặc gửi lại không bị trừ tiền hay giao hàng hai lần.
  - Thức ăn Pet **chưa bán**, vì chưa có Pet (Giai đoạn C).
- **Vé dịch chuyển:**
  - Server khi khởi động **từ chối vé dẫn tới bản đồ chưa tồn tại**. Hiện chỉ có vé Đồng Cỏ Gió, vì chỉ có 2 bản đồ thật.
  - Dùng vé: bỏ đúng 1 vé và chuyển bản đồ trong một transaction.
  - Đang ở sẵn nơi đó, thiếu cấp, hoặc vật phẩm không phải vé: từ chối và **giữ nguyên vé**.
- **Đăng nhập lại luôn về sảnh:** nhân vật đang ở bản đồ farm khi thoát, mất mạng hay crash đều xuất hiện ở điểm spawn an toàn của Làng Chuông Gió. Gold, đồ, cấp, EXP và nhiệm vụ giữ nguyên. Đang ở sảnh thì giữ vị trí đã lưu.
- **Nhiệm vụ** (`quests.json`, DB v6):
  - **10 nhiệm vụ chính** đúng thứ tự chỉ thị, mở từng bước một: Gặp Trưởng Làng → luyện đánh thường → luyện 3 kỹ năng → gặp Mộc Linh → mua và dùng thuốc → tới Đồng Cỏ Gió → hạ 3 Slime → nhặt Vàng 3 lần → báo Trưởng Làng → hạ Slime Rêu.
  - **3 nhiệm vụ phụ.**
  - **3 nhiệm vụ hằng ngày**, làm mới lúc **00:00 giờ Việt Nam** theo đồng hồ server.
  - Tiến độ đến từ **sự kiện server**: hạ quái, nhặt Vàng, kỹ năng trúng quái, mua hàng, dùng thuốc, dịch chuyển.
  - Client chỉ được báo các sự kiện trong danh sách cho phép: `talk:*` và `practice:*`. Sự kiện khác trả `event_not_allowed`.
  - Nhận thưởng một lần cho mỗi (nhiệm vụ, kỳ): EXP, Gold, vật phẩm (kiểm tra túi trước). EXP lên cấp theo đường cong **đề xuất** 100 × cấp.
- **Thuốc khi thanh đầy:** người chơi có thể chủ động uống (cờ `force`). Vẫn không hồi vượt mức tối đa. Phím Q/R không bao giờ dùng `force`.
- **Test:** 6 test shop/vé/đăng nhập/nhiệm vụ. Server **127/127**.

### Client Unity
- **Dược Sư Mộc Linh (NPC):**
  - dựng trong engine, không dùng Meshy: dùng lại model chibi của nghề Pháp Sư, đội vương miện hoa sen và cánh chuồn chuồn;
  - quầy thảo dược ghép từ khối cơ bản: kệ, chai HP đỏ, MP xanh, hũ thảo dược, cuộn vé;
  - có bảng tên, "E · Nói chuyện", dấu nhiệm vụ "!"/"?";
  - khi nói chuyện: quay về phía người chơi, mỉm cười (rig khuôn mặt), động tác vẫy trượng chào.
  - ⚠ **Cần model NPC riêng và animation chào riêng** (cần duyệt credit/art).
- **Cửa sổ shop:**
  - tab Dược Phẩm / Vé Dịch Chuyển, dòng hàng có icon và giá;
  - chọn số lượng −/+, 1/10/99 (tối đa 99/lần), hiện tổng tiền;
  - **hộp xác nhận** trước khi mua;
  - Gold và túi đồ cập nhật theo server.
- **Vé:** mua ở shop. Trong Túi đồ bấm Dùng → hỏi xác nhận → dùng (hiệu ứng warp và màn tải). Thất bại thì vé vẫn còn.
- **Bảng Nhiệm vụ (bên trái HUD):**
  - tab Chính / Phụ / Hằng ngày; tiến độ thật từng mục; mô tả; phần thưởng;
  - nút Nhận thưởng, nút Chỉ đường (nói bản đồ và NPC cần tới);
  - cấp và thanh EXP; mũi tên thu gọn.
- **Dấu "!"/"?"** trên Trưởng Làng, Mộc Linh, Chủ Tiệm Lụa, theo nhật ký nhiệm vụ server.
- **Trưởng Làng** có thể nói chuyện bất kỳ lúc nào (E): bong bóng lời thoại và sự kiện `talk:elder`.
- **Luyện tập trong làng:** bấm kỹ năng trong khu an toàn sẽ chạy animation, không gây sát thương, không tốn MP, và tính cho nhiệm vụ luyện tập.
- Thông báo của cụm nút chuyển xuống giữa đáy màn hình, không che icon.

### Kiểm thử Play Mode
- `QuestHealerTests` (server thật, người chơi mới):
  - Trưởng Làng chào → nhiệm vụ 1 xong, có "?" → nhận thưởng;
  - luyện đánh và 3 kỹ năng → nhận thưởng;
  - "!" trên Mộc Linh → nói chuyện mở shop;
  - mua 5 thuốc qua giao diện (tổng 150, xác nhận) → Gold đúng theo server;
  - chọn số lượng 120 thì bị giới hạn 99;
  - mua và uống thuốc: hỏi xác nhận trước khi uống phí;
  - mua vé → dùng từ túi đồ → tới Đồng Cỏ Gió → nhiệm vụ 6 hoàn thành, vé đã mất.
- UI audit 5 độ phân giải: sạch, có thêm bảng nhiệm vụ.
- **Chạy toàn bộ:** server 127/127; EditMode 4/4; **PlayMode 46 test, 44 qua, 2 bỏ qua, 0 lỗi, 0 exception.**
- **Ảnh:** `QA/Quests/01_elder_turn_in.png` … `06_arrived_quest_done.png`, `QA/UI/town/*`.

### Còn lại / giới hạn
- **NPC Mộc Linh:** model mượn và động tác chào mượn từ hero, cần art riêng.
- **Hệ thống Bang Hội / NPC Đội Trưởng Liên Minh:** chưa làm. Mục này thuộc Phần VI/XIV, không thuộc danh sách Giai đoạn B.
- **"Không để nhân vật đứng bất động online":** hiện chưa có người chơi khác nhìn thấy nhau (chưa có multiplayer hiển thị), nên chưa áp dụng.
- **Chống thoát game để né chết:** trạng thái gục ngã nằm trên server. Đăng nhập lại về sảnh rồi quay lại farm vẫn phải hồi sinh. Hồi sinh hiện miễn phí.
- **Chờ duyệt:** giá thuốc, giá vé, thưởng nhiệm vụ, đường cong EXP.

---
## ★★★★★★★★★★★★ GIAI ĐOẠN B — đợt 1: kỹ năng, MP, Auto Skill, nhặt thủ công, túi đồ, thuốc, trang bị (2026-11-10)
**Trạng thái:** đã làm và kiểm thử. Commit từng phần: server `dba6a0d` + client (xem `git log`).
**Chưa làm trong Giai đoạn B:** NPC Dược Sư Mộc Linh, vé dịch chuyển, nhiệm vụ chính/phụ/hằng ngày, logout về sảnh.
Không tiêu Meshy, không mua gì, không đổi giá.

### Server (ASP.NET Core 8) — server quyết định mọi thứ
- **12 kỹ năng** (`config/skills.json`):
  - đúng tên theo chỉ thị;
  - hồi chiêu 5/15/20 giây, MP gốc 10/25/45, chi phí MP tăng 3 %/cấp;
  - hồi chiêu chung 0,45 giây.
- **MP do server giữ:** Kiếm Sĩ 80, Pháp Sư 140, Sát Thủ 90, Cung Thủ 100. MP tối đa tăng 5 %/cấp, hồi liên tục theo đồng hồ server.
- **Hệ số sát thương, vùng ảnh hưởng (target/self/line) và số mục tiêu tối đa là ĐỀ XUẤT CÂN BẰNG chờ duyệt.**
- `POST /characters/{id}/farm/skill` từ chối khi:
  - đang hồi chiêu, hoặc bấm nhanh hơn hồi chiêu chung;
  - không đủ MP;
  - không có mục tiêu trong tầm (không mất MP, không mất hồi chiêu);
  - ở khu an toàn (sảnh);
  - nhân vật đã gục;
  - kỹ năng không tồn tại.
- Quái chết do kỹ năng rơi Vàng như đánh thường (nhặt một lần, có ledger).
- **Túi đồ** (DB v5):
  - 40 ô, mỗi ô tối đa 99 vật phẩm cộng dồn;
  - thêm vào mà không đủ chỗ thì từ chối toàn bộ (`bag_full`, không mất gì); có hàm `Fits` để shop kiểm tra trước khi trừ Gold sau này;
  - mỗi thao tác là một transaction SQLite, vật phẩm chỉ nằm ở đúng một dòng nên không nhân bản được.
- **Quà tân thủ** cấp đúng một lần: 10 thuốc HP nhỏ, 10 MP nhỏ, vũ khí đúng nghề, áo vải, bùa. Đây là **ĐỀ XUẤT**.
- **Thuốc:**
  - trừ đúng 1 và hồi trong cùng giao dịch;
  - không hồi vượt mức tối đa;
  - đầy thanh hoặc đã gục thì từ chối, không trừ thuốc;
  - chống spam: 1 giây/lần.
- **Trang bị** (vũ khí / áo / phụ kiện):
  - đổi chỗ qua túi;
  - kiểm tra đúng nghề;
  - Tháo khi túi đầy thì từ chối;
  - **bonus tấn công/phòng thủ được combat server dùng thật** (Kiếm Sĩ 18 → 22 khi cầm Kiếm Tập Sự).
- **Test:** 8 test kỹ năng + 4 test túi đồ. Server **121/121**.

### Client Unity
- **Cụm chiến đấu góc phải** (`HeroActionBar`, cả PC và cảm ứng):
  - nút Đánh lớn, 3 kỹ năng xếp vòng cung (kỹ năng 3 khung vàng nổi bật hơn);
  - Auto, Nhặt, thuốc HP/MP (số lượng thật từ túi đồ), Túi đồ.
- **Phím:** J đánh · 1/2/3 kỹ năng · ↓/S nhặt (không nhặt khi đang leo thang) · Q thuốc HP · R thuốc MP · I túi đồ. Có nhãn phím trên PC.
- **UI động, không in sẵn vào ảnh:**
  - vòng hồi chiêu và số giây;
  - nút xanh xám + "MP" khi thiếu MP;
  - nhún khi bấm;
  - vòng sáng khi Auto bật.
- Bấm khi chưa dùng được thì có thông báo lý do (hồi chiêu, thiếu MP, khu an toàn).
- **Auto Skill:**
  - bật/tắt;
  - thứ tự ưu tiên đổi được (3›2›1, 1›2›3, 2›3›1, 3›1›2);
  - chỉ thi triển khi có quái trong tầm, đủ MP và hết hồi chiêu;
  - **không tự di chuyển, không tự tìm quái, không nhặt Vàng.**
- **Bỏ tự nhặt Vàng:** đứng lên đồng Vàng không còn tự nhặt; phải bấm ↓/S hoặc nút Nhặt. Nhặt tự động sẽ là việc của **Pet** (Giai đoạn C, chưa làm).
- **Túi đồ** (`InventoryPanel`): 40 ô có số lượng, 3 ô trang bị và tổng bonus, chi tiết vật phẩm, nút Dùng / Trang bị / Tháo, tất cả gọi server.
- **Thanh HP/MP:** HUD lấy MP từ server trên map farm. Kỹ năng gây hiệu ứng vòng sáng theo màu nghề và số sát thương bay lên.
- Cụm nút tự ẩn khi nói chuyện NPC, dịch chuyển hoặc gục ngã.

### Mỹ thuật (CẦN HOÀN THIỆN MỸ THUẬT THƯƠNG MẠI)
- `SkillIconArt.cs` tự vẽ bằng code (không dịch vụ trả phí, không Meshy):
  - 12 icon kỹ năng; icon Đánh, Auto, Nhặt, thuốc HP/MP, Túi đồ;
  - 6 icon vật phẩm;
  - khung vàng thường và khung kỹ năng 3.
- Thư mục: `Assets/Art/UI/Skills/`.
- Màu theo nghề, có chiều sâu và hiệu ứng sáng, nhưng **chưa đạt mức vẽ tay thương mại.** Icon Thiên Thạch còn yếu nhất.
- PNG mẫu của chủ dự án trong `Assets/Art/UI/SkillButtons/ExtractedConcept/` **vẫn chưa dùng** (có số in sẵn), giữ nguyên, chưa commit.

### Kiểm thử Play Mode
- `SkillCombatTests` (server thật, Pháp Sư):
  - sảnh từ chối kỹ năng;
  - Thiên Thạch hạ slime, trừ MP, hồi chiêu hiện 20 giây;
  - bấm lại khi đang hồi chiêu và khi thiếu MP: không gửi, có thông báo;
  - thuốc MP: 10 → 9, MP tăng;
  - Auto tự đánh khi có quái và đứng yên khi không có; nhân vật không bị di chuyển;
  - túi đồ: trang bị trượng, tấn công +6, vật phẩm được chuyển (không nhân bản).
- `FarmTests`: đứng trên Vàng không tự nhặt; bấm nhặt thì server cộng một lần.
- UI audit 5 độ phân giải: thêm màn Túi đồ. Audit giờ còn ghi rõ phần tử nào che chữ.
- **Ảnh:** `QA/Skills/01_meteor_cooldown.png`, `02_auto_on.png`, `03_bag_selected.png`, `04_bag_equipped.png`, `QA/UI/town/02g_bag_*.png`.

### Kết quả chạy
- Server **121/121**; EditMode **4/4**.
- Lần chạy toàn bộ PlayMode: 45 test, 42 qua, 2 bỏ qua, 1 lỗi (`SkillCombatTests`). Lỗi do test bị race: heartbeat của server trả MP thật về trước khi kịp bấm.
- Đã sửa test và chạy lại `SkillCombatTests`, `UiTownScreensTests`, `FarmTests`: 4/4 qua, 0 exception.

### Cần chủ dự án quyết định
- Hệ số sát thương / vùng / số mục tiêu của 12 kỹ năng.
- MP tối đa và tốc độ hồi MP.
- Sức hồi của thuốc.
- Quà tân thủ.
- Chỉ số trang bị tập sự.
- Giá thuốc (để sau cho Dược Sư).

---
## ★★★★★★★★★★★ Chỉ thị tổng "Kinh tế 200 map" — GIAI ĐOẠN A: sửa lỗi thực tế (2026-11-10)
**Trạng thái:** đã làm + kiểm thử Giai đoạn A. **Chưa bắt đầu Giai đoạn B** (skill/MP/Auto Skill, túi đồ, Dược Sư, vé, nhiệm vụ, logout về sảnh) và các giai đoạn sau. Meshy vẫn 931 (không gọi). Không mua tài nguyên, không bật thanh toán, không đổi giá/kinh tế (giá shop + 5.000 Gold vẫn là ĐỀ XUẤT).

### Đã sửa (có kiểm thử Play Mode thật)
1. **Hồi sinh vẫn nằm:**
   - **Nguyên nhân:** trạng thái `Death` trong animator không có đường ra. Chỉ trigger `Attack` (AnyState) kéo được nhân vật dậy, nên phải bấm đánh mới đứng lên.
   - **Sửa:**
     - `CharacterVisual3D` phát hiện HP từ 0 lên >0 (server hồi sinh), reset trigger Attack/Hit/Skill và chuyển ngay về `Idle`;
     - `HeroArtPipeline` có thêm chuyển trạng thái Death→Idle cho lần dựng lại sau.
   - **Test:** `FarmTests` chạy chết → hồi sinh → không còn tư thế Death → đi → nhảy → đánh.
   - **Ảnh:** `QA/Farm/08_revived.png`, `09_after_revive_moving.png`.
2. **Cổng dịch chuyển:**
   - **Nguyên nhân:** bậc đá của mô hình cổng không có va chạm, nên nhân vật đi xuyên ở mặt đất và trông như lún vào bệ.
   - **Sửa:**
     - Lúc dựng scene, `VillageSceneBuilder` đo chính mesh cổng: mặt cắt bậc thềm tại đường đi z=0 (cao 0,86 m, các bậc ≤0,30 m) và tâm lỗ vòng bằng lưới tia.
     - Từ số đo đó tạo `PortalSteps` (PolygonCollider2D lớp Ground).
     - Lốc xoáy đặt đúng tâm vòng: y=2,79. Giá trị chỉnh tay cũ là 3,3, bị lệch lên trên.
     - Vùng trigger đặt trên bậc cao nhất.
     - `PlayerMotor2D` có thêm bước lên bậc thấp ≤0,45 m (giống cầu thang thật; tường cao hơn thì vẫn chặn).
   - **Test mới `PortalStepsTests`:**
     - leo từ trái, đứng đúng mặt bậc trên cùng, vào được trigger;
     - lốc xoáy nằm giữa vòng;
     - xuống bên phải rồi leo lại;
     - nhảy và đáp lại trên bậc;
     - cổng vẫn hỏi xác nhận.
   - `TownHubTests` (đi đường thật qua sảnh) cũng qua.
   - **Ảnh:** `QA/Portal/01_on_top_step.png`, `QA/HubTest/05_portal.png`.
   - Dịch chuyển thất bại vẫn trả lại quyền điều khiển, không đổi dữ liệu (code cũ, không đổi).
3. **Mũi tên thu gọn:** chuyển vào góc trên-trái cụm icon, ngay dưới nút cài đặt, cạnh minimap; không còn lơ lửng giữa map. Cỡ 68 cho đủ vùng chạm ≥40 px trên điện thoại. Minimap luôn hiện kể cả khi thu gọn.
4. **Icon HUD bớt sơ sài:**
   - `HudArt` vẽ lại toàn bộ huy hiệu:
     - viền vàng vát cạnh có đinh tán;
     - nền xanh ngọc có quầng sáng;
     - mỗi biểu tượng có viền mực, đổ bóng và chiếu sáng cạnh;
     - thêm sao lấp lánh cho Sự kiện và dấu sáp cho Thư.
   - Vẫn là art tự vẽ bằng code, không lấy của game khác.
   - **Chưa đạt** mức ảnh mẫu thương mại vẽ tay. Nếu muốn mức đó cần art riêng (chờ duyệt).
5. **Logo màn đăng nhập — CẬP NHẬT 2026-11-10: dùng logo của chủ dự án.**
   - Chủ dự án gửi logo (rồng, vòng cung, vương miện, chữ ADVENTURE KING khắc rune, ruy băng REBORN).
   - Nguồn: `ArtSource/OwnerProvided/Logo/AK_Logo_Owner_white.png`, nền trắng, 1024×576.
   - `LogoArt.cs` tách nền trắng thành trong suốt, cả vùng trắng bên trong vòng cung. Bóng xám chuyển thành bóng tối bán trong suốt.
   - File game: `Assets/Art/Branding/AK_TitleLogo.png`. Hiện bên trái khung đăng nhập trên PC/điện thoại; ẩn trên 4:3 vì không đủ chỗ.
   - **Đã xóa theo yêu cầu** các logo cũ: logo đề xuất tự dựng (chữ 3D + vương miện 3D), các phần ghép của nó, bản nền đá cẩm thạch.
   - Logo SAM (màn mở đầu của studio) giữ nguyên.
   - Độ phân giải gốc chỉ 1024×576: đủ cho 1080p. Nếu cần nét hơn trên màn 2K/4K, cần bản PNG lớn hơn (tốt nhất nền trong suốt).
   - *Mục cũ dưới đây đã thay thế:*
   **Logo màn đăng nhập (ĐỀ XUẤT chờ duyệt — ĐÃ THAY):**
   - Logo "ADVENTURE KING / REBORN" dựng hoàn toàn trong Unity (`LogoArt.cs`): vương miện gắn ngọc, chữ vàng chuyển màu viền nâu đậm, ruy băng đỏ.
   - File `Assets/Art/Branding/AK_TitleLogo_PROPOSAL.png`.
   - Đặt bên trái, cân với khung đăng nhập. Tự ẩn trên màn 4:3 vì khung nằm giữa, không đủ chỗ.
   - Logo SAM của chủ dự án vẫn là màn mở đầu.
   - **Ảnh:** `QA/UI/entry/02_login_*.png`.
6. **Google/Facebook:**
   - Nút xám không bấm được, chữ "· chưa mở", dòng "Google / Facebook: chưa khả dụng".
   - Server vẫn từ chối (không có đăng nhập giả).
   - Lưu ý: nút Facebook trên ảnh vẫn hơi xanh vì skin nút đè màu disabled. Không bấm được (có test), nhưng nên chỉnh màu cho rõ hơn.
7. **Nhạc nền (BẢN NHÁP, chờ chủ dự án nghe duyệt):**
   - `MusicComposer.cs` sáng tác và tổng hợp 6 vòng lặp hoàn toàn bằng code: không sample, không giai điệu của tác phẩm khác; vòng hợp âm + motif sinh có seed, harp/chuông FM/marimba/sáo/dây/trống tổng hợp, reverb.
   - Các bài: Đăng nhập (Rê trưởng, 84 bpm), Làng ngày (Sol trưởng, 108), Làng đêm (Mi thứ, hộp nhạc, 72), Đồng cỏ (La mixolydian, 124), Boss (Đô thứ hòa âm, 150), Lễ cưới (Fa trưởng, valse 3/4, 96).
   - File `Assets/Audio/BGM/*_DRAFT.wav` (32 kHz stereo, 25–53 giây, streaming Vorbis).
   - **Đã gắn:**
     - đăng nhập/chọn nhân vật dùng bài Đăng nhập;
     - sảnh đổi ngày↔đêm theo giờ VN và chế độ hiển thị, chuyển mượt;
     - Đồng Cỏ Gió dùng bài Đồng cỏ.
   - Boss và Lễ cưới **đã có bài nhưng chưa có map nào phát** (chưa có map Boss/khu cưới).
   - **Cài đặt:** Âm thanh (tắt hết), **Nhạc nền bật/tắt riêng**, **Âm lượng nhạc** và **Âm lượng hiệu ứng** riêng (bước 25 %).
   - **Đo kỹ thuật:** RMS −13,7…−15,6 dB, đỉnh −2 dB, chỗ nối vòng nhỏ hơn bước nhảy lớn nhất trong bài (không có tiếng "tạch" khi lặp).
   - **Test:** nhạc làng thực sự đang phát (`isPlaying`, không mute, volume > 0), tắt nhạc không ảnh hưởng hiệu ứng, đêm thì chuyển bài đêm, farm thì phát bài Đồng cỏ, màn đăng nhập phát bài Đăng nhập.
   - ⚠ **Claude không nghe được bằng tai.** Chất lượng âm nhạc phải do chủ dự án nghe trong Unity rồi duyệt; đây không phải nhạc thương mại đã duyệt.
   - 6 WAV nháp của chủ dự án (`AKOriginalDrafts/`) giữ nguyên, chưa gắn.
8. **Trang phục chồng lớp:**
   - Đã kiểm tra: `HeroOutfit.Apply` dọn toàn bộ mảnh cũ trước khi dựng mảnh mới, skin vào đúng xương/bindpose của thân. `EquipmentManager` không gắn ở đâu.
   - Test bổ sung trong `Every_Catalog_Item_Builds_On_Every_Class` (21 món × 4 nghề):
     - không còn mảnh của bộ trước;
     - mesh thân vẫn còn;
     - không có `EquipmentManager`.
   - Trạng thái Idle/Run/Jump/Climb/Attack/Skill vẫn do test contact sheet cũ kiểm (`QA/Shop/states_*.png`).

### Kiểm thử
- Server 109/109.
- EditMode 4/4.
- **PlayMode toàn bộ 44 (42 qua + 2 bỏ qua), 0 lỗi, 0 exception.**
- UI audit ở 1920×1080, 1366×768, 16:9, 19.5:9 và 4:3: sạch.

### Còn tồn / chưa làm (ghi thật)
- Trong 1–3 ảnh audit đầu tiên của `UiTownScreensTests`, chữ dưới các icon chưa mở (Sự kiện, Nạp đầu…) bị xám/nhòe. Các ảnh sau trong cùng lượt chạy (ví dụ `07_portal_confirm`) thì sạch. **Chưa xác định nguyên nhân.**
  - Đã thử ép làm mới chữ khi atlas font dựng lại (`FitOneLine`): không hết lỗi nhưng vô hại.
  - Cần điều tra tiếp.
- Trạng thái thu gọn icon lưu trong PlayerPrefs dùng chung với Editor. Test giờ tự đặt về "mở" trước khi chụp.
- Icon skill PNG mẫu của chủ dự án (`Assets/Art/UI/SkillButtons/ExtractedConcept/`, `README_SKILL_ART.md`) **chưa commit, chưa dùng**. Đó là việc Giai đoạn B (nút skill thật).
- HUD theo ảnh mẫu còn thiếu: bảng nhiệm vụ, chat, thanh nút dưới, nút skill/Auto/thuốc/Nhặt, Lực Chiến. Tất cả thuộc Giai đoạn B/C.

### Cần chủ dự án quyết định
1. Duyệt hay đề nghị sửa **logo đề xuất**.
2. **Nghe 6 bài nhạc nháp** và duyệt/yêu cầu sửa (`Assets/Audio/BGM/`).
3. Có cần art icon/skill vẽ tay thương mại (tốn tiền/credit) hay giữ art tự vẽ bằng code.
4. Giá shop, Vàng khởi đầu, thưởng điểm danh: vẫn chờ duyệt như trước.

---
## ★★★★★★★★★★ Hoàn thiện Đợt 5 + rà soát gói 17H (2026-11-10)
**Trạng thái:** đã test, commit (xem `git log`). Chưa sang Pet, chống macro, xã hội. Meshy vẫn 931.

### Đợt 5 — phần còn tồn đọng (đã làm + kiểm thử)
1. **Quái đánh trả, HP người chơi do server quyết định** (`FarmService`):
   - Quái bị đánh sẽ bám mục tiêu trong phạm vi xích 3,5 m quanh điểm sinh.
   - Server tự tính các đòn đánh theo đồng hồ server: `attack - phòng thủ/2`, mỗi 1,6–1,8 giây, dựa trên vị trí cuối cùng người chơi báo về. **Im lặng không né được đòn.**
   - Hồi máu ngoài giao tranh (sau 6 giây, 3 %/giây).
   - Gục ngã: không đánh/nhặt được, màn hình "Bạn đã gục ngã!". `POST /farm/revive` hồi sinh tại cổng làng với đầy máu, không mất Vàng hay đồ.
   - Heartbeat vị trí `POST /farm/presence` 2 lần/giây, có kiểm tra tốc độ.
2. **Quái 3D trong Play Mode thật:**
   - Slime nhảy lại gần (trong phạm vi xích), lao vào khi server báo trúng đòn.
   - Thanh máu và **tên quái phía trên thanh HP**. Trước đó tên không hiện vì ô chữ thấp hơn chiều cao dòng.
   - Ảnh `QA/Farm/06_slime_fights_back.png`, `07_knocked_out.png`, `08_revived.png`.
3. **Vàng:**
   - Bay ra theo vòng cung, chạm đất nảy 2 lần, có lấp lánh (quad additive).
   - Chồng xu to 1,7 lần.
   - **Tiếng "ting" nhặt vàng** tự tổng hợp bằng code (2 nốt E6–B6, file gốc, không lấy từ game khác).
   - Chữ **+N Vàng** cỡ 44, bám theo phía trên nhân vật.
   - Ảnh `03a_gold_flying.png`, `03_gold_on_ground.png`, `04_picked_up.png`.
4. **Đã kiểm tra lại:**
   - Hồi sinh 60 giây (server test: 59 giây vẫn chết; PlayMode: slime tự sống lại).
   - Chống nhận trùng (8 yêu cầu đồng thời chỉ trả 1 lần).
   - Chống gian lận (dịch chuyển, ngoài tầm, đánh quá nhanh, nhặt của người khác, đánh khi đã gục).

### Gói 17H — đối chiếu từng mục
| Mục | Trạng thái |
|---|---|
| `EquipmentManager.cs` | Đã biên dịch, **không gắn vào nhân vật**. `HeroOutfit` vẫn là đường mặc đồ duy nhất (đã dọn mảnh cũ trước khi dựng mảnh mới, skin vào xương thân, kiểm 4 nghề × 6 trạng thái). Gắn song song sẽ vẽ trùng slot, đúng như cảnh báo trong gói. Chỉ dùng khi có prefab áo rời xuất từ đúng rig. |
| Điểm danh 7 ngày | **Đã làm thật:** server `CheckinService` (DB V4) và popup (6 ô 3×2 + ô ngày 7 lớn, có chọn quà), chấm đỏ chỉ khi server báo nhận được, chống bấm trùng. Thưởng là **DRAFT**: `config/welfare.json` giữ `enabled=false` (server từ chối bật bản draft); test dùng `welfare.dev.json`. **Cần chủ dự án duyệt mức quà mới bật.** |
| Mũi tên thu gọn/mở rộng icon | Đã làm, chạy trên cả PC và mobile, lưu trạng thái, không che minimap. |
| Khung tên trên đầu | Hiện **tên thật** từ server. Khung VIP/Boss Hunter **chưa hiện**: chưa có dữ liệu sở hữu khung trên server, không làm giả "VIP 5". PNG trong `ArtSource/OwnerProvided/Nameplates_2026-11-08/` chưa nhập vì cần QA mép và độ phân giải. |
| Sự kiện / Phúc lợi / Nạp đầu / Thư / Giftcode / BXH | **Chưa làm.** Các nút vẫn báo thật "chưa được mở". Nạp đầu khóa vì thanh toán đang tắt. |
| Âm thanh BGM | 6 BGM nháp trong `Assets/Audio/AKOriginalDrafts/` **chưa gắn vào game**: gói ghi là bản nháp chưa duyệt. Đã có tiếng nhặt vàng tự tạo. |
| HUD theo ảnh mẫu | Đã có: cụm trái, icon phải, minimap, mũi tên. Chưa có: khung chat có tab, thanh nút dưới (túi đồ/nhân vật/pet/shop/bang/kết hôn/kỹ năng), nút skill + Auto, bảng nhiệm vụ, Lực Chiến. |

### Giá shop + Vàng khởi đầu — VẪN LÀ ĐỀ XUẤT (chủ dự án xác nhận 2026-11-10)
- Chủ dự án xác nhận chưa duyệt: lần bấm "Approve, start Đợt 5" không phải ý duyệt chính thức.
- Đã trả về trạng thái **đề xuất**:
  - `cosmetics.json` ghi "PROPOSALS pending owner approval";
  - Vàng khởi đầu trở lại **5.000** (mức trước đó, cũng chỉ là đề xuất; 800 vẫn là phương án đề xuất trong tài liệu kinh tế);
  - tài liệu `COSMETICS_SHOP_GOLD_DIAMONDS.md` ghi "chưa duyệt".
- Test `ProposedEconomyTests` ghim giá trị đề xuất hiện tại (muốn đổi phải sửa có chủ đích) và kiểm tra config không ghi "APPROVED".

### Lưu ý commit
Commit `6866036` đã vô tình gom luôn các file gói 17H mà chủ dự án đặt vào repo: tài liệu, ảnh tham khảo, `EquipmentManager.cs`, WAV nháp. Đã đọc lại: không có khóa/bí mật.

---
## ★★★★★★★★★ Đợt 5 (2026-11-09 đêm): quái + vàng rơi do server quản lý; bảng giá & Vàng khởi đầu đã duyệt
**Trạng thái:** đã test, đã commit, **chưa push**. Meshy vẫn 931.

**[ĐÃ THU HỒI 2026-11-10 — xem mục ★★★★★★★★★★]** Lúc này giá và Vàng khởi đầu 800 từng được ghi là "đã duyệt". Chủ dự án sau đó xác nhận **chưa duyệt**: giá đã trả về "đề xuất", Vàng khởi đầu về 5.000.
- Test PlayMode cần mua nhiều món (`ShopFashionTests`, `UiTownScreensTests`) truyền rõ fixture 5.000 qua `TestServer(port, starterGold)`. Test HTTP mua chuông 600 thay vì giày 900 (800 không đủ mua giày, đúng hành vi).

**Server (authoritative) — `Domain/FarmService.cs`, `config/monsters.json`, endpoint `/characters/{id}/farm`, `/farm/attack`, `/farm/pickup`:**
- **Quái:** 8 slime ở Đồng Cỏ Gió: Slime Bông 60 HP, 5–11 Vàng; Slime Rêu 90 HP, 7–13 Vàng.
  - Server giữ HP, cái chết và **hồi sinh sau đúng 60 giây** theo đồng hồ server.
  - Sát thương lấy từ bảng nghề trên server; client không gửi sát thương.
- **Kiểm tra mỗi đòn đánh:**
  - nhân vật thuộc phiên đăng nhập và đang ở đúng bản đồ;
  - vị trí báo về đi được bằng tốc độ chạy (≤ 12 m/s + 2,5 m sai số);
  - mục tiêu trong tầm đánh của nghề;
  - đúng nhịp tấn công của nghề.
- **Vàng rơi:**
  - server tung số vàng, tạo túi vàng thuộc **riêng** người hạ quái, tồn tại 120 giây;
  - nhặt phải đứng gần (≤ 1,8 m);
  - mỗi túi chỉ trả **đúng một lần** (cờ đã nhặt + khoá idempotency `loot:{dropId}` trong ledger), kể cả khi 8 yêu cầu đến cùng lúc.
- Không giới hạn theo ngày, không giảm thưởng.
- Sảnh không có quái, config chặn đặt quái ở sảnh; không có đường gây sát thương người chơi khác (không PK).

**Client:**
- `FarmField`: hiển thị quái từ server, đòn chém → yêu cầu đánh, tự nhặt túi vàng khi đứng lên, HUD hiện số dư server xác nhận, hỏi lại server mỗi 1,5 giây để thấy hồi sinh/hết hạn.
- `FarmMonsterView`: slime chibi dựng trong engine, nảy/nhảy, thanh máu, chớp khi trúng đòn, xẹp khi chết, bật ra khi hồi sinh.
- `GoldDropView`: chồng xu vàng bật ra, xoay, nhấp nháy trước khi hết hạn, bay vào nhân vật khi nhặt.
- Chữ nổi: "-18", "+N Vàng".

**Kiểm thử:**
- Server **100/100**, gồm 7 test farm:
  - hạ quái → 4 đòn với 60 HP;
  - hồi sinh đúng 60 giây (59 giây vẫn chết);
  - nhặt đúng một lần;
  - nhặt đồng thời;
  - giả vị trí / ngoài tầm / đánh quá nhanh bị từ chối;
  - sảnh không có quái.
- PlayMode **42** (40 qua + 2 bỏ qua), 0 exception, gồm `FarmTests` end-to-end trên server thật: vào đồng cỏ → chém bằng phím tấn công → server báo chết → túi vàng → đi lên nhặt → ví server +N đúng một lần, HUD 800→800+N → slime tự hồi sinh sau 60 giây.
- Ảnh trong `QA/Farm/`.

**Còn lại / hạn chế:**
- Di chuyển vẫn do client (vật lý platformer). Server chỉ kiểm tra tốc độ hợp lý, nên cheat dịch chuyển ngắn trong giới hạn tốc độ vẫn có thể.
- Quái chưa đánh người chơi (slime hiền), HP người chơi chưa do server quản lý.
- Trạng thái quái và túi vàng nằm trong bộ nhớ server: restart server thì quái hồi đầy, túi chưa nhặt mất. Vàng đã nhặt nằm an toàn trong DB.
- Chưa có kênh / nhiều server.
- Tên quái trên thanh máu chưa hiện (chỉ có thanh máu).
- Chưa đo thu nhập thật theo giờ trong game.

---
## ★★★★★★★★ Đợt 4 (2026-11-09 đêm): skin vui nhộn khác dáng thật, giá đề xuất — dựng trong engine, 0 credit Meshy
**Trạng thái:** đã test, đã commit, **chưa push**. Chủ dự án chọn "chỉ dựng trong engine". **Meshy vẫn 931.**

**Đã làm:**
- File mới `Assets/Editor/ArtPipeline/OutfitThemes.cs` (partial của `OutfitArtPipeline`) dựng 10 món mới cho cả 4 nghề. Mỗi món đổi **dáng**, không chỉ đổi màu.
- **Vàng (7 món):**
  - Vịt Con Đội Đầu: chú vịt ngồi trên đỉnh đầu, kể cả chóp mũ Pháp Sư.
  - Bờm Ếch Mắt Lồi.
  - Bờm Tai Thỏ: tai có khớp riêng, nhún theo bước chạy.
  - Bờm Tai Mèo Cam (có chuông).
  - Bờm Tai Gấu Trúc (nhánh trúc).
  - Nấm Đỏ Chấm Bi.
  - Mũ Đầu Bếp.
- **Kim Cương (3 bộ):**
  - Khủng Long Con: gai đầu, áo vảy, đuôi gai biết vẫy.
  - Cá Mập Nhỏ: vây đầu + răng, áo xám bụng trắng, đuôi chẻ.
  - Giấc Mơ Ngọt Ngào: mũ ngủ rủ quả bông, áo flannel in sao.
- **Lưu ý thiết kế:** mũ của từng nghề nằm trong mesh thân (mũ phù thuỷ, mũ cung thủ), nên món đầu dùng bờm theo vòng đầu hoặc đậu lên đỉnh đầu, không trùm đầu (trùm sẽ thành khối khổng lồ trên mũ Pháp Sư).
- **Server:** `cosmetics.json` lên v2, 21 món. Server tự chặn sai chính sách (món lẻ = Vàng, bộ = KC). Giữ nguyên ID cũ.
- **Giá:** tất cả là **ĐỀ XUẤT chờ duyệt**. Mô phỏng thu nhập + đề xuất Vàng khởi đầu 800 (hiện 5.000, **chưa đổi**) nằm trong `Docs/COSMETICS_SHOP_GOLD_DIAMONDS.md`, mục cuối.
- **Test:**
  - Mới: `Every_Catalog_Item_Builds_On_Every_Class` — mọi món trong catalog server đều có art 3D thật, dựng và skin đúng xương trên cả 4 nghề.
  - Test trạng thái (Idle / Run / Jump / Climb / Attack / Skill) thêm 6 bộ phối mới; ảnh trong `QA/Shop/states_fun_*.png` và `states_set_*.png`.
  - Một assert kiểm tra bounds đã bị gỡ: bounds của SkinnedMeshRenderer tính từ xương nên không phản ánh vị trí món đồ (chính Vòng Hoa Sen cũ cũng báo 2,5 m). Vị trí được kiểm bằng ảnh.

**Kiểm thử:**
- Server 83/83, EditMode 4/4, PlayMode **41** (39 qua + 2 bỏ qua), 0 exception.
- UI audit 0 lỗi.
- Đã xem bảng ảnh trạng thái của các bộ dino, shark, dream, duck, bunny trên 4 nghề.
- Đã sửa sau khi xem ảnh:
  - tai thỏ/gấu nổi trên mũ;
  - mắt ếch, tai mèo, gai, vây quá nhỏ;
  - áo cá mập thành sọc rách (texture lặp 3 lần quanh thân);
  - viền vàng áo dino nhìn răng cưa;
  - mũ ngủ quá nhỏ.

**Còn lại / hạn chế:**
- Chưa có skin vũ khí: slot "Vũ khí" có bộ lọc nhưng chưa có món.
- Chưa có tóc rời.
- Viền cổ/gấu áo dạng "shell" hơi răng cưa ở vài góc.
- Icon bộ KC chụp cả người nên chi tiết nhỏ.
- Món mới nằm ở phía dưới lưới, phải cuộn mới thấy.
- Thu nhập Vàng mới là mô hình, Đợt 5 sẽ đo thật.
- **Chủ dự án cần duyệt:** bảng giá Vàng/KC, Vàng khởi đầu (đề xuất 800).

---
## ★★★★★★★ Đợt 3 (2026-11-09 tối): hoàn thiện thẩm mỹ + UX shop (giữ nguyên chức năng thật)
**Trạng thái:** đã test, đã commit, **chưa push**.

**Đã làm:**
- **Sân khấu thử đồ:**
  - Bục đá mặt trăng màu oải hương nhiều tầng: viền vàng, vòng ngọc bích.
  - Đèn spotlight ấm "thở" sau nhân vật, hạt sáng vàng bay lên (`ShopStageFx`, chỉ dùng UI, texture tạo lúc chạy).
- **Icon món đồ chụp đúng món:** áo, váy chụp theo mảnh đồ (trước đây cả nhân vật tí hon); găng chụp một tay (`OutfitIcons`).
- **Thẻ món:** huy hiệu góc "Đang mặc" (ngọc bích) hoặc "Đã có" (đá xám).
- **Khung chi tiết** gọn hơn (196), để lưới hiện gần 2 hàng trên điện thoại 16:9.
- **Trạng thái trống:** hiện hướng dẫn và biểu tượng tiền tệ của tab.
- **Chức năng giữ nguyên:**
  - mặc thử / bỏ thử / mua / trang bị / tháo / lưu bộ phối;
  - giá và sở hữu do server quyết định;
  - đăng nhập lại vẫn còn (ShopFashionTests 2/2).

**Kiểm thử:**
- Server 83/83, EditMode 4/4, PlayMode 38 + 2 bỏ qua, 0 exception.
- UI audit 0 lỗi ở 5 độ phân giải.
- Đã xem ảnh shop (PC, 4:3, 16:9, 19.5:9).

**Còn lại:**
- Icon găng vẫn lộ một phần thân (mesh 2 tay chung một mảnh).
- Chưa có nhiều món để lấp lưới (Đợt 4: skin khác biệt + giá, cần chủ dự án duyệt).
- Hạt sáng khá nhẹ trên nền trời sáng.

---
## ★★★★★★ Đợt 2 (2026-11-09 tối): ngày/đêm theo giờ VN, chiều sâu sảnh, minimap
**Trạng thái:** đã test, đã commit, **chưa push**.

**Đã làm:**
- **Ngày/đêm theo giờ Việt Nam** (`DayNightCycle.cs`, UTC+7 lấy từ đồng hồ thiết bị; chỉ để hiển thị):
  - Hoàng hôn 17:30–18:45, bình minh 05:00–06:15, chuyển mượt.
  - Điều khiển: mặt trời → ánh trăng, ambient SH, sương mù, màu trời, các tranh cutout (biến global `_AKNight` trong `AK_Cutout.shader`, mặc định 0 nên không ảnh hưởng cảnh khác), lớp sao + trăng (vẽ thủ tục), đèn lồng.
  - Cài đặt có nút "Thời gian: Theo giờ VN / Luôn ngày / Luôn đêm" (lưu cục bộ, chỉ là tuỳ chọn hiển thị).
- **Chiều sâu sảnh:** thêm 6 đám mây toon vẽ thủ tục, trôi chậm theo gió, parallax giữa tranh nền và thị trấn, tối đi vào ban đêm. Cổng dịch chuyển giữ nguyên (đã rõ, to).
- **Minimap:** dải tên map, giờ VN kèm biểu tượng mặt trời/trăng, ký hiệu cổng (xoáy), NPC (bong bóng "!"), người chơi (hình thoi đỏ nhấp nháy) thay cho chấm tròn.
- **Biển tên NPC** mờ đi khi nằm dưới cụm HUD góc (`NamePlateFade`).
- **Test ổn định:** `DeterministicClock` ghim mọi PlayMode test ở 12:00, ban ngày, nên ảnh/kiểm thử không phụ thuộc giờ thật.
  - TownHubTests kiểm tra thật: 21:00 = đêm, mặt trời dịu, tranh tối, đèn sáng gấp 3 lần trở lên; hoàng hôn là trạng thái pha trộn; trở về ngày.
  - Teardown luôn trả về ban ngày, kể cả khi test lỗi giữa chừng.

**Kiểm thử:**
- Server 83/83, EditMode 4/4, PlayMode 38 qua + 2 bỏ qua, 0 exception.
- UI audit 0 lỗi (login và thị trấn, 5 độ phân giải).
- Đã xem ảnh: đêm (`QA/HubTest/01c_spawn_night_lanterns.png`), mây (`03_wind_lookout.png`), cài đặt và minimap.

**Còn lại:**
- Mây có đáy phẳng (theo phong cách toon), có thể tinh chỉnh thêm.
- Sao/trăng chỉ thấy khi khung hình có trời.
- Chưa có thời tiết (mưa…), theo đúng yêu cầu "chưa cần".
- Joystick, nút skill và auto skill cho mobile chưa làm lại.

---
## ★★★★★ 2026-11-09 chiều: hệ thống khung UI riêng từng nhóm + HUD + shop
**Trạng thái:** đã test, đã commit (xem `git log`), **chưa push** (chủ dự án push bằng GitHub Desktop). **Meshy: 0 credit** (vẫn 931). Không mua tài nguyên, không bật thanh toán.

### Đã làm (code + kiểm thử thật)
- **Bỏ khung vàng lặp lại.** File mới `Assets/Editor/ArtPipeline/FrameArt.cs` vẽ thủ tục (không ảnh ngoài), xuất 9-slice vào `Assets/Art/UI/Skin/`. Mỗi nhóm có một khung riêng:
  - **Đăng nhập** `auth_card`: kính đêm, viền chỉ vàng đôi, góc lá.
  - **Nút đá quý**: vàng (chính), ngọc bích (Đăng ký/xác nhận), xanh hoàng gia, đá xám, đỏ (Lưu & Thoát / đóng), trắng (Google), xanh dương (Facebook), ghost (link).
  - **Ô nhập** `field`; **sảnh chọn nhân vật** `hall_panel` (tím, ke góc bạc) + `hall_card`.
  - **HUD**: `hud_plate` (đuôi vát, mép vàng), `bar_track` + `bar_hp` (đỏ bóng) + `bar_mp` (xanh bóng).
  - **Ví**: `purse_gold` (đồng/coin) và `purse_gem` (pha lê, khác hẳn nhau).
  - `minimap_frame` (viền đồng, đinh tán); **hội thoại NPC** `parchment` (giấy da, chữ mực nâu) + `ribbon` (tên người nói / tên map).
  - `popup_card` (dải tiêu đề), `settings_card` (thép xước, đinh bạc).
  - **Shop**: `shop_window` (nhung tím, hoa góc), `shop_tab(_on)`, `chip(_on)`, `item_cell` (`_on` = viền vàng khi chọn, `_set` = viền tím cho bộ set), `stage_bezel`.
- **Nút trong builder và shop tự chọn đúng loại đá quý theo vai trò** (`UiKit.SkinFor`), sửa ở nguồn sinh UI (`EntrySceneBuilder`, `VillageSceneBuilder`, `FashionShopUI`, `FashionMerchant`).
- **HUD trái là một khối:**
  - avatar, tấm kính gồm tên, Lv, HP, MP;
  - ví Vàng và Kim Cương gắn ngay dưới;
  - số liệu thật (Character / Health2D / PlayerMana).
- **Lỗi thật đã sửa trong shop:** hàng chip lọc cao 154 thay vì 54, do RectTransform giữ sizeDelta mặc định, nên các chip bị cắt thành hình chữ nhật. Chip nay cao 62 (đủ 40 px ở 720p).
- **UiAudit nay kiểm tra cả các khung mới:** độ dày hoa văn của từng khung nằm trong `SkinOrnament`. Trước đây khung không tên "frame" bị bỏ qua.
- **Khi suite chỉ tìm thấy 4 test PlayMode:** nguyên nhân là `Library/ScriptMapper` hỏng sau một lần chạy bị ngắt. Cách khắc phục: xóa `Library/ScriptAssemblies` và `Library/ScriptMapper` (Unity tự tạo lại).

### Kiểm thử
- **Server:** 83/83.
- **EditMode:** 4/4.
- **PlayMode:** 40 test (38 qua, 2 bỏ qua có chủ đích), 0 exception, gồm:
  - ServerEndToEnd (đăng nhập → vào map → lưu → khởi động lại server → giữ tiến độ);
  - FashionShop;
  - UiScreens và UiTownScreens.
- **UI audit:** 0 lỗi ở 1920×1080, 1366×768, 19.5:9, 16:9 và 4:3 (có safe area).
  - Màn đăng nhập (`QA/UI/entry/`): splash, đăng nhập, đăng ký, lỗi đăng ký, sảnh tạo nhân vật.
  - Màn trong thị trấn (`QA/UI/town/`): hội thoại Trưởng Làng, HUD, cài đặt, popup phím tắt, menu thương nhân, shop, xác nhận mua, tủ đồ, xác nhận cổng.
- Đã xem ảnh bằng mắt: login, sảnh, HUD, hội thoại, cài đặt, shop (PC + điện thoại).

### Còn lại / hạn chế (thật)
- **Avatar:** chọn ảnh từ album, cắt ảnh, lưu server và kiểm duyệt đều **chưa làm**. Hiện dùng chân dung 3D theo nghề.
- **MP** là giá trị đề xuất; chưa có kỹ năng tiêu MP.
- Các phím tắt (sự kiện, thư…) **chưa có backend**: chúng báo thật "chưa được mở", không giả. Quà Nạp Đầu khóa.
- Thẻ nghề đang chọn ở sảnh chưa nổi bật. Biển tên NPC trong thế giới có thể đè lên icon HUD khi NPC đứng dưới góc phải.
- Chưa có: đổi sáng/tối theo giờ VN, joystick/nút skill/auto skill đẹp cho mobile, skin mới, kinh tế, quái/rơi vàng server, pet, xã hội.
- Chưa test trên thiết bị Android thật; chưa đo FPS trên điện thoại.

### Bước tiếp theo (theo lệnh tổng hợp)
- **Đợt 2:** sảnh, parallax, đổi sáng/tối theo giờ VN, minimap.
- **Đợt 3:** hoàn thiện UX shop.
- **Đợt 4:** skin khác biệt và giá Vàng/KC; giá cần chủ dự án duyệt.
- **Đợt 5:** quái, rơi vàng do server quản lý.
- **Đợt 6:** pet, chống macro, xã hội.

---
## ★★★★ Lệnh tổng hợp 2026-11-09: Đợt 1 (UI + nền đăng nhập)
**Trạng thái:** Đợt 1 xong, đã test, đã commit (xem `git log`). Đợt 2 (HUD) và Đợt 3 (Shop) đang làm. **Meshy: 0 credit** (931).

**Nguyên nhân gốc lỗi chữ đè hoa văn:** `Assets/Art/UI/Fantasy/frame_panel.png` được import ở chế độ sprite *Multiple* nên sprite Unity dùng (`frame_panel_0`) có viền 9-slice = 0 → mọi khung (đăng nhập, sảnh, hội thoại NPC, xác nhận cổng, shop) bị vẽ như MỘT ảnh kéo giãn: hoa văn phình theo kích thước panel và đè lên chữ. Sửa trong mã sinh (`EntrySceneBuilder.FantasySprite` → Single).

**Giải pháp dùng chung** (`Assets/Editor/ArtPipeline/EntryLayoutKit.cs`): đo viền khung Unity thực vẽ (`DrawnBorder` từ mesh 9-slice), chỉnh độ dày viền (`SetBand`), vùng nội dung bên trong hoa văn (`FramedContent`), xếp bằng Layout Group (`VStack/HRow/Size`), nhãn nút một dòng tự co (`FitOneLine`), chữ trong ô nhập cách viền, panel cách mép ≥ 100 đơn vị (an toàn tai thỏ).

**Nền đăng nhập mới** (`LoginBackdropBaker.cs` → `Assets/Art/UI/LoginBackdrop/`): Làng Chuông Gió lúc bình minh (cầu đá, đa cổ treo đèn, tháp chuông, rừng nấm phát sáng) render từ chính thế giới game 2816×1584 + trời/đồi thủ tục — không ảnh phóng to, không tài nguyên ngoài. `LoginBackdrop.cs`: 4 lớp parallax trôi chậm (chu kỳ 22 s, ≤ 3% màn hình), sương, tia nắng, đom đóm; đồ họa "Thấp" → tĩnh, tắt hạt. Nền này cũng nằm sau sảnh chọn nhân vật (camera sân khấu nền trong suốt) và màn hình tải.

**Kiểm tra UI tự động** (`Assets/Tests/PlayMode/UiAudit.cs`): render từng màn ở 1920×1080, 1366×768, điện thoại 19.5:9 / 16:9, tablet 4:3; đo glyph thật: chữ đè chữ, chữ bị cắt/tràn, chữ trên viền hoa văn, chữ/nút ngoài safe area, nút < 40 px, nút bị che, chữ bị panel che; bỏ qua nền phía sau hộp thoại modal. `UiScreensTests`: (1) đối chứng âm — dựng lại đúng lỗi chủ dự án chụp, audit phải bắt được; (2) Splash, Đăng nhập, Đăng ký, lỗi đăng ký thật từ server, Sảnh tạo nhân vật — **0 lỗi ở cả 5 độ phân giải**. Ảnh: `QA/UI/entry/*.png`, báo cáo `QA/UI/entry/report.txt`, nền `QA/UI/login_bg_composite.png`.

**Kết quả test Đợt 1:** server 83/83, EditMode 4/4, PlayMode 37 passed / 0 failed / 2 skipped, 0 exception. Test audit trong làng (`UiTownScreensTests`) chưa đưa vào Đợt 1 vì HUD/Shop cũ còn lỗi — sẽ vào cùng Đợt 2/3 (không đổi thứ tự test để che lỗi).

---
## ★★★ Giai đoạn B/C: Shop thời trang 3D, tủ đồ server, NPC bán hàng (2026-11-08 tối)
**Commit:** `0b9b539` (server) + commit client/art tiếp theo (xem `git log`), nối tiếp `b073e14`. **Meshy: 0 credit** (số dư vẫn 931). Không mua tài nguyên, không bật thanh toán thật, không đổi giá gói Kim Cương.

### Đã hoàn thành (có kiểm thử thật)
- **Server (tủ đồ + giao dịch):** schema V3 `owned_items` (kind: cosmetic / cosmetic_set / pet / pet_food, quantity — sẵn cho Pet sau này) + `shop_purchases` (lịch sử, `UNIQUE(account, request_id)`). Danh mục `Server/.../config/cosmetics.json` (data-driven, có version; server từ chối catalog sai: món lẻ bắt buộc Gold, skin nguyên bộ bắt buộc Kim Cương, giá > 0). API: `GET /characters/{id}/wardrobe`, `POST .../wardrobe/purchase` {itemId, requestId, equip}, `POST .../wardrobe/equip` {set, slots[]}. Mua = 1 transaction IMMEDIATE: kiểm tra tài khoản sở hữu nhân vật → món còn bán, đúng nghề, chưa sở hữu → giá server → trừ qua `Wallet` (ledger, idempotency key) → cấp đồ → ghi lịch sử → (mặc). Gửi lại cùng requestId (mất mạng/timeout) trả kết quả cũ, không trừ lần 2. Đồ Gold thuộc nhân vật; skin Kim Cương thuộc tài khoản. Trang phục đang mặc lưu trong `characters.appearance_json`, trả về trong `/enter` và `/profile` (`outfit`).
- **Vàng khởi đầu 5.000/nhân vật** (cấp 1 lần bằng ledger key `starter_gold:<id>`, cả nhân vật cũ khi vào game) để có thể mua thử — **đề xuất, chờ chủ dự án duyệt** (`AdventureKing:StarterGold`).
- **11 món thời trang 3D thật, khớp skeleton của cả 4 nghề** (`Assets/Editor/ArtPipeline/OutfitArtPipeline.cs`, chạy lại được: menu *AdventureKing/Art/Rebuild Outfits*): Vòng Hoa Sen, Vòng Đầu Chuông Gió (chuông đung đưa), Áo Gấm Hoa Văn Mây, Váy Gió Xếp Ly, Găng Lụa Ngọc, Giày Mây Trắng, Ủng Than Hồng, Chuông Bạc Đeo Hông, Áo Choàng Gió, Cánh Chuồn Chuồn (Gold) + **skin nguyên bộ "Thiên Phong Pha Lê"** (Kim Cương: cánh pha lê, vương miện pha lê, áo choàng sao, hào quang hạt sáng; chỉ ngoại hình).
  - Áo/găng/giày: mesh riêng lấy vùng cơ thể đẩy ra theo pháp tuyến, **giữ trọng số skin của chính nhân vật**, UV + texture hoa văn mới, viền cổ/cổ tay/cổ ủng riêng.
  - Mũ/váy/áo choàng/cánh/chuông: mesh dựng theo số đo từng nhân vật (tiết diện đầu/vành mũ, độ sâu lưng gồm tóc tết và ống tên, vòng eo/đùi). Áo choàng + chuông có khớp phụ, rủ theo trọng lực, bay ra sau khi chạy/rơi; cánh vỗ chậm khi đứng, nhanh khi nhảy (`HeroOutfit.cs`).
  - Không tạo lại nhân vật: dùng chung rig/animation; mặt (biểu cảm, chớp mắt) và ngón tay giữ nguyên.
- **NPC "Chủ Tiệm Lụa"** (model Thủ Kho có sẵn, chuyển xuống quầy Chợ Phiên x = −30): đứng Idle, quay về phía người chơi, vẫy tay, nói, cúi chào khi mua xong. Lại gần mới hiện "E · Nói chuyện" (+ nút cảm ứng "Nói chuyện"); chào **đúng tên nhân vật**; menu Mua sắm / Xem tủ đồ / Trò chuyện / Đóng — tất cả nối chức năng thật (`FashionMerchant.cs`).
- **Shop + Phòng thử đồ 3D** (`FashionShopUI.cs`): 3 tab Thời Trang · Vàng / Skin Đặc Biệt · KC / Tủ Đồ Của Tôi; lọc theo ô (Mũ, Áo, Quần/Váy, Găng, Giày, Phụ kiện, Choàng/Cánh) và theo nghề (của tôi / tất cả); icon render từ chính mesh 3D; giá + số dư từ server; Mặc thử / Bỏ thử / Mua (hộp xác nhận) / Mặc / Tháo / Lưu bộ phối. Phòng thử: đúng model nhân vật đang chơi, kéo chuột/ngón tay để xoay, Idle tự nhiên + chớp mắt, nút Đứng/Chạy/Nhảy/Đánh. Mặc thử không trừ tiền; đóng shop khi chưa mua → nhân vật thật không đổi; mua/mặc xong nhân vật ngoài làng đổi ngay theo kết quả server. Chống double-click (khoá nút + requestId), mất mạng tự gửi lại cùng requestId tối đa 3 lần.

### Kết quả kiểm thử
- Server: **83/83** (11 test mới: catalog hợp lệ, vàng khởi đầu đúng 1 lần, thử đồ miễn phí + mua đúng giá + mặc, thiếu Gold không trừ/không cấp, gửi lại requestId không trừ 2 lần, 8 luồng mua đồng thời → trừ 1 lần, kiểm tra quyền sở hữu/ô/nghề khi mặc, tài khoản khác không mua/mặc hộ được, skin KC chỉ trừ KC + dùng chung tài khoản + tháo về đồ Gold, khôi phục sau khởi động lại, HTTP end-to-end).
- PlayMode `FashionShopTests` (server thật): `Merchant_TryOn_Buy_Wear_Survive_Relog_And_Restart` **Passed**: đi tới NPC → prompt → chào đúng tên → shop → thử đồ (Gold không đổi, nhân vật thật không đổi) → mua có xác nhận, double-click chỉ gửi 1 request → mua liên tiếp 3 món → mua lại món đã có bị từ chối → thiếu Vàng (client + server từ chối, không trừ) → tab KC thiếu Kim Cương → tủ đồ tháo/mặc → Lưu & Thoát → **khởi động lại server** → đăng nhập → đúng trang phục, đúng Gold. `Outfits_Follow_Every_State_On_All_Classes` **Passed**: 4 nghề × 3 bộ × Idle/Run/Jump/Climb/Attack/Skill — mọi xương nối đúng, mũ luôn trên đầu, vũ khí luôn trong tay, mặt vẫn hoạt động.
- **Kết quả toàn bộ (chạy lại hết sau khi xong):** server **83/83**, EditMode **4/4**, PlayMode **35 passed / 0 failed / 2 skipped**, 0 exception, 0 lỗi biên dịch. Baseline đầu phiên: 72/72, 4/4, 33/0/2.
- Lưu ý kiểm thử: `MovementTests.Coyote_Jump_Allowed_Briefly_After_Leaving_Ledge` nhạy với thời gian khung hình (chờ 0,05 s, cửa sổ 0,1 s): khi chạy ngay sau một test tải cả thị trấn nó trượt; chạy riêng vẫn 11/11. Đã đặt tên lớp test shop là `ShopFashionTests` để chạy sau MovementTests như các test thị trấn khác — nên sửa test coyote dùng thời gian cố định thay vì thời gian thực (việc tiếp theo).
- Video Play Mode thật: `QA/Video/fashion_shop_playmode_2026-11-08.mp4` (đi tới NPC → chào → shop → thử đồ → mua → tủ đồ → đi ra làng với đồ mới).

### Ảnh kiểm chứng (Play Mode thật)
`QA/Shop/01_merchant_prompt.png` … `11_after_restart_relog.png` (prompt, chào hỏi, shop Gold, thử đồ khi chạy/nhảy, xác nhận mua, mua xong, thiếu Vàng, thử skin KC, tủ đồ PC + điện thoại 19.5:9, nhân vật mặc đồ ngoài làng, sau khởi động lại server); `QA/Shop/states_gold_A.png`, `states_gold_B.png`, `states_premium.png` (4 nghề × 6 trạng thái); `QA/Outfits/editor_<Nghề>.png` (Idle 4 hướng, Run, Jump, Climb, Attack, Skill).

### Chưa hoàn thành / hạn chế (ghi rõ)
- **Ô Tóc và Ngoại hình vũ khí:** hệ thống (server + client) hỗ trợ, nhưng **chưa có món nào** — đổi tóc/vũ khí đạt chuẩn cần mesh mới (Meshy hoặc dựng tay trong Blender); không bán món tạm.
- Áo/găng/giày là lớp áo bám theo hình cơ thể (đổi chất liệu + hoa văn + viền), chưa đổi được kiểu dáng lớn (tay áo dài, giáp vai…). Áo choàng ở góc nghiêng 110° trong làng trông mỏng; nhìn rõ trong phòng thử đồ.
- Skin "Thiên Phong Pha Lê" là bộ dựng bằng code (không phải model điêu khắc): **giá 300 KC chỉ là đề xuất**; chủ dự án cần xem ảnh `states_premium.png` và quyết định có mở bán không (đổi `listed` trong `cosmetics.json`, không cần sửa code). Hiện mọi người có 0 KC vì thanh toán thật chưa bật.
- Giá Gold trong `cosmetics.json` là **đề xuất**, chờ duyệt.
- Chưa kiểm thử trên điện thoại Android thật (chỉ ảnh khung 19.5:9 trong Editor). FPS phòng thử đồ trên điện thoại chưa đo.
- Pet: chỉ chuẩn bị dữ liệu (kind pet / pet_food + quantity trong `owned_items`); chưa có Pet, đồ ăn, độ no 60 phút, nhặt vàng — không có nút Pet giả.
- Icon dạng vòng (mũ) hơi mảnh ở cỡ nhỏ.

### Cần chủ dự án quyết định
1. Giá Gold từng món + số Vàng khởi đầu (đang 5.000). 2. Mở bán hay chưa skin KC "Thiên Phong Pha Lê" và giá. 3. Ngân sách Meshy cho tóc/vũ khí/skin điêu khắc cao cấp. 4. Thiết bị Android để test.

---
## ★★ Giai đoạn A, đợt 2 (nhân vật, animation, sảnh)
**Commit:** `ab47513` "Phase A2: hero faces, seamless loops, town polish" + commit tiếp theo "Rung-accurate ladder climb" (clip leo mới) trên `feat/diamond-store-catalog-20261107`, nối tiếp `85c770d`. **Meshy: 0 credit** (chỉ Unity + công cụ tự viết; số dư vẫn 931).

### Ưu tiên 1 — Biểu cảm khuôn mặt 4 nghề (đã làm, có kiểm chứng Play Mode)
- **Kiểm tra thực tế:** FBX Meshy **không có** xương mặt, xương ngón, blendshape; mắt/miệng chỉ là hình vẽ trên atlas texture bị chia mảnh ⇒ shape key hình học sẽ làm méo hình vẽ. **Không giả vờ có blendshape.**
- **Giải pháp (lớp mặt có kiểm soát, không dán ảnh):** `HeroFaceRig` dựng ảnh chính diện từ mesh bind-pose + texture của từng nhân vật, tự **đo** vị trí 2 mắt, miệng, màu da, màu mí, màu mi, màu môi (`QA/Faces/diag_all.png`). `HeroFaceImport` ghi toạ độ mặt vào kênh UV3 lúc import (FBX/rig/clip gốc không đổi). Shader `AdventureKing/Toon` (`_FACE`) **giữ nguyên mắt vẽ gốc** (nhận diện), vẽ **mí mắt thật** (trên + dưới, viền mi chạy theo mép mí), vẽ lại miệng (cười, mếu, há, run), má hồng, nước mắt. `HeroFace` (runtime) trộn biểu cảm + chớp mắt ngẫu nhiên 2.2–5.8 s, 14% chớp đôi, ~0.23 s/lần.
- Biểu cảm: Bình thường, Vui (mắt ^^, cười há, má hồng), Buồn (mí xệ đuôi, mếu, nước mắt), Ngạc nhiên (miệng O), Tức giận (mí xiên vào trong), Đau (mắt >< + miệng run) — tự bật khi trúng đòn (Health2D), giận khi đánh/skill. Chạy chồng lên Idle/Run/Jump/Attack/Skill (chỉ đổi 3 vector shader, không đụng Animator).
- **Bằng chứng:** `HeroFaceTests` (Play Mode, prefab thật, Idle chạy nền): `QA/Faces/play_sheet_full.png` (4 nghề × Idle/Blink/Happy/Sad/Surprised/Angry/Hit), ảnh lẻ `QA/Faces/play_<Nghề>_<Biểu cảm>.png`; kiểm tra số: chớp mắt đóng ≥97%, Vui mắt đóng thành vòng cung, Buồn miệng mếu, Idle vẫn chạy.
- **Hạn chế còn lại:** "Ngạc nhiên" chỉ đổi miệng (mắt vẽ không thể mở to hơn); lông mày không đổi (vẽ trên texture, có tóc che); ngón tay vẫn không có xương (cần rig lại ngón trong Blender — chưa làm).

### Ưu tiên 2 — Animation (đã đo, đã sửa nguyên nhân rung/giật)
- Công cụ mới: `MotionQualityAudit` (seam vòng lặp + rung tần số cao đầu/tay/vũ khí), `LoopSearch` (dò dải frame lặp thật trong take Meshy), test Play Mode `HeroMotionTests` đo trên Animator thật.
- **Nguyên nhân "rung lắc/giật về vị trí đầu" tìm được (trước sửa):** Run bị cắt 0–17 trong chu kỳ 21 frame ⇒ khớp chân nhảy **53.6°** mỗi vòng (≈3 lần/giây); Climb cắt 0–38/48 ⇒ **47°** + hông rơi **18–31 cm** mỗi vòng; Idle cắt ngắn ⇒ giật 3–5° mỗi 1.3 s. Rung trong clip rất nhỏ (đầu/tay ≤ 0.1 cm) ⇒ không phải camera.
- **Sửa:** Idle dùng trọn take (lặp 0.0°), Run 0–21, Climb 1–48, bật Loop Pose; Climb tạo bản **tại chỗ** (`InPlace/Climb.anim`, bỏ trôi hông theo chu kỳ — FBX không đổi); tốc độ phát Climb theo độ cao leo thật của clip × tỉ lệ nhân vật.
- **Sau sửa (Play Mode, Animator thật, 4 s × 4 nghề):** Idle hông ≤ 0.07 cm/frame (thở nhẹ, không lắc), Run không có frame nhảy (max ≤ 2.2× p90), Climb hông ≤ 0.65 cm/frame (hết giật 30 cm). File: `QA/AnimAudit/playmode_motion.txt`, `motion_quality.md`, `loop_search.md`.
- Trạng thái: test `All_Gameplay_States_Reached_And_Captured` — 4 nghề × Idle/Run/Jump/Fall/Climb/Attack/Skill/Hit đều vào đúng state, ảnh `QA/AnimAudit/play_states_all.png`. Vũ khí bám tay ≤ 5.2 cm, trượt chân khi chạy 0%.
- Camera: đã kiểm (Rigidbody2D Interpolate + SmoothDamp trong LateUpdate) — không phải nguồn rung.
- **Leo thang — đã làm clip leo mới (chủ dự án chọn "New climb clip"):** `ClimbClipBaker` bake clip leo bằng IK hai khớp cho từng nghề: tay và chân đặt **đúng lên thanh thang thật** (khoảng cách thanh 0.30 m, mặt phẳng thang 0.40 m), luân phiên tay trái/chân phải ↔ tay phải/chân trái, pha bám 55% chu kỳ, pha với lên vòng ra ngoài để không xuyên thanh; thân/đầu lấy từ take Meshy (tại chỗ). Thời gian clip **gắn với độ cao thật** của nhân vật trên thang (`ClimbPhase`) ⇒ tay đang bám đứng yên ở mọi tốc độ leo, dừng thì giữ nguyên tư thế, leo xuống thì động tác chạy ngược. Khi leo, thân hình tự canh vào giữa thang (chỉ phần hình ảnh). Làm trong Unity editor thay vì xuất qua Blender để không phải xuất lại FBX/rig (cùng kết quả, không tốn credit).
  - Thanh thang đổi 0.42 → **0.30 m** cho đúng tỉ lệ chibi (tay dài 0.26–0.33 m không với tới 0.52 m mỗi lần bám) — chỉ là hình, collider leo không đổi.
  - **Bằng chứng:** test `Climb_Hands_Stay_On_Rungs`: 4 nghề, thân đi lên 1.2 cm/frame, tay đang bám trượt **< 0.05 cm/frame**, lệch độ cao thanh < 0.05 cm, lệch mặt phẳng thang < 0.05 cm (`QA/AnimAudit/climb_rungs.txt`); baker: Pháp Sư thiếu tầm với tối đa 1.9 cm ở mép đầu/cuối pha bám. Ảnh video thật: `QA/Video/climb_frames_strip.png` (leo xuống thang Hiên Quán Trà).
  - Còn lại: leo **dây** dùng cùng clip (dây không có thanh — tay bám đúng độ cao, nhìn chấp nhận được).
- **Xương ngón tay — đã thêm (chủ dự án duyệt "Yes, add fingers"):**
  - Kiểm tra thực tế (`QA/Hands/diag_right_all.png`): bàn tay bind-pose mở, 5 ngón có hình, nhưng 4 ngón **dính liền dọc thân ngón tới gần đầu ngón** (kiểu găng/mitten). Đã thử tách từng ngón (cắt theo trục, khoảng cách bề mặt, merge-tree, profile đầu ngón) — kết quả trọng số loang lổ, sẽ rách da khi gập (`QA/Hands/segmentation_all.png`) ⇒ **không dùng**.
  - Giải pháp sạch: `HeroHandRig` (lúc import, FBX không đổi) thêm **khối 4 ngón với 3 khớp** (gốc ngón / giữa / đầu) dưới mỗi xương bàn tay, trọng số chuyển mượt (vùng hoà rộng, kéo vào lòng bàn tay tới 100% bàn tay ở mép ⇒ không có đường gãy); ngón cái giữ theo bàn tay.
  - `HeroFingers` (chạy sau Animator): tay cầm vũ khí **khép quanh chuôi** (độ khép đo lúc build: đầu ngón cách chuôi 0.1–2.6 cm), tay trống nghỉ cong nhẹ, **leo thang hai tay nắm lại**, trúng đòn tay trống bật mở.
  - Bằng chứng: test `HeroHandsTests` (4 nghề, trước/sau/leo) + ảnh `QA/Hands/play_hands.png` (cột 1–2 tay vũ khí trước/sau, 3–4 tay trống trước/sau, 5–6 khi leo).
  - Hạn chế: không cử động từng ngón riêng lẻ (do lưới dính liền), còn vài đường viền mảnh ở nếp gập sâu nhất; muốn ngón rời thật cần model tay mới (tạo lại lưới tay — có thể cần Meshy/Blender sculpt, **chưa làm, cần duyệt**).

### Ưu tiên 3 — Sảnh 3D (đã sửa, ảnh trước/sau)
Ảnh trước: `QA/Before_PhaseA2/`; ảnh sau: `QA/WorldCaptures/`, `QA/HubTest/`.
| Hạng mục | Sửa |
|---|---|
| Mối nối khối đất | Lõi khối đất (`ground_block_core.asset`): cắt bỏ 2 đầu bo tròn bằng clip tam giác chính xác, normal mặt cắt vuông góc, lát xen kẽ soi gương ⇒ mỗi mối nối là 2 mặt cắt giống hệt; bờ suối khớp chính xác |
| Mái ngói mép sàn | Ngói âm dương cong thật 2 hàng so le, nắp tròn đầu ngói, xà nóc, **đầu đao cong vút** 2 góc (thay hộp phẳng nghiêng) |
| Cầu treo, dây | Dây tay vịn liền mạch võng tự nhiên, dây dẫn dọc ván, dây treo, cột đầu cầu (thay ống trụ rời rạc lệch nhau) |
| Đèn lồng ban đêm | `LanternGlow`: hào quang cộng sáng đặt đúng phần giấy đỏ/ô cửa (tìm trên texture), vũng sáng dưới đất; không dùng đèn realtime (mobile). Ban ngày mờ, ban đêm sáng; test kiểm sáng ×3 khi đêm, ảnh `QA/HubTest/01c_spawn_night_lanterns.png` |
| Suối | Hộp nền nâu che thác đã bỏ, thác tràn mép đất hiện rõ, đá cuội bo tròn bọc mép cắt |
| Bảng hiệu/hộp thoại | Giữ từ đợt 1 (kiểm tra tự động vẫn đạt) |
- **Còn lại:** nền vẽ xa chưa tối theo đêm (thuộc hệ ngày/đêm Giai đoạn B); một vạch mảnh giữa hàng đất trước/sau; khu trống chưa thêm chi tiết mới; ván cầu gỗ suối vẫn là khối đơn giản.

### Ưu tiên 4 — Giao diện
Ảnh Play Mode 3 tỉ lệ: PC 16:9 (`01_spawn`), điện thoại 19.5:9 (`01b`, `00b` hộp thoại), máy tính bảng 4:3 (`01d`): joystick tròn không méo, nút không đè nhân vật/viền, hướng dẫn phím ẩn trên cảm ứng, chữ trong khung. Joystick đa chạm có test riêng. Bàn phím PC vẫn qua toàn bộ test di chuyển. **Thanh HP/MP chưa có trong HUD thị trấn** (chỉ có ở sảnh chọn nhân vật) — thuộc hệ chiến đấu.

### Ưu tiên 5 — Kiểm thử
- Server **72/72**, EditMode **4/4**, PlayMode **33 đạt / 0 lỗi / 2 bỏ qua** (gói Input System; lần chạy sau khi thêm clip leo mới và xương ngón tay), log không có Exception/lỗi shader — bao gồm test mới: HeroFaceTests, HeroMotionTests ×2, Town_Pc_Frame_Rate, kiểm tra đèn đêm và ảnh 4:3.
- Cổng Làng ↔ Đồng Cỏ Gió, đăng nhập lại giữ vị trí/dữ liệu: `TownHubTests`, `ServerEndToEndTests` (chạy lại).
- **FPS PC:** 60 FPS ổn định (trung bình 16.7 ms, p95 16.8 ms, tệ nhất 18 ms — chạm giới hạn vsync) trên GTX 1050 Ti + Xeon E3-1230 v3, Editor Play Mode. `QA/HubTest/fps_pc.txt`. **FPS điện thoại: CHƯA đo — không có thiết bị.**
- Video Play Mode: `QA/Video/town_playmode_2026-11-08_phaseA2.mp4`.

### Cần chủ dự án quyết định (đợt 2)
1. ~~Leo thang~~ → đã chọn và đã làm clip leo mới (xem Ưu tiên 2).
2. ~~Xương ngón tay~~ → đã làm (khối ngón 3 khớp). Muốn ngón rời từng ngón thật cần làm lại lưới bàn tay — chờ duyệt.
3. Các mục cũ vẫn chờ: ngân sách Meshy, ngưỡng VIP 1–14, khung tên bản sạch, nguồn nhạc, điện thoại Android để thử APK.

---
## ★ Giai đoạn A, đợt 1 (2026-11-08 chiều) (theo "Yêu cầu tổng hợp phát triển")
**Commit:** "Phase A: sign/dialog text fit, round joystick, phone HUD, Meshy audit" trên `feat/diamond-store-catalog-20261107`, nối tiếp `8ff5bbb` (SHA: xem `git log -1`).

### A0. Kiểm tra đầu phiên
- Nhánh local = `origin` = `8ff5bbb`, **không có thay đổi chưa commit, không có stash** trước khi sửa.
- Chạy lại trên máy này (không dùng kết quả cũ): server **72/72**, EditMode **4/4**, PlayMode **26 đạt / 0 lỗi / 2 bỏ qua** → trùng báo cáo trước.
- Sao lưu hiện có: `C:\AdventureKing_ArtBackup\` (raw Meshy 11-07, 11-08, session, git bundle) + zip khung tên của chủ dự án.

### A1. Kiểm toán credit Meshy (chỉ gọi API đọc số dư, **không tạo tác vụ mới**)
- Số dư thực tế qua API lúc kiểm tra: **931** (681 tháng + 250 vĩnh viễn) — khớp báo cáo.
- `ArtSource/Meshy/manifest.jsonl` (79 tác vụ, 0 tác vụ lỗi bị tính tiền) cộng lại: 11-07 = **479**; 11-08 = **798** → 1729 − 798 = 931, **khớp tuyệt đối**.
- Chi tiết 798 credit ngày 11-08: đợt sáng 144 (19 ảnh concept = 114, 1 model tre = 30); đợt sau khi nâng giới hạn key 654 (19 model image-to-3D × 30 = **570** — 15 đạo cụ còn lại (tre đã làm buổi sáng), 1 khối đất, 3 NPC; 4 ảnh concept = 24; 3 rig = 15; 3 bộ animation NPC = 45).
- **Vượt ngân sách 600 credit đã duyệt khoảng 198 credit.** Nguyên nhân: ngân sách 600 được hiểu theo đợt sau khi chủ dự án nâng giới hạn key, nhưng đợt sáng (144) đã tiêu trước đó, và riêng 20 model 3D đã là 600. Đây là lỗi quản lý ngân sách của Claude, không phải phí ẩn. **Từ nay: không tạo tác vụ Meshy tốn credit nào cho đến khi chủ dự án duyệt ngân sách mới**; mọi đợt sau sẽ ghi dự toán trước khi chạy.

### A2. Đã sửa và có kiểm thử
| Lỗi | Sửa | Bằng chứng |
|---|---|---|
| Chữ bảng hiệu tràn khỏi bảng ("Phố Thợ Rèn & Chợ Ph…") | `SignPanelMeasure`: bắn lưới tia từ phía camera vào model bảng hiệu, đọc texture tại điểm chạm, lấy vùng giấy da liên thông lớn nhất ⇒ đo được mặt bảng thật **0.76 × 0.92 m**; chữ đặt đúng mặt phẳng đó, lề 12%/14%, tự co chữ; câu chữ xuống dòng theo nghĩa | `QA/WorldCaptures/hub_03_smithy_street.png`, `sign_zoom_smithy.png`, `QA/Logs/sign_panel_samples.png` (ảnh những gì tia "nhìn thấy") |
| Chữ hộp thoại NPC dính viền trái, sát nút | Hộp 1240×320, vùng chữ 940×160 nằm trong lòng khung (đo trên ảnh chụp), tự co 20–26, nút ở hàng riêng | `QA/HubTest/00_elder_greeting.png`, `00b_elder_greeting_phone.png` |
| Kiểm thử tự động chữ không tràn | `AssertTextFits`: dựng chữ hai lần (cắt / không cắt) — số ký tự phải bằng nhau; có đối chứng âm (văn bản quá dài phải bị phát hiện) | `TownHubTests`: hộp thoại 199/199 ký tự (PC + điện thoại), 3 bảng hiệu đủ ký tự |
| Điện thoại vẫn hiện hướng dẫn phím | `TouchControlsVisibility.hideOnTouch` | `QA/HubTest/01b_spawn_phone_19.5x9.png` |
| D-pad 4 nút, ▼ sát mép | **Joystick tròn** (`VirtualJoystick2D`): theo đúng 1 ngón (multi-touch), vùng chết 0.22, ngang ≥0.3 = chạy tối đa (tránh trượt chân), dọc cần ≥0.6 (chạy chéo không vô tình leo thang), thả/ẩn là về giữa. Nút phải: Đánh 200, Nhảy 160, Tương tác 130, cách mép ≥ 60 | `MovementTests.Joystick_Moves_Ignores_Jitter_Second_Finger_And_Recentres` |

**Kết quả cuối đợt:** server **72/72** · EditMode **4/4** · PlayMode **27 đạt / 0 lỗi / 2 bỏ qua** (thêm 1 test joystick) · log PlayMode không có Exception. Video Play Mode: `QA/Video/town_playmode_2026-11-08_phaseA.mp4`.

### A3. Gói khung tên của chủ dự án
Lưu nguyên vẹn ở `ArtSource/OwnerProvided/Nameplates_2026-11-08/`. **Chưa đưa vào game** vì: độ phân giải 181–399 px, `Basic` còn ký tự thừa ".t", `VIP_13`/`BossHunter_10` có vệt mép trái, `BossHunter_03` vạch mép trên, `BossHunter_05` mất cánh phải (chi tiết: `Docs/ART_ASSET_PROVENANCE.md`). Hệ thống khung tên (server cấp qua Thư, trang bị 1 khung) làm ở Giai đoạn F; cần bộ ảnh sạch ≥ 512 px.

### A4. Chưa làm được trong đợt này (vẫn thuộc Giai đoạn A)
- **Thử APK trên điện thoại Android thật:** không có thiết bị kết nối với máy → **chưa kiểm chứng**.
- Mối nối khối đất, mái ngói mép sàn, dây leo, đèn lồng phát sáng: chưa sửa.
- Biểu cảm khuôn mặt / ngón tay (rig Meshy không có xương mặt, ngón): chưa làm — cần rig mặt trong Blender, không tốn credit.
- Bộ nút skill 4–6 ô + Auto Skill: chờ hệ thống kỹ năng (Giai đoạn B/G).

### A5. Cần chủ dự án quyết định
1. Ngân sách Meshy cho đợt kế tiếp (đang **dừng hoàn toàn**).
2. Ngưỡng VIP 1–14 (VIP 15 = 50.000.000 VND đã chốt).
3. Bộ khung tên bản sạch, độ phân giải cao (hoặc cho phép Claude vẽ lại bằng công cụ miễn phí).
4. Nguồn nhạc nền (CC0 / thương mại đã xác minh).
5. Kết nối 1 điện thoại Android (USB debugging) để thử APK.
---

## 1. Yêu cầu phải giữ nguyên
- Nền tảng: **Windows PC (chuột + phím), Android và iPhone/iPad (ngang, cảm ứng)**, cùng tài khoản server.
- Đồ họa **chibi 3D trên gameplay 2.5D**, chất lượng thương mại, một art direction thống nhất (`Docs/ART_DIRECTION_TOWN.md`).
- Thiết kế **nguyên bản**: chỉ học cảm giác thị trấn nhiều tầng kiểu MMORPG cổ điển; không sao chép MapleStory (map, nhân vật, cổng, UI, nhạc).
- Server-authoritative; không lưu nhân vật bằng PlayerPrefs; Google/Facebook và thanh toán thật **tắt** đến khi chủ dự án duyệt.
- **Không mua credit, không thuê server, không thanh toán.** Không gửi bản build thô cho chủ dự án.

## 2. Đã hoàn thành và có kiểm thử
| Hạng mục | Trạng thái | Bằng chứng |
|---|---|---|
| Server local (ASP.NET 8 + SQLite) | Tài khoản, nhân vật (4 ô, tên tiếng Việt có lọc), vị trí/bản đồ, cổng dịch chuyển (đúng map, trong tầm, hồi 1.5 s), tutorial, ví có sổ cái, Shop KC (thanh toán tắt), VIP, backup/restore | **72/72** `dotnet test Server/AdventureKing.Server.Tests` |
| Luồng đầy đủ | Mở game → đăng ký → tạo nhân vật → vào làng → đi → Lưu & Thoát → tắt/bật server → đăng nhập → đúng vị trí | PlayMode `ServerEndToEndTests` |
| Thị trấn — di chuyển | Chạy, nhảy, leo 2 dây + 4 thang (lên/xuống xuyên sàn gỗ một chiều), cầu treo, rơi khỏi map được cứu, đi hết map 2 chiều không kẹt, layer/collider đúng | PlayMode `TownHubTests` + video |
| Cổng dịch chuyển | Vùng trigger, tên bản đồ đích, hộp xác nhận (khi rời thị trấn), chặn kích hoạt kép, hiệu ứng xoáy + âm thanh, màn tải thật (async + thanh tiến trình), server xác thực, xuất hiện đúng điểm ở Đồng Cỏ Gió và quay về | `TownHubTests`, ảnh `QA/HubTest/05*` |
| NPC Trưởng Làng | Người mới: ông đi tới (animation đi thật), quay mặt, vẫy tay, chào đúng tên nhân vật trong hộp thoại; đóng hộp thoại → server lưu tutorial bước 1 → cúi chào, đi về; không chào lại lần sau | `TownHubTests` (đoạn đầu), ảnh `00_elder_greeting` |
| 4 nhân vật | Vũ khí bám tay ≤ 5.2 cm; tốc độ phát Run khớp tốc độ đi (trượt chân 0%) | `QA/AnimAudit/` |
| Logo | Logo studio SAM của chủ dự án ở Splash | — |
| Mobile chuẩn bị | Android + iOS Build Support đã cài; **APK development build thành công (69.9 MB, 0 lỗi)**; nút cảm ứng, vùng an toàn tai thỏ, tùy chọn đồ họa Thấp/Vừa/Cao; server chế độ LAN | `QA/Logs/34_android_build.log`, `Docs/MOBILE_LAN_TESTING.md` |
| Lỗi treo PlayMode | Nguyên nhân: test/scene Single unload scene của Test Runner. Đã sửa bằng `TestRunnerGuard` | Bộ test chạy hết |

**Kết quả lần chạy cuối của phiên (2026-11-08 10:3x):** Server **72/72** · EditMode **4/4** · PlayMode **26 passed / 0 failed** (+2 test của gói Input System bị chính Unity bỏ qua). Video Play Mode: `QA/Video/town_playmode_3d_2026-11-08.mp4` (96.9 s, 30 fps, quay trực tiếp trong test, có UI).

## 3. Tình trạng từng khu vực
- **Thị trấn Chuông Gió (AK_Village):** đã chuyển sang **3D thống nhất**: 16 đạo cụ Meshy 3D (quán trà, nhà, lò rèn, sạp, tháp chuông, cối xay, cây đa, tre, đèn, bảng hiệu, tượng, cầu đá, cổng rune, thùng, chậu hoa), mặt đất bằng khối đất 3D Meshy lát nối, sàn gỗ/thang/cầu treo bằng hình học bo tròn cùng shader Toon + viền, nền tranh vẽ chỉ ở xa (parallax), post-processing chung (bloom nhẹ, chỉnh màu, vignette). Bố cục: mặt đất + 2 tầng sàn + đài cao nhất, 2 dây leo, 4 thang, cầu treo, suối + cầu gỗ, khu Thợ Rèn/Chợ (tây), Quảng trường + tượng (giữa), Quán Trà (đông), cổng sang Đồng Cỏ Gió (đông). Ảnh Play Mode: `QA/WorldCaptures/`, `QA/HubTest/`.
- **Đồng Cỏ Gió (AK_Meadow):** cùng hệ đồ họa, 3 bệ, dây/thang, cổng về. **Chưa có quái.**
- **Sảnh chọn tướng (AK_Entry):** giữ nguyên 4 model 3D, xoay, xem đòn đánh/kỹ năng. UI đã đổi: khung vàng chạm đá quý (art Meshy) cho bảng lớn, nút men bóng viền vàng, ô nhập chìm, thanh chỉ số khung vàng, font **Be Vietnam Pro**. **Chưa duyệt kỹ bằng mắt toàn bộ màn hình sau khi đổi** (xem ảnh `QA/LobbyCaptures/` của lần chạy cuối).
- **NPC:** Trưởng Làng (chào người mới), Thợ Rèn, Thủ Kho — model 3D rig + 5 animation, bảng tên. Thợ Rèn/Thủ Kho **chỉ đứng idle, chưa có hội thoại/cửa hàng**.

## 4. Meshy — credit và tác vụ
- Số dư đầu ngày 2026-11-08: **1729** → cuối phiên: **931** ⇒ dùng **≈ 798 credit** (tất cả từ credit sẵn có, không mua thêm).
- Đã tạo trong ngày: 16 concept + 16 model đạo cụ, 1 tranh nền, 1 khối đất (concept + model), 3 NPC (concept, model, rig, animation), 2 khung UI.
- Lỗi đã gặp: "API key credit limit reached" — chủ dự án đã nâng giới hạn API key.
- **Không còn tác vụ Meshy nào đang chạy.** File gốc sao lưu tại `C:\AdventureKing_ArtBackup\2026-11-08_session\` (79 thư mục tác vụ). Nhật ký: `ArtSource/Meshy/manifest.jsonl`.

## 5. Lỗi / hạn chế chưa xử lý
**Animation & nhân vật**
- Rig Meshy **không có xương ngón tay, xương mặt, blendshape** → không có biểu cảm khuôn mặt thật (nét mặt cố định), ngón tay không cử động. Cần: rig mặt trong Blender (shape keys mắt/miệng) hoặc đổi texture biểu cảm; thêm xương ngón.
- Leo thang: tốc độ hoạt ảnh khớp khoảng cách bậc, tay chưa bám chính xác từng bậc.
- NPC đi bằng tốc độ cố định (1.3 m/s), chưa đo stride như nhân vật chính.

**Đồ họa**
- Khối đất lát nối: có thể thấy mối nối/lặp lại ở một số vị trí; texture hơi kéo giãn khi scale.
- Mái ngói ở mép sàn còn thô so với đạo cụ Meshy; dây leo đơn giản.
- Đèn lồng chưa phát sáng thật (URP đang tắt đèn phụ để tiết kiệm mobile).
- ~~Bảng hiệu: chữ đặt theo ước lượng~~ → đã đo mặt bảng thật (mục A2).
- Chưa có LOD/tối ưu cho Android; chưa đo FPS trên điện thoại thật.

**UI**
- Mới đổi khung/nút/font; bố cục sảnh chưa được thiết kế lại hoàn toàn; chưa có hiệu ứng mở/đóng cửa sổ.
- Nhãn HUD/tên vẫn dùng panel tối đơn giản ở vài chỗ.
- ~~Hộp thoại NPC: chữ sát mép trái khung~~ → đã sửa (mục A2).
- ~~Bố cục điện thoại: hướng dẫn phím hiện trên điện thoại; nút ▼ sát mép; mũi tên nhỏ~~ → đã sửa, thay bằng joystick tròn (mục A2).

**Nền tảng**
- APK build được nhưng **chưa cài/kiểm thử trên điện thoại thật**; tường lửa Windows cho cổng 5080 do chủ dự án tự cho phép.
- iOS: mới có cấu hình + iOS Build Support; **cần máy Mac + Xcode** để build/ký/chạy.

**Âm thanh:** chưa có nhạc nền có bản quyền (chờ chủ dự án chọn nguồn).

**Unity log:** có cảnh báo licensing (`LicensingClient failed validation`) và "missing authored music" — không phải lỗi biên dịch.

## 6. Việc tiếp theo (thứ tự ưu tiên)
1. **Hoàn thiện map**: duyệt ảnh/video Play Mode, sửa mối nối đất, mái ngói, dây leo; thêm ánh sáng đèn lồng (light cookie/emissive), chi tiết khu Chợ/Quán Trà; LOD + đo hiệu năng Android.
2. **Quái ở Đồng Cỏ Gió**: 2–3 loại quái nguyên bản (Meshy concept → model → rig → idle/walk/attack/hit/death), AI tuần tra, va chạm đòn đánh với animation event, **sát thương & loot do server quyết định**.
3. **Boss**: Mini Boss đầu tiên với kỹ năng riêng, bảng loot server, điểm Boss Hunter.
4. **Gameplay chiến đấu**: kỹ năng theo nghề, EXP/level-up lưu server, nhặt Gold/vật phẩm.
5. Hội thoại + cửa hàng cho Thợ Rèn/Thủ Kho; túi đồ.
6. Thử APK trên điện thoại thật qua LAN; chuẩn bị build iOS trên Mac.
7. Nhạc nền (khi chủ dự án chọn nguồn).

## 7. Cách chạy
```
dotnet run --project Server/AdventureKing.Server              # PC
dotnet run --project Server/AdventureKing.Server --environment Lan   # thử điện thoại cùng Wi-Fi
```
Unity 6000.6.7f1: mở `Assets/Scenes/AK_Entry.unity` → Play. Dựng lại map: menu `AdventureKing/Scenes/Rebuild World Maps`; NPC: `AdventureKing/Art/Rebuild NPC Visuals`; build: `AdventureKing/Build/*`.

## 8. Sao lưu
- `C:\AdventureKing_ArtBackup\2026-11-08_session\` — ArtSource (Meshy raw, request, spec, logo), Tools.
- Git bundle toàn bộ lịch sử (kể cả commit chưa push): `C:\AdventureKing_ArtBackup\git\`.
- Database server thật: **chưa có dữ liệu người chơi thật** (chỉ database thử nghiệm tạm, tự xóa sau test).

## 9. Trạng thái lúc kết thúc phiên
- Toàn bộ thay đổi đã kiểm thử được commit trên nhánh `feat/diamond-store-catalog-20261107` (commit "Unified 3D town, real portal travel, Elder greeting, mobile prep, fantasy UI").
- Push: thử bằng git của GitHub Desktop; nếu báo cần đăng nhập thì **chủ dự án bấm Push trong GitHub Desktop** (Claude không đọc/nhập thông tin đăng nhập).
- Git bundle sao lưu: `C:\AdventureKing_ArtBackup\git\AdventureKing_Reborn_2026-11-08.bundle`.
- Không còn tiến trình Unity, server thử nghiệm hay tác vụ Meshy nào đang chạy.
- APK nội bộ ở `Builds/Android/` (không đưa lên Git, không gửi cho chủ dự án).
