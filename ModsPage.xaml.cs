using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MinecraftLauncher
{
    // Класс для отображения мода в списке
    public class ModItem
    {
        public string Slug { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Author { get; set; } = "";
        public string IconUrl { get; set; } = "";
        public string Downloads { get; set; } = "";
    }

    public partial class ModsPage : Page
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public ModsPage()
        {
            InitializeComponent();
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("QLauncher_By_dyagnostic");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Возвращаемся в главное меню
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.CloseSettings(); // Этот же метод скроет MainFrame
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformSearch();
        }

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await PerformSearch();
            }
        }

        private async Task PerformSearch()
        {
            string query = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;

            LoadingText.Text = "Поиск...";
            LoadingText.Visibility = Visibility.Visible;
            ModsListControl.ItemsSource = null;

            try
            {
                // Поиск по Modrinth API
                string url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query)}&limit=15";
                string json = await HttpClient.GetStringAsync(url);

                using JsonDocument doc = JsonDocument.Parse(json);
                var hits = doc.RootElement.GetProperty("hits");

                var mods = new List<ModItem>();

                foreach (var hit in hits.EnumerateArray())
                {
                    mods.Add(new ModItem
                    {
                        Slug = hit.GetProperty("slug").GetString() ?? "",
                        Title = hit.GetProperty("title").GetString() ?? "Без названия",
                        Description = hit.GetProperty("description").GetString() ?? "",
                        Author = hit.GetProperty("author").GetString() ?? "Неизвестно",
                        IconUrl = hit.TryGetProperty("icon_url", out var icon) && icon.ValueKind != JsonValueKind.Null ? icon.GetString() : "/logo.png",
                        Downloads = $"↓ {hit.GetProperty("downloads").GetInt32():N0}"
                    });
                }

                ModsListControl.ItemsSource = mods;
                LoadingText.Visibility = mods.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
                if (mods.Count == 0) LoadingText.Text = "Ничего не найдено :(";
            }
            catch (Exception ex)
            {
                LoadingText.Text = "Ошибка загрузки: " + ex.Message;
            }
        }

        private void InstallMod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string slug)
            {
                var settings = SettingsManager.Load();
                var modpacks = settings.Modpacks;

                if (modpacks.Count == 0)
                {
                    MessageBox.Show("У вас пока нет ни одной сборки! Создайте её в главном меню.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Создаем выпадающее меню со списком сборок
                ContextMenu menu = new ContextMenu();
                foreach (var pack in modpacks)
                {
                    // Исключаем ванильные сборки (туда моды не ставятся)
                    if (pack.Loader == "Vanilla") continue;

                    MenuItem item = new MenuItem { Header = $"Установить в: {pack.Name} ({pack.GameVersion})" };
                    item.Click += async (s, args) => await DownloadAndInstallModToPack(slug, pack, btn);
                    menu.Items.Add(item);
                }

                if (menu.Items.Count == 0)
                {
                    MessageBox.Show("У вас нет сборок с установленным Fabric, Forge или Quilt.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Показываем меню прямо под кнопкой
                btn.ContextMenu = menu;
                menu.PlacementTarget = btn;
                menu.IsOpen = true;
            }
        }

        private async Task DownloadAndInstallModToPack(string slug, ModpackProfile pack, Button btn)
        {
            string originalContent = btn.Content.ToString() ?? "Установить";
            btn.Content = "Загрузка...";
            btn.IsEnabled = false;

            try
            {
                string loader = pack.Loader.ToLower(); // Modrinth понимает только маленькие буквы (fabric, forge)
                if (loader == "neoforge") loader = "neoforge"; // Адаптация названия

                // Ищем мод ИМЕННО под версию игры сборки и ИМЕННО под её загрузчик
                string url = $"https://api.modrinth.com/v2/project/{slug}/version?game_versions=[\"{pack.GameVersion}\"]&loaders=[\"{loader}\"]";

                string json = await HttpClient.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetArrayLength() == 0)
                {
                    MessageBox.Show($"Этот мод не поддерживает версию {pack.GameVersion} или загрузчик {pack.Loader}.", "Несовместимо", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Берем самый первый (самый новый) подходящий файл
                var latestVersion = root[0];
                var files = latestVersion.GetProperty("files");

                if (files.GetArrayLength() > 0)
                {
                    string downloadUrl = files[0].GetProperty("url").GetString() ?? "";
                    string fileName = files[0].GetProperty("filename").GetString() ?? "";

                    // Папка mods внутри папки сборки
                    string modsDir = Path.Combine(pack.FolderPath, "mods");
                    Directory.CreateDirectory(modsDir); // Создаем, если её еще нет

                    string filePath = Path.Combine(modsDir, fileName);

                    // Скачиваем сам .jar файл
                    byte[] fileBytes = await HttpClient.GetByteArrayAsync(downloadUrl);
                    File.WriteAllBytes(filePath, fileBytes);

                    btn.Content = "Готово!";
                    await Task.Delay(2000); // Показываем "Готово!" 2 секунды
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при скачивании мода: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btn.Content = originalContent;
                btn.IsEnabled = true;
            }
        }
    }
}