// Добавить в папку Services/
// Генерирует XML в формате КНД 1010212 (книга покупок) и КНД 1010213 (книга продаж)
// для загрузки в СБИС/SABY.

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

        // ─── Публичные точки входа ────────────────────────────────────────────

        public static string ExportKnigaPokupok(
            IEnumerable<BankStatementOperation> ops,
            OrgInfo org, int year, int quarter, string outputFolder)
            => Export(
                ops.Where(o => o.IsMarkedForVatBook && o.VatBookType == "Покупки"),
                org, year, quarter, isPurchases: true, outputFolder);

        public static string ExportKnigaProdazh(
            IEnumerable<BankStatementOperation> ops,
            OrgInfo org, int year, int quarter, string outputFolder)
            => Export(
                ops.Where(o => o.IsMarkedForVatBook && o.VatBookType == "Продажи"),
                org, year, quarter, isPurchases: false, outputFolder);

        // ─── Генерация XML ────────────────────────────────────────────────────

        private static string Export(
            IEnumerable<BankStatementOperation> rawOps,
            OrgInfo org, int year, int quarter,
            bool isPurchases, string outputFolder)
        {
            var ops = rawOps.OrderBy(o => o.Date).ToList();
            string knd = isPurchases ? "1010212" : "1010213";
            string pfx = isPurchases ? "ON_KNPOK" : "ON_KNPR";
            string g8 = Guid.NewGuid().ToString("N")[..8].ToUpper();

            decimal sum20 = ops.Where(o => o.VatRate == 20m).Sum(o => o.VatAmount);
            decimal sum10 = ops.Where(o => o.VatRate == 10m).Sum(o => o.VatAmount);
            decimal sum0 = ops.Where(o => o.VatRate == 0m).Sum(o => o.VatAmount);

            Directory.CreateDirectory(outputFolder);
            string fileName = $"{pfx}_{org.INN}_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
            string filePath = Path.Combine(outputFolder, fileName);

            var xSettings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            };

            using var fs = File.Create(filePath);
            using var xw = XmlWriter.Create(fs, xSettings);

            xw.WriteStartDocument();

            // ── Корневой элемент ──────────────────────────────────────────────
            xw.WriteStartElement("Файл");
            xw.WriteAttributeString("ИдФайл", $"{pfx}_{org.INN}_{g8}_{DateTime.Now:yyyyMMdd}");
            xw.WriteAttributeString("ВерсФорм", "5.07");
            xw.WriteAttributeString("ПрогрОбесп", "ClientAccountApp");
            xw.WriteAttributeString("ДатаФайл", DateTime.Now.ToString("dd.MM.yyyy"));
            xw.WriteAttributeString("ВремяФайл", DateTime.Now.ToString("HH.mm.ss"));

            // ── Документ ─────────────────────────────────────────────────────
            xw.WriteStartElement("Документ");
            xw.WriteAttributeString("КНД", knd);
            xw.WriteAttributeString("ДатаДок", DateTime.Now.ToString("dd.MM.yyyy"));
            xw.WriteAttributeString("НомДок", "1");
            xw.WriteAttributeString("ОтчетГод", year.ToString());
            xw.WriteAttributeString("НомКв", quarter.ToString());

            // ── Сведения о налогоплательщике ─────────────────────────────────
            xw.WriteStartElement("СвНП");
            xw.WriteAttributeString("ИННЮЛ", org.INN);
            xw.WriteAttributeString("КПП", org.KPP);
            xw.WriteAttributeString("НаимОрг", org.Name);
            xw.WriteEndElement();

            // ── Книга ─────────────────────────────────────────────────────────
            string bookEl = isPurchases ? "КнигаПокупок" : "КнигаПродаж";
            string rowsEl = isPurchases ? "СтрокиКнигиПокупок" : "СтрокиКнигиПродаж";
            string rowEl = isPurchases ? "СтрокКнигиПокупок" : "СтрокКнигиПродаж";

            xw.WriteStartElement(bookEl);
            xw.WriteStartElement(rowsEl);
            xw.WriteAttributeString("ВсегоСумНДС20", F(sum20));
            xw.WriteAttributeString("ВсегоСумНДС10", F(sum10));
            xw.WriteAttributeString("ВсегоСумНДС0", F(sum0));

            // ── Строки ────────────────────────────────────────────────────────
            int n = 1;
            foreach (var op in ops)
            {
                string sfNum = op.VatInvoiceNumber;
                string sfDate = op.VatInvoiceDate?.ToString("dd.MM.yyyy") ?? "";
                string kpp = string.IsNullOrEmpty(op.CounterpartyKPP) ? "0" : op.CounterpartyKPP;
                string rateAttr = op.VatRate switch { 20m => "НДС20", 10m => "НДС10", _ => "НДС0" };

                xw.WriteStartElement(rowEl);
                xw.WriteAttributeString("НомСтр", n.ToString());
                xw.WriteAttributeString("КодВидОпер", "01"); // обычная операция

                if (isPurchases)
                {
                    xw.WriteStartElement("СвСчФактПост");
                    xw.WriteAttributeString("НомСчФ", sfNum);
                    xw.WriteAttributeString("ДатаСчФ", sfDate);
                    xw.WriteAttributeString("СумНДС", F(op.VatAmount));
                    xw.WriteStartElement("СвПрод");
                    xw.WriteAttributeString("ИННЮЛ", op.CounterpartyINN);
                    xw.WriteAttributeString("КПП", kpp);
                    xw.WriteAttributeString("НаимПрод", op.CounterpartyName);
                    xw.WriteEndElement(); // СвПрод
                    xw.WriteEndElement(); // СвСчФактПост

                    xw.WriteStartElement("СтоимПокупокВс");
                    xw.WriteAttributeString(rateAttr, F(op.VatAmount));
                    xw.WriteAttributeString("СумОсн", F(op.AmountWithoutVat));
                    xw.WriteEndElement();
                }
                else
                {
                    xw.WriteStartElement("СвСчФактВыст");
                    xw.WriteAttributeString("НомСчФ", sfNum);
                    xw.WriteAttributeString("ДатаСчФ", sfDate);
                    xw.WriteAttributeString("СумНДС", F(op.VatAmount));
                    xw.WriteStartElement("СвПокуп");
                    xw.WriteAttributeString("ИННЮЛ", op.CounterpartyINN);
                    xw.WriteAttributeString("КПП", kpp);
                    xw.WriteAttributeString("НаимПокуп", op.CounterpartyName);
                    xw.WriteEndElement(); // СвПокуп
                    xw.WriteEndElement(); // СвСчФактВыст

                    xw.WriteStartElement("СтоимПродажВс");
                    xw.WriteAttributeString(rateAttr, F(op.VatAmount));
                    xw.WriteAttributeString("СумОсн", F(op.AmountWithoutVat));
                    xw.WriteEndElement();
                }

                xw.WriteEndElement(); // rowEl
                n++;
            }

            xw.WriteEndElement(); // rowsEl
            xw.WriteEndElement(); // bookEl
            xw.WriteEndElement(); // Документ
            xw.WriteEndElement(); // Файл
            xw.WriteEndDocument();

            return filePath;
        }

        private static string F(decimal v) =>
            v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    }
}