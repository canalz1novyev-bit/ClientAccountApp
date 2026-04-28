using System;

namespace ClientAccountApp
{
    public class InvoiceDocument
    {
        public int Id { get; set; }

        public int InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        public int ClientInfoId { get; set; }
        public ClientInfo? ClientInfo { get; set; }

        public int? OrganizationProfileId { get; set; }
        public OrganizationProfile? OrganizationProfile { get; set; }

        public string DocumentType { get; set; } = "";
        public string DocumentFormat { get; set; } = "";

        public string OriginalFileName { get; set; } = "";
        public string RelativePath { get; set; } = "";

        public long FileSizeBytes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}