namespace ClientAccountApp
{
    /// <summary>
    /// Аргумент навигации на BillingPage из мастера договоров.
    /// Позволяет предзаполнить форму начисления данными договора.
    /// </summary>
    public sealed class BillingNavigationArgs
    {
        /// <summary>Id клиента — предвыбирается в ComboBox клиентов.</summary>
        public int ClientId { get; init; }

        /// <summary>Id договора — проставляется в Invoice.ClientContractId.</summary>
        public int ContractId { get; init; }

        /// <summary>"UPD" или "Invoice" — тип документа для создания.</summary>
        public string DocumentType { get; init; } = "";
    }
}
