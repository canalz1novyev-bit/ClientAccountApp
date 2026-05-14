using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace ClientAccountApp
{
    /// <summary>
    /// Данные одной стороны договора.
    /// Заполняется из профиля организации, базы клиентов или вручную.
    /// Сериализуется в JSON и хранится в ClientContract.Party1Json / Party2Json.
    /// </summary>
    public sealed class ContractParty
    {
        public string SourceType { get; set; } = ContractPartySourceType.Manual;
        public int? ClientInfoId { get; set; }
        public int? OrganizationProfileId { get; set; }

        // Реквизиты
        public string Name { get; set; } = "";
        public string ShortName { get; set; } = "";
        public string Inn { get; set; } = "";
        public string Kpp { get; set; } = "";
        public string Ogrn { get; set; } = "";
        public string Address { get; set; } = "";
        public string Phone { get; set; } = "";

        // Подписант
        public string SignerFullName { get; set; } = "";
        public string SignerPosition { get; set; } = "Директор";
        public string SignerBasis { get; set; } = "Устава";

        // Банковские реквизиты
        public string BankName { get; set; } = "";
        public string BankBic { get; set; } = "";
        public string SettlementAccount { get; set; } = "";
        public string CorrespondentAccount { get; set; } = "";

        // Вычисляемые
        public string SignerShortName => BuildShortName(SignerFullName);
        public bool IsValid => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Inn);

        // Сериализация
        public string ToJson() => JsonSerializer.Serialize(this);

        public static ContractParty? FromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<ContractParty>(json); }
            catch { return null; }
        }

        // Фабричные методы
        public static ContractParty FromOrganizationProfile(OrganizationProfile org) => new()
        {
            SourceType = ContractPartySourceType.Organization,
            OrganizationProfileId = org.Id,
            Name = org.Name,
            ShortName = string.IsNullOrWhiteSpace(org.ShortName) ? org.Name : org.ShortName,
            Inn = org.Inn,
            Kpp = org.Kpp,
            Ogrn = org.Ogrn,
            Address = org.LegalAddress,
            Phone = org.Phone,
            SignerFullName = org.DirectorName,
            SignerPosition = string.IsNullOrWhiteSpace(org.DirectorPosition) ? "Директор" : org.DirectorPosition,
            SignerBasis = "Устава",
            BankName = org.BankName,
            BankBic = org.BankBic,
            SettlementAccount = org.SettlementAccount,
            CorrespondentAccount = org.CorrespondentAccount
        };

        public static ContractParty FromClientInfo(ClientInfo client, BankAccount? bank = null)
        {
            bool isIp = string.Equals(client.ClientType, "ИП", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(client.ClientType, "ИПГКФХ", StringComparison.OrdinalIgnoreCase);

            return new ContractParty
            {
                SourceType = ContractPartySourceType.Client,
                ClientInfoId = client.Id,
                Name = client.Name,
                ShortName = client.Name,
                Inn = client.Inn,
                Kpp = GetStringProperty(client, "Kpp", "KPP"),
                Ogrn = client.Ogrn,
                Address = client.Address,
                SignerFullName = client.DirectorFullName,
                SignerBasis = isIp ? "Листа записи" : "Устава",
                BankName = bank?.BankName ?? "",
                BankBic = bank?.BIC ?? "",
                SettlementAccount = bank?.AccountNumber ?? "",
                CorrespondentAccount = bank?.CorrespondentAccount ?? ""
            };
        }

        public static ContractParty Empty() => new() { SourceType = ContractPartySourceType.Manual };

        private static string GetStringProperty(object? source, params string[] names)
        {
            if (source == null) return "";
            foreach (var name in names)
            {
                var prop = source.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null) continue;
                var val = prop.GetValue(source)?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
            return "";
        }

        private static string BuildShortName(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "";
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "";
            var initials = string.Concat(parts.Skip(1).Select(p => $"{char.ToUpperInvariant(p[0])}."));
            return string.IsNullOrWhiteSpace(initials) ? parts[0] : $"{parts[0]} {initials}";
        }
    }

    public static class ContractPartySourceType
    {
        public const string Manual = "Manual";
        public const string Organization = "Organization";
        public const string Client = "Client";
    }
}
