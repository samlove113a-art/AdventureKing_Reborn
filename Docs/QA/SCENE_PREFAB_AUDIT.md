# Adventure King — scene and prefab static audit

This branch adds a **read-only** QA command, based on `feat/ak-map-forge-m02`.

In Unity 6000.6.7f1, choose **AdventureKing → QA (internal) → Audit Scenes and Prefabs**. The script writes `Logs/AKScenePrefabAudit.txt` (ignored by Git). It checks:
- All enabled Build Settings scene files exist; at least one scene is enabled.
- Internal `Assets/QA_Internal` scenes are not accidentally enabled in a production build.
- Every serialized `m_Script` GUID in YAML scenes and prefabs resolves through Unity's AssetDatabase.
- Prefabs/scenes skipped because they are not readable YAML are clearly reported.

Command-line integration after importing the project:
```powershell
& "C:\\Program Files\\Unity\\Hub\\Editor\\6000.6.7f1\\Editor\\Unity.exe" -batchmode -nographics -quit -projectPath "C:\\path\\to\\AdventureKing_Reborn" -executeMethod AdventureKing.EditorTools.AKScenePrefabAudit.RunBatch -logFile "C:\\path\\to\\audit-unity.log"
```

**Important:** Do not treat a clean static audit as a verified build. It does not check missing assigned Inspector references, animation quality, scene behaviour, compile success, multiplayer/server authority, mobile input or FPS. Claude Code must run the menu, inspect the report and separately run EditMode/PlayMode tests, a Windows player build and real-device Android tests. Do not merge into `main` until those pass.
