using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using MinecraftLauncher.Common;
using MinecraftLauncher.Models;
using MinecraftLauncher.Services;
using MinecraftLauncher.Services.LaunchEngine;
using MinecraftLauncher.Views.Windows;

namespace MinecraftLauncher.ViewModels
{
    public class ModpacksViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IToastService _toastService;
        private readonly IAudioService _audioService;

        public ObservableCollection<ModpackCardItem> Modpacks { get; } = new();

        public event Action<ModpackProfile>? RequestManageModpack;
        public event Action? RequestCreateModpack;
        public event Action? RequestClose;

        public RelayCommand<string> SelectPackCommand { get; }
        public RelayCommand<string> ManagePackCommand { get; }
        public RelayCommand<string> OpenPackFolderCommand { get; }
        public RelayCommand<string> OpenPackModsCommand { get; }
        public RelayCommand<string> DuplicatePackCommand { get; }
        public RelayCommand<string> CreateShortcutCommand { get; }
        public RelayCommand<string> DeletePackCommand { get; }

        public ModpacksViewModel() : this(SettingsService.Instance, ToastService.Instance, AudioService.Instance)
        {
        }

        public ModpacksViewModel(ISettingsService settingsService, IToastService toastService, IAudioService audioService)
        {
            _settingsService = settingsService;
            _toastService = toastService;
            _audioService = audioService;

            SelectPackCommand = new RelayCommand<string>(s => { if (s != null) SelectPack(s); });
            ManagePackCommand = new RelayCommand<string>(s => { if (s != null) ManagePack(s); });
            OpenPackFolderCommand = new RelayCommand<string>(ExecuteOpenPackFolder);
            OpenPackModsCommand = new RelayCommand<string>(ExecuteOpenPackMods);
            DuplicatePackCommand = new RelayCommand<string>(ExecuteDuplicatePack);
            CreateShortcutCommand = new RelayCommand<string>(ExecuteCreateShortcut);
            DeletePackCommand = new RelayCommand<string>(ExecuteDeletePack);

            LoadModpacks();
        }

        public void LoadModpacks()
        {
            var settings = _settingsService.Load();
            Modpacks.Clear();

            string currentActive = settings.LastSelectedVersion ?? "";

            foreach (var pack in settings.Modpacks)
            {
                string packTag = $"⭐ {pack.Name} ({pack.Loader})";
                bool isActive = currentActive == packTag || currentActive.StartsWith($"⭐ {pack.Name}");

                long hours = pack.PlaytimeMinutes / 60;
                long mins = pack.PlaytimeMinutes % 60;

                Modpacks.Add(new ModpackCardItem
                {
                    Name = pack.Name,
                    LoaderTag = string.IsNullOrWhiteSpace(pack.Loader) ? "Vanilla" : pack.Loader,
                    GameVersionTag = string.IsNullOrWhiteSpace(pack.GameVersion) ? "1.20.1" : pack.GameVersion,
                    PlaytimeText = $"{hours} ч {mins} мин • {pack.LaunchCount} запусков",
                    IsActiveVisibility = isActive ? Visibility.Visible : Visibility.Collapsed
                });
            }
        }

        public void SelectPack(string packName)
        {
            var settings = _settingsService.Settings;
            var pack = settings.Modpacks.Find(p => p.Name == packName);
            if (pack != null)
            {
                string packTag = $"⭐ {pack.Name} ({pack.Loader})";
                settings.LastSelectedVersion = packTag;
                _settingsService.Save(settings);

                LoadModpacks();
                _toastService.ShowSuccess($"Активная сборка переключена на '{pack.Name}'", "Сборка");
                RequestClose?.Invoke();
            }
        }

        public void ManagePack(string packName)
        {
            _audioService.PlayClickSound(_settingsService.Settings.EnableUiSounds);
            var settings = _settingsService.Settings;
            var pack = settings.Modpacks.Find(p => p.Name == packName);
            if (pack != null)
            {
                RequestManageModpack?.Invoke(pack);
            }
        }

        public void CreatePack()
        {
            RequestCreateModpack?.Invoke();
        }

        public async void ImportZip()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Modpack files (*.mrpack;*.zip)|*.mrpack;*.zip|Modrinth Pack (*.mrpack)|*.mrpack|Zip Archive (*.zip)|*.zip",
                Title = "Импорт сборки Minecraft"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var settings = _settingsService.Settings;
                    string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();

