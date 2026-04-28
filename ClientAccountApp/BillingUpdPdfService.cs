using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;

namespace ClientAccountApp.Services
{
    public class BillingUpdPdfService
    {
        private static readonly CultureInfo Ru = new CultureInfo("ru-RU");

        public string Generate(BillingUpdPdfData data, string outputPath)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Не указан путь сохранения УПД PDF.", nameof(outputPath));

            var folder = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            if (data.Lines == null)
                data.Lines = new List<BillingUpdPdfLine>();

            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(5);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(4.8f));

                    page.Content().Row(row =>
                    {
                        row.ConstantItem(56).Element(e => LeftStatusBlock(e, data));
                        row.ConstantItem(3);
                        row.RelativeItem().Column(column =>
                        {
                            column.Spacing(2.5f);

                            column.Item().Element(e => TopHeader(e, data));
                            column.Item().Element(e => GoodsTable(e, data));
                            column.Item().Element(e => SignatureBlock(e, data));
                        });
                    });
                });
            }).GeneratePdf(outputPath);

            return outputPath;
        }
        
        private static void LeftStatusBlock(IContainer container, BillingUpdPdfData data)
        {
            container
                .BorderRight(1.2f)
                .BorderColor(Colors.Black)
                .PaddingRight(4)
                .Column(column =>
                {
                    column.Spacing(6);

                    column.Item().Text("Универсальный\nпередаточный\nдокумент")
                        .FontSize(6)
                        .SemiBold()
                        .AlignCenter();

                    column.Item().Row(row =>
                    {
                        row.ConstantItem(32).Text("Статус:").FontSize(6);
                        row.RelativeItem()
                            .Border(1)
                            .Padding(2)
                            .AlignCenter()
                            .Text(string.IsNullOrWhiteSpace(data.Status) ? "1" : data.Status)
                            .Bold()
                            .FontSize(8);
                    });

                    column.Item().Text("1 – счет-фактура\nи передаточный\nдокумент (акт)\n2 – передаточный\nдокумент (акт)")
                        .FontSize(6);
                });
        }

        private static void TopHeader(IContainer container, BillingUpdPdfData data)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text($"Счет-фактура № {Dash(data.DocumentNumber)} от {DateLong(data.DocumentDate)}")
     .Bold()
     .FontSize(9.5f);

                    column.Item().Text("Исправление № --- от ---")
                        .FontSize(5);

                    column.Item().Element(e => HeaderLines(e, data));
                });

                row.ConstantItem(165).Column(column =>
                {
                    column.Item().AlignRight().Text("Приложение N 1 к постановлению Правительства Российской Федерации от 26.12.2011 №1137")
                        .FontSize(5);

                    column.Item().AlignRight().Text("(в ред. Постановления Правительства РФ от 23.01.2026 № 26)")
                        .FontSize(5);

                    column.Item().PaddingTop(2).AlignRight().Element(e => QrCodeBlock(e, data));
                });
            });
        }

        private static void HeaderLines(IContainer container, BillingUpdPdfData data)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(175);
                    columns.RelativeColumn();
                    columns.ConstantColumn(24);
                });

                AddHeaderLine(table, "Продавец", data.SellerName, "2", true);
                AddHeaderLine(table, "Адрес", data.SellerAddress, "2а", false);
                AddHeaderLine(table, "ИНН/КПП продавца", JoinInnKpp(data.SellerInn, data.SellerKpp), "2б", false);
                AddHeaderLine(table, "Грузоотправитель и его адрес:", data.ShipperName, "3", false);
                AddHeaderLine(table, "Грузополучатель и его адрес:", data.ConsigneeName, "4", false);
                AddHeaderLine(table, "К платежно-расчетному документу", "-", "5", false);
                AddHeaderLine(table, "Документ об отгрузке:", $"Универсальный передаточный документ, № {Dash(data.DocumentNumber)} от {DateShort(data.DocumentDate)}", "5а", false);
                AddHeaderLine(table, "К счету-фактуре, выставленному при получении оплаты, частичной оплаты или иных платежей", "-", "5б", false);
                AddHeaderLine(table, "Покупатель", data.BuyerName, "6", true);
                AddHeaderLine(table, "Адрес", data.BuyerAddress, "6а", false);
                AddHeaderLine(table, "ИНН/КПП покупателя", JoinInnKpp(data.BuyerInn, data.BuyerKpp), "6б", false);
                AddHeaderLine(table, "Валюта: наименование, код", "Российский рубль, код - 643", "7", false);
                AddHeaderLine(table, "Идентификатор государственного контракта, договора (соглашения) (при наличии)", Dash(data.ContractInfo), "8", false);
            });
        }

        private static void AddHeaderLine(TableDescriptor table, string label, string value, string number, bool boldValue)
        {
            table.Cell().Element(HeaderLabelCell).Text(label).FontSize(5);

            var text = table.Cell().Element(HeaderValueCell).Text(Dash(value)).FontSize(5);
            if (boldValue)
                text.Bold();

            table.Cell().Element(HeaderNumberCell).Text($"({number})").FontSize(5);
        }

        private static void GoodsTable(IContainer container, BillingUpdPdfData data)
        {
            var lines = data.Lines.Count == 0
                ? new List<BillingUpdPdfLine>
                {
                    new BillingUpdPdfLine
                    {
                        Name = "Услуги по договору",
                        UnitName = "усл.",
                        Quantity = 1,
                        PriceWithoutVat = 0,
                        VatRatePercent = data.DefaultVatRatePercent
                    }
                }
                : data.Lines;

            var totalWithoutVat = lines.Sum(x => x.AmountWithoutVat);
            var totalVat = lines.Sum(x => x.VatAmount);
            var totalWithVat = lines.Sum(x => x.AmountWithVat);

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(36);  // А
                    columns.ConstantColumn(18);  // 1
                    columns.RelativeColumn(2.8f); // 1а
                    columns.ConstantColumn(34);  // 1б
                    columns.ConstantColumn(20);  // 2
                    columns.ConstantColumn(36);  // 2а
                    columns.ConstantColumn(38);  // 3
                    columns.ConstantColumn(42);  // 4
                    columns.ConstantColumn(52);  // 5
                    columns.ConstantColumn(42);  // 6
                    columns.ConstantColumn(34);  // 7
                    columns.ConstantColumn(52);  // 8
                    columns.ConstantColumn(56);  // 9
                    columns.ConstantColumn(28);  // 10
                    columns.ConstantColumn(38);  // 10а
                    columns.ConstantColumn(58);  // 11
                });

                AddTableHeader(table);

                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];

                    AddCell(table, Dash(line.Code), true);
                    AddCell(table, (i + 1).ToString(), true);
                    AddCell(table, Dash(line.Name));
                    AddCell(table, Dash(line.ProductTypeCode), true);
                    AddCell(table, Dash(line.UnitCode), true);
                    AddCell(table, Dash(line.UnitName), true);
                    AddCell(table, Qty(line.Quantity), true);
                    AddCell(table, Money(line.PriceWithoutVat), false, true);
                    AddCell(table, Money(line.AmountWithoutVat), false, true);
                    AddCell(table, "без акциза", true);
                    AddCell(table, VatRate(line.VatRatePercent), true);
                    AddCell(table, Money(line.VatAmount), false, true);
                    AddCell(table, Money(line.AmountWithVat), false, true);
                    AddCell(table, Dash(line.CountryCode), true);
                    AddCell(table, Dash(line.CountryName), true);
                    AddCell(table, Dash(line.CustomsNumber), true);
                }

                table.Cell().ColumnSpan(8).Element(TotalCell).AlignRight().Text("Всего к оплате").Bold().FontSize(5);
                table.Cell().Element(TotalCell).AlignRight().Text(Money(totalWithoutVat)).FontSize(5);
                table.Cell().Element(TotalCell).AlignCenter().Text("Х").FontSize(5);
                table.Cell().Element(TotalCell).AlignCenter().Text("").FontSize(5);
                table.Cell().Element(TotalCell).AlignRight().Text(Money(totalVat)).FontSize(5);
                table.Cell().Element(TotalCell).AlignRight().Text(Money(totalWithVat)).FontSize(5);
                table.Cell().Element(TotalCell).Text("");
                table.Cell().Element(TotalCell).Text("");
                table.Cell().Element(TotalCell).Text("");
            });
        }

        private static void AddTableHeader(TableDescriptor table)
        {
            AddHeaderCell(table, "Код\nтовара/\nработ,\nуслуг");
            AddHeaderCell(table, "№\nп/\nп");
            AddHeaderCell(table, "Наименование товара\n(описание выполненных работ,\nоказанных услуг),\nимущественного права");
            AddHeaderCell(table, "Код вида\nтовара");
            AddHeaderCell(table, "Единица\nизмерения\n\nкод");
            AddHeaderCell(table, "условное\nобозначе-\nние (нацио-\nнальное)");
            AddHeaderCell(table, "Количество\n(объем)");
            AddHeaderCell(table, "Цена\n(тариф)\nза единицу\nизмерения");
            AddHeaderCell(table, "Стоимость\nтоваров\n(работ,услуг),\nимущественных\nправ без\nналога - всего");
            AddHeaderCell(table, "В том\nчисле\nсумма\nакциза");
            AddHeaderCell(table, "Налого-\nвая\nставка");
            AddHeaderCell(table, "Сумма налога,\nпредъявляемая\nпокупателю");
            AddHeaderCell(table, "Стоимость\nтоваров\n(работ, услуг),\nимущественных\nправ с\nналогом - всего");
            AddHeaderCell(table, "Страна\nпроисхож-\nдения товара\n\nЦифро-\nвой код");
            AddHeaderCell(table, "Краткое\nнаимено-\nвание");
            AddHeaderCell(table, "Регистрационный\nномер декларации на\nтовары или\nрегистрационный\nномер партии товара,\nподлежащего\nпрослеживаемости");

            AddCodeCell(table, "А");
            AddCodeCell(table, "1");
            AddCodeCell(table, "1а");
            AddCodeCell(table, "1б");
            AddCodeCell(table, "2");
            AddCodeCell(table, "2а");
            AddCodeCell(table, "3");
            AddCodeCell(table, "4");
            AddCodeCell(table, "5");
            AddCodeCell(table, "6");
            AddCodeCell(table, "7");
            AddCodeCell(table, "8");
            AddCodeCell(table, "9");
            AddCodeCell(table, "10");
            AddCodeCell(table, "10а");
            AddCodeCell(table, "11");
        }

        private static void SignatureBlock(IContainer container, BillingUpdPdfData data)
        {
            container.Column(column =>
            {
                column.Spacing(3);

                column.Item().Row(row =>
                {
                    row.ConstantItem(64)
                        .PaddingTop(4)
                        .Text("Документ\nсоставлен на\n___ листах")
                        .FontSize(5);

                    row.RelativeItem().Column(sig =>
                    {
                        sig.Item().Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Руководитель организации\nили иное уполномоченное лицо").FontSize(5);
                                c.Item().Element(e => SignatureLine(e, data.SellerDirector));
                            });

                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Главный бухгалтер\nили иное уполномоченное лицо").FontSize(5);
                                c.Item().Element(e => SignatureLine(e, data.SellerAccountant));
                            });
                        });

                        sig.Item().Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Индивидуальный предприниматель\nили иное уполномоченное лицо").FontSize(5);
                                c.Item().Element(e => SignatureLine(e, ""));
                            });

                            r.RelativeItem().Text("(основной государственный регистрационный номер индивидуального предпринимателя и дата присвоения такого номера)")
                                .FontSize(4)
                                .AlignCenter();
                        });
                    });
                });
                column.Item().Element(UpperSignatureSeparator);
                column.Item().Element(e => FullLine(e, "Основание передачи (сдачи)/получения приемки", "(договор; доверенность и др.)"));
                column.Item().Element(e => FullLine(e, "Данные о транспортировке и грузе", "(транспортная накладная, поручение экспедитору, экспедиторская/складская расписка и др./масса нетто/брутто груза, если не приведены ссылки на транспортные документы, содержащие эти сведения)"));

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(e => PartySide(e, true, data));
                    row.ConstantItem(0.7f).Background(Colors.Black);
                    row.RelativeItem().Element(e => PartySide(e, false, data));
                });
            });
        }
        private static void UpperSignatureSeparator(IContainer container)
        {
            container
                .PaddingTop(1)
                .BorderBottom(0.9f)
                .BorderColor(Colors.Black);
        }
        private static void PartySide(IContainer container, bool sellerSide, BillingUpdPdfData data)
        {
            container.PaddingHorizontal(4).Column(column =>
            {
                column.Spacing(3);

                column.Item().Text(sellerSide
                        ? "Товар (груз) передал/услуги, результаты работ, права сдал"
                        : "Товар (груз) получил/услуги, результаты работ, права принял")
                    .FontSize(5);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(SignatureSmallLine);
                    row.ConstantItem(8);
                    row.RelativeItem().Element(SignatureSmallLine);
                    row.ConstantItem(8);
                    row.RelativeItem().Element(SignatureSmallLine);
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().AlignCenter().Text("(должность)").FontSize(4);
                    row.ConstantItem(8);
                    row.RelativeItem().AlignCenter().Text("(подпись)").FontSize(4);
                    row.ConstantItem(8);
                    row.RelativeItem().AlignCenter().Text("(ф.и.о.)").FontSize(4);
                });

                if (sellerSide)
                {
                    column.Item().Text($"Дата отгрузки, передачи (сдачи)     « {data.DocumentDate:dd} » {MonthName(data.DocumentDate)} 20 {data.DocumentDate:yy} г.")
                        .FontSize(5);
                }
                else
                {
                    column.Item().Text("Дата получения (приемки)     « ___ » ____________ 20 ___ г.")
                        .FontSize(5);
                }

                column.Item().Element(e => FullLine(e, sellerSide ? "Иные сведения об отгрузке, передаче" : "Иные сведения о получении, приемке", sellerSide
                    ? "(ссылки на неотъемлемые приложения, сопутствующие документы, иные документы и т. п.)"
                    : "(информация о наличии/отсутствии претензии; ссылки на неотъемлемые приложения и другие документы и т. п.)"));

                column.Item().Text("Ответственный за правильность оформления факта хозяйственной жизни").FontSize(5);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(SignatureSmallLine);
                    row.ConstantItem(8);
                    row.RelativeItem().Element(SignatureSmallLine);
                    row.ConstantItem(8);
                    row.RelativeItem().Element(SignatureSmallLine);
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().AlignCenter().Text("(должность)").FontSize(4);
                    row.ConstantItem(8);
                    row.RelativeItem().AlignCenter().Text("(подпись)").FontSize(4);
                    row.ConstantItem(8);
                    row.RelativeItem().AlignCenter().Text("(ф.и.о.)").FontSize(4);
                });

                column.Item().Text(sellerSide
                        ? "Наименование экономического субъекта — составителя документа (в т. ч. комиссионера/агента)"
                        : "Наименование экономического субъекта — составителя документа")
                    .FontSize(5);

                column.Item().BorderBottom(0.7f).Text(sellerSide ? Dash(data.SellerName) : "").FontSize(5);

                column.Item().AlignCenter().Text("(может не заполняться при проставлении печати в М. П., может быть указан ИНН/КПП)")
                    .FontSize(4);

                column.Item().Text("М.П.").FontSize(5);
            });
        }

        private static void SignatureLine(IContainer container, string name)
        {
            container.Row(row =>
            {
                row.RelativeItem().BorderBottom(0.7f).Text("").FontSize(5);
                row.ConstantItem(8);
                row.RelativeItem().BorderBottom(0.7f).AlignCenter().Text(name ?? "").FontSize(5);
            });
        }

        private static void FullLine(IContainer container, string label, string hint)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(180).Text(label).FontSize(5);
                    row.RelativeItem().BorderBottom(0.7f).Text("").FontSize(5);
                });

                column.Item().AlignCenter().Text(hint).FontSize(4);
            });
        }

        private static void QrCodeBlock(IContainer container, BillingUpdPdfData data)
        {
            var payload = BuildQrPayload(data);

            using var generator = new QRCodeGenerator();
            using var qrData = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);

            var qrCode = new PngByteQRCode(qrData);
            byte[] qrBytes = qrCode.GetGraphic(6);

            container
    .Width(60)
    .Height(60)
    .Border(0.8f)
    .BorderColor(Colors.Black)
    .Padding(2)
    .Image(qrBytes, ImageScaling.FitArea);
        }

        private static string BuildQrPayload(BillingUpdPdfData data)
        {
            decimal total = data.Lines?.Sum(x => x.AmountWithVat) ?? 0m;
            decimal vat = data.Lines?.Sum(x => x.VatAmount) ?? 0m;

            return string.Join(";",
                "Документ=УПД",
                "Статус=" + Dash(data.Status),
                "Номер=" + Dash(data.DocumentNumber),
                "Дата=" + data.DocumentDate.ToString("dd.MM.yyyy", Ru),
                "Продавец=" + Dash(data.SellerName),
                "ИННПродавца=" + Dash(data.SellerInn),
                "Покупатель=" + Dash(data.BuyerName),
                "ИННПокупателя=" + Dash(data.BuyerInn),
                "Сумма=" + total.ToString("0.00", CultureInfo.InvariantCulture),
                "НДС=" + vat.ToString("0.00", CultureInfo.InvariantCulture));
        }

        private static void AddHeaderCell(TableDescriptor table, string text)
        {
            table.Cell().Element(TableHeaderCell).AlignCenter().AlignMiddle().Text(text).FontSize(5);
        }

        private static void AddCodeCell(TableDescriptor table, string text)
        {
            table.Cell().Element(TableCodeCell).AlignCenter().AlignMiddle().Text(text).FontSize(5).Bold();
        }

        private static void AddCell(TableDescriptor table, string text, bool center = false, bool right = false)
        {
            var cell = table.Cell().Element(TableBodyCell).AlignMiddle();

            if (right)
                cell.AlignRight().Text(text ?? "").FontSize(5);
            else if (center)
                cell.AlignCenter().Text(text ?? "").FontSize(5);
            else
                cell.Text(text ?? "").FontSize(5);
        }

        private static IContainer HeaderLabelCell(IContainer container)
        {
            return container.PaddingRight(2).MinHeight(6.5f);
        }

        private static IContainer HeaderValueCell(IContainer container)
        {
            return container.BorderBottom(0.7f).PaddingLeft(2).MinHeight(6.5f);
        }

        private static IContainer HeaderNumberCell(IContainer container)
        {
            return container.AlignRight().AlignMiddle().MinHeight(6.5f);
        }

        private static IContainer TableHeaderCell(IContainer container)
        {
            return container.Border(0.7f).BorderColor(Colors.Black).Padding(1).MinHeight(38);
        }

        private static IContainer TableCodeCell(IContainer container)
        {
            return container.Border(0.7f).BorderColor(Colors.Black).Padding(1).MinHeight(7);
        }

        private static IContainer TableBodyCell(IContainer container)
        {
            return container.Border(0.7f).BorderColor(Colors.Black).Padding(1).MinHeight(8.5f);
        }

        private static IContainer TotalCell(IContainer container)
        {
            return container
                .Border(0.7f)
                .BorderColor(Colors.Black)
                .Padding(1)
                .MinHeight(8.5f);
        }

        private static IContainer SignatureSmallLine(IContainer container)
        {
            return container.BorderBottom(0.7f).MinHeight(9);
        }

        private static string JoinInnKpp(string inn, string kpp)
        {
            inn = inn ?? "";
            kpp = kpp ?? "";

            if (string.IsNullOrWhiteSpace(inn) && string.IsNullOrWhiteSpace(kpp))
                return "";

            if (string.IsNullOrWhiteSpace(kpp))
                return inn;

            return inn + "/" + kpp;
        }

        private static string Dash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string DateLong(DateTime date)
        {
            return date.ToString("d MMMM yyyy 'г.'", Ru);
        }

        private static string DateShort(DateTime date)
        {
            return date.ToString("dd.MM.yyyy", Ru);
        }

        private static string MonthName(DateTime date)
        {
            return Ru.DateTimeFormat.GetMonthName(date.Month);
        }

        private static string Money(decimal value)
        {
            return value.ToString("N2", Ru);
        }

        private static string Qty(decimal value)
        {
            return value.ToString("0.###", Ru);
        }

        private static string VatRate(decimal rate)
        {
            if (rate <= 0)
                return "без НДС";

            return rate.ToString("0.##", Ru) + "%";
        }
    }

    public class BillingUpdPdfData
    {
        public string Status { get; set; } = "1";

        public string DocumentNumber { get; set; } = "";
        public DateTime DocumentDate { get; set; } = DateTime.Today;

        public string SellerName { get; set; } = "";
        public string SellerInn { get; set; } = "";
        public string SellerKpp { get; set; } = "";
        public string SellerAddress { get; set; } = "";
        public string SellerDirector { get; set; } = "";
        public string SellerAccountant { get; set; } = "";

        public string BuyerName { get; set; } = "";
        public string BuyerInn { get; set; } = "";
        public string BuyerKpp { get; set; } = "";
        public string BuyerAddress { get; set; } = "";

        public string ShipperName { get; set; } = "-";
        public string ConsigneeName { get; set; } = "-";
        public string ContractInfo { get; set; } = "";

        public decimal DefaultVatRatePercent { get; set; } = 22;

        public List<BillingUpdPdfLine> Lines { get; set; } = new List<BillingUpdPdfLine>();
    }

    public class BillingUpdPdfLine
    {
        public string Code { get; set; } = "-";
        public string Name { get; set; } = "";
        public string ProductTypeCode { get; set; } = "-";
        public string UnitCode { get; set; } = "-";
        public string UnitName { get; set; } = "усл.";

        public decimal Quantity { get; set; } = 1;
        public decimal PriceWithoutVat { get; set; }
        public decimal VatRatePercent { get; set; } = 22;

        public string CountryCode { get; set; } = "-";
        public string CountryName { get; set; } = "-";
        public string CustomsNumber { get; set; } = "-";

        public decimal AmountWithoutVat
        {
            get { return Math.Round(Quantity * PriceWithoutVat, 2); }
        }

        public decimal VatAmount
        {
            get { return Math.Round(AmountWithoutVat * VatRatePercent / 100m, 2); }
        }

        public decimal AmountWithVat
        {
            get { return AmountWithoutVat + VatAmount; }
        }
    }
}