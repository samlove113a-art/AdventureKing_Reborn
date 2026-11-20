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
                {
                    string vip = !ok ? (err ?? "Không tải được VIP.")
                        : !configured ? $"VIP hiện tại: {level}. Mốc nạp VIP 1–14 chưa được chủ dự án phê duyệt, nên chưa có cấp VIP nào được mở."
                        : $"VIP hiện tại: {level}.";
                    popup.Show(title, vip);
                    var w = AdventureWorldSession.Current;
                    if (!ok || !w || w.Character == null) return;
                    // nameplate frames the server says this character owns (VIP / Boss Hunter / special)
                    AdventureServerConnection.Instance.GetTitles(w.Character.characterId, (tok, t, terr) =>
                    {
                        if (!tok || t == null || !popup.IsOpen) return;
                        string frames = t.owned.Length == 0 ? t.note : "Khung tên: " + string.Join(", ", System.Array.ConvertAll(t.owned, o => o.name));
                        popup.Show(title, vip + "\n" + frames);
                    });
                });
                return;
            }
            popup.Show(title, message);
        }
    }
}

