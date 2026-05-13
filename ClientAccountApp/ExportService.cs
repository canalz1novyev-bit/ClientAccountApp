using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;

namespace ClientAccountApp
{
    public static class ExportService
    {
        private const string Navy = "1F4E79";
        private const string NavyDark = "17324D";
        private const string Gold = "C99A2E";
        private const string LightBlue = "EEF5FB";
        private const string LightGray = "F7F9FC";
        private const string Border = "D8E0EA";
        private const string TextDark = "111827";
        private const string TextMuted = "5B677A";
        private const string White = "FFFFFF";

        private const string TableWidth = "10000";
        private const string LabelColumnWidth = "2450";
        private const string ValueColumnWidth = "7550";

        public static string GetExportFolder()
        {
            string exportFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ClientAccountApp",
                "Exports");

            Directory.CreateDirectory(exportFolder);
            return exportFolder;
        }

        public static string ExportClientCardToWord(int clientId)
        {
            using var db = new AppDbContext();

            var client = db.Clients
                .AsNoTracking()
                .FirstOrDefault(c => c.Id == clientId);

            if (client == null)
                throw new InvalidOperationException("Клиент не найден.");

            var bankAccounts = db.BankAccounts
                .AsNoTracking()
                .Where(a => a.ClientInfoId == clientId)
                .OrderBy(a => a.BankName)
                .ThenBy(a => a.AccountNumber)
                .ToList();

            string safeClientName = MakeSafeFileName(client.Name);
            string fileName = $"Карточка_контрагента_{safeClientName}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
            string exportPath = Path.Combine(GetExportFolder(), fileName);

            using var wordDocument = WordprocessingDocument.Create(exportPath, WordprocessingDocumentType.Document);
            MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();

            mainPart.Document = new Document();
            Body body = new Body();

            body.Append(CreateDocumentHeader(client));

            body.Append(CreateSectionTitle("1. Основные сведения"));
            body.Append(CreateInfoTable(
                ("Наименование", GetValueOrDash(client.Name)),
                ("Категория бизнеса", GetValueOrDash(client.BusinessCategory)),
                ("Основной ОКВЭД", GetValueOrDash(client.MainOkved))
            ));

            body.Append(CreateSectionTitle("2. Регистрационные данные"));

            string kpp = GetOptionalStringProperty(client, "Kpp", "KPP");
            string ogrnLabel = IsEntrepreneur(client.ClientType) ? "ОГРНИП" : "ОГРН";

            body.Append(CreateInfoTable(
                ("ИНН", GetValueOrDash(client.Inn)),
                ("КПП", GetValueOrDash(kpp)),
                (ogrnLabel, GetValueOrDash(client.Ogrn))
            ));

            body.Append(CreateSectionTitle("3. Юридический адрес"));
            body.Append(CreateSingleValueBox(GetValueOrDash(client.Address)));

            body.Append(CreateSectionTitle("4. Банковские реквизиты"));

            if (bankAccounts.Count == 0)
            {
                body.Append(CreateSingleValueBox("Банковские счета не добавлены.", italic: true));
            }
            else
            {
                var account = bankAccounts.First();

                string corr = GetOptionalStringProperty(
                    account,
                    "CorrespondentAccount",
                    "CorrAccount",
                    "CorAccount");

                body.Append(CreateInfoTable(
                    ("Банк", GetValueOrDash(account.BankName)),
                    ("БИК", GetValueOrDash(account.BIC)),
                    ("Расчётный счёт", GetValueOrDash(account.AccountNumber)),
                    ("Корреспондентский счёт", GetValueOrDash(corr))
                ));

                if (bankAccounts.Count > 1)
                {
                    body.Append(CreateSmallNote(
                        $"В карточке клиента указано несколько счетов: {bankAccounts.Count}. В документ выведен первый счёт."));
                }
            }

            body.Append(CreateSectionTitle(IsEntrepreneur(client.ClientType)
                ? "5. Предприниматель"
                : "5. Руководитель"));

            body.Append(CreateInfoTable(
                ("ФИО", GetValueOrDash(client.DirectorFullName)),
                ("Основание", BuildAuthorityText(client))
            ));

            body.Append(CreateFooterBlock());
            body.Append(CreateSectionProperties());

            mainPart.Document.Append(body);
            mainPart.Document.Save();

            return exportPath;
        }

        private static Table CreateDocumentHeader(ClientInfo client)
        {
            var table = CreateBaseTable();
            table.Append(CreateOneColumnGrid());

            string titleText =
                "КАРТОЧКА КОНТРАГЕНТА" +
                Environment.NewLine +
                BuildOrganizationHeader(client) +
                Environment.NewLine +
                $"ИНН {GetValueOrDash(client.Inn)}";

            var titleRow = new TableRow();

            titleRow.Append(CreateCell(
                CreateParagraph(
                    titleText,
                    bold: true,
                    fontSize: "22",
                    color: NavyDark,
                    justify: JustificationValues.Center,
                    preserveLineBreaks: true),
                shading: LightBlue,
                width: TableWidth,
                marginTop: "220",
                marginBottom: "220"));

            table.Append(titleRow);

            table.Append(CreateThinAccentRow());

            return table;
        }
        private static TableRow CreateThinAccentRow()
        {
            var row = new TableRow();

            row.Append(new TableRowProperties(
                new TableRowHeight
                {
                    Val = 45,
                    HeightType = HeightRuleValues.Exact
                }));

            var cell = new TableCell();

            cell.Append(new TableCellProperties(
                new TableCellWidth
                {
                    Width = TableWidth,
                    Type = TableWidthUnitValues.Dxa
                },
                new Shading
                {
                    Val = ShadingPatternValues.Clear,
                    Fill = Gold
                },
                new TableCellMargin(
                    new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "0", Type = TableWidthUnitValues.Dxa })
            ));

            cell.Append(CreateParagraph("", fontSize: "1"));

            row.Append(cell);

            return row;
        }
        private static Paragraph CreateSectionTitle(string title)
        {
            var paragraph = new Paragraph();

            paragraph.Append(new ParagraphProperties(
                new SpacingBetweenLines { Before = "125", After = "55" },
                new KeepNext()));

            var run = new Run();

            run.Append(new RunProperties(
                new Bold(),
                new FontSize { Val = "17" },
                new Color { Val = Navy }));

            run.Append(new Text(title) { Space = SpaceProcessingModeValues.Preserve });
            paragraph.Append(run);

            return paragraph;
        }

        private static Table CreateInfoTable(params (string Label, string Value)[] rows)
        {
            var table = CreateBaseTable();
            table.Append(CreateTwoColumnGrid());

            foreach (var rowData in rows)
            {
                var row = new TableRow();

                row.Append(CreateCell(
                    CreateParagraph(rowData.Label, bold: true, fontSize: "15", color: NavyDark),
                    shading: LightBlue,
                    width: LabelColumnWidth,
                    marginTop: "62",
                    marginBottom: "62"));

                row.Append(CreateCell(
                    CreateParagraph(
                        GetValueOrDash(rowData.Value),
                        fontSize: "15",
                        color: TextDark,
                        preserveLineBreaks: true),
                    shading: White,
                    width: ValueColumnWidth,
                    marginTop: "62",
                    marginBottom: "62"));

                table.Append(row);
            }

            return table;
        }

        private static Table CreateSingleValueBox(string text, bool italic = false)
        {
            var table = CreateBaseTable();
            table.Append(CreateOneColumnGrid());

            var row = new TableRow();

            row.Append(CreateCell(
                CreateParagraph(
                    GetValueOrDash(text),
                    fontSize: "15",
                    color: TextDark,
                    italic: italic,
                    preserveLineBreaks: true),
                shading: White,
                width: TableWidth,
                marginTop: "75",
                marginBottom: "75"));

            table.Append(row);

            return table;
        }

        private static Table CreateFooterBlock()
        {
            var table = CreateBaseTable();
            table.Append(CreateOneColumnGrid());

            string orgName = ActiveOrganizationService.Current?.Name ?? "—";
            string orgInn = ActiveOrganizationService.Current?.Inn ?? "—";

            string footerText =
    $"Документ сформирован автоматически в NIATEC.Client · {DateTime.Now:dd.MM.yyyy HH:mm}";

            var row = new TableRow();

            row.Append(CreateCell(
                CreateParagraph(
                    footerText,
                    fontSize: "13",
                    color: TextMuted,
                    preserveLineBreaks: true),
                shading: LightGray,
                width: TableWidth,
                marginTop: "65",
                marginBottom: "65"));

            table.Append(row);

            return table;
        }

        private static Table CreateBaseTable()
        {
            var table = new Table();

            table.AppendChild(new TableProperties(
                new TableWidth { Width = TableWidth, Type = TableWidthUnitValues.Dxa },
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4, Color = Border },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Border },
                    new LeftBorder { Val = BorderValues.Single, Size = 4, Color = Border },
                    new RightBorder { Val = BorderValues.Single, Size = 4, Color = Border },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = Border },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = Border }
                )));

            return table;
        }

        private static TableGrid CreateTwoColumnGrid()
        {
            return new TableGrid(
                new GridColumn { Width = LabelColumnWidth },
                new GridColumn { Width = ValueColumnWidth });
        }

        private static TableGrid CreateOneColumnGrid()
        {
            return new TableGrid(
                new GridColumn { Width = TableWidth });
        }

        private static TableCell CreateCell(
            Paragraph paragraph,
            string shading,
            string width,
            string marginTop = "75",
            string marginBottom = "75")
        {
            var cell = new TableCell();

            cell.Append(new TableCellProperties(
                new TableCellWidth { Width = width, Type = TableWidthUnitValues.Dxa },
                new Shading { Val = ShadingPatternValues.Clear, Fill = shading },
                new TableCellMargin(
                    new TopMargin { Width = marginTop, Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = marginBottom, Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "130", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "130", Type = TableWidthUnitValues.Dxa })
            ));

            cell.Append(paragraph);
            return cell;
        }

        private static Paragraph CreateParagraph(
            string text,
            bool bold = false,
            bool italic = false,
            string fontSize = "15",
            string color = TextDark,
            JustificationValues? justify = null,
            bool preserveLineBreaks = false)
        {
            var paragraph = new Paragraph();

            paragraph.Append(new ParagraphProperties(
                new SpacingBetweenLines { Before = "0", After = "0" }));

            if (justify.HasValue)
            {
                paragraph.ParagraphProperties!.Append(
                    new Justification { Val = justify.Value });
            }

            var run = new Run();
            var runProperties = new RunProperties();

            if (bold)
                runProperties.Append(new Bold());

            if (italic)
                runProperties.Append(new Italic());

            runProperties.Append(new FontSize { Val = fontSize });
            runProperties.Append(new Color { Val = color });

            run.Append(runProperties);

            if (preserveLineBreaks)
            {
                string normalized = text
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n");

                string[] lines = normalized.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    if (i > 0)
                        run.Append(new Break());

                    run.Append(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
                }
            }
            else
            {
                run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            }

            paragraph.Append(run);
            return paragraph;
        }

        private static Paragraph CreateSmallNote(string text)
        {
            var paragraph = new Paragraph();

            paragraph.Append(new ParagraphProperties(
                new SpacingBetweenLines { Before = "45", After = "30" }));

            var run = new Run();

            run.Append(new RunProperties(
                new Italic(),
                new FontSize { Val = "13" },
                new Color { Val = TextMuted }));

            run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            paragraph.Append(run);

            return paragraph;
        }

        private static SectionProperties CreateSectionProperties()
        {
            return new SectionProperties(
                new PageSize
                {
                    Width = 11906,
                    Height = 16838
                },
                new PageMargin
                {
                    Top = 620,
                    Right = 760,
                    Bottom = 620,
                    Left = 760,
                    Header = 300,
                    Footer = 300,
                    Gutter = 0
                });
        }

        private static string BuildOrganizationHeader(ClientInfo client)
        {
            string name = (client.Name ?? string.Empty).Trim();
            string type = (client.ClientType ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(name))
                return "Контрагент без названия";

            if (type == "ООО")
            {
                if (name.StartsWith("ООО ", StringComparison.OrdinalIgnoreCase))
                {
                    string shortName = name.Substring(4).Trim().Trim('"', '«', '»');
                    return $"ОБЩЕСТВО С ОГРАНИЧЕННОЙ ОТВЕТСТВЕННОСТЬЮ «{shortName}»";
                }

                return $"ОБЩЕСТВО С ОГРАНИЧЕННОЙ ОТВЕТСТВЕННОСТЬЮ «{name}»";
            }

            if (type == "АНО")
            {
                if (name.StartsWith("АНО ", StringComparison.OrdinalIgnoreCase))
                {
                    string shortName = name.Substring(4).Trim().Trim('"', '«', '»');
                    return $"АВТОНОМНАЯ НЕКОММЕРЧЕСКАЯ ОРГАНИЗАЦИЯ «{shortName}»";
                }

                return $"АВТОНОМНАЯ НЕКОММЕРЧЕСКАЯ ОРГАНИЗАЦИЯ «{name}»";
            }

            return name;
        }

        private static string BuildAuthorityText(ClientInfo client)
        {
            if (IsEntrepreneur(client.ClientType))
                return "Действует на основании государственной регистрации в качестве индивидуального предпринимателя.";

            if (string.Equals(client.ClientType, "АНО", StringComparison.OrdinalIgnoreCase))
                return "Действует на основании Устава организации.";

            return "Действует на основании Устава.";
        }

        private static bool IsEntrepreneur(string? clientType)
        {
            return string.Equals(clientType, "ИП", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clientType, "ИПГКФХ", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetValueOrDash(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        }

        private static string GetOptionalStringProperty(object source, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                PropertyInfo? property = source.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.Instance);

                string? value = property?.GetValue(source)?.ToString();

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static string MakeSafeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "Контрагент";

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            return fileName;
        }
    }
}