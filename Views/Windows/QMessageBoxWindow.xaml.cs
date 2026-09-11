using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace MinecraftLauncher.Views.Windows
{
    public partial class QMessageBoxWindow : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public QMessageBoxWindow(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            InitializeComponent();

            MessageText.Text = message;
            TitleText.Text = title;

            if (image == MessageBoxImage.Error)
                TopAccent.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E81123"));
            else if (image == MessageBoxImage.Warning)
                TopAccent.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5A623"));
            else
                TopAccent.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B85E6"));

            if (buttons == MessageBoxButton.YesNo)
            {
                AddButton("Да", MessageBoxResult.Yes, "#3B85E6", true);
                AddButton("Нет", MessageBoxResult.No, "#3A3D4D", false);
            }
            else
            {
                AddButton("ОК", MessageBoxResult.OK, "#3B85E6", true);
            }
        }

        private void AddButton(string text, MessageBoxResult result, string colorHex, bool isPrimary)
        {
            var btn = new Button
            {
                Content = text,
                Width = 90,
                Height = 35,
                Margin = new Thickness(10, 0, 0, 0),
                Foreground = Brushes.White,
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                BorderThickness = new Thickness(0),
                Template = CreateButtonTemplate(isPrimary ? 6 : 4)
            };

            btn.Click += (_, _) =>
            {
                Result = result;
                Close();
            };

            ButtonsPanel.Children.Add(btn);
        }

        private static ControlTemplate CreateButtonTemplate(int cornerRadius)
        {
            string xaml = $@"
                <ControlTemplate TargetType='Button' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                    <Border Background='{{TemplateBinding Background}}' CornerRadius='{cornerRadius}'>
                        <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                    </Border>
                </ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(xaml);
        }

        public static MessageBoxResult Show(string message, string title = "Уведомление", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information)
        {
            var msgBox = new QMessageBoxWindow(message, title, buttons, image);

            if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                msgBox.Owner = Application.Current.MainWindow;
            }

            msgBox.ShowDialog();
            return msgBox.Result;
        }
    }
}
