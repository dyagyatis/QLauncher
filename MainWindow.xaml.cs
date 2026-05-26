using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Installer;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;
using fNbt;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MinecraftLauncher
{
    public class TelegramPost
    {
        public string Date { get; set; } = "";
        public string Text { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public Visibility ImageVisibility => string.IsNullOrEmpty(ImageUrl) ? Visibility.Collapsed : Visibility.Visible;
    }

    public class CornerRadiusAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(CornerRadius);
        protected override Freezable CreateInstanceCore() => new CornerRadiusAnimation();
        public static readonly DependencyProperty FromProperty = DependencyProperty.Register("From", typeof(CornerRadius), typeof(CornerRadiusAnimation));
        public CornerRadius From { get => (CornerRadius)GetValue(FromProperty); set => SetValue(FromProperty, value); }
        public static readonly DependencyProperty ToProperty = DependencyProperty.Register("To", typeof(CornerRadius), typeof(CornerRadiusAnimation));
        public CornerRadius To { get => (CornerRadius)GetValue(ToProperty); set => SetValue(ToProperty, value); }
        public static readonly DependencyProperty EasingFunctionProperty = DependencyProperty.Register("EasingFunction", typeof(IEasingFunction), typeof(CornerRadiusAnimation));
        public IEasingFunction EasingFunction { get => (IEasingFunction)GetValue(EasingFunctionProperty); set => SetValue(EasingFunctionProperty, value); }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            if (!animationClock.CurrentProgress.HasValue) return From;
            double p = animationClock.CurrentProgress.Value;
            if (EasingFunction != null) p = EasingFunction.Ease(p);
            return new CornerRadius(
                From.TopLeft + (To.TopLeft - From.TopLeft) * p,
                From.TopRight + (To.TopRight - From.TopRight) * p,
                From.BottomRight + (To.BottomRight - From.BottomRight) * p,
                From.BottomLeft + (To.BottomLeft - From.BottomLeft) * p
            );
        }
    }
    
    
    public partial class MainWindow : Window
    {
        private CmlLib.Core.MinecraftLauncher _launcher = null!;
        private MinecraftPath _minecraftPath = null!;
        private double _normalLeft, _normalTop, _normalWidth, _normalHeight;
        private bool _isCustomMaximized = false;
        private DispatcherTimer _newsTimer = null!;

        private long _lastBytes = 0;
        private DateTime _lastTime = DateTime.Now;
        private string _currentSpeedStr = "0 МБ/с";

        private static readonly HttpClient SharedHttpClient = CreateAntiBlockClient();

        private static HttpClient CreateAntiBlockClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };

            var client = new HttpClient(handler);
            client.DefaultRequestVersion = new Version(2, 0);

            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,application/json,*/*;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua", "\"Chromium\";v=\"122\", \"Not(A:Brand\";v=\"24\", \"Google Chrome\";v=\"122\"");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua-Mobile", "?0");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua-Platform", "\"Windows\"");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "none");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-User", "?1");

            return client;
        }

        // === ЛОГИКА СМЕНЫ ТЕМЫ (С АНИМАЦИЕЙ) ===
        private bool _isDarkTheme = true;

        private void AnimateNavigate(Page page)
        {
            HomeView.Visibility = Visibility.Collapsed;
            MainFrame.Visibility = Visibility.Visible;
            MainFrame.Navigate(page);

            // Анимация выезда справа (от 100 пикселей до 0)
            DoubleAnimation slideAnim = new DoubleAnimation
            {
                From = 100,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // Анимация прозрачности
            DoubleAnimation fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            MainFrameTransform.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            MainFrame.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        private void ThemeToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            string themeName = _isDarkTheme ? "DarkTheme.xaml" : "LightTheme.xaml";

            var uri = new Uri($"Themes/{themeName}", UriKind.Relative);
            var dict = new ResourceDictionary() { Source = uri };

            // Анимация исчезновения
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, args) =>
            {
                // Смена цветов
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(dict);

                if (sender is Button btn) btn.Content = _isDarkTheme ? "\uE706" : "\uE708";

                // Анимация появления
                var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
                ContentGrid.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };

            ContentGrid.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        public MainWindow()
        {
            InitializeComponent();
            DiscordManager.StartRpc();
            Loaded += MainWindow_Loaded;
        }

        public class ServerListItem
        {
            public string Name { get; set; } = "";
            public string Ip { get; set; } = "";
            public string OnlineText { get; set; } = "Загрузка...";
            public string Version { get; set; } = "...";
            public double ProgressWidth { get; set; } = 0;
        }

        private async Task UpdateServerStatus()
        {
            try
            {
                var settings = SettingsManager.Load();
                string serversFile = Path.Combine(settings.GamePath, "servers.dat");

                var uiServers = new List<ServerListItem>();

                // 1. Читаем servers.dat
                if (File.Exists(serversFile))
                {
                    var nbtFile = new NbtFile();
                    nbtFile.LoadFromFile(serversFile);
                    var serversList = nbtFile.RootTag.Get<NbtList>("servers");

                    if (serversList != null)
                    {
                        foreach (NbtCompound serverTag in serversList)
                        {
                            string name = serverTag.Get<NbtString>("name")?.Value ?? "Minecraft Server";
                            string ip = serverTag.Get<NbtString>("ip")?.Value ?? "";

                            if (!string.IsNullOrEmpty(ip))
                            {
                                uiServers.Add(new ServerListItem { Name = name, Ip = ip });
                            }
                        }
                    }
                }

                // Если у игрока еще нет добавленных серверов
                if (uiServers.Count == 0)
                {
                    uiServers.Add(new ServerListItem { Name = "Список пуст", OnlineText = "Добавьте сервер в игре", Version = "" });
                    Dispatcher.Invoke(() => ServersItemsControl.ItemsSource = uiServers);
                    return;
                }

                // Показываем список сразу, пока идет пинг (со статусом "Загрузка...")
                Dispatcher.Invoke(() => {
                    ServersItemsControl.ItemsSource = null;
                    ServersItemsControl.ItemsSource = uiServers;
                });

                // 2. Асинхронно пингуем каждый сервер
                foreach (var srv in uiServers)
                {
                    var status = await ServerStatusManager.GetStatusAsync(srv.Ip);
                    if (status.Online)
                    {
                        srv.OnlineText = $"{status.PlayersNow}/{status.PlayersMax}";
                        srv.Version = status.Version;

                        // Полоска прогресса (максимальная ширина примерно 200 пикселей)
                        double percentage = status.PlayersMax > 0 ? (double)status.PlayersNow / status.PlayersMax : 0;
                        srv.ProgressWidth = 200 * percentage;
                    }
                    else
                    {
                        srv.OnlineText = "Offline";
                        srv.Version = "Нет связи";
                        srv.ProgressWidth = 0;
                    }
                }

                // 3. Обновляем список на экране после пинга
                Dispatcher.Invoke(() => {
                    ServersItemsControl.ItemsSource = null;
                    ServersItemsControl.ItemsSource = uiServers;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка обновления серверов: " + ex.Message);
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Плавное появление интерфейса при запуске
            var loadFade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(400));
            ContentGrid.BeginAnimation(UIElement.OpacityProperty, loadFade);

            await InitializeLauncherAsync();
            await UpdateServerStatus();
            await LoadTelegramNewsAsync();

            _newsTimer = new DispatcherTimer();
            _newsTimer.Interval = TimeSpan.FromMinutes(3);
            _newsTimer.Tick += async (s, args) => await LoadTelegramNewsAsync();
            _newsTimer.Start();

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Tick += async (s, ev) => await UpdateServerStatus();
            timer.Interval = TimeSpan.FromMinutes(2);
            timer.Start();
        }

        // === АВТО-ДОБАВЛЕНИЕ СЕРВЕРОВ В SERVERS.DAT ===
        private void InjectServersIfEnabled(string gamePath)
        {
            var settings = SettingsManager.Load();
            if (!settings.AutoAddServers) return;

            try
            {
                string serversFile = Path.Combine(gamePath, "servers.dat");
                NbtFile nbtFile = new NbtFile();
                NbtList serversList;

                if (File.Exists(serversFile))
                {
                    nbtFile.LoadFromFile(serversFile);
                    serversList = nbtFile.RootTag.Get<NbtList>("servers") ?? new NbtList("servers", NbtTagType.Compound);
                    if (nbtFile.RootTag.Get("servers") == null) nbtFile.RootTag.Add(serversList);
                }
                else
                {
                    nbtFile.RootTag = new NbtCompound("");
                    serversList = new NbtList("servers", NbtTagType.Compound);
                    nbtFile.RootTag.Add(serversList);
                }

                string targetIp = "play.scraft.ru";
                string targetName = "SCRAFT Server";

                bool exists = false;
                foreach (NbtCompound server in serversList)
                {
                    if (server.Get<NbtString>("ip")?.Value == targetIp)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    var newServer = new NbtCompound();
                    newServer.Add(new NbtString("ip", targetIp));
                    newServer.Add(new NbtString("name", targetName));
                    newServer.Add(new NbtByte("acceptTextures", 1));
                    serversList.Add(newServer);

                    nbtFile.SaveToFile(serversFile, NbtCompression.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Не удалось добавить сервер: " + ex.Message);
            }
        }

        private async void CheckForQuickPlay()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();

                // Проверяем все переданные аргументы
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i] == "-quickplay" && i + 2 < args.Length)
                    {
                        string targetVersion = args[i + 1];
                        string serverIp = args[i + 2];

                        // Подстраховка: если лаунчер еще не успел выбрать аккаунт визуально,
                        // принудительно выбираем первый из списка, чтобы игра не отменила запуск
                        if (NicknameComboBox.SelectedItem == null && NicknameComboBox.Items.Count > 0)
                        {
                            var settings = SettingsManager.Load();
                            NicknameComboBox.SelectedItem = settings.Accounts.Find(a => a.Nickname == settings.ActiveAccount)
                                                          ?? settings.Accounts[0];
                        }

                        // Устанавливаем версию в списке
                        VersionComboBox.SelectedItem = targetVersion;

                        // Вызываем запуск
                        await StartGame(targetVersion, serverIp);
                        return; // Выходим из цикла
                    }
                }
            }
            catch (Exception ex)
            {
                // Если что-то сломалось, лаунчер больше не будет молчать!
                QMessageBoxWindow.Show($"Сбой при попытке автозапуска:\n{ex.Message}", "Ошибка QuickPlay", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task InitializeLauncherAsync()
        {
            var settings = SettingsManager.Load();

            NicknameComboBox.ItemsSource = settings.Accounts;
            NicknameComboBox.SelectedItem = settings.Accounts.Find(a => a.Nickname == settings.ActiveAccount);

            _minecraftPath = new MinecraftPath(settings.GamePath);
            _launcher = new CmlLib.Core.MinecraftLauncher(_minecraftPath);

            _launcher.FileProgressChanged += (s, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (args.TotalTasks > 0) ProgressTextBlock.Text = $"Подготовка ({args.ProgressedTasks}/{args.TotalTasks})";
                });
            };

            _launcher.ByteProgressChanged += (s, args) =>
            {
                var now = DateTime.Now;
                var timeDiff = (now - _lastTime).TotalSeconds;

                if (timeDiff >= 0.5 && args.ProgressedBytes >= _lastBytes)
                {
                    double bytesPerSec = (args.ProgressedBytes - _lastBytes) / timeDiff;
                    double speedMb = bytesPerSec / 1048576.0;
                    _currentSpeedStr = speedMb >= 1 ? $"{speedMb:F1} МБ/с" : $"{(bytesPerSec / 1024.0):F0} КБ/с";
                    _lastBytes = args.ProgressedBytes;
                    _lastTime = now;
                }

                Dispatcher.Invoke(() =>
                {
                    if (args.TotalBytes > 0)
                    {
                        double percent = (double)args.ProgressedBytes / args.TotalBytes;
                        int percentInt = (int)(percent * 100);

                        if (ProgressFillBorder.Parent is Grid parentGrid)
                            ProgressFillBorder.Width = percent * parentGrid.ActualWidth;

                        double progMb = args.ProgressedBytes / 1048576.0;
                        double totMb = args.TotalBytes / 1048576.0;
                        string progStr = progMb >= 1024 ? $"{(progMb / 1024.0):F1} ГБ" : $"{progMb:F1} МБ";
                        string totStr = totMb >= 1024 ? $"{(totMb / 1024.0):F1} ГБ" : $"{totMb:F1} МБ";

                        ProgressTextBlock.Text = $"{progStr} / {totStr} ({percentInt}%) • {_currentSpeedStr}";
                    }
                });
            };

            await LoadVersionsAsync();
            CheckForQuickPlay();
        }

        private async Task<string> GetOrInstallJavaAsync(string gameVersion)
        {
            var settings = SettingsManager.Load();

            if (!string.IsNullOrWhiteSpace(settings.JavaPath) && File.Exists(settings.JavaPath))
                return settings.JavaPath;

            int javaVersion = 8;
            var match = Regex.Match(gameVersion, @"1\.(\d+)(?:\.(\d+))?");
            if (match.Success)
            {
                int minor = int.Parse(match.Groups[1].Value);
                int patch = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;

                if (minor >= 21 || (minor == 20 && patch >= 5)) javaVersion = 21;
                else if (minor >= 17) javaVersion = 17;
            }
            else if (Regex.IsMatch(gameVersion, @"2[3-9]w\d+[a-z]")) javaVersion = 21;

            string runtimesFolder = Path.Combine(settings.GamePath, "runtimes");
            string javaFolder = Path.Combine(runtimesFolder, $"jre{javaVersion}");

            if (Directory.Exists(javaFolder))
            {
                var files = Directory.GetFiles(javaFolder, "javaw.exe", SearchOption.AllDirectories);
                if (files.Length > 0) return files[0];
            }

            Directory.CreateDirectory(javaFolder);
            string zipPath = Path.Combine(runtimesFolder, $"java{javaVersion}.zip");
            string downloadUrl = $"https://api.adoptium.net/v3/binary/latest/{javaVersion}/ga/windows/x64/jre/hotspot/normal/eclipse";

            Dispatcher.Invoke(() => ProgressTextBlock.Text = $"Скачивание Java {javaVersion}...");

            var response = await SharedHttpClient.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            {
                await response.Content.CopyToAsync(fs);
            }

            Dispatcher.Invoke(() => ProgressTextBlock.Text = $"Распаковка Java {javaVersion}...");
            ZipFile.ExtractToDirectory(zipPath, javaFolder, true);
            File.Delete(zipPath);

            var newFiles = Directory.GetFiles(javaFolder, "javaw.exe", SearchOption.AllDirectories);
            if (newFiles.Length > 0) return newFiles[0];

            throw new Exception($"Не удалось найти java.exe после установки Java {javaVersion}!");
        }

        private async Task LoadTelegramNewsAsync()
        {
            try
            {
                string url = $"https://t.me/s/QLauncher_MC";

                string html = await SharedHttpClient.GetStringAsync(url);
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                var posts = new List<TelegramPost>();
                var messageNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'tgme_widget_message_wrap')]");

                if (messageNodes != null)
                {
                    foreach (var node in messageNodes)
                    {
                        var textNode = node.SelectSingleNode(".//div[contains(@class, 'tgme_widget_message_text')]");
                        var dateNode = node.SelectSingleNode(".//time");
                        var photoWrap = node.SelectSingleNode(".//a[contains(@class, 'tgme_widget_message_photo_wrap')]");

                        if (textNode != null || photoWrap != null)
                        {
                            string cleanText = "";
                            if (textNode != null)
                            {
                                string rawText = textNode.InnerHtml.Replace("<br>", "\n").Replace("<br/>", "\n");
                                cleanText = HttpUtility.HtmlDecode(rawText);
                                HtmlDocument textDoc = new HtmlDocument();
                                textDoc.LoadHtml(cleanText);
                                cleanText = textDoc.DocumentNode.InnerText.Trim();
                            }

                            string imageUrl = "";
                            if (photoWrap != null)
                            {
                                string style = photoWrap.GetAttributeValue("style", "");
                                var match = Regex.Match(style, @"url\('(.*?)'\)");
                                if (match.Success) imageUrl = match.Groups[1].Value;
                            }

                            posts.Add(new TelegramPost { Text = cleanText, Date = dateNode != null ? dateNode.InnerText : "Недавно", ImageUrl = imageUrl });
                        }
                    }
                }
                posts.Reverse();
                Dispatcher.Invoke(() => { NewsItemsControl.ItemsSource = posts; });

            }
            catch (Exception)
            {
                Dispatcher.Invoke(() => { NewsItemsControl.ItemsSource = new List<TelegramPost> { new TelegramPost { Date = "Ошибка", Text = "Не удалось загрузить новости." } }; });
            }
        }

        
        
        private async Task LoadVersionsAsync()
        {
            ProgressTextBlock.Text = "Получение списка версий...";
            try
            {
                VersionComboBox.Items.Clear();
                var settings = SettingsManager.Load();

                foreach (var pack in settings.Modpacks) VersionComboBox.Items.Add($"⭐ {pack.Name} ({pack.Loader})");

                var versions = await _launcher.GetAllVersionsAsync();
                foreach (var version in versions) if (version.Type == "release") VersionComboBox.Items.Add(version.Name);

                // ... тут выше код, где версии добавляются в VersionComboBox.Items ...

                if (VersionComboBox.Items.Count > 0)
                {

                    // Если в настройках есть сохраненная версия И она есть в текущем списке
                    if (!string.IsNullOrEmpty(SettingsManager.Load().LastSelectedVersion) && VersionComboBox.Items.Contains(SettingsManager.Load().LastSelectedVersion))
                    {
                        VersionComboBox.SelectedItem = SettingsManager.Load().LastSelectedVersion; // Выбираем её!
                    }
                    else
                    {
                        VersionComboBox.SelectedIndex = 0; // Иначе просто выбираем самую первую
                    }
                    ProgressTextBlock.Text = "Готов к запуску";
                }
            }
            catch (Exception) { }
        }

        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Проверяем, что список уже загружен и выбор не пустой
            if (VersionComboBox.SelectedItem != null)
            {
                var settings = SettingsManager.Load();
                settings.LastSelectedVersion = VersionComboBox.SelectedItem.ToString();
                SettingsManager.Save(settings);
            }
        }

        private void NicknameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Теперь в списке лежат объекты AccountProfile, а не просто текст
            if (NicknameComboBox.SelectedItem is AccountProfile selectedAcc)
            {
                var settings = SettingsManager.Load();
                settings.ActiveAccount = selectedAcc.Nickname; // Сохраняем ник
                SettingsManager.Save(settings);
            }
        }

        // Открытие и закрытие оверлея с плавной анимацией
        private async void OpenShortcutOverlay_Click(object sender, RoutedEventArgs e)
        {
            ShortcutVersionBox.ItemsSource = VersionComboBox.Items;
            if (VersionComboBox.SelectedItem != null) ShortcutVersionBox.SelectedItem = VersionComboBox.SelectedItem;

            await FadeInElement(ShortcutOverlay, 200);
        }

        private async void CloseShortcutOverlay_Click(object sender, RoutedEventArgs e)
        {
            await FadeOutElement(ShortcutOverlay, 200);
        }

        // Кнопка СОЗДАТЬ ярлык
        private void CreateShortcutBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ShortcutVersionBox.SelectedItem == null || string.IsNullOrWhiteSpace(ShortcutIpBox.Text))
            {
                QMessageBoxWindow.Show("Заполните все поля!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string version = ShortcutVersionBox.SelectedItem.ToString();
            string ip = ShortcutIpBox.Text.Trim();

            try
            {
                // Заменяем двоеточие на нижнее подчеркивание, чтобы Windows не ругалась на имя файла
                string safeIpForFileName = ip.Replace(":", "_");

                // Получаем путь к рабочему столу и нашему лаунчеру
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, $"Играть на {safeIpForFileName}.lnk");
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

                // Создаем системный COM-объект Windows для ярлыков
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(t);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);

                // Настраиваем ярлык
                shortcut.TargetPath = exePath;

                // ВАЖНО: В аргументы мы передаем оригинальный IP с двоеточием, лаунчер его поймет!
                shortcut.Arguments = $"-quickplay \"{version}\" \"{ip}\"";

                shortcut.IconLocation = exePath + ",0"; // Берем иконку от нашего лаунчера
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                shortcut.Save();

                CloseShortcutOverlay_Click(null, null);
                QMessageBoxWindow.Show($"Ярлык успешно создан на рабочем столе!\nОн автоматически запустит {version} и зайдет на {ip}.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                QMessageBoxWindow.Show($"Ошибка создания ярлыка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (VersionComboBox.SelectedItem == null)
            {
                QMessageBoxWindow.Show("Пожалуйста, выберите версию игры!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string launchVersion = VersionComboBox.SelectedItem.ToString() ?? "";

            // Запускаем игру!
            await StartGame(launchVersion, null);
        }

        private async Task StartGame(string launchVersion, string autoJoinServerIp = null)
        {
            var settings = SettingsManager.Load();
            var selectedAcc = settings.Accounts.Find(a => a.Nickname == settings.ActiveAccount) ??
                             (settings.Accounts.Count > 0 ? settings.Accounts[0] : null);

            if (selectedAcc == null) { QMessageBoxWindow.Show("Выберите аккаунт!"); return; }

            string packName = launchVersion.StartsWith("⭐") ? launchVersion.Replace("⭐", "").Trim() : "vanilla";
            if (packName.Contains(" (")) packName = packName.Substring(0, packName.LastIndexOf(" (")).Trim();

            // 1. ИЗОЛЯЦИЯ МОДОВ (Подменяем папку перед запуском)
            string sourceMods = Path.Combine(_minecraftPath.BasePath, "instances", packName, "mods");
            string destMods = Path.Combine(_minecraftPath.BasePath, "mods");

            try
            {
                if (Directory.Exists(destMods)) Directory.Delete(destMods, true);
                Directory.CreateDirectory(destMods);
                if (Directory.Exists(sourceMods))
                {
                    foreach (var file in Directory.GetFiles(sourceMods))
                    {
                        File.Copy(file, Path.Combine(destMods, Path.GetFileName(file)));
                    }
                }
            }
            catch { /* Игнорируем ошибки доступа, если моды не скопировались */ }

            // 2. Установка Fabric
            string realVersionName = launchVersion;
            if (launchVersion.StartsWith("⭐"))
            {
                var pack = settings.Modpacks.Find(p => p.Name == packName);
                if (pack != null && pack.Loader == "Fabric")
                {
                    ActionOverlayText.Text = "Установка Fabric...";
                    await FadeInElement(ActionOverlay, 300);
                    try
                    {
                        var installer = new FabricInstaller(SharedHttpClient);
                        await installer.Install(pack.Version ?? pack.GameVersion, new CmlLib.Core.MinecraftPath(_minecraftPath.BasePath));
                        await _launcher.GetAllVersionsAsync();
                        var dirInfo = new DirectoryInfo(Path.Combine(_minecraftPath.BasePath, "versions"));
                        var foundDir = dirInfo.GetDirectories("fabric-loader-*").OrderByDescending(d => d.CreationTime).FirstOrDefault();
                        if (foundDir != null) realVersionName = foundDir.Name;
                    }
                    catch (Exception ex) { QMessageBoxWindow.Show("Ошибка Fabric: " + ex.Message); await FadeOutElement(ActionOverlay, 300); return; }
                }
            }

            // 3. ЗАПУСК
            ActionOverlayText.Text = "Запуск...";
            await FadeInElement(ActionOverlay, 300);
            try
            {
                var launchOption = new MLaunchOption
                {
                    MaximumRamMb = settings.RamMb,
                    Session = selectedAcc.AccessToken != "offline"
                        ? new MSession(selectedAcc.Nickname, selectedAcc.AccessToken, selectedAcc.Uuid)
                        : MSession.CreateOfflineSession(selectedAcc.Nickname)
                };

                var process = await _launcher.CreateProcessAsync(realVersionName, launchOption);
                process.Start();
                this.Hide();
            }
            catch (Exception ex) { QMessageBoxWindow.Show($"Ошибка: {ex.Message}"); }
            finally { await FadeOutElement(ActionOverlay, 300); }
        }

        private async Task DownloadModrinthModAsync(string slug, string gameVersion, string modsPath)
        {
            try
            {
                SharedHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("QLauncher_By_dyagnostic");
                string url = $"https://api.modrinth.com/v2/project/{slug}/version?game_versions=[\"{gameVersion}\"]&loaders=[\"fabric\"]";

                string json = await SharedHttpClient.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetArrayLength() > 0)
                {
                    var firstVersion = root[0];
                    var files = firstVersion.GetProperty("files");
                    if (files.GetArrayLength() > 0)
                    {
                        string downloadUrl = files[0].GetProperty("url").GetString() ?? "";
                        string fileName = files[0].GetProperty("filename").GetString() ?? "";
                        string filePath = Path.Combine(modsPath, fileName);

                        byte[] fileBytes = await SharedHttpClient.GetByteArrayAsync(downloadUrl);
                        File.WriteAllBytes(filePath, fileBytes);
                    }
                }
            }
            catch (Exception) { }
        }

        private async void CreateModpackBtn_Click(object sender, RoutedEventArgs e)
        {
            string packName = ModpackNameBox.Text.Trim();
            string gameVersion = ModpackVersionBox.SelectedItem?.ToString() ?? "";
            string loader = ((ComboBoxItem)ModLoaderBox.SelectedItem)?.Content.ToString() ?? "Vanilla";

            bool sodium = InstallSodiumCheck.IsChecked ?? false;
            bool iris = InstallIrisCheck.IsChecked ?? false;

            if (string.IsNullOrWhiteSpace(packName) || string.IsNullOrWhiteSpace(gameVersion))
            {
                QMessageBoxWindow.Show("Введите название сборки и выберите версию!", "Внимание");
                return;
            }

            var settings = SettingsManager.Load();
            if (settings.Modpacks.Exists(m => m.Name == packName))
            {
                QMessageBoxWindow.Show("Сборка с таким именем уже существует!", "Ошибка");
                return;
            }

            string instancesPath = Path.Combine(settings.GamePath, "instances", packName);
            Directory.CreateDirectory(instancesPath);

            if (loader == "Fabric")
            {
                string modsPath = Path.Combine(instancesPath, "mods");
                Directory.CreateDirectory(modsPath);

                if (sodium) await DownloadModrinthModAsync("sodium", gameVersion, modsPath);
                if (iris) await DownloadModrinthModAsync("iris", gameVersion, modsPath);
            }

            settings.Modpacks.Add(new ModpackProfile { Name = packName, GameVersion = gameVersion, Loader = loader, FolderPath = instancesPath });
            SettingsManager.Save(settings);
            ReloadLauncher();

            MessageBox.Show($"Сборка '{packName}' успешно создана!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            await FadeOutElement(ModpackOverlay, 200);
            ModpackOverlay.Visibility = Visibility.Collapsed;
        }

        private void DeleteModpackBtn_Click(object sender, RoutedEventArgs e)
        {
            if (VersionComboBox.SelectedItem == null) return;
            string selectedItem = VersionComboBox.SelectedItem.ToString() ?? "";

            if (!selectedItem.StartsWith("⭐ "))
            {
                QMessageBoxWindow.Show("Вы можете удалить только созданные вами сборки!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string packName = selectedItem.Substring(2, selectedItem.LastIndexOf('(') - 3);

            var result = QMessageBoxWindow.Show($"Вы действительно хотите удалить сборку '{packName}'?\nВсе сохранения, моды и настройки внутри этой сборки будут безвозвратно удалены!",
                                         "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var settings = SettingsManager.Load();
                var modpack = settings.Modpacks.Find(p => p.Name == packName);

                if (modpack != null)
                {
                    try
                    {
                        if (Directory.Exists(modpack.FolderPath))
                        {
                            Directory.Delete(modpack.FolderPath, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        QMessageBoxWindow.Show($"Не удалось удалить файлы с диска (возможно, они заняты другой программой): {ex.Message}", "Ошибка");
                    }

                    settings.Modpacks.Remove(modpack);
                    SettingsManager.Save(settings);
                    ReloadLauncher();

                    QMessageBoxWindow.Show("Сборка успешно удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private Task FadeOutElement(UIElement element, double durationMs = 200)
        {
            var tcs = new TaskCompletionSource<bool>();
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(durationMs));
            anim.Completed += (s, ev) => tcs.SetResult(true);
            element.BeginAnimation(UIElement.OpacityProperty, anim);
            return tcs.Task;
        }

        private Task FadeInElement(UIElement element, double durationMs = 200)
        {
            var tcs = new TaskCompletionSource<bool>();
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs));
            anim.Completed += (s, ev) => tcs.SetResult(true);
            element.BeginAnimation(UIElement.OpacityProperty, anim);
            return tcs.Task;
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            AnimateNavigate(new SettingsPage());
        }

        // Метод для обновления списков и интерфейса
        public void ReloadLauncher()
        {
            _ = InitializeLauncherAsync();
        }

        // Метод для закрытия настроек и возврата на главную
        public async void CloseSettings()
        {
            await FadeOutElement(MainFrame, 200);
            MainFrame.Visibility = Visibility.Collapsed;
            MainFrame.Content = null;

            ReloadLauncher(); // Обновляем списки (ники, сборки)

            HomeView.Opacity = 0;
            HomeView.Visibility = Visibility.Visible;
            await FadeInElement(HomeView, 200);
        }

        private async void OpenModpackOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn) btn.IsEnabled = false;
            ModpackVersionBox.Items.Clear();
            foreach (var item in VersionComboBox.Items) if (!item.ToString()!.StartsWith("⭐")) ModpackVersionBox.Items.Add(item);
            if (ModpackVersionBox.Items.Count > 0) ModpackVersionBox.SelectedIndex = 0;
            ModpackOverlay.Opacity = 0;
            ModpackOverlay.Visibility = Visibility.Visible;
            await FadeInElement(ModpackOverlay, 200);
            if (sender is Button btnReEn) btnReEn.IsEnabled = true;
        }

        private async void CloseModpackOverlay_Click(object sender, RoutedEventArgs e)
        {
            await FadeOutElement(ModpackOverlay, 200);
            ModpackOverlay.Visibility = Visibility.Collapsed;
        }

        private void ModLoaderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FabricOptionsPanel == null) return;
            if (ModLoaderBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content.ToString() == "Fabric") FabricOptionsPanel.Visibility = Visibility.Visible;
            else FabricOptionsPanel.Visibility = Visibility.Collapsed;
        }

        private async void ModsButton_Click(object sender, RoutedEventArgs e)
        {
            AnimateNavigate(new ModsPage());
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2) MaximizeButton_Click(sender, e);
                else
                {
                    if (_isCustomMaximized)
                    {
                        ClearAnimations();
                        Point clickPos = e.GetPosition(this);
                        double ratio = clickPos.X / this.ActualWidth;
                        this.Width = _normalWidth; this.Height = _normalHeight;
                        this.Left = this.Left + clickPos.X - (_normalWidth * ratio);
                        this.Top = this.Top + clickPos.Y - clickPos.Y;
                        if (OuterBorder != null) OuterBorder.CornerRadius = new CornerRadius(14);
                        if (TitleBorder != null) TitleBorder.CornerRadius = new CornerRadius(10, 10, 0, 0);
                        if (CloseBtn != null) CloseBtn.Tag = new CornerRadius(0, 10, 0, 0);
                        _isCustomMaximized = false;
                    }
                    DragMove();
                }
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Отключаем Discord, чтобы убрать статус "Играет" перед закрытием лаунчера
            DiscordManager.StopRpc();

            // Полностью закрываем приложение
            Application.Current.Shutdown();
        }

        private void ClearAnimations()
        {
            this.BeginAnimation(Window.LeftProperty, null);
            this.BeginAnimation(Window.TopProperty, null);
            this.BeginAnimation(Window.WidthProperty, null);
            this.BeginAnimation(Window.HeightProperty, null);
            OuterBorder?.BeginAnimation(Border.CornerRadiusProperty, null);
            TitleBorder?.BeginAnimation(Border.CornerRadiusProperty, null);
        }

        private void AnimateCorners(Border border, CornerRadius to, TimeSpan duration, IEasingFunction ease)
        {
            CornerRadiusAnimation anim = new CornerRadiusAnimation { From = border.CornerRadius, To = to, Duration = duration, EasingFunction = ease };
            border.BeginAnimation(Border.CornerRadiusProperty, anim);
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            TimeSpan duration = TimeSpan.FromMilliseconds(250);
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

            if (!_isCustomMaximized)
            {
                _normalLeft = this.Left; _normalTop = this.Top; _normalWidth = this.Width; _normalHeight = this.Height;
                this.BeginAnimation(Window.LeftProperty, new DoubleAnimation(SystemParameters.WorkArea.Left, duration) { EasingFunction = ease });
                this.BeginAnimation(Window.TopProperty, new DoubleAnimation(SystemParameters.WorkArea.Top, duration) { EasingFunction = ease });
                this.BeginAnimation(Window.WidthProperty, new DoubleAnimation(SystemParameters.WorkArea.Width, duration) { EasingFunction = ease });
                this.BeginAnimation(Window.HeightProperty, new DoubleAnimation(SystemParameters.WorkArea.Height, duration) { EasingFunction = ease });
                if (OuterBorder != null) AnimateCorners(OuterBorder, new CornerRadius(0), duration, ease);
                if (TitleBorder != null) AnimateCorners(TitleBorder, new CornerRadius(0), duration, ease);
                if (CloseBtn != null) CloseBtn.Tag = new CornerRadius(0);
                _isCustomMaximized = true;
            }
            else
            {
                this.BeginAnimation(Window.LeftProperty, new DoubleAnimation(_normalLeft, duration) { EasingFunction = ease });
                this.BeginAnimation(Window.TopProperty, new DoubleAnimation(_normalTop, duration) { EasingFunction = ease });
                this.BeginAnimation(Window.WidthProperty, new DoubleAnimation(_normalWidth, duration) { EasingFunction = ease });
                this.BeginAnimation(Window.HeightProperty, new DoubleAnimation(_normalHeight, duration) { EasingFunction = ease });
                if (OuterBorder != null) AnimateCorners(OuterBorder, new CornerRadius(14), duration, ease);
                if (TitleBorder != null) AnimateCorners(TitleBorder, new CornerRadius(10, 10, 0, 0), duration, ease);
                if (CloseBtn != null) CloseBtn.Tag = new CornerRadius(0, 10, 0, 0);
                _isCustomMaximized = false;
            }
        }

        // === ЛОГИКА ТРЕЯ ===
        private void LauncherTrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            RestoreLauncher();
        }

        private void TrayRestore_Click(object sender, RoutedEventArgs e)
        {
            RestoreLauncher();
        }

        private void TrayExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void RestoreLauncher()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();

            LauncherTrayIcon.Visibility = Visibility.Collapsed;
        }

        // Универсальный метод плавного ПОЯВЛЕНИЯ
        public async Task FadeInElement(UIElement element, int durationMs = 300)
        {
            element.Opacity = 0; // Делаем полностью прозрачным
            element.Visibility = Visibility.Visible; // Включаем отображение

            DoubleAnimation anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs));
            element.BeginAnimation(UIElement.OpacityProperty, anim);

            await Task.Delay(durationMs); // Ждем, пока анимация не закончится
        }

        // Универсальный метод плавного ЗАТУХАНИЯ
        public async Task FadeOutElement(UIElement element, int durationMs = 300)
        {
            DoubleAnimation anim = new DoubleAnimation(element.Opacity, 0, TimeSpan.FromMilliseconds(durationMs));
            element.BeginAnimation(UIElement.OpacityProperty, anim);

            await Task.Delay(durationMs); // Ждем окончания анимации
            element.Visibility = Visibility.Collapsed; // Полностью выключаем элемент, чтобы он не перекрывал клики
        }
    }
}