using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ClientAccountApp
{
    public class ClientInfo
    {
        [Key]
        public int Id { get; set; }

        public string ClientType { get; set; } = "";
        public string Name { get; set; } = "";
        public string DirectorFullName { get; set; } = "";
        public string Inn { get; set; } = "";
        public string Ogrn { get; set; } = "";
        public string Address { get; set; } = "";

        public string Status { get; set; } = "Активный";

        public string ContractStatus { get; set; } = "Требует договора";
        public DateTime? ContractGeneratedAt { get; set; }
        public DateTime? ContractSignedAt { get; set; }

        public ICollection<DigitalSignature> DigitalSignatures { get; set; } = new List<DigitalSignature>();
        public ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
        public ICollection<ClientNote> Notes { get; set; } = new List<ClientNote>();
        public ICollection<ClientFile> ClientFiles { get; set; } = new List<ClientFile>();

        [NotMapped]
        public string StatusDisplayText =>
            string.IsNullOrWhiteSpace(Status) ? "Активный" : Status;

        [NotMapped]
        public string ContractStatusDisplayText =>
            string.IsNullOrWhiteSpace(ContractStatus) ? "Требует договора" : ContractStatus;

        [NotMapped]
        public bool HasSignedContract =>
            string.Equals(ContractStatusDisplayText, "Договор подписан", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public Visibility StatusBadgeVisibility =>
            HasSignedContract ? Visibility.Visible : Visibility.Collapsed;

        [NotMapped]
        public string StatusBadgeText => "✓";

        [NotMapped]
        public SolidColorBrush StatusBadgeBackgroundBrush =>
            new SolidColorBrush(ColorHelper.FromArgb(255, 58, 110, 72));

        [NotMapped]
        public string ClientListMetaText
        {
            get
            {
                string type = string.IsNullOrWhiteSpace(ClientType) ? "Клиент" : ClientType;
                string inn = string.IsNullOrWhiteSpace(Inn) ? "ИНН не указан" : $"ИНН {Inn}";
                return $"{type} | {inn}";
            }
        }

        [NotMapped]
        public int SignatureCount { get; set; }

        [NotMapped]
        public DateTime? NearestSignatureExpiresDate { get; set; }

        [NotMapped]
        public string SignatureWarningText { get; set; } = "ЭЦП не добавлена";

        [NotMapped]
        public string SignatureShortInfo
        {
            get
            {
                if (SignatureCount <= 0 || !NearestSignatureExpiresDate.HasValue)
                {
                    return "ЭЦП: нет";
                }

                return $"ЭЦП: {SignatureCount}, ближайшая до {NearestSignatureExpiresDate.Value:dd.MM.yyyy}";
            }
        }

        [NotMapped]
        public int AccountCount { get; set; }

        [NotMapped]
        public string PrimaryBankName { get; set; } = "—";

        [NotMapped]
        public string AccountShortInfo
        {
            get
            {
                if (AccountCount <= 0)
                {
                    return "Счета: нет";
                }

                return $"Счета: {AccountCount}, основной банк: {PrimaryBankName}";
            }
        }

        [NotMapped]
        public int NoteCount { get; set; }

        [NotMapped]
        public DateTime? LatestNoteCreatedAt { get; set; }

        [NotMapped]
        public string NoteShortInfo
        {
            get
            {
                if (NoteCount <= 0 || !LatestNoteCreatedAt.HasValue)
                {
                    return "Заметки: нет";
                }

                return $"Заметки: {NoteCount}, последняя {LatestNoteCreatedAt.Value:dd.MM.yyyy}";
            }
        }

        [NotMapped]
        public string DisplayText =>
            $"{ClientType} | {Name} | ИНН {Inn}";
    }
}