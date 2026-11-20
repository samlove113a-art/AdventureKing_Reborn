using System.IO;
using System.Text;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace AdventureKing.Tests
{
    /// <summary>The in-app QR encoder: writes QR pictures of increasing size to QA/TopUp/qr_*.png (checked by an independent decoder in the QA step).</summary>
    public sealed class QrCodeTests
    {
        public static readonly string[] Samples =
        {
            "AKR123456",
            "https://adventureking.example/pay?order=AKR123456&amount=20000",
            "00020101021238540010A0000007270124000697042201100123456789020800QRIBFTTA53037045405200005802VN62130809AKR12345663041234",
            "00020101021238570010A00000072701270006970422011301234567890123020800QRIBFTTA530370454061990000580 2VN62130809AKR123456 0123456789 abcdefghijklmnopqrstuvwxyz ABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789 0123456789 0123456789",
            "Tiếng Việt có dấu: Nạp Kim Cương AKR654321",
        };

        [Test]
        public void Encodes_Every_Sample_Into_A_Square_Matrix_Of_A_Valid_Version()
        {
            Directory.CreateDirectory("QA/TopUp");
            for (int i = 0; i < Samples.Length; i++)
            {
                bool[,] m = QrCodeEncoder.Encode(Samples[i]);
                Assert.IsNotNull(m, "fits in version 14: " + i);
                int n = m.GetLength(0);
                Assert.AreEqual(1, (n - 17) % 4 == 0 ? 1 : 0, "size = 17 + 4 x version");
                // finder pattern corners are dark, the dark module sits at (8, n-8)
                Assert.IsTrue(m[0, 0] && m[0, n - 1] && m[n - 1, 0]);
                Assert.IsTrue(m[n - 8, 8]);
                Texture2D t = QrCodeEncoder.ToTexture(Samples[i], 512);
                Assert.IsNotNull(t);
                Assert.AreEqual(FilterMode.Point, t.filterMode);
                File.WriteAllBytes($"QA/TopUp/qr_{i}.png", t.EncodeToPNG());
                File.WriteAllText($"QA/TopUp/qr_{i}.txt", Samples[i], new UTF8Encoding(false));
                Object.DestroyImmediate(t);
            }
        }

        [Test]
        public void Too_Long_Text_Returns_Null_Instead_Of_A_Broken_Code()
        {
            Assert.IsNull(QrCodeEncoder.Encode(new string('a', 1000)));
        }
    }
}
