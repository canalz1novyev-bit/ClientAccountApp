using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClientAccountApp
{
    public class DigitalSignature
    {
        [Key]
        public int Id { get; set; }

        public int ClientInfoId { get; set; }

        public ClientInfo? ClientInfo { get; set; }

        public string CertificationAuthority { get; set; } = "";

        public string Comment { get; set; } = "";

        public DateTime IssuedDate { get; set; }

        public DateTime ExpiresDate { get; set; }

        [NotMapped]
        public string DisplayText
        {
            get
            {
                return $"{IssuedDate:dd.MM.yyyy} — {ExpiresDate:dd.MM.yyyy}";
            }
        }
    }
}