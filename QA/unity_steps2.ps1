$u="C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe"
Set-Location C:\AdventureKing_Reborn
$p = Start-Process -FilePath $u -ArgumentList '-batchmode','-quit','-projectPath','C:\AdventureKing_Reborn','-executeMethod','AdventureKing.ArtPipeline.Chibi2DArt.BuildAllFromCommandLine','-logFile','C:\AdventureKing_Reborn\QA\art_build2.log' -PassThru -Wait
"art exit $($p.ExitCode)" | Set-Content QA\unity_steps2.log
$p = Start-Process -FilePath $u -ArgumentList '-batchmode','-projectPath','C:\AdventureKing_Reborn','-runTests','-testPlatform','PlayMode','-testFilter','AdventureKing.Tests.Chibi2DPostureTests;AdventureKing.Tests.Chapter1MapsPlayTests','-testResults','C:\AdventureKing_Reborn\QA\maps_results.xml','-logFile','C:\AdventureKing_Reborn\QA\maps_test.log' -PassThru -Wait
"tests exit $($p.ExitCode)" | Add-Content QA\unity_steps2.log
