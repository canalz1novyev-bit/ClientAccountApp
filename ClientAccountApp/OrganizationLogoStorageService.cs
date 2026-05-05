using System;
using System.IO;
using Windows.Storage;

namespace ClientAccountApp
{
    public static class OrganizationLogoStorageService
    {
        private static string RootFolder =>
            Path.Combine(AppPaths.AppDataFolder, "OrganizationFiles");

        public static string SaveLogoForOrganization(int organizationId, string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Путь к логотипу пустой.", nameof(sourcePath));

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Файл логотипа не найден.", sourcePath);

            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();

            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                throw new InvalidOperationException("Поддерживаются только PNG, JPG и JPEG.");

            string organizationFolder = Path.Combine(RootFolder, $"Organization_{organizationId}");
            Directory.CreateDirectory(organizationFolder);

            string targetFileName = $"logo{extension}";
            string targetFullPath = Path.Combine(organizationFolder, targetFileName);

            File.Copy(sourcePath, targetFullPath, true);

            return Path.Combine($"Organization_{organizationId}", targetFileName);
        }

        public static string GetLogoFullPath(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return "";

            return Path.Combine(RootFolder, relativePath);
        }
    }
}