using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.IO;
using System.Linq;
using System.Text;
using UglyToad.PdfPig;

namespace ClientAccountApp
{
    public static class TaxRequirementTextExtractorService
    {
        public static string ExtractText(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу пустой.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Файл требования не найден.", filePath);

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            string text = extension switch
            {
                ".pdf" => ExtractPdfText(filePath),
                ".docx" => ExtractDocxText(filePath),
                ".txt" => File.ReadAllText(filePath),
                ".xml" => File.ReadAllText(filePath),
                ".doc" => throw new NotSupportedException("Формат .doc пока не поддерживается. Сохраните документ как .docx или PDF."),
                _ => TryExtractAsText(filePath)
            };

            text = NormalizeText(text);

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException(
                    "Не удалось извлечь текст из файла. Возможно, это скан без текстового слоя. Пока поддерживаются текстовые PDF, DOCX, TXT и XML.");
            }

            return text;
        }

        private static string ExtractPdfText(string filePath)
        {
            var sb = new StringBuilder();

            using var document = PdfDocument.Open(filePath);

            foreach (var page in document.GetPages())
            {
                sb.AppendLine($"--- Страница {page.Number} ---");
                sb.AppendLine(page.Text);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string ExtractDocxText(string filePath)
        {
            var sb = new StringBuilder();

            using var wordDocument = WordprocessingDocument.Open(filePath, false);

            var body = wordDocument.MainDocumentPart?.Document.Body;

            if (body == null)
                return "";

            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                string paragraphText = string.Concat(
                    paragraph.Descendants<Text>().Select(x => x.Text));

                if (!string.IsNullOrWhiteSpace(paragraphText))
                    sb.AppendLine(paragraphText);
            }

            return sb.ToString();
        }

        private static string TryExtractAsText(string filePath)
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch
            {
                throw new NotSupportedException(
                    $"Формат файла «{Path.GetExtension(filePath)}» пока не поддерживается для извлечения текста.");
            }
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var lines = text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split('\n')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return string.Join(Environment.NewLine, lines);
        }
    }
}