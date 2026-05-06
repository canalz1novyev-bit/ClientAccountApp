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

        public string Status { get; set; } = "Требует договора";

        public string ContractNumber { get; set; } = "";
        public string DocumentRelativePath { get; set; } = "";

        public DateTime? GeneratedAt { get; set; }
        public DateTime? SignedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        // === НОВОЕ: Навигационное свойство для счетов ===
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        // ================================================
    }

}