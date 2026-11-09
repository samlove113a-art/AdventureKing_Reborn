using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace AdventureKing.Gameplay
{
    public enum EntryScreen { Splash, Authentication, CharacterSelection }

    public enum SignInMethod { PasswordLogin, PasswordRegister, Google, Facebook }

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
        [SerializeField] private GameObject authRoot;
        [SerializeField] private GameObject selectionRoot;
        [SerializeField, Min(0f)] private float splashDurationSeconds = 2.5f;

        [Header("Branding")]
        [SerializeField] private Image splashLogo;
        [SerializeField] private Sprite officialAdventureKingLogo;

        [Header("Authentication UI")]
        [SerializeField] private InputField accountInput;
        [SerializeField] private InputField passwordInput;
        [SerializeField] private InputField confirmPasswordInput;
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject registerFields;

        [Header("Four hero classes")]
        [SerializeField] private Text heroName;
        [SerializeField] private Text heroDescription;
        [SerializeField] private Text heroStats;
        [SerializeField] private InputField characterNameInput;
        [SerializeField] private Text[] slotLabels = new Text[4];
        [SerializeField] private Text existingCharacterDetails;
        [SerializeField] private HeroSelectionPreview3D heroPreview;

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

        private void Awake()
        {
            if (splashLogo && officialAdventureKingLogo)
                splashLogo.sprite = officialAdventureKingLogo;
            Show(EntryScreen.Splash);
        }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(splashDurationSeconds);
            if (screen == EntryScreen.Splash) Show(EntryScreen.Authentication);
        }

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

        public void SkipSplash()
        {
            if (screen == EntryScreen.Splash) Show(EntryScreen.Authentication);
        }

        public void SetRegisterMode(bool register)
        {
            if (registerFields) registerFields.SetActive(register);
            SetStatus("");
        }

        public void LoginPassword() => PasswordAuth(SignInMethod.PasswordLogin);
        public void RegisterPassword() => PasswordAuth(SignInMethod.PasswordRegister);
        public void ContinueWithGoogle() => SubmitAuth(SignInMethod.Google, "", "");
        public void ContinueWithFacebook() => SubmitAuth(SignInMethod.Facebook, "", "");

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
            if (name.Length < 3 || name.Length > 16)
            {
                SetStatus("Tên nhân vật phải từ 3 đến 16 ký tự.");
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
            if (authRoot) authRoot.SetActive(destination == EntryScreen.Authentication);
            if (selectionRoot) selectionRoot.SetActive(destination == EntryScreen.CharacterSelection);
            SetStatus("");
            if (AdventureAudioDirector.Instance)
            {
                AdventureMusicStage stage = destination == EntryScreen.Splash
                    ? AdventureMusicStage.Splash :
                    destination == EntryScreen.Authentication
                        ? AdventureMusicStage.Authentication
                        : AdventureMusicStage.CharacterSelection;
                AdventureAudioDirector.Instance.PlayStage(stage);
            }
            if (destination == EntryScreen.CharacterSelection) SelectClass(selectedClass);
        }

        private void SetStatus(string message)
        {
            if (statusText) statusText.text = message;
        }
    }
}
