using Microsoft.Win32;
using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ImageEnhancement;

namespace ImageEnhancementWpf
{
    public partial class MainWindow : Window
    {
        private Bitmap? _originalBitmap;
        private Bitmap? _processedBitmap;

        private readonly double[] _zoomFactors = new[] { 1.0, 1.5, 2.0 };
        private int _originalZoomIndex = 0;
        private int _processedZoomIndex = 0;

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
                _originalBitmap?.Dispose();
                _processedBitmap?.Dispose();

                _originalBitmap = new Bitmap(ofd.FileName);
                _processedBitmap = null;

                OriginalImageControl.Source = BitmapToImageSource(_originalBitmap);
                ProcessedImageControl.Source = null;

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

        private void ResetZoom()
        {
            _originalZoomIndex = 0;
            _processedZoomIndex = 0;
            OriginalImageScale.ScaleX = OriginalImageScale.ScaleY = 1.0;
            ProcessedImageScale.ScaleX = ProcessedImageScale.ScaleY = 1.0;
        }

        private int NextZoomIndex(int current)
        {
            current++;
            if (current >= _zoomFactors.Length)
                current = 0;
            return current;
        }

        /// <summary>
        /// Применить метод обработки, показать результат и посчитать SSIM.
        /// </summary>
        private void ApplyAndShow(Func<Bitmap, Bitmap> processFunc, string methodName)
        {
            if (!CheckOriginalLoaded())
                return;

            _processedBitmap?.Dispose();
            _processedBitmap = processFunc(_originalBitmap!);

            ProcessedImageControl.Source = BitmapToImageSource(_processedBitmap);

            // при каждом новом результате сбрасываем зум для нижнего окна
            _processedZoomIndex = 0;
            ProcessedImageScale.ScaleX = ProcessedImageScale.ScaleY = 1.0;

            double ssim = MetricsComputeSafe();
            SsimValueTextBlock.Text = ssim.ToString("F4");

            this.Title = $"Улучшение качества изображений – {methodName}, SSIM={ssim:F4}";
        }

        private double MetricsComputeSafe()
        {
            if (_originalBitmap == null || _processedBitmap == null)
                return 0.0;

            return Metrics.ComputeSSIM(_originalBitmap, _processedBitmap);
        }

        #endregion

        #region Zoom по клику

        private void OriginalImageControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (OriginalImageControl.Source == null)
                return;

            _originalZoomIndex = NextZoomIndex(_originalZoomIndex);
            double factor = _zoomFactors[_originalZoomIndex];
            OriginalImageScale.ScaleX = factor;
            OriginalImageScale.ScaleY = factor;
        }

        private void ProcessedImageControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ProcessedImageControl.Source == null)
                return;

            _processedZoomIndex = NextZoomIndex(_processedZoomIndex);
            double factor = _zoomFactors[_processedZoomIndex];
            ProcessedImageScale.ScaleX = factor;
            ProcessedImageScale.ScaleY = factor;
        }

        #endregion

        #region Методы обработки

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
