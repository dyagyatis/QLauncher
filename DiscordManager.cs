using DiscordRPC;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftLauncher
{
    public static class DiscordManager
    {
        private static DiscordRpcClient _client;
        private static CancellationTokenSource _cts;

        // ВАЖНО: ЗАМЕНИ НА СВОЙ ID ПРИЛОЖЕНИЯ ИЗ DISCORD DEVELOPER PORTAL
        private const string ApplicationId = "1144124876001644564";

        public static void StartRpc()
        {
            var settings = SettingsManager.Load();
            if (!settings.EnableDiscordRpc) return;

            if (_client == null || _client.IsDisposed)
            {
                _client = new DiscordRpcClient(ApplicationId);
                _client.Initialize();
            }

            SetMenuState();
        }

        public static void StopRpc()
        {
            _cts?.Cancel();
            if (_client != null && !_client.IsDisposed)
            {
                _client.Dispose();
                _client = null;
            }
        }

        public static void SetMenuState()
        {
            if (_client == null || _client.IsDisposed) return;

            _client.SetPresence(new RichPresence()
            {
                Details = "Выбирает сборку",
                State = "В лаунчере",
                Assets = new Assets()
                {
                    LargeImageKey = "logo", // Имя картинки, загруженной в Discord Portal
                    LargeImageText = "QLauncher"
                }
            });
        }

        // Этот метод вызываем, когда нажимаем "Играть"
        public static void StartGameTracking(string version, string gamePath)
        {
            if (_client == null || _client.IsDisposed) return;

            var settings = SettingsManager.Load();
            string logFile = Path.Combine(gamePath, "logs", "latest.log");

            // Базовый статус при запуске
            _client.SetPresence(new RichPresence()
            {
                Details = $"Играет в {version}",
                State = "В главном меню",
                Timestamps = Timestamps.Now,
                Assets = new Assets() { LargeImageKey = "logo", LargeImageText = "QLauncher" }
            });

            // Запускаем чтение логов в фоне
            _cts = new CancellationTokenSource();
            Task.Run(() => WatchLogFile(logFile, version, settings.HideServerIp, _cts.Token));
        }

        private static async Task WatchLogFile(string logFile, string version, bool hideIp, CancellationToken token)
        {
            // Даем Майнкрафту 3 секунды, чтобы он успел стереть старый лог и создать новый
            await Task.Delay(3000, token);

            while (!File.Exists(logFile))
            {
                if (token.IsCancellationRequested) return;
                await Task.Delay(1000, token);
            }

            try
            {
                // Разрешаем удаление/перезапись файла самой игрой (FileShare.Delete)
                using (var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(fs, Encoding.UTF8))
                {
                    long lastPosition = fs.Length;
                    fs.Seek(lastPosition, SeekOrigin.Begin);

                    while (!token.IsCancellationRequested)
                    {
                        // МАГИЯ ЗДЕСЬ: Если файл стал меньше, значит игра его очистила!
                        // Возвращаемся в самое начало файла.
                        if (fs.Length < lastPosition)
                        {
                            fs.Seek(0, SeekOrigin.Begin);
                            lastPosition = 0;
                            reader.DiscardBufferedData(); // Сбрасываем кэш чтеца
                        }

                        string line = await reader.ReadLineAsync();

                        if (line == null)
                        {
                            lastPosition = fs.Position;
                            await Task.Delay(500, token); // Ждем новые строчки от игры
                            continue;
                        }

                        lastPosition = fs.Position;

                        // ==========================================
                        // АНАЛИЗАТОР СОБЫТИЙ
                        // ==========================================

                        if (line.Contains("Connecting to "))
                        {
                            string serverDetails = ""; // По умолчанию пусто, если скрываем
                            if (!hideIp)
                            {
                                try
                                {
                                    int idx = line.IndexOf("Connecting to ") + 14;
                                    serverDetails = line.Substring(idx).Split(',')[0].Trim();
                                }
                                catch { }
                            }
                            // Передаем IP только если скрытие выключено
                            UpdateGameState(version, "В мультиплеере", serverDetails);
                        }
                        else if (line.Contains("Starting integrated minecraft server") || line.Contains("Saving chunks for level 'ServerLevel'"))
                        {
                            UpdateGameState(version, "В одиночной игре", "Локальный мир");
                        }
                        else if (line.Contains("Stopping server") || line.Contains("Disconnecting from") || line.Contains("Disconnected"))
                        {
                            UpdateGameState(version, "В главном меню", "");
                        }
                    }
                }
            }
            catch
            {
                // Если файл заблокирован, просто тихо выходим, чтобы не крашить лаунчер
            }
        }
        private static void UpdateGameState(string version, string state, string details)
        {
            if (_client == null || _client.IsDisposed) return;

            string finalDetails = $"Играет в {version}";

            // Добавляем скобки с IP только если там есть какой-то текст!
            if (!string.IsNullOrEmpty(details))
            {
                finalDetails += $" ({details})";
            }

            _client.SetPresence(new RichPresence()
            {
                Details = finalDetails,
                State = state,
                Timestamps = Timestamps.Now,
                Assets = new Assets() { LargeImageKey = "logo", LargeImageText = "QLauncher" }
            });
        }
    }
}