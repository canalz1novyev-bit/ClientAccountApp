using System;
using System.IO;
using System.Text.Json;

namespace ClientAccountApp
{
    public sealed class CounterpartyProviderSettings
    {
        public bool KadApiEnabled { get; set; } = false;

        public string KadProviderName { get; set; } = "";

        public string KadApiUrl { get; set; } = "";

        public string KadHttpMethod { get; set; } = "POST";

        public string KadApiKeyHeaderName { get; set; } = "Authorization";

        public string KadApiKeyPrefix { get; set; } = "Bearer";

        public string KadApiKey { get; set; } = "";
    }

    public static class CounterpartyProviderSettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private static string SettingsFolder
        {
            get
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "ClientAccountApp");
            }
        }

        private static string SettingsFilePath
        {
            get
            {
                return Path.Combine(SettingsFolder, "counterparty-providers.json");
            }
        }

        public static CounterpartyProviderSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return new CounterpartyProviderSettings();

                string json = File.ReadAllText(SettingsFilePath);

                var settings = JsonSerializer.Deserialize<CounterpartyProviderSettings>(json, JsonOptions);

                return settings ?? new CounterpartyProviderSettings();
            }
            catch
            {
                return new CounterpartyProviderSettings();
            }
        }

        public static void Save(CounterpartyProviderSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            Directory.CreateDirectory(SettingsFolder);

            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }

        public static bool HasKadApiKey()
        {
            return !string.IsNullOrWhiteSpace(Load().KadApiKey);
        }

        public static void SaveKadApiKey(string apiKey)
        {
            var settings = Load();
            settings.KadApiKey = apiKey?.Trim() ?? "";
            Save(settings);
        }

        public static string GetSettingsFilePath()
        {
            return SettingsFilePath;
        }
    }
}