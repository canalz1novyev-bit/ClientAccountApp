// Добавить в папку Services/
// Генерирует Книгу учёта доходов и расходов (КУДиР) по КНД 1110385
// для организаций на УСН. Предназначен для загрузки в СБИС/SABY.

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
        public record OrgInfo(string INN, string KPP, string Name);

        public enum UsnType
        {
            Income6,        // Доходы — 6%
            IncomeMinus15   // Доходы минус расходы — 15%
        }

        /// <summary>
        /// Формирует КУДиР (КНД 1110385) из размеченных операций выписки.
        /// VatBookType="Продажи" → Доходы (СумД).
        /// VatBookType="Покупки" → Расходы (СумР) — только для УСН 15%.
        /// </summary>
        public static string ExportKudir(
            IEnumerable<BankStatementOperation> ops,
            OrgInfo org, int year, int quarter,
            UsnType usnType, string outputFolder)
        {
            bool inclExpenses = usnType == UsnType.IncomeMinus15;

            var rows = ops
                .Where(o => o.IsMarkedForVatBook &&
                    (o.VatBookType == "Продажи" ||
                     (inclExpenses && o.VatBookType == "Покупки")))
                .OrderBy(o => o.Date)
                .ToList();

            Directory.CreateDirectory(outputFolder);
            string fileName = $"KNI_1110385_{org.INN}_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
            string filePath = Path.Combine(outputFolder, fileName);

            // НалПер: 1=I кв., 2=полугодие, 3=9 мес., 0=год
            int nalPer = quarter;

            var xSettings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            };

            using var fs = File.Create(filePath);
            using var xw = XmlWriter.Create(fs, xSettings);

            xw.WriteStartDocument();

            // ── Файл ────────────────────────────────────────────────────────
            xw.WriteStartElement("Файл");
            xw.WriteAttributeString("ИдФайл", $"KNI_1110385_{org.INN}_{DateTime.Now:yyyyMMdd}");
            xw.WriteAttributeString("ВерсФорм", "1.07");
            xw.WriteAttributeString("ПрогрОбесп", "ClientAccountApp");
            xw.WriteAttributeString("ДатаФайл", DateTime.Now.ToString("dd.MM.yyyy"));
            xw.WriteAttributeString("ВремяФайл", DateTime.Now.ToString("HH.mm.ss"));

            // ── Документ ────────────────────────────────────────────────────
            xw.WriteStartElement("Документ");
            xw.WriteAttributeString("КНД", "1110385");
            xw.WriteAttributeString("ОтчетГод", year.ToString());
            xw.WriteAttributeString("НалПер", nalPer.ToString());
            xw.WriteAttributeString("НомКорр", "0");

            // ── Налогоплательщик ─────────────────────────────────────────────
            xw.WriteStartElement("СведНП");
            xw.WriteAttributeString("ИННЮЛ", org.INN);
            xw.WriteAttributeString("КПП", org.KPP);
            xw.WriteAttributeString("НаимОрг", org.Name);
            xw.WriteEndElement();

            // ── Раздел I — Доходы и расходы ──────────────────────────────────
            xw.WriteStartElement("Раздел1");

            int n = 1;
            foreach (var op in rows)
            {
                bool isIncome = op.VatBookType == "Продажи";
                string sumD = isIncome ? F(op.Amount) : "0.00";
                string sumR = !isIncome && inclExpenses ? F(op.Amount) : "0.00";
                string docNum = string.IsNullOrEmpty(op.DocNumber) ? $"п/п {n}" : op.DocNumber;
                string purpose = op.PaymentPurpose.Length > 200
                    ? op.PaymentPurpose[..197] + "…" : op.PaymentPurpose;

                xw.WriteStartElement("СтрРазд1");
                xw.WriteAttributeString("НомСтр", n.ToString());
                xw.WriteAttributeString("ДатаОпер", op.Date.ToString("dd.MM.yyyy"));
                xw.WriteAttributeString("НомДокОпер", docNum);
                xw.WriteAttributeString("СодОпер", purpose);
                xw.WriteAttributeString("СумД", sumD);
                xw.WriteAttributeString("СумР", sumR);
                xw.WriteEndElement();
                n++;
            }

            // Итоговая строка раздела
            decimal totalD = rows.Where(o => o.VatBookType == "Продажи").Sum(o => o.Amount);
            decimal totalR = inclExpenses ? rows.Where(o => o.VatBookType == "Покупки").Sum(o => o.Amount) : 0m;
            xw.WriteStartElement("ИтогРазд1");
            xw.WriteAttributeString("СумД", F(totalD));
            xw.WriteAttributeString("СумР", F(totalR));
            xw.WriteEndElement();

            xw.WriteEndElement(); // Раздел1
            xw.WriteEndElement(); // Документ
            xw.WriteEndElement(); // Файл
            xw.WriteEndDocument();

            return filePath;
        }

        private static string F(decimal v) =>
            v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    }
}