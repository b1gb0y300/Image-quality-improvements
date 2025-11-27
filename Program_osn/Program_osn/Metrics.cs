using System;
using System.Drawing;

namespace ImageEnhancement
{
    public static class Metrics
    {
        /// <summary>
        /// Перевод Bitmap в массив double [0..255].
        /// </summary>
        private static double[,] BitmapToGrayArray(Bitmap img)
        {
            int width = img.Width;
            int height = img.Height;
            var arr = new double[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int v = img.GetPixel(x, y).R; // для градаций серого R=G=B
                    arr[y, x] = v;
                }
            }

            return arr;
        }

        /// <summary>
        /// Рассчёт SSIM между двумя изображениями (должны быть одного размера).
        /// Используется скользящее окно size x size.
        /// </summary>
        public static double ComputeSSIM(Bitmap img1, Bitmap img2, int windowSize = 8)
        {
            if (img1.Width != img2.Width || img1.Height != img2.Height)
                throw new ArgumentException("Изображения должны быть одного размера");

            // Перевод в градации серого
            Bitmap g1 = ImageProcessors.ToGrayscale(img1);
            Bitmap g2 = ImageProcessors.ToGrayscale(img2);

            var x = BitmapToGrayArray(g1);
            var y = BitmapToGrayArray(g2);

            int width = g1.Width;
            int height = g1.Height;

            // Константы для стабильности (обычно берут от динамического диапазона L=255)
            double L = 255.0;
            double k1 = 0.01;
            double k2 = 0.03;
            double C1 = (k1 * L) * (k1 * L);
            double C2 = (k2 * L) * (k2 * L);

            int half = windowSize / 2;

            double ssimSum = 0.0;
            int windowsCount = 0;

            for (int cy = half; cy < height - half; cy++)
            {
                for (int cx = half; cx < width - half; cx++)
                {
                    // Локальное окно
                    double meanX = 0.0;
                    double meanY = 0.0;
                    int count = 0;

                    for (int j = -half; j <= half; j++)
                    {
                        for (int i = -half; i <= half; i++)
                        {
                            int yy = cy + j;
                            int xx = cx + i;

                            double xv = x[yy, xx];
                            double yv = y[yy, xx];

                            meanX += xv;
                            meanY += yv;
                            count++;
                        }
                    }

                    meanX /= count;
                    meanY /= count;

                    double varX = 0.0;
                    double varY = 0.0;
                    double covXY = 0.0;

                    for (int j = -half; j <= half; j++)
                    {
                        for (int i = -half; i <= half; i++)
                        {
                            int yy = cy + j;
                            int xx = cx + i;

                            double xv = x[yy, xx];
                            double yv = y[yy, xx];

                            double dx = xv - meanX;
                            double dy = yv - meanY;

                            varX += dx * dx;
                            varY += dy * dy;
                            covXY += dx * dy;
                        }
                    }

                    varX /= (count - 1);
                    varY /= (count - 1);
                    covXY /= (count - 1);

                    double numerator = (2 * meanX * meanY + C1) * (2 * covXY + C2);
                    double denominator = (meanX * meanX + meanY * meanY + C1) * (varX + varY + C2);

                    double ssimLocal = numerator / denominator;
                    ssimSum += ssimLocal;
                    windowsCount++;
                }
            }

            if (windowsCount == 0) return 0.0;
            return ssimSum / windowsCount;
        }
    }
}
