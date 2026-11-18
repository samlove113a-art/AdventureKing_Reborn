$bl="C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
Set-Location C:\AdventureKing_Reborn
foreach($p in @(@("Mage","_meshy2"),@("Swordsman","_meshy"),@("Assassin","_meshy2"))){
  & $bl -b --factory-startup -P Tools\blender\bake_chibi_pixel.py -- $p[0] "-" "ArtSource/Chibi2D/$($p[1])/$($p[0]).fbx" 2>&1 | Select-String "DONE|Traceback|rror" | % { $_.Line } | Add-Content QA\bake_all2.log
}
"ALLDONE" | Add-Content QA\bake_all2.log
