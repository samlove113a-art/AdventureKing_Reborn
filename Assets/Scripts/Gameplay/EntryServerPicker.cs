using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    [Serializable]
    public sealed class EntryServerInfo
    {
        public string id = "s1";
        public string displayName = "Server 1 - Vùng Đất Khởi Nguyên";
        [Tooltip("API base url, e.g. http://host:5080/api/v1. Empty = the address the game is configured with (ServerEndpoint.txt / the default).")]
        public string apiUrl = "";
    }

    public enum ServerState { Checking, Online, Offline }

    /// <summary>
    /// "Chọn Server" bar of the title screen: shows the selected server's name and live state (pings GET {api}/health), a button opens a
    /// list of servers. Adding a server later = one more entry in <see cref="servers"/> (no code change). Selecting a server points
    /// <see cref="AdventureServerConnection"/> at it. Pure UI.
    /// </summary>
    public sealed class EntryServerPicker : MonoBehaviour
    {
        [SerializeField] private EntryServerInfo[] servers = { new EntryServerInfo() };
        [SerializeField] private Text nameLabel, stateLabel;
        [SerializeField] private Image stateDot;
        [SerializeField] private GameObject listPanel;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private Button rowTemplate;
        [SerializeField] private Button openButton;
        [SerializeField, Min(1f)] private float timeoutSeconds = 3f;

        public EntryServerInfo Selected { get; private set; }
        public ServerState State { get; private set; } = ServerState.Checking;
        public int ServerCount => servers == null ? 0 : servers.Length;
        public bool ListOpen => listPanel && listPanel.activeSelf;
        public static ServerState LastState { get; private set; } = ServerState.Checking;
        private Coroutine pinging;

        private void OnEnable()
        {
            if (Selected == null && servers != null && servers.Length > 0) Select(0);
            else Refresh();
            if (listPanel) listPanel.SetActive(false);
        }

        private void Awake()
        {
            if (openButton) openButton.onClick.AddListener(ToggleList);
            BuildRows();
        }

        private void BuildRows()
        {
            if (!rowTemplate || !listContent || servers == null) return;
            rowTemplate.gameObject.SetActive(false);
            for (int i = 0; i < servers.Length; i++)
            {
                int index = i;
                Button row = Instantiate(rowTemplate, listContent);
                row.gameObject.SetActive(true);
                row.name = "ServerRow_" + servers[i].id;
                Text t = row.GetComponentInChildren<Text>();
                if (t) t.text = servers[i].displayName;
                row.onClick.AddListener(() => { Select(index); if (listPanel) listPanel.SetActive(false); });
            }
        }

        public void ToggleList() { if (listPanel) listPanel.SetActive(!listPanel.activeSelf); }

        public void Select(int index)
        {
            if (servers == null || index < 0 || index >= servers.Length) return;
            Selected = servers[index];
            string url = string.IsNullOrWhiteSpace(Selected.apiUrl) ? null : Selected.apiUrl.Trim();
            if (url != null && AdventureServerConnection.Instance) AdventureServerConnection.Instance.SetBaseUrl(url);
            Refresh();
        }

        /// <summary>Re-checks the selected server's state.</summary>
        public void Refresh()
        {
            if (nameLabel && Selected != null) nameLabel.text = Selected.displayName;
            if (pinging != null) StopCoroutine(pinging);
            if (!isActiveAndEnabled) return;
            SetState(ServerState.Checking);
            pinging = StartCoroutine(Check());
        }

        private IEnumerator Check()
        {
            bool ok = false, done = false;
            yield return Ping(CurrentApi(), r => { ok = r; done = true; }, timeoutSeconds);
            if (!done) ok = false;
            SetState(ok ? ServerState.Online : ServerState.Offline);
            pinging = null;
        }

        private string CurrentApi()
        {
            if (Selected != null && !string.IsNullOrWhiteSpace(Selected.apiUrl)) return Selected.apiUrl.Trim().TrimEnd('/');
            return AdventureServerConnection.Instance ? AdventureServerConnection.Instance.BaseUrl : "http://127.0.0.1:5080/api/v1";
        }

        /// <summary>GET {api}/health with a timeout; reports reachability. Used by the loading screen too.</summary>
        public static IEnumerator Ping(string apiBase, Action<bool> result, float timeout = 3f)
        {
            using UnityWebRequest req = UnityWebRequest.Get(apiBase.TrimEnd('/') + "/health");
            req.timeout = Mathf.CeilToInt(timeout);
            yield return req.SendWebRequest();
            result?.Invoke(req.result == UnityWebRequest.Result.Success);
        }

        private void SetState(ServerState s)
        {
            State = s;
            LastState = s;
            if (stateLabel) stateLabel.text = s == ServerState.Online ? "Hoạt động" : s == ServerState.Offline ? "Không kết nối được" : "Đang kiểm tra...";
            if (stateDot) stateDot.color = s == ServerState.Online ? new Color(0.35f, 0.9f, 0.5f) : s == ServerState.Offline ? new Color(0.95f, 0.35f, 0.3f) : new Color(1f, 0.82f, 0.3f);
            if (stateLabel) stateLabel.color = stateDot ? stateDot.color : Color.white;
        }
    }
}
