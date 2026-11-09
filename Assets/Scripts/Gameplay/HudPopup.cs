using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>Small framed popup (title, message, Đóng) used by HUD shortcuts.</summary>
    public sealed class HudPopup : MonoBehaviour
    {
        [SerializeField] private Text titleText, messageText;
        public bool IsOpen => gameObject.activeSelf;
        public string Message => messageText ? messageText.text : "";

        public void Show(string title, string message)
        {
            if (titleText) titleText.text = title;
            if (messageText) messageText.text = message;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}

