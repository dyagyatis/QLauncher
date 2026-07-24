using System.Windows;
using System.Windows.Controls;

namespace MinecraftLauncher
{
    public static class ToastManager
    {
        private static Panel? _toastContainer;

        public static void RegisterContainer(Panel container)
        {
            _toastContainer = container;
        }

        public static void ShowSuccess(string message, string title = "Успех")
        {
            ShowToast(title, message, ToastType.Success);
        }

        public static void ShowError(string message, string title = "Ошибка")
        {
            ShowToast(title, message, ToastType.Error);
        }

        public static void ShowWarning(string message, string title = "Внимание")
        {
            ShowToast(title, message, ToastType.Warning);
        }

        public static void ShowInfo(string message, string title = "Информация")
        {
            ShowToast(title, message, ToastType.Info);
        }

        private static void ShowToast(string title, string message, ToastType type)
        {
            if (_toastContainer == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var toast = new ToastNotification();
                _toastContainer.Children.Add(toast);
                toast.Show(title, message, type);
            });
        }
    }
}
