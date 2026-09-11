using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MinecraftLauncher.ViewModels;

namespace MinecraftLauncher.Views.Pages
{
    public partial class ModsPage : Page
    {
        public ModsViewModel ViewModel { get; }

        public ModsPage()
        {
            InitializeComponent();
            ViewModel = new ModsViewModel();
            DataContext = ViewModel;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.CloseSettings();
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ViewModel.SearchCommand.CanExecute(null))
            {
                ViewModel.SearchCommand.Execute(null);
            }
        }
    }
}
