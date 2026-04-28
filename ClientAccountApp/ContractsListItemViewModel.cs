using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace ClientAccountApp
{
    public sealed class ContractsListItemViewModel
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = "";
        public string ClientMetaText { get; set; } = "";
        public string ContractStatusText { get; set; } = "";
        public string ContractNumber { get; set; } = "—";
        public string GeneratedAtText { get; set; } = "—";
        public string SignedAtText { get; set; } = "—";
        public string ContractFileButtonText { get; set; } = "Открыть договор";
        public bool HasContractFile { get; set; }
        public string SignToggleButtonText { get; set; } = "Отметить договор подписан";
        public bool CanToggleSigned { get; set; }

        public string StatusIcon { get; set; } = "•";

        public SolidColorBrush StatusBrush { get; set; } =
            new SolidColorBrush(ColorHelper.FromArgb(255, 80, 80, 80));

        public SolidColorBrush StatusTextBrush { get; set; } =
            new SolidColorBrush(ColorHelper.FromArgb(255, 184, 184, 184));
    }
}