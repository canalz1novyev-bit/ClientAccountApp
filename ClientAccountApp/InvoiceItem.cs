namespace ClientAccountApp
{
    public class InvoiceItem
    {
        public int Id { get; set; }

        public int InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        public int? ServiceCatalogId { get; set; }
        public ServiceCatalog? ServiceCatalog { get; set; }

        public string ServiceName { get; set; } = "";
        public string Unit { get; set; } = "усл.";

        public decimal Quantity { get; set; } = 1m;
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }

        // true = НДС выделяется из цены (цена включает НДС)
        // false = НДС начисляется сверх (по умолчанию)
        public bool IsVatIncluded { get; set; } = false;

        public decimal AmountWithoutVat { get; set; }
        public decimal VatAmount { get; set; }
        public decimal AmountWithVat { get; set; }

        public int SortOrder { get; set; }

        // Вычисляемые свойства для отображения в списке
        public string AmountWithVatText =>
            AmountWithVat.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) + " ₽";

        public string VatAmountText =>
            VatAmount > 0
                ? "НДС: " + VatAmount.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) + " ₽"
                : "";
    }
}