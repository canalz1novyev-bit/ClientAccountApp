using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI;

namespace ClientAccountApp
{
    public static class CamoThemeHelper
    {
        // ── Палитра: тёмные военные оттенки, совпадают с темой Military ──────
        // Формат BGRA
        private static readonly byte[] ColBase     = { 0x10, 0x22, 0x16, 0xFF }; // #16220F тёмно-оливковый фон
        private static readonly byte[] ColDarkest  = { 0x06, 0x10, 0x08, 0xFF }; // #081006 почти чёрный
        private static readonly byte[] ColForest   = { 0x16, 0x30, 0x1C, 0xFF }; // #1C3016 лесной зелёный
        private static readonly byte[] ColEarth    = { 0x22, 0x32, 0x3E, 0xFF }; // #3E3222 земляной коричневый
        private static readonly byte[] ColOlive    = { 0x1C, 0x3C, 0x28, 0xFF }; // #283C1C оливковый

        public static WriteableBitmap CreateM81Bitmap()
        {
            // Высокое разрешение — 2× для чёткости при растяжении
            const int W = 440, H = 680;
            var pix = new byte[W * H * 4];

            var rngNoise = new Random(42);

            // ── 1. Заполнение фоном с тонким шумом ───────────────────────────
            for (int i = 0; i < pix.Length; i += 4)
            {
                int n = rngNoise.Next(-6, 7);
                pix[i]     = Clamp(ColBase[0] + n);
                pix[i + 1] = Clamp(ColBase[1] + n);
                pix[i + 2] = Clamp(ColBase[2] + n);
                pix[i + 3] = 255;
            }

            // ── 2. Определяем пятна ───────────────────────────────────────────
            // (cx, cy, rw, rh, angleDeg, colorIdx 1=earth 2=forest 3=darkest 4=olive, seed)
            // Три уровня масштаба: крупные → средние → мелкие

            var blobs = new (float cx, float cy, float rw, float rh, float ang, int col, int seed)[]
            {
                // ── КРУПНЫЕ якорные пятна (покрывают значительную площадь) ───
                (  80,  60, 110, 64,  12, 3,  1),
                ( 330,  40,  96, 58, -18, 3,  2),
                ( 180, 130, 120, 70,  25, 2,  3),
                (  50, 200, 100, 62, -10, 3,  4),
                ( 360, 170,  90, 58,  20, 2,  5),
                ( 160, 300, 116, 66, -22, 3,  6),
                ( 360, 330, 104, 60,  14, 3,  7),
                (  70, 400,  98, 60,  28, 2,  8),
                ( 280, 430, 114, 66, -16, 3,  9),
                ( 160, 520, 108, 62,  18, 2, 10),
                (  60, 580, 100, 58, -24, 3, 11),
                ( 350, 530,  96, 64,  10, 3, 12),
                ( 230, 620, 110, 60,  -8, 2, 13),
                ( 400, 620,  80, 52,  22, 3, 14),
                ( 120, 660,  88, 54, -14, 3, 15),

                // ── СРЕДНИЕ перекрывающие пятна ──────────────────────────────
                ( 220,  30,  72, 44,  -8, 1, 21),
                (  30, 110,  68, 42,  20, 4, 22),
                ( 360, 100,  64, 40, -15, 1, 23),
                ( 140, 180,  70, 46,   8, 4, 24),
                ( 310, 230,  66, 42,  30, 1, 25),
                (  90, 270,  62, 40, -20, 4, 26),
                ( 400, 280,  60, 38,  12, 1, 27),
                ( 240, 350,  68, 44, -10, 4, 28),
                (  60, 460,  64, 40,  22, 1, 29),
                ( 310, 400,  70, 44,  -6, 4, 30),
                ( 180, 460,  66, 42,  16, 1, 31),
                ( 420, 460,  58, 38, -18, 4, 32),
                (  90, 555,  62, 40,  24, 1, 33),
                ( 300, 560,  70, 44,  -8, 4, 34),
                ( 420, 580,  60, 38,  14, 1, 35),
                ( 200, 640,  68, 42, -22, 4, 36),
                (  30, 640,  64, 40,  10, 1, 37),
                ( 360, 640,  62, 40,  -4, 4, 38),

                // ── МЕЛКИЕ акцентные пятна (детализация) ─────────────────────
                ( 280,  80,  44, 28,  -5, 3, 41),
                ( 130,  80,  40, 26,  18, 1, 42),
                ( 400, 200,  42, 26, -12, 3, 43),
                (  30, 165,  38, 24,  22, 2, 44),
                ( 240, 195,  46, 28,   8, 3, 45),
                ( 100, 325,  40, 26, -18, 1, 46),
                ( 420, 360,  38, 24,  14, 3, 47),
                ( 155, 390,  42, 28,  -8, 2, 48),
                ( 290, 500,  40, 26,  20, 3, 49),
                (  30, 510,  38, 24, -14, 1, 50),
                ( 410, 510,  42, 26,  10, 2, 51),
                ( 230, 590,  40, 26,  -6, 3, 52),
                ( 110, 610,  38, 24,  18, 1, 53),
                ( 360, 600,  40, 26, -20, 2, 54),
                (  60, 695,  42, 28,   8, 3, 55),
                ( 310, 695,  44, 26, -10, 1, 56),
            };

            // Порядок рисования: darkest → earth → forest → olive (как реальный камуфляж)
            int[] drawOrder = { 3, 1, 2, 4 };

            foreach (int pass in drawOrder)
                foreach (var b in blobs)
                    if (b.col == pass)
                    {
                        var color = pass switch
                        {
                            1 => ColEarth,
                            2 => ColForest,
                            3 => ColDarkest,
                            _ => ColOlive
                        };
                        DrawPremiumPatch(pix, W, H, b.cx, b.cy, b.rw, b.rh, b.ang, color, b.seed);
                    }

            // ── 3. Шумовая текстура — имитация зернистости ткани ─────────────
            var rngTex = new Random(999);
            for (int i = 0; i < pix.Length; i += 4)
            {
                int n = rngTex.Next(-9, 10);
                pix[i]     = Clamp(pix[i]     + n);
                pix[i + 1] = Clamp(pix[i + 1] + n);
                pix[i + 2] = Clamp(pix[i + 2] + n);
            }

            // ── 4. Виньетка левого и правого краёв (плавный переход) ──────────
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float left  = x < 40 ? 0.45f + 0.55f * (x / 40f) : 1f;
                    float right = x > W - 30 ? 0.65f + 0.35f * ((W - x) / 30f) : 1f;
                    float top   = y < 20 ? 0.70f + 0.30f * (y / 20f) : 1f;
                    float d     = left * right * top;
                    if (d >= 1f) continue;
                    int idx = (y * W + x) * 4;
                    pix[idx]     = (byte)(pix[idx]     * d);
                    pix[idx + 1] = (byte)(pix[idx + 1] * d);
                    pix[idx + 2] = (byte)(pix[idx + 2] * d);
                }

            var bmp = new WriteableBitmap(W, H);
            using var s = bmp.PixelBuffer.AsStream();
            s.Write(pix, 0, pix.Length);
            return bmp;
        }

        /// <summary>
        /// Рисует камуфляжное пятно с плавными органическими краями.
        /// Много вершин (22–28) + малое отклонение = премиальная плавность.
        /// </summary>
        static void DrawPremiumPatch(byte[] pix, int W, int H,
            float cx, float cy, float rw, float rh,
            float angleDeg, byte[] color, int seed)
        {
            var rng = new Random(seed * 1031 + 7);
            float baseAngle = angleDeg * MathF.PI / 180f;

            // Много вершин → очень гладкий контур
            int verts = rng.Next(22, 29);
            var vx = new float[verts];
            var vy = new float[verts];

            for (int i = 0; i < verts; i++)
            {
                // Равномерное угловое распределение с малым случайным сдвигом
                float a = 2f * MathF.PI * i / verts
                          + (float)(rng.NextDouble() - 0.5) * (MathF.PI / verts * 0.5f);

                // Малое отклонение радиуса (85%–100%) → плавный абрис
                float r = 0.85f + 0.15f * (float)rng.NextDouble();

                float lx = MathF.Cos(a) * rw * r;
                float ly = MathF.Sin(a) * rh * r;

                float cos = MathF.Cos(baseAngle);
                float sin = MathF.Sin(baseAngle);
                vx[i] = cx + lx * cos - ly * sin;
                vy[i] = cy + lx * sin + ly * cos;
            }

            float minX = vx[0], maxX = vx[0], minY = vy[0], maxY = vy[0];
            for (int i = 1; i < verts; i++)
            {
                if (vx[i] < minX) minX = vx[i];
                if (vx[i] > maxX) maxX = vx[i];
                if (vy[i] < minY) minY = vy[i];
                if (vy[i] > maxY) maxY = vy[i];
            }

            int x0 = Math.Max(0, (int)minX - 2);
            int x1 = Math.Min(W - 1, (int)maxX + 2);
            int y0 = Math.Max(0, (int)minY - 2);
            int y1 = Math.Min(H - 1, (int)maxY + 2);

            // Шум внутри пятна — лёгкая вариация тона для имитации ткани
            var rngInner = new Random(seed * 997 + 13);

            for (int py = y0; py <= y1; py++)
                for (int px = x0; px <= x1; px++)
                {
                    if (!PointInPolygon(px + 0.5f, py + 0.5f, vx, vy, verts)) continue;

                    // Широкая зона антиалиаса (3 px) — мягкий переход
                    float edgeDist = MinEdgeDist(px + 0.5f, py + 0.5f, vx, vy, verts);
                    float alpha = edgeDist < 3f ? edgeDist / 3f : 1f;

                    // Тонкий шум внутри пятна (±5) — зернистость
                    int n = rngInner.Next(-5, 6);

                    int idx = (py * W + px) * 4;
                    pix[idx]     = Lerp(pix[idx],     Clamp(color[0] + n), alpha);
                    pix[idx + 1] = Lerp(pix[idx + 1], Clamp(color[1] + n), alpha);
                    pix[idx + 2] = Lerp(pix[idx + 2], Clamp(color[2] + n), alpha);
                    pix[idx + 3] = 255;
                }
        }

        static bool PointInPolygon(float px, float py, float[] vx, float[] vy, int n)
        {
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if ((vy[i] > py) != (vy[j] > py) &&
                    px < (vx[j] - vx[i]) * (py - vy[i]) / (vy[j] - vy[i]) + vx[i])
                    inside = !inside;
            }
            return inside;
        }

        static float MinEdgeDist(float px, float py, float[] vx, float[] vy, int n)
        {
            float minD = float.MaxValue;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float ex = vx[i] - vx[j], ey = vy[i] - vy[j];
                float len2 = ex * ex + ey * ey;
                float t = len2 < 1e-6f ? 0f :
                    Math.Clamp(((px - vx[j]) * ex + (py - vy[j]) * ey) / len2, 0f, 1f);
                float dx = px - (vx[j] + t * ex);
                float dy = py - (vy[j] + t * ey);
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d < minD) minD = d;
            }
            return minD;
        }

        static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);

        static byte Lerp(byte a, byte b, float t) =>
            (byte)(a + (b - a) * Math.Clamp(t, 0f, 1f));

        public static WriteableBitmap CreateSolidBitmap(Color c)
        {
            const int S = 4;
            var pixels = new byte[S * S * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = c.B; pixels[i + 1] = c.G;
                pixels[i + 2] = c.R; pixels[i + 3] = c.A;
            }
            var bmp = new WriteableBitmap(S, S);
            using var stream = bmp.PixelBuffer.AsStream();
            stream.Write(pixels, 0, pixels.Length);
            return bmp;
        }
    }
}
