using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MinecraftLauncher.Helpers
{
    public static class AvatarHelper
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly ConcurrentDictionary<string, BitmapImage> Cache = new ConcurrentDictionary<string, BitmapImage>();
        private static readonly string CacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "Avatars");

        static AvatarHelper()
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("QLauncher/2.0");
            }
            catch { }
        }

        public static async Task<BitmapImage?> GetAvatarAsync(string nickname, string uuid = "")
        {
            if (string.IsNullOrWhiteSpace(nickname)) return GetDefaultAvatar();

            string key = nickname.ToLowerInvariant();
            if (Cache.TryGetValue(key, out var cached)) return cached;

            string localPath = Path.Combine(CacheDir, $"{key}.png");
            if (File.Exists(localPath))
            {
                try
                {
                    var bitmap = LoadBitmapFromFile(localPath);
                    if (bitmap != null)
                    {
                        Cache[key] = bitmap;
                        return bitmap;
                    }
                }
                catch { }
            }

            try
            {
                string target = string.IsNullOrWhiteSpace(uuid) || uuid == "offline" ? nickname : uuid;
                string url = $"https://minotar.net/helm/{Uri.EscapeDataString(target)}/32.png";

                byte[] bytes = await HttpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(localPath, bytes);

                var bitmap = LoadBitmapFromFile(localPath);
                if (bitmap != null)
                {
                    Cache[key] = bitmap;
                    return bitmap;
                }
            }
            catch { }

            try
            {
                string elyUrl = $"https://ely.by/services/skins-system/skins/{Uri.EscapeDataString(nickname)}.png";
                byte[] bytes = await HttpClient.GetByteArrayAsync(elyUrl);
                await File.WriteAllBytesAsync(localPath, bytes);

                var bitmap = LoadBitmapFromFile(localPath);
                if (bitmap != null)
                {
                    Cache[key] = bitmap;
                    return bitmap;
                }
            }
            catch { }

            return GetDefaultAvatar();
        }

        public static async Task<BitmapImage?> GetBodyPreviewAsync(string nickname, string uuid = "")
        {
            if (string.IsNullOrWhiteSpace(nickname)) return GetDefaultAvatar();

            string key = $"body_{nickname.ToLowerInvariant()}";
            if (Cache.TryGetValue(key, out var cached)) return cached;

            string localPath = Path.Combine(CacheDir, $"{key}.png");
            if (File.Exists(localPath))
            {
                try
                {
                    var bitmap = LoadBitmapFromFile(localPath);
                    if (bitmap != null)
                    {
                        Cache[key] = bitmap;
                        return bitmap;
                    }
                }
                catch { }
            }

            try
            {
                string target = string.IsNullOrWhiteSpace(uuid) || uuid == "offline" ? nickname : uuid;
                string url = $"https://minotar.net/armor/body/{Uri.EscapeDataString(target)}/120.png";

                byte[] bytes = await HttpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(localPath, bytes);

                var bitmap = LoadBitmapFromFile(localPath);
                if (bitmap != null)
                {
                    Cache[key] = bitmap;
                    return bitmap;
                }
            }
            catch { }

            return GetDefaultAvatar();
        }

        private static BitmapImage? LoadBitmapFromFile(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public static BitmapImage GetDefaultAvatar()
        {
            string key = "__default_steve__";
            if (Cache.TryGetValue(key, out var cached)) return cached;

            try
            {
                var bitmap = new BitmapImage(new Uri("pack://application:,,,/logo.png", UriKind.Absolute));
                Cache[key] = bitmap;
                return bitmap;
            }
            catch
            {
                return new BitmapImage();
            }
        }
    }
}
