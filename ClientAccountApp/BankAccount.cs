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
    }
}