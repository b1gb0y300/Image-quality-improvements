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
            var main = new MainWindow();
            main.Show();
            Close();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var help = new HelpWindow();
            help.Owner = this;
            help.ShowDialog();
        }
    }
}
