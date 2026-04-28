using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public sealed class BankLookupResult
    {
        public string Bic { get; set; } = "";
        public string BankName { get; set; } = "";
        public string CorrespondentAccount { get; set; } = "";
    }
}