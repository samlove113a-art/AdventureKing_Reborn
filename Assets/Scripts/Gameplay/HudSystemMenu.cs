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
        [SerializeField] private Text timeLabel;
        public bool IsOpen => panel && panel.activeSelf;

        /// <summary>Day/night: Theo giờ VN -> Luôn ngày -> Luôn đêm (display preference only).</summary>
        public void CycleTimeOfDay()
        {
            DayNightCycle.CycleMode();
            Refresh();
        }

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

        [SerializeField] private Text musicLabel;
        [SerializeField] private Text musicVolumeLabel;
        [SerializeField] private Text effectsVolumeLabel;

        public void ToggleMusic()
        {
            var a = AdventureAudioDirector.Instance;
            if (a) a.SetMusicOn(!a.MusicOn);
            Refresh();
        }

        public void CycleMusicVolume()
        {
            var a = AdventureAudioDirector.Instance;
            if (a) a.CycleMusicVolume();
            Refresh();
        }

        public void CycleEffectsVolume()
        {
            var a = AdventureAudioDirector.Instance;
            if (a) a.CycleEffectsVolume();
            Refresh();
        }

        public string MusicLabelText => musicLabel ? musicLabel.text : "";

        private void Refresh()
        {
            var a = AdventureAudioDirector.Instance;
            if (soundLabel) soundLabel.text = a && a.Muted ? "Âm thanh: Tắt" : "Âm thanh: Bật";
            if (musicLabel) musicLabel.text = a && !a.MusicOn ? "Nhạc nền: Tắt" : "Nhạc nền: Bật";
            if (musicVolumeLabel) musicVolumeLabel.text = "Âm lượng nhạc: " + Mathf.RoundToInt((a ? a.MusicVolume : 0f) * 100f) + "%";
            if (effectsVolumeLabel) effectsVolumeLabel.text = "Âm lượng hiệu ứng: " + Mathf.RoundToInt((a ? a.EffectsVolume : 0f) * 100f) + "%";
            if (timeLabel) timeLabel.text = DayNightCycle.ModeLabel;
        }
    }
}

