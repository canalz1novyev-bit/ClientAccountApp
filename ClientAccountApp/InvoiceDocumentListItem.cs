namespace ClientAccountApp
{
    public sealed class InvoiceDocumentListItem
    {
        public int Id { get; set; }

        public string Title { get; set; } = "";
        public string MetaText { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public string StatusText { get; set; } = "";
        public bool CanOpen { get; set; }
    }
}