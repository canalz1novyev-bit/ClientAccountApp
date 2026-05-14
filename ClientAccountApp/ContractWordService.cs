using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;

namespace ClientAccountApp
{
    public static class ContractWordService
    {
        private static readonly CultureInfo RuCulture = new("ru-RU");

        // ─────────────────────────────────────────────────────────────────────
        // ПУБЛИЧНЫЙ API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Генерирует договор через мастер: обе стороны переданы явно.
        /// Возвращает абсолютный путь к созданному .docx.
        /// </summary>
        public static string GenerateContractDocx(ClientContract contract, ContractParty party1, ContractParty party2)
        {
            if (!party1.IsValid)
                throw new InvalidOperationException("Не заполнены реквизиты стороны 1 (Наименование и ИНН обязательны).");
            if (!party2.IsValid)
                throw new InvalidOperationException("Не заполнены реквизиты стороны 2 (Наименование и ИНН обязательны).");

            string templatePath = ContractTypeDefinitions.ResolveTemplatePath(contract.ContractType);
            return GenerateFromTemplate(contract, party1, party2, templatePath);
        }

        /// <summary>
        /// Обратная совместимость: генерирует договор только по clientId.
        /// Сторона 1 берётся из профиля активной организации.
        /// Сторона 2 — из базы клиентов.
        /// </summary>
        public static string GenerateContractDocx(int clientId)
        {
            using var db = new AppDbContext();

            var client = db.Clients.AsNoTracking().FirstOrDefault(c => c.Id == clientId)
                ?? throw new InvalidOperationException("Клиент не найден.");

            var org = ActiveOrganizationService.GetRequired();

            var bank = db.BankAccounts.AsNoTracking()
                .Where(a => a.ClientInfoId == clientId)
                .OrderBy(a => a.BankName)
                .FirstOrDefault();

            var party1 = ContractParty.FromOrganizationProfile(org);
            var party2 = ContractParty.FromClientInfo(client, bank);

            // Используем фиктивный контракт с дефолтными значениями
            var contract = new ClientContract
            {
                ContractType = "services",
                City = "",
                Subject = "",
                VatMode = "Без НДС"
            };

            string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "contract_template_niatek.docx");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Шаблон договора не найден.", templatePath);

            return GenerateFromTemplate(contract, party1, party2, templatePath);
        }

        // ─────────────────────────────────────────────────────────────────────
        // ВНУТРЕННЯЯ ЛОГИКА
        // ─────────────────────────────────────────────────────────────────────

        private static string GenerateFromTemplate(
            ClientContract contract,
            ContractParty party1,
            ContractParty party2,
            string templatePath)
        {
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Шаблон не найден: {templatePath}");

            DateTime contractDate = DateTime.Today;
            string contractNumber = string.IsNullOrWhiteSpace(contract.ContractNumber)
                ? BuildContractNumber(party2, contractDate)
                : contract.ContractNumber;

            string tempFolder = Path.Combine(AppPaths.AppDataFolder, "Temp", "Contracts");
            Directory.CreateDirectory(tempFolder);

            string safeName = MakeSafeFileName(party2.Name);
            string outputPath = Path.Combine(tempFolder,
                $"Договор_{safeName}_{contractDate:yyyyMMdd}.docx");

            File.Copy(templatePath, outputPath, true);

            var placeholders = BuildPlaceholders(contract, party1, party2, contractDate, contractNumber);
            ReplacePlaceholders(outputPath, placeholders);

            return outputPath;
        }

        private static Dictionary<string, string> BuildPlaceholders(
            ClientContract contract,
            ContractParty party1,
            ContractParty party2,
            DateTime date,
            string contractNumber)
        {
            // Даты действия
            DateTime startDate = contract.ValidFrom ?? date;
            DateTime endDate = contract.ValidTo ?? new DateTime(date.Year, 12, 31);

            // Роли сторон по типу договора
            var typeInfo = ContractTypeDefinitions.GetByKey(contract.ContractType);

            // Сумма и НДС
            decimal amount = contract.Amount;
            string vatMode = contract.VatMode ?? "Без НДС";
            decimal vatAmount = vatMode == "НДС 20%" ? Math.Round(amount * 20 / 120, 2)
                              : vatMode == "НДС 10%" ? Math.Round(amount * 10 / 110, 2)
                              : 0;
            decimal amountWithVat = amount + vatAmount;

            string city = string.IsNullOrWhiteSpace(contract.City) ? "_______________" : contract.City;
            string subject = string.IsNullOrWhiteSpace(contract.Subject) ? typeInfo.DefaultSubject : contract.Subject;

            return new Dictionary<string, string>
            {
                // Шапка договора
                ["{{NUM}}"] = contractNumber,
                ["{{CITY}}"] = city,
                ["{{DATE_DM}}"] = $"\"{date.Day}\" {GetMonthName(date)}",
                ["{{DATE_FULL}}"] = date.ToString("dd MMMM yyyy", RuCulture),
                ["{{YEAR}}"] = date.Year.ToString(),

                // Роли сторон
                ["{{P1_ROLE}}"] = typeInfo.Party1Role,
                ["{{P2_ROLE}}"] = typeInfo.Party2Role,

                // Предмет и суммы
                ["{{SUBJECT}}"] = subject,
                ["{{AMOUNT}}"] = FormatMoney(amount),
                ["{{VAT_AMOUNT}}"] = FormatMoney(vatAmount),
                ["{{AMOUNT_WITH_VAT}}"] = FormatMoney(amountWithVat),
                ["{{VAT_MODE}}"] = vatMode,

                // Сроки
                ["{{START_DAY}}"] = startDate.Day.ToString(),
                ["{{START_MONTH}}"] = GetMonthName(startDate),
                ["{{START_YEAR}}"] = startDate.Year.ToString(),
                ["{{END_DAY}}"] = endDate.Day.ToString(),
                ["{{END_MONTH}}"] = GetMonthName(endDate),
                ["{{END_YEAR}}"] = endDate.Year.ToString(),

                // ── Сторона 1 (EXE) ──────────────────────────────────────────
                ["{{EXE_CONTRACT_NAME}}"] = Dash(party1.Name),
                ["{{EXE_NAME}}"] = Dash(party1.Name),
                ["{{EXE_SHORT_NAME}}"] = Dash(party1.ShortName),
                ["{{EXE_BASIS}}"] = Dash(party1.SignerBasis),
                ["{{EXE_SIGNER}}"] = Dash(party1.SignerFullName),
                ["{{EXE_SIGN_SHORT}}"] = Dash(party1.SignerShortName),
                ["{{EXE_POSITION}}"] = Dash(party1.SignerPosition),
                ["{{EXE_INN}}"] = Dash(party1.Inn),
                ["{{EXE_KPP}}"] = Dash(party1.Kpp),
                ["{{EXE_OGRN}}"] = Dash(party1.Ogrn),
                ["{{EXE_ADDR}}"] = Dash(party1.Address),
                ["{{EXE_PHONE}}"] = Dash(party1.Phone),
                ["{{EXE_RS}}"] = Dash(party1.SettlementAccount),
                ["{{EXE_BANK}}"] = Dash(party1.BankName),
                ["{{EXE_KS}}"] = Dash(party1.CorrespondentAccount),
                ["{{EXE_BIK}}"] = Dash(party1.BankBic),

                // ── Сторона 2 (CL) ───────────────────────────────────────────
                ["{{CL_CONTRACT_NAME}}"] = Dash(party2.Name),
                ["{{CL_NAME}}"] = Dash(party2.Name),
                ["{{CL_SHORT_NAME}}"] = Dash(party2.ShortName),
                ["{{CL_BASIS}}"] = Dash(party2.SignerBasis),
                ["{{CL_SIGNER}}"] = Dash(party2.SignerFullName),
                ["{{CL_SIGN_SHORT}}"] = Dash(party2.SignerShortName),
                ["{{CL_POSITION}}"] = Dash(party2.SignerPosition),
                ["{{CL_INN}}"] = Dash(party2.Inn),
                ["{{CL_KPP}}"] = Dash(party2.Kpp),
                ["{{CL_OGRN}}"] = Dash(party2.Ogrn),
                ["{{CL_ADDR}}"] = Dash(party2.Address),
                ["{{CL_PHONE}}"] = Dash(party2.Phone),
                ["{{CL_RS}}"] = Dash(party2.SettlementAccount),
                ["{{CL_BANK}}"] = Dash(party2.BankName),
                ["{{CL_KS}}"] = Dash(party2.CorrespondentAccount),
                ["{{CL_BIK}}"] = Dash(party2.BankBic)
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // ЗАМЕНА ПЛЕЙСХОЛДЕРОВ В DOCX
        // ─────────────────────────────────────────────────────────────────────

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
        }

        private static void ReplaceInRoot(OpenXmlElement? root, Dictionary<string, string> placeholders)
        {
            if (root == null) return;

            foreach (var paragraph in root.Descendants<Paragraph>())
            {
                // Собираем текст всего абзаца, заменяем, раскладываем обратно
                string fullText = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));

                bool changed = false;
                foreach (var (key, value) in placeholders)
                {
                    if (fullText.Contains(key, StringComparison.Ordinal))
                    {
                        fullText = fullText.Replace(key, value, StringComparison.Ordinal);
                        changed = true;
                    }
                }

                if (!changed) continue;

                // Записываем результат в первый Text-элемент абзаца, остальные очищаем
                var textNodes = paragraph.Descendants<Text>().ToList();
                if (textNodes.Count == 0) continue;

                textNodes[0].Text = fullText;
                for (int i = 1; i < textNodes.Count; i++)
                    textNodes[i].Text = string.Empty;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ─────────────────────────────────────────────────────────────────────

        private static string BuildContractNumber(ContractParty party2, DateTime date)
        {
            // Формат: ГГММДД-ИНН(последние 4 цифры)
            string innSuffix = party2.Inn.Length >= 4
                ? party2.Inn[^4..]
                : party2.Inn.PadLeft(4, '0');
            return $"{date:yyMMdd}-{innSuffix}";
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("N2", RuCulture);
        }

        private static string GetMonthName(DateTime date)
        {
            return date.ToString("MMMM", RuCulture);
        }

        private static string Dash(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value;
        }

        private static string MakeSafeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            return fileName.Length > 60 ? fileName[..60] : fileName;
        }
    }
}
