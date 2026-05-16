using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

            string? contractBasis = null;
            if (signedContract != null)
            {
                // Очищаем номер договора — в базе может лежать имя файла
                string rawNumber = signedContract.ContractNumber ?? "";
                string cleanNumber = ExtractCleanContractNumber(
                    rawNumber, signedContract.ClientInfoId, signedContract.GeneratedAt);
                string contractDate = signedContract.SignedAt.HasValue
                    ? signedContract.SignedAt.Value.ToString("dd.MM.yyyy")
                    : signedContract.GeneratedAt?.ToString("dd.MM.yyyy") ?? "—";

                if (cleanNumber != "—")
                    contractBasis = $"Договор № {cleanNumber} от {contractDate}";
            }

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

            // QR вычисляем заранее — он нужен в шапке
            byte[]? qrBytesHeader = TryGeneratePaymentQr(organization, totalWithVat, invoice.InvoiceNumber);

            body.Append(CreateTopAccentLine());
            body.Append(CreateHeaderWithQr(mainPart, organization, invoice, qrBytesHeader));

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

            // Примечание — отдельный заметный блок
            if (!string.IsNullOrWhiteSpace(invoice.Comment))
            {
                body.Append(CreateSpacerParagraph());
                body.Append(CreateParagraph(
                    "Примечание",
                    true,
                    11,
                    "374151",
                    JustificationValues.Left,
                    80,
                    40));
                body.Append(CreateParagraph(
                    invoice.Comment,
                    false,
                    11,
                    "374151",
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



        private static Paragraph AppendLogoToParagraph(MainDocumentPart mainPart, string logoPath)
        {
            byte[] imgBytes = File.ReadAllBytes(logoPath);
            string ext = Path.GetExtension(logoPath).ToLowerInvariant();
            var imgType = ext switch
            {
                ".png" => ImagePartType.Png,
                ".jpg" => ImagePartType.Jpeg,
                ".jpeg" => ImagePartType.Jpeg,
                _ => ImagePartType.Png
            };
            string relId = "logo_hdr_" + Guid.NewGuid().ToString("N")[..6];
            using var ms = new MemoryStream(imgBytes);
            var imgPart = mainPart.AddImagePart(imgType, relId);
            imgPart.FeedData(ms);

            long cx = 1800000L; // ~2 см
            long cy = 900000L;

            var drawing = new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = cx, Cy = cy },
                    new DW.EffectExtent { LeftEdge=0L, TopEdge=0L, RightEdge=0L, BottomEdge=0L },
                    new DW.DocProperties { Id = 10U, Name = "logo_hdr" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = "logo_hdr" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X=0L, Y=0L },
                                    new A.Extents { Cx=cx, Cy=cy }),
                                new A.PresetGeometry(new A.AdjustValueList())
                                { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                { DistanceFromTop=0U, DistanceFromBottom=0U, DistanceFromLeft=0U, DistanceFromRight=0U });

            var para = new Paragraph();
            para.Append(new Run(drawing));
            return para;
        }
        /// <summary>
        /// Двухколоночная шапка: слева — логотип+заголовок, справа — QR для оплаты.
        /// </summary>
        private static Table CreateHeaderWithQr(
            MainDocumentPart mainPart,
            OrganizationProfile org,
            Invoice invoice,
            byte[]? qrBytes)
        {
            var table = new Table();
            table.Append(new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None }),
                new TableWidth { Width = "10466", Type = TableWidthUnitValues.Dxa },
                new TableJustification { Val = TableRowAlignmentValues.Left }));

            var row = new TableRow();

            // Левая колонка: логотип + СЧЁТ НА ОПЛАТУ + номер
            var leftCell = new TableCell();
            leftCell.Append(new TableCellProperties(
                new TableCellWidth { Width = "7766", Type = TableWidthUnitValues.Dxa },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Top }));

            // Логотип если есть
            string? logoPath = null;
            if (!string.IsNullOrWhiteSpace(org.LogoRelativePath))
            {
                string full = Path.Combine(AppPaths.ClientFilesFolder, org.LogoRelativePath);
                if (File.Exists(full)) logoPath = full;
            }
            if (logoPath != null)
            {
                try
                {
                    var logoPara = AppendLogoToParagraph(mainPart, logoPath);
                    leftCell.Append(logoPara);
                }
                catch (Exception _ex)
            {
                AppLogger.LogError("InvoiceWordService.CreateHeaderWithQr", _ex);
            }
            }

            leftCell.Append(CreateParagraph(
                "СЧЁТ НА ОПЛАТУ", true, 18, "1F4E79", JustificationValues.Left, 0, 30));
            leftCell.Append(CreateParagraph(
                $"№ {invoice.InvoiceNumber}", true, 28, "111827", JustificationValues.Left, 0, 60));

            row.Append(leftCell);

            // Правая колонка: QR-код
            var rightCell = new TableCell();
            rightCell.Append(new TableCellProperties(
                new TableCellWidth { Width = "2700", Type = TableWidthUnitValues.Dxa },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Top }));

            if (qrBytes != null && qrBytes.Length > 0)
            {
                try
                {
                    rightCell.Append(CreateQrImageParagraph(mainPart, qrBytes, 900000, 900000, JustificationValues.Right));
                    rightCell.Append(CreateParagraph(
                        "Сканируйте для оплаты",
                        false, 8, "6B7280", JustificationValues.Right, 0, 0));
                }
                catch
                {
                    rightCell.Append(CreateParagraph("", false, 8, "FFFFFF", JustificationValues.Left, 0, 0));
                }
            }
            else
            {
                rightCell.Append(CreateParagraph(
                    "[Заполните банковские реквизиты для QR]",
                    false, 8, "9CA3AF", JustificationValues.Center, 20, 0));
            }

            row.Append(rightCell);
            table.Append(row);
            return table;
        }

        /// <summary>
        /// Генерирует QR-код для оплаты по российскому стандарту (ST00012).
        /// Возвращает PNG-байты или null если не хватает реквизитов.
        /// </summary>
        private static byte[]? TryGeneratePaymentQr(
            OrganizationProfile org, decimal totalWithVat, string invoiceNumber)
        {
            // Для QR нужен расчётный счёт и БИК
            if (string.IsNullOrWhiteSpace(org.SettlementAccount) ||
                string.IsNullOrWhiteSpace(org.BankBic))
                return null;

            // Сумма в копейках, без разделителей
            long sumKopeks = (long)Math.Round(totalWithVat * 100m, 0);

            string name = string.IsNullOrWhiteSpace(org.Name)
                ? org.ShortName : org.Name;

            // Стандарт Банка России для QR-кода платёжного поручения
            string qrData = string.Join("|",
                "ST00012",
                $"Name={name}",
                $"PersonalAcc={org.SettlementAccount.Replace(" ", "")}",
                $"BankName={org.BankName}",
                $"BIC={org.BankBic.Replace(" ", "")}",
                $"CorrespAcc={org.CorrespondentAccount.Replace(" ", "")}",
                $"Sum={sumKopeks}",
                $"Purpose=Оплата счёта № {invoiceNumber}",
                $"PayeeINN={org.Inn.Replace(" ", "")}"
            );

            try
            {
                using var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.M);
                var qrCode = new PngByteQRCode(qrCodeData);
                return qrCode.GetGraphic(4); // 4 пикселя на модуль
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Блок подписей с QR-кодом справа.
        /// </summary>
        private static Table CreateSignatureWithQrBlock(
            MainDocumentPart mainPart, OrganizationProfile org, byte[]? qrBytes)
        {
            var table = new Table();

            table.Append(new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None }),
                new TableWidth { Width = "9360", Type = TableWidthUnitValues.Dxa }));

            var row = new TableRow();

            // Левая колонка — подписи
            var sigCell = new TableCell();
            sigCell.Append(new TableCellProperties(
                new TableCellWidth { Width = "6500", Type = TableWidthUnitValues.Dxa }));

            string directorPos = string.IsNullOrWhiteSpace(org.DirectorPosition)
                ? "Директор" : org.DirectorPosition;
            string directorName = org.DirectorName ?? "";

            sigCell.Append(CreateParagraph(
                $"{directorPos} _________________ / {directorName} /",
                false, 10, "374151", JustificationValues.Left, 120, 40));
            sigCell.Append(CreateParagraph(
                "Главный бухгалтер _________________ /________________________/",
                false, 10, "374151", JustificationValues.Left, 60, 40));
            sigCell.Append(CreateParagraph(
                "М.П.",
                false, 10, "374151", JustificationValues.Left, 80, 0));

            row.Append(sigCell);

            // Правая колонка — QR
            var qrCell = new TableCell();
            qrCell.Append(new TableCellProperties(
                new TableCellWidth { Width = "2860", Type = TableWidthUnitValues.Dxa }));

            if (qrBytes != null && qrBytes.Length > 0)
            {
                try
                {
                    var imgPara = CreateQrImageParagraph(mainPart, qrBytes, 900000, 900000);
                    qrCell.Append(imgPara);
                    qrCell.Append(CreateParagraph(
                        "Сканируйте для оплаты",
                        false, 8, "6B7280", JustificationValues.Center, 0, 0));
                }
                catch
                {
                    qrCell.Append(CreateParagraph("QR: реквизиты не заполнены", false, 8, "9CA3AF", JustificationValues.Center, 0, 0));
                }
            }
            else
            {
                qrCell.Append(CreateParagraph(
                    "[Заполните банковские реквизиты] [для отображения QR-кода]",
                    false, 8, "9CA3AF", JustificationValues.Center, 60, 0));
            }

            row.Append(qrCell);
            table.Append(row);

            return table;
        }

        private static Paragraph CreateQrImageParagraph(
            MainDocumentPart mainPart, byte[] imageBytes, long widthEmu, long heightEmu)
            => CreateQrImageParagraph(mainPart, imageBytes, widthEmu, heightEmu, JustificationValues.Center);

        private static Paragraph CreateQrImageParagraph(
            MainDocumentPart mainPart, byte[] imageBytes, long widthEmu, long heightEmu,
            JustificationValues justification)
        {
            string relationshipId = "qrId" + Guid.NewGuid().ToString("N")[..8];

            using var ms = new MemoryStream(imageBytes);
            var imgPart = mainPart.AddImagePart(ImagePartType.Png, relationshipId);
            imgPart.FeedData(ms);

            var element = new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                    new DW.EffectExtent
                    {
                        LeftEdge = 0L, TopEdge = 0L,
                        RightEdge = 0L, BottomEdge = 0L
                    },
                    new DW.DocProperties { Id = 2U, Name = "QR-код оплаты" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties
                                    {
                                        Id = 0U,
                                        Name = "qr.png"
                                    },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = relationshipId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                    new A.PresetGeometry(
                                        new A.AdjustValueList())
                                    { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                {
                    DistanceFromTop = 0U,
                    DistanceFromBottom = 0U,
                    DistanceFromLeft = 0U,
                    DistanceFromRight = 0U
                });

            var paragraph = new Paragraph();
            paragraph.Append(new ParagraphProperties(
                new Justification { Val = justification }));
            paragraph.Append(new Run(element));
            return paragraph;
        }


        /// <summary>Извлекает чистый номер договора из поля которое может содержать имя файла.</summary>
        private static string ExtractCleanContractNumber(string? value, int clientId, DateTime? date)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (clientId > 0 && date.HasValue)
                    return $"{date.Value:yyMMdd}-{clientId:000}";
                return "—";
            }

            string text = Path.GetFileNameWithoutExtension(value.Trim());

            // Номер вида 260420-018
            var match = Regex.Match(text, @"\d{6}-\d{2,}");
            if (match.Success) return match.Value;

            // Дата + ID
            if (clientId > 0 && date.HasValue)
                return $"{date.Value:yyMMdd}-{clientId:000}";

            // Убираем префиксы
            text = Regex.Replace(text, @"^(Договор|ДОГОВОР)_?", "", RegexOptions.IgnoreCase).Trim('_', ' ', '-');
            return string.IsNullOrWhiteSpace(text) ? "—" : text;
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