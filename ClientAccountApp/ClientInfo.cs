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
        public string MainOkved { get; set; } = "";
        public string BusinessCategory { get; set; } = "";
        public string Status { get; set; } = "Активный";

        public string ContractStatus { get; set; } = "Требует договора";
        public DateTime? ContractGeneratedAt { get; set; }
        public DateTime? ContractSignedAt { get; set; }

        public ICollection<DigitalSignature> DigitalSignatures { get; set; } = new List<DigitalSignature>();
        public ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
        public ICollection<ClientNote> Notes { get; set; } = new List<ClientNote>();
        public ICollection<ClientFile> ClientFiles { get; set; } = new List<ClientFile>();
        [NotMapped]
        public SolidColorBrush BusinessCategoryChipBackgroundBrush
        {
            get
            {
                string category = BusinessCategoryChipText;

                if (category.Contains("Торговля", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 38, 96, 150));

                if (category.Contains("Перевозки", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("логистика", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 97, 76, 150));

                if (category.Contains("С/Х", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 72, 125, 58));

                if (category.Contains("IT", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("связь", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 35, 118, 145));

                if (category.Contains("Строительство", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 158, 99, 32));

                if (category.Contains("Производство", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 94, 103, 120));

                if (category.Contains("общепит", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("Гостиницы", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 156, 83, 45));

                if (category.Contains("консалтинг", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("бух", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("Юр", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 122, 88, 160));

                if (category.Contains("Недвижимость", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 82, 98, 150));

                if (category.Contains("Медицина", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 48, 132, 110));

                if (category.Contains("Образование", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 120, 104, 45));

                if (category.Contains("Не указано", StringComparison.OrdinalIgnoreCase) ||
                    category.Contains("Без категории", StringComparison.OrdinalIgnoreCase))
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 90, 96, 110));

                return new SolidColorBrush(ColorHelper.FromArgb(255, 80, 90, 110));
            }
        }

        [NotMapped]
        public SolidColorBrush BusinessCategoryChipForegroundBrush =>
            new SolidColorBrush(Colors.White);
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
        public string BusinessCategoryChipText =>
    string.IsNullOrWhiteSpace(BusinessCategory)
        ? "Без категории"
        : BusinessCategory;

        [NotMapped]
        public string MainOkvedDisplayText =>
            string.IsNullOrWhiteSpace(MainOkved)
                ? "ОКВЭД не указан"
                : $"ОКВЭД {MainOkved}";

        [NotMapped]
        public string ClientTypeAndBusinessCategoryText
        {
            get
            {
                string type = string.IsNullOrWhiteSpace(ClientType) ? "Клиент" : ClientType;
                string category = string.IsNullOrWhiteSpace(BusinessCategory) ? "Без категории" : BusinessCategory;
                return $"{type} · {category}";
            }
        }

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

        /// <summary>
        /// Имя + категория бизнеса через точку. Тип клиента (ИП/ООО) уже содержится в Name,
        /// поэтому здесь не повторяется. Используется в карточке списка клиентов.
        /// </summary>
        [NotMapped]
        public string NameWithCategory
        {
            get
            {
                if (string.IsNullOrWhiteSpace(BusinessCategory))
                    return Name;
                return $"{Name} · {BusinessCategory}";
            }
        }

        /// <summary>
        /// ИНН без типа клиента: "ИНН 6805000020605" или "ИНН не указан".
        /// </summary>
        [NotMapped]
        public string InnDisplayText =>
            string.IsNullOrWhiteSpace(Inn)
                ? "ИНН не указан"
                : $"ИНН {Inn}";

        /// <summary>
        /// Visibility.Visible — ЭЦП в норме (текст начинается с "ЭЦП действует").
        /// Используется для условного цвета в карточке: серый когда OK, оранжевый когда проблема.
        /// </summary>
        [NotMapped]
        public Visibility SignatureOk =>
            SignatureWarningText.StartsWith("ЭЦП действует", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;

        [NotMapped]
        public Visibility SignatureNotOk =>
            SignatureWarningText.StartsWith("ЭЦП действует", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Collapsed
                : Visibility.Visible;

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