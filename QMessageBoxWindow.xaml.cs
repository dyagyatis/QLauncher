using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MinecraftLauncher
{
    public partial class QMessageBoxWindow : Window
    {
        // Свойство, в котором мы сохраним ответ пользователя (Yes, No, OK)
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public QMessageBoxWindow(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            InitializeComponent();

            MessageText.Text = message;
            TitleText.Text = title;

            // Настраиваем цвет верхней полоски в зависимости от типа сообщения
            if (image == MessageBoxImage.Error)
                TopAccent.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E81123")); // Красный
            else if (image == MessageBoxImage.Warning)
                TopAccent.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5A623")); // Оранжевый
            else
                TopAccent.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B85E6")); // Синий

            // Генерируем кнопки
            if (buttons == MessageBoxButton.YesNo)
            {
                AddButton("Да", MessageBoxResult.Yes, "#3B85E6", true);
                AddButton("Нет", MessageBoxResult.No, "#3A3D4D", false);
            }
            else // По умолчанию показываем просто "ОК"
            {
                AddButton("ОК", MessageBoxResult.OK, "#3B85E6", true);
            }
        }

        private void AddButton(string text, MessageBoxResult result, string colorHex, bool isPrimary)
        {
            Button btn = new Button
            {
                Content = text,
                Width = 90,
                Height = 35,
                Margin = new Thickness(10, 0, 0, 0),
                Foreground = Brushes.White,
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                BorderThickness = new Thickness(0),
                // Если у тебя есть глобальные стили кнопок в App.xaml, можешь подключить их тут:
                // Style = (Style)FindResource("PlayButtonStyle") 
            };

            // Делаем красивые закругления для кнопки через Template (чтобы не писать кучу XAML)
            btn.Template = CreateButtonTemplate(isPrimary ? 6 : 4);

            btn.Click += (s, e) =>
            {
                Result = result;
                this.Close();
            };

            ButtonsPanel.Children.Add(btn);
        }

        // Вспомогательный метод для закругления кнопок прямо из кода
        private ControlTemplate CreateButtonTemplate(int cornerRadius)
        {
            string xaml = $@"
                <ControlTemplate TargetType='Button' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                    <Border Background='{{TemplateBinding Background}}' CornerRadius='{cornerRadius}'>
                        <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                    </Border>
                </ControlTemplate>";
            return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        // =========================================================================
        // УДОБНЫЙ СТАТИЧЕСКИЙ МЕТОД ДЛЯ ВЫЗОВА ИЗ ЛЮБОГО МЕСТА ПРОГРАММЫ
        // =========================================================================
        public static MessageBoxResult Show(string message, string title = "Уведомление", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information)
        {
            QMessageBoxWindow msgBox = new QMessageBoxWindow(message, title, buttons, image);

            // Пытаемся привязать окно к главному окну лаунчера, чтобы оно вылезало по центру лаунчера
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                msgBox.Owner = Application.Current.MainWindow;
            }

            msgBox.ShowDialog(); // Блокируем лаунчер, пока не нажмут кнопку
            return msgBox.Result;
        }
    }
}