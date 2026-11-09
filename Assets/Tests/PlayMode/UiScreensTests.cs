using System.Collections;
using System.Collections.Generic;
using System.IO;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Every entry screen (splash, login, register, character hall: create + existing, error message) rendered
    /// at PC 1920x1080 / 1366x768, phone 19.5:9 / 16:9 and tablet 4:3, audited by <see cref="UiAudit"/>
    /// (overlapping text, overflow, text on frame ornaments, off-screen / unsafe / tiny / covered buttons).
    /// PNGs + report: QA/UI/entry/.
    /// </summary>
    public sealed class UiScreensTests
    {
        private TestServer server;
        private const string Dir = "QA/UI/entry";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5211);
            yield return server.Start();
            Directory.CreateDirectory(Dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestRunnerGuard.CloseGame();
            server.Stop();
        }

        /// <summary>
        /// Negative control: the audit must catch the exact bugs the owner reported (title on the frame ornament,
        /// text over text, truncated text, a button hidden under another panel). Otherwise a green audit means nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator Audit_Catches_Known_Layout_Bugs()
        {
            var canvasGo = new GameObject("AuditControlCanvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var camGo = new GameObject("AuditControlCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.cullingMask = 0;
            try
            {
#if UNITY_EDITOR
                var frame = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Fantasy/frame_panel.png");
                var font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/BeVietnamPro/BeVietnamPro-SemiBold.ttf");
#else
                Sprite frame = null; Font font = null;
#endif
                Assert.IsNotNull(frame, "frame sprite");
                Assert.Greater(frame.border.w, 0f, "frame sprite must be 9-sliced (Single sprite mode with a border)");
                GameObject Box(string name, Transform parent, Vector2 pos, Vector2 size, Sprite s = null)
                {
                    var go = new GameObject(name, typeof(RectTransform));
                    go.transform.SetParent(parent, false);
                    var r = (RectTransform)go.transform;
                    r.anchoredPosition = pos; r.sizeDelta = size;
                    var img = go.AddComponent<Image>();
                    img.sprite = s;
                    img.type = s ? Image.Type.Sliced : Image.Type.Simple;
                    return go;
                }
                Text Txt(string name, Transform parent, string s, Vector2 pos, Vector2 size, int fontSize = 40)
                {
                    var go = new GameObject(name, typeof(RectTransform));
                    go.transform.SetParent(parent, false);
                    var r = (RectTransform)go.transform;
                    r.anchoredPosition = pos; r.sizeDelta = size;
                    var t = go.AddComponent<Text>();
                    t.font = font; t.fontSize = fontSize; t.text = s; t.alignment = TextAnchor.MiddleCenter;
                    t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
                    return t;
                }
                GameObject card = Box("Card", canvasGo.transform, Vector2.zero, new Vector2(700, 800), frame);
                card.GetComponent<Image>().pixelsPerUnitMultiplier = 1f;
                Txt("TitleOnCrest", card.transform, "Đăng nhập", new Vector2(0, 385), new Vector2(560, 60));          // the reported bug
                Txt("A", card.transform, "Chồng chữ", new Vector2(0, 0), new Vector2(400, 60));
                Txt("B", card.transform, "Chữ đè lên", new Vector2(10, 10), new Vector2(400, 60));                    // overlaps A
                Txt("Cut", card.transform, "Một câu rất dài không thể nào vừa trong một ô nhỏ như thế này được", new Vector2(0, -150), new Vector2(200, 50));
                var btnGo = Box("HiddenButton", card.transform, new Vector2(0, -280), new Vector2(200, 70));
                var btn = btnGo.AddComponent<Button>();
                btn.targetGraphic = btnGo.GetComponent<Image>();
                Box("CoverPanel", card.transform, new Vector2(0, -280), new Vector2(300, 120));                     // drawn after -> covers it
                yield return null;
                UiAudit.Report rep = UiAudit.CaptureAndAudit(cam, "QA/UI/entry/00_audit_negative_control.png", 1920, 1080);
                string all = rep.ToString();
                TestContext.WriteLine(all);
                StringAssert.Contains("ON FRAME BORDER: AuditControlCanvas/Card/TitleOnCrest", all, "title on the ornament detected");
                StringAssert.Contains("TEXT OVERLAP", all, "overlapping texts detected");
                StringAssert.Contains("TRUNCATED: AuditControlCanvas/Card/Cut", all, "truncation detected");
                StringAssert.Contains("COVERED: AuditControlCanvas/Card/HiddenButton", all, "covered button detected");
            }
            finally
            {
                Object.Destroy(canvasGo);
                Object.Destroy(camGo);
            }
        }

        [UnityTest]
        public IEnumerator Entry_Screens_Pass_Ui_Audit_At_All_Resolutions()
        {
            var problems = new List<string>();
            yield return server.OpenGame();
            var flow = Object.FindAnyObjectByType<AdventureEntryFlow>();
            yield return new WaitForSeconds(0.4f);
            yield return UiAudit.Screen(Dir, "01_splash", problems);
            flow.SkipSplash();
            yield return new WaitForSeconds(0.6f);
            yield return UiAudit.Screen(Dir, "02_login", problems);
            flow.SetRegisterMode(true);
            yield return new WaitForSeconds(0.3f);
            yield return UiAudit.Screen(Dir, "03_register", problems);

            // a real server refusal shows a status line inside the card
            EntryLobbyTests.Find<InputField>("AccountInput").text = "ui.audit@test.vn";
            EntryLobbyTests.Find<InputField>("PasswordInput").text = "short";
            EntryLobbyTests.Find<InputField>("ConfirmPasswordInput").text = "short";
            flow.RegisterPassword();
            yield return EntryLobbyTests.WaitIdle(flow);
            yield return UiAudit.Screen(Dir, "04_register_error", problems);

            EntryLobbyTests.Find<InputField>("PasswordInput").text = "ui-audit-pass-2026";
            EntryLobbyTests.Find<InputField>("ConfirmPasswordInput").text = "ui-audit-pass-2026";
            flow.RegisterPassword();
            yield return EntryLobbyTests.WaitIdle(flow);
            Assert.AreEqual(EntryScreen.CharacterSelection, flow.CurrentScreen, flow.StatusMessage);
            flow.SelectSlot(0);
            flow.SelectClass(HeroClass.Archer);
            Assert.IsTrue(flow.IsCardHighlighted(HeroClass.Archer), "chosen class card shows the selected look");
            flow.SelectClass(HeroClass.Mage);
            Assert.IsTrue(flow.IsCardHighlighted(HeroClass.Mage), "highlight follows the new choice");
            Assert.IsFalse(flow.IsCardHighlighted(HeroClass.Archer), "only one class card is highlighted");
            EntryLobbyTests.Find<InputField>("CharacterNameInput").text = "Pháp Sư Nguyệt Quế";
            yield return new WaitForSeconds(0.8f);
            yield return UiAudit.Screen(Dir, "05_hall_create", problems);

            UiAudit.WriteReport($"{Dir}/report.txt", problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
