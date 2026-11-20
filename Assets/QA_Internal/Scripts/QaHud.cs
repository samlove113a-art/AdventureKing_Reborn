using AdventureKing.Gameplay;
using UnityEngine;

namespace AdventureKing.QA
{
    /// <summary>
    /// INTERNAL QA ONLY: on-screen diagnostics for manual smoke tests (FPS, frame time, player state,
    /// HP). Locks the frame rate to the configured target so camera smoothness is judged at the real
    /// target frame rate, as Docs/UNITY_HANDOFF.md requires.
    /// </summary>
    public sealed class QaHud : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private PlayerMotor2D motor;
        [SerializeField] private PlayerInput2D input;
        [SerializeField] private Health2D playerHealth;
        [SerializeField] private Health2D enemyHealth;
        [SerializeField] private QaNpcDialog npcDialog;

        private float smoothedDelta = 1f / 60f;
        private float worstDelta;
        private float worstResetAt;
        private GUIStyle style;

        public float SmoothedFps => 1f / Mathf.Max(0.0001f, smoothedDelta);

        // Optional unattended benchmark for internal QA builds: "-qa-benchmark <seconds>".
        // The player auto-runs left/right and jumps through the touch API; frame stats are logged.
        private float benchmarkSeconds;
        private float benchmarkStart = -1f;
        private readonly System.Collections.Generic.List<float> frameTimes = new System.Collections.Generic.List<float>(8192);
        private PlayerInput2D.TouchAction autoDir = PlayerInput2D.TouchAction.Right;
        private float nextFlip, nextJump;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-qa-benchmark" && float.TryParse(args[i + 1],
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s))
                    benchmarkSeconds = Mathf.Max(5f, s);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            smoothedDelta = Mathf.Lerp(smoothedDelta, dt, 0.05f);
            if (Time.unscaledTime >= worstResetAt) { worstDelta = 0f; worstResetAt = Time.unscaledTime + 2f; }
            worstDelta = Mathf.Max(worstDelta, dt);
            if (benchmarkSeconds > 0f) RunBenchmark(dt);
        }

        private void RunBenchmark(float dt)
        {
            if (benchmarkStart < 0f)
            {
                if (Time.frameCount < 30) return; // skip startup hitches
                benchmarkStart = Time.unscaledTime;
                nextFlip = benchmarkStart + 2f;
                nextJump = benchmarkStart + 0.7f;
                if (input) input.SetTouchHeld(autoDir, true);
                Debug.Log($"[QA-BENCH] start target={targetFrameRate} res={Screen.width}x{Screen.height} " +
                          $"gpu={SystemInfo.graphicsDeviceName} cpu={SystemInfo.processorType}");
                return;
            }
            frameTimes.Add(dt);
            float now = Time.unscaledTime;
            if (input && now >= nextFlip)
            {
                input.SetTouchHeld(autoDir, false);
                autoDir = autoDir == PlayerInput2D.TouchAction.Right ? PlayerInput2D.TouchAction.Left : PlayerInput2D.TouchAction.Right;
                input.SetTouchHeld(autoDir, true);
                nextFlip = now + 2f;
            }
            if (input && now >= nextJump)
            {
                input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, true);
                input.SetTouchHeld(PlayerInput2D.TouchAction.Jump, false);
                nextJump = now + 1.3f;
            }
            if (now - benchmarkStart < benchmarkSeconds) return;

            if (input) input.SetTouchHeld(autoDir, false);
            frameTimes.Sort();
            float sum = 0f;
            int over33 = 0;
            foreach (float f in frameTimes) { sum += f; if (f > 1f / 30f) over33++; }
            float avgFps = frameTimes.Count / sum;
            float p99 = frameTimes[Mathf.Min(frameTimes.Count - 1, (int)(frameTimes.Count * 0.99f))];
            Debug.Log($"[QA-BENCH] frames={frameTimes.Count} avgFPS={avgFps:0.0} p99FrameMs={p99 * 1000f:0.00} " +
                      $"maxFrameMs={frameTimes[frameTimes.Count - 1] * 1000f:0.00} framesOver33ms={over33} " +
                      $"playerHP={(playerHealth ? playerHealth.CurrentHealth : -1)}");
            benchmarkSeconds = 0f;
            Application.Quit(0);
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(8, 8, 430, 190), Texture2D.whiteTexture);
            GUI.color = Color.white;
            string text =
                "QA NỘI BỘ — KHÔNG PHẢI BẢN DEMO\n" +
                $"FPS {SmoothedFps:0} (mục tiêu {targetFrameRate}) | max frame 2s: {worstDelta * 1000f:0.0} ms\n" +
                (motor ? $"Grounded {motor.IsGrounded} | Climbing {motor.IsClimbing} | Vel {motor.Velocity}\n" : "") +
                (input ? $"Input H {input.Horizontal:0.0} V {input.Vertical:0.0} Jump {input.JumpHeld}\n" : "") +
                (playerHealth ? $"Player HP {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}\n" : "") +
                (enemyHealth ? $"Enemy HP {enemyHealth.CurrentHealth}/{enemyHealth.MaxHealth}\n" : "") +
                (npcDialog ? $"NPC interacts {npcDialog.InteractCount}\n" : "") +
                "A/D move · W/S ladder · Space jump · J attack · E talk";
            GUI.Label(new Rect(16, 12, 420, 190), text, style);
        }
    }
}
