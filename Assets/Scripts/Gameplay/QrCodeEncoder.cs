using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Self-contained QR Code generator (ISO/IEC 18004): byte mode, error correction level M, versions 1-14, all 8 masks with the standard penalty rules.
    /// Turns the VietQR (EMVCo) payload string into a crisp Texture2D on the device: no network, no image download, no third-party package.
    /// </summary>
    public static class QrCodeEncoder
    {
        // ECC level M, per version 1..14: EC codewords per block, then (blocks, data codewords) of group 1 and group 2
        private static readonly int[][] BlockInfo =
        {
            new[] { 10, 1, 16, 0, 0 }, new[] { 16, 1, 28, 0, 0 }, new[] { 26, 1, 44, 0, 0 }, new[] { 18, 2, 32, 0, 0 }, new[] { 24, 2, 43, 0, 0 },
            new[] { 16, 4, 27, 0, 0 }, new[] { 18, 4, 31, 0, 0 }, new[] { 22, 2, 38, 2, 39 }, new[] { 22, 3, 36, 2, 37 }, new[] { 26, 4, 43, 1, 44 },
            new[] { 30, 1, 50, 4, 51 }, new[] { 22, 6, 36, 2, 37 }, new[] { 22, 8, 37, 1, 38 }, new[] { 24, 4, 40, 5, 41 },
        };
        private static readonly int[][] Align =
        {
            new int[0], new[] { 6, 18 }, new[] { 6, 22 }, new[] { 6, 26 }, new[] { 6, 30 }, new[] { 6, 34 }, new[] { 6, 22, 38 }, new[] { 6, 24, 42 }, new[] { 6, 26, 46 },
            new[] { 6, 28, 50 }, new[] { 6, 30, 54 }, new[] { 6, 32, 58 }, new[] { 6, 34, 62 }, new[] { 6, 26, 46, 66 },
        };

        /// <summary>The module matrix (true = dark) of the smallest version that fits, or null when the text is too long.</summary>
        public static bool[,] Encode(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text ?? "");
            for (int version = 1; version <= BlockInfo.Length; version++)
            {
                int[] bi = BlockInfo[version - 1];
                int dataCw = bi[1] * bi[2] + bi[3] * bi[4];
                int countBits = version <= 9 ? 8 : 16;
                if (4 + countBits + data.Length * 8 > dataCw * 8) continue;
                return Build(version, data, dataCw, countBits);
            }
            return null;
        }

        private static bool[,] Build(int version, byte[] data, int dataCw, int countBits)
        {
            // ---- data bits: mode 0100 (byte), length, bytes, terminator, padding
            var bits = new List<bool>();
            void Put(int val, int len) { for (int i = len - 1; i >= 0; i--) bits.Add(((val >> i) & 1) != 0); }
            Put(4, 4); Put(data.Length, countBits);
            foreach (byte b in data) Put(b, 8);
            Put(0, Math.Min(4, dataCw * 8 - bits.Count));
            while (bits.Count % 8 != 0) bits.Add(false);
            for (int pad = 0xEC; bits.Count < dataCw * 8; pad ^= 0xEC ^ 0x11) Put(pad, 8);
            var cw = new byte[dataCw];
            for (int i = 0; i < bits.Count; i++) if (bits[i]) cw[i >> 3] |= (byte)(0x80 >> (i & 7));

            // ---- split into blocks, Reed-Solomon, interleave
            int[] bi = BlockInfo[version - 1];
            int ec = bi[0], nBlocks = bi[1] + bi[3];
            var dataBlocks = new byte[nBlocks][]; var ecBlocks = new byte[nBlocks][];
            byte[] gen = Generator(ec);
            int pos = 0;
            for (int b = 0; b < nBlocks; b++)
            {
                int len = b < bi[1] ? bi[2] : bi[4];
                dataBlocks[b] = new byte[len]; Array.Copy(cw, pos, dataBlocks[b], 0, len); pos += len;
                ecBlocks[b] = RemainderOf(dataBlocks[b], gen);
            }
            var all = new List<byte>();
            int maxLen = Math.Max(bi[2], bi[4]);
            for (int i = 0; i < maxLen; i++) for (int b = 0; b < nBlocks; b++) if (i < dataBlocks[b].Length) all.Add(dataBlocks[b][i]);
            for (int i = 0; i < ec; i++) for (int b = 0; b < nBlocks; b++) all.Add(ecBlocks[b][i]);

            // ---- matrix
            int size = version * 4 + 17;
            var modules = new bool[size, size]; var isFn = new bool[size, size];
            DrawFunctionPatterns(modules, isFn, version, size);
            DrawCodewords(modules, isFn, all.ToArray(), size);
            int best = 0, bestScore = int.MaxValue;
            for (int m = 0; m < 8; m++)
            {
                Mask(modules, isFn, m, size);
                DrawFormat(modules, isFn, m, size);
                int s = Penalty(modules, size);
                if (s < bestScore) { bestScore = s; best = m; }
                Mask(modules, isFn, m, size);                                   // undo (XOR again)
            }
            Mask(modules, isFn, best, size);
            DrawFormat(modules, isFn, best, size);
            return modules;
        }

        // ---------------------------------------------------------------- Reed-Solomon over GF(256), polynomial 0x11D
        private static int Mul(int x, int y) { int z = 0; for (int i = 7; i >= 0; i--) { z = (z << 1) ^ ((z >> 7) * 0x11D); z ^= ((y >> i) & 1) * x; } return z; }
        private static byte[] Generator(int degree)
        {
            var r = new byte[degree]; r[degree - 1] = 1; int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++) { r[j] = (byte)Mul(r[j], root); if (j + 1 < degree) r[j] ^= r[j + 1]; }
                root = Mul(root, 2);
            }
            return r;
        }
        private static byte[] RemainderOf(byte[] data, byte[] gen)
        {
            var r = new byte[gen.Length];
            foreach (byte b in data)
            {
                int f = b ^ r[0];
                Array.Copy(r, 1, r, 0, r.Length - 1); r[r.Length - 1] = 0;
                for (int i = 0; i < r.Length; i++) r[i] ^= (byte)Mul(gen[i], f);
            }
            return r;
        }

        // ---------------------------------------------------------------- function patterns
        private static void Set(bool[,] m, bool[,] fn, int x, int y, bool dark, int size) { if (x >= 0 && x < size && y >= 0 && y < size) { m[y, x] = dark; fn[y, x] = true; } }

        private static void DrawFunctionPatterns(bool[,] m, bool[,] fn, int version, int size)
        {
            for (int i = 0; i < size; i++) { Set(m, fn, 6, i, i % 2 == 0, size); Set(m, fn, i, 6, i % 2 == 0, size); }       // timing
            foreach (var (cx, cy) in new[] { (3, 3), (size - 4, 3), (3, size - 4) })                                         // finders + separators
                for (int dy = -4; dy <= 4; dy++)
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                        Set(m, fn, cx + dx, cy + dy, dist != 2 && dist != 4, size);
                    }
            int[] pos = Align[version - 1];
            for (int i = 0; i < pos.Length; i++)
                for (int j = 0; j < pos.Length; j++)
                {
                    if ((i == 0 && j == 0) || (i == 0 && j == pos.Length - 1) || (i == pos.Length - 1 && j == 0)) continue;
                    for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) Set(m, fn, pos[i] + dx, pos[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1, size);
                }
            DrawFormat(m, fn, 0, size);                                                                                       // reserve the format area
            if (version >= 7)
            {
                int rem = version;
                for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
                int bitsV = version << 12 | rem;
                for (int i = 0; i < 18; i++)
                {
                    bool bit = ((bitsV >> i) & 1) != 0;
                    int a = size - 11 + i % 3, b = i / 3;
                    Set(m, fn, a, b, bit, size); Set(m, fn, b, a, bit, size);
                }
            }
        }

        private static void DrawFormat(bool[,] m, bool[,] fn, int mask, int size)
        {
            int data = (0 << 3) | mask;                                   // ECC level M = 00
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = (data << 10 | rem) ^ 0x5412;
            bool Bit(int i) => ((bits >> i) & 1) != 0;
            for (int i = 0; i <= 5; i++) Set(m, fn, 8, i, Bit(i), size);
            Set(m, fn, 8, 7, Bit(6), size); Set(m, fn, 8, 8, Bit(7), size); Set(m, fn, 7, 8, Bit(8), size);
            for (int i = 9; i < 15; i++) Set(m, fn, 14 - i, 8, Bit(i), size);
            for (int i = 0; i < 8; i++) Set(m, fn, size - 1 - i, 8, Bit(i), size);
            for (int i = 8; i < 15; i++) Set(m, fn, 8, size - 15 + i, Bit(i), size);
            Set(m, fn, 8, size - 8, true, size);                          // the always-dark module
        }

        private static void DrawCodewords(bool[,] m, bool[,] fn, byte[] data, int size)
        {
            int i = 0;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < size; vert++)
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j; bool up = ((right + 1) & 2) == 0; int y = up ? size - 1 - vert : vert;
                        if (!fn[y, x] && i < data.Length * 8) { m[y, x] = ((data[i >> 3] >> (7 - (i & 7))) & 1) != 0; i++; }
                    }
            }
        }

        private static void Mask(bool[,] m, bool[,] fn, int mask, int size)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (fn[y, x]) continue;
                    bool inv = mask switch
                    {
                        0 => (x + y) % 2 == 0, 1 => y % 2 == 0, 2 => x % 3 == 0, 3 => (x + y) % 3 == 0, 4 => (x / 3 + y / 2) % 2 == 0,
                        5 => x * y % 2 + x * y % 3 == 0, 6 => (x * y % 2 + x * y % 3) % 2 == 0, _ => ((x + y) % 2 + x * y % 3) % 2 == 0,
                    };
                    if (inv) m[y, x] = !m[y, x];
                }
        }

        private static int Penalty(bool[,] m, int size)
        {
            int score = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int a = 0; a < size; a++)
                {
                    int run = 1;
                    var hist = new List<int>();
                    for (int b = 1; b < size; b++)
                    {
                        bool cur = pass == 0 ? m[a, b] : m[b, a], prev = pass == 0 ? m[a, b - 1] : m[b - 1, a];
                        if (cur == prev) { run++; if (run == 5) score += 3; else if (run > 5) score++; }
                        else { hist.Add(run); run = 1; }
                    }
                    hist.Add(run);
                    for (int i = 0; i + 4 < hist.Count; i++)                         // finder-like 1:1:3:1:1 runs
                        if (hist[i + 1] == hist[i + 3] && hist[i + 2] == 3 * hist[i + 1] && hist[i + 3] == hist[i + 4] && hist[i + 1] > 0) score += 40;
                }
            for (int y = 0; y < size - 1; y++) for (int x = 0; x < size - 1; x++) if (m[y, x] == m[y, x + 1] && m[y, x] == m[y + 1, x] && m[y, x] == m[y + 1, x + 1]) score += 3;
            int dark = 0; foreach (bool d in m) if (d) dark++;
            score += Math.Abs(dark * 20 - size * size * 10) / (size * size) * 10;
            return score;
        }

        /// <summary>Crisp black-on-white texture: whole pixels per module, 4-module quiet zone, Point filter.</summary>
        public static Texture2D ToTexture(string text, int targetPixels = 512)
        {
            bool[,] m = Encode(text);
            if (m == null) return null;
            int n = m.GetLength(0), quiet = 4, modules = n + quiet * 2, scale = Mathf.Max(1, targetPixels / modules), px = modules * scale;
            var colors = new Color32[px * px];
            var white = new Color32(255, 255, 255, 255); var black = new Color32(0, 0, 0, 255);
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                {
                    int mx = x / scale - quiet, my = y / scale - quiet;
                    bool dark = mx >= 0 && my >= 0 && mx < n && my < n && m[n - 1 - my, mx];     // texture y grows upward: flip so the code is upright
                    colors[y * px + x] = dark ? black : white;
                }
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(colors); tex.Apply(false, false);
            return tex;
        }
    }
}
