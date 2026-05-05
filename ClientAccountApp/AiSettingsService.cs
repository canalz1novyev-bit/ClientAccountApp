using System;
using System.IO;
using System.Text.Json;
using Windows.Security.Credentials;
using Windows.Storage;

namespace ClientAccountApp
{
    public sealed class AiSettings
    {
        public string Provider { get; set; } = "GigaChat";

        public string GigaChatOAuthUrl { get; set; } =
            "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";

        public string GigaChatApiUrl { get; set; } =
            "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";

        public string GigaChatModel { get; set; } = "GigaChat";

        public string GigaChatScope { get; set; } = "GIGACHAT_API_PERS";

        public bool IsEnabled { get; set; } = false;
    }

    public static class AiSettingsService
    {
        private const string VaultResource = "ClientAccountApp.GigaChat";
        private const string VaultUserName = "AuthorizationKey";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private static string FilePath =>
            Path.Combine(AppPaths.AppDataFolder, "ai-settings.json");

        public static AiSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new AiSettings();

                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AiSettings>(json, JsonOptions) ?? new AiSettings();
            }
            catch
            {
                return new AiSettings();
            }
        }

        public static void Save(AiSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }

        public static void SaveGigaChatAuthorizationKey(string authorizationKey)
        {
            var vault = new PasswordVault();

            try
            {
                var oldCredential = vault.Retrieve(VaultResource, VaultUserName);
                vault.Remove(oldCredential);
            }
            catch
            {
                // старого ключа может не быть
            }

            if (!string.IsNullOrWhiteSpace(authorizationKey))
            {
                vault.Add(new PasswordCredential(
                    VaultResource,
                    VaultUserName,
                    authorizationKey.Trim()));
            }
        }

        public static string GetGigaChatAuthorizationKey()
        {
            try
            {
                var vault = new PasswordVault();
                var credential = vault.Retrieve(VaultResource, VaultUserName);
                credential.RetrievePassword();
                return credential.Password ?? "";
            }
            catch
            {
                return "";
            }
        }

        public static bool HasGigaChatAuthorizationKey()
        {
            return !string.IsNullOrWhiteSpace(GetGigaChatAuthorizationKey());
        }
    }
}