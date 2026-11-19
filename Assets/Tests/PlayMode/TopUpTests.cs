using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace AdventureKing.Tests
{
    /// <summary>VietQR top-up end to end on the real server (test bank account + secret): order, QR panel, bank webhook, Diamonds credited once, panel closes itself. Screenshots: QA/TopUp/*.png</summary>
    public sealed class TopUpTests
    {
        private TestServer server;
        private const string Secret = "playmode-secret-0123456789";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRunnerGuard.Protect();
            server = new TestServer(5301, extraArgs: $"--AdventureKing:VietQr:Enabled=true --AdventureKing:VietQr:BankId=MB --AdventureKing:VietQr:AccountNo=0123456789 \"--AdventureKing:VietQr:AccountName=NGUYEN VAN A\" --AdventureKing:VietQr:WebhookSecret={Secret}");
            yield return server.Start();
            Directory.CreateDirectory("QA/TopUp");
        }

        [UnityTearDown]
        public IEnumerator TearDown() { HeroVisualStyle.Override = null; yield return TestRunnerGuard.CloseGame(); server.Stop(); }

        private static IEnumerator Shot(string name, int w = 1600, int h = 900)
        {
            yield return null;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            GameplayRecorder.Capture(Camera.main, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            File.WriteAllBytes($"QA/TopUp/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; Object.Destroy(tex); rt.Release();
        }

        private static IEnumerator Until(System.Func<bool> cond, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(cond(), "timeout: " + what);
        }

        [UnityTest]
        public IEnumerator Package_To_QR_To_Bank_Webhook_Credits_Diamonds_Once()
        {
            HeroVisualStyle.Override = "2d";
            yield return server.OpenGame();
            yield return TestServer.RegisterCreateAndEnter("tup" + Random.Range(1000, 9999), "tup-pass-2026", HeroClass.Mage, "Nạp Thử");
            var world = AdventureWorldSession.Current;
            var greeter = Object.FindAnyObjectByType<ElderGreeter>();
            float until = Time.time + 20f;
            while (greeter && !greeter.DialogOpen && Time.time < until) yield return null;
            if (greeter && greeter.DialogOpen) greeter.Close();
            until = Time.time + 15f;
            while (greeter && !greeter.Done && Time.time < until) yield return null;

            var panel = Object.FindAnyObjectByType<TopUpPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "top-up window exists");
            panel.Open();
            yield return Until(() => panel.Catalog != null, 8f, "catalog");
            Assert.IsTrue(panel.Catalog.paymentsEnabled, "the test server has top-up switched on");
            StorePackageState pkg = panel.Catalog.packages.First(p => p.id == "pouch_1");
            Assert.IsTrue(pkg.purchasable);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("01_packages");

            panel.Choose(pkg);
            yield return Until(() => panel.Order != null, 8f, "order");
            Assert.That(panel.Order.code, Does.Match(@"^AKR\d{6}$"));
            Assert.AreEqual(10000, panel.Order.amountVnd);
            StringAssert.Contains("img.vietqr.io/image/MB-0123456789-compact2.png?amount=10000&addInfo=" + panel.Order.code, panel.Order.qrUrl);
            yield return new WaitForSeconds(2.5f);                                 // the QR picture downloads from img.vietqr.io (needs internet: not asserted)
            Debug.Log("[TOPUP] QR picture shown: " + panel.QrShown);
            yield return Shot("02_order_qr");

            // a foreign / wrong-secret notification does nothing
            long before = world.Character.diamonds;
            yield return Webhook("wrong-secret-0123456789", panel.Order.code, 10000, "BANK-T0", 401);
            yield return Webhook(Secret, panel.Order.code, 9999, "BANK-T1", 200);
            yield return new WaitForSeconds(3.5f);
            Assert.IsFalse(panel.Completed, "wrong secret / wrong amount credits nothing");

            // the real notification: exact code + amount
            yield return Webhook(Secret, "NAP KIM CUONG " + panel.Order.code, 10000, "BANK-T2", 200);
            yield return Until(() => panel.Completed, 12f, "panel sees the payment");
            StringAssert.Contains("Kim Cương", panel.LastMessage);
            yield return Shot("03_paid");
            yield return Webhook(Secret, panel.Order.code, 10000, "BANK-T2", 200);          // the same notification again
            yield return new WaitForSeconds(0.5f);
            yield return Until(() => !panel.IsOpen, 6f, "panel closes itself");
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(before + 20, world.Character.diamonds, "10 000 VND = 10 KC, first purchase doubles to 20, credited once");
        }

        private IEnumerator Webhook(string secret, string content, long amount, string txn, int expect)
        {
            string json = $"{{\"content\":\"{content}\",\"amount\":{amount},\"transactionId\":\"{txn}\"}}";
            using var req = new UnityWebRequest(server.ApiUrl + "/store/topup/webhook", "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)), downloadHandler = new DownloadHandlerBuffer(),
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", secret);
            yield return req.SendWebRequest();
            Assert.AreEqual(expect, (int)req.responseCode, req.downloadHandler.text);
        }
    }
}
