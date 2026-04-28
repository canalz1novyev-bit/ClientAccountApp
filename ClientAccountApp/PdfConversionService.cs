using System;
using System.IO;
using System.Runtime.InteropServices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClientAccountApp
{
    public static class PdfConversionService
    {
        public static void ConvertFileToPdf(string sourcePath, string destinationPdfPath)
        {
            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();

            if (extension == ".pdf")
            {
                throw new InvalidOperationException("Выбранный файл уже имеет формат PDF.");
            }

            if (IsImageExtension(extension))
            {
                ConvertImageToPdf(sourcePath, destinationPdfPath);
                return;
            }

            if (IsTextExtension(extension))
            {
                ConvertTextToPdf(sourcePath, destinationPdfPath);
                return;
            }

            if (extension == ".doc" || extension == ".docx")
            {
                ConvertWordToPdf(sourcePath, destinationPdfPath);
                return;
            }

            if (extension == ".xls" || extension == ".xlsx" || extension == ".csv")
            {
                ConvertExcelToPdf(sourcePath, destinationPdfPath);
                return;
            }

            throw new InvalidOperationException($"Конвертация файлов типа {extension} пока не поддерживается.");
        }

        private static void ConvertImageToPdf(string sourcePath, string destinationPdfPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            byte[] imageBytes = File.ReadAllBytes(sourcePath);
            string fileName = Path.GetFileName(sourcePath);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);

                    page.Header()
                        .Text($"Конвертировано в PDF: {fileName}")
                        .FontSize(18)
                        .SemiBold();

                    page.Content()
                        .PaddingTop(10)
                        .Image(imageBytes)
                        .FitArea();

                    page.Footer()
                        .AlignCenter()
                        .Text($"Создано: {DateTime.Now:dd.MM.yyyy HH:mm}");
                });
            }).GeneratePdf(destinationPdfPath);
        }

        private static void ConvertTextToPdf(string sourcePath, string destinationPdfPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            string fileName = Path.GetFileName(sourcePath);
            string text = File.ReadAllText(sourcePath);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(25);

                    page.Header()
                        .Text($"Конвертировано в PDF: {fileName}")
                        .FontSize(18)
                        .SemiBold();

                    page.Content()
                        .PaddingTop(10)
                        .Text(text)
                        .FontSize(11);

                    page.Footer()
                        .AlignCenter()
                        .Text($"Создано: {DateTime.Now:dd.MM.yyyy HH:mm}");
                });
            }).GeneratePdf(destinationPdfPath);
        }

        private static void ConvertWordToPdf(string sourcePath, string destinationPdfPath)
        {
            Type? wordType = Type.GetTypeFromProgID("Word.Application");

            if (wordType == null)
            {
                throw new InvalidOperationException("Microsoft Word не найден. Для конвертации DOC/DOCX в PDF Word должен быть установлен.");
            }

            dynamic? wordApp = null;
            dynamic? document = null;

            try
            {
                wordApp = Activator.CreateInstance(wordType);
                wordApp.Visible = false;

                document = wordApp.Documents.Open(sourcePath, ReadOnly: true);
                document.SaveAs2(destinationPdfPath, 17); // wdFormatPDF = 17
                document.Close(false);
                wordApp.Quit(false);
            }
            finally
            {
                if (document != null) Marshal.FinalReleaseComObject(document);
                if (wordApp != null) Marshal.FinalReleaseComObject(wordApp);
            }
        }

        private static void ConvertExcelToPdf(string sourcePath, string destinationPdfPath)
        {
            Type? excelType = Type.GetTypeFromProgID("Excel.Application");

            if (excelType == null)
            {
                throw new InvalidOperationException("Microsoft Excel не найден. Для конвертации XLS/XLSX/CSV в PDF Excel должен быть установлен.");
            }

            dynamic? excelApp = null;
            dynamic? workbook = null;

            try
            {
                excelApp = Activator.CreateInstance(excelType);
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;

                workbook = excelApp.Workbooks.Open(sourcePath);
                workbook.ExportAsFixedFormat(0, destinationPdfPath); // xlTypePDF = 0
                workbook.Close(false);
                excelApp.Quit();
            }
            finally
            {
                if (workbook != null) Marshal.FinalReleaseComObject(workbook);
                if (excelApp != null) Marshal.FinalReleaseComObject(excelApp);
            }
        }

        private static bool IsImageExtension(string extension)
        {
            return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";
        }

        private static bool IsTextExtension(string extension)
        {
            return extension is ".txt" or ".xml" or ".json" or ".log";
        }
    }
}