using System;

namespace ClientAccountApp
{
    public class ClientRecurringService
    {
        public int Id { get; set; }

        public int ClientInfoId { get; set; }
        public ClientInfo? ClientInfo { get; set; }

        public int? ServiceCatalogId { get; set; }
        public ServiceCatalog? ServiceCatalog { get; set; }

        public string ServiceName { get; set; } = "";
        public string Unit { get; set; } = "усл.";
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; } = 1m;
        public decimal VatRate { get; set; }

        public string BillingCycle { get; set; } = BillingCycleNames.Monthly;
        public bool IsAdvanceBilling { get; set; } = true;
        public int GenerateDay { get; set; } = 1;

        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; } = true;
        public string Comment { get; set; } = "";

        public string LastGeneratedPeriodKey { get; set; } = "";
    }
}