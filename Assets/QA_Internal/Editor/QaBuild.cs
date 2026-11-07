using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AdventureKing.QA.Editor
{
    /// <summary>
    /// INTERNAL QA ONLY: builds the grey-box sandbox to Builds/QA_Windows (git-ignored) to verify the
    /// player compiles and to measure frame times on real hardware. This executable is a test probe,
    /// never a deliverable, and the sandbox is passed explicitly so it never enters Build Settings.
    /// </summary>
    public static class QaBuild
    {
        public const string WindowsOutput = "Builds/QA_Windows/AdventureKing_QA_Internal.exe";

        [MenuItem("AdventureKing/QA (internal)/Build Windows QA Probe")]
        public static void BuildWindowsMenu() => BuildWindows();

        public static void BuildWindowsFromCommandLine() => EditorApplication.Exit(BuildWindows() ? 0 : 1);

        public static bool BuildWindows()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { QaSceneBuilder.ScenePath },
                locationPathName = WindowsOutput,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            Debug.Log($"[QA-BUILD] result={s.result} errors={s.totalErrors} warnings={s.totalWarnings} " +
                      $"size={s.totalSize / (1024 * 1024)}MB time={s.totalTime.TotalSeconds:0.0}s");
            return s.result == BuildResult.Succeeded;
        }
    }
}
