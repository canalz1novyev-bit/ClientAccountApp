using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ClientAccountApp
{
    public static class BillingInvoiceFacturaWordService
    {
        private static readonly CultureInfo Ru = new("ru-RU");

        public static string Generate(int invoiceId)
        {
            using var db = new AppDbContext();

            var invoice = db.Invoices.First(x => x.Id == invoiceId);
            var client = db.Clients.First(x => x.Id == invoice.ClientInfoId);
            var org = invoice.OrganizationProfileId.HasValue
                ? db.OrganizationProfiles.First(x => x.Id == invoice.OrganizationProfileId.Value)
                : ActiveOrganizationService.GetRequired();

            var items = db.InvoiceItems
                .Where(x => x.InvoiceId == invoice.Id)
                .OrderBy(x => x.Id)
                .ToList();

            string path = Path.Combine(Path.GetTempPath(), $"СФ_{invoice.Id}.docx");

            using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());

            var body = main.Document.Body;

            body.Append(P("СЧЕТ-ФАКТУРА", true, 28, JustificationValues.Center));
            body.Append(P($"№ {invoice.InvoiceNumber} от {invoice.InvoiceDate:dd.MM.yyyy}", true, 20, JustificationValues.Center));
            body.Append(P(" ", false));

            body.Append(P("Продавец:", true));
            body.Append(P(org.Name));
            body.Append(P($"ИНН/КПП: {org.Inn} / {org.Kpp}"));
            body.Append(P($"Адрес: {org.LegalAddress}"));

            body.Append(P(" "));
            body.Append(P("Покупатель:", true));
            body.Append(P(client.Name));
            body.Append(P($"ИНН: {client.Inn}"));
            body.Append(P($"Адрес: {client.Address}"));

            body.Append(P(" "));
            body.Append(P("Валюта: Российский рубль (643)", false));

            body.Append(P(" "));
            body.Append(P("Товары (работы, услуги)", true));

            body.Append(CreateTable(items));

            decimal total = items.Sum(x => x.AmountWithVat);
            decimal vat = items.Sum(x => x.VatAmount);

            body.Append(P(" "));
            body.Append(P($"Всего с НДС: {Money(total)}", true));
            body.Append(P($"Сумма НДС: {Money(vat)}", true));

            body.Append(P(" "));
            body.Append(P("Руководитель _____________", false));
            body.Append(P("Главный бухгалтер _____________", false));

            main.Document.Save();

            return path;
        }

        private static Table CreateTable(System.Collections.Generic.List<InvoiceItem> items)
        {
            var table = new Table();

            table.Append(new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single },
                    new BottomBorder { Val = BorderValues.Single },
                    new LeftBorder { Val = BorderValues.Single },
                    new RightBorder { Val = BorderValues.Single },
                    new InsideHorizontalBorder { Val = BorderValues.Single },
                    new InsideVerticalBorder { Val = BorderValues.Single }
                )));

            table.Append(Row(
                "№",
                "Наименование",
                "Кол-во",
                "Цена",
                "Стоимость",
                "НДС",
                "Сумма НДС",
                "С НДС"
            ));

            int i = 1;

            foreach (var it in items)
            {
                table.Append(Row(
                    i.ToString(),
                    it.ServiceName,
                    it.Quantity.ToString("N2", Ru),
                    Money(it.UnitPrice),
                    Money(it.AmountWithoutVat),
                    VatRate(it.VatRate),
                    Money(it.VatAmount),
                    Money(it.AmountWithVat)
                ));
                i++;
            }

            return table;
        }

        private static TableRow Row(params string[] cells)
        {
            var row = new TableRow();

            foreach (var c in cells)
            {
                row.Append(new TableCell(
                    new Paragraph(
                        new Run(new Text(c ?? ""))
                    )
                ));
            }

            return row;
        }

        private static Paragraph P(string text, bool bold = false, int size = 20,
    JustificationValues? align = null)
        {
            var run = new Run();

            var props = new RunProperties(new FontSize { Val = size.ToString() });
            if (bold) props.Append(new Bold());

            run.Append(props);
            run.Append(new Text(text));

            return new Paragraph(
                new ParagraphProperties(new Justification { Val = align ?? JustificationValues.Left }),
                run
            );
        }

        private static string Money(decimal d)
            => d.ToString("N2", Ru) + " ₽";

        private static string VatRate(decimal r)
            => r <= 0 ? "без НДС" : r.ToString("0.##") + "%";
    }
}