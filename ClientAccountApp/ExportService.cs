using System;
using System.Globalization;
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

            var primaryBankAccount = db.BankAccounts
                .AsNoTracking()
                .Where(a => a.ClientInfoId == clientId)
                .OrderBy(a => a.BankName)
                .ThenBy(a => a.AccountNumber)
                .FirstOrDefault();

            string safeClientName = MakeSafeFileName(client.Name);
            string fileName = $"Карточка_контрагента_{safeClientName}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
            string exportPath = Path.Combine(GetExportFolder(), fileName);

            using var wordDocument = WordprocessingDocument.Create(exportPath, WordprocessingDocumentType.Document);
            MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = new Body();

            body.Append(CreateParagraph("КАРТОЧКА КОНТРАГЕНТА", bold: true, fontSize: "28", justify: JustificationValues.Center));
            body.Append(CreateEmptyParagraph());

            string organizationHeader = BuildOrganizationHeader(client);
            body.Append(CreateParagraph(organizationHeader, bold: true, fontSize: "26"));
            body.Append(CreateEmptyParagraph());

            body.Append(CreateParagraph($"ИНН: {GetValueOrDash(client.Inn)}"));

            string kpp = GetOptionalStringProperty(client, "Kpp", "KPP");
            body.Append(CreateParagraph($"КПП: {GetValueOrDash(kpp)}"));

            body.Append(CreateParagraph($"ОГРН: {GetValueOrDash(client.Ogrn)}"));

            string ogrnAssignedDate = GetOptionalDatePropertyAsText(
                client,
                "OgrnAssignedDate",
                "OgrnAssignedAt",
                "OgrnDate",
                "RegistrationDate");

            if (!string.IsNullOrWhiteSpace(ogrnAssignedDate))
            {
                body.Append(CreateParagraph($"Дата присвоения ОГРН: {ogrnAssignedDate}"));
            }

            body.Append(CreateEmptyParagraph());

            if (primaryBankAccount != null)
            {
                body.Append(CreateParagraph($"Расчётный счёт: {GetValueOrDash(primaryBankAccount.AccountNumber)}"));

                body.Append(CreateParagraph(
                    $"Наименование: {GetValueOrDash(primaryBankAccount.BankName)}",
                    preserveLineBreaks: true));

                body.Append(CreateParagraph($"БИК: {GetValueOrDash(primaryBankAccount.BIC)}"));

                string corr = GetOptionalStringProperty(primaryBankAccount, "CorrespondentAccount", "CorrAccount", "CorAccount");
                body.Append(CreateParagraph($"Корсчёт: {GetValueOrDash(corr)}"));
            }
            else
            {
                body.Append(CreateParagraph("Расчётный счёт: —"));
                body.Append(CreateParagraph("Наименование: —"));
                body.Append(CreateParagraph("БИК: —"));
                body.Append(CreateParagraph("Корсчёт: —"));
            }

            body.Append(CreateEmptyParagraph());

            body.Append(CreateParagraph($"Юр. адрес : {GetValueOrDash(client.Address)}", preserveLineBreaks: true));
            body.Append(CreateEmptyParagraph());

            string directorLabel = GetDirectorLabel(client.ClientType);
            body.Append(CreateParagraph($"{directorLabel} :"));
            body.Append(CreateParagraph($"{GetValueOrDash(client.DirectorFullName)}, действует на основании Устава"));
            body.Append(CreateEmptyParagraph());

            mainPart.Document.Append(body);
            mainPart.Document.Save();

            return exportPath;
        }

        private static string BuildOrganizationHeader(ClientInfo client)
        {
            string name = (client.Name ?? string.Empty).Trim();
            string type = (client.ClientType ?? string.Empty).Trim().ToUpperInvariant();

            if (type == "ООО")
            {
                if (name.StartsWith("ООО ", StringComparison.OrdinalIgnoreCase))
                {
                    string shortName = name.Substring(4).Trim().Trim('"', '«', '»');
                    return $"ОБЩЕСТВО С ОГРАНИЧЕННОЙ ОТВЕТСТВЕННОСТЬЮ «{shortName}» ({name})";
                }

                return $"ОБЩЕСТВО С ОГРАНИЧЕННОЙ ОТВЕТСТВЕННОСТЬЮ «{name}»";
            }

            if (type == "АНО")
            {
                return $"АВТОНОМНАЯ НЕКОММЕРЧЕСКАЯ ОРГАНИЗАЦИЯ «{name}»";
            }

            return name;
        }

        private static string GetDirectorLabel(string clientType)
        {
            if (string.Equals(clientType, "ИП", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(clientType, "ИПГКФХ", StringComparison.OrdinalIgnoreCase))
            {
                return "Индивидуальный предприниматель";
            }

            return "Генеральный директор";
        }

        private static Paragraph CreateParagraph(
            string text,
            bool bold = false,
            string fontSize = "24",
            JustificationValues? justify = null,
            bool preserveLineBreaks = false)
        {
            var paragraph = new Paragraph();

            if (justify.HasValue)
            {
                paragraph.ParagraphProperties = new ParagraphProperties(
                    new Justification { Val = justify.Value });
            }

            var run = new Run();
            var runProperties = new RunProperties();

            if (bold)
                runProperties.Append(new Bold());

            runProperties.Append(new FontSize { Val = fontSize });
            run.Append(runProperties);

            if (preserveLineBreaks && text.Contains(Environment.NewLine))
            {
                string[] lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

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

        private static Paragraph CreateEmptyParagraph()
        {
            return new Paragraph(new Run(new Text("")));
        }

        private static string GetValueOrDash(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value;
        }

        private static string GetOptionalStringProperty(object source, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                var property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                var value = property?.GetValue(source)?.ToString();

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static string GetOptionalDatePropertyAsText(object source, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                PropertyInfo? property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                    continue;

                object? raw = property.GetValue(source);
                if (raw == null)
                    continue;

                if (raw is DateTime dt)
                    return dt.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

                if (raw is DateTimeOffset dto)
                    return dto.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

                if (DateTime.TryParse(raw.ToString(), out DateTime parsed))
                    return parsed.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        private static string MakeSafeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            return fileName;
        }
    }
}