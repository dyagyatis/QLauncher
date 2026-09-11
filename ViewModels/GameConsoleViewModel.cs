using System;
using System.Diagnostics;
using System.Windows;
using MinecraftLauncher.Common;

namespace MinecraftLauncher.ViewModels
{
    public class GameConsoleViewModel : ViewModelBase
    {
        private string _processStatusText = "Ожидание запуска игры...";
        private bool _autoScroll = true;

        public string ProcessStatusText
        {
            get => _processStatusText;
            set => SetProperty(ref _processStatusText, value);
        }

        public bool AutoScroll
        {
            get => _autoScroll;
            set => SetProperty(ref _autoScroll, value);
        }

        public event Action<string, bool>? LogLineReceived;
        public event Action? RequestClear;
        public event Action? RequestCopy;

        public RelayCommand ClearLogCommand { get; }
        public RelayCommand CopyLogCommand { get; }

        public GameConsoleViewModel()
        {
            ClearLogCommand = new RelayCommand(() => RequestClear?.Invoke());
            CopyLogCommand = new RelayCommand(() => RequestCopy?.Invoke());
        }

        public void AttachProcess(Process process)
        {
            ProcessStatusText = $"Игра запущена (PID: {process.Id})";

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    LogLineReceived?.Invoke(e.Data, false);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    LogLineReceived?.Invoke(e.Data, true);
                }
            };

            process.Exited += (_, _) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProcessStatusText = $"Процесс завершился с кодом {process.ExitCode}";
                    LogLineReceived?.Invoke($"--- Процесс игры завершен (Exit Code: {process.ExitCode}) ---", process.ExitCode != 0);
                });
            };
        }
    }
}
