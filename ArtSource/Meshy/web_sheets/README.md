# Pet animation sheets for the Meshy web app (6 frames per pet)

For each file in `todo/` (one per pet, 1536x1024, 3 columns x 2 rows, flat grey background, frame 1 already in the top-left cell):

1. Open Meshy web -> an **image editing / image-to-image** tool (the one that takes a reference picture + a prompt, e.g. Nano Banana). Upload `todo/<pet>.png`, paste the prompt of the pet's type below, generate.
2. Save the result as `done/<pet>.png` (same name, PNG, keep the 3x2 layout).
3. Tell Claude: the sheets are in `done/`. Claude cuts the 6 frames, removes the grey, aligns them to the feet line and imports them (`Tools/costume/pet_sheet_import.py`).

If a result is bad (wrong layout, missing tail, blurry), just regenerate it with the same prompt; a good sheet has 6 clearly separate cells, the creature in the same size and position in every cell, feet on the same line.

## Prompt WALK (use for every pet except the flyers below)
```
This image is a pixel-art game creature in the top-left cell of a 3x2 grid (6 equal cells, flat grey background). Keep the first cell exactly as it is and fill the other 5 cells with the next 5 frames of a smooth LOOPING WALK CYCLE of the SAME creature, in order left to right, top to bottom: frame 2 = front foot reaching forward, frame 3 = body lowest with weight on the front foot, frame 4 = legs passing under the body, frame 5 = back foot pushing off, body highest, frame 6 = both legs mid-air gathering, ready to loop back to frame 1. Tail, ears, mane and flames sway naturally with the steps. Every cell: the same creature, same design and colours, same size, centred horizontally, feet on the same baseline near the bottom of the cell, facing right, all limbs and the whole tail fully drawn and attached. Crisp pixel art with NO anti-aliasing and NO blur, dark outline, flat grey background in every cell, no text, no borders, no shadows, no extra objects.
```

## Prompt FLY (pet_war_dragon, pet_phoenix, pet_night_owl)
```
This image is a pixel-art game flying creature in the top-left cell of a 3x2 grid (6 equal cells, flat grey background). Keep the first cell exactly as it is and fill the other 5 cells with the next 5 frames of a smooth LOOPING WING-BEAT CYCLE of the SAME creature, in order left to right, top to bottom: wings rising, wings at the top fully spread, wings sweeping down, wings at the bottom, wings rising again to loop back to frame 1. The body bobs slightly (a little higher at the top of the beat, lower at the bottom), tail and feathers trail naturally. Every cell: the same creature, same design and colours, same size, centred, facing right, the whole tail and both wings fully drawn. Crisp pixel art with NO anti-aliasing and NO blur, dark outline, flat grey background in every cell, no text, no borders, no shadows.
```

## Prompt FLOAT (pet_lantern_wisp, pet_rainbow_cloud)
```
This image is a pixel-art floating game creature in the top-left cell of a 3x2 grid (6 equal cells, flat grey background). Keep the first cell exactly as it is and fill the other 5 cells with the next 5 frames of a smooth LOOPING FLOAT CYCLE of the SAME creature: it drifts up and down by a few pixels, the body puffs slightly and relaxes, the tail / wisp / flame / rainbow tail waves left and right in a flowing loop, returning to frame 1 at the end. Every cell: the same creature, same design and colours, same size, centred, facing right. Crisp pixel art with NO anti-aliasing and NO blur, dark outline, flat grey background in every cell, no text, no borders, no shadows.
```

## Prompt HOP (pet_slime_jelly, pet_mushroom_walker, pet_chick, pet_radish_bunny)
```
This image is a pixel-art game creature in the top-left cell of a 3x2 grid (6 equal cells, flat grey background). Keep the first cell exactly as it is and fill the other 5 cells with the next 5 frames of a smooth LOOPING HOPPING-WALK CYCLE of the SAME creature: crouch, push off, in the air (stretched), in the air (peak), landing squash, recover to frame 1. Ears, leaves, sprout, scarf and tail follow the motion. Every cell: the same creature, same design and colours, same size, centred, feet / base on the same baseline near the bottom (except in the air), facing right. Crisp pixel art with NO anti-aliasing and NO blur, dark outline, flat grey background in every cell, no text, no borders, no shadows.
```

## Which prompt for which pet
- WALK: pet_cloud_cat, pet_jade_turtle, pet_jade_dragon, pet_fire_fox, pet_snow_wolf, pet_leaf_dragon, pet_wind_unicorn, pet_ember_tiger, pet_fire_dragon, pet_ice_dragon, pet_thunder_dragon, pet_shadow_dragon, pet_gold_dragon, pet_night_horse
- FLY: pet_war_dragon, pet_phoenix, pet_night_owl
- FLOAT: pet_lantern_wisp, pet_rainbow_cloud
- HOP: pet_slime_jelly, pet_mushroom_walker, pet_chick, pet_radish_bunny
- pet_ink_dragon is already animated (slither wave): no sheet needed.