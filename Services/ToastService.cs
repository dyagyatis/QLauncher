using System.Windows;
using System.Windows.Controls;
using MinecraftLauncher.Views.Controls;

namespace MinecraftLauncher.Services
{
    public interface IToastService
    {
        void RegisterContainer(Panel container);
        void ShowSuccess(string message, string title = "Успех");
        void ShowError(string message, string title = "Ошибка");
        void ShowWarning(string message, string title = "Внимание");
        void ShowInfo(string message, string title = "Информация");
    }

    public class ToastService : IToastService
    {
        private Panel? _toastContainer;

        public static ToastService Instance { get; } = new ToastService();

        public void RegisterContainer(Panel container)
        {
            _toastContainer = container;
        }

        public void ShowSuccess(string message, string title = "Успех") =>
            ShowToast(title, message, ToastType.Success);

        public void ShowError(string message, string title = "Ошибка") =>
            ShowToast(title, message, ToastType.Error);

        public void ShowWarning(string message, string title = "Внимание") =>
            ShowToast(title, message, ToastType.Warning);

        public void ShowInfo(string message, string title = "Информация") =>
            ShowToast(title, message, ToastType.Info);

        private void ShowToast(string title, string message, ToastType type)
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
