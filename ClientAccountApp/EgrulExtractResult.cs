namespace ClientAccountApp
{
    public sealed class EgrulExtractResult
    {
        public string TempPdfPath { get; set; } = string.Empty;
        public string QueryUsed { get; set; } = string.Empty;
        public string SuggestedFileName { get; set; } = string.Empty;
        public string RegistryKind { get; set; } = string.Empty; // ЕГРЮЛ / ЕГРИП
    }
}