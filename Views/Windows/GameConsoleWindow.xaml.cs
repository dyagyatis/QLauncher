using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MinecraftLauncher.Services;
using MinecraftLauncher.ViewModels;

namespace MinecraftLauncher.Views.Windows
{
    public partial class GameConsoleWindow : Window
    {
        public GameConsoleViewModel ViewModel { get; }

        public GameConsoleWindow()
        {
            InitializeComponent();
            ViewModel = new GameConsoleViewModel();
            DataContext = ViewModel;

            ViewModel.LogLineReceived += OnLogLineReceived;
            ViewModel.RequestClear += OnRequestClear;
            ViewModel.RequestCopy += OnRequestCopy;
        }

        public void AttachProcess(Process process)
        {
            ViewModel.AttachProcess(process);
        }

        private void OnLogLineReceived(string line, bool isError)
        {
            Dispatcher.Invoke(() =>
            {
                var run = new Run(line + "\n");

                if (isError || line.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase) || line.Contains("Exception", StringComparison.OrdinalIgnoreCase))
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(255, 99, 99));
                }
                else if (line.Contains("[WARN]", StringComparison.OrdinalIgnoreCase))
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 80));
                }
                else if (line.Contains("[INFO]", StringComparison.OrdinalIgnoreCase))
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(160, 220, 255));
                }
                else
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(210, 210, 220));
                }

                LogParagraph.Inlines.Add(run);

                if (ViewModel.AutoScroll)
                {
                    LogRichTextBox.ScrollToEnd();
                }
            });
        }

        private void OnRequestClear()
        {
            LogParagraph.Inlines.Clear();
        }

        private void OnRequestCopy()
        {
            var textRange = new TextRange(LogRichTextBox.Document.ContentStart, LogRichTextBox.Document.ContentEnd);
            Clipboard.SetText(textRange.Text);
            ToastService.Instance.ShowSuccess("Журнал скопирован в буфер обмена.", "Консоль");
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaximizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();
    }
}
