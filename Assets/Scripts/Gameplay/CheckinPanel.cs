using System;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    [Serializable]
    public sealed class CheckinRewardView { public string type, label; public long amount; public string[] choices = new string[0]; public string[] choiceNames = new string[0]; }
    [Serializable]
    public sealed class CheckinDayView { public int day; public string icon, state; public bool milestone; public CheckinRewardView[] rewards = new CheckinRewardView[0]; }
    [Serializable]
    public sealed class CheckinStatus
    {
        public bool enabled, canClaimToday;
        public string status, message, todayVn;
        public int cycle, claimedDays;
        public long gold, diamonds;
        public CheckinDayView[] days = new CheckinDayView[0];
        public CheckinRewardView[] granted = new CheckinRewardView[0];
    }

    /// <summary>
    /// "Điểm Danh 7 Ngày": six day cells (3×2) + a large day-7 milestone. Everything shown comes from the server
    /// (rewards, which day is claimable, VN calendar day); the claim button is guarded against double clicks and only
    /// the server's answer marks a day as received. While the owner has not approved the rewards the server keeps
    /// check-in disabled: the panel then shows the plan with an honest "chưa mở" notice and no red dot.
    /// </summary>
    public sealed class CheckinPanel : MonoBehaviour
    {
        [Serializable]
        public sealed class Cell
        {
            public Image frame, icon, done;
            public Text dayLabel, rewardLabel, stateLabel;
        }

        [SerializeField] private GameObject root;
        [SerializeField] private Text statusText;
        [SerializeField] private Cell[] cells = new Cell[7];
        [SerializeField] private Button claimButton, closeButton;
        [SerializeField] private Text claimLabel;
        [SerializeField] private Button[] choiceButtons = new Button[0];
        [SerializeField] private GameObject redDot;
        [SerializeField] private Sprite goldIcon, diamondIcon, chestIcon, skinIcon, cellSprite, cellOnSprite;

        private CheckinStatus last;
        private bool busy;
        private string choice;

        public bool IsOpen => root && root.activeSelf;
        public CheckinStatus Last => last;
        public bool Busy => busy;
        public string Choice => choice;
        public bool RedDotShown => redDot && redDot.activeSelf;

        private AdventureWorldSession World => AdventureWorldSession.Current;

        private void Start()
        {
            if (root) root.SetActive(false);
            if (redDot) redDot.SetActive(false);
            if (claimButton) claimButton.onClick.AddListener(Claim);
            if (closeButton) closeButton.onClick.AddListener(Close);
            for (int i = 0; i < choiceButtons.Length; i++) { int k = i; if (choiceButtons[i]) choiceButtons[i].onClick.AddListener(() => Pick(k)); }
            Invoke(nameof(Refresh), 1f); // red dot only from real server data
        }

        public void Open()
        {
            if (!root) return;
            root.SetActive(true);
            if (statusText) statusText.text = "Đang tải từ máy chủ...";
            if (claimButton) claimButton.interactable = false;
            Refresh();
        }

        public void Close() { if (root) root.SetActive(false); }

        public void Refresh()
        {
            var w = World;
            var net = AdventureServerConnection.Instance;
            if (!w || w.Character == null || !net) return;
            net.GetCheckin(w.Character.characterId, (ok, s, err) =>
            {
                if (!ok || s == null)
                {
                    if (redDot) redDot.SetActive(false); // offline: never a fake dot
                    if (statusText && IsOpen) statusText.text = err ?? "Không tải được điểm danh.";
                    return;
                }
                Render(s);
            });
        }

        public void Pick(int index)
        {
            CheckinDayView d7 = last?.days != null && last.days.Length >= 7 ? last.days[6] : null;
            string[] choices = d7 != null && d7.rewards.Length > 0 ? d7.rewards[0].choices : null;
            if (choices == null || index >= choices.Length) return;
            choice = choices[index];
            for (int i = 0; i < choiceButtons.Length; i++)
                if (choiceButtons[i]) choiceButtons[i].GetComponent<Image>().color = i == index ? Color.white : new Color(0.6f, 0.6f, 0.7f, 1f);
        }

        public void Claim()
        {
            if (busy || last == null || !last.canClaimToday) return;
            var w = World;
            busy = true;
            if (claimButton) claimButton.interactable = false;
            if (claimLabel) claimLabel.text = "Đang nhận...";
            AdventureServerConnection.Instance.ClaimCheckin(w.Character.characterId, choice, (ok, s, err) =>
            {
                busy = false;
                if (!ok || s == null)
                {
                    if (statusText) statusText.text = err ?? "Nhận quà thất bại.";
                    if (claimLabel) claimLabel.text = "Nhận Quà";
                    if (claimButton) claimButton.interactable = last != null && last.canClaimToday;
                    return;
                }
                if (w && w.Character != null) { w.Character.gold = s.gold; w.Character.diamonds = s.diamonds; } // HUD shows server balances
                Render(s);
                if (statusText && s.granted.Length > 0)
                    statusText.text = "Đã nhận: " + string.Join(", ", Array.ConvertAll(s.granted, g => g.label));
            });
        }

        private Sprite IconFor(string icon) => icon switch
        {
            "reward_gold" => goldIcon,
            "reward_diamond" => diamondIcon,
            "reward_bronze_chest" => chestIcon,
            "reward_skin_choice" => skinIcon,
            _ => goldIcon,
        };

        private void Render(CheckinStatus s)
        {
            last = s;
            if (redDot) redDot.SetActive(s.enabled && s.canClaimToday);
            if (!IsOpen) return;
            if (statusText) statusText.text = s.message;
            for (int i = 0; i < cells.Length && i < s.days.Length; i++)
            {
                Cell c = cells[i];
                CheckinDayView d = s.days[i];
                if (c == null) continue;
                bool claimed = d.state == "claimed", claimable = d.state == "claimable";
                if (c.dayLabel) c.dayLabel.text = "Ngày " + d.day;
                if (c.icon) { c.icon.sprite = IconFor(d.icon); c.icon.color = claimed ? new Color(1f, 1f, 1f, 0.45f) : Color.white; }
                if (c.rewardLabel) c.rewardLabel.text = d.rewards.Length > 0 ? d.rewards[0].label : "";
                if (c.done) c.done.gameObject.SetActive(claimed);
                if (c.stateLabel) c.stateLabel.text = claimed ? "Đã nhận" : claimable ? "Có thể nhận" : s.enabled ? "Khóa" : "Chưa mở";
                if (c.frame && cellSprite) c.frame.sprite = claimable && cellOnSprite ? cellOnSprite : cellSprite;
            }
            // day-7 choice buttons show the eligible item names
            CheckinDayView d7 = s.days.Length >= 7 ? s.days[6] : null;
            string[] choices = d7 != null && d7.rewards.Length > 0 ? d7.rewards[0].choices : new string[0];
            string[] names = d7 != null && d7.rewards.Length > 0 ? d7.rewards[0].choiceNames : new string[0];
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (!choiceButtons[i]) continue;
                choiceButtons[i].gameObject.SetActive(i < choices.Length);
                if (i < choices.Length) choiceButtons[i].GetComponentInChildren<Text>().text = i < names.Length ? names[i] : choices[i];
            }
            bool day7Next = s.claimedDays == 6;
            if (claimButton) claimButton.interactable = s.canClaimToday && !busy && (!day7Next || !string.IsNullOrEmpty(choice));
            if (claimLabel) claimLabel.text = !s.enabled ? "Chưa mở" : s.canClaimToday ? (day7Next && string.IsNullOrEmpty(choice) ? "Chọn quà ngày 7" : "Nhận Quà") : "Đã nhận hôm nay";
        }
    }
}
