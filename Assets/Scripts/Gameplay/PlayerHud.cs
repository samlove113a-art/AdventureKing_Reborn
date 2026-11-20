using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Player frame (top-left): class portrait, name, level, HP / MP bars and Gold / Diamonds — all read from real
    /// state every frame: the server-confirmed character snapshot (name, level, Gold, Diamonds; refreshed after every
    /// purchase), the player's Health2D and PlayerMana. Bars ease towards the value. Also shows the map-name banner on
    /// arrival (fades out by itself) and hides the keyboard hint while a dialog / menu covers the bottom of the screen.
    /// </summary>
    public sealed class PlayerHud : MonoBehaviour
    {
        [SerializeField] private Image avatar;
        [SerializeField] private Sprite[] portraits = new Sprite[4];
        [SerializeField] private Text nameLabel, levelLabel, hpText, mpText, goldText, diamondText;
        [SerializeField] private Image hpFill, mpFill;
        [SerializeField] private GameObject keyboardHint;
        [SerializeField] private CanvasGroup mapBanner;
        [SerializeField] private Text mapBannerText;
        [SerializeField] private string mapDisplayName;
        [SerializeField] private GameObject[] coveringPanels = new GameObject[0];

        private Health2D health;
        private ElderGreeter elder;
        private bool elderSearched;
        private PlayerMana mana;
        private float hpShown = 1f, mpShown = 1f;

        /// <summary>Server-confirmed VIP level of the account (0 = none). Set by RefreshVip / giftcode / top-up; shown next to the level.</summary>
        public static int VipLevel { get; set; }
        public string LevelShown => levelLabel ? levelLabel.text : "";

        /// <summary>Asks the server for the current VIP level (after login, a giftcode or a top-up) and updates the HUD.</summary>
        public static void RefreshVip()
        {
            var s = AdventureServerConnection.Instance;
            if (s) s.GetVip((ok, level, configured, err) => { if (ok) VipLevel = level; });
        }

        public string GoldShown => goldText ? goldText.text : "";
        public string DiamondsShown => diamondText ? diamondText.text : "";
        public float HpFraction => hpFill ? hpFill.fillAmount : 0f;
        public float MpFraction => mpFill ? mpFill.fillAmount : 0f;
        public bool MapBannerVisible => mapBanner && mapBanner.alpha > 0.01f;

        private IEnumerator Start()
        {
            if (mapBanner) mapBanner.alpha = 0f;
            float until = Time.time + 5f;
            while ((!AdventureWorldSession.Current || AdventureWorldSession.Current.Character == null) && Time.time < until) yield return null;
            VipLevel = 0; RefreshVip();                                   // never show the previous account's VIP
            AdventureWorldSession world = AdventureWorldSession.Current;
            if (world && world.Player)
            {
                health = world.Player.GetComponent<Health2D>();
                mana = world.Player.GetComponent<PlayerMana>();
            }
            if (world && world.Character != null && avatar)
            {
                int k = (int)world.Character.heroClass;
                if (k >= 0 && k < portraits.Length && portraits[k]) avatar.sprite = portraits[k];
            }
            if (mapBanner && mapBannerText)
            {
                mapBannerText.text = mapDisplayName;
                yield return Banner();
            }
        }

        private IEnumerator Banner()
        {
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime) { mapBanner.alpha = t / 0.5f; yield return null; }
            mapBanner.alpha = 1f;
            yield return new WaitForSecondsRealtime(2.6f);
            for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime) { mapBanner.alpha = 1f - t / 0.8f; yield return null; }
            mapBanner.alpha = 0f;
        }

        private static string Money(long v) => v.ToString("#,0").Replace(',', '.');

        private void Update()
        {
            AdventureWorldSession world = AdventureWorldSession.Current;
            AdventureCharacterSnapshot c = world ? world.Character : null;
            if (c != null)
            {
                if (nameLabel) nameLabel.text = c.characterName;
                if (levelLabel)
                {
                    levelLabel.text = VipLevel > 0 ? "Lv." + c.level + "  <color=#ffd34d>VIP " + VipLevel + "</color>" : "Lv." + c.level;
                    levelLabel.supportRichText = true; levelLabel.horizontalOverflow = HorizontalWrapMode.Overflow; levelLabel.verticalOverflow = VerticalWrapMode.Overflow;
                }
                if (goldText) goldText.text = Money(c.gold);
                if (diamondText) diamondText.text = Money(c.diamonds);
            }
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f);
            if (health)
            {
                hpShown = Mathf.Lerp(hpShown, health.CurrentHealth / (float)health.MaxHealth, k);
                if (hpFill) hpFill.fillAmount = hpShown;
                if (hpText) hpText.text = health.CurrentHealth + " / " + health.MaxHealth;
            }
            if (mana)
            {
                mpShown = Mathf.Lerp(mpShown, mana.Current / Mathf.Max(1f, mana.Max), k);
                if (mpFill) mpFill.fillAmount = mpShown;
                if (mpText) mpText.text = Mathf.FloorToInt(mana.Current) + " / " + Mathf.RoundToInt(mana.Max);
            }
            if (keyboardHint)
            {
                bool covered = (world && (world.ConfirmOpen || world.LoadingVisible));
                foreach (GameObject g in coveringPanels) covered |= g && g.activeInHierarchy;
                if (!elderSearched) { elder = FindAnyObjectByType<ElderGreeter>(); elderSearched = true; }
                if (elder && elder.DialogOpen) covered = true;
                var merchant = FashionMerchant.Active;
                if (merchant && merchant.MenuOpen) covered = true;
                if (keyboardHint.activeSelf == covered) keyboardHint.SetActive(!covered);
            }
        }
    }
}

