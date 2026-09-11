using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MinecraftLauncher.Views.Controls
{
    public enum ToastType
    {
        Success,
        Error,
        Warning,
        Info
    }

    public partial class ToastNotification : UserControl
    {
        public ToastNotification()
        {
            InitializeComponent();
        }

        public async void Show(string title, string message, ToastType type = ToastType.Info, int durationMs = 3000)
        {
            TitleText.Text = title;
            MessageText.Text = message;

            switch (type)
            {
                case ToastType.Success:
                    IconText.Text = "\uE73E";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(80, 220, 100));
                    ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(80, 220, 100));
                    break;
                case ToastType.Error:
                    IconText.Text = "\uEA39";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(255, 90, 90));
                    ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 90, 90));
                    break;
                case ToastType.Warning:
                    IconText.Text = "\uE7BA";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(255, 195, 60));
                    ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 195, 60));
                    break;
                case ToastType.Info:
                default:
                    IconText.Text = "\uE946";
                    IconText.Foreground = (Brush)Application.Current.FindResource("AccentBrush");
                    ToastBorder.BorderBrush = (Brush)Application.Current.FindResource("AccentBrush");
                    break;
            }

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
            var slideUp = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(250));

            var transform = new TranslateTransform();
            ToastBorder.RenderTransform = transform;

            ToastBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            transform.BeginAnimation(TranslateTransform.YProperty, slideUp);

            await Task.Delay(durationMs);

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250));
            fadeOut.Completed += (_, _) =>
            {
                if (Parent is Panel parentPanel)
                {
                    parentPanel.Children.Remove(this);
                }
            };
            ToastBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }
}
