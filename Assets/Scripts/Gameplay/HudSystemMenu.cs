using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>System / settings menu: graphics preset, sound on/off, "Lưu & Thoát" (saves on the server first).</summary>
    public sealed class HudSystemMenu : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text soundLabel;
        public bool IsOpen => panel && panel.activeSelf;

        public void Toggle()
        {
            if (!panel) return;
            panel.SetActive(!panel.activeSelf);
            Refresh();
        }

        public void Close() { if (panel) panel.SetActive(false); }

        public void ToggleSound()
        {
            var a = AdventureAudioDirector.Instance;
            if (a) a.SetAllMuted(!a.Muted);
            Refresh();
        }

        public void SaveAndExit()
        {
            Close();
            AdventureWorldSession.Current?.ExitToLogin();
        }

        private void Refresh()
        {
            var a = AdventureAudioDirector.Instance;
            if (soundLabel) soundLabel.text = a && a.Muted ? "Âm thanh: Tắt" : "Âm thanh: Bật";
        }
    }
}

