using System;
using System.Collections.Generic;
using System.Text;

namespace ClientAccountApp
{
    public sealed class CounterpartyDueDiligenceFacts
    {
        public string Inn { get; set; } = "";
        public DateTime CheckedAt { get; set; } = DateTime.Now;

        public string Name { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Kpp { get; set; } = "";
        public string Ogrn { get; set; } = "";
        public string Okved { get; set; } = "";
        public string Status { get; set; } = "";
        public string RegistrationDate { get; set; } = "";
        public string LiquidationDate { get; set; } = "";
        public string DirectorName { get; set; } = "";
        public string DirectorPost { get; set; } = "";
        public string Address { get; set; } = "";
        public string AddressInvalidity { get; set; } = "";
        public string ManagementDisqualified { get; set; } = "";
        public string Capital { get; set; } = "";

        public string TaxSystem { get; set; } = "";
        public string FinanceYear { get; set; } = "";
        public string FinanceIncome { get; set; } = "";
        public string FinanceExpense { get; set; } = "";
        public string FinanceRevenue { get; set; } = "";
        public string FinanceDebt { get; set; } = "";
        public string FinancePenalty { get; set; } = "";

        public string NpdStatus { get; set; } = "";
        public string NpdMessage { get; set; } = "";

        public string RostrudStatus { get; set; } = "";
        public string RostrudSummary { get; set; } = "";

        public string RnpStatus { get; set; } = "";
        public string RnpSummary { get; set; } = "";
        public bool RnpNeedsManualCheck { get; set; }

        public List<string> Sources { get; set; } = new();
        public List<string> SubstantialRiskFactors { get; set; } = new();
        public List<string> ModerateRiskFactors { get; set; } = new();
        public List<string> InformationalFactors { get; set; } = new();
        public List<string> ManualChecks { get; set; } = new();
        public List<string> DocumentsToRequest { get; set; } = new();

        public string PreliminaryRiskLevel { get; set; } = "Недостаточно данных";

        public string ToPromptText()
        {
            var sb = new StringBuilder();

            sb.AppendLine("СТРУКТУРИРОВАННОЕ ДОСЬЕ КОНТРАГЕНТА");
            sb.AppendLine("========================================");
            sb.AppendLine();

            sb.AppendLine("1. Основные сведения");
            sb.AppendLine($"ИНН: {Empty(Inn)}");
            sb.AppendLine($"Наименование: {Empty(Name)}");
            sb.AppendLine($"Полное наименование: {Empty(FullName)}");
            sb.AppendLine($"КПП: {Empty(Kpp)}");
            sb.AppendLine($"ОГРН / ОГРНИП: {Empty(Ogrn)}");
            sb.AppendLine($"Дата регистрации: {Empty(RegistrationDate)}");
            sb.AppendLine($"Статус: {Empty(Status)}");
            sb.AppendLine($"Дата ликвидации: {Empty(LiquidationDate)}");
            sb.AppendLine($"Руководитель / ИП: {Empty(DirectorName)}");
            sb.AppendLine($"Должность руководителя: {Empty(DirectorPost)}");
            sb.AppendLine($"Адрес: {Empty(Address)}");
            sb.AppendLine($"Основной ОКВЭД: {Empty(Okved)}");
            sb.AppendLine($"Уставный капитал: {Empty(Capital)}");
            sb.AppendLine();

            sb.AppendLine("2. Регистрационные и риск-признаки");
            sb.AppendLine($"Недостоверность адреса: {Empty(AddressInvalidity)}");
            sb.AppendLine($"Дисквалификация руководителя: {Empty(ManagementDisqualified)}");
            sb.AppendLine();

            sb.AppendLine("3. Финансовые сведения");
            sb.AppendLine($"Система налогообложения: {Empty(TaxSystem)}");
            sb.AppendLine($"Год финансовых сведений: {Empty(FinanceYear)}");
            sb.AppendLine($"Доходы: {Empty(FinanceIncome)}");
            sb.AppendLine($"Расходы: {Empty(FinanceExpense)}");
            sb.AppendLine($"Выручка: {Empty(FinanceRevenue)}");
            sb.AppendLine($"Задолженность: {Empty(FinanceDebt)}");
            sb.AppendLine($"Штрафы / пени: {Empty(FinancePenalty)}");
            sb.AppendLine();

            sb.AppendLine("4. Статус НПД");
            sb.AppendLine($"Статус НПД: {Empty(NpdStatus)}");
            sb.AppendLine($"Сообщение ФНС: {Empty(NpdMessage)}");
            sb.AppendLine();

            sb.AppendLine("5. Роструд");
            sb.AppendLine($"Статус проверки Роструда: {Empty(RostrudStatus)}");
            sb.AppendLine($"Сводка Роструда: {Empty(RostrudSummary)}");
            sb.AppendLine();

            sb.AppendLine("6. ЕИС / РНП");
            sb.AppendLine($"Статус РНП: {Empty(RnpStatus)}");
            sb.AppendLine($"Сводка РНП: {Empty(RnpSummary)}");
            sb.AppendLine($"Требуется ручная проверка карточки РНП: {(RnpNeedsManualCheck ? "да" : "нет")}");
            sb.AppendLine();

            sb.AppendLine("7. Использованные источники");
            AppendList(sb, Sources);
            sb.AppendLine();

            sb.AppendLine("8. Предварительные факторы риска");
            sb.AppendLine("Существенные факторы:");
            AppendList(sb, SubstantialRiskFactors);

            sb.AppendLine("Умеренные факторы:");
            AppendList(sb, ModerateRiskFactors);

            sb.AppendLine("Информационные факторы:");
            AppendList(sb, InformationalFactors);
            sb.AppendLine();

            sb.AppendLine("9. Что проверить вручную");
            AppendList(sb, ManualChecks);
            sb.AppendLine();

            sb.AppendLine("10. Документы, которые рекомендуется запросить");
            AppendList(sb, DocumentsToRequest);
            sb.AppendLine();

            sb.AppendLine("11. Предварительный уровень риска");
            sb.AppendLine(PreliminaryRiskLevel);

            return sb.ToString();
        }

        private static void AppendList(StringBuilder sb, List<string> items)
        {
            if (items == null || items.Count == 0)
            {
                sb.AppendLine("- не выявлено по полученным данным");
                return;
            }

            foreach (string item in items)
            {
                if (!string.IsNullOrWhiteSpace(item))
                    sb.AppendLine("- " + item.Trim());
            }
        }

        private static string Empty(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "не указано"
                : value.Trim();
        }
    }
}