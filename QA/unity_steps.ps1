$u="C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe"
Set-Location C:\AdventureKing_Reborn
$p = Start-Process -FilePath $u -ArgumentList '-batchmode','-quit','-projectPath','C:\AdventureKing_Reborn','-executeMethod','AdventureKing.ArtPipeline.Chibi2DArt.BuildAllFromCommandLine','-logFile','C:\AdventureKing_Reborn\QA\art_build.log' -PassThru -Wait
"art exit $($p.ExitCode)" | Set-Content QA\unity_steps.log
$p = Start-Process -FilePath $u -ArgumentList '-batchmode','-quit','-projectPath','C:\AdventureKing_Reborn','-executeMethod','AdventureKing.ArtPipeline.VillageSceneBuilder.BuildChapter1Maps3To10FromCommandLine','-logFile','C:\AdventureKing_Reborn\QA\scenes_build.log' -PassThru -Wait
"scenes exit $($p.ExitCode)" | Add-Content QA\unity_steps.log
