$u="C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe"
Set-Location C:\AdventureKing_Reborn
$p = Start-Process -FilePath $u -ArgumentList '-projectPath','C:\AdventureKing_Reborn','-runTests','-testPlatform','PlayMode','-testResults','C:\AdventureKing_Reborn\QA\full_results.xml','-logFile','C:\AdventureKing_Reborn\QA\full_test.log' -PassThru -Wait
"full exit $($p.ExitCode)" | Set-Content QA\unity_full.log

