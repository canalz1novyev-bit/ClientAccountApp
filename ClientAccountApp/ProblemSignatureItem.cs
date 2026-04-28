using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace ClientAccountApp
{
    public class ProblemSignatureItem
    {
        public int ClientId { get; set; }
        public int SignatureId { get; set; }

        public string ClientName { get; set; } = "";
        public string Inn { get; set; } = "";
        public string CertificationAuthority { get; set; } = "";
        public string Comment { get; set; } = "";

        public DateTime ExpiresDate { get; set; }

        public int DaysLeft { get; set; }

        public string StatusText
        {
            get
            {
                if (DaysLeft < 0)
                {
                    return $"ПРОСРОЧЕНО: {Math.Abs(DaysLeft)} дн.";
                }

                if (DaysLeft <= 7)
                {
                    return $"СРОЧНО: {DaysLeft} дн.";
                }

                return $"ВНИМАНИЕ: {DaysLeft} дн.";
            }
        }

        public string ExpiresDateText
        {
            get
            {
                return ExpiresDate.ToString("dd.MM.yyyy");
            }
        }

        public Brush StatusBrush
        {
            get
            {
                if (DaysLeft < 0)
                {
                    return new SolidColorBrush(Colors.IndianRed);
                }

                if (DaysLeft <= 7)
                {
                    return new SolidColorBrush(Colors.OrangeRed);
                }

                return new SolidColorBrush(Colors.Goldenrod);
            }
        }
    }
}