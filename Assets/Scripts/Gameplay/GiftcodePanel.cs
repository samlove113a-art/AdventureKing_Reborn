using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// "Giftcode": one input (A–Z, 0–9; case-insensitive) and Đổi Quà. The server checks everything (exists, expiry,
    /// uses left, once per account, level) and pays Gold + common items; this window only shows its answer.
    /// No code is built in: production has none until the owner approves a campaign.
    /// </summary>
    public sealed class GiftcodePanel : CodePanel
    {
        public InputField Input { get; private set; }
        public GiftcodeResult Last { get; private set; }
        private Button redeem;

        protected override void Build()
        {
            BuildShell("Giftcode", new Vector2(900f, 520f));
            Txt(card, "Hint", "Nhập mã quà tặng từ sự kiện chính thức của Adventure King.", 22, Cream, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(760f, 34f), TextAnchor.MiddleCenter);
            Image field = Make<Image>(card, "CodeField", new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(620f, 84f));
            field.sprite = cellSprite; field.type = cellSprite ? Image.Type.Sliced : Image.Type.Simple; field.color = new Color(0.95f, 0.92f, 0.84f, 1f);
            Text placeholder = Txt(field.transform, "Placeholder", "Nhập giftcode...", 28, new Color(0.45f, 0.4f, 0.35f, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 70f), TextAnchor.MiddleCenter);
            placeholder.fontStyle = FontStyle.Italic;
            Text text = Txt(field.transform, "Text", "", 32, new Color(0.2f, 0.13f, 0.07f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 70f), TextAnchor.MiddleCenter, true);
            text.resizeTextForBestFit = false; text.supportRichText = false;
            Input = field.gameObject.AddComponent<InputField>();
            Input.targetGraphic = field;
            Input.textComponent = text;
            Input.placeholder = placeholder;
            Input.characterLimit = 24;
            Input.characterValidation = InputField.CharacterValidation.Alphanumeric;
            Input.onValidateInput += (s, i, c) => char.ToUpperInvariant(c);
            Input.onEndEdit.AddListener(_ =>
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Redeem();
            });
            redeem = Btn(card, "Redeem", "Đổi Quà", new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(300f, 72f), Amber, Redeem, 26);
        }

        public override void Open()
        {
            base.Open();
            Input.text = "";
            Last = null;
            Input.ActivateInputField();
        }

        public void Redeem()
        {
            string code = Input ? Input.text.Trim() : "";
            if (Busy) return;
            if (code.Length == 0) { Message("Hãy nhập giftcode."); return; }
            if (CharacterId == null || !Server) { Message("Chưa kết nối máy chủ."); return; }
            Busy = true;
            redeem.interactable = false;
            Message("Đang kiểm tra mã...");
            Server.RedeemGiftcode(CharacterId, code, (ok, r, err) =>
            {
                Busy = false;
                redeem.interactable = true;
                if (ok && r != null) { Last = r; ShowGold(r.balance); Message("<color=#a8f0a0>Đổi quà thành công:</color> " + r.reward); Input.text = ""; }
                else Message(err ?? "Không đổi được giftcode.");
            });
        }
    }
}
