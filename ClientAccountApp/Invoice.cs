using System;
using System.Collections.Generic;

namespace ClientAccountApp
{
    public class Invoice
    {
        public int Id { get; set; }

        public int ClientInfoId { get; set; }
        public ClientInfo? Client { get; set; }
        public int? OrganizationProfileId { get; set; }
        public OrganizationProfile? OrganizationProfile { get; set; }

        public string InvoiceNumber { get; set; } = "";
        public DateTime InvoiceDate { get; set; } = DateTime.Today;
        public DateTime? DueDate { get; set; }

        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }

        public string PeriodText { get; set; } = "";
        public string Status { get; set; } = InvoiceStatusNames.Draft;
        public string SourceType { get; set; } = InvoiceSourceTypeNames.Manual;

        public decimal TotalWithoutVat { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalWithVat { get; set; }

        public string Comment { get; set; } = "";
        public string DocumentRelativePath { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}