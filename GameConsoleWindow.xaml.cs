using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace MinecraftLauncher
{
    public partial class GameConsoleWindow : Window
    {
        private Process? _gameProcess;

        public GameConsoleWindow()
        {
            InitializeComponent();
        }

        public void AttachProcess(Process process)
        {
            _gameProcess = process;
            _gameProcess.OutputDataReceived += (s, e) => AppendLogLine(e.Data, isError: false);
            _gameProcess.ErrorDataReceived += (s, e) => AppendLogLine(e.Data, isError: true);

            _gameProcess.Exited += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = $"Процесс завершился с кодом {_gameProcess.ExitCode}";
                    AppendLogLine($"--- Процесс завершён (Exit Code: {_gameProcess.ExitCode}) ---", isError: _gameProcess.ExitCode != 0);
                });
            };
        }

        public void AppendLogLine(string? line, bool isError)
        {
            if (string.IsNullOrEmpty(line)) return;

            Dispatcher.Invoke(() =>
            {
                Run run = new Run(line + "\n");

                if (isError || line.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase) || line.Contains("Exception", StringComparison.OrdinalIgnoreCase))
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(255, 99, 99)); // Красный
                }
                else if (line.Contains("[WARN]", StringComparison.OrdinalIgnoreCase))
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 80)); // Жёлтый
                }
                else if (line.Contains("[INFO]", StringComparison.OrdinalIgnoreCase))
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(160, 220, 255)); // Голубой
                }
                else
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(210, 210, 220)); // Обычный
                }

                LogParagraph.Inlines.Add(run);

                if (AutoScrollCheck.IsChecked == true)
                {
                    LogRichTextBox.ScrollToEnd();
                }
            });
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void CopyLog_Click(object sender, RoutedEventArgs e)
        {
            TextRange textRange = new TextRange(LogRichTextBox.Document.ContentStart, LogRichTextBox.Document.ContentEnd);
            Clipboard.SetText(textRange.Text);
            MessageBox.Show("Лог скопирован в буфер обмена!", "QLauncher", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogParagraph.Inlines.Clear();
        }
    }
}