                    if (ext == ".mrpack")
                    {
                        _toastService.ShowInfo("Начат импорт Modrinth сборки (.mrpack)...", "Импорт");
                        var profile = await MrPackInstaller.InstallMrPackAsync(dlg.FileName, settings.GamePath);
                        settings.Modpacks.Add(profile);
                        _settingsService.Save(settings);

                        LoadModpacks();
                        _toastService.ShowSuccess($"Сборка '{profile.Name}' ({profile.Loader}) успешно установлена!", "Импорт .mrpack");
                    }
                    else
                    {
                        string packName = Path.GetFileNameWithoutExtension(dlg.FileName);
                        string instancesPath = Path.Combine(settings.GamePath, "instances", packName);
                        if (Directory.Exists(instancesPath))
                        {
                            packName += "_" + DateTime.Now.ToString("HHmmss");
                            instancesPath = Path.Combine(settings.GamePath, "instances", packName);
                        }

                        Directory.CreateDirectory(instancesPath);
                        ZipFile.ExtractToDirectory(dlg.FileName, instancesPath, true);

                        settings.Modpacks.Add(new ModpackProfile
                        {
                            Name = packName,
                            GameVersion = "1.20.1",
                            Loader = "Custom",
                            FolderPath = instancesPath
                        });

                        _settingsService.Save(settings);
                        LoadModpacks();
                        _toastService.ShowSuccess($"Сборка '{packName}' успешно импортирована!", "Импорт");
                    }
                }
                catch (Exception ex)
                {
                    _toastService.ShowError($"Ошибка импорта: {ex.Message}", "Ошибка");
                }
            }
        }

        private void ExecuteOpenPackFolder(string? packName)
        {
            if (string.IsNullOrWhiteSpace(packName)) return;
            var pack = _settingsService.Settings.Modpacks.Find(p => p.Name == packName);
            if (pack != null && Directory.Exists(pack.FolderPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = pack.FolderPath, UseShellExecute = true });
                }
                catch { }
            }
        }

        private void ExecuteOpenPackMods(string? packName)
        {
            if (string.IsNullOrWhiteSpace(packName)) return;
            var pack = _settingsService.Settings.Modpacks.Find(p => p.Name == packName);
            if (pack != null)
            {
                string modsDir = Path.Combine(pack.FolderPath, "mods");
                Directory.CreateDirectory(modsDir);
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = modsDir, UseShellExecute = true });
                }
                catch { }
            }
        }

        private void ExecuteDuplicatePack(string? packName)
        {
            if (string.IsNullOrWhiteSpace(packName)) return;
            var pack = _settingsService.Settings.Modpacks.Find(p => p.Name == packName);
            if (pack == null || !Directory.Exists(pack.FolderPath)) return;

            try
            {
                string newName = $"{pack.Name} (Копия)";
                string instancesDir = Path.Combine(_settingsService.Settings.GamePath, "instances");
                string newFolder = Path.Combine(instancesDir, newName);
                int counter = 2;
                while (Directory.Exists(newFolder) || _settingsService.Settings.Modpacks.Exists(p => p.Name == newName))
                {
                    newName = $"{pack.Name} (Копия {counter++})";
                    newFolder = Path.Combine(instancesDir, newName);
                }

                Directory.CreateDirectory(newFolder);
                CopyDirectory(pack.FolderPath, newFolder);

                var newProfile = new ModpackProfile
                {
                    Name = newName,
                    GameVersion = pack.GameVersion,
                    Version = pack.Version,
                    Loader = pack.Loader,
                    FolderPath = newFolder
                };

                var settings = _settingsService.Settings;
                settings.Modpacks.Add(newProfile);
                _settingsService.Save(settings);

                LoadModpacks();
                _toastService.ShowSuccess($"Сборка '{newName}' успешно создана!", "Клонирование");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Ошибка дублирования: {ex.Message}", "Ошибка");
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }

        private void ExecuteCreateShortcut(string? packName)
        {
            if (string.IsNullOrWhiteSpace(packName)) return;
            var pack = _settingsService.Settings.Modpacks.Find(p => p.Name == packName);
            if (pack == null) return;

            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutFile = Path.Combine(desktop, $"Minecraft ({pack.Name}).lnk");
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";

                if (!string.IsNullOrEmpty(exePath))
                {
                    Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType)!;
                        dynamic shortcut = shell.CreateShortcut(shortcutFile);
                        shortcut.TargetPath = exePath;
                        string packTag = $"⭐ {pack.Name} ({pack.Loader})";
                        shortcut.Arguments = $"-quickplay \"{packTag}\"";
                        shortcut.IconLocation = $"{exePath},0";
                        shortcut.WorkingDirectory = Path.GetDirectoryName(exePath) ?? "";
                        shortcut.Save();

                        _toastService.ShowSuccess($"Ярлык для '{pack.Name}' создан на рабочем столе!", "Ярлык");
                    }
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Ошибка создания ярлыка: {ex.Message}", "Ошибка");
            }
        }

        private void ExecuteDeletePack(string? packName)
        {
            if (string.IsNullOrWhiteSpace(packName)) return;
            var pack = _settingsService.Settings.Modpacks.Find(p => p.Name == packName);
            if (pack == null) return;

            if (QMessageBoxWindow.Show($"Удалить сборку '{pack.Name}' и все её файлы?", "Удаление сборки", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    if (Directory.Exists(pack.FolderPath))
                    {
                        Directory.Delete(pack.FolderPath, true);
                    }
                }
                catch { }

                var settings = _settingsService.Settings;
                settings.Modpacks.Remove(pack);
                _settingsService.Save(settings);

                LoadModpacks();
                _toastService.ShowInfo($"Сборка '{pack.Name}' удалена.", "Успешно");
            }
        }
    }
}
