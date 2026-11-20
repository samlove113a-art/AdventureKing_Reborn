using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// "Đang tải vào game": a bar + percent + status line over a pixel-art dark-fantasy backdrop. The work behind the bar is real
    /// (server health check, then the four class art prefabs are preloaded with Resources.LoadAsync); the bar never runs faster than
    /// <see cref="minSeconds"/> so the screen is readable. Raises <see cref="Completed"/> once; an unreachable server does not block
    /// (the title screen then says so). Pure UI.
    /// </summary>
    public sealed class LoadingScreen2D : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;          // anchored 0..1 inside its track (we drive anchorMax.x)
        [SerializeField] private Text percentLabel, statusLabel, tipLabel;
        [SerializeField, Min(0.5f)] private float minSeconds = 1.9f;
        [SerializeField] private string[] tips =
        {
            "Mẹo: nhấn E để nói chuyện với Trưởng Làng Nấm.",
            "Mẹo: thuốc hồi phục bán ở Dược Sư Mộc Linh.",
            "Mẹo: hoàn thành nhiệm vụ của map để mở cổng sang map kế.",
        };

        public event Action Completed;
        public float Progress { get; private set; }
        public bool Done { get; private set; }

        private void OnEnable()
        {
            Done = false;
            Progress = 0f;
            Show(0f, "Đang tải...");
            if (tipLabel && tips != null && tips.Length > 0) tipLabel.text = tips[UnityEngine.Random.Range(0, tips.Length)];
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float started = Time.unscaledTime;
            float real = 0f;                      // 0..1 share of the real work that is finished
            string status = "Đang tải...";
            const int Steps = 5;
            string[] classes = { "Swordsman", "Mage", "Assassin", "Archer" };

            status = "Kết nối máy chủ...";
            bool pingDone = false;
            string api = AdventureServerConnection.Instance ? AdventureServerConnection.Instance.BaseUrl : "https://held-dip-recently-assessed.trycloudflare.com/api/v1";
            StartCoroutine(EntryServerPicker.Ping(api, _ => pingDone = true, 3f));
            for (int i = 0; i < Steps; i++)
            {
                if (i == 0) { while (!pingDone && Time.unscaledTime - started < 3.5f) { Tick(started, real, status); yield return null; } }
                else
                {
                    status = "Tải nhân vật " + classes[i - 1] + "...";
                    ResourceRequest rq = Resources.LoadAsync<GameObject>("Chibi2D/" + classes[i - 1] + "_Visual2D");
                    while (!rq.isDone) { Tick(started, real, status); yield return null; }
                }
                real = (i + 1) / (float)Steps;
            }
            status = "Sắp xong...";
            while (Time.unscaledTime - started < minSeconds) { Tick(started, real, status); yield return null; }
            Progress = 1f;
            Show(1f, "Hoàn tất");
            yield return null;
            Done = true;
            Completed?.Invoke();
        }

        private void Tick(float started, float real, string status)
        {
            float time = Mathf.Clamp01((Time.unscaledTime - started) / minSeconds);
            float target = Mathf.Min(real, time);
            Progress = Mathf.MoveTowards(Progress, target, Time.unscaledDeltaTime * 1.2f);
            Show(Progress, status);
        }

        private void Show(float p, string status)
        {
            if (fill) fill.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
            if (percentLabel) percentLabel.text = Mathf.RoundToInt(p * 100f) + "%";
            if (statusLabel) statusLabel.text = status;
        }
    }
}
