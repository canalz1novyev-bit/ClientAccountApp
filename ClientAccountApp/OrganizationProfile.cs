using System;

namespace ClientAccountApp
{
    public class OrganizationProfile
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";
        public string ShortName { get; set; } = "";

        public string Inn { get; set; } = "";
        public string Kpp { get; set; } = "";
        public string Ogrn { get; set; } = "";
        public string LegalAddress { get; set; } = "";

        public string DirectorName { get; set; } = "";
        public string DirectorPosition { get; set; } = "Директор";

        public string BankName { get; set; } = "";
        public string BankBic { get; set; } = "";
        public string SettlementAccount { get; set; } = "";
        public string CorrespondentAccount { get; set; } = "";

        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";

        public string LogoRelativePath { get; set; } = "";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}