using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MinecraftLauncher
{
    public class ServerStatus
    {
        public bool Online { get; set; }
        public int PlayersNow { get; set; }
        public int PlayersMax { get; set; }
        public string Version { get; set; }
        public string Motd { get; set; }
    }

    public static class ServerStatusManager
    {
        // Используем бесплатное и надежное API для получения статуса
        public static async Task<ServerStatus> GetStatusAsync(string ip)
        {
            try
            {
                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                string url = $"https://api.mcstatus.io/v2/status/java/{ip}";

                string response = await client.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (!root.GetProperty("online").GetBoolean())
                    return new ServerStatus { Online = false };

                return new ServerStatus
                {
                    Online = true,
                    PlayersNow = root.GetProperty("players").GetProperty("online").GetInt32(),
                    PlayersMax = root.GetProperty("players").GetProperty("max").GetInt32(),
                    Version = root.GetProperty("version").GetProperty("name_clean").GetString(),
                    Motd = root.GetProperty("motd").GetProperty("clean").GetString()
                };
            }
            catch { return new ServerStatus { Online = false }; }
        }
    }
}