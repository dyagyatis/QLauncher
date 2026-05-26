using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace MinecraftLauncher
{
    public partial class SplashScreenWindow : Window
    {
        public SplashScreenWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Запускаем процесс фейковой/реальной загрузки
            await SimulateLoadingAsync();

            // Создаем и показываем главное окно
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();

            // Закрываем загрузочный экран
            this.Close();
        }

        private async Task SimulateLoadingAsync()
        {
            // Шаг 1
            StatusText.Text = "Инициализация ядра лаунчера...";
            AnimateProgressBar(50); // Заполняем на 50 пикселей из 300
            await Task.Delay(600);  // Имитируем работу

            // Шаг 2
            StatusText.Text = "Загрузка настроек и профилей...";
            AnimateProgressBar(150);
            await Task.Delay(800);

            // Шаг 3
            StatusText.Text = "Проверка обновлений...";
            AnimateProgressBar(250);
            await Task.Delay(700);

            // Шаг 4
            StatusText.Text = "Готово!";
            AnimateProgressBar(300); // 100%
            await Task.Delay(400); // Даем игроку увидеть надпись "Готово!"
        }

        // Метод для плавной анимации полоски
        private void AnimateProgressBar(double toWidth)
        {
            var anim = new DoubleAnimation(toWidth, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressBarFill.BeginAnimation(FrameworkElement.WidthProperty, anim);
        }
    }
}