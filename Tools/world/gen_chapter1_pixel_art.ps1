# Generates the Chapter 1 pixel-art panoramas (maps 2-10) and the extra shared props with Meshy text-to-image (6 credits each).
# Run only after the owner approved the spend and the balance was checked:  powershell -File Tools\world\gen_chapter1_pixel_art.ps1 -Yes
param([switch]$Yes)
if (-not $Yes) { Write-Host "refused: add -Yes after the owner approved ~78 credits (9 panoramas + 4 props)"; exit 1 }
$py = 'C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe'
$env:MESHY_API_KEY = [Environment]::GetEnvironmentVariable('MESHY_API_KEY', 'User')
Set-Location (Resolve-Path "$PSScriptRoot\..\..")
$common = "high-detail 2D pixel art parallax background for a dark fantasy action RPG side-scroller, square canvas, NO characters, NO text, NO UI. The TOP 45% is the sky, the MIDDLE has layered misty distant scenery, the BOTTOM 30% stays calm, fairly dark and plain (a soft dark misty band) so a game floor can sit on it. Crisp discrete pixels with NO anti-aliasing, NO blur, hand-pixeled clustered shading with dithering, strong contrast, rich saturated accent colors against dark tones, atmospheric depth, horizontally balanced composition so it can wrap around."
$pans = @(
  @{ n = 'm02'; d = 'a wide green meadow at dawn: rolling emerald hills with wildflowers, a far windmill on a hill, tall slim trees, soft golden-pink sky with drifting clouds and a pale sun, a few glowing fireflies' },
  @{ n = 'm03'; d = 'a twilight glow forest: giant glowing teal and violet mushrooms, drifting luminous spores, dark teal tree silhouettes, a deep teal-purple sky with a crescent moon' },
  @{ n = 'm04'; d = 'a flower hill under a pastel dusk: rolling hills covered in pink cherry blossom trees and wildflowers, petals drifting in the air, a far windmill, a lilac-to-peach sky' },
  @{ n = 'm05'; d = 'a deep mushroom cave: dark cavern walls with glowing blue and violet crystal veins, hanging stalactites, glowing cave mushrooms, faint mist, a dark navy ceiling' },
  @{ n = 'm06'; d = 'a jade waterfall canyon: tall teal jade cliffs with several glowing waterfalls, turquoise mist, mossy ledges, a pale mint sky' },
  @{ n = 'm07'; d = 'a dark haunted forest: twisted black trees with hanging glowing violet lanterns, dense purple fog, a few pale moonbeams, a deep purple night sky with sparks of spores' },
  @{ n = 'm08'; d = 'a toxic poison marsh: sickly olive-green swamp pools bubbling, giant glowing purple and lime mushrooms, dead trees, green fog, a murky yellow-green sky' },
  @{ n = 'm09'; d = 'ancient overgrown ruins: broken stone pillars and arches covered in moss, a distant glowing cyan crystal gate, crumbling walls, a stormy gray-gold dusk sky' },
  @{ n = 'm10'; d = 'the Mushroom King temple grounds: a huge dark temple of stone and giant crimson mushrooms with golden trim glowing in the distance, crimson sky with embers and floating spores, black spires' }
)
foreach ($p in $pans) {
  $body = @{ prompt = "$common Subject: $($p.d)."; ai_model = 'nano-banana-2'; remove_background = $false; aspect_ratio = '1:1' }
  $name = "pixel_bg_$($p.n)"
  $body | ConvertTo-Json | Set-Content "ArtSource\Meshy\requests\$name.json" -Encoding utf8
  & $py Tools\meshy\meshy.py run text-to-image $name "ArtSource\Meshy\requests\$name.json" 2>&1 | Select-String 'credits|HTTP|rror' | Select-Object -First 2
}
$style = "high-detail 2D pixel art game prop for a dark fantasy action RPG side-scroller, strict flat side/front orthographic view, crisp discrete pixels with NO anti-aliasing, NO blur, NO smooth gradients, dark selective outline, hand-pixeled clustered shading with 3-4 tones per material, light from the top-left, rich saturated accents against dark twilight tones, single object only, the object fully visible and centered on a flat solid magenta #FF00FF background, no ground, no shadow, no text, no characters, no frame."
$props = @(
  @{ n = 'flower_bush'; d = 'a round bush of dark green leaves covered in glowing pink, yellow and violet flowers with a few fireflies' },
  @{ n = 'fence'; d = 'a short section of weathered dark wooden fence with three posts and two rails, moss and a few small glowing mushrooms at the base' },
  @{ n = 'banyan'; d = 'a huge ancient twisted banyan tree with a wide dark green canopy, thick gnarled trunk, many hanging aerial roots and a few glowing blue lanterns hanging from the branches' },
  @{ n = 'windmill'; d = 'a stone windmill on a small mound: round stone tower with a conical dark-red roof, four large wooden sails, a small arched door and a warm glowing window' }
)
foreach ($p in $props) {
  $body = @{ prompt = "$style Subject: $($p.d)."; ai_model = 'nano-banana-2'; remove_background = $false; aspect_ratio = '1:1' }
  $name = "pixel_prop_$($p.n)"
  $body | ConvertTo-Json | Set-Content "ArtSource\Meshy\requests\$name.json" -Encoding utf8
  & $py Tools\meshy\meshy.py run text-to-image $name "ArtSource\Meshy\requests\$name.json" 2>&1 | Select-String 'credits|HTTP|rror' | Select-Object -First 2
}
& $py Tools\meshy\meshy.py balance
