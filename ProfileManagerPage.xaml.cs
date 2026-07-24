using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MinecraftLauncher
{
    public class LocalModItem
    {
        public string FileName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsEnabled { get; set; }
        public string StatusText => IsEnabled ? "Включён" : "Отключён";
        public Brush StatusColor => IsEnabled ? new SolidColorBrush(Color.FromRgb(80, 220, 100)) : new SolidColorBrush(Color.FromRgb(220, 80, 80));
        public string ActionText => IsEnabled ? "Отключить" : "Включить";
    }

    public class WorldItem
    {
        public string WorldName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string LastPlayedText { get; set; } = "";
        public string IconPath { get; set; } = "/logo.png";
    }

    public partial class ProfileManagerPage : Page
    {
        private readonly ModpackProfile _profile;

        public ProfileManagerPage(ModpackProfile profile)
        {
            InitializeComponent();
            _profile = profile;
            TitleText.Text = $"Сборка: {profile.Name}";

            long hours = profile.PlaytimeMinutes / 60;
            long mins = profile.PlaytimeMinutes % 60;
            PlaytimeText.Text = $"Время в игре: {hours} ч {mins} мин • Запусков: {profile.LaunchCount}";

            LoadMods();
            LoadWorlds();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWin)
            {
                mainWin.CloseSettings();
            }
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (ModsView == null || WorldsView == null) return;
            if (TabModsRadio.IsChecked == true)
            {
                ModsView.Visibility = Visibility.Visible;
                WorldsView.Visibility = Visibility.Collapsed;
            }
            else
            {
                ModsView.Visibility = Visibility.Collapsed;
                WorldsView.Visibility = Visibility.Visible;
            }
        }

        // ==================== MODS LOGIC ====================
        private void LoadMods()
        {
            string modsDir = Path.Combine(_profile.FolderPath, "mods");
            var list = new List<LocalModItem>();

            if (Directory.Exists(modsDir))
            {
                var files = Directory.GetFiles(modsDir, "*.*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".jar" || ext == ".disabled" || file.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
                    {
                        bool enabled = ext == ".jar";
                        list.Add(new LocalModItem
                        {
                            FileName = Path.GetFileName(file),
                            FullPath = file,
                            IsEnabled = enabled
                        });
                    }
                }
            }

            LocalModsItemsControl.ItemsSource = list;
            if (NoModsText != null) NoModsText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ToggleMod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path && File.Exists(path))
            {
                try
                {
                    string newPath;
                    if (path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                    {
                        newPath = path.Substring(0, path.Length - ".disabled".Length);
                    }
                    else
                    {
                        newPath = path + ".disabled";
                    }

                    File.Move(path, newPath);
                    LoadMods();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка переименования файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteMod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path && File.Exists(path))
            {
                if (MessageBox.Show($"Удалить файл мода {Path.GetFileName(path)}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Delete(path);
                        LoadMods();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void OpenModsFolder_Click(object sender, RoutedEventArgs e)
        {
            string modsDir = Path.Combine(_profile.FolderPath, "mods");
            Directory.CreateDirectory(modsDir);
            Process.Start("explorer.exe", modsDir);
        }

        // ==================== WORLDS LOGIC ====================
        private void LoadWorlds()
        {
            string savesDir = Path.Combine(_profile.FolderPath, "saves");
            var list = new List<WorldItem>();

            if (Directory.Exists(savesDir))
            {
                var dirs = Directory.GetDirectories(savesDir);
                foreach (var dir in dirs)
                {
                    string icon = Path.Combine(dir, "icon.png");
                    if (!File.Exists(icon)) icon = "/logo.png";

                    var dirInfo = new DirectoryInfo(dir);
                    list.Add(new WorldItem
                    {
                        WorldName = dirInfo.Name,
                        FullPath = dir,
                        LastPlayedText = $"Изменён: {dirInfo.LastWriteTime:g}",
                        IconPath = icon
                    });
                }
            }

            WorldsItemsControl.ItemsSource = list;
            if (NoWorldsText != null) NoWorldsText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenSavesFolder_Click(object sender, RoutedEventArgs e)
        {
            string savesDir = Path.Combine(_profile.FolderPath, "saves");
            Directory.CreateDirectory(savesDir);
            Process.Start("explorer.exe", savesDir);
        }

        private void BackupWorld_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string worldPath && Directory.Exists(worldPath))
            {
                try
                {
                    string backupsDir = Path.Combine(_profile.FolderPath, "backups");
                    Directory.CreateDirectory(backupsDir);

                    string worldName = new DirectoryInfo(worldPath).Name;
                    string zipName = $"{worldName}_backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
                    string zipPath = Path.Combine(backupsDir, zipName);

                    ZipFile.CreateFromDirectory(worldPath, zipPath);
                    MessageBox.Show($"Бэкап успешно создан!\n\nПуть: {zipPath}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка создания бэкапа: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteWorld_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string worldPath && Directory.Exists(worldPath))
            {
                string name = new DirectoryInfo(worldPath).Name;
                if (MessageBox.Show($"Вы действительно хотите безвозвратно удалить мир '{name}'?", "Удаление мира", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        Directory.Delete(worldPath, true);
                        LoadWorlds();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления мира: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void CreatePack_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWin)
            {
                mainWin.CloseSettings();
                mainWin.OpenModpackOverlay_Click(sender, e);
            }
        }

        private void CreateShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWin)
            {
                mainWin.OpenShortcutOverlay_Click(sender, e);
            }
        }

        private void DeletePack_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWin)
            {
                mainWin.DeleteModpackBtn_Click(sender, e);
            }
        }

        private void ExportPackZip_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Zip Archive (*.zip)|*.zip",
                    FileName = $"{_profile.Name}_modpack.zip"
                };

                if (dlg.ShowDialog() == true)
                {
                    if (File.Exists(dlg.FileName)) File.Delete(dlg.FileName);
                    ZipFile.CreateFromDirectory(_profile.FolderPath, dlg.FileName);
                    MessageBox.Show($"Сборка '{_profile.Name}' успешно экспортирована в Zip!\nФайл: {dlg.FileName}", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте сборки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
