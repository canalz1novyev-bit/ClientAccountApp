using System;
using System.Collections.Generic;
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
    public static class ContractWordService
    {
        private static readonly CultureInfo RuCulture = new("ru-RU");

        private sealed class ExecutorInfo
        {
            public string ContractName { get; set; } = "";
            public string Basis { get; set; } = "";
            public string SignerFullName { get; set; } = "";
            public string FullName { get; set; } = "";
            public string Address { get; set; } = "";
            public string Inn { get; set; } = "";
            public string Kpp { get; set; } = "";
            public string Ogrn { get; set; } = "";
            public string BankAccount { get; set; } = "";
            public string BankName { get; set; } = "";
            public string CorrAccount { get; set; } = "";
            public string Bic { get; set; } = "";
            public string Phone { get; set; } = "";
            public string SignerShortName { get; set; } = "";
        }

        // Здесь зашиты реквизиты Исполнителя по образцу договора.
        // Если изменятся — правим только этот блок.
        private static readonly ExecutorInfo Executor = new()
        {
            ContractName = "ОБЩЕСТВО С ОГРАНИЧЕННОЙ ОТВЕТСТВЕННОСТЬЮ «НИАТЕК»",
            Basis = "УСТАВА",
            SignerFullName = "Зиновьева Игоря Алексеевича",
            FullName = "Общество с ограниченной ответственностью \"НИА ТЕКНОЛОДЖИЗ\"",
            Address = "393310, Тамбовская область, м.р-н Инжавинский, г.п. Инжавинский Поссовет, рп Инжавино, ул. Советская, д. 92",
            Inn = "6800000586",
            Kpp = "680001001",
            Ogrn = "1226800005981",
            BankAccount = "40702810261000009175",
            BankName = "ТАМБОВСКОЕ ОТДЕЛЕНИЕ N8594 ПАО СБЕРБАНК",
            CorrAccount = "30101810800000000649",
            Bic = "046850649",
            Phone = "8-953-128-67-14",
            SignerShortName = "Зиновьев И.А."
        };

        public static string GenerateContractDocx(int clientId)
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

            if (string.IsNullOrWhiteSpace(client.Name))
                throw new InvalidOperationException("У клиента не заполнено наименование.");

            string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "contract_template_niatek.docx");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Шаблон договора не найден. Проверь файл Templates\\contract_template_niatek.docx", templatePath);

            DateTime contractDate = DateTime.Today;
            string contractNumber = BuildContractNumber(client, contractDate);

            string tempFolder = Path.Combine(AppPaths.AppDataFolder, "Temp", "Contracts");
            Directory.CreateDirectory(tempFolder);

            string outputPath = Path.Combine(
                tempFolder,
                $"Договор_{MakeSafeFileName(client.Name)}_{contractDate:yyyyMMdd}.docx");

            File.Copy(templatePath, outputPath, true);

            var placeholders = BuildPlaceholders(client, primaryBankAccount, contractDate, contractNumber);
            ReplacePlaceholders(outputPath, placeholders);

            return outputPath;
        }

        private static Dictionary<string, string> BuildPlaceholders(
            ClientInfo client,
            BankAccount? bankAccount,
            DateTime contractDate,
            string contractNumber)
        {
            string clientKpp = GetOptionalStringProperty(client, "Kpp", "KPP");
            string clientBasis = GetClientBasis(client);
            string clientSigner = GetValueOrDash(client.DirectorFullName);
            string clientSignerShort = BuildShortName(client.DirectorFullName);

            DateTime startDate = contractDate;
            DateTime endDate = new DateTime(contractDate.Year, 12, 31);

            string clientRs = bankAccount?.AccountNumber ?? "—";
            string clientBank = bankAccount?.BankName ?? "—";
            string clientBic = bankAccount?.BIC ?? "—";
            string clientKs = GetOptionalStringProperty(bankAccount, "CorrespondentAccount", "CorrAccount", "CorAccount");

            if (string.IsNullOrWhiteSpace(clientKs))
                clientKs = "—";

            return new Dictionary<string, string>
            {
                ["{{NUM}}"] = contractNumber,
                ["{{CITY}}"] = "Тамб.обл.",
                ["{{DATE_DM}}"] = $"\"{contractDate.Day}\" {GetMonthName(contractDate)}",
                ["{{YEAR}}"] = contractDate.Year.ToString(),

                ["{{EXE_CONTRACT_NAME}}"] = Executor.ContractName,
                ["{{EXE_BASIS}}"] = Executor.Basis,
                ["{{EXE_SIGNER}}"] = Executor.SignerFullName,

                ["{{CL_CONTRACT_NAME}}"] = GetValueOrDash(client.Name),
                ["{{CL_SIGNER}}"] = clientSigner,
                ["{{CL_BASIS}}"] = clientBasis,

                ["{{START_DAY}}"] = startDate.Day.ToString(),
                ["{{START_MONTH}}"] = GetMonthName(startDate),
                ["{{START_YEAR}}"] = startDate.Year.ToString(),
                ["{{END_DAY}}"] = endDate.Day.ToString(),
                ["{{END_MONTH}}"] = GetMonthName(endDate),
                ["{{END_YEAR}}"] = endDate.Year.ToString(),

                ["{{CL_NAME}}"] = GetValueOrDash(client.Name),
                ["{{CL_ADDR}}"] = GetValueOrDash(client.Address),
                ["{{CL_INN}}"] = GetValueOrDash(client.Inn),
                ["{{CL_KPP}}"] = GetValueOrDash(clientKpp),
                ["{{CL_OGRN}}"] = GetValueOrDash(client.Ogrn),
                ["{{CL_RS}}"] = clientRs,
                ["{{CL_BANK}}"] = clientBank,
                ["{{CL_KS}}"] = clientKs,
                ["{{CL_BIK}}"] = clientBic,
                ["{{CL_SIGN_SHORT}}"] = clientSignerShort,

                ["{{EXE_NAME}}"] = Executor.FullName,
                ["{{EXE_ADDR}}"] = Executor.Address,
                ["{{EXE_INN}}"] = Executor.Inn,
                ["{{EXE_KPP}}"] = Executor.Kpp,
                ["{{EXE_OGRN}}"] = Executor.Ogrn,
                ["{{EXE_RS}}"] = Executor.BankAccount,
                ["{{EXE_BANK}}"] = Executor.BankName,
                ["{{EXE_KS}}"] = Executor.CorrAccount,
                ["{{EXE_BIK}}"] = Executor.Bic,
                ["{{EXE_PHONE}}"] = Executor.Phone,
                ["{{EXE_SIGN_SHORT}}"] = Executor.SignerShortName
            };
        }

        private static void ReplacePlaceholders(string docxPath, Dictionary<string, string> placeholders)
        {
            using var wordDocument = WordprocessingDocument.Open(docxPath, true);

            ReplaceInRoot(wordDocument.MainDocumentPart?.Document, placeholders);

            if (wordDocument.MainDocumentPart != null)
            {
                foreach (var headerPart in wordDocument.MainDocumentPart.HeaderParts)
                    ReplaceInRoot(headerPart.Header, placeholders);

                foreach (var footerPart in wordDocument.MainDocumentPart.FooterParts)
                    ReplaceInRoot(footerPart.Footer, placeholders);
            }

            wordDocument.MainDocumentPart?.Document?.Save();
        }

        private static void ReplaceInRoot(OpenXmlElement? root, Dictionary<string, string> placeholders)
        {
            if (root == null)
                return;

            foreach (var text in root.Descendants<Text>())
            {
                foreach (var pair in placeholders)
                {
                    if (text.Text.Contains(pair.Key, StringComparison.Ordinal))
                    {
                        text.Text = text.Text.Replace(pair.Key, pair.Value);
                    }
                }
            }
        }

        private static string GetClientBasis(ClientInfo client)
        {
            string explicitBasis = GetOptionalStringProperty(client, "ContractBasis", "ActingBasis", "SignerBasis", "Basis");
            if (!string.IsNullOrWhiteSpace(explicitBasis))
                return explicitBasis;

            if (string.Equals(client.ClientType, "ИП", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(client.ClientType, "ИПГКФХ", StringComparison.OrdinalIgnoreCase))
            {
                return "Листа записи";
            }

            return "Устава";
        }

        private static string GetOptionalStringProperty(object? source, params string[] propertyNames)
        {
            if (source == null)
                return string.Empty;

            foreach (string propertyName in propertyNames)
            {
                PropertyInfo? property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                    continue;

                string? value = property.GetValue(source)?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static string BuildShortName(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "________________";

            var parts = fullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (parts.Count == 0)
                return "________________";

            string surname = parts[0];
            string initials = string.Concat(parts.Skip(1).Select(p => $"{char.ToUpperInvariant(p[0])}."));

            return string.IsNullOrWhiteSpace(initials)
                ? surname
                : $"{surname} {initials}";
        }

        private static string BuildContractNumber(ClientInfo client, DateTime date)
        {
            return $"{date:yyMMdd}-{client.Id:D3}";
        }

        private static string GetMonthName(DateTime date)
        {
            return date.ToString("MMMM", RuCulture);
        }

        private static string GetValueOrDash(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value;
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
