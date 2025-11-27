using Microsoft.Win32;
using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageEnhancement; // здесь лежат ImageProcessors и Metrics

namespace ImageEnhancementWpf
{
    public partial class MainWindow : Window
    {
        private Bitmap? _originalBitmap;
        private Bitmap? _processedBitmap;

        private double _originalZoom = 1.0;
        private double _processedZoom = 1.0;

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Загрузка / сохранение

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp"
            };

            if (ofd.ShowDialog() == true)
            {
                // Освобождаем старые битмапы, если были
                _originalBitmap?.Dispose();
                _processedBitmap?.Dispose();

                // Загружаем новое изображение
                _originalBitmap = new Bitmap(ofd.FileName);
                _processedBitmap = null;

                // Показываем исходное изображение
                OriginalImageControl.Source = BitmapToImageSource(_originalBitmap);
                ProcessedImageControl.Source = null;

                // Сбрасываем зум и SSIM
                ResetZoom();
                SsimValueTextBlock.Text = "—";

                this.Title = "Улучшение качества изображений – исходное изображение загружено";
            }
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            if (_processedBitmap == null)
            {
                MessageBox.Show("Нет обработанного изображения для сохранения.",
                                "Сохранение",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "PNG|*.png|JPEG|*.jpg;*.jpeg|BMP|*.bmp",
                FileName = "output.png"
            };

            if (sfd.ShowDialog() == true)
            {
                string ext = Path.GetExtension(sfd.FileName).ToLowerInvariant();
                var format = System.Drawing.Imaging.ImageFormat.Png;

                if (ext == ".jpg" || ext == ".jpeg")
                    format = System.Drawing.Imaging.ImageFormat.Jpeg;
                else if (ext == ".bmp")
                    format = System.Drawing.Imaging.ImageFormat.Bmp;

                _processedBitmap.Save(sfd.FileName, format);
            }
        }

        #endregion

        #region Вспомогательные методы

        private void ResetZoom()
        {
            _originalZoom = 1.0;
            _processedZoom = 1.0;

            OriginalImageScale.ScaleX = 1.0;
            OriginalImageScale.ScaleY = 1.0;

            ProcessedImageScale.ScaleX = 1.0;
            ProcessedImageScale.ScaleY = 1.0;
        }

        private static BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;

                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
        }

        private bool CheckOriginalLoaded()
        {
            if (_originalBitmap == null)
            {
                MessageBox.Show("Сначала загрузите исходное изображение.",
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Универсальный метод: применить обработку, показать результат и посчитать SSIM.
        /// </summary>
        private void ApplyAndShow(Func<Bitmap, Bitmap> processFunc, string methodName)
        {
            if (!CheckOriginalLoaded())
                return;

            // старый обработанный битмап освобождаем
            _processedBitmap?.Dispose();

            // вызываем нужный алгоритм
            _processedBitmap = processFunc(_originalBitmap!);

            // показываем картинку
            ProcessedImageControl.Source = BitmapToImageSource(_processedBitmap);

            // считаем SSIM
            double ssim = Metrics.ComputeSSIM(_originalBitmap!, _processedBitmap);
            SsimValueTextBlock.Text = ssim.ToString("F4");

            this.Title = $"Улучшение качества изображений – {methodName}, SSIM={ssim:F4}";
        }

        private double NextZoom(double current)
        {
            // цикл 1.0 → 1.5 → 2.0 → 1.0
            if (current < 1.5) return 1.5;
            if (current < 2.0) return 2.0;
            return 1.0;
        }

        #endregion

        #region Обработчики кликов по изображениям (zoom)

        private void OriginalImageControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (OriginalImageControl.Source == null)
                return;

            _originalZoom = NextZoom(_originalZoom);
            OriginalImageScale.ScaleX = _originalZoom;
            OriginalImageScale.ScaleY = _originalZoom;
        }

        private void ProcessedImageControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ProcessedImageControl.Source == null)
                return;

            _processedZoom = NextZoom(_processedZoom);
            ProcessedImageScale.ScaleX = _processedZoom;
            ProcessedImageScale.ScaleY = _processedZoom;
        }

        #endregion

        #region Кнопки методов обработки

        private void LinearContrast_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndShow(ImageProcessors.LinearContrastStretch,
                         "Линейное контрастирование");
        }

        private void HistogramEq_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndShow(ImageProcessors.HistogramEqualization,
                         "Эквализация гистограммы");
        }

        private void MedianFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndShow(src => ImageProcessors.MedianFilter(src, radius: 1),
                         "Медианный фильтр (3x3)");
        }

        private void WienerFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckOriginalLoaded())
                return;

            int radius = (int)WienerRadiusSlider.Value;

            // поддержка и точки, и запятой
            string text = NoiseVarianceTextBox.Text.Replace(',', '.');

            if (!double.TryParse(text,
                                 System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture,
                                 out double noiseVar))
            {
                MessageBox.Show("Некорректное значение дисперсии шума.",
                                "Ошибка ввода",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return;
            }

            ApplyAndShow(src => ImageProcessors.WienerFilter(src,
                                                             windowRadius: radius,
                                                             noiseVariance: noiseVar),
                         $"Фильтр Винера (r={radius}, σ²={noiseVar})");
        }

        #endregion
    }
}
