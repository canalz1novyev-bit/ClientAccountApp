using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.IO;
using System.Linq;
using Windows.Storage;

namespace ClientAccountApp
{
    public static class TaxRequirementResponseWordService
    {
        public static string GenerateResponseDocx(
    OrganizationProfile organization,
    ClientInfo client,
    string sourceRequirementFilePath,
    string responseText)
        {
            if (organization == null)
                throw new ArgumentNullException(nameof(organization));

            if (string.IsNullOrWhiteSpace(responseText))
                throw new ArgumentException("Текст ответа пустой.", nameof(responseText));

            string folder = Path.Combine(
                AppPaths.AppDataFolder,
                "AiResponses");

            Directory.CreateDirectory(folder);

            string organizationKey = !string.IsNullOrWhiteSpace(organization.Inn)
                ? organization.Inn.Trim()
                : organization.Id.ToString();

            string fileName =
                $"Ответ_на_требование_ФНС_{organizationKey}_{DateTime.Now:yyyyMMdd_HHmm}.docx";

            string fullPath = Path.Combine(folder, fileName);

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            using var document = WordprocessingDocument.Create(
                fullPath,
                WordprocessingDocumentType.Document);

            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            Body body = mainPart.Document.Body!;

            body.Append(CreateParagraph("ЧЕРНОВИК ОТВЕТА НА ТРЕБОВАНИЕ ФНС", true, 26));
            body.Append(CreateParagraph($"Дата подготовки: {DateTime.Now:dd.MM.yyyy HH:mm}", false, 20));
            body.Append(CreateParagraph(" "));

            body.Append(CreateParagraph("Организация", true, 22));
            body.Append(CreateParagraph(EmptyDash(organization.Name)));
            body.Append(CreateParagraph($"ИНН: {EmptyDash(organization.Inn)}"));
            body.Append(CreateParagraph($"КПП: {EmptyDash(organization.Kpp)}"));
            body.Append(CreateParagraph($"ОГРН / ОГРНИП: {EmptyDash(organization.Ogrn)}"));
            body.Append(CreateParagraph($"Адрес: {EmptyDash(organization.LegalAddress)}"));

            body.Append(CreateParagraph("Клиент / налогоплательщик", true, 22));
            body.Append(CreateParagraph(EmptyDash(client.Name)));
            body.Append(CreateParagraph($"Тип клиента: {EmptyDash(client.ClientType)}"));
            body.Append(CreateParagraph($"ИНН: {EmptyDash(client.Inn)}"));
            body.Append(CreateParagraph($"ОГРН / ОГРНИП: {EmptyDash(client.Ogrn)}"));
            body.Append(CreateParagraph($"Адрес: {EmptyDash(client.Address)}"));

            if (!string.IsNullOrWhiteSpace(client.DirectorFullName))
            {
                body.Append(CreateParagraph($"Руководитель / предприниматель: {client.DirectorFullName}"));
            }

            body.Append(CreateParagraph(" "));
            if (!string.IsNullOrWhiteSpace(organization.DirectorName))
            {
                body.Append(CreateParagraph(
                    $"{EmptyDash(organization.DirectorPosition)}: {organization.DirectorName}"));
            }

            body.Append(CreateParagraph(" "));

            body.Append(CreateParagraph("Исходный файл требования", true, 22));
            body.Append(CreateParagraph(Path.GetFileName(sourceRequirementFilePath)));
            body.Append(CreateParagraph(" "));

            body.Append(CreateParagraph("Текст ответа", true, 22));

            foreach (var paragraphText in SplitIntoParagraphs(responseText))
            {
                body.Append(CreateParagraph(paragraphText));
            }

            body.Append(CreateParagraph(" "));
            body.Append(CreateParagraph("Важно: документ подготовлен с помощью ИИ и требует проверки специалистом перед отправкой в ФНС.", false, 18));

            body.Append(CreateParagraph(" "));
            body.Append(CreateParagraph("Подпись: ____________________", false, 20));

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

            return fullPath;
        }

        private static Paragraph CreateParagraph(
            string text,
            bool bold = false,
            int fontSize = 21)
        {
            var paragraph = new Paragraph();

            paragraph.Append(new ParagraphProperties(
                new SpacingBetweenLines
                {
                    Before = "0",
                    After = "120"
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

        private static string[] SplitIntoParagraphs(string text)
        {
            return text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split('\n')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
        }

        private static string EmptyDash(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "—"
                : value.Trim();
        }
    }
}