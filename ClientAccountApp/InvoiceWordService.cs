using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;


namespace ClientAccountApp
{
    public static class InvoiceWordService
    {
        // Потом вынесем эти реквизиты в настройки приложения.
        private const string SellerName = "ООО «НИАТЕК»";
        private const string SellerInn = "ИНН: —";
        private const string SellerKpp = "КПП: —";
        private const string SellerBank = "Банк: —";
        private const string SellerAccount = "Р/с: —";
        private const string SellerBik = "БИК: —";
        private const string SellerEmail = "E-mail: —";

        private static readonly CultureInfo RuCulture = new("ru-RU");

        public static string GenerateInvoiceDocx(int invoiceId)
        {
            using var db = new AppDbContext();

            var invoice = db.Invoices
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == invoiceId);

            if (invoice == null)
                throw new InvalidOperationException("Счет не найден в базе данных.");

            var client = db.Clients
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == invoice.ClientInfoId);

            if (client == null)
                throw new InvalidOperationException("Клиент счета не найден.");

            var organization = ResolveOrganization(db, invoice);

            // Ищем подписанный договор с этим клиентом для текущей организации
            // AsEnumerable() переносит фильтрацию статуса в память —
            // EF Core не поддерживает string.Equals с StringComparison в SQL
            var signedContract = db.ClientContracts
                .AsNoTracking()
                .Where(c =>
                    c.ClientInfoId == client.Id &&
                    c.OrganizationProfileId == invoice.OrganizationProfileId)
                .AsEnumerable()
                .Where(c => string.Equals(c.Status, "Договор подписан", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.SignedAt)
                .FirstOrDefault();

            string contractBasis = signedContract != null && !string.IsNullOrWhiteSpace(signedContract.ContractNumber)
                ? $"Договор № {signedContract.ContractNumber} от {(signedContract.SignedAt.HasValue ? signedContract.SignedAt.Value.ToString("dd.MM.yyyy") : signedContract.GeneratedAt?.ToString("dd.MM.yyyy") ?? "—")}"
                : null;

            var items = db.InvoiceItems
                .AsNoTracking()
                .Where(x => x.InvoiceId == invoice.Id)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToList();

            if (items.Count == 0)
                throw new InvalidOperationException("В счете нет строк услуг.");

            decimal totalWithoutVat = items.Sum(x => x.AmountWithoutVat);
            decimal vatAmount = items.Sum(x => x.VatAmount);
            decimal totalWithVat = items.Sum(x => x.AmountWithVat);

            string clientKey = !string.IsNullOrWhiteSpace(client.Inn)
                ? client.Inn.Trim()
                : client.Id.ToString();

            string fileName = $"Счет_{clientKey}_{invoice.InvoiceDate:yyyyMMdd}_{invoice.Id}.docx";
            string tempPath = Path.Combine(Path.GetTempPath(), fileName);

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using var document = WordprocessingDocument.Create(
                tempPath,
                WordprocessingDocumentType.Document);

            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            Body body = mainPart.Document.Body!;

            AppendInvoiceOrganizationLogoIfExists(mainPart, body, organization);

            body.Append(CreateTopAccentLine());
            body.Append(CreateParagraph("СЧЁТ НА ОПЛАТУ", true, 18, "1F4E79", JustificationValues.Left, 0, 60));
            body.Append(CreateParagraph($"№ {invoice.InvoiceNumber}", true, 28, "111827", JustificationValues.Left, 0, 120));

            body.Append(CreateInvoiceInfoTable(invoice));
            body.Append(CreateSpacerParagraph());

            body.Append(CreatePartiesTable(organization, client));
            body.Append(CreateSpacerParagraph());

            body.Append(CreateParagraph("Состав счёта", true, 16, "111827", JustificationValues.Left, 120, 80));
            body.Append(CreateItemsTable(items));

            body.Append(CreateSpacerParagraph());
            body.Append(CreateTotalsTable(totalWithoutVat, vatAmount, totalWithVat));

            body.Append(CreateSpacerParagraph());
            body.Append(CreateParagraph(
                $"Сумма к оплате прописью: {CapitalizeFirst(MoneyToWords(totalWithVat))}",
                false,
                11,
                "374151",
                JustificationValues.Left,
                80,
                80));

            if (!string.IsNullOrWhiteSpace(contractBasis))
            {
                body.Append(CreateParagraph(
                    $"Основание: {contractBasis}",
                    false,
                    11,
                    "374151",
                    JustificationValues.Left,
                    80,
                    80));
            }

            if (!string.IsNullOrWhiteSpace(invoice.Comment))
            {
                body.Append(CreateParagraph(
                    $"Комментарий: {invoice.Comment}",
                    false,
                    10,
                    "4B5563",
                    JustificationValues.Left,
                    80,
                    80));
            }

            body.Append(CreateSpacerParagraph());
            body.Append(CreateSignatureBlock(organization));

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

        private static Paragraph CreateTopAccentLine()
        {
            var paragraph = new Paragraph();

            paragraph.Append(new ParagraphProperties(
                new SpacingBetweenLines { Before = "0", After = "180" },
                new ParagraphBorders(
                    new BottomBorder
                    {
                        Val = BorderValues.Single,
                        Color = "1F4E79",
                        Size = 24,
                        Space = 1
                    })));

            paragraph.Append(new Run(new Text("")));

            return paragraph;
        }
        private static OrganizationProfile ResolveOrganization(AppDbContext db, Invoice invoice)
        {
            int? organizationId = invoice.OrganizationProfileId;

            if (!organizationId.HasValue)
            {
                organizationId = ActiveOrganizationService.CurrentOrganizationId;
            }

            if (organizationId.HasValue)
            {
                var organization = db.OrganizationProfiles
                    .AsNoTracking()
                    .FirstOrDefault(x => x.Id == organizationId.Value && x.IsActive);

                if (organization != null)
                    return organization;
            }

            return ActiveOrganizationService.GetRequired();
        }
        private static Table CreateInvoiceInfoTable(Invoice invoice)
        {
            var table = CreateBaseTable();

            var row = new TableRow(
                CreateInfoCell(
                    "Дата счёта",
                    new[] { invoice.InvoiceDate.ToString("dd.MM.yyyy") },
                    "F3F6FA",
                    "33%"),

                CreateInfoCell(
                    "Срок оплаты",
                    new[] { invoice.DueDate.HasValue ? invoice.DueDate.Value.ToString("dd.MM.yyyy") : "—" },
                    "F3F6FA",
                    "33%"),

                CreateInfoCell(
                    "Период",
                    new[] { string.IsNullOrWhiteSpace(invoice.PeriodText) ? "—" : invoice.PeriodText },
                    "F3F6FA",
                    "34%"));

            table.Append(row);
            return table;
        }

        private static Table CreatePartiesTable(OrganizationProfile organization, ClientInfo client)
        {
            var table = CreateBaseTable();

            var sellerLines = new List<string>
    {
        string.IsNullOrWhiteSpace(organization.Name) ? "Организация" : organization.Name,
        $"ИНН: {EmptyDash(organization.Inn)}   КПП: {EmptyDash(organization.Kpp)}",
        $"ОГРН / ОГРНИП: {EmptyDash(organization.Ogrn)}",
        $"Адрес: {EmptyDash(organization.LegalAddress)}",
        $"Р/с: {EmptyDash(organization.SettlementAccount)}",
        $"Банк: {EmptyDash(organization.BankName)}",
        $"БИК: {EmptyDash(organization.BankBic)}   К/с: {EmptyDash(organization.CorrespondentAccount)}",
        $"E-mail: {EmptyDash(organization.Email)}   Тел.: {EmptyDash(organization.Phone)}"
    };

            if (!string.IsNullOrWhiteSpace(organization.DirectorName))
            {
                sellerLines.Add($"{EmptyDash(organization.DirectorPosition)}: {organization.DirectorName}");
            }

            var clientLines = new List<string>
    {
        GetClientDisplayName(client),
        string.IsNullOrWhiteSpace(client.Inn) ? "ИНН: —" : $"ИНН: {client.Inn}",
        string.IsNullOrWhiteSpace(client.Ogrn) ? "ОГРН / ОГРНИП: —" : $"ОГРН / ОГРНИП: {client.Ogrn}",
        string.IsNullOrWhiteSpace(client.Address) ? "Адрес: —" : $"Адрес: {client.Address}"
    };

            var row = new TableRow(
                CreateInfoCell("Исполнитель", sellerLines, "F3F6FA", "50%"),
                CreateInfoCell("Плательщик", clientLines, "FFFFFF", "50%"));

            table.Append(row);
            return table;
        }



        private static Table CreateItemsTable(List<InvoiceItem> items)
        {
            var table = CreateBaseTable();

            table.Append(CreateItemsHeaderRow());

            int index = 1;

            foreach (var item in items)
            {
                var row = new TableRow(
                    CreateCell(index.ToString(), false, "FFFFFF", "500", JustificationValues.Center),
                    CreateCell(item.ServiceName, false, "FFFFFF", "4300", JustificationValues.Left),
                    CreateCell(item.Quantity.ToString("N2", RuCulture), false, "FFFFFF", "800", JustificationValues.Right),
                    CreateCell(item.Unit, false, "FFFFFF", "700", JustificationValues.Center),
                    CreateCell(FormatMoney(item.UnitPrice), false, "FFFFFF", "1200", JustificationValues.Right),
                    CreateCell($"{item.VatRate:N2}%", false, "FFFFFF", "800", JustificationValues.Right),
                    CreateCell(FormatMoney(item.AmountWithVat), true, "FFFFFF", "1300", JustificationValues.Right)
                );

                table.Append(row);
                index++;
            }

            return table;
        }
        private static void AppendOrganizationLogoIfExists(
    MainDocumentPart mainPart,
    Body body,
    OrganizationProfile organization)
        {
            if (string.IsNullOrWhiteSpace(organization.LogoRelativePath))
                return;

            string logoFullPath = OrganizationLogoStorageService.GetLogoFullPath(organization.LogoRelativePath);

            if (string.IsNullOrWhiteSpace(logoFullPath) || !File.Exists(logoFullPath))
                return;

            var paragraph = CreateInvoiceLogoParagraph(mainPart, logoFullPath);

            if (paragraph != null)
                body.Append(paragraph);
        }
        private static TableRow CreateItemsHeaderRow()
        {
            return new TableRow(
                CreateCell("№", true, "EAF1F8", "500", JustificationValues.Center, "1F4E79"),
                CreateCell("Наименование услуги", true, "EAF1F8", "4300", JustificationValues.Left, "1F4E79"),
                CreateCell("Кол-во", true, "EAF1F8", "800", JustificationValues.Right, "1F4E79"),
                CreateCell("Ед.", true, "EAF1F8", "700", JustificationValues.Center, "1F4E79"),
                CreateCell("Цена", true, "EAF1F8", "1200", JustificationValues.Right, "1F4E79"),
                CreateCell("НДС", true, "EAF1F8", "800", JustificationValues.Right, "1F4E79"),
                CreateCell("Сумма", true, "EAF1F8", "1300", JustificationValues.Right, "1F4E79")
            );
        }

        private static void AppendInvoiceOrganizationLogoIfExists(
      MainDocumentPart mainPart,
      Body body,
      OrganizationProfile organization)
        {
            if (string.IsNullOrWhiteSpace(organization.LogoRelativePath))
                return;

            string logoFullPath = OrganizationLogoStorageService.GetLogoFullPath(organization.LogoRelativePath);

            if (string.IsNullOrWhiteSpace(logoFullPath) || !File.Exists(logoFullPath))
                return;

            var paragraph = CreateInvoiceLogoParagraph(mainPart, logoFullPath);

            if (paragraph != null)
                body.Append(paragraph);
        }

        private static Paragraph? CreateInvoiceLogoParagraph(MainDocumentPart mainPart, string imagePath)
        {
            string extension = Path.GetExtension(imagePath).ToLowerInvariant();

            var imagePartType = extension switch
            {
                ".png" => ImagePartType.Png,
                ".jpg" => ImagePartType.Jpeg,
                ".jpeg" => ImagePartType.Jpeg,
                _ => ImagePartType.Png
            };

            ImagePart imagePart = mainPart.AddImagePart(imagePartType);

            using (var stream = File.OpenRead(imagePath))
            {
                imagePart.FeedData(stream);
            }

            string relationshipId = mainPart.GetIdOfPart(imagePart);

            const long widthEmu = 1900000L;
            const long heightEmu = 760000L;

            var drawing =
                new Drawing(
                    new DW.Inline(
                        new DW.Extent
                        {
                            Cx = widthEmu,
                            Cy = heightEmu
                        },
                        new DW.EffectExtent
                        {
                            LeftEdge = 0L,
                            TopEdge = 0L,
                            RightEdge = 0L,
                            BottomEdge = 0L
                        },
                        new DW.DocProperties
                        {
                            Id = 1U,
                            Name = "Organization logo"
                        },
                        new DW.NonVisualGraphicFrameDrawingProperties(
                            new A.GraphicFrameLocks
                            {
                                NoChangeAspect = true
                            }),
                        new A.Graphic(
                            new A.GraphicData(
                                new PIC.Picture(
                                    new PIC.NonVisualPictureProperties(
                                        new PIC.NonVisualDrawingProperties
                                        {
                                            Id = 0U,
                                            Name = Path.GetFileName(imagePath)
                                        },
                                        new PIC.NonVisualPictureDrawingProperties()),
                                    new PIC.BlipFill(
                                        new A.Blip
                                        {
                                            Embed = relationshipId
                                        },
                                        new A.Stretch(
                                            new A.FillRectangle())),
                                    new PIC.ShapeProperties(
                                        new A.Transform2D(
                                            new A.Offset
                                            {
                                                X = 0L,
                                                Y = 0L
                                            },
                                            new A.Extents
                                            {
                                                Cx = widthEmu,
                                                Cy = heightEmu
                                            }),
                                        new A.PresetGeometry(
                                            new A.AdjustValueList())
                                        {
                                            Preset = A.ShapeTypeValues.Rectangle
                                        })))
                            {
                                Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
                            }))
                    {
                        DistanceFromTop = 0U,
                        DistanceFromBottom = 0U,
                        DistanceFromLeft = 0U,
                        DistanceFromRight = 0U
                    });

            var paragraph = new Paragraph();

            paragraph.Append(new ParagraphProperties(
                new Justification { Val = JustificationValues.Left },
                new SpacingBetweenLines
                {
                    Before = "0",
                    After = "120"
                }));

            paragraph.Append(new Run(drawing));

            return paragraph;
        }

        private static Table CreateTotalsTable(decimal totalWithoutVat, decimal vatAmount, decimal totalWithVat)
        {
            var table = new Table();

            table.Append(new TableProperties(
                new TableWidth { Width = "4200", Type = TableWidthUnitValues.Dxa },
                new TableJustification { Val = TableRowAlignmentValues.Right },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Color = "CBD5E1", Size = 4 },
                    new BottomBorder { Val = BorderValues.Single, Color = "CBD5E1", Size = 4 },
                    new LeftBorder { Val = BorderValues.Single, Color = "CBD5E1", Size = 4 },
                    new RightBorder { Val = BorderValues.Single, Color = "CBD5E1", Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Color = "E5E7EB", Size = 4 },
                    new InsideVerticalBorder { Val = BorderValues.Single, Color = "E5E7EB", Size = 4 })));

            table.Append(CreateTotalRow("Итого без НДС", FormatMoney(totalWithoutVat), false));
            table.Append(CreateTotalRow("НДС", FormatMoney(vatAmount), false));
            table.Append(CreateTotalRow("ИТОГО К ОПЛАТЕ", FormatMoney(totalWithVat), true));

            return table;
        }

        private static TableRow CreateTotalRow(string label, string value, bool isFinal)
        {
            string fill = isFinal ? "1F4E79" : "F8FAFC";
            string color = isFinal ? "FFFFFF" : "111827";

            return new TableRow(
                CreateCell(label, true, fill, "2300", JustificationValues.Left, color),
                CreateCell(value, true, fill, "1900", JustificationValues.Right, color)
            );
        }

        private static Table CreateSignatureBlock(OrganizationProfile organization)
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

            string directorLine = string.IsNullOrWhiteSpace(organization.DirectorName)
                ? "____________________"
                : $"____________________ / {organization.DirectorName.Trim()} /";

            string directorLabel = string.IsNullOrWhiteSpace(organization.DirectorPosition)
                ? "Руководитель"
                : organization.DirectorPosition.Trim();

            var row = new TableRow(
                CreateSignatureCell(directorLabel, directorLine),
                CreateSignatureCell("М.П.", "____________________"));

            table.Append(row);
            return table;
        }

        private static TableCell CreateSignatureCell(string label, string line)
        {
            var cell = new TableCell();

            cell.Append(new TableCellProperties(
                new TableCellWidth { Width = "50%", Type = TableWidthUnitValues.Pct }));

            cell.Append(CreateParagraph(label, false, 10, "6B7280", JustificationValues.Left, 120, 40));
            cell.Append(CreateParagraph(line, false, 12, "111827", JustificationValues.Left, 80, 40));

            return cell;
        }

        private static Table CreateBaseTable()
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

            return table;
        }

        private static TableCell CreateInfoCell(string title, IEnumerable<string> lines, string fill, string width)
        {
            var cell = new TableCell();

            cell.Append(new TableCellProperties(
                new TableCellWidth { Width = width, Type = TableWidthUnitValues.Pct },
                new Shading { Fill = fill, Val = ShadingPatternValues.Clear, Color = "auto" },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Top }));

            cell.Append(CreateParagraph(title, true, 10, "1F4E79", JustificationValues.Left, 60, 40));

            foreach (string rawLine in lines)
            {
                string line = string.IsNullOrWhiteSpace(rawLine) ? "—" : rawLine.Trim();
                cell.Append(CreateParagraph(line, false, 10, "111827", JustificationValues.Left, 0, 40));
            }

            return cell;
        }

        private static TableCell CreateCell(
    string text,
    bool bold = false,
    string fill = "FFFFFF",
    string width = "1200",
    JustificationValues? alignment = null,
    string color = "111827")
        {
            var cell = new TableCell();

            cell.Append(new TableCellProperties(
                new TableCellWidth { Width = width, Type = TableWidthUnitValues.Dxa },
                new Shading { Fill = fill, Val = ShadingPatternValues.Clear, Color = "auto" },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));

            cell.Append(CreateParagraph(text, bold, 9, color, alignment ?? JustificationValues.Left, 40, 40));

            return cell;
        }

        private static Paragraph CreateSpacerParagraph()
        {
            return CreateParagraph(" ", false, 6, "111827", JustificationValues.Left, 60, 60);
        }

        private static Paragraph CreateParagraph(
    string text,
    bool bold = false,
    int fontSize = 11,
    string color = "111827",
    JustificationValues? alignment = null,
    int before = 0,
    int after = 80)
        {
            var paragraph = new Paragraph();

            paragraph.Append(new ParagraphProperties(
                new Justification { Val = alignment ?? JustificationValues.Left },
                new SpacingBetweenLines
                {
                    Before = before.ToString(),
                    After = after.ToString()
                }));

            var runProperties = new RunProperties(
                new RunFonts
                {
                    Ascii = "Aptos",
                    HighAnsi = "Aptos",
                    EastAsia = "Aptos",
                    ComplexScript = "Aptos"
                },
                new FontSize { Val = (fontSize * 2).ToString() },
                new Color { Val = color });

            if (bold)
                runProperties.Append(new Bold());

            var run = new Run();
            run.Append(runProperties);
            run.Append(new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve });

            paragraph.Append(run);

            return paragraph;
        }

        private static string GetClientDisplayName(ClientInfo client)
        {
            return string.IsNullOrWhiteSpace(client.Name)
                ? "Клиент без названия"
                : client.Name.Trim();
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

        private static string MoneyToWords(decimal amount)
        {
            amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

            long rubles = (long)Math.Floor(amount);
            int kopecks = (int)((amount - rubles) * 100m);

            return $"{NumberToWords(rubles)} {Decline(rubles, "рубль", "рубля", "рублей")} {kopecks:00} {Decline(kopecks, "копейка", "копейки", "копеек")}";
        }

        private static string NumberToWords(long number)
        {
            if (number == 0)
                return "ноль";

            var parts = new List<string>();

            string[][] scales =
            {
                new[] { "", "", "" },
                new[] { "тысяча", "тысячи", "тысяч" },
                new[] { "миллион", "миллиона", "миллионов" },
                new[] { "миллиард", "миллиарда", "миллиардов" }
            };

            int scaleIndex = 0;

            while (number > 0 && scaleIndex < scales.Length)
            {
                int group = (int)(number % 1000);

                if (group > 0)
                {
                    bool feminine = scaleIndex == 1;
                    string groupWords = ThreeDigitsToWords(group, feminine);
                    string scaleWord = scaleIndex == 0
                        ? ""
                        : Decline(group, scales[scaleIndex][0], scales[scaleIndex][1], scales[scaleIndex][2]);

                    parts.Insert(0, $"{groupWords} {scaleWord}".Trim());
                }

                number /= 1000;
                scaleIndex++;
            }

            return string.Join(" ", parts);
        }

        private static string ThreeDigitsToWords(int number, bool feminine)
        {
            string[] hundreds =
            {
                "", "сто", "двести", "триста", "четыреста",
                "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот"
            };

            string[] tens =
            {
                "", "десять", "двадцать", "тридцать", "сорок",
                "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто"
            };

            string[] teens =
            {
                "десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать",
                "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать"
            };

            string[] unitsMale =
            {
                "", "один", "два", "три", "четыре", "пять",
                "шесть", "семь", "восемь", "девять"
            };

            string[] unitsFemale =
            {
                "", "одна", "две", "три", "четыре", "пять",
                "шесть", "семь", "восемь", "девять"
            };

            var words = new List<string>();

            int h = number / 100;
            int t = (number / 10) % 10;
            int u = number % 10;

            if (h > 0)
                words.Add(hundreds[h]);

            if (t == 1)
            {
                words.Add(teens[u]);
            }
            else
            {
                if (t > 0)
                    words.Add(tens[t]);

                if (u > 0)
                    words.Add(feminine ? unitsFemale[u] : unitsMale[u]);
            }

            return string.Join(" ", words);
        }

        private static string Decline(long number, string one, string two, string five)
        {
            long n = Math.Abs(number) % 100;
            long n1 = n % 10;

            if (n > 10 && n < 20)
                return five;

            if (n1 > 1 && n1 < 5)
                return two;

            if (n1 == 1)
                return one;

            return five;
        }

        private static string CapitalizeFirst(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            return char.ToUpper(text[0], RuCulture) + text[1..];
        }
    }
}