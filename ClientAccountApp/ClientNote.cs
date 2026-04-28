using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClientAccountApp
{
    public class ClientNote
    {
        [Key]
        public int Id { get; set; }

        public int ClientInfoId { get; set; }

        public ClientInfo? ClientInfo { get; set; }

        public string NoteText { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        [NotMapped]
        public string CreatedAtText
        {
            get
            {
                return CreatedAt.ToString("dd.MM.yyyy HH:mm");
            }
        }
    }
}