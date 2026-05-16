using System;

namespace ClientAccountApp
{
    /// <summary>
    /// УСТАРЕЛО. Ключ DaData больше не хранится в исходниках.
    /// Используйте <see cref="CounterpartyApiKeysService.GetDaDataApiKey"/> — ключ берётся
    /// из защищённого Windows Credential Vault. Настройка ключа: «Параметры → Источники проверок контрагентов».
    /// Этот класс оставлен для обратной совместимости и кидает исключение при обращении к токену.
    /// </summary>
    [Obsolete("Используйте CounterpartyApiKeysService.GetDaDataApiKey(). Ключ хранится в Windows Credential Vault.", error: true)]
    public static class InnLookupSettings
    {
        [Obsolete("Используйте CounterpartyApiKeysService.GetDaDataApiKey()", error: true)]
        public static string DadataToken =>
            throw new InvalidOperationException(
                "InnLookupSettings.DadataToken удалён из соображений безопасности. " +
                "Используйте CounterpartyApiKeysService.GetDaDataApiKey() — ключ хранится в Windows Credential Vault.");
    }
}
