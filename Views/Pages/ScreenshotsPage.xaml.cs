using System.Windows;
using System.Windows.Controls;
using MinecraftLauncher.ViewModels;

namespace MinecraftLauncher.Views.Pages
{
    public partial class ScreenshotsPage : Page
    {
        public ScreenshotsViewModel ViewModel { get; }

        public ScreenshotsPage(string gamePath)
        {
            InitializeComponent();
            ViewModel = new ScreenshotsViewModel(gamePath);
            DataContext = ViewModel;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWin)
            {
                mainWin.CloseSettings();
            }
        }
    }
}
