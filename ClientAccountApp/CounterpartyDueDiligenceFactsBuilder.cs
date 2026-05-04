using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClientAccountApp
{
    public static class CounterpartyDueDiligenceFactsBuilder
    {
        public static CounterpartyDueDiligenceFacts Build(CounterpartyAutoCheckResult result)
        {
            var facts = new CounterpartyDueDiligenceFacts
            {
                Inn = result.Inn,
                CheckedAt = result.CheckedAt
            };

            foreach (var source in result.Sources)
            {
                facts.Sources.Add($"{source.SourceName}: {source.Status}");

                if (source.SourceName.Contains("DaData", StringComparison.OrdinalIgnoreCase))
                    ApplyDaDataFacts(facts, source);

                if (source.SourceName.Contains("НПД", StringComparison.OrdinalIgnoreCase))
                    ApplyNpdFacts(facts, source);

                if (source.SourceName.Contains("Роструд", StringComparison.OrdinalIgnoreCase))
                    ApplyRostrudFacts(facts, source);

                if (source.SourceName.Contains("ЕИС", StringComparison.OrdinalIgnoreCase) ||
                    source.SourceName.Contains("РНП", StringComparison.OrdinalIgnoreCase))
                    ApplyRnpFacts(facts, source);
            }

            BuildRiskFactors(facts);
            BuildManualChecks(facts);
            BuildDocumentsToRequest(facts);
            DetectRiskLevel(facts);

            return facts;
        }

        private static void ApplyDaDataFacts(
            CounterpartyDueDiligenceFacts facts,
            CounterpartySourceCheckResult source)
        {
            string details = source.Details ?? "";

            facts.Name = FirstNotEmpty(
                Extract(details, "Наименование"),
                Extract(details, "Полное наименование"),
                ExtractFromSummary(source.Summary));

            facts.FullName = Extract(details, "Полное наименование");
            facts.Kpp = Extract(details, "КПП");
            facts.Ogrn = Extract(details, "ОГРН / ОГРНИП");
            facts.Okved = Extract(details, "ОКВЭД");
            facts.Status = Extract(details, "Статус");
            facts.RegistrationDate = Extract(details, "Дата регистрации");
            facts.LiquidationDate = Extract(details, "Дата ликвидации");
            facts.DirectorName = Extract(details, "Руководитель");
            facts.DirectorPost = Extract(details, "Должность руководителя");
            facts.Address = FirstNotEmpty(
                Extract(details, "Полный адрес"),
                Extract(details, "Адрес"));

            facts.AddressInvalidity = Extract(details, "Недостоверность адреса");
            facts.ManagementDisqualified = Extract(details, "Дисквалификация руководителя");
            facts.Capital = Extract(details, "Уставный капитал");

            facts.TaxSystem = Extract(details, "Система налогообложения");
            facts.FinanceYear = Extract(details, "Финансы — год");
            facts.FinanceIncome = Extract(details, "Финансы — доходы");
            facts.FinanceExpense = Extract(details, "Финансы — расходы");
            facts.FinanceRevenue = Extract(details, "Финансы — выручка");
            facts.FinanceDebt = Extract(details, "Финансы — задолженность");
            facts.FinancePenalty = Extract(details, "Финансы — штрафы/пени");
        }

        private static void ApplyNpdFacts(
            CounterpartyDueDiligenceFacts facts,
            CounterpartySourceCheckResult source)
        {
            string details = source.Details ?? "";

            facts.NpdStatus = FirstNotEmpty(
                Extract(details, "Статус НПД"),
                source.Summary);

            facts.NpdMessage = Extract(details, "Сообщение ФНС");
        }

        private static void ApplyRostrudFacts(
            CounterpartyDueDiligenceFacts facts,
            CounterpartySourceCheckResult source)
        {
            facts.RostrudStatus = source.Status;
            facts.RostrudSummary = source.Summary;

            if (source.Status.Contains("Не найдено", StringComparison.OrdinalIgnoreCase))
            {
                facts.InformationalFactors.Add(
                    "В выбранном наборе Роструда плановые проверки работодателя не выявлены. Это не заменяет полную проверку трудовых рисков.");
            }
            else if (source.IsRealDataReceived)
            {
                facts.ModerateRiskFactors.Add(
                    "По Роструду получены сведения, которые требуют оценки содержания проверки.");
            }
        }

        private static void ApplyRnpFacts(
            CounterpartyDueDiligenceFacts facts,
            CounterpartySourceCheckResult source)
        {
            facts.RnpStatus = source.Status;
            facts.RnpSummary = source.Summary;

            if (source.Status.Contains("Возможное", StringComparison.OrdinalIgnoreCase))
            {
                facts.RnpNeedsManualCheck = true;

                facts.ModerateRiskFactors.Add(
                    "По ЕИС / РНП найдено возможное совпадение по ИНН. Факт включения в РНП требует ручного подтверждения карточки записи.");
            }
            else if (source.Status.Contains("Не найдено", StringComparison.OrdinalIgnoreCase))
            {
                facts.InformationalFactors.Add(
                    "По публичной проверке ЕИС / РНП подтверждённого совпадения не выявлено.");
            }
        }

        private static void BuildRiskFactors(CounterpartyDueDiligenceFacts facts)
        {
            if (!string.IsNullOrWhiteSpace(facts.Status) &&
                !facts.Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                facts.SubstantialRiskFactors.Add(
                    $"Статус контрагента отличается от ACTIVE: {facts.Status}.");
            }

            if (!string.IsNullOrWhiteSpace(facts.LiquidationDate))
            {
                facts.SubstantialRiskFactors.Add(
                    $"В данных указана дата ликвидации: {facts.LiquidationDate}.");
            }

            if (HasMeaningfulValue(facts.AddressInvalidity))
            {
                facts.ModerateRiskFactors.Add(
                    "В данных присутствуют сведения о недостоверности адреса или связанные отметки.");
            }

            if (HasMeaningfulValue(facts.ManagementDisqualified))
            {
                facts.ModerateRiskFactors.Add(
                    "В данных присутствуют сведения по дисквалификации руководителя.");
            }

            if (HasNonZeroMoney(facts.FinanceDebt))
            {
                facts.ModerateRiskFactors.Add(
                    $"В финансовых данных указана задолженность: {facts.FinanceDebt}.");
            }

            if (HasNonZeroMoney(facts.FinancePenalty))
            {
                facts.ModerateRiskFactors.Add(
                    $"В финансовых данных указаны штрафы/пени: {facts.FinancePenalty}.");
            }

            if (!string.IsNullOrWhiteSpace(facts.NpdStatus) &&
                facts.NpdStatus.Contains("не является", StringComparison.OrdinalIgnoreCase))
            {
                facts.InformationalFactors.Add(
                    "Контрагент не является плательщиком НПД на дату проверки. Это само по себе не является негативным фактором.");
            }

            if (facts.SubstantialRiskFactors.Count == 0 &&
                facts.ModerateRiskFactors.Count == 0)
            {
                facts.InformationalFactors.Add(
                    "Явные существенные риск-признаки по полученным данным не выявлены.");
            }
        }

        private static void BuildManualChecks(CounterpartyDueDiligenceFacts facts)
        {
            facts.ManualChecks.Add("Сверить актуальную выписку ЕГРЮЛ / ЕГРИП на дату сделки.");
            facts.ManualChecks.Add("Проверить полномочия подписанта договора.");
            facts.ManualChecks.Add("Сверить банковские реквизиты с карточкой контрагента и договором.");
            facts.ManualChecks.Add("Проверить соответствие предмета договора основному или дополнительным видам деятельности.");

            if (facts.RnpNeedsManualCheck)
            {
                facts.ManualChecks.Add("Вручную открыть карточку ЕИС / РНП и подтвердить, относится ли найденное совпадение именно к данному контрагенту.");
            }

            if (facts.Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) == false)
            {
                facts.ManualChecks.Add("Проверить регистрационный статус контрагента в официальной выписке.");
            }
        }

        private static void BuildDocumentsToRequest(CounterpartyDueDiligenceFacts facts)
        {
            facts.DocumentsToRequest.Add("Актуальная выписка ЕГРЮЛ / ЕГРИП.");
            facts.DocumentsToRequest.Add("Карточка организации или ИП с реквизитами.");
            facts.DocumentsToRequest.Add("Проект договора и документы по сделке.");
            facts.DocumentsToRequest.Add("Документы, подтверждающие полномочия подписанта.");

            if (!string.IsNullOrWhiteSpace(facts.NpdStatus) &&
                facts.NpdStatus.Contains("является плательщиком", StringComparison.OrdinalIgnoreCase))
            {
                facts.DocumentsToRequest.Add("Подтверждение статуса плательщика НПД на дату сделки.");
            }
        }

        private static void DetectRiskLevel(CounterpartyDueDiligenceFacts facts)
        {
            if (facts.SubstantialRiskFactors.Count > 0)
            {
                facts.PreliminaryRiskLevel = "Повышенный риск";
                return;
            }

            if (facts.ModerateRiskFactors.Count > 0)
            {
                facts.PreliminaryRiskLevel = "Умеренный риск";
                return;
            }

            if (!string.IsNullOrWhiteSpace(facts.Name) ||
                !string.IsNullOrWhiteSpace(facts.Ogrn))
            {
                facts.PreliminaryRiskLevel = "Низкий риск";
                return;
            }

            facts.PreliminaryRiskLevel = "Недостаточно данных";
        }

        private static string Extract(string text, string label)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var match = Regex.Match(
                text,
                Regex.Escape(label) + @"\s*:\s*(.+)",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return "";

            string value = match.Groups[1].Value.Trim();

            int lineBreakIndex = value.IndexOf('\n');

            if (lineBreakIndex >= 0)
                value = value.Substring(0, lineBreakIndex).Trim();

            return value;
        }

        private static string ExtractFromSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
                return "";

            var match = Regex.Match(
                summary,
                @"Найдена запись DaData:\s*(.+?)(\.|$)",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return "";

            return match.Groups[1].Value.Trim();
        }

        private static string FirstNotEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value) && HasMeaningfulValue(value))
                    return value.Trim();
            }

            return "";
        }

        private static bool HasMeaningfulValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.Trim();

            return !normalized.Equals("null", StringComparison.OrdinalIgnoreCase) &&
                   !normalized.Equals("false", StringComparison.OrdinalIgnoreCase) &&
                   !normalized.Equals("0", StringComparison.OrdinalIgnoreCase) &&
                   !normalized.Equals("0,00 ₽", StringComparison.OrdinalIgnoreCase) &&
                   !normalized.Equals("не указано", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasNonZeroMoney(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return !value.Trim().StartsWith("0,00", StringComparison.OrdinalIgnoreCase);
        }
    }
}