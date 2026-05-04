using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClientAccountApp
{
    public static class CounterpartyReportPdfService
    {
        private const string Navy = "#10233F";
        private const string Blue = "#0B4FA8";
        private const string Gold = "#C98A1F";
        private const string LightBlue = "#F4F8FD";
        private const string LightGold = "#FFF8EC";
        private const string Border = "#D7DEE8";
        private const string Text = "#1F2937";
        private const string Muted = "#6B7280";
        private const string Danger = "#B42318";

        public static string CreatePdf(
            CounterpartyAutoCheckResult? checkResult,
            string reportText)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            string cleanedReport = CleanReportText(reportText);
            var report = ParseMarkerReport(cleanedReport);

            string inn = checkResult?.Inn ?? ExtractLineValue(report.CounterpartyCard, "ИНН");
            string checkedAt = checkResult?.CheckedAt.ToString("dd.MM.yyyy HH:mm") ?? DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            string companyName = ExtractCompanyName(checkResult, report);
            string riskLevel = NormalizeRiskLevel(report.RiskLevel, cleanedReport);
            string riskStatus = DetectRiskStatus(riskLevel);

            string reportsFolder = Path.Combine(
                @"C:\NIATEC",
                "Reports",
                "CounterpartyChecks");

            Directory.CreateDirectory(reportsFolder);

            string safeInn = string.IsNullOrWhiteSpace(inn) ? "unknown" : MakeSafeFileName(inn);

            string filePath = Path.Combine(
                reportsFolder,
                $"CounterpartyCheck_{safeInn}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.pdf");

            string logoPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "Reports",
                "NiatecCounterpartyLogo.png");

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9).FontColor(Text));

                    page.Header().Element(x => ComposeHeader(x, logoPath));

                    page.Content().Column(column =>
                    {
                        column.Spacing(10);

                        column.Item().Text("ОТЧЁТ ПРОВЕРКИ КОНТРАГЕНТА")
                            .FontSize(22)
                            .Bold()
                            .FontColor(Navy);

                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"ИНН: {Safe(inn)}").FontSize(8.5f).FontColor(Text);
                            row.RelativeItem().Text($"Дата проверки: {checkedAt}").FontSize(8.5f).FontColor(Text);
                            row.RelativeItem().Text("Источник: DaData / ФНС / открытые данные").FontSize(8.5f).FontColor(Text);
                        });

                        column.Item().Element(x => ComposeExecutiveSummary(
                            x,
                            companyName,
                            inn,
                            riskLevel,
                            riskStatus,
                            report.Summary));

                        column.Item().Element(x => ComposeCompactSources(x, checkResult));

                        if (!string.IsNullOrWhiteSpace(report.Summary))
                        {
                            column.Item().Element(x => ComposeKeySummaryCard(x, report.Summary));
                        }

                        column.Item().Element(x => ComposeCounterpartyCard(
                            x,
                            companyName,
                            inn,
                            report.CounterpartyCard));

                        ComposeSectionIfNotEmpty(column, "РЕГИСТРАЦИОННЫЙ СТАТУС", report.RegistrationStatus);
                        ComposeSectionIfNotEmpty(column, "НПД / САМОЗАНЯТОСТЬ", report.NpdStatus);

                        if (!string.IsNullOrWhiteSpace(report.LabourRisks) ||
                            !string.IsNullOrWhiteSpace(report.RnpStatus))
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Element(x => ComposeSmallCard(
                                    x,
                                    "ТРУДОВЫЕ РИСКИ",
                                    report.LabourRisks));

                                row.ConstantItem(10);

                                row.RelativeItem().Element(x => ComposeSmallCard(
                                    x,
                                    "ЕИС / РНП",
                                    report.RnpStatus));
                            });
                        }

                        ComposeSectionIfNotEmpty(column, "ФАКТОРЫ РИСКА", report.RiskFactors, isRiskBlock: true);
                        ComposeSectionIfNotEmpty(column, "ДОКУМЕНТЫ, КОТОРЫЕ РЕКОМЕНДУЕТСЯ ЗАПРОСИТЬ", report.RequestDocuments);
                        ComposeSectionIfNotEmpty(column, "ЧТО ПРОВЕРИТЬ ДОПОЛНИТЕЛЬНО ВРУЧНУЮ", report.ManualChecks);
                        ComposeSectionIfNotEmpty(column, "ИТОГОВАЯ РЕКОМЕНДАЦИЯ БУХГАЛТЕРУ", report.AccountantRecommendation);

                        column.Item().Element(x => ComposeFinalNote(x, riskLevel));

                        column.Item().PageBreak();

                        column.Item().Element(x => ComposeTechnicalAppendix(x, checkResult));
                    });

                    page.Footer().Element(footer =>
                    {
                        footer.BorderTop(1)
                            .BorderColor(Border)
                            .PaddingTop(8)
                            .Row(row =>
                            {
                                row.RelativeItem().Text("Отчёт сформирован в системе NIATEC.Client")
                                    .FontSize(8)
                                    .FontColor(Muted);

                                row.ConstantItem(120).AlignRight().Text(text =>
                                {
                                    text.Span("Страница ").FontSize(8).FontColor(Muted);
                                    text.CurrentPageNumber().FontSize(8).FontColor(Muted);
                                    text.Span(" из ").FontSize(8).FontColor(Muted);
                                    text.TotalPages().FontSize(8).FontColor(Muted);
                                });
                            });
                    });
                });
            }).GeneratePdf(filePath);

            return filePath;
        }

        public static void OpenPdf(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            if (!File.Exists(filePath))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }

        private static void ComposeHeader(IContainer container, string logoPath)
        {
            container.Column(column =>
            {
                column.Item()
                    .Background("#F7FAFE")
                    .Border(1)
                    .BorderColor(Border)
                    .Padding(10)
                    .Row(row =>
                    {
                        row.ConstantItem(52).Height(40).Element(x =>
                        {
                            if (File.Exists(logoPath))
                            {
                                x.Image(logoPath).FitArea();
                            }
                            else
                            {
                                x.AlignMiddle()
                                    .AlignCenter()
                                    .Text("</>")
                                    .FontSize(18)
                                    .Bold()
                                    .FontColor(Blue);
                            }
                        });

                        row.RelativeItem().PaddingLeft(12).Column(col =>
                        {
                            col.Item().Text("NIATEC.Client")
                                .FontSize(20)
                                .Bold()
                                .FontColor(Navy);

                            col.Item().Text("Проверка контрагентов • аналитика • уверенность")
                                .FontSize(7.5f)
                                .FontColor(Muted);
                        });

                        row.ConstantItem(115)
                            .AlignRight()
                            .AlignMiddle()
                            .Text("Due Diligence Report")
                            .FontSize(8)
                            .FontColor(Muted);
                    });

                column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Border);
            });
        }

        private static void ComposeExecutiveSummary(
            IContainer container,
            string companyName,
            string inn,
            string riskLevel,
            string riskStatus,
            string summary)
        {
            container
                .Border(1)
                .BorderColor(Border)
                .Background("#FFFFFF")
                .Padding(14)
                .Column(main =>
                {
                    main.Item().Row(row =>
                    {
                        row.ConstantItem(54)
                            .AlignMiddle()
                            .AlignCenter()
                            .Text("✓")
                            .FontSize(32)
                            .Bold()
                            .FontColor(GetRiskColor(riskLevel));

                        row.RelativeItem().PaddingLeft(10).Column(col =>
                        {
                            col.Item().Text("ПРЕДВАРИТЕЛЬНАЯ ОЦЕНКА")
                                .FontSize(9)
                                .Bold()
                                .FontColor(Navy);

                            col.Item().Text(riskLevel.ToUpperInvariant())
                                .FontSize(18)
                                .Bold()
                                .FontColor(GetRiskColor(riskLevel));

                            col.Item().PaddingTop(4).Text(riskStatus)
                                .FontSize(8.5f)
                                .LineHeight(1.2f)
                                .FontColor(Muted);
                        });

                        row.ConstantItem(135)
                            .Border(1)
                            .BorderColor("#E7EEF7")
                            .Background(GetRiskBackground(riskLevel))
                            .Padding(10)
                            .Column(col =>
                            {
                                col.Item().AlignCenter().Text("Уровень риска")
                                    .FontSize(8)
                                    .Bold()
                                    .FontColor(Navy);

                                col.Item().PaddingTop(8).AlignCenter().Text(riskLevel)
                                    .FontSize(12)
                                    .Bold()
                                    .FontColor(GetRiskColor(riskLevel));
                            });
                    });

                    main.Item().PaddingTop(10).LineHorizontal(1).LineColor(Border);

                    main.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Cell().Text("Контрагент").FontSize(8).FontColor(Muted);
                        table.Cell().Text("ИНН").FontSize(8).FontColor(Muted);

                        table.Cell().PaddingTop(2).Text(Safe(companyName)).FontSize(9).Bold().FontColor(Text);
                        table.Cell().PaddingTop(2).Text(Safe(inn)).FontSize(9).Bold().FontColor(Text);
                    });

                    if (!string.IsNullOrWhiteSpace(summary))
                    {
                        main.Item().PaddingTop(8)
                            .Background(LightBlue)
                            .Border(1)
                            .BorderColor("#E7EEF7")
                            .Padding(8)
                            .Text(Shorten(summary, 520))
                            .FontSize(8.3f)
                            .LineHeight(1.2f)
                            .FontColor(Text);
                    }
                });
        }

        private static void ComposeCompactSources(IContainer container, CounterpartyAutoCheckResult? checkResult)
        {
            container
                .Border(1)
                .BorderColor(Border)
                .Background(LightBlue)
                .Padding(10)
                .Column(column =>
                {
                    column.Spacing(6);

                    column.Item().Text("ИСТОЧНИКИ ПРОВЕРКИ")
                        .FontSize(10)
                        .Bold()
                        .FontColor(Navy);

                    column.Item().Text(
                            "Внешние источники, использованные при автоматической проверке. Подробная техническая сводка приведена в приложении.")
                        .FontSize(8)
                        .LineHeight(1.2f)
                        .FontColor(Muted);

                    if (checkResult?.Sources == null || checkResult.Sources.Count == 0)
                    {
                        column.Item().Text("Источники проверки не зафиксированы.")
                            .FontSize(8)
                            .FontColor(Muted);

                        return;
                    }

                    column.Item().PaddingTop(4).Row(row =>
                    {
                        foreach (var source in checkResult.Sources.Take(4))
                        {
                            row.RelativeItem()
                                .Border(1)
                                .BorderColor("#E7EEF7")
                                .Background("#FFFFFF")
                                .Padding(6)
                                .Column(card =>
                                {
                                    card.Item().Text(ShortSourceName(source.SourceName))
                                        .FontSize(7.5f)
                                        .Bold()
                                        .FontColor(Navy);

                                    card.Item().PaddingTop(3).Text(source.IsRealDataReceived ? "проверено" : "требует внимания")
                                        .FontSize(7)
                                        .FontColor(source.IsRealDataReceived ? Blue : Gold);
                                });
                        }
                    });
                });
        }

        private static void ComposeKeySummaryCard(IContainer container, string summary)
        {
            container
                .Border(1)
                .BorderColor(Border)
                .Background("#FFFFFF")
                .Padding(12)
                .Column(col =>
                {
                    col.Item().Text("КЛЮЧЕВЫЕ ВЫВОДЫ")
                        .FontSize(11)
                        .Bold()
                        .FontColor(Navy);

                    col.Item().PaddingTop(6).Text(Shorten(summary, 900))
                        .FontSize(8.6f)
                        .LineHeight(1.25f)
                        .FontColor(Text);
                });
        }

        private static void ComposeCounterpartyCard(
            IContainer container,
            string companyName,
            string inn,
            string cardText)
        {
            container
                .Border(1)
                .BorderColor(Border)
                .Background("#FFFFFF")
                .Padding(12)
                .Column(col =>
                {
                    col.Item().Text("КАРТОЧКА КОНТРАГЕНТА")
                        .FontSize(11)
                        .Bold()
                        .FontColor(Navy);

                    col.Item().PaddingTop(6).Text(Safe(companyName))
                        .FontSize(11)
                        .Bold()
                        .FontColor(Text);

                    col.Item().Text($"ИНН: {Safe(inn)}")
                        .FontSize(8.5f)
                        .FontColor(Muted);

                    if (!string.IsNullOrWhiteSpace(cardText))
                    {
                        col.Item().PaddingTop(8).Text(FormatBusinessText(cardText))
                            .FontSize(8.5f)
                            .LineHeight(1.25f)
                            .FontColor(Text);
                    }
                });
        }

        private static void ComposeSectionIfNotEmpty(
            ColumnDescriptor column,
            string title,
            string body,
            bool isRiskBlock = false)
        {
            if (string.IsNullOrWhiteSpace(body))
                return;

            column.Item().Element(x => ComposeBusinessSection(x, title, body, isRiskBlock));
        }

        private static void ComposeBusinessSection(
            IContainer container,
            string title,
            string body,
            bool isRiskBlock = false)
        {
            container
                .Border(1)
                .BorderColor(isRiskBlock ? "#E8C77D" : Border)
                .Background(isRiskBlock ? LightGold : "#FFFFFF")
                .Padding(12)
                .Column(col =>
                {
                    col.Item().Text(title)
                        .FontSize(11)
                        .Bold()
                        .FontColor(Navy);

                    col.Item().PaddingTop(6).Text(FormatBusinessText(body))
                        .FontSize(8.5f)
                        .LineHeight(1.25f)
                        .FontColor(Text);
                });
        }

        private static void ComposeSmallCard(IContainer container, string title, string body)
        {
            container
                .Border(1)
                .BorderColor(Border)
                .Background("#FFFFFF")
                .Padding(10)
                .Column(col =>
                {
                    col.Item().Text(title)
                        .FontSize(10)
                        .Bold()
                        .FontColor(Navy);

                    col.Item().PaddingTop(6).Text(string.IsNullOrWhiteSpace(body) ? "Сведения не представлены." : FormatBusinessText(body))
                        .FontSize(8)
                        .LineHeight(1.2f)
                        .FontColor(Text);
                });
        }

        private static void ComposeFinalNote(IContainer container, string riskLevel)
        {
            container
                .Border(1)
                .BorderColor("#E8C77D")
                .Background(LightGold)
                .Padding(12)
                .Column(col =>
                {
                    col.Item().Text("ИТОГОВОЕ ПРИМЕЧАНИЕ")
                        .FontSize(11)
                        .Bold()
                        .FontColor(Navy);

                    col.Item().PaddingTop(6).Text(
                            $"Отчёт сформирован автоматически на основании полученных внешних сведений. Уровень риска: {riskLevel}. Перед принятием решения рекомендуется проверить первичные документы и при необходимости выполнить ручную проверку источников.")
                        .FontSize(8.5f)
                        .LineHeight(1.25f)
                        .FontColor(Text);
                });
        }

        private static void ComposeTechnicalAppendix(
            IContainer container,
            CounterpartyAutoCheckResult? checkResult)
        {
            container.Column(column =>
            {
                column.Spacing(10);

                column.Item().Text("ПРИЛОЖЕНИЕ")
                    .FontSize(18)
                    .Bold()
                    .FontColor(Navy);

                column.Item().Text("Техническая сводка источников проверки")
                    .FontSize(12)
                    .Bold()
                    .FontColor(Muted);

                column.Item()
                    .Background("#F7FAFE")
                    .Border(1)
                    .BorderColor(Border)
                    .Padding(10)
                    .Text(
                        "В этом разделе приведены технические сведения, полученные из внешних источников. " +
                        "Они используются как доказательная база для делового отчёта и помогают проверить, " +
                        "на каких данных был сформирован вывод.")
                    .FontSize(8.5f)
                    .LineHeight(1.25f)
                    .FontColor(Text);

                if (checkResult == null || checkResult.Sources == null || checkResult.Sources.Count == 0)
                {
                    column.Item()
                        .Border(1)
                        .BorderColor(Border)
                        .Padding(10)
                        .Text("Технические сведения источников отсутствуют.")
                        .FontSize(9)
                        .FontColor(Muted);

                    return;
                }

                foreach (var source in checkResult.Sources)
                {
                    column.Item().Element(x => ComposeTechnicalSourceBlock(x, source));
                }
            });
        }

        private static void ComposeTechnicalSourceBlock(
            IContainer container,
            CounterpartySourceCheckResult source)
        {
            string statusColor = source.IsRealDataReceived ? Blue : Gold;

            container
                .Border(1)
                .BorderColor(Border)
                .Background("#FFFFFF")
                .Padding(10)
                .Column(column =>
                {
                    column.Spacing(6);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(source.SourceName)
                            .FontSize(11)
                            .Bold()
                            .FontColor(Navy);

                        row.ConstantItem(150).AlignRight().Text(source.Status)
                            .FontSize(8.5f)
                            .Bold()
                            .FontColor(statusColor);
                    });

                    if (!string.IsNullOrWhiteSpace(source.Summary))
                    {
                        column.Item()
                            .Background(LightBlue)
                            .Border(1)
                            .BorderColor("#E7EEF7")
                            .Padding(8)
                            .Text(source.Summary)
                            .FontSize(8.5f)
                            .LineHeight(1.25f)
                            .FontColor(Text);
                    }

                    if (!string.IsNullOrWhiteSpace(source.Details))
                    {
                        column.Item().Text("Полученные сведения")
                            .FontSize(8)
                            .Bold()
                            .FontColor(Muted);

                        column.Item()
                            .Text(FormatTechnicalText(source.Details))
                            .FontSize(7.7f)
                            .LineHeight(1.2f)
                            .FontColor(Text);
                    }

                    if (!string.IsNullOrWhiteSpace(source.TechnicalInfo))
                    {
                        column.Item()
                            .BorderTop(1)
                            .BorderColor(Border)
                            .PaddingTop(5)
                            .Text("Техническая информация")
                            .FontSize(8)
                            .Bold()
                            .FontColor(Muted);

                        column.Item()
                            .Text(FormatTechnicalText(source.TechnicalInfo))
                            .FontSize(7)
                            .LineHeight(1.15f)
                            .FontColor(Muted);
                    }
                });
        }

        private static MarkerReport ParseMarkerReport(string text)
        {
            var report = new MarkerReport
            {
                RawText = text,
                Summary = ExtractMarkerBlock(text, "[SUMMARY]"),
                CounterpartyCard = ExtractMarkerBlock(text, "[COUNTERPARTY_CARD]"),
                Sources = ExtractMarkerBlock(text, "[SOURCES]"),
                RegistrationStatus = ExtractMarkerBlock(text, "[REGISTRATION_STATUS]"),
                NpdStatus = ExtractMarkerBlock(text, "[NPD_STATUS]"),
                LabourRisks = ExtractMarkerBlock(text, "[LABOUR_RISKS]"),
                RnpStatus = ExtractMarkerBlock(text, "[RNP_STATUS]"),
                RiskFactors = ExtractMarkerBlock(text, "[RISK_FACTORS]"),
                RequestDocuments = ExtractMarkerBlock(text, "[REQUEST_DOCUMENTS]"),
                ManualChecks = ExtractMarkerBlock(text, "[MANUAL_CHECKS]"),
                AccountantRecommendation = ExtractMarkerBlock(text, "[ACCOUNTANT_RECOMMENDATION]"),
                RiskLevel = ExtractMarkerBlock(text, "[RISK_LEVEL]")
            };

            if (string.IsNullOrWhiteSpace(report.Summary))
                report.Summary = Shorten(text, 900);

            return report;
        }

        private static string ExtractMarkerBlock(string text, string marker)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            string[] markers =
            {
                "[SUMMARY]",
                "[COUNTERPARTY_CARD]",
                "[SOURCES]",
                "[REGISTRATION_STATUS]",
                "[NPD_STATUS]",
                "[LABOUR_RISKS]",
                "[RNP_STATUS]",
                "[RISK_FACTORS]",
                "[REQUEST_DOCUMENTS]",
                "[MANUAL_CHECKS]",
                "[ACCOUNTANT_RECOMMENDATION]",
                "[RISK_LEVEL]"
            };

            int start = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (start < 0)
                return "";

            start += marker.Length;

            int end = text.Length;

            foreach (string nextMarker in markers)
            {
                if (nextMarker.Equals(marker, StringComparison.OrdinalIgnoreCase))
                    continue;

                int nextIndex = text.IndexOf(nextMarker, start, StringComparison.OrdinalIgnoreCase);

                if (nextIndex >= 0 && nextIndex < end)
                    end = nextIndex;
            }

            return text.Substring(start, end - start).Trim();
        }

        private static string CleanReportText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            value = value.Replace("\r\n", "\n");

            value = Regex.Replace(value, @"#{1,6}\s*", "");
            value = Regex.Replace(value, @"\*\*(.*?)\*\*", "$1");
            value = Regex.Replace(value, @"^\s*[-*]\s+", "• ", RegexOptions.Multiline);
            value = Regex.Replace(value, @"\n{3,}", "\n\n");

            return value.Trim();
        }

        private static string FormatBusinessText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            value = value.Replace("\r\n", "\n").Trim();

            value = Regex.Replace(value, @"\n{3,}", "\n\n");

            return value;
        }

        private static string FormatTechnicalText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            value = value.Replace("\r\n", "\n").Trim();

            value = Regex.Replace(value, @"\n{3,}", "\n\n");

            if (value.Length > 5000)
                value = value.Substring(0, 5000) + "\n\n...текст сокращён для PDF-отчёта.";

            return value;
        }

        private static string ExtractCompanyName(CounterpartyAutoCheckResult? checkResult, MarkerReport report)
        {
            string fromCard = ExtractLineValue(report.CounterpartyCard, "Наименование");

            if (!string.IsNullOrWhiteSpace(fromCard))
                return fromCard;

            string fullFromCard = ExtractLineValue(report.CounterpartyCard, "Полное наименование");

            if (!string.IsNullOrWhiteSpace(fullFromCard))
                return fullFromCard;

            string fromSources = ExtractFromSources(checkResult, "Наименование");

            if (!string.IsNullOrWhiteSpace(fromSources))
                return fromSources;

            string fullFromSources = ExtractFromSources(checkResult, "Полное наименование");

            if (!string.IsNullOrWhiteSpace(fullFromSources))
                return fullFromSources;

            return "Контрагент";
        }

        private static string ExtractFromSources(CounterpartyAutoCheckResult? checkResult, string label)
        {
            if (checkResult?.Sources == null)
                return "";

            foreach (var source in checkResult.Sources)
            {
                string value = ExtractLineValue(source.Details, label);

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        private static string NormalizeRiskLevel(string markerRisk, string fullText)
        {
            string value = markerRisk;

            if (string.IsNullOrWhiteSpace(value))
                value = fullText;

            if (string.IsNullOrWhiteSpace(value))
                return "Недостаточно данных";

            if (value.Contains("повыш", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("высок", StringComparison.OrdinalIgnoreCase))
                return "Повышенный риск";

            if (value.Contains("умерен", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("сред", StringComparison.OrdinalIgnoreCase))
                return "Умеренный риск";

            if (value.Contains("низк", StringComparison.OrdinalIgnoreCase))
                return "Низкий риск";

            return "Недостаточно данных";
        }

        private static string DetectRiskStatus(string riskLevel)
        {
            if (riskLevel.Contains("Повыш", StringComparison.OrdinalIgnoreCase))
                return "Выявлены факторы, требующие дополнительной проверки до заключения сделки.";

            if (riskLevel.Contains("Умер", StringComparison.OrdinalIgnoreCase))
                return "Выявлены отдельные факторы, требующие внимания и дополнительного анализа.";

            if (riskLevel.Contains("Низк", StringComparison.OrdinalIgnoreCase))
                return "Явные риск-признаки по полученным данным не выявлены.";

            return "Данных недостаточно для уверенной оценки. Рекомендуется дополнительная проверка.";
        }

        private static string GetRiskColor(string riskLevel)
        {
            if (riskLevel.Contains("Повыш", StringComparison.OrdinalIgnoreCase))
                return Danger;

            if (riskLevel.Contains("Умер", StringComparison.OrdinalIgnoreCase))
                return Gold;

            if (riskLevel.Contains("Низк", StringComparison.OrdinalIgnoreCase))
                return Blue;

            return Muted;
        }

        private static string GetRiskBackground(string riskLevel)
        {
            if (riskLevel.Contains("Повыш", StringComparison.OrdinalIgnoreCase))
                return "#FFF1F0";

            if (riskLevel.Contains("Умер", StringComparison.OrdinalIgnoreCase))
                return LightGold;

            if (riskLevel.Contains("Низк", StringComparison.OrdinalIgnoreCase))
                return LightBlue;

            return "#F5F5F5";
        }

        private static string ExtractLineValue(string text, string label)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var match = Regex.Match(
                text,
                Regex.Escape(label) + @"\s*:\s*(.+)",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return "";

            string value = match.Groups[1].Value.Trim();

            int lineBreakIndex = value.IndexOf('\n');

            if (lineBreakIndex >= 0)
                value = value.Substring(0, lineBreakIndex).Trim();

            return value;
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            value = value.Trim();

            if (value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength) + "...";
        }

        private static string ShortSourceName(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
                return "Источник";

            if (sourceName.Contains("DaData", StringComparison.OrdinalIgnoreCase))
                return "DaData / ФНС";

            if (sourceName.Contains("НПД", StringComparison.OrdinalIgnoreCase))
                return "ФНС НПД";

            if (sourceName.Contains("Роструд", StringComparison.OrdinalIgnoreCase))
                return "Роструд";

            if (sourceName.Contains("ЕИС", StringComparison.OrdinalIgnoreCase) ||
                sourceName.Contains("РНП", StringComparison.OrdinalIgnoreCase))
                return "ЕИС / РНП";

            return sourceName;
        }

        private static string Safe(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "не указано"
                : value.Trim();
        }

        private static string MakeSafeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }

            return value.Trim();
        }

        private sealed class MarkerReport
        {
            public string RawText { get; set; } = "";
            public string Summary { get; set; } = "";
            public string CounterpartyCard { get; set; } = "";
            public string Sources { get; set; } = "";
            public string RegistrationStatus { get; set; } = "";
            public string NpdStatus { get; set; } = "";
            public string LabourRisks { get; set; } = "";
            public string RnpStatus { get; set; } = "";
            public string RiskFactors { get; set; } = "";
            public string RequestDocuments { get; set; } = "";
            public string ManualChecks { get; set; } = "";
            public string AccountantRecommendation { get; set; } = "";
            public string RiskLevel { get; set; } = "";
        }
    }
}