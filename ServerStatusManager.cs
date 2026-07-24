using System;
using System.Diagnostics;
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
        public string Version { get; set; } = "";
        public string Motd { get; set; } = "";
        public long PingMs { get; set; } = -1;
    }

    public static class ServerStatusManager
    {
        public static async Task<ServerStatus> GetStatusAsync(string ip)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                string url = $"https://api.mcstatus.io/v2/status/java/{ip}";

                string response = await client.GetStringAsync(url);
                sw.Stop();

                using JsonDocument doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (!root.GetProperty("online").GetBoolean())
                    return new ServerStatus { Online = false };

                return new ServerStatus
                {
                    Online = true,
                    PlayersNow = root.GetProperty("players").GetProperty("online").GetInt32(),
                    PlayersMax = root.GetProperty("players").GetProperty("max").GetInt32(),
                    Version = root.GetProperty("version").GetProperty("name_clean").GetString() ?? "",
                    Motd = root.GetProperty("motd").GetProperty("clean").GetString() ?? "",
                    PingMs = sw.ElapsedMilliseconds
                };
            }
            catch { return new ServerStatus { Online = false }; }
        }
    }
}