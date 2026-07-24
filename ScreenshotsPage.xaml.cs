using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MinecraftLauncher
{
    public class ScreenshotItem
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
    }

    public partial class ScreenshotsPage : Page
    {
        private string _targetFolder = "";

        public ScreenshotsPage(string gamePath)
        {
            InitializeComponent();
            _targetFolder = Path.Combine(gamePath, "screenshots");
            LoadScreenshots();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWin)
            {
                mainWin.CloseSettings();
            }
        }

        private void LoadScreenshots()
        {
            var list = new List<ScreenshotItem>();

            if (Directory.Exists(_targetFolder))
            {
                var files = Directory.GetFiles(_targetFolder, "*.png", SearchOption.TopDirectoryOnly);
                Array.Sort(files, (a, b) => File.GetCreationTime(b).CompareTo(File.GetCreationTime(a)));

                foreach (var file in files)
                {
                    list.Add(new ScreenshotItem
                    {
                        FileName = Path.GetFileName(file),
                        FilePath = file
                    });
                }
            }

            ScreenshotsItemsControl.ItemsSource = list;
            EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(_targetFolder);
            Process.Start("explorer.exe", _targetFolder);
        }

        private void Image_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image img && img.Tag is string path && File.Exists(path))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();

                PreviewImage.Source = bitmap;
                PreviewOverlay.Visibility = Visibility.Visible;
            }
        }

        private void ClosePreview_Click(object sender, MouseButtonEventArgs e)
        {
            PreviewOverlay.Visibility = Visibility.Collapsed;
            PreviewImage.Source = null;
        }

        private void CopyImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path && File.Exists(path))
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(path, UriKind.Absolute));
                    Clipboard.SetImage(bitmap);
                    MessageBox.Show("Скриншот скопирован в буфер обмена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка копирования: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path && File.Exists(path))
            {
                if (MessageBox.Show("Удалить выбранный скриншот?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Delete(path);
                        LoadScreenshots();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
