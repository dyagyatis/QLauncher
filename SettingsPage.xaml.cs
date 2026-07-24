using CmlLib.Core.Auth.Microsoft;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace MinecraftLauncher
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings(); // Вызываем загрузку настроек при открытии страницы
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Load();

            // 1. Восстанавливаем галочки
            CloseOnLaunchCheck.IsChecked = settings.CloseOnLaunch;
            DiscordRpcCheck.IsChecked = settings.EnableDiscordRpc;
            UiSoundsCheck.IsChecked = settings.EnableUiSounds;
            AutoAddServersCheck.IsChecked = settings.AutoAddServers;
            HideServerIpCheck.IsChecked = settings.HideServerIp;

            // 2. Восстанавливаем ползунок памяти, путь к Java и пресет
            RamSlider.Value = settings.RamMb;
            JavaPathBox.Text = settings.JavaPath;

            foreach (ComboBoxItem item in JvmPresetBox.Items)
            {
                if (item.Tag?.ToString() == settings.JvmPreset)
                {
                    JvmPresetBox.SelectedItem = item;
                    break;
                }
            }

            // 3. Разрешение экрана и обои
            FullScreenCheck.IsChecked = settings.IsFullScreen;
            ResWidthBox.Text = settings.ScreenWidth.ToString();
            ResHeightBox.Text = settings.ScreenHeight.ToString();
            WallpaperPathBox.Text = settings.CustomWallpaperPath;

            UpdateCacheInfo(settings.GamePath);

            // 4. Восстанавливаем список аккаунтов
            if (AccountsListControl != null)
            {
                AccountsListControl.ItemsSource = null;
                AccountsListControl.ItemsSource = settings.Accounts;
            }
        }

        private void UpdateCacheInfo(string gamePath)
        {
            long sizeBytes = CacheCleanerHelper.CalculateCacheSize(gamePath);
            double sizeMb = sizeBytes / (1024.0 * 1024.0);
            CacheInfoText.Text = $"Размер кэша и логов: {sizeMb:F1} МБ";
        }

        private void BrowseWallpaper_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Image Files (*.jpg;*.png)|*.jpg;*.png" };
            if (dlg.ShowDialog() == true)
            {
                WallpaperPathBox.Text = dlg.FileName;
            }
        }

        private void ResetWallpaper_Click(object sender, RoutedEventArgs e)
        {
            WallpaperPathBox.Text = "";
        }

        private void CleanCache_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsManager.Load();
            double freedMb = CacheCleanerHelper.CleanCache(settings.GamePath);
            UpdateCacheInfo(settings.GamePath);
            QMessageBoxWindow.Show($"Кэш и журналы очищены! Освобождено {freedMb:F1} МБ.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string nickname)
            {
                var settings = SettingsManager.Load();
                var accToRemove = settings.Accounts.Find(a => a.Nickname == nickname);

                if (accToRemove != null)
                {
                    settings.Accounts.Remove(accToRemove);

                    // Если удалили активный аккаунт, сбрасываем выбор
                    if (settings.ActiveAccount == nickname) settings.ActiveAccount = "";

                    SettingsManager.Save(settings);
                    LoadSettings(); // Обновляем список на экране
                }
            }
        }

        private void RamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RamTextBox != null)
            {
                RamTextBox.Text = ((int)e.NewValue).ToString();
            }
        }

        private void BrowseJava_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Java Executable (javaw.exe)|javaw.exe|All Files (*.*)|*.*";
            if (dlg.ShowDialog() == true)
            {
                JavaPathBox.Text = dlg.FileName;
            }
        }

        private void AddOfflineAccount_Click(object sender, RoutedEventArgs e)
        {
            string nickname = OfflineNicknameBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(nickname) || nickname.Length < 2)
            {
                QMessageBoxWindow.Show("Пожалуйста, введите корректный никнейм (от 2 символов).", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var settings = SettingsManager.Load();

            // Проверяем, чтобы не добавить дубликат
            if (!settings.Accounts.Exists(a => a.Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase)))
            {
                settings.Accounts.Add(new AccountProfile
                {
                    Nickname = nickname,
                    Uuid = Guid.NewGuid().ToString("N"), // Фейковый UUID для пиратки
                    AccessToken = "offline" // Специальная метка
                });
            }

            settings.ActiveAccount = nickname;
            SettingsManager.Save(settings);

            QMessageBoxWindow.Show($"Оффлайн аккаунт '{nickname}' успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            OfflineNicknameBox.Text = "";
        }

        private async void AddMicrosoftAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Если кнопка нажата, меняем текст, чтобы игрок понимал, что процесс пошел
                if (sender is Button btn) btn.Content = "Ожидание входа в браузере...";

                // CmlLib сам откроет браузер и создаст локальный сервер для перехвата токена!
                var loginHandler = JELoginHandlerBuilder.BuildDefault();
                var session = await loginHandler.AuthenticateInteractively();

                var settings = SettingsManager.Load();

                // Проверяем, нет ли уже такого аккаунта, чтобы не дублировать
                if (!settings.Accounts.Exists(a => a.Nickname == session.Username))
                {
                    settings.Accounts.Add(new AccountProfile
                    {
                        Nickname = session.Username,
                        Uuid = session.UUID,
                        AccessToken = session.AccessToken
                    });
                }

                // Делаем его активным
                settings.ActiveAccount = session.Username;
                SettingsManager.Save(settings);

                QMessageBoxWindow.Show($"Лицензионный аккаунт {session.Username} успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                if (sender is Button b) b.Content = "Добавить аккаунт Microsoft";
            }
            catch (Exception ex)
            {
                QMessageBoxWindow.Show($"Ошибка авторизации: {ex.Message}\n\nВозможно, вы закрыли окно браузера.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                if (sender is Button b) b.Content = "Добавить аккаунт Microsoft";
            }
        }

        private void ExportSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json", FileName = "qlauncher_settings.json" };
            if (dlg.ShowDialog() == true)
            {
                var settings = SettingsManager.Load();
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json);
                QMessageBoxWindow.Show("Настройки успешно экспортированы!", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ImportSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "JSON Files (*.json)|*.json" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(dlg.FileName);
                    var settings = JsonSerializer.Deserialize<LauncherSettings>(json);
                    if (settings != null)
                    {
                        SettingsManager.Save(settings);
                        LoadSettings(); // Обновляем ползунки и чекбоксы на экране
                        QMessageBoxWindow.Show("Настройки успешно импортированы! Вернитесь в главное меню для обновления списков.", "Импорт", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch
                {
                    QMessageBoxWindow.Show("Ошибка при чтении файла. Файл поврежден или имеет неверный формат.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            var result = QMessageBoxWindow.Show("Вы уверены...", "Сброс настроек", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Создаем абсолютно чистые настройки и перезаписываем ими файл
                var newSettings = new LauncherSettings();
                SettingsManager.Save(newSettings);

                LoadSettings(); // Обновляем UI
                QMessageBoxWindow.Show("Настройки сброшены.", "Сброс", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = SettingsManager.Load();
                settings.CloseOnLaunch = CloseOnLaunchCheck.IsChecked ?? false;
                settings.EnableDiscordRpc = DiscordRpcCheck.IsChecked ?? true;
                settings.EnableUiSounds = UiSoundsCheck.IsChecked ?? true;
                settings.AutoAddServers = AutoAddServersCheck.IsChecked ?? true;
                settings.HideServerIp = HideServerIpCheck.IsChecked ?? false;
                settings.RamMb = (int)RamSlider.Value;
                settings.JavaPath = JavaPathBox.Text;
                settings.JvmPreset = (JvmPresetBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "default";

                int w = 1920, h = 1080;
                int.TryParse(ResWidthBox.Text, out w);
                int.TryParse(ResHeightBox.Text, out h);

                settings.IsFullScreen = FullScreenCheck.IsChecked ?? false;
                settings.ScreenWidth = Math.Max(800, w);
                settings.ScreenHeight = Math.Max(600, h);
                settings.CustomWallpaperPath = WallpaperPathBox.Text;

                SettingsManager.Save(settings);

                // Если DiscordManager у тебя еще не написан, закомментируй следующие две строки!
                // if (settings.EnableDiscordRpc) DiscordManager.Initialize();
                // else DiscordManager.Stop();

                ToastManager.ShowSuccess("Настройки успешно сохранены!", "Настройки");

                BackButton_Click(null, null); // Возвращаемся в меню
            }
            catch (Exception ex)
            {
                ToastManager.ShowError($"Сбой внутри кнопки сохранения:\n{ex.Message}", "Ошибка");
            }
        }

        private void ThemeAccent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string preset)
            {
                ThemeAccentManager.ApplyPreset(preset);
                ToastManager.ShowSuccess($"Акцентный цвет темы изменён!", "Тема оформления");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Возвращаемся в главное меню через главное окно
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.CloseSettings();
            }
        }
    }
}