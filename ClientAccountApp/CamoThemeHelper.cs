using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI;

namespace ClientAccountApp
{
    public static class CamoThemeHelper
    {
        // M81 Woodland BGRA
        private static readonly byte[] ColBase = { 0x24, 0x38, 0x1E, 0xFF };
        private static readonly byte[] ColBlack = { 0x0C, 0x14, 0x0A, 0xFF };
        private static readonly byte[] ColDkGreen = { 0x14, 0x2C, 0x16, 0xFF };
        private static readonly byte[] ColBrown = { 0x28, 0x38, 0x50, 0xFF };
        private static readonly byte[] ColMdGreen = { 0x38, 0x56, 0x2C, 0xFF };

        public static WriteableBitmap CreateM81Bitmap()
        {
            const int W = 220, H = 340;
            var pix = new byte[W * H * 4];

            // Фон
            for (int i = 0; i < pix.Length; i += 4)
            {
                pix[i] = ColBase[0]; pix[i + 1] = ColBase[1];
                pix[i + 2] = ColBase[2]; pix[i + 3] = 255;
            }

            // Пятна: cx, cy, w, h, angleDeg, colorIdx, seed
            // Намеренно вытянутые и повёрнутые — как на реальной ткани
            var blobs = new (float cx, float cy, float w, float h, float ang, int col, int seed)[]
            {
                // чёрные крупные
                ( 30,  35,  70, 38,  15, 3,  1),
                (155,  18,  55, 32, -20, 3,  2),
                ( 80, 105,  80, 42,  30, 3,  3),
                (188,  88,  58, 36,  -8, 3,  4),
                ( 18, 175,  64, 35,  22, 3,  5),
                (162, 160,  72, 40, -25, 3,  6),
                ( 65, 248,  68, 38,  18, 3,  7),
                (198, 230,  56, 34,  -5, 3,  8),
                ( 30, 308,  60, 36,  28, 3,  9),
                (148, 298,  70, 42, -15, 3, 10),

                // тёмно-зелёные средние
                (112,  40,  48, 28,  10, 2, 11),
                ( 55,  78,  52, 30, -18, 2, 12),
                (178, 140,  46, 32,  25, 2, 13),
                ( 25, 128,  44, 26,  -8, 2, 14),
                (122, 208,  54, 30,  20, 2, 15),
                ( 88, 285,  48, 28, -22, 2, 16),
                (210, 172,  40, 26,  12, 2, 17),
                (168,  52,  44, 28, -10, 2, 18),
                ( 45, 198,  50, 30,  32, 2, 19),
                (202, 308,  46, 28,  -6, 2, 20),

                // коричневые
                (100,  70,  38, 24,  -5, 1, 21),
                (160, 108,  42, 26,  18, 1, 22),
                ( 38, 152,  36, 22, -12, 1, 23),
                (118, 178,  44, 28,  25, 1, 24),
                (192, 252,  38, 24,  -8, 1, 25),
                ( 72, 325,  34, 22,  15, 1, 26),
                (142, 252,  40, 24,  -2, 1, 27),
                ( 20,  68,  34, 20,  20, 1, 28),
                (212,  48,  36, 22, -15, 1, 29),
                (175, 325,  38, 22,  10, 1, 30),

                // светло-зелёные небольшие перекрытия
                ( 95,  68,  30, 18,  -8, 4, 31),
                (160, 112,  34, 20,  22, 4, 32),
                ( 40, 158,  28, 18, -15, 4, 33),
                (115, 182,  36, 22,  12, 4, 34),
                (195, 258,  30, 18,  -5, 4, 35),
                ( 70, 330,  28, 18,  18, 4, 36),
            };

            int[] drawOrder = { 3, 2, 1, 4 };
            foreach (int pass in drawOrder)
                foreach (var b in blobs)
                    if (b.col == pass)
                    {
                        var color = pass switch
                        {
                            1 => ColBrown,
                            2 => ColDkGreen,
                            3 => ColBlack,
                            _ => ColMdGreen
                        };
                        DrawCamoPatch(pix, W, H, b.cx, b.cy, b.w, b.h, b.ang, color, b.seed);
                    }

            // Тонкое затемнение левого края
            for (int y = 0; y < H; y++)
                for (int x = 0; x < 20; x++)
                {
                    float d = 0.6f + 0.4f * (x / 20f);
                    int idx = (y * W + x) * 4;
                    pix[idx] = (byte)(pix[idx] * d);
                    pix[idx + 1] = (byte)(pix[idx + 1] * d);
                    pix[idx + 2] = (byte)(pix[idx + 2] * d);
                }

            var bmp = new WriteableBitmap(W, H);
            using var s = bmp.PixelBuffer.AsStream();
            s.Write(pix, 0, pix.Length);
            return bmp;
        }

        /// <summary>
        /// Рисует камуфляжное пятно как повёрнутый выпуклый многоугольник
        /// с 10–14 вершинами, у каждой вершины случайное отклонение ±30% от радиуса.
        /// Никаких синусоид — только прямолинейные грани, как на ткани.
        /// </summary>
        static void DrawCamoPatch(byte[] pix, int W, int H,
            float cx, float cy, float rw, float rh,
            float angleDeg, byte[] color, int seed)
        {
            var rng = new Random(seed * 1031 + 7);
            float baseAngle = angleDeg * MathF.PI / 180f;

            // Генерируем вершины многоугольника
            int verts = rng.Next(9, 15);
            var vx = new float[verts];
            var vy = new float[verts];

            for (int i = 0; i < verts; i++)
            {
                // Угол вершины — равномерно, но с небольшим случайным сдвигом
                float a = (float)(2 * Math.PI * i / verts)
                          + (float)(rng.NextDouble() - 0.5) * (float)(Math.PI / verts * 0.7);

                // Радиус — случайный в диапазоне 70%–100%
                float r = 0.70f + 0.30f * (float)rng.NextDouble();

                // Локальные координаты (эллипс)
                float lx = MathF.Cos(a) * rw * r;
                float ly = MathF.Sin(a) * rh * r;

                // Поворот
                float cos = MathF.Cos(baseAngle);
                float sin = MathF.Sin(baseAngle);
                vx[i] = cx + lx * cos - ly * sin;
                vy[i] = cy + lx * sin + ly * cos;
            }

            // Bounding box
            float minX = vx[0], maxX = vx[0], minY = vy[0], maxY = vy[0];
            for (int i = 1; i < verts; i++)
            {
                if (vx[i] < minX) minX = vx[i];
                if (vx[i] > maxX) maxX = vx[i];
                if (vy[i] < minY) minY = vy[i];
                if (vy[i] > maxY) maxY = vy[i];
            }

            int x0 = Math.Max(0, (int)minX - 1);
            int x1 = Math.Min(W - 1, (int)maxX + 1);
            int y0 = Math.Max(0, (int)minY - 1);
            int y1 = Math.Min(H - 1, (int)maxY + 1);

            for (int py = y0; py <= y1; py++)
                for (int px = x0; px <= x1; px++)
                {
                    if (!PointInPolygon(px + 0.5f, py + 0.5f, vx, vy, verts)) continue;

                    // Антиалиас: расстояние до ближайшего ребра
                    float edgeDist = MinEdgeDist(px + 0.5f, py + 0.5f, vx, vy, verts);
                    float alpha = edgeDist < 1.5f ? edgeDist / 1.5f : 1f;

                    int idx = (py * W + px) * 4;
                    pix[idx] = Lerp(pix[idx], color[0], alpha);
                    pix[idx + 1] = Lerp(pix[idx + 1], color[1], alpha);
                    pix[idx + 2] = Lerp(pix[idx + 2], color[2], alpha);
                    pix[idx + 3] = 255;
                }
        }

        // Ray-casting point-in-polygon
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

        // Минимальное расстояние до ребра для антиалиаса
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