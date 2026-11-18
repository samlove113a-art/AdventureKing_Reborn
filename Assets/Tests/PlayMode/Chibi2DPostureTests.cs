using System.IO;
using System.Linq;
using AdventureKing.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Posture of the four 2D heroes, checked on the baked frame strips themselves (ArtSource/Chibi2D/{Class}/{Class}_{Clip}.png):
    /// standing Idle has the head over the feet (no lean to either side or hunch over), the feet sit on one baseline in every clip,
    /// no frame is clipped by its cell, and a mirrored sprite never tilts (flip is scale.x only, pivot on the feet).
    /// </summary>
    public sealed class Chibi2DPostureTests
    {
        private static readonly string[] Classes = { "Mage", "Swordsman", "Archer", "Assassin" };
        private static string Strip(string cls, string clip) => Path.Combine("ArtSource", "Chibi2D", cls, $"{cls}_{clip}.png");

        private static bool[][,] Frames(string cls, string clip, out int cell)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(Strip(cls, clip))), cls + " " + clip);
            cell = tex.height;
            int n = tex.width / cell;
            Color32[] px = tex.GetPixels32();
            var frames = new bool[n][,];
            for (int f = 0; f < n; f++)
            {
                frames[f] = new bool[cell, cell];
                for (int y = 0; y < cell; y++)
                    for (int x = 0; x < cell; x++) frames[f][y, x] = px[y * tex.width + f * cell + x].a > 127;   // y = 0 is the bottom row
            }
            Object.DestroyImmediate(tex);
            return frames;
        }

        private static (int top, int bottom, float headX, float feetX) Measure(bool[,] f, int cell)
        {
            int top = -1, bottom = cell;
            for (int y = 0; y < cell; y++) for (int x = 0; x < cell; x++) if (f[y, x]) { top = Mathf.Max(top, y); bottom = Mathf.Min(bottom, y); }
            int h = top - bottom;
            float hs = 0, hn = 0, fs = 0, fn = 0;
            for (int y = bottom; y <= top; y++)
                for (int x = 0; x < cell; x++)
                {
                    if (!f[y, x]) continue;
                    if (y > top - h * 0.35f) { hs += x; hn++; }
                    if (y < bottom + h * 0.12f) { fs += x; fn++; }
                }
            return (top, bottom, hs / Mathf.Max(1, hn), fs / Mathf.Max(1, fn));
        }

        [Test]
        public void Idle_Stands_Upright_Head_Over_Feet_On_One_Baseline([ValueSource(nameof(Classes))] string cls)
        {
            var frames = Frames(cls, "Idle", out int cell);
            int firstBottom = -1;
            for (int i = 0; i < frames.Length; i++)
            {
                var m = Measure(frames[i], cell);
                float height = m.top - m.bottom;
                float lean = (m.headX - m.feetX) / height;
                Assert.LessOrEqual(Mathf.Abs(lean), 0.16f, $"{cls} Idle frame {i}: head is {lean:0.00} of the body height off the feet (leaning)");
                Assert.Greater(height, cell * 0.55f, $"{cls} Idle frame {i}: hunched / too small ({height}px of {cell})");
                if (firstBottom < 0) firstBottom = m.bottom;
                Assert.LessOrEqual(Mathf.Abs(m.bottom - firstBottom), 1, $"{cls} Idle frame {i}: feet moved off the baseline");
            }
        }

        [Test]
        public void No_Clip_Is_Cut_By_Its_Frame_And_Feet_Stay_On_The_Ground_Line([ValueSource(nameof(Classes))] string cls)
        {
            int idleBottom = Measure(Frames(cls, "Idle", out int cell0)[0], cell0).bottom;
            foreach (string clip in new[] { "Idle", "Run", "Jump", "Fall", "Attack", "Skill", "Climb", "Hit" })
            {
                var frames = Frames(cls, clip, out int cell);
                for (int i = 0; i < frames.Length; i++)
                {
                    var f = frames[i];
                    bool cut = false;
                    for (int k = 0; k < cell && !cut; k++) cut = f[k, 0] || f[k, cell - 1] || f[cell - 1, k];     // left / right / top edge (the bottom row is the ground)
                    Assert.IsFalse(cut, $"{cls} {clip} frame {i} touches the left / right / top edge of its frame (sprite cut)");
                    if (clip is "Idle" or "Attack" or "Hit")      // (a Skill may leave the ground: a leap, a spin)
                        Assert.LessOrEqual(Mathf.Abs(Measure(f, cell).bottom - idleBottom), 7, $"{cls} {clip} frame {i}: feet are off the ground line");
                }
            }
        }

        [Test]
        public void A_Mirrored_Sprite_Body_Only_Changes_Scale_X()
        {
            var root = new GameObject("body2d");
            try
            {
                root.transform.localScale = Vector3.one;
                Actor2D.Flip(root, true);
                Assert.Less(root.transform.localScale.x, 0f);
                Assert.Greater(root.transform.localScale.y, 0f);
                Assert.AreEqual(Quaternion.identity, root.transform.localRotation, "mirroring never rotates the body (no tilt)");
                Actor2D.Flip(root, false);
                Assert.Greater(root.transform.localScale.x, 0f);
                Assert.AreEqual(Quaternion.identity, root.transform.localRotation);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
