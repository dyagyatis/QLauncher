using System.Windows;
using System.Windows.Controls;
using MinecraftLauncher.Models;
using MinecraftLauncher.ViewModels;

namespace MinecraftLauncher.Views.Pages
{
    public partial class ModpacksPage : Page
    {
        public ModpacksViewModel ViewModel { get; }

        public ModpacksPage()
        {
            InitializeComponent();
            ViewModel = new ModpacksViewModel();
            DataContext = ViewModel;

            ViewModel.RequestManageModpack += OnRequestManageModpack;
            ViewModel.RequestCreateModpack += OnRequestCreateModpack;
            ViewModel.RequestClose += OnRequestClose;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWin)
            {
                mainWin.AnimateGoBack();
            }
            else if (NavigationService != null && NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private void SelectPack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string packName)
            {
                ViewModel.SelectPack(packName);
                if (Window.GetWindow(this) is MainWindow mainWin)
                {
                    mainWin.CloseSettings();
                }
            }
        }

        private void ManagePack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string packName)
            {
                ViewModel.ManagePack(packName);
            }
        }

        private void CreatePack_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CreatePack();
        }

        private void ImportZip_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ImportZip();
        }

        private void OnRequestManageModpack(ModpackProfile pack)
        {
            if (Window.GetWindow(this) is MainWindow mainWin)
            {
                mainWin.AnimateNavigate(new ProfileManagerPage(pack));
            }
            else
            {
                NavigationService?.Navigate(new ProfileManagerPage(pack));
            }
        }

        private void OnRequestCreateModpack()
        {
            if (Window.GetWindow(this) is MainWindow mainWin)
            {
                mainWin.CloseSettings();
                mainWin.OpenModpackOverlay();
            }
        }

        private void OnRequestClose()
        {
            if (Window.GetWindow(this) is MainWindow mainWin)
            {
                mainWin.CloseSettings();
            }
        }
    }
}
