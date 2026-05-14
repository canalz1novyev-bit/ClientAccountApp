using System;
using System.Collections.Generic;

namespace ClientAccountApp
{
    public class ClientContract
    {
        public int Id { get; set; }

        public int OrganizationProfileId { get; set; }
        public OrganizationProfile? OrganizationProfile { get; set; }

        public int ClientInfoId { get; set; }
        public ClientInfo? ClientInfo { get; set; }

        // Статус и документ
        public string Status { get; set; } = "Требует договора";
        public string ContractNumber { get; set; } = "";
        public string DocumentRelativePath { get; set; } = "";

        public DateTime? GeneratedAt { get; set; }
        public DateTime? SignedAt { get; set; }

        // === МАСТЕР ДОГОВОРОВ (v1.2) ===
        /// <summary>Ключ типа договора. См. ContractTypeDefinitions.</summary>
        public string ContractType { get; set; } = "services";

        /// <summary>Предмет договора — свободный текст.</summary>
        public string Subject { get; set; } = "";

        /// <summary>Сумма договора (без НДС).</summary>
        public decimal Amount { get; set; } = 0;

        /// <summary>"Без НДС" | "НДС 20%" | "НДС 10%"</summary>
        public string VatMode { get; set; } = "Без НДС";

        /// <summary>Город/место составления договора.</summary>
        public string City { get; set; } = "";

        /// <summary>Дата начала действия договора.</summary>
        public DateTime? ValidFrom { get; set; }

        /// <summary>Дата окончания действия договора.</summary>
        public DateTime? ValidTo { get; set; }

        /// <summary>JSON-сериализованная ContractParty для стороны 1.</summary>
        public string Party1Json { get; set; } = "";

        /// <summary>JSON-сериализованная ContractParty для стороны 2.</summary>
        public string Party2Json { get; set; } = "";
        // ================================

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

        // Вспомогательные методы
        public ContractParty? GetParty1() => ContractParty.FromJson(Party1Json);
        public ContractParty? GetParty2() => ContractParty.FromJson(Party2Json);

        /// <summary>
        /// True если договор скоро истекает (в течение 30 дней) или уже истёк.
        /// </summary>
        public bool IsExpiringSoon =>
            ValidTo.HasValue &&
            ValidTo.Value <= DateTime.Today.AddDays(30) &&
            !string.Equals(Status, "Требует договора", StringComparison.OrdinalIgnoreCase);

        public bool IsExpired =>
            ValidTo.HasValue &&
            ValidTo.Value < DateTime.Today;
    }
}
