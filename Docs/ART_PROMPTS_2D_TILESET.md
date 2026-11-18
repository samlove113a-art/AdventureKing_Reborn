# Prompt tạo Tileset + Trang trí (DRAFT — chờ chủ dự án duyệt)

Style khóa chung: nét viền cứng tối, màu bão hòa, cel-shading 2 mảng, ánh sáng từ trên, nhìn ngang 2D phẳng, lưới 100 px = 1 unit (100 PPU).
Không ghi tên game khác vào prompt (xem `ART_DIRECTION_25D.md` §Non-negotiable: cảm hứng, không clone). Motif lấy từ **Làng Chuông Gió**.

## STYLE LOCK (dán vào đầu mọi prompt)
```
original 2D side-scrolling fantasy MMORPG game asset, flat orthographic side view, no perspective,
bold hard-edged dark brown outline 3px on every shape, vibrant highly saturated colors,
cel shading with exactly two flat tones per material plus one crisp highlight band,
single light source directly from above, top surfaces brightest, undersides darker,
no blur, no gradients, no soft shadows, no ambient occlusion, no texture noise,
cute rounded chunky proportions sized for super-deformed chibi characters with 1:1 head-to-body ratio,
clean game-ready sprite, isolated on pure white background #FFFFFF, generous padding, no drop shadow on background
```

## NEGATIVE (dùng cho mọi prompt nếu công cụ hỗ trợ)
```
perspective, isometric, 3/4 view, photorealistic, painterly, watercolor, blur, depth of field, bloom, glow haze,
soft gradient shading, 3D render look, noise, grain, text, letters, watermark, logo, signature, UI frame,
cast shadow on background, cropped edges, characters, people, animals, existing game characters or logos
```

## 1. Tileset mặt đất cỏ xanh
```
[STYLE LOCK], tileset sprite sheet on a strict 100x100 pixel grid, 8 columns x 4 rows, each tile exactly 100x100 px,
seamless tiling edges, grassy ground for a cheerful wind-chime village:
row 1: grass top-left cap, grass top middle A, grass top middle B (small clover tufts), grass top-right cap,
       grass slope up 45 degrees, grass slope down 45 degrees, single-tile floating grass block, thin one-way grass ledge;
row 2: soil fill under grass with rounded pebbles A, soil fill B, soil left wall, soil right wall,
       soil bottom-left rounded corner, soil bottom-right rounded corner, soil bottom middle with hanging roots, inner corner;
row 3: mossy stone terrace top, mossy stone terrace fill, wooden plank bridge segment, rope bridge segment,
       wooden ladder segment (seamless vertically), green vine rope segment (seamless vertically), ladder top cap, vine top knot;
row 4: floating platform left cap, floating platform middle, floating platform right cap, small grass tuft edge decoration,
       two flower clusters (yellow, pink), small rock, empty tile;
bright leaf green #5FCB3A grass top with darker #2E8B2A shade band, warm soil brown #B5713A with #7A4422 shade,
outline #2B1A10, grass blades overhang the soil edge by 8 px with a jagged hard-edged lip
```

## 2. Cây cối / nhà cửa trang trí (Midground — sáng hơn tileset ~10–15 %)
```
[STYLE LOCK], prop sprite sheet for the midground layer, each prop separated with 40 px white gap, slightly lighter and
less contrasted than gameplay tiles so it reads as behind the player, flat side view:
1. two-storey timber cottage with upward-curved roof eaves, teal roof tiles, round window, hanging brass wind chimes;
2. small timber shop stall with striped cloth awning and wind chime;
3. giant ancient tree with twisted trunk, wide round canopy in two green tones, glowing seed-pod lanterns hanging from branches;
4. medium round bush tree, small sapling;
5. luminous seed lamp post (warm yellow glass bulb shaped like a seed) on a wooden pole;
6. mossy stone terrace wall section and stone stairs;
7. little wooden windmill-style wind mechanism with spinning paper vanes;
8. hero statue on a stone plinth (generic adventurer pose, original design, no recognizable character);
9. wooden fence segment (tileable), signpost with blank board, barrel and crate pile;
props scaled for chibi characters about 100 px tall: cottage door 120 px tall, cottage total ~420 px tall
```

## 3. (Tùy chọn) Nền xa — Sky / Far mountains
```
[STYLE LOCK] but outline thinner 1px and lower contrast, horizontally seamless wide background strip 3840x1080 px,
layer A: clear cyan sky #7FD8FF to light #CFF3FF in two flat bands only, puffy cel-shaded white clouds with flat blue undersides;
layer B (separate image): distant rolling hills and terraced rice mountains in desaturated blue-green, flat silhouettes, no detail
```

## Đưa vào Unity
- Import: Texture Type `Sprite (2D and UI)`, **Pixels Per Unit 100**, Filter `Point` (giữ nét viền) hoặc `Bilinear` nếu ảnh > 1×, Compression `None`/`High Quality`, Mesh Type `Full Rect` cho tile.
- Tileset: Sprite Mode `Multiple` → Sprite Editor → Slice `Grid By Cell Size` 100×100.
- Nền 4 lớp: dùng `ParallaxBackground4` (`Assets/Scripts/Gameplay/ParallaxBackground4.cs`) — Add Component lên object "Background" → chuột phải component → **Apply Art Bible defaults** → kéo sprite vào 4 slot (Sky / FarMountains / MidForest / Foreground).
