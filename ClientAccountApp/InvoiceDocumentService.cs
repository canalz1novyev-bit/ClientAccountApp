using System;
using System.Linq;

namespace ClientAccountApp
{
    public static class InvoiceDocumentService
    {
        public static void RegisterInvoiceDocument(
            AppDbContext db,
            Invoice invoice,
            ClientInfo client,
            string documentType,
            string documentFormat,
            string originalFileName,
            string relativePath,
            long fileSizeBytes)
        {
            InvoiceDocumentSchemaService.EnsureInvoiceDocumentTables();

            var existing = db.InvoiceDocuments.FirstOrDefault(x =>
                x.InvoiceId == invoice.Id &&
                x.DocumentType == documentType &&
                x.DocumentFormat == documentFormat);

            if (existing == null)
            {
                db.InvoiceDocuments.Add(new InvoiceDocument
                {
                    InvoiceId = invoice.Id,
                    ClientInfoId = client.Id,
                    OrganizationProfileId = invoice.OrganizationProfileId,
                    DocumentType = documentType,
                    DocumentFormat = documentFormat,
                    OriginalFileName = originalFileName,
                    RelativePath = relativePath,
                    FileSizeBytes = fileSizeBytes,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });

                return;
            }

            existing.ClientInfoId = client.Id;
            existing.OrganizationProfileId = invoice.OrganizationProfileId;
            existing.OriginalFileName = originalFileName;
            existing.RelativePath = relativePath;
            existing.FileSizeBytes = fileSizeBytes;
            existing.UpdatedAt = DateTime.Now;
        }
    }
}