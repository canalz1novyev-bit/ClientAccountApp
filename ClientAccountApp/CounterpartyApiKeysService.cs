using System;
using Windows.Security.Credentials;

namespace ClientAccountApp
{
    /// <summary>
    /// Безопасное хранение API-ключей внешних сервисов (DaData и др.) в Windows Credential Vault.
    /// Не храним ключи в исходниках — они попадают в git и становятся достоянием публики.
    /// </summary>
    public static class CounterpartyApiKeysService
    {
        private const string VaultResource    = "ClientAccountApp.DaData";
        private const string VaultUserName    = "ApiKey";

        // ── DaData ───────────────────────────────────────────────────────────

        public static void SaveDaDataApiKey(string apiKey)
        {
            var vault = new PasswordVault();

            // Удаляем старое значение, если есть
            try
            {
                var old = vault.Retrieve(VaultResource, VaultUserName);
                vault.Remove(old);
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning(nameof(CounterpartyApiKeysService) + ".SaveDaDataApiKey",
                    "Старый ключ DaData в Vault не найден (это нормально при первом сохранении): " + ex.Message);
            }

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                vault.Add(new PasswordCredential(
                    VaultResource,
                    VaultUserName,
                    apiKey.Trim()));
            }
        }

        public static string GetDaDataApiKey()
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
                // ключ не сохранён — это допустимо
                return "";
            }
        }

        public static bool HasDaDataApiKey()
        {
            return !string.IsNullOrWhiteSpace(GetDaDataApiKey());
        }
    }
}
