using System;

namespace ClientAccountApp
{
    public class ServiceCatalog
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";
        public string Unit { get; set; } = "усл.";

        public decimal DefaultPrice { get; set; }
        public decimal DefaultVatRate { get; set; }

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }

        public string Comment { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}