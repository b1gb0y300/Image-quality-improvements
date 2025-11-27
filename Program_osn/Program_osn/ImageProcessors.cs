using System;
using System.Collections.Generic;
using System.Drawing;

namespace ImageEnhancement
{
    public static class ImageProcessors
    {
        // Вспомогательный метод: перевод в градации серого
        public static Bitmap ToGrayscale(Bitmap source)
        {
            var result = new Bitmap(source.Width, source.Height);

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color c = source.GetPixel(x, y);
                    // стандартная яркость по NTSC
                    int gray = (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                    gray = Math.Clamp(gray, 0, 255);
                    result.SetPixel(x, y, Color.FromArgb(gray, gray, gray));
                }
            }

            return result;
        }

        /// <summary>
        /// Линейное контрастирование (растяжение гистограммы).
        /// Берём min и max по изображению и растягиваем в [0; 255].
        /// </summary>
        public static Bitmap LinearContrastStretch(Bitmap source)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;

            int min = 255;
            int max = 0;

            // Находим min и max
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int v = gray.GetPixel(x, y).R;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }

            if (max == min)
            {
                // Изображение константное – вернуть копию
                return (Bitmap)gray.Clone();
            }

            var result = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int v = gray.GetPixel(x, y).R;
                    double stretched = (double)(v - min) / (max - min) * 255.0;
                    int newVal = Math.Clamp((int)Math.Round(stretched), 0, 255);
                    result.SetPixel(x, y, Color.FromArgb(newVal, newVal, newVal));
                }
            }

            return result;
        }

        /// <summary>
        /// Эквализация гистограммы для одноканального (градации серого) изображения.
        /// </summary>
        public static Bitmap HistogramEqualization(Bitmap source)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;
            int totalPixels = width * height;

            int[] hist = new int[256];

            // Строим гистограмму
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int v = gray.GetPixel(x, y).R;
                    hist[v]++;
                }
            }

            // Накопленная гистограмма (CDF)
            int[] cdf = new int[256];
            cdf[0] = hist[0];
            for (int i = 1; i < 256; i++)
            {
                cdf[i] = cdf[i - 1] + hist[i];
            }

            // Ищем первый ненулевой элемент cdf (cdf_min)
            int cdfMin = 0;
            for (int i = 0; i < 256; i++)
            {
                if (cdf[i] != 0)
                {
                    cdfMin = cdf[i];
                    break;
                }
            }

            // Формируем таблицу преобразования
            int[] map = new int[256];
            for (int i = 0; i < 256; i++)
            {
                if (cdf[i] == 0)
                {
                    map[i] = 0;
                }
                else
                {
                    double val = (double)(cdf[i] - cdfMin) / (totalPixels - cdfMin) * 255.0;
                    map[i] = Math.Clamp((int)Math.Round(val), 0, 255);
                }
            }

            var result = new Bitmap(width, height);

            // Применяем преобразование к каждому пикселю
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int v = gray.GetPixel(x, y).R;
                    int newVal = map[v];
                    result.SetPixel(x, y, Color.FromArgb(newVal, newVal, newVal));
                }
            }

            return result;
        }

        /// <summary>
        /// Медианная фильтрация (квадратное окно (2*radius+1)x(2*radius+1)).
        /// </summary>
        public static Bitmap MedianFilter(Bitmap source, int radius = 1)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;
            var result = new Bitmap(width, height);

            int windowSize = 2 * radius + 1;
            int windowPixelCount = windowSize * windowSize;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    List<int> values = new List<int>(windowPixelCount);

                    for (int j = -radius; j <= radius; j++)
                    {
                        int yy = y + j;
                        if (yy < 0 || yy >= height) continue;

                        for (int i = -radius; i <= radius; i++)
                        {
                            int xx = x + i;
                            if (xx < 0 || xx >= width) continue;

                            int v = gray.GetPixel(xx, yy).R;
                            values.Add(v);
                        }
                    }

                    values.Sort();
                    int median = values[values.Count / 2];
                    result.SetPixel(x, y, Color.FromArgb(median, median, median));
                }
            }

            return result;
        }

        /// <summary>
        /// Пространственный фильтр Винера.
        /// Локальная версия: y = μ + (σ^2 - σ_n^2) / σ^2 * (x - μ), если σ^2 > σ_n^2,
        /// иначе y = μ.
        /// noiseVariance – оценка дисперсии шума.
        /// windowRadius – радиус локального окна.
        /// </summary>
        public static Bitmap WienerFilter(Bitmap source, int windowRadius = 1, double noiseVariance = 10.0)
        {
            var gray = ToGrayscale(source);
            int width = gray.Width;
            int height = gray.Height;
            var result = new Bitmap(width, height);

            int windowSize = 2 * windowRadius + 1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Собираем локальное окно
                    List<double> window = new List<double>();

                    for (int j = -windowRadius; j <= windowRadius; j++)
                    {
                        int yy = y + j;
                        if (yy < 0 || yy >= height) continue;

                        for (int i = -windowRadius; i <= windowRadius; i++)
                        {
                            int xx = x + i;
                            if (xx < 0 || xx >= width) continue;

                            int v = gray.GetPixel(xx, yy).R;
                            window.Add(v);
                        }
                    }

                    // Локальное среднее
                    double mu = 0.0;
                    foreach (double v in window) mu += v;
                    mu /= window.Count;

                    // Локальная дисперсия
                    double sigma2 = 0.0;
                    foreach (double v in window)
                    {
                        double d = v - mu;
                        sigma2 += d * d;
                    }
                    sigma2 /= window.Count;

                    double xVal = gray.GetPixel(x, y).R;
                    double yVal;

                    if (sigma2 > noiseVariance && sigma2 > 0)
                    {
                        double k = (sigma2 - noiseVariance) / sigma2;
                        yVal = mu + k * (xVal - mu);
                    }
                    else
                    {
                        yVal = mu;
                    }

                    int outVal = Math.Clamp((int)Math.Round(yVal), 0, 255);
                    result.SetPixel(x, y, Color.FromArgb(outVal, outVal, outVal));
                }
            }

            return result;
        }
    }
}
