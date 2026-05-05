using ClientAccountApp.Services;
using System;
using System.IO;
using System.Linq;

namespace ClientAccountApp
{
    public sealed class LegacyInvoiceDocumentsMigrationResult
    {
        public int CheckedInvoices { get; set; }
        public int RegisteredDocuments { get; set; }
        public int AlreadyExists { get; set; }
        public int MissingFiles { get; set; }
        public int MissingClients { get; set; }
    }

    public static class LegacyInvoiceDocumentsMigrationService
    {
        public static LegacyInvoiceDocumentsMigrationResult Run()
        {

            var result = new LegacyInvoiceDocumentsMigrationResult();

            using var db = new AppDbContext();

            var invoices = db.Invoices
                .Where(x => !string.IsNullOrWhiteSpace(x.DocumentRelativePath))
                .ToList();

            result.CheckedInvoices = invoices.Count;

            foreach (var invoice in invoices)
            {
                string relativePath = invoice.DocumentRelativePath ?? "";

                bool documentAlreadyExists = db.InvoiceDocuments.Any(x =>
                    x.InvoiceId == invoice.Id &&
                    x.RelativePath == relativePath);

                if (documentAlreadyExists)
                {
                    result.AlreadyExists++;
                    continue;
                }

                var client = db.Clients.FirstOrDefault(x => x.Id == invoice.ClientInfoId);

                if (client == null)
                {
                    result.MissingClients++;
                    continue;
                }

                string fullPath;

                try
                {
                    fullPath = ClientFileStorageService.GetFullPath(relativePath);
                }
                catch
                {
                    result.MissingFiles++;
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    result.MissingFiles++;
                    continue;
                }

                string fileName = Path.GetFileName(fullPath);
                long fileSizeBytes = new FileInfo(fullPath).Length;

                bool clientFileExists = db.ClientFiles.Any(f =>
                    f.ClientInfoId == client.Id &&
                    f.RelativePath == relativePath);

                if (!clientFileExists)
                {
                    db.ClientFiles.Add(new ClientFile
                    {
                        ClientInfoId = client.Id,
                        OriginalFileName = fileName,
                        RelativePath = relativePath,
                        FileSizeBytes = fileSizeBytes,
                        AddedAt = DateTime.Now,
                        Category = ClientFileCategoryNames.Invoice
                    });
                }

                string documentFormat = GetDocumentFormat(fileName);

                InvoiceDocumentService.RegisterInvoiceDocument(
                    db,
                    invoice,
                    client,
                    "Счёт",
                    documentFormat,
                    fileName,
                    relativePath,
                    fileSizeBytes);

                result.RegisteredDocuments++;
            }

            db.SaveChanges();

            return result;
        }

        private static string GetDocumentFormat(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();

            return extension switch
            {
                ".docx" => "Word",
                ".doc" => "Word",
                ".pdf" => "PDF",
                ".xml" => "XML",
                _ => "Файл"
            };
        }
    }
}