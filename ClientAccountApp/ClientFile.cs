using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;

namespace ClientAccountApp
{
    public class ClientFile
    {
        [Key]
        public int Id { get; set; }

        public int ClientInfoId { get; set; }

        public ClientInfo? ClientInfo { get; set; }

        public string OriginalFileName { get; set; } = "";

        public string RelativePath { get; set; } = "";

        public long FileSizeBytes { get; set; }

        public DateTime AddedAt { get; set; }

        public string Category { get; set; } = "Прочее";

        [NotMapped]
        public string AddedAtText => AddedAt.ToString("dd.MM.yyyy HH:mm");

        [NotMapped]
        public string FileSizeText => FormatFileSize(FileSizeBytes);

        [NotMapped]
        public string FileExtension
        {
            get
            {
                return Path.GetExtension(OriginalFileName).ToLowerInvariant();
            }
        }

        [NotMapped]
        public string FileTypeLabel
        {
            get
            {
                return FileExtension switch
                {
                    ".pdf" => "PDF",
                    ".doc" => "WORD",
                    ".docx" => "WORD",
                    ".xls" => "EXCEL",
                    ".xlsx" => "EXCEL",
                    ".csv" => "EXCEL",
                    ".jpg" => "IMG",
                    ".jpeg" => "IMG",
                    ".png" => "IMG",
                    ".bmp" => "IMG",
                    ".gif" => "IMG",
                    ".webp" => "IMG",
                    ".zip" => "ZIP",
                    ".rar" => "ZIP",
                    ".7z" => "ZIP",
                    ".txt" => "TEXT",
                    ".xml" => "XML",
                    _ => "FILE"
                };
            }
        }

        [NotMapped]
        public string FileTypeDescription
        {
            get
            {
                return FileExtension switch
                {
                    ".pdf" => "PDF документ",
                    ".doc" => "Документ Word",
                    ".docx" => "Документ Word",
                    ".xls" => "Таблица Excel",
                    ".xlsx" => "Таблица Excel",
                    ".csv" => "CSV файл",
                    ".jpg" => "Изображение JPG",
                    ".jpeg" => "Изображение JPG",
                    ".png" => "Изображение PNG",
                    ".bmp" => "Изображение BMP",
                    ".gif" => "Изображение GIF",
                    ".webp" => "Изображение WEBP",
                    ".zip" => "ZIP архив",
                    ".rar" => "RAR архив",
                    ".7z" => "7Z архив",
                    ".txt" => "Текстовый файл",
                    ".xml" => "XML файл",
                    _ => "Прочий файл"
                };
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} Б";
            }

            if (bytes < 1024 * 1024)
            {
                return $"{bytes / 1024.0:F1} КБ";
            }

            if (bytes < 1024 * 1024 * 1024)
            {
                return $"{bytes / 1024.0 / 1024.0:F1} МБ";
            }

            return $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} ГБ";
        }
    }
}