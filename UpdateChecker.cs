using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MinecraftLauncher
{
    public class ReleaseInfo
    {
        public string TagName { get; set; } = "";
        public string HtmlUrl { get; set; } = "";
        public string Body { get; set; } = "";
        public bool HasUpdate { get; set; }
    }

    public static class UpdateChecker
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        public const string CurrentVersion = "v1.1.0";

        static UpdateChecker()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("QLauncher_UpdateChecker");
        }

        public static async Task<ReleaseInfo?> CheckForUpdatesAsync()
        {
            try
            {
                string url = "https://api.github.com/repos/dyagyatis/QLauncher/releases/latest";
                string json = await HttpClient.GetStringAsync(url);

                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.GetProperty("tag_name").GetString() ?? "";
                string htmlUrl = root.GetProperty("html_url").GetString() ?? "";
                string body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

                bool hasUpdate = !string.Equals(tagName.Trim(), CurrentVersion, StringComparison.OrdinalIgnoreCase);

                return new ReleaseInfo
                {
                    TagName = tagName,
                    HtmlUrl = htmlUrl,
                    Body = body,
                    HasUpdate = hasUpdate
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
