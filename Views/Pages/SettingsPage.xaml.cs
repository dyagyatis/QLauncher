using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MinecraftLauncher.Models;
using MinecraftLauncher.Services;
using MinecraftLauncher.ViewModels;
using MinecraftLauncher.Views.Windows;

namespace MinecraftLauncher.Views.Pages
{
    public partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            InitializeComponent();
            ViewModel = new SettingsViewModel();
            DataContext = ViewModel;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.CloseSettings();
            }
        }

        private void ExportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                FileName = "qlauncher_settings.json",
                Filter = "JSON Files (*.json)|*.json",
                Title = "Экспорт настроек"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string json = JsonSerializer.Serialize(SettingsService.Instance.Settings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dlg.FileName, json);
                    ToastService.Instance.ShowSuccess("Настройки успешно экспортированы!", "Экспорт");
                }
                catch (Exception ex)
                {
                    ToastService.Instance.ShowError($"Ошибка экспорта: {ex.Message}", "Ошибка");
                }
            }
        }

        private void ImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                Title = "Импорт настроек"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(dlg.FileName);
                    var settings = JsonSerializer.Deserialize<LauncherSettings>(json);
                    if (settings != null)
                    {
                        SettingsService.Instance.Save(settings);
                        ViewModel.LoadFromSettings();
                        ToastService.Instance.ShowSuccess("Настройки успешно импортированы!", "Импорт");
                    }
                }
                catch (Exception ex)
                {
                    ToastService.Instance.ShowError($"Ошибка импорта: {ex.Message}", "Ошибка");
                }
            }
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            if (QMessageBoxWindow.Show("Сбросить все настройки лаунчера по умолчанию?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                var defaults = new LauncherSettings();
                SettingsService.Instance.Save(defaults);
                ViewModel.LoadFromSettings();
                ToastService.Instance.ShowInfo("Настройки сброшены до стандартных значений.", "Сброс");
            }
        }
    }
}
