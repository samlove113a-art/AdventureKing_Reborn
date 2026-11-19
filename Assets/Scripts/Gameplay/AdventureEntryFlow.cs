using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace AdventureKing.Gameplay
{
    /// <summary>Splash = the logo intro. Order of the player's journey: Splash -> Loading -> Title -> Authentication -> CharacterSelection.</summary>
    public enum EntryScreen { Splash, Authentication, CharacterSelection, Loading, Title }

    public enum SignInMethod { PasswordLogin, PasswordRegister, Google, Facebook, Guest }

    /// <summary>
    /// External service contract only. A real secure backend and OAuth SDK are REQUIRED.
    /// No default implementation silently authenticates or creates an account.
    /// </summary>
    public interface IAdventureAccountService
    {
        void SignIn(SignInMethod method, string account, string password,
            Action<bool, string> completed);
        void CreateCharacter(string characterName, HeroClass heroClass,
            Action<bool, string> completed);
    }

    /// <summary>
    /// Splash -> real authentication -> 4 class selection -> gameplay scene.
    /// Link scene roots/buttons in Unity Inspector. Before backend integration
    /// all auth actions fail closed rather than pretending a real login worked.
    /// </summary>
    public sealed class AdventureEntryFlow : MonoBehaviour
    {
        [Header("Screens")]
        [SerializeField] private GameObject splashRoot;
        [SerializeField] private GameObject loadingRoot;
        [SerializeField] private GameObject titleRoot;
        [SerializeField] private GameObject authRoot;
        [SerializeField] private GameObject selectionRoot;
        [SerializeField, Min(0f)] private float splashDurationSeconds = 4.5f;   // safety net: the intro ends itself in ~3 s
        [SerializeField] private LogoAssemblyIntro intro;
        [SerializeField] private LoadingScreen2D loading;
        [SerializeField] private EntryServerPicker serverPicker;
        [Tooltip("Optional: login / register tabs (highlighted by mode).")]
        [SerializeField] private Image loginTab, registerTab;
        [SerializeField] private Sprite tabSprite, tabOnSprite;

        [Header("Branding")]
        [SerializeField] private Image splashLogo;
        [SerializeField] private Sprite officialAdventureKingLogo;

        [Header("Authentication UI")]
        [SerializeField] private InputField accountInput;
        [SerializeField] private InputField passwordInput;
        [SerializeField] private InputField confirmPasswordInput;
        [SerializeField] private Text statusText;
        [Tooltip("Status line on the hero hall (create/enter/name errors). Falls back to statusText.")]
        [SerializeField] private Text selectionStatusText;
        [SerializeField] private GameObject registerFields;
        [Tooltip("Optional: card title / mode switch link / buttons, updated when switching login <-> register.")]
        [SerializeField] private Text authTitle;
        [SerializeField] private Text registerToggleLabel;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button registerButton;
        public bool RegisterMode { get; private set; }

        [Header("Four hero classes")]
        [SerializeField] private Text heroName;
        [SerializeField] private Text heroDescription;
        [SerializeField] private Text heroStats;
        [SerializeField] private InputField characterNameInput;
        [SerializeField] private Text[] slotLabels = new Text[4];
        [SerializeField] private Text existingCharacterDetails;
        [SerializeField] private HeroSelectionPreview2D heroPreview;
        // Selection highlight: class cards (HeroClass order) and slot buttons swap to the gold-rimmed "on" skin.
        [SerializeField] private Image[] classCards = new Image[0];
        [SerializeField] private Image[] slotCards = new Image[0];
        [SerializeField] private Sprite cardSprite, cardOnSprite;

        private void MarkSelected(Image[] cards, int index)
        {
            if (cards == null || !cardSprite || !cardOnSprite) return;
            for (int i = 0; i < cards.Length; i++)
            {
                if (!cards[i]) continue;
                bool on = i == index;
                cards[i].sprite = on ? cardOnSprite : cardSprite;
                cards[i].rectTransform.localScale = on ? new Vector3(1.04f, 1.04f, 1f) : Vector3.one;
            }
        }

        /// <summary>True when that class card currently shows the selected look (for tests).</summary>
        public bool IsCardHighlighted(HeroClass c) =>
            classCards != null && (int)c < classCards.Length && classCards[(int)c] && classCards[(int)c].sprite == cardOnSprite && cardOnSprite;

        [Header("Gameplay")]
        [SerializeField] private string gameplaySceneName = "MainGameScene";

        private IAdventureAccountService accounts;
        private IAdventureProfileService profiles;
        private AdventureAccountProfile loadedProfile;
        private bool busy, authenticated;
        private int selectedSlotIndex;
        private HeroClass selectedClass = HeroClass.Swordsman;
        private EntryScreen screen;

        public EntryScreen CurrentScreen => screen;
        public HeroClass SelectedClass => selectedClass;
        public AdventureAccountProfile LoadedProfile => loadedProfile;
        public int SelectedSlotIndex => selectedSlotIndex;

        /// <summary>Set by the world when the player signs out, so the studio splash is not replayed.</summary>
        internal static bool ReturningFromWorld;

        private void Awake()
        {
            if (splashLogo && officialAdventureKingLogo)
                splashLogo.sprite = officialAdventureKingLogo;
            if (!HotUpdateBanner.FindAnyObjectByType<HotUpdateBanner>()) HotUpdateBanner.Create(null, null);   // checks for new maps / events / skins on the login screen
            if (characterNameInput) characterNameInput.characterLimit = 12;   // the typing box stops at 12: longer names would not fit inside the name-plate frames
            SetRegisterMode(false);
            if (ReturningFromWorld)
            {
                ReturningFromWorld = false;
                Show(EntryScreen.Authentication);
                SetStatus("Đã lưu tiến trình và đăng xuất.");
            }
            else Show(EntryScreen.Splash);
        }

        private void OnEnable()
        {
            if (intro) intro.Finished += OnIntroFinished;
            if (loading) loading.Completed += OnLoadingCompleted;
        }

        private void OnDisable()
        {
            if (intro) intro.Finished -= OnIntroFinished;
            if (loading) loading.Completed -= OnLoadingCompleted;
        }

        private void OnIntroFinished() { if (screen == EntryScreen.Splash) Show(EntryScreen.Loading); }
        private void OnLoadingCompleted() { if (screen == EntryScreen.Loading) Show(EntryScreen.Title); }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(splashDurationSeconds);
            if (screen == EntryScreen.Splash) Show(EntryScreen.Loading);   // safety net if the intro component is missing
        }

        /// <summary>Intro (logo assembly) -> Loading, e.g. from a tap. The intro component also calls this when it finishes.</summary>
        public void SkipIntro()
        {
            if (screen != EntryScreen.Splash) return;
            if (intro) intro.Skip(); else Show(EntryScreen.Loading);
        }

        /// <summary>Title screen "BẮT ĐẦU": on to the login / register card.</summary>
        public void StartGame()
        {
            if (screen == EntryScreen.Title) Show(EntryScreen.Authentication);
        }

        public EntryServerPicker ServerPicker => serverPicker;

        /// <summary>
        /// Call from a trusted startup composition root after configuring real auth.
        /// Do not wire an editor-only/mock provider into production builds.
        /// </summary>
        public void SetAccountService(IAdventureAccountService service)
        {
            accounts = service;
            // Production adapter should implement both authentication and profile APIs.
            profiles = service as IAdventureProfileService;
        }

        public void SetProfileService(IAdventureProfileService service)
        {
            profiles = service;
        }

        /// <summary>Developer / test shortcut: jump over the whole pre-login sequence (intro, loading, title) straight to the login card.</summary>
        public void SkipSplash()
        {
            if (screen == EntryScreen.Splash || screen == EntryScreen.Loading || screen == EntryScreen.Title) Show(EntryScreen.Authentication);
        }

        public void SetRegisterMode(bool register)
        {
            RegisterMode = register;
            if (registerFields) registerFields.SetActive(register);
            if (authTitle) authTitle.text = register ? "Tạo tài khoản" : "Đăng nhập";
            if (registerToggleLabel) registerToggleLabel.text = register ? "Đã có tài khoản? Đăng nhập" : "Chưa có tài khoản? Tạo mới";
            // one clear primary action per mode
            if (loginButton) loginButton.gameObject.SetActive(!register);
            if (registerButton) registerButton.gameObject.SetActive(register);
            if (loginTab && tabSprite && tabOnSprite) loginTab.sprite = register ? tabSprite : tabOnSprite;
            if (registerTab && tabSprite && tabOnSprite) registerTab.sprite = register ? tabOnSprite : tabSprite;
            SetStatus("");
        }

        /// <summary>"Chưa có tài khoản? Tạo mới" / "Đã có tài khoản? Đăng nhập".</summary>
        public void ToggleRegisterMode() => SetRegisterMode(!RegisterMode);
        public void ShowLoginTab() => SetRegisterMode(false);
        public void ShowRegisterTab() => SetRegisterMode(true);

        public void LoginPassword() => PasswordAuth(SignInMethod.PasswordLogin);
        public void RegisterPassword() => PasswordAuth(SignInMethod.PasswordRegister);
        public void ContinueWithGoogle() => SubmitAuth(SignInMethod.Google, "", "");
        public void ContinueWithFacebook() => SubmitAuth(SignInMethod.Facebook, "", "");
        public void ContinueAsGuest() => SubmitAuth(SignInMethod.Guest, "", "");

        private void PasswordAuth(SignInMethod method)
        {
            string account = accountInput ? accountInput.text.Trim() : "";
            string password = passwordInput ? passwordInput.text : "";
            if (account.Length < 3 || password.Length < 8)
            {
                SetStatus("Tài khoản cần ít nhất 3 ký tự, mật khẩu ít nhất 8 ký tự.");
                return;
            }
            if (method == SignInMethod.PasswordRegister &&
                (!confirmPasswordInput || password != confirmPasswordInput.text))
            {
                SetStatus("Mật khẩu xác nhận không khớp.");
                return;
            }
            SubmitAuth(method, account, password);
        }

        private void SubmitAuth(SignInMethod method, string account, string password)
        {
            if (busy) return;
            if (accounts == null)
            {
                SetStatus("Chưa kết nối máy chủ đăng nhập. Không thể đăng nhập thật.");
                return;
            }

            busy = true;
            SetStatus("Đang xác thực...");
            try
            {
                accounts.SignIn(method, account, password, (success, message) =>
                {
                    // Provider adapter must marshal callback to Unity's main thread.
                    busy = false;
                    authenticated = success;
                    if (success) LoadExistingCharacters();
                    else SetStatus(string.IsNullOrEmpty(message) ? "Đăng nhập thất bại." : message);
                });
            }
            catch (Exception ex)
            {
                busy = false;
                SetStatus("Đăng nhập thất bại. Kiểm tra dịch vụ xác thực.");
                Debug.LogError("Adventure King authentication adapter failure: " + ex.Message);
            }
        }

        public void SelectSwordsman() => SelectClass(HeroClass.Swordsman);
        public void SelectMage() => SelectClass(HeroClass.Mage);
        public void SelectAssassin() => SelectClass(HeroClass.Assassin);
        public void SelectArcher() => SelectClass(HeroClass.Archer);

        public void SelectClass(HeroClass heroClass)
        {
            selectedClass = heroClass;
            MarkSelected(classCards, (int)heroClass);
            if (heroPreview) heroPreview.ShowClass(heroClass);
            HeroClassStats stats = HeroClassCatalog.Get(heroClass);
            if (heroName) heroName.text = stats.displayName;
            if (heroDescription) heroDescription.text = stats.description;
            if (heroStats)
                heroStats.text = "HP " + stats.startingHealth + "  |  DMG " +
                    stats.attackDamage + "  |  DEF " + stats.startingDefense +
                    "  |  DPS " + stats.NominalDamagePerSecond.ToString("0.0");
        }

        /// <summary>
        /// Called after a real account login. Existing characters are loaded from the
        /// backend before any class/slot UI is shown. Client storage is NOT the save.
        /// </summary>
        public void LoadExistingCharacters()
        {
            if (!authenticated || busy) return;
            if (profiles == null)
            {
                SetStatus("Máy chủ chưa hỗ trợ lưu nhân vật. Không thể tiếp tục.");
                return;
            }

            busy = true;
            SetStatus("Đang tải nhân vật đã lưu...");
            try
            {
                profiles.LoadProfile((success, result, message) =>
                {
                    // Backend adapter must invoke on Unity's main thread.
                    busy = false;
                    if (!success || result == null || !IsValidProfile(result))
                    {
                        SetStatus(string.IsNullOrEmpty(message)
                            ? "Không tải được dữ liệu nhân vật từ máy chủ." : message);
                        return;
                    }

                    loadedProfile = result;
                    selectedSlotIndex = FindInitialSlot(result);
                    Show(EntryScreen.CharacterSelection);
                    SelectSlot(selectedSlotIndex);
                    RenderSlots();
                });
            }
            catch (Exception ex)
            {
                busy = false;
                SetStatus("Kết nối dữ liệu nhân vật thất bại.");
                Debug.LogError("Profile fetch failed: " + ex.Message);
            }
        }

        public void SelectSlot0() => SelectSlot(0);
        public void SelectSlot1() => SelectSlot(1);
        public void SelectSlot2() => SelectSlot(2);
        public void SelectSlot3() => SelectSlot(3);

        public void SelectSlot(int slotIndex)
        {
            if (!authenticated || loadedProfile == null || slotIndex < 0 || slotIndex > 3)
                return;

            selectedSlotIndex = slotIndex;
            MarkSelected(slotCards, slotIndex);
            AdventureCharacterSnapshot existing = FindSlot(slotIndex);
            if (existing != null)
            {
                SelectClass(existing.heroClass);
                if (existingCharacterDetails)
                    existingCharacterDetails.text = "Nhân vật: " + existing.characterName +
                        " | Lv." + existing.level + " | Skin: " +
                        (existing.appearance == null || string.IsNullOrEmpty(existing.appearance.equippedSkinId)
                            ? "Mặc định" : existing.appearance.equippedSkinId);
                if (characterNameInput) characterNameInput.text = existing.characterName;
            }
            else
            {
                if (existingCharacterDetails)
                    existingCharacterDetails.text = "Ô nhân vật trống — chọn nghề và đặt tên.";
                if (characterNameInput) characterNameInput.text = "";
            }

            // Art integration: apply saved appearance IDs to the actual 3D preview model.
            // ID strings alone do not mean skins/equipment are visually implemented.
            RenderSlots();
        }

        public void EnterSelectedCharacter()
        {
            if (!authenticated || busy || profiles == null) return;
            AdventureCharacterSnapshot chosen = FindSlot(selectedSlotIndex);
            if (chosen == null)
            {
                SetStatus("Ô nhân vật này còn trống. Hãy tạo nhân vật trước.");
                return;
            }
            EnterServerCharacter(chosen.characterId);
        }

        public void CreateCharacterAndEnter()
        {
            if (!authenticated || busy || profiles == null || loadedProfile == null)
            {
                SetStatus("Cần kết nối máy chủ để tạo nhân vật.");
                return;
            }
            if (FindSlot(selectedSlotIndex) != null)
            {
                SetStatus("Ô này đã có nhân vật. Chọn Vào game hoặc ô trống.");
                return;
            }
            string name = characterNameInput ? characterNameInput.text.Trim() : "";
            if (name.Length < 3 || name.Length > 12)
            {
                SetStatus("Tên nhân vật phải từ 3 đến 12 ký tự.");
                return;
            }

            busy = true;
            SetStatus("Đang tạo nhân vật trên máy chủ...");
            try
            {
                profiles.CreateCharacter(name, selectedClass, selectedSlotIndex,
                    (success, created, message) =>
                    {
                        busy = false;
                        if (!success || created == null || string.IsNullOrWhiteSpace(created.characterId))
                        {
                            SetStatus(string.IsNullOrEmpty(message)
                                ? "Không thể tạo nhân vật." : message);
                            return;
                        }
                        // Enter only AFTER a verified server-created character is issued.
                        EnterServerCharacter(created.characterId);
                    });
            }
            catch (Exception ex)
            {
                busy = false;
                SetStatus("Máy chủ không thể tạo nhân vật.");
                Debug.LogError("CreateCharacter adapter failed: " + ex.Message);
            }
        }

        private void EnterServerCharacter(string characterId)
        {
            if (!authenticated || profiles == null || busy || string.IsNullOrEmpty(characterId))
                return;

            busy = true;
            SetStatus("Đang vào game với tiến trình đã lưu...");
            try
            {
                profiles.EnterCharacter(characterId, (success, character, message) =>
                {
                    busy = false;
                    if (!success || character == null ||
                        character.characterId != characterId)
                    {
                        SetStatus(string.IsNullOrEmpty(message)
                            ? "Máy chủ không xác nhận nhân vật này." : message);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(gameplaySceneName) ||
                        !Application.CanStreamedLevelBeLoaded(gameplaySceneName))
                    {
                        SetStatus("Scene gameplay chưa được cấu hình trong Build Settings.");
                        return;
                    }

                    AdventurePlayerSession.Activate(character);
                    SceneManager.LoadScene(gameplaySceneName);
                });
            }
            catch (Exception ex)
            {
                busy = false;
                SetStatus("Không thể tải tiến trình nhân vật.");
                Debug.LogError("EnterCharacter adapter failed: " + ex.Message);
            }
        }

        private static bool IsValidProfile(AdventureAccountProfile profile)
        {
            if (profile == null || profile.characters == null ||
                profile.characters.Length > 4 || string.IsNullOrWhiteSpace(profile.accountId))
                return false;

            int usedSlots = 0;
            foreach (AdventureCharacterSnapshot character in profile.characters)
            {
                if (character == null || character.slotIndex < 0 || character.slotIndex > 3 ||
                    character.level < 1 || !Enum.IsDefined(typeof(HeroClass), character.heroClass) ||
                    string.IsNullOrWhiteSpace(character.characterId))
                    return false;
                int flag = 1 << character.slotIndex;
                if ((usedSlots & flag) != 0) return false;
                usedSlots |= flag;
            }
            return true;
        }

        private static int FindInitialSlot(AdventureAccountProfile profile)
        {
            if (profile.characters.Length > 0)
                return profile.characters[0].slotIndex;
            return 0;
        }

        private AdventureCharacterSnapshot FindSlot(int slotIndex)
        {
            if (loadedProfile == null || loadedProfile.characters == null) return null;
            foreach (AdventureCharacterSnapshot character in loadedProfile.characters)
                if (character.slotIndex == slotIndex) return character;
            return null;
        }

        private void RenderSlots()
        {
            if (slotLabels == null) return;
            for (int i = 0; i < slotLabels.Length && i < 4; i++)
            {
                if (!slotLabels[i]) continue;
                AdventureCharacterSnapshot character = FindSlot(i);
                slotLabels[i].text = character == null ? "Ô " + (i + 1) + ": Trống" :
                    "Ô " + (i + 1) + ": " + character.characterName +
                    " • Lv." + character.level + " • " +
                    HeroClassCatalog.Get(character.heroClass).displayName;
            }
        }

        public void BackToAuthentication()
        {
            authenticated = false;
            loadedProfile = null;
            AdventurePlayerSession.Clear();
            Show(EntryScreen.Authentication);
        }

        private void Show(EntryScreen destination)
        {
            screen = destination;
            if (splashRoot) splashRoot.SetActive(destination == EntryScreen.Splash);
            if (loadingRoot) loadingRoot.SetActive(destination == EntryScreen.Loading);
            if (titleRoot) titleRoot.SetActive(destination == EntryScreen.Title);
            if (authRoot) authRoot.SetActive(destination == EntryScreen.Authentication);
            if (selectionRoot) selectionRoot.SetActive(destination == EntryScreen.CharacterSelection);
            SetStatus("");
            if (AdventureAudioDirector.Instance)
            {
                AdventureMusicStage stage = destination == EntryScreen.Splash || destination == EntryScreen.Loading
                    ? AdventureMusicStage.Splash :
                    destination == EntryScreen.Authentication || destination == EntryScreen.Title
                        ? AdventureMusicStage.Authentication
                        : AdventureMusicStage.CharacterSelection;
                AdventureAudioDirector.Instance.PlayStage(stage);
            }
            if (destination == EntryScreen.CharacterSelection) SelectClass(selectedClass);
        }

        private void SetStatus(string message)
        {
            Text target = screen == EntryScreen.CharacterSelection && selectionStatusText ? selectionStatusText : statusText;
            if (statusText && target != statusText) statusText.text = "";
            if (target) target.text = message;
        }

        /// <summary>Current status line shown to the player (for UI tests and accessibility readers).</summary>
        public string StatusMessage =>
            screen == EntryScreen.CharacterSelection && selectionStatusText ? selectionStatusText.text :
            statusText ? statusText.text : "";

        public bool Busy => busy;
    }
}
