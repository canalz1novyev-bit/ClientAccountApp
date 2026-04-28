namespace ClientAccountApp
{
    public static class InvoiceStatusNames
    {
        public const string Draft = "Черновик";
        public const string Issued = "Выставлен";
        public const string Paid = "Оплачен";
        public const string Cancelled = "Отменен";
    }

    public static class InvoiceSourceTypeNames
    {
        public const string Manual = "Ручной";
        public const string Automatic = "Авто";
    }

    public static class BillingCycleNames
    {
        public const string Monthly = "Ежемесячно";
        public const string Quarterly = "Ежеквартально";
        public const string Yearly = "Ежегодно";
        public const string OneTime = "Разово";
    }

    public static class ClientFileCategoryNames
    {
        public const string Invoice = "Счет";
    }
}