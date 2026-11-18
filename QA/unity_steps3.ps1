$u="C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe"
Set-Location C:\AdventureKing_Reborn
$p = Start-Process -FilePath $u -ArgumentList '-batchmode','-projectPath','C:\AdventureKing_Reborn','-runTests','-testPlatform','PlayMode','-testFilter','AdventureKing.Tests.Chapter1MapsPlayTests;AdventureKing.Tests.Pixel2DSkillTests','-testResults','C:\AdventureKing_Reborn\QA\maps_results.xml','-logFile','C:\AdventureKing_Reborn\QA\maps_test.log' -PassThru -Wait
"tests exit $($p.ExitCode)" | Set-Content QA\unity_steps3.log

