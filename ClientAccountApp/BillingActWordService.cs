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
    public static class BillingActWordService
    {
        private static readonly CultureInfo RuCulture = new("ru-RU");

        public static string GenerateActDocx(int invoiceId)
        {
            using var db = new AppDbContext();

            var invoice = db.Invoices
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == invoiceId);

            if (invoice == null)
                throw new InvalidOperationException("Счёт не найден.");

            var client = db.Clients
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == invoice.ClientInfoId);

            if (client == null)
                throw new InvalidOperationException("Клиент счёта не найден.");

            OrganizationProfile? organization = null;

            if (invoice.OrganizationProfileId.HasValue)
            {
                organization = db.OrganizationProfiles
                    .AsNoTracking()
                    .FirstOrDefault(x => x.Id == invoice.OrganizationProfileId.Value);
            }

            organization ??= ActiveOrganizationService.GetRequired();

            var items = db.InvoiceItems
                .AsNoTracking()
                .Where(x => x.InvoiceId == invoice.Id)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToList();

            if (items.Count == 0)
                throw new InvalidOperationException("В счёте нет строк услуг.");

            decimal totalWithoutVat = items.Sum(x => x.AmountWithoutVat);
            decimal vatAmount = items.Sum(x => x.VatAmount);
            decimal totalWithVat = items.Sum(x => x.AmountWithVat);

            string clientKey = !string.IsNullOrWhiteSpace(client.Inn)
                ? client.Inn.Trim()
                : client.Id.ToString();

            string fileName = $"Акт_{clientKey}_{invoice.InvoiceDate:yyyyMMdd}_{invoice.Id}.docx";
            string tempPath = Path.Combine(Path.GetTempPath(), fileName);

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using var document = WordprocessingDocument.Create(
                tempPath,
                WordprocessingDocumentType.Document);

            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            Body body = mainPart.Document.Body!;

            body.Append(CreateParagraph("АКТ ОКАЗАННЫХ УСЛУГ", true, 28));
            body.Append(CreateParagraph($"к счёту № {invoice.InvoiceNumber} от {invoice.InvoiceDate:dd.MM.yyyy}", false, 20));
            body.Append(CreateParagraph($"Дата акта: {DateTime.Today:dd.MM.yyyy}", false, 20));
            body.Append(CreateParagraph(" "));

            body.Append(CreateParagraph("Исполнитель", true, 22));
            body.Append(CreateParagraph(EmptyDash(organization.Name)));
            body.Append(CreateParagraph($"ИНН: {EmptyDash(organization.Inn)}   КПП: {EmptyDash(organization.Kpp)}"));
            body.Append(CreateParagraph($"ОГРН / ОГРНИП: {EmptyDash(organization.Ogrn)}"));
            body.Append(CreateParagraph($"Адрес: {EmptyDash(organization.LegalAddress)}"));

            body.Append(CreateParagraph(" "));

            body.Append(CreateParagraph("Заказчик", true, 22));
            body.Append(CreateParagraph(EmptyDash(client.Name)));
            body.Append(CreateParagraph($"ИНН: {EmptyDash(client.Inn)}"));
            body.Append(CreateParagraph($"ОГРН / ОГРНИП: {EmptyDash(client.Ogrn)}"));
            body.Append(CreateParagraph($"Адрес: {EmptyDash(client.Address)}"));

            body.Append(CreateParagraph(" "));

            if (!string.IsNullOrWhiteSpace(invoice.PeriodText))
            {
                body.Append(CreateParagraph($"Период оказания услуг: {invoice.PeriodText}", false, 20));
                body.Append(CreateParagraph(" "));
            }

            body.Append(CreateParagraph("Состав оказанных услуг", true, 22));
            body.Append(CreateItemsTable(items));

            body.Append(CreateParagraph(" "));

            body.Append(CreateParagraph($"Итого без НДС: {FormatMoney(totalWithoutVat)}", true, 20));
            body.Append(CreateParagraph($"НДС: {FormatMoney(vatAmount)}", true, 20));
            body.Append(CreateParagraph($"Итого с НДС: {FormatMoney(totalWithVat)}", true, 22));

            body.Append(CreateParagraph(" "));

            body.Append(CreateParagraph(
                "Услуги оказаны в полном объёме. Стороны претензий по объёму, качеству и срокам оказания услуг не имеют.",
                false,
                20));

            if (!string.IsNullOrWhiteSpace(invoice.Comment))
            {
                body.Append(CreateParagraph(" "));
                body.Append(CreateParagraph($"Комментарий: {invoice.Comment}", false, 18));
            }

            body.Append(CreateParagraph(" "));
            body.Append(CreateSignatureTable(organization, client));

            body.Append(new SectionProperties(
                new PageSize
                {
                    Width = 11906U,
                    Height = 16838U
                },
                new PageMargin
                {
                    Top = 720,
                    Right = 720U,
                    Bottom = 720,
                    Left = 720U,
                    Header = 450U,
                    Footer = 450U,
                    Gutter = 0U
                }));

            mainPart.Document.Save();

            return tempPath;
        }

        private static Table CreateItemsTable(System.Collections.Generic.List<InvoiceItem> items)
        {
            var table = new Table();

            table.Append(new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Color = "CBD5E1", Size = 4 },
                    new BottomBorder { Val = BorderValues.Single, Color = "CBD5E1", Size = 4 },
                    new LeftBorder { Val = BorderValues.Single, Color = "CBD5E1", Size = 4 },
                    new RightBorder { Val = BorderValues.Single, Color = "CBD5E1", Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Color = "E5E7EB", Size = 4 },
                    new InsideVerticalBorder { Val = BorderValues.Single, Color = "E5E7EB", Size = 4 })));

            table.Append(new TableRow(
                CreateCell("№", true, "EAF1F8", "500", JustificationValues.Center),
                CreateCell("Услуга", true, "EAF1F8", "4200", JustificationValues.Left),
                CreateCell("Кол-во", true, "EAF1F8", "900", JustificationValues.Right),
                CreateCell("Ед.", true, "EAF1F8", "800", JustificationValues.Center),
                CreateCell("Цена", true, "EAF1F8", "1200", JustificationValues.Right),
                CreateCell("Сумма", true, "EAF1F8", "1300", JustificationValues.Right)
            ));

            int index = 1;

            foreach (var item in items)
            {
                table.Append(new TableRow(
                    CreateCell(index.ToString(), false, "FFFFFF", "500", JustificationValues.Center),
                    CreateCell(item.ServiceName, false, "FFFFFF", "4200", JustificationValues.Left),
                    CreateCell(item.Quantity.ToString("N2", RuCulture), false, "FFFFFF", "900", JustificationValues.Right),
                    CreateCell(item.Unit, false, "FFFFFF", "800", JustificationValues.Center),
                    CreateCell(FormatMoney(item.UnitPrice), false, "FFFFFF", "1200", JustificationValues.Right),
                    CreateCell(FormatMoney(item.AmountWithVat), true, "FFFFFF", "1300", JustificationValues.Right)
                ));

                index++;
            }

            return table;
        }

        private static Table CreateSignatureTable(OrganizationProfile organization, ClientInfo client)
        {
            var table = new Table();

            table.Append(new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

            string organizationSigner = string.IsNullOrWhiteSpace(organization.DirectorName)
                ? "____________________"
                : $"____________________ / {organization.DirectorName.Trim()} /";

            string clientSigner = string.IsNullOrWhiteSpace(client.DirectorFullName)
                ? "____________________"
                : $"____________________ / {client.DirectorFullName.Trim()} /";

            table.Append(new TableRow(
                CreateSignatureCell("Исполнитель", organizationSigner),
                CreateSignatureCell("Заказчик", clientSigner)
            ));

            return table;
        }

        private static TableCell CreateSignatureCell(string title, string signer)
        {
            var cell = new TableCell();

            cell.Append(new TableCellProperties(
                new TableCellWidth { Width = "50%", Type = TableWidthUnitValues.Pct }));

            cell.Append(CreateParagraph(title, true, 18));
            cell.Append(CreateParagraph(" "));
            cell.Append(CreateParagraph(signer, false, 18));
            cell.Append(CreateParagraph("М.П.", false, 18));

            return cell;
        }

        private static TableCell CreateCell(
            string text,
            bool bold,
            string fill,
            string width,
            JustificationValues alignment)
        {
            var cell = new TableCell();

            cell.Append(new TableCellProperties(
                new TableCellWidth { Width = width, Type = TableWidthUnitValues.Dxa },
                new Shading { Fill = fill, Val = ShadingPatternValues.Clear, Color = "auto" },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));

            cell.Append(CreateParagraph(text, bold, 18, alignment));

            return cell;
        }

        private static Paragraph CreateParagraph(
            string text,
            bool bold = false,
            int fontSize = 20,
            JustificationValues? alignment = null)
        {
            var paragraph = new Paragraph();

            paragraph.Append(new ParagraphProperties(
                new Justification { Val = alignment ?? JustificationValues.Left },
                new SpacingBetweenLines
                {
                    Before = "0",
                    After = "100"
                }));

            var runProperties = new RunProperties(
                new RunFonts
                {
                    Ascii = "Aptos",
                    HighAnsi = "Aptos",
                    EastAsia = "Aptos",
                    ComplexScript = "Aptos"
                },
                new FontSize { Val = fontSize.ToString() });

            if (bold)
                runProperties.Append(new Bold());

            var run = new Run();
            run.Append(runProperties);
            run.Append(new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve });

            paragraph.Append(run);

            return paragraph;
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("N2", RuCulture) + " ₽";
        }

        private static string EmptyDash(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "—"
                : value.Trim();
        }
    }
}