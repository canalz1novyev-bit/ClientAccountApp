using System;
using System.Reflection;
using System.Threading.Tasks;
using Velopack;

namespace ClientAccountApp
{
    public sealed class AppUpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = "";
        public string AvailableVersion { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public static class AppUpdateService
    {
        // Позже заменим на реальный адрес обновлений.
        // Например: https://ниатек.рф/downloads/niatec-client/releases
        private const string UpdateUrl = "https://example.com/niatec-client/releases";

        public static string GetCurrentVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                var infoVersion = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;

                if (!string.IsNullOrWhiteSpace(infoVersion))
                    return infoVersion;

                return assembly.GetName().Version?.ToString() ?? "0.9.0-beta";
            }
            catch
            {
                return "0.9.0-beta";
            }
        }

        public static async Task<AppUpdateCheckResult> CheckForUpdatesAsync()
        {
            try
            {
                var manager = new UpdateManager(UpdateUrl);

                var updateInfo = await manager.CheckForUpdatesAsync();

                string currentVersion = GetCurrentVersion();

                if (updateInfo == null)
                {
                    return new AppUpdateCheckResult
                    {
                        IsUpdateAvailable = false,
                        CurrentVersion = currentVersion,
                        AvailableVersion = "",
                        Message = "Обновлений нет."
                    };
                }

                string availableVersion = updateInfo.TargetFullRelease?.Version?.ToString() ?? "новая версия";

                return new AppUpdateCheckResult
                {
                    IsUpdateAvailable = true,
                    CurrentVersion = currentVersion,
                    AvailableVersion = availableVersion,
                    Message = $"Доступно обновление: {availableVersion}"
                };
            }
            catch (Exception ex)
            {
                return new AppUpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    CurrentVersion = GetCurrentVersion(),
                    AvailableVersion = "",
                    Message = "Ошибка проверки обновлений: " + ex.Message
                };
            }
        }

        public static async Task<string> DownloadAndApplyUpdateAsync()
        {
            try
            {
                var manager = new UpdateManager(UpdateUrl);

                var updateInfo = await manager.CheckForUpdatesAsync();

                if (updateInfo == null)
                    return "Обновлений нет.";

                await manager.DownloadUpdatesAsync(updateInfo);

                if (updateInfo.TargetFullRelease == null)
                    return "Обновление скачано, но не удалось определить пакет для установки.";

                manager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);

                return "Обновление скачано. Приложение будет перезапущено.";
            }
            catch (Exception ex)
            {
                return "Ошибка установки обновления: " + ex.Message;
            }
        }
    }
}