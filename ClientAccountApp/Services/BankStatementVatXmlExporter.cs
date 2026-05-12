// Генерирует XML книги покупок (NO_NDS.8) и книги продаж (NO_NDS.9)
// Формат ВерсФорм 5.12, кодировка WINDOWS-1251.
// Структура проверена побайтово по образцам СБИС/SABY.

using ClientAccountApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace ClientAccountApp.Services
{
    public static class BankStatementVatXmlExporter
    {
        public record OrgInfo(string INN, string KPP, string Name);

        public static string ExportKnigaPokupok(
            IEnumerable<BankStatementOperation> ops,
            OrgInfo org, int year, int quarter, string outputFolder)
            => Export(
                ops.Where(o => o.IsMarkedForVatBook && o.VatBookType == "Покупки"),
                org, isPurchases: true, outputFolder);

        public static string ExportKnigaProdazh(
            IEnumerable<BankStatementOperation> ops,
            OrgInfo org, int year, int quarter, string outputFolder)
            => Export(
                ops.Where(o => o.IsMarkedForVatBook && o.VatBookType == "Продажи"),
                org, isPurchases: false, outputFolder);

        private static string Export(
            IEnumerable<BankStatementOperation> rawOps,
            OrgInfo org,
            bool isPurchases,
            string outputFolder)
        {
            var ops = rawOps.OrderBy(o => o.Date).ToList();

            string region  = org.INN.Length >= 4 ? org.INN[..4] : org.INN;
            string innKpp  = org.INN + (string.IsNullOrEmpty(org.KPP) ? "" : org.KPP);
            string date8   = DateTime.Now.ToString("yyyyMMdd");
            string guid    = Guid.NewGuid().ToString().ToUpper();   // верхний регистр, с дефисами
            string bookNum = isPurchases ? "8" : "9";

            // ИдФайл: точка после NO_NDS (как в образце СБИС)
            string idFail   = $"NO_NDS.{bookNum}_{region}_{region}_{innKpp}_{date8}_{guid}";
            // Имя файла: подчёркивание (как в образце СБИС)
            string fileName = $"NO_NDS_{bookNum}_{region}_{region}_{innKpp}_{date8}_{guid}.xml";

            Directory.CreateDirectory(outputFolder);
            string filePath = Path.Combine(outputFolder, fileName);

            // Шаг 1: пишем тело XML в StringBuilder (без декларации)
            var sb = new StringBuilder();
            var xs = new XmlWriterSettings
            {
                Indent             = true,
                IndentChars        = " ",       // 1 пробел — как в образце
                OmitXmlDeclaration = true,      // декларацию добавим вручную
                ConformanceLevel   = ConformanceLevel.Document,
            };

            using (var xw = XmlWriter.Create(sb, xs))
            {
                xw.WriteStartElement("Файл");
                xw.WriteAttributeString("ИдФайл",  idFail);
                xw.WriteAttributeString("ВерсПрог", "ClientAccountApp 1.1");
                xw.WriteAttributeString("ВерсФорм", "5.12");

                xw.WriteStartElement("Документ");
                xw.WriteAttributeString("Индекс",  isPurchases ? "0000080" : "0000090");
                xw.WriteAttributeString("НомКорр", "0");

                if (isPurchases) WritePurchases(xw, ops);
                else             WriteSales(xw, ops);

                xw.WriteEndElement(); // Документ
                xw.WriteEndElement(); // Файл
            }

            // Шаг 2: нормализуем под формат СБИС
            // - прописные WINDOWS-1251 в декларации
            // - Unix переносы строк (\n), как в образце СБИС
            // - убираем пробел перед /> в самозакрывающихся тегах
            string xmlBody = sb.ToString()
                .Replace("\r\n", "\n")   // Windows → Unix переносы
                .Replace(" />", "/>");   // <СведИП ... /> → <СведИП .../>

            string fullXml = "<?xml version=\"1.0\" encoding=\"WINDOWS-1251\"?>\n"
                             + xmlBody;

            // Шаг 3: сохраняем в кодировке Windows-1251
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            File.WriteAllText(filePath, fullXml, Encoding.GetEncoding("windows-1251"));

            return filePath;
        }

        private static void WritePurchases(XmlWriter xw, List<BankStatementOperation> ops)
        {
            decimal totalVat = ops.Sum(o => o.VatAmount);

            xw.WriteStartElement("КнигаПокуп");
            xw.WriteAttributeString("СумНДСВсКПк", F(totalVat));

            for (int i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                xw.WriteStartElement("КнПокСтр");
                xw.WriteAttributeString("НомерПор",    (i + 1).ToString());
                xw.WriteAttributeString("НомСчФПрод",  SfNum(op.VatInvoiceNumber));
                xw.WriteAttributeString("ДатаСчФПрод", DateStr(op.VatInvoiceDate ?? op.Date));
                xw.WriteAttributeString("СтоимПокупВ", F(op.Amount));
                xw.WriteAttributeString("СумНДСВыч",   F(op.VatAmount));

                xw.WriteElementString("КодВидОпер", "01");

                xw.WriteStartElement("СвПрод");
                WriteCounterparty(xw, op.CounterpartyINN, op.CounterpartyKPP);
                xw.WriteEndElement();

                xw.WriteEndElement(); // КнПокСтр
            }

            xw.WriteEndElement(); // КнигаПокуп
        }

        private static void WriteSales(XmlWriter xw, List<BankStatementOperation> ops)
        {
            decimal base20 = ops.Where(o => o.VatRate >= 20m).Sum(o => o.AmountWithoutVat);
            decimal base10 = ops.Where(o => o.VatRate == 10m).Sum(o => o.AmountWithoutVat);
            decimal base0  = ops.Where(o => o.VatRate == 0m) .Sum(o => o.AmountWithoutVat);

            xw.WriteStartElement("КнигаПрод");
            if (base20 > 0) xw.WriteAttributeString("СтПродБезНДС20", F(base20));
            if (base10 > 0) xw.WriteAttributeString("СтПродБезНДС10", F(base10));
            if (base0  > 0) xw.WriteAttributeString("СтПродНДС0",     F(base0));

            for (int i = 0; i < ops.Count; i++)
            {
                var op  = ops[i];
                string sfx = op.VatRate >= 20m ? "20" : op.VatRate == 10m ? "10" : "0";

                xw.WriteStartElement("КнПродСтр");
                xw.WriteAttributeString("НомерПор",          (i + 1).ToString());
                xw.WriteAttributeString("НомСчФПрод",         SfNum(op.VatInvoiceNumber));
                xw.WriteAttributeString("ДатаСчФПрод",        DateStr(op.VatInvoiceDate ?? op.Date));
                xw.WriteAttributeString("СтоимПродСФ",        F(op.Amount));
                xw.WriteAttributeString($"СтоимПродСФ{sfx}", F(op.AmountWithoutVat));
                xw.WriteAttributeString($"СумНДССФ{sfx}",    F(op.VatAmount));

                xw.WriteElementString("КодВидОпер", "01");

                xw.WriteStartElement("СвПокуп");
                WriteCounterparty(xw, op.CounterpartyINN, op.CounterpartyKPP);
                xw.WriteEndElement();

                xw.WriteEndElement(); // КнПродСтр
            }

            xw.WriteEndElement(); // КнигаПрод
        }

        private static void WriteCounterparty(XmlWriter xw, string inn, string kpp)
        {
            inn = (inn ?? "").Trim();
            if (inn.Length == 12) // ИП = 12-значный ИНН
            {
                xw.WriteStartElement("СведИП");
                xw.WriteAttributeString("ИННФЛ", inn);
                xw.WriteEndElement();
            }
            else // ЮЛ = 10-значный ИНН
            {
                xw.WriteStartElement("СведЮЛ");
                xw.WriteAttributeString("ИННЮЛ", inn);
                if (!string.IsNullOrWhiteSpace(kpp))
                    xw.WriteAttributeString("КПП", kpp);
                xw.WriteEndElement();
            }
        }

        private static string F(decimal v) =>
            v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        private static string DateStr(DateTime d) =>
            d == default ? "" : d.ToString("dd.MM.yyyy");

        private static string SfNum(string? s) =>
            string.IsNullOrWhiteSpace(s) ? "б/н" : s.Trim();
    }
}
