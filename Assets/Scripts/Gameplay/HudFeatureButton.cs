using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// A HUD shortcut (Sự kiện, Điểm danh, Thư, VIP …). Features whose backend does not exist yet say so honestly in
    /// a small popup — they never pretend to work, and they never show a red notification dot.
    /// VIP asks the real server (/vip) and shows its answer.
    /// </summary>
    public sealed class HudFeatureButton : MonoBehaviour
    {
        [SerializeField] private string title;
        [TextArea, SerializeField] private string message;
        [SerializeField] private bool queryVip;
        [SerializeField] private HudPopup popup;

        public string Title => title;

        public void Open()
        {
            if (!popup) return;
            if (queryVip && AdventureServerConnection.Instance)
            {
                popup.Show(title, "Đang hỏi máy chủ...");
                AdventureServerConnection.Instance.GetVip((ok, level, configured, err) =>
                    popup.Show(title, !ok ? (err ?? "Không tải được VIP.")
                        : !configured ? $"VIP hiện tại: {level}. Mốc nạp VIP 1–14 chưa được chủ dự án phê duyệt, nên chưa có cấp VIP nào được mở."
                        : $"VIP hiện tại: {level}."));
                return;
            }
            popup.Show(title, message);
        }
    }
}

