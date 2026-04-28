using System;
using System.IO;
using System.Text.Json;
using Windows.Storage;

namespace ClientAccountApp
{
    public sealed class OrganizationSettings
    {
        public string Name { get; set; } = "ООО «НИАТЕК»";
        public string Inn { get; set; } = "";
        public string Kpp { get; set; } = "";
        public string Address { get; set; } = "";

        public string BankName { get; set; } = "";
        public string BankAccount { get; set; } = "";
        public string Bik { get; set; } = "";
        public string CorrespondentAccount { get; set; } = "";

        public string Director { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
    }

    public static class OrganizationSettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private static string FilePath =>
            Path.Combine(ApplicationData.Current.LocalFolder.Path, "organization-settings.json");

        public static OrganizationSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new OrganizationSettings();

                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<OrganizationSettings>(json, JsonOptions)
                    ?? new OrganizationSettings();
            }
            catch
            {
                return new OrganizationSettings();
            }
        }

        public static void Save(OrganizationSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
    }
}