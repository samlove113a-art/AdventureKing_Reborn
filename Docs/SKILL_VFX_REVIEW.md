# Bộ VFX 12 kỹ năng — BẢN NHÁP 1, CHỜ CHỦ DỰ ÁN DUYỆT (2026-11-11)

> **Chưa nghiệm thu.** Code chạy và test pass KHÔNG có nghĩa là đạt chất lượng hình ảnh. Tài liệu này để chủ dự án xem video/ảnh thật và quyết định sửa gì trước khi nhân rộng sang các map tiếp theo.

## Xem ở đâu
- **Video Game View thật (30 fps, 1280×720, tốc độ thật):** `QA/SkillVfx/video/<nghề>_<skill>.mp4` — 12 skill + 4 bản điện thoại của skill 3 (`*_3_phone.mp4`).
- **Ảnh 1920×1080 (JPG q90), 4 khoảnh khắc mỗi skill:** `QA/SkillVfx/<nghề>_<skill>_a_charge / b_release / c_impact / d_dissipate.jpg`; ảnh điện thoại: `*_3_phone_c_impact.jpg`.
- Cách tạo lại: PlayMode `SkillVfxShowcaseTests` (chạy CÓ cửa sổ, server thật, Làng Nấm, mục tiêu Nấm Non) → ghép video bằng `Tools/blender/encode_video.py`.

## Đã làm (Unity URP + công cụ sẵn có, **0 credit Meshy**, không tài nguyên bên ngoài)
- **Shader** `AdventureKing/VFXFlow`: mặt nạ hình × noise cuộn; gradient lõi/viền HDR (bloom URP làm lõi phát sáng); tan biến (dissolve) có viền sáng; trượt UV cho trail.
- **Texture tự sinh** (`SkillVfxArt`, `Assets/Resources/SkillVfx`), 13 tấm: noise mây, noise sọc, vệt chém lưỡi liềm, vòng sóng xung kích, tia lửa, lưỡi lửa, khói, lông vũ, vết nứt, sao 4 cánh, cột sáng, ấn phép, quầng sáng.
- **Mesh thủ tục:** kiếm trời, mảnh băng, thiên thạch, dao/mũi tên, dải lốc xoáy. Riêng bóng phân thân là ảnh tĩnh chụp lại tư thế thật của nhân vật (BakeMesh).
- **Biên đạo 4 pha cho mỗi skill** (`SkillVfxDirector`): tích tụ → tung chiêu → va chạm → tan biến. Sát thương, quái chết và rơi đồ hiện **đúng nhịp va chạm**; server vẫn quyết định mọi con số.
- **Phản hồi:**
  - Rung camera theo độ mạnh; khựng hình (hit-stop) 35–100 ms, chỉ ở phía client.
  - Skill 3 có thêm zoom-punch.
  - Khi tung skill, camera đưa hành động sang trái để tránh cụm nút HUD.
- **Quái phản ứng:** bị đẩy lùi có lò xo, chớp màu của skill, chạy clip Hit (Nấm Non có rig).
- **Âm thanh:** 29 hiệu ứng tổng hợp nguyên bản (`Tools/audio/skill_sfx.py`), gồm âm tích tụ, nhịp đánh và va chạm riêng cho từng skill.
- **Animation:** mỗi skill chọn clip phù hợp với nhịp VFX, ví dụ Thiên Kiếm = giơ tay (Cheer) rồi chém; Thiên Thạch = niệm chú tích tụ; Ám Ảnh = lộn né rồi xoay chém.
  - Không tạo clip mới bằng Meshy.
- **Chất lượng:** PC dùng bộ High. Điện thoại dùng bộ nhẹ: khoảng 45% hạt, ít lớp phụ, rung camera nhẹ hơn (`SkillVfxKit.Mobile`).
  - Riêng skill 3 có bản xem trước điện thoại.

## Thiết kế từng skill
| Nghề | Skill | Màu | Hình dạng / nhịp | Trúng đòn |
|---|---|---|---|---|
| Kiếm Sĩ | 1 Chém Gió | ngọc–trắng | vệt chém lưỡi liềm + lưỡi gió bay 2,6 m, nhịp nhanh | chữ X + tia + gió |
| | 2 Kiếm Khí Xoáy | ngọc–lục | ấn gió dưới chân, lốc xoáy 2 lớp, 3 vòng kiếm xoay | 3 nhịp; nhịp cuối nổ sóng |
| | 3 Thiên Kiếm | vàng–trắng | giơ tay triệu ấn + cột sáng, kiếm khổng lồ rơi từ trời | chớp, 2 sóng xung kích, trụ sáng, vết nứt; rung + khựng + zoom |
| Pháp Sư | 1 Cầu Lửa | cam–đỏ | xoáy lửa ở trượng, cầu lửa sống kèm tàn lửa | nổ lửa toả tia, khói, vết cháy |
| | 2 Bão Băng | lam–trắng | ấn băng, tuyết rơi, 11 mảnh băng rơi | mỗi mảnh vỡ thành sao/tia/vòng băng, cuối có sương |
| | 3 Thiên Thạch | đỏ thẫm–tím | 2 ấn ma pháp, thiên thạch cháy lao chéo xuống, khói | nổ lớn, trụ lửa, đá văng, hố nứt; rung + khựng + zoom |
| Sát Thủ | 1 Phi Dao | tím | 3 dao xoè xoay, trail tím | 3 nhát X nhanh |
| | 2 Ảnh Phân Thân | tím | bóng phân thân (đúng tư thế nhân vật) lao 2 bên, 3 nhịp | chém lưỡi liềm hai phía |
| | 3 Ám Ảnh Liên Trảm | tím–đỏ thẫm | lộn để lại bóng, 7 nhát chém chéo trong cầu bóng tối | X đỏ thẫm + nổ bóng; rung + khựng + zoom |
| Cung Thủ | 1 Tên Xuyên | lục–vàng | tên sáng kèm vòng xoắn, bay 8 m | xuyên qua từng mục tiêu: tia toả về phía sau |
| | 2 Mưa Tên | lục | bắn lên trời → tách → 15 tên rơi vào ấn | bụi + tia ở mỗi điểm cắm |
| | 3 Phượng Hoàng Tiễn | vàng–cam | cánh lửa xoè sau lưng khi kéo cung; phượng hoàng vỗ cánh bay 9 m, rụng lông lửa | nổ lửa ở từng mục tiêu, cuối tan thành lông vũ |

## Tự đánh giá thật (đã xem ảnh, chưa đạt)
1. **Va chạm skill 3 của Kiếm Sĩ và Cung Thủ** vẫn còn quầng trắng lớn, át màu riêng.
2. **Thiên Thạch:**
   - Khói va chạm quá đen, nặng.
   - Có vài mảng lửa thành đốm tròn phẳng; cần thay bằng lửa dạng tờ (flipbook) có chuyển động.
3. **Thiên Kiếm, lúc tung chiêu:**
   - Ấn dưới đất mỏng, cột sáng mờ.
   - Kiếm rơi quá nhanh, gần như chỉ thấy ở khung va chạm.
4. **Ấn/vòng dưới đất:** camera nhìn rất xiên nên ấn thành elip dẹt, khó đọc. Có thể nghiêng ấn về phía camera.
5. **Skill 1 nói chung** hơi nhỏ ở khung va chạm; cảm giác lực đánh chủ yếu nhờ rung + khựng.
6. **Animation:** dùng clip có sẵn ghép lại, chưa phải động tác riêng cho từng skill. Nếu chủ dự án muốn, có thể thêm clip từ thư viện Meshy (~3 credit/clip) sau khi duyệt.
7. **Chưa có:**
   - Méo không khí (distortion) và vệt bóng chuyển động (motion blur) trên nhân vật.
   - Bản điện thoại chưa đo FPS trên máy thật (chưa có thiết bị).
8. **Kiểm thử chỉ chứng minh:**
   - Skill chạy qua server thật, quái phản ứng, camera có phản hồi, hit-stop luôn được nhả.
   - Âm thanh tồn tại cho cả 12 skill.
   - Không có exception.

   Kiểm thử không chứng minh được skill "đẹp".

## Cần chủ dự án quyết định
- Hướng màu/hình dạng từng skill ở bảng trên có đúng ý không; skill nào sửa trước.
- Có cho dùng Meshy (thư viện animation, ~3 credit/clip) để mỗi skill có động tác riêng không.
