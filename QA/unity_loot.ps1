$u="C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe"
Set-Location C:\AdventureKing_Reborn
$p = Start-Process -FilePath $u -ArgumentList '-projectPath','C:\AdventureKing_Reborn','-runTests','-testPlatform','PlayMode','-testFilter','AdventureKing.Tests.EnhancePanelTests','-testResults','C:\AdventureKing_Reborn\QA\loot_results.xml','-logFile','C:\AdventureKing_Reborn\QA\loot_test.log' -PassThru -Wait
"loot exit $($p.ExitCode)" | Set-Content QA\unity_loot.log


