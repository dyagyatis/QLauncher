using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MinecraftLauncher
{
    public class ModItem
    {
        public string Slug { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Author { get; set; } = "";
        public string IconUrl { get; set; } = "";
        public string Downloads { get; set; } = "";
        public string ProjectType { get; set; } = "mod";
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
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.CloseSettings();
            }
        }

        private void CategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCategoryHint();
        }

        private void UpdateCategoryHint()
        {
            if (CategoryDescText == null || CategoryBox == null) return;
            string tag = (CategoryBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "mod";
            CategoryDescText.Text = tag switch
            {
                "resourcepack" => "Поиск текстурпаков и ресурспаков для загрузки в папку resourcepacks",
                "shader" => "Поиск шейдеров (OptiFine / Iris) для загрузки в папку shaderpacks",
                _ => "Поиск модификаций для расширения игрового процесса"
            };
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

            string projectType = (CategoryBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "mod";

            LoadingText.Text = "Поиск...";
            LoadingText.Visibility = Visibility.Visible;
            ModsListControl.ItemsSource = null;

            try
            {
                // Формируем URL поиска Modrinth с ограничением типа (mod, resourcepack, shader)
                string facets = Uri.EscapeDataString($"[[\"project_type:{projectType}\"]]");
                string url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query)}&facets={facets}&limit=15";
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
                        Downloads = $"↓ {hit.GetProperty("downloads").GetInt32():N0}",
                        ProjectType = projectType
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

                string projectType = (CategoryBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "mod";

                ContextMenu menu = new ContextMenu();
                foreach (var pack in modpacks)
                {
                    if (projectType == "mod" && pack.Loader == "Vanilla") continue;

                    MenuItem item = new MenuItem { Header = $"Установить в: {pack.Name} ({pack.GameVersion})" };
                    item.Click += async (s, args) => await DownloadAndInstallToPack(slug, projectType, pack, btn);
                    menu.Items.Add(item);
                }

                if (menu.Items.Count == 0)
                {
                    MessageBox.Show("Нет доступных сборок для установки.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btn.ContextMenu = menu;
                menu.PlacementTarget = btn;
                menu.IsOpen = true;
            }
        }

        private async Task DownloadAndInstallToPack(string slug, string projectType, ModpackProfile pack, Button btn)
        {
            string originalContent = btn.Content.ToString() ?? "Установить";
            btn.Content = "Загрузка...";
            btn.IsEnabled = false;

            try
            {
                string loader = pack.Loader.ToLower();
                string url;

                if (projectType == "mod")
                {
                    url = $"https://api.modrinth.com/v2/project/{slug}/version?game_versions=[\"{pack.GameVersion}\"]&loaders=[\"{loader}\"]";
                }
                else
                {
                    url = $"https://api.modrinth.com/v2/project/{slug}/version?game_versions=[\"{pack.GameVersion}\"]";
                }

                string json = await HttpClient.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetArrayLength() == 0)
                {
                    // Пробуем скачать без фильтра версии
                    url = $"https://api.modrinth.com/v2/project/{slug}/version";
                    json = await HttpClient.GetStringAsync(url);
                    doc.Dispose();
                    using var fallbackDoc = JsonDocument.Parse(json);
                    root = fallbackDoc.RootElement.Clone();
                }

                if (root.GetArrayLength() == 0)
                {
                    MessageBox.Show($"Этот контент недоступен для скачивания.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var latestVersion = root[0];
                var files = latestVersion.GetProperty("files");

                if (files.GetArrayLength() > 0)
                {
                    string downloadUrl = files[0].GetProperty("url").GetString() ?? "";
                    string fileName = files[0].GetProperty("filename").GetString() ?? "";

                    string targetSubFolder = projectType switch
                    {
                        "resourcepack" => "resourcepacks",
                        "shader" => "shaderpacks",
                        _ => "mods"
                    };

                    string targetDir = Path.Combine(pack.FolderPath, targetSubFolder);
                    Directory.CreateDirectory(targetDir);

                    string filePath = Path.Combine(targetDir, fileName);

                    byte[] fileBytes = await HttpClient.GetByteArrayAsync(downloadUrl);
                    File.WriteAllBytes(filePath, fileBytes);

                    btn.Content = "Готово!";
                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при скачивании: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btn.Content = originalContent;
                btn.IsEnabled = true;
            }
        }
    }
}