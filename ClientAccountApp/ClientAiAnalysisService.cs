using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public static class ClientAiAnalysisService
    {
        private static readonly CultureInfo RuCulture = new("ru-RU");

        public static async Task<string> AnalyzeClientAsync(int clientId)
        {

            var organization = ActiveOrganizationService.GetRequired();

            using var db = new AppDbContext();

            var client = db.Clients
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == clientId);

            if (client == null)
                throw new InvalidOperationException("Клиент не найден.");

            var signatures = db.DigitalSignatures
                .AsNoTracking()
                .Where(x => x.ClientInfoId == client.Id)
                .OrderBy(x => x.ExpiresDate)
                .ToList();

            var accounts = db.BankAccounts
                .AsNoTracking()
                .Where(x => x.ClientInfoId == client.Id)
                .OrderBy(x => x.BankName)
                .ToList();

            var notes = db.ClientNotes
                .AsNoTracking()
                .Where(x => x.ClientInfoId == client.Id)
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .ToList();

            var files = db.ClientFiles
                .AsNoTracking()
                .Where(x => x.ClientInfoId == client.Id)
                .OrderByDescending(x => x.AddedAt)
                .Take(10)
                .ToList();

            var contract = db.ClientContracts
                .AsNoTracking()
                .FirstOrDefault(x =>
                    x.OrganizationProfileId == organization.Id &&
                    x.ClientInfoId == client.Id);

            var invoices = db.Invoices
                .AsNoTracking()
                .Where(x =>
                    x.ClientInfoId == client.Id &&
                    x.OrganizationProfileId == organization.Id)
                .OrderByDescending(x => x.InvoiceDate)
                .Take(10)
                .ToList();

            string localCheck = BuildLocalClientCheck(
                organization,
                client,
                contract,
                signatures,
                accounts,
                notes,
                files,
                invoices);

            try
            {
                var settings = AiSettingsService.Load();

                if (!settings.IsEnabled)
                {
                    return localCheck +
                        Environment.NewLine +
                        Environment.NewLine +
                        "Примечание: GigaChat выключен в настройках. Показана локальная проверка клиента.";
                }

                string aiResponse = await GigaChatAiService.AskAsync(
                    BuildSystemPrompt(),
                    BuildUserPrompt(
                        organization,
                        client,
                        contract,
                        signatures,
                        accounts,
                        notes,
                        files,
                        invoices,
                        localCheck));

                if (IsAiAnswerTooShort(aiResponse))
                {
                    return localCheck +
                        Environment.NewLine +
                        Environment.NewLine +
                        "Примечание: GigaChat вернул слишком короткий ответ. Показана локальная проверка клиента.";
                }

                return localCheck +
                    Environment.NewLine +
                    Environment.NewLine +
                    "6. Дополнительный комментарий ИИ" +
                    Environment.NewLine +
                    aiResponse.Trim();
            }
            catch (Exception ex)
            {
                return localCheck +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Примечание: расширенный ИИ-анализ не выполнен. Причина: {ex.Message}";
            }
        }

        private static string BuildSystemPrompt()
        {
            return
                "Ты помощник бухгалтера в Российской Федерации. " +
                "Проверь карточку клиента и дай дополнительный деловой комментарий. " +
                "Не возвращай только заголовки. " +
                "Не выдумывай факты, даты, суммы, документы и статусы. " +
                "Если данных нет, прямо укажи, что они отсутствуют. " +
                "Пиши кратко, структурированно и по делу. " +
                "Не используй Markdown-таблицы.";
        }

        private static string BuildUserPrompt(
            OrganizationProfile organization,
            ClientInfo client,
            ClientContract? contract,
            List<DigitalSignature> signatures,
            List<BankAccount> accounts,
            List<ClientNote> notes,
            List<ClientFile> files,
            List<Invoice> invoices,
            string localCheck)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Ниже приведена локальная проверка карточки клиента.");
            sb.AppendLine("Дай дополнительный комментарий бухгалтера: что важно проверить, какие риски есть, какие действия выполнить.");
            sb.AppendLine("Не повторяй весь текст полностью. Добавь только полезный комментарий.");
            sb.AppendLine();
            sb.AppendLine(localCheck);
            sb.AppendLine();

            sb.AppendLine("Рабочая организация:");
            sb.AppendLine($"Наименование: {Empty(organization.Name)}");
            sb.AppendLine($"ИНН: {Empty(organization.Inn)}");
            sb.AppendLine();

            sb.AppendLine("Клиент:");
            sb.AppendLine($"Тип: {Empty(client.ClientType)}");
            sb.AppendLine($"Наименование / ФИО: {Empty(client.Name)}");
            sb.AppendLine($"ИНН: {Empty(client.Inn)}");
            sb.AppendLine($"ОГРН / ОГРНИП: {Empty(client.Ogrn)}");
            sb.AppendLine($"Адрес: {Empty(client.Address)}");
            sb.AppendLine($"Руководитель / предприниматель: {Empty(client.DirectorFullName)}");
            sb.AppendLine();

            return sb.ToString();
        }

        private static bool IsAiAnswerTooShort(string? response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return true;

            string trimmed = response.Trim();

            if (trimmed.Length < 250)
                return true;

            int lineCount = trimmed
                .Replace("\r\n", "\n")
                .Split('\n')
                .Count(x => !string.IsNullOrWhiteSpace(x));

            return lineCount < 5;
        }

        private static string BuildLocalClientCheck(
            OrganizationProfile organization,
            ClientInfo client,
            ClientContract? contract,
            List<DigitalSignature> signatures,
            List<BankAccount> accounts,
            List<ClientNote> notes,
            List<ClientFile> files,
            List<Invoice> invoices)
        {
            var sb = new StringBuilder();

            bool hasInn = !string.IsNullOrWhiteSpace(client.Inn);
            bool hasName = !string.IsNullOrWhiteSpace(client.Name);
            bool hasAddress = !string.IsNullOrWhiteSpace(client.Address);
            bool hasDirector = !string.IsNullOrWhiteSpace(client.DirectorFullName);

            bool hasContractSigned = contract != null &&
                string.Equals(contract.Status, "Договор подписан", StringComparison.OrdinalIgnoreCase);

            bool hasSignatures = signatures.Count > 0;
            bool hasActiveSignature = signatures.Any(x => x.ExpiresDate.Date >= DateTime.Today);
            bool hasAccounts = accounts.Count > 0;
            bool hasFiles = files.Count > 0;
            bool hasInvoices = invoices.Count > 0;

            int problemCount = 0;

            if (!hasInn) problemCount++;
            if (!hasName) problemCount++;
            if (!hasAddress) problemCount++;
            if (!hasDirector) problemCount++;
            if (!hasContractSigned) problemCount++;
            if (!hasSignatures) problemCount++;
            if (hasSignatures && !hasActiveSignature) problemCount++;
            if (!hasAccounts) problemCount++;

            sb.AppendLine("1. Общая оценка клиента");

            if (problemCount == 0)
            {
                sb.AppendLine("Клиент выглядит готовым к работе. Основные данные заполнены, договор подписан, ЭЦП и банковские счета присутствуют.");
            }
            else if (problemCount <= 3)
            {
                sb.AppendLine("Клиент частично готов к работе, но есть пункты, которые желательно проверить или дополнить.");
            }
            else
            {
                sb.AppendLine("Клиент требует внимания. В карточке отсутствует несколько важных данных или документов.");
            }

            sb.AppendLine();
            sb.AppendLine("2. Что заполнено корректно");

            bool hasAnyGood = false;

            if (hasName)
            {
                hasAnyGood = true;
                sb.AppendLine($"✓ Наименование / ФИО: {client.Name}");
            }

            if (hasInn)
            {
                hasAnyGood = true;
                sb.AppendLine($"✓ ИНН: {client.Inn}");
            }

            if (!string.IsNullOrWhiteSpace(client.Ogrn))
            {
                hasAnyGood = true;
                sb.AppendLine($"✓ ОГРН / ОГРНИП: {client.Ogrn}");
            }

            if (hasAddress)
            {
                hasAnyGood = true;
                sb.AppendLine($"✓ Адрес: {client.Address}");
            }

            if (hasDirector)
            {
                hasAnyGood = true;
                sb.AppendLine($"✓ Руководитель / предприниматель: {client.DirectorFullName}");
            }

            if (hasContractSigned)
            {
                hasAnyGood = true;
                sb.AppendLine("✓ Договор по текущей организации подписан.");
            }

            if (hasActiveSignature)
            {
                hasAnyGood = true;
                sb.AppendLine("✓ Есть действующая ЭЦП.");
            }

            if (hasAccounts)
            {
                hasAnyGood = true;
                sb.AppendLine($"✓ Банковские счета добавлены: {accounts.Count}.");
            }

            if (hasFiles)
            {
                hasAnyGood = true;
                sb.AppendLine($"✓ Файлы прикреплены: {files.Count}.");
            }

            if (hasInvoices)
            {
                hasAnyGood = true;
                sb.AppendLine($"✓ Есть счета по текущей организации: {invoices.Count}.");
            }

            if (!hasAnyGood)
            {
                sb.AppendLine("Пока нет заполненных ключевых блоков, которые можно отметить как готовые.");
            }

            sb.AppendLine();
            sb.AppendLine("3. Что требует внимания");

            bool hasAttention = false;

            if (!hasName)
            {
                hasAttention = true;
                sb.AppendLine("• Не заполнено наименование / ФИО клиента.");
            }

            if (!hasInn)
            {
                hasAttention = true;
                sb.AppendLine("• Не заполнен ИНН клиента.");
            }

            if (!hasAddress)
            {
                hasAttention = true;
                sb.AppendLine("• Не заполнен юридический адрес.");
            }

            if (!hasDirector)
            {
                hasAttention = true;
                sb.AppendLine("• Не заполнен руководитель / предприниматель.");
            }

            if (contract == null)
            {
                hasAttention = true;
                sb.AppendLine("• Нет договорной записи по текущей организации.");
            }
            else if (!hasContractSigned)
            {
                hasAttention = true;
                sb.AppendLine($"• Договор по текущей организации не подписан. Текущий статус: {Empty(contract.Status)}.");
            }

            if (!hasSignatures)
            {
                hasAttention = true;
                sb.AppendLine("• ЭЦП не добавлена.");
            }
            else
            {
                foreach (var signature in signatures)
                {
                    int daysLeft = (signature.ExpiresDate.Date - DateTime.Today).Days;

                    if (daysLeft < 0)
                    {
                        hasAttention = true;
                        sb.AppendLine($"• ЭЦП «{Empty(signature.CertificationAuthority)}» просрочена на {Math.Abs(daysLeft)} дн.");
                    }
                    else if (daysLeft <= 30)
                    {
                        hasAttention = true;
                        sb.AppendLine($"• ЭЦП «{Empty(signature.CertificationAuthority)}» истекает через {daysLeft} дн.");
                    }
                }
            }

            if (!hasAccounts)
            {
                hasAttention = true;
                sb.AppendLine("• Банковские счета не добавлены.");
            }

            if (!hasFiles)
            {
                hasAttention = true;
                sb.AppendLine("• Файлы клиента не прикреплены.");
            }

            if (!hasAttention)
            {
                sb.AppendLine("• Критичных замечаний по карточке клиента не найдено.");
            }

            sb.AppendLine();
            sb.AppendLine("4. Рекомендуемые действия");

            int actionNumber = 1;

            if (!hasInn || !hasName || !hasAddress)
            {
                sb.AppendLine($"{actionNumber}. Дозаполнить основные реквизиты клиента.");
                actionNumber++;
            }

            if (!hasContractSigned)
            {
                sb.AppendLine($"{actionNumber}. Проверить договор по текущей организации: сформировать или отметить подписанным.");
                actionNumber++;
            }

            if (!hasSignatures || !hasActiveSignature)
            {
                sb.AppendLine($"{actionNumber}. Добавить или продлить ЭЦП клиента.");
                actionNumber++;
            }

            if (!hasAccounts)
            {
                sb.AppendLine($"{actionNumber}. Добавить банковский счёт клиента.");
                actionNumber++;
            }

            if (!hasFiles)
            {
                sb.AppendLine($"{actionNumber}. Прикрепить основные документы клиента.");
                actionNumber++;
            }

            var unpaidInvoices = invoices
                .Where(x =>
                    !string.Equals(x.Status, "Оплачен", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(x.Status, "Отменен", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(x.Status, "Отменён", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (unpaidInvoices.Count > 0)
            {
                sb.AppendLine($"{actionNumber}. Проверить неоплаченные счета: {unpaidInvoices.Count}, сумма {FormatMoney(unpaidInvoices.Sum(x => x.TotalWithVat))}.");
                actionNumber++;
            }

            if (actionNumber == 1)
            {
                sb.AppendLine("1. Дополнительных срочных действий не требуется.");
            }

            sb.AppendLine();
            sb.AppendLine("5. Краткий итог");

            if (problemCount == 0)
            {
                sb.AppendLine("Клиент готов к полноценной работе в системе.");
            }
            else
            {
                sb.AppendLine("Клиента можно вести в системе, но перед активной работой желательно закрыть указанные замечания.");
            }

            return sb.ToString();
        }

        private static string Empty(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "не указано"
                : value.Trim();
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("N2", RuCulture) + " ₽";
        }
    }
}