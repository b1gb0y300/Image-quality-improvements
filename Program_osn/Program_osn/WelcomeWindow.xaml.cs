using System.Windows;

namespace ImageEnhancementWpf
{
    public partial class WelcomeWindow : Window
    {
        public WelcomeWindow()
        {
            InitializeComponent();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            var main = new MainWindow(); // это твое рабочее окно
            main.Show();
            this.Close(); // закрываем приветственное
        }
    }
}
