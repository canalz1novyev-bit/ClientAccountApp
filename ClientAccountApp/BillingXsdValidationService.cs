using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace ClientAccountApp
{
    public static class BillingXsdValidationService
    {
        private static string XsdRootFolder =>
            Path.Combine(AppContext.BaseDirectory, "XsdSchemas");

        public static string GetActSellerSchemaPath()
        {
            return Path.Combine(XsdRootFolder, "ACT", "ON_NSCHFDOPPR.xsd");
        }

        public static string GetInvoiceFacturaSellerSchemaPath()
        {
            return Path.Combine(XsdRootFolder, "CHET_F", "ON_NSCHFDOPPR.xsd");
        }

        public static void ValidateOrThrow(string xmlPath, string xsdPath)
        {
            if (string.IsNullOrWhiteSpace(xmlPath))
                throw new ArgumentException("Путь к XML пустой.", nameof(xmlPath));

            if (!File.Exists(xmlPath))
                throw new FileNotFoundException("XML-файл не найден.", xmlPath);

            if (string.IsNullOrWhiteSpace(xsdPath))
                throw new ArgumentException("Путь к XSD пустой.", nameof(xsdPath));

            if (!File.Exists(xsdPath))
                throw new FileNotFoundException(
                    "XSD-схема не найдена. Проверьте, что файлы XSD добавлены в проект и копируются в выходную папку.",
                    xsdPath);

            var errors = new List<string>();

            var schemas = new XmlSchemaSet();
            schemas.Add(null, xsdPath);
            schemas.Compile();

            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = schemas
            };

            settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;

            settings.ValidationEventHandler += (_, args) =>
            {
                string severity = args.Severity == XmlSeverityType.Warning
                    ? "Предупреждение"
                    : "Ошибка";

                string lineInfo = "";

                if (args.Exception != null)
                {
                    lineInfo = $" строка {args.Exception.LineNumber}, позиция {args.Exception.LinePosition}";
                }

                errors.Add($"{severity}{lineInfo}: {args.Message}");
            };

            using var reader = XmlReader.Create(xmlPath, settings);

            while (reader.Read())
            {
                // чтение запускает XSD-валидацию
            }

            if (errors.Count > 0)
            {
                var sb = new StringBuilder();

                sb.AppendLine("XML сформирован, но не прошёл проверку по XSD.");
                sb.AppendLine();
                sb.AppendLine("Первые ошибки:");

                foreach (string error in errors.Take(12))
                {
                    sb.AppendLine("• " + error);
                }

                if (errors.Count > 12)
                {
                    sb.AppendLine($"... ещё ошибок: {errors.Count - 12}");
                }

                throw new InvalidOperationException(sb.ToString());
            }
        }
    }
}