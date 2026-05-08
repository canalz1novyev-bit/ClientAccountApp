using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public sealed class AppUpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = "";
        public string AvailableVersion { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Проверка обновлений через GitHub Releases API.
    /// Пользователь скачивает новую версию вручную — без автоустановки.
    /// </summary>
    public static class AppUpdateService
    {
        // ⚠️ Замени на свой GitHub репозиторий (логин/репо)
        private const string GitHubOwner = "canalz1novyev-bit";
        private const string GitHubRepo = "ClientAccountApp";

        private static readonly HttpClient _http = new HttpClient();

        static AppUpdateService()
        {
            // GitHub API требует User-Agent
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("ClientAccountApp/1.0");
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        public static string GetCurrentVersion()
        {
            try
            {
                var ver = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion;

                if (!string.IsNullOrWhiteSpace(ver))
                {
                    int plus = ver.IndexOf('+');
                    return plus > 0 ? ver[..plus] : ver;
                }

                return Assembly.GetExecutingAssembly()
                    .GetName().Version?.ToString(3) ?? "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }

        public static async Task<AppUpdateCheckResult> CheckForUpdatesAsync()
        {
            string currentVersion = GetCurrentVersion();

            try
            {
                string url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
                string json = await _http.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tag = root.GetProperty("tag_name").GetString() ?? "";
                string available = tag.TrimStart('v');
                string notes = root.TryGetProperty("body", out var body)
                    ? body.GetString() ?? "" : "";

                string downloadUrl = $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest";

                bool isNewer = IsVersionNewer(available, currentVersion);

                return new AppUpdateCheckResult
                {
                    IsUpdateAvailable = isNewer,
                    CurrentVersion = currentVersion,
                    AvailableVersion = available,
                    ReleaseNotes = notes,
                    DownloadUrl = downloadUrl,
                    Message = isNewer
                        ? $"Доступна новая версия {available}!"
                        : "Установлена последняя версия."
                };
            }
            catch (Exception ex)
            {
                return new AppUpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    Message = "Ошибка проверки: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Открывает страницу релиза в браузере — пользователь скачивает сам.
        /// </summary>
        public static void OpenDownloadPage(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = string.IsNullOrWhiteSpace(url)
                        ? $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest"
                        : url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private static bool IsVersionNewer(string available, string current)
        {
            try
            {
                return Version.Parse(available) > Version.Parse(current);
            }
            catch
            {
                return string.Compare(available, current,
                    StringComparison.OrdinalIgnoreCase) > 0;
            }
        }
    }
}
