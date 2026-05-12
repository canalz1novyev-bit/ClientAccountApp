// Генерирует КУДиР (FNS_KNIGAUSN) для УСН
// Формат ВерсФорм 5.05, кодировка WINDOWS-1251.
// Структура проверена по образцу СБИС/SABY.

using ClientAccountApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace ClientAccountApp.Services
{
    public static class BankStatementUsnXmlExporter
    {
        public record OrgInfo(
            string INN, string KPP, string Name,
            string BankName = "", string BankAccount = "", string BankBIC = "",
            string OKPO = "");

        public enum UsnType
        {
            Income6,        // Доходы 6%    → ОбъектНП="1"
            IncomeMinus15   // Доходы−расходы 15% → ОбъектНП="2"
        }

        public static string ExportKudir(
            IEnumerable<BankStatementOperation> ops,
            OrgInfo org, int year, int quarter,
            UsnType usnType, string outputFolder)
        {
            bool inclExpenses = usnType == UsnType.IncomeMinus15;
            string objNP = inclExpenses ? "2" : "1";

            var rows = ops
                .Where(o => o.IsMarkedForVatBook &&
                    (o.VatBookType == "Продажи" ||
                     (inclExpenses && o.VatBookType == "Покупки")))
                .OrderBy(o => o.Date)
                .ToList();

            // Итоговые суммы
            decimal totalD = rows.Where(o => o.VatBookType == "Продажи").Sum(o => o.Amount);
            decimal totalR = inclExpenses
                ? rows.Where(o => o.VatBookType == "Покупки").Sum(o => o.Amount) : 0m;
            decimal loss = totalR > totalD ? totalR - totalD : 0m;

            // Накопительные квартальные итоги (как в образце СБИС)
            decimal d1 = SumByQ(rows, 1, false);
            decimal d6 = d1 + SumByQ(rows, 2, false);
            decimal d9 = d6 + SumByQ(rows, 3, false);
            decimal d12 = d9 + SumByQ(rows, 4, false);

            decimal r1  = inclExpenses ? SumByQ(rows, 1, true) : 0m;
            decimal r6  = r1  + (inclExpenses ? SumByQ(rows, 2, true) : 0m);
            decimal r9  = r6  + (inclExpenses ? SumByQ(rows, 3, true) : 0m);
            decimal r12 = r9  + (inclExpenses ? SumByQ(rows, 4, true) : 0m);

            // Период: Q1=21, полугодие=31, 9мес=33, год=34
            string period = quarter switch { 1 => "21", 2 => "31", 3 => "33", _ => "34" };

            // Реквизиты для НомерСчета
            string bankLine = string.IsNullOrEmpty(org.BankName) ? ""
                : $"{org.BankName}, {org.BankAccount}; ";

            // Имя файла как в образце СБИС: FNS_KNIGAUSN_5_05_{ИНН+КПП}_{дата}_{guid}
            string innKpp   = org.INN + (string.IsNullOrEmpty(org.KPP) ? "" : org.KPP);
            string date8    = DateTime.Now.ToString("yyyyMMdd");
            string guid     = Guid.NewGuid().ToString().ToLower();
            string fileName = $"FNS_KNIGAUSN_5_05_{innKpp}_{date8}_{guid}.xml";

            Directory.CreateDirectory(outputFolder);
            string filePath = Path.Combine(outputFolder, fileName);

            // Шаг 1: пишем тело XML в StringBuilder
            var sb = new StringBuilder();
            var xs = new XmlWriterSettings
            {
                Indent             = true,
                IndentChars        = " ",
                OmitXmlDeclaration = true,
                ConformanceLevel   = ConformanceLevel.Document,
            };

            using (var xw = XmlWriter.Create(sb, xs))
            {
                // <Файл>
                xw.WriteStartElement("Файл");
                xw.WriteAttributeString("ВерсФорм", "5.05");

                // <Документ>
                xw.WriteStartElement("Документ");
                xw.WriteAttributeString("КНД",       "КНДОХОДРАСХОД");
                xw.WriteAttributeString("ОтчетГод",  year.ToString());
                xw.WriteAttributeString("ДатаДок",   DateTime.Now.ToString("yyyy-MM-dd"));
                xw.WriteAttributeString("Период",    period);

                // <СвНП>
                xw.WriteStartElement("СвНП");
                if (!string.IsNullOrEmpty(org.OKPO))
                    xw.WriteAttributeString("ОКПО", org.OKPO);
                xw.WriteAttributeString("НомерСчета", bankLine);
                xw.WriteAttributeString("НаимОрг",   org.Name);
                xw.WriteAttributeString("ИНН",        org.INN);
                xw.WriteAttributeString("ОбъектНП",  objNP);
                if (!string.IsNullOrEmpty(org.KPP))
                    xw.WriteAttributeString("КПП", org.KPP);

                // <Банки>
                if (!string.IsNullOrEmpty(org.BankName))
                {
                    xw.WriteStartElement("Банки");
                    xw.WriteStartElement("Банк");
                    xw.WriteAttributeString("Наименование",              org.BankName);
                    xw.WriteAttributeString("НомерСчетаСтрахователя",   org.BankAccount);
                    xw.WriteAttributeString("БИК",                       org.BankBIC);
                    xw.WriteEndElement(); // Банк
                    xw.WriteEndElement(); // Банки
                }
                xw.WriteEndElement(); // СвНП

                // <КнигаДохРасх>
                xw.WriteStartElement("КнигаДохРасх");
                xw.WriteElementString("СумДох",  F(totalD));
                xw.WriteElementString("СумРасх", F(totalR));
                if (loss > 0)
                    xw.WriteElementString("Убыт", F(loss));

                // <ДохРасх>
                xw.WriteStartElement("ДохРасх");

                // Накопительные итоги — обязательные элементы
                xw.WriteElementString("СумДох1Ит",   F(d1));
                xw.WriteElementString("СумДох6Ит",   F(d6));
                xw.WriteElementString("СумДох9Ит",   F(d9));
                xw.WriteElementString("СумДох12Ит",  F(d12));
                xw.WriteElementString("СумРасх1Ит",  F(r1));
                xw.WriteElementString("СумРасх6Ит",  F(r6));
                xw.WriteElementString("СумРасх9Ит",  F(r9));
                xw.WriteElementString("СумРасх12Ит", F(r12));

                // Строки по кварталам: КварталN
                int n = 1;
                for (int q = 1; q <= 4; q++)
                {
                    var qRows = rows.Where(o => GetQuarter(o.Date) == q).ToList();
                    foreach (var op in qRows)
                    {
                        bool isIncome = op.VatBookType == "Продажи";
                        string sumD = isIncome ? F(op.Amount) : "0.00";
                        string sumR = !isIncome && inclExpenses ? F(op.Amount) : "0.00";
                        string docNum = string.IsNullOrEmpty(op.DocNumber)
                            ? n.ToString() : op.DocNumber;
                        string purpose = op.PaymentPurpose.Length > 200
                            ? op.PaymentPurpose[..197] + "…" : op.PaymentPurpose;

                        xw.WriteStartElement($"Квартал{q}");
                        xw.WriteElementString("НомерСтроки", n.ToString());
                        xw.WriteElementString("ДатаДок",     op.Date.ToString("yyyy-MM-dd"));
                        xw.WriteElementString("НомерДок",    docNum);
                        xw.WriteElementString("Содержание",  purpose);
                        xw.WriteElementString("СумДох",      sumD);
                        xw.WriteElementString("СумРасх",     sumR);
                        xw.WriteEndElement(); // КварталN
                        n++;
                    }
                }

                xw.WriteEndElement(); // ДохРасх
                xw.WriteEndElement(); // КнигаДохРасх
                xw.WriteEndElement(); // Документ
                xw.WriteEndElement(); // Файл
            }

            // Шаг 2: нормализуем под формат СБИС
            string xmlBody = sb.ToString()
                .Replace("\r\n", "\n")
                .Replace(" />", "/>");

            string fullXml = "<?xml version=\"1.0\" encoding=\"WINDOWS-1251\"?>\n"
                             + xmlBody;

            // Шаг 3: сохраняем в кодировке Windows-1251
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            File.WriteAllText(filePath, fullXml, Encoding.GetEncoding("windows-1251"));

            return filePath;
        }

        private static int GetQuarter(DateTime d) =>
            d.Month switch { <= 3 => 1, <= 6 => 2, <= 9 => 3, _ => 4 };

        private static decimal SumByQ(
            List<BankStatementOperation> rows, int q, bool expenses)
            => rows
                .Where(o => GetQuarter(o.Date) == q &&
                    (expenses ? o.VatBookType == "Покупки" : o.VatBookType == "Продажи"))
                .Sum(o => o.Amount);

        private static string F(decimal v) =>
            v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }
}
