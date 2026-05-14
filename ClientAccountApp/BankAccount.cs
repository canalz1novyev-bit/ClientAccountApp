using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClientAccountApp
{
    public class BankAccount
    {
        [Key]
        public int Id { get; set; }

        public int ClientInfoId { get; set; }

        public ClientInfo? ClientInfo { get; set; }

        public string BankName { get; set; } = "";

        public string BIC { get; set; } = "";

        public string CorrespondentAccount { get; set; } = "";

        public string AccountNumber { get; set; } = "";

        public string Comment { get; set; } = "";

        [NotMapped]
        public string DisplayText
        {
            get
            {
                return $"{BankName} | р/с {AccountNumber}";
            }
        }

        [NotMapped]
        public string BankDisplayText =>
            string.IsNullOrWhiteSpace(BankName)
                ? "Банк не указан"
                : BankName;

        [NotMapped]
        public string BicDisplayText =>
            string.IsNullOrWhiteSpace(BIC)
                ? "БИК не указан"
                : $"БИК {BIC}";

        [NotMapped]
        public string AccountDisplayText =>
            string.IsNullOrWhiteSpace(AccountNumber)
                ? "Расчётный счёт не указан"
                : $"р/с {AccountNumber}";

        [NotMapped]
        public string CorrespondentAccountDisplayText =>
            string.IsNullOrWhiteSpace(CorrespondentAccount)
                ? "к/с не указан"
                : $"к/с {CorrespondentAccount}";

        [NotMapped]
        public string CommentDisplayText =>
            string.IsNullOrWhiteSpace(Comment)
                ? "Комментарий не указан"
                : Comment;
    }
}