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
        [SerializeField] private HeroSelectionPreview3D heroPreview;

        [Header("Gameplay")]
        [SerializeField] private string gameplaySceneName = "MainGameScene";

        private IAdventureAccountService accounts;
        private bool busy, authenticated;
        private HeroClass selectedClass = HeroClass.Swordsman;
        private EntryScreen screen;

        public EntryScreen CurrentScreen => screen;
        public HeroClass SelectedClass => selectedClass;

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
                    if (success) Show(EntryScreen.CharacterSelection);
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

        public void CreateCharacterAndEnter()
        {
            if (busy || !authenticated || accounts == null)
            {
                SetStatus("Bạn cần đăng nhập trên máy chủ trước khi tạo nhân vật.");
                return;
            }
            string name = characterNameInput ? characterNameInput.text.Trim() : "";
            if (name.Length < 3 || name.Length > 16)
            {
                SetStatus("Tên nhân vật phải từ 3 đến 16 ký tự.");
                return;
            }
            busy = true;
            SetStatus("Đang tạo nhân vật...");
            try
            {
                accounts.CreateCharacter(name, selectedClass, (success, message) =>
                {
                    busy = false;
                    if (!success)
                    {
                        SetStatus(string.IsNullOrEmpty(message) ?
                            "Không thể tạo nhân vật." : message);
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(gameplaySceneName) ||
                        !Application.CanStreamedLevelBeLoaded(gameplaySceneName))
                    {
                        SetStatus("Scene gameplay chưa được cấu hình trong Build Settings.");
                        return;
                    }
                    SceneManager.LoadScene(gameplaySceneName);
                });
            }
            catch (Exception ex)
            {
                busy = false;
                SetStatus("Tạo nhân vật thất bại. Kiểm tra kết nối.");
                Debug.LogError("Adventure King character adapter failure: " + ex.Message);
            }
        }

        public void BackToAuthentication()
        {
            authenticated = false;
            Show(EntryScreen.Authentication);
        }

        private void Show(EntryScreen destination)
        {
            screen = destination;
            if (splashRoot) splashRoot.SetActive(destination == EntryScreen.Splash);
            if (authRoot) authRoot.SetActive(destination == EntryScreen.Authentication);
            if (selectionRoot) selectionRoot.SetActive(destination == EntryScreen.CharacterSelection);
            SetStatus("");
            if (destination == EntryScreen.CharacterSelection) SelectClass(selectedClass);
        }

        private void SetStatus(string message)
        {
            if (statusText) statusText.text = message;
        }
    }
}
