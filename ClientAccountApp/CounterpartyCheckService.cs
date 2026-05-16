using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Text.RegularExpressions;

namespace ClientAccountApp
{
    public sealed class CounterpartyAutoCheckResult
    {
        public string Inn { get; set; } = "";
        public DateTime CheckedAt { get; set; } = DateTime.Now;
        public List<CounterpartySourceCheckResult> Sources { get; set; } = new();

        public bool HasAnyRealFacts
        {
            get
            {
                return Sources.Any(x => x.IsRealDataReceived);
            }
        }
    }

    public sealed class CounterpartySourceCheckResult
    {
        public string SourceName { get; set; } = "";
        public string Status { get; set; } = "";
        public bool IsRealDataReceived { get; set; }
        public string Summary { get; set; } = "";
        public string Details { get; set; } = "";
        public string TechnicalInfo { get; set; } = "";
    }

    public static class CounterpartyCheckService
    {
        private static readonly HttpClient Http = new HttpClient();
        private static readonly SemaphoreSlim NpdRequestSemaphore = new SemaphoreSlim(1, 1);
        private static DateTime _lastNpdRequestAt = DateTime.MinValue;
        private const string RostrudCheckPlanUrl =
    "https://www.rostrud.ru/opendata/7712345678-chekplan/data-20160527T0000-structure-20160101T0000.json";
        private const string NpdStatusUrl =
            "https://statusnpd.nalog.ru:443/api/v1/tracker/taxpayer_status";
        private const string DaDataPartyUrl =
            "https://suggestions.dadata.ru/suggestions/api/4_1/rs/findById/party";
        private const string EisRnpSearchUrl =
    "https://zakupki.gov.ru/epz/dishonestsupplier/search/results.html";


        // API-ключ DaData хранится в Windows Credential Vault через CounterpartyApiKeysService.
        // НИКОГДА не хранить ключ в исходниках — он попадёт в git и станет публичным.
        // Настроить ключ можно в окне "Параметры → Источники проверок контрагентов".

        static CounterpartyCheckService()
        {
            Http.Timeout = TimeSpan.FromSeconds(35);

            try
            {
                Http.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "NIATEC.Client/0.9 CounterpartyCheck");
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning("CounterpartyCheckService.UserAgent", ex.Message);
            }
        }

        public static async Task<CounterpartyAutoCheckResult> RunAutomaticExternalCheckAsync(string inn)
        {
            inn = NormalizeInn(inn);

            if (string.IsNullOrWhiteSpace(inn))
                throw new InvalidOperationException("Введите ИНН контрагента.");

            var result = new CounterpartyAutoCheckResult
            {
                Inn = inn,
                CheckedAt = DateTime.Now
            };

            result.Sources.Add(await CheckDaDataPartyAsync(inn));
            result.Sources.Add(await CheckNpdStatusAsync(inn));
            result.Sources.Add(await CheckRostrudCheckPlanAsync(inn));
            result.Sources.Add(await CheckEisRnpAsync(inn));

            return result;
        }
        private static async Task<CounterpartySourceCheckResult> CheckRostrudCheckPlanAsync(string inn)
        {
            inn = NormalizeInn(inn);

            if (string.IsNullOrWhiteSpace(inn))
            {
                return new CounterpartySourceCheckResult
                {
                    SourceName = "Роструд / План проверок",
                    Status = "Ошибка",
                    IsRealDataReceived = false,
                    Summary = "ИНН не указан.",
                    Details = "Проверка плана проверок Роструда не выполнена."
                };
            }

            try
            {
                string url =
                    RostrudCheckPlanUrl +
                    "?nPageSize=20&iNumPage=1&filter[inn]=" +
                    Uri.EscapeDataString(inn);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                request.Headers.TryAddWithoutValidation("User-Agent", "NIATEC.Client/0.9");

                using var response = await Http.SendAsync(request);

                string responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new CounterpartySourceCheckResult
                    {
                        SourceName = "Роструд / План проверок",
                        Status = "Ошибка",
                        IsRealDataReceived = false,
                        Summary = "Роструд вернул ошибку при запросе плана проверок.",
                        Details = "Не удалось получить сведения по ИНН.",
                        TechnicalInfo =
                            $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}" +
                            Environment.NewLine +
                            Limit(responseText, 3000)
                    };
                }

                if (string.IsNullOrWhiteSpace(responseText))
                {
                    return new CounterpartySourceCheckResult
                    {
                        SourceName = "Роструд / План проверок",
                        Status = "Пустой ответ",
                        IsRealDataReceived = false,
                        Summary = "Роструд вернул пустой ответ.",
                        Details = "Проверка плана проверок не выполнена.",
                        TechnicalInfo = url
                    };
                }

                string trimmed = responseText.TrimStart();

                if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
                {
                    return new CounterpartySourceCheckResult
                    {
                        SourceName = "Роструд / План проверок",
                        Status = "Ошибка формата ответа",
                        IsRealDataReceived = false,
                        Summary = "Роструд вернул не JSON-ответ.",
                        Details = "Возможно, структура набора данных изменилась или сервис временно вернул HTML-страницу.",
                        TechnicalInfo = Limit(responseText, 3000)
                    };
                }

                return ParseRostrudCheckPlanResponse(inn, responseText, url);
            }
            catch (Exception ex)
            {
                return new CounterpartySourceCheckResult
                {
                    SourceName = "Роструд / План проверок",
                    Status = "Ошибка",
                    IsRealDataReceived = false,
                    Summary = "Проверка плана проверок Роструда не выполнена.",
                    Details = "Возможные причины: нет интернета, сервис Роструда временно недоступен, структура открытых данных изменилась.",
                    TechnicalInfo = ex.Message
                };
            }
        }
        private static async Task<CounterpartySourceCheckResult> CheckEisRnpAsync(string inn)
        {
            inn = NormalizeInn(inn);

            if (string.IsNullOrWhiteSpace(inn))
            {
                return new CounterpartySourceCheckResult
                {
                    SourceName = "ЕИС Госзакупки / РНП",
                    Status = "Ошибка",
                    IsRealDataReceived = false,
                    Summary = "ИНН не указан.",
                    Details = "Проверка реестра недобросовестных поставщиков не выполнена."
                };
            }

            try
            {
                string url =
                    EisRnpSearchUrl +
                    "?searchString=" + Uri.EscapeDataString(inn) +
                    "&morphology=on" +
                    "&sortBy=UPDATE_DATE" +
                    "&pageNumber=1" +
                    "&recordsPerPage=_50";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                request.Headers.TryAddWithoutValidation(
                    "Accept",
                    "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                request.Headers.TryAddWithoutValidation(
                    "Accept-Language",
                    "ru-RU,ru;q=0.9,en;q=0.8");

                request.Headers.TryAddWithoutValidation(
                    "User-Agent",
                    "Mozilla/5.0 NIATEC.Client/0.9");

                using var response = await Http.SendAsync(request);

                string responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new CounterpartySourceCheckResult
                    {
                        SourceName = "ЕИС Госзакупки / РНП",
                        Status = "Ошибка",
                        IsRealDataReceived = false,
                        Summary = "ЕИС Госзакупки вернула ошибку при проверке РНП.",
                        Details = "Не удалось получить сведения по ИНН.",
                        TechnicalInfo =
                            $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}" +
                            Environment.NewLine +
                            Limit(responseText, 3000)
                    };
                }

                if (string.IsNullOrWhiteSpace(responseText))
                {
                    return new CounterpartySourceCheckResult
                    {
                        SourceName = "ЕИС Госзакупки / РНП",
                        Status = "Пустой ответ",
                        IsRealDataReceived = false,
                        Summary = "ЕИС Госзакупки вернула пустой ответ.",
                        Details = "Проверка РНП не выполнена.",
                        TechnicalInfo = url
                    };
                }

                return ParseEisRnpHtmlResponse(inn, responseText, url);
            }
            catch (Exception ex)
            {
                return new CounterpartySourceCheckResult
                {
                    SourceName = "ЕИС Госзакупки / РНП",
                    Status = "Ошибка",
                    IsRealDataReceived = false,
                    Summary = "Проверка РНП не выполнена.",
                    Details =
                        "Возможные причины: нет интернета, сайт ЕИС временно недоступен, изменилась структура страницы или включена защита от автоматических запросов.",
                    TechnicalInfo = ex.Message
                };
            }
        }
        private static CounterpartySourceCheckResult ParseEisRnpHtmlResponse(
    string inn,
    string html,
    string sourceUrl)
        {
            try
            {
                string plainText = ConvertHtmlToPlainText(html);

                bool containsInn = plainText.Contains(inn, StringComparison.OrdinalIgnoreCase);

                bool noResults =
                    plainText.Contains("ничего не найдено", StringComparison.OrdinalIgnoreCase) ||
                    plainText.Contains("по вашему запросу ничего не найдено", StringComparison.OrdinalIgnoreCase) ||
                    plainText.Contains("записи не найдены", StringComparison.OrdinalIgnoreCase);

                if (noResults && !containsInn)
                {
                    return new CounterpartySourceCheckResult
                    {
                        SourceName = "ЕИС Госзакупки / РНП",
                        Status = "Не найдено",
                        IsRealDataReceived = true,
                        Summary = $"По ИНН {inn} сведения в РНП на странице ЕИС не найдены.",
                        Details =
                            "Контрагент не найден в результате публичного поиска РНП ЕИС по указанному ИНН." +
                            Environment.NewLine +
                            "Это не является юридическим заключением: при важной сделке рекомендуется дополнительно проверить РНП вручную.",
                        TechnicalInfo =
                            "Источник: публичный поиск РНП на zakupki.gov.ru." + Environment.NewLine +
                            sourceUrl
                    };
                }

                if (containsInn)
                {
                    string snippet = ExtractTextSnippet(plainText, inn, 1800);

                    return new CounterpartySourceCheckResult
                    {
                        SourceName = "ЕИС Госзакупки / РНП",
                        Status = "Возможное совпадение",
                        IsRealDataReceived = true,
                        Summary = $"На странице РНП ЕИС найдено упоминание ИНН {inn}. Требуется проверка карточки записи.",
                        Details =
                            "Фрагмент найденных сведений:" +
                            Environment.NewLine +
                            snippet,
                        TechnicalInfo =
                            "Источник: публичный поиск РНП на zakupki.gov.ru." + Environment.NewLine +
                            "Режим: бесплатная проверка через публичную страницу поиска." + Environment.NewLine +
                            sourceUrl
                    };
                }

                return new CounterpartySourceCheckResult
                {
                    SourceName = "ЕИС Госзакупки / РНП",
                    Status = "Не удалось однозначно распознать",
                    IsRealDataReceived = false,
                    Summary = "ЕИС вернула страницу, но приложение не смогло однозначно определить наличие или отсутствие записи в РНП.",
                    Details =
                        "Такое возможно, если сайт изменил разметку, результаты подгружаются скриптами или требуется дополнительный параметр поиска.",
                    TechnicalInfo =
                        "Источник: публичный поиск РНП на zakupki.gov.ru." + Environment.NewLine +
                        sourceUrl + Environment.NewLine +
                        Limit(plainText, 3000)
                };
            }
            catch (Exception ex)
            {
                return new CounterpartySourceCheckResult
                {
                    SourceName = "ЕИС Госзакупки / РНП",
                    Status = "Ошибка разбора",
                    IsRealDataReceived = false,
                    Summary = "Ответ ЕИС получен, но приложение не смогло разобрать страницу РНП.",
                    Details = "Возможно, структура HTML изменилась.",
                    TechnicalInfo = ex.Message + Environment.NewLine + Limit(html, 3000)
                };
            }
        }
        private static string ConvertHtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return "";

            string text = html;

            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                "<script[\\s\\S]*?</script>",
                " ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                "<style[\\s\\S]*?</style>",
                " ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                "<[^>]+>",
                " ");

            text = WebUtility.HtmlDecode(text);

            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                "\\s+",
                " ");

            return text.Trim();
        }

        private static string ExtractTextSnippet(string text, string searchValue, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            int index = text.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
                return Limit(text, maxLength);

            int start = Math.Max(0, index - maxLength / 3);

            int length = Math.Min(maxLength, text.Length - start);

            return text.Substring(start, length).Trim();
        }
        private static CounterpartySourceCheckResult ParseRostrudCheckPlanResponse(
    string inn,
    string responseJson,
    string sourceUrl)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);

                JsonElement root = doc.RootElement;

                JsonElement records = ExtractRostrudRecords(root);

                int recordsCount = records.ValueKind == JsonValueKind.Array
                    ? records.GetArrayLength()
                    : 0;

                int totalCount = ExtractRostrudTotalCount(root, recordsCount);

                if (recordsCount == 0)
                {
                    return new CounterpartySourceCheckResult
                    {
                        SourceName = "Роструд / План проверок",
                        Status = "Не найдено",
                        IsRealDataReceived = true,
                        Summary = $"По ИНН {inn} плановые проверки Роструда в выбранном наборе не найдены.",
                        Details =
                            $"Найдено записей: 0." + Environment.NewLine +
                            "Это не означает отсутствие любых трудовых рисков, а только отсутствие записей в данном наборе открытых данных.",
                        TechnicalInfo =
                            "Источник: открытые данные Роструда, набор chekplan." + Environment.NewLine +
                            sourceUrl
                    };
                }

                var details = new StringBuilder();

                details.AppendLine($"Найдено записей: {recordsCount}");
                details.AppendLine($"Всего записей по пагинации: {totalCount}");
                details.AppendLine();
                details.AppendLine("Первые записи:");

                int index = 0;

                foreach (JsonElement record in records.EnumerateArray())
                {
                    index++;

                    details.AppendLine();
                    details.AppendLine($"{index}. {FirstNotEmpty(GetAnyJsonString(record, "NAME", "name", "ORG_NAME", "ORGANIZATION_NAME"), "организация без названия")}");

                    AppendAny(details, record, "ИНН", "INN", "inn", "IDN", "INN_NUM");
                    AppendAny(details, record, "Субъект РФ", "SUBJECT", "subject", "REGION", "region");
                    AppendAny(details, record, "Адрес", "ADDRESS", "address", "ADDR");
                    AppendAny(details, record, "Орган контроля", "CONTROL_ORGAN", "CONTROL_ORG", "INSPECTION", "TERRITORIAL_ORGAN", "ORGAN");
                    AppendAny(details, record, "Цель / предмет проверки", "PURPOSE", "SUBJECT_CHECK", "CHECK_SUBJECT", "OBJECTIVE");
                    AppendAny(details, record, "Дата начала", "DATE_FROM", "START_DATE", "DATE_START", "BEGIN_DATE");
                    AppendAny(details, record, "Дата окончания", "DATE_TO", "END_DATE", "DATE_END");
                    AppendAny(details, record, "Месяц проверки", "MONTH", "CHECK_MONTH");
                    AppendAny(details, record, "Год", "YEAR", "CHECK_YEAR");

                    if (index >= 10)
                        break;
                }

                string summary;

                if (recordsCount == 1)
                {
                    summary = $"По ИНН {inn} найдена 1 запись в плане проверок Роструда.";
                }
                else
                {
                    summary = $"По ИНН {inn} найдено записей в плане проверок Роструда: {recordsCount}.";
                }

                return new CounterpartySourceCheckResult
                {
                    SourceName = "Роструд / План проверок",
                    Status = "Данные получены",
                    IsRealDataReceived = true,
                    Summary = summary,
                    Details = details.ToString().Trim(),
                    TechnicalInfo =
                        "Источник: открытые данные Роструда, набор chekplan." + Environment.NewLine +
                        sourceUrl
                };
            }
            catch (Exception ex)
            {
                return new CounterpartySourceCheckResult
                {
                    SourceName = "Роструд / План проверок",
                    Status = "Ошибка разбора",
                    IsRealDataReceived = false,
                    Summary = "Ответ Роструда получен, но приложение не смогло разобрать его структуру.",
                    Details = "Возможно, структура JSON отличается от ожидаемой.",
                    TechnicalInfo = ex.Message + Environment.NewLine + Limit(responseJson, 3000)
                };
            }
        }
        public static string BuildPlainTextReport(CounterpartyAutoCheckResult result)
        {
            var report = new StringBuilder();

            report.AppendLine("АВТОМАТИЧЕСКАЯ ПРОВЕРКА КОНТРАГЕНТА");
            report.AppendLine("========================================");
            report.AppendLine();
            report.AppendLine($"ИНН: {result.Inn}");
            report.AppendLine($"Дата проверки: {result.CheckedAt:dd.MM.yyyy HH:mm}");
            report.AppendLine();
            report.AppendLine("Используемый источник:");
            report.AppendLine("DaData — организация / ИП по ИНН.");
            report.AppendLine();

            foreach (var source in result.Sources)
            {
                report.AppendLine(source.SourceName.ToUpperInvariant());
                report.AppendLine("----------------------------------------");
                report.AppendLine($"Статус: {source.Status}");
                report.AppendLine();

                if (!string.IsNullOrWhiteSpace(source.Summary))
                {
                    report.AppendLine("Кратко:");
                    report.AppendLine(source.Summary);
                    report.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(source.Details))
                {
                    report.AppendLine("Детали:");
                    report.AppendLine(source.Details);
                    report.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(source.TechnicalInfo))
                {
                    report.AppendLine("Техническая информация:");
                    report.AppendLine(source.TechnicalInfo);
                    report.AppendLine();
                }
            }

            report.AppendLine("ИТОГ АВТОСБОРА");
            report.AppendLine("----------------------------------------");

            if (result.HasAnyRealFacts)
            {
                report.AppendLine("Сведения по контрагенту получены автоматически через DaData.");
            }
            else
            {
                report.AppendLine("Автоматически получить сведения по контрагенту не удалось.");
            }

            report.AppendLine();
            report.AppendLine("Следующий шаг: сформировать ИИ-отчёт по автоматически собранным данным.");

            return report.ToString();
        }
        private static JsonElement ExtractRostrudRecords(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
                return root;

            if (root.ValueKind == JsonValueKind.Object)
            {
                string[] possibleNames =
                {
            "data",
            "Data",
            "items",
            "Items",
            "records",
            "Records",
            "list",
            "List"
        };

                foreach (string name in possibleNames)
                {
                    if (root.TryGetProperty(name, out JsonElement value) &&
                        value.ValueKind == JsonValueKind.Array)
                    {
                        return value;
                    }
                }
            }

            return default;
        }

        private static int ExtractRostrudTotalCount(JsonElement root, int fallback)
        {
            if (root.ValueKind != JsonValueKind.Object)
                return fallback;

            if (root.TryGetProperty("pagination", out JsonElement pagination) &&
                pagination.ValueKind == JsonValueKind.Object)
            {
                int count = GetJsonIntFlexible(pagination, "NavRecordCount");

                if (count > 0)
                    return count;
            }

            string[] possibleCountFields =
            {
        "NavRecordCount",
        "recordCount",
        "RecordCount",
        "total",
        "Total",
        "count",
        "Count"
    };

            foreach (string field in possibleCountFields)
            {
                int count = GetJsonIntFlexible(root, field);

                if (count > 0)
                    return count;
            }

            return fallback;
        }

        private static int GetJsonIntFlexible(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
                return 0;

            if (value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out int intValue))
            {
                return intValue;
            }

            if (int.TryParse(GetElementAsText(value), out int parsed))
                return parsed;

            return 0;
        }

        private static void AppendAny(
            StringBuilder sb,
            JsonElement element,
            string title,
            params string[] propertyNames)
        {
            string value = GetAnyJsonString(element, propertyNames);

            if (!string.IsNullOrWhiteSpace(value))
            {
                sb.AppendLine($"   {title}: {value}");
            }
        }

        private static string GetAnyJsonString(JsonElement element, params string[] propertyNames)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return "";

            foreach (string propertyName in propertyNames)
            {
                if (element.TryGetProperty(propertyName, out JsonElement value))
                {
                    string text = GetElementAsText(value);

                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }

            return "";
        }
        public static async Task<string> BuildAiReportFromAutomaticCheckAsync(CounterpartyAutoCheckResult result)
        {
            if (result == null)
                throw new InvalidOperationException("Сначала выполните автоматическую проверку.");

            var facts = CounterpartyDueDiligenceFactsBuilder.Build(result);
            string dossier = facts.ToPromptText();

            string systemPrompt =
                "Ты профессиональный помощник бухгалтера и специалист по должной осмотрительности контрагентов в Российской Федерации. " +
                "Твоя задача — подготовить представительский деловой отчёт для руководителя, бухгалтера или внутреннего контроля. " +
                "Работай только с фактами из структурированного досье. " +
                "Не выдумывай сведения, которых нет в досье. " +
                "Если источник не дал данных, дал возможное совпадение или требует ручной проверки — прямо укажи это. " +
                "Не делай окончательных юридических выводов. " +
                "Не используй Markdown: не пиши ###, ####, **, таблицы Markdown и декоративные символы. " +
                "Пиши связным деловым русским языком. " +
                "Верни отчёт строго в формате блоков с маркерами в квадратных скобках. " +
                "Не меняй названия маркеров.";

            string userPrompt =
                "Подготовь деловой отчёт проверки контрагента по структурированному досье.\n\n" +
                "СТРУКТУРИРОВАННОЕ ДОСЬЕ:\n" +
                dossier + "\n\n" +

                "ОБЩИЕ ПРАВИЛА:\n" +
                "1. Не переписывай досье построчно.\n" +
                "2. Не используй Markdown-разметку.\n" +
                "3. Не склеивай разделы.\n" +
                "4. Возможное совпадение в ЕИС / РНП не является подтверждённым включением в реестр. Укажи необходимость ручной проверки карточки записи.\n" +
                "5. Отсутствие записей Роструда не означает отсутствие любых трудовых рисков.\n" +
                "6. Если НПД не применяется, укажи это как факт, а не как риск.\n" +
                "7. Не завышай риск без подтверждённых оснований.\n" +
                "8. Пиши так, чтобы отчёт можно было сразу приложить к внутреннему досье контрагента.\n\n" +

                "ВЕРНИ ОТЧЁТ СТРОГО В ТАКОМ ФОРМАТЕ:\n\n" +

                "[SUMMARY]\n" +
                "Краткое деловое резюме в 5–7 предложениях.\n\n" +

                "[COUNTERPARTY_CARD]\n" +
                "Карточка контрагента: наименование, ИНН, КПП, ОГРН/ОГРНИП, дата регистрации, статус, руководитель, адрес, ОКВЭД.\n\n" +

                "[SOURCES]\n" +
                "Источники проверки и практический результат по каждому.\n\n" +

                "[REGISTRATION_STATUS]\n" +
                "Регистрационный статус, признаки ликвидации, недостоверности адреса, дисквалификации руководителя.\n\n" +

                "[NPD_STATUS]\n" +
                "Статус НПД / самозанятости и практическое значение.\n\n" +

                "[LABOUR_RISKS]\n" +
                "Данные Роструда и ограниченность этих сведений.\n\n" +

                "[RNP_STATUS]\n" +
                "Результат ЕИС / РНП и необходимость ручного подтверждения при возможном совпадении.\n\n" +

                "[RISK_FACTORS]\n" +
                "Существенные факторы:\n" +
                "Умеренные факторы:\n" +
                "Информационные факторы:\n\n" +

                "[REQUEST_DOCUMENTS]\n" +
                "Документы, которые рекомендуется запросить.\n\n" +

                "[MANUAL_CHECKS]\n" +
                "Что проверить дополнительно вручную.\n\n" +

                "[ACCOUNTANT_RECOMMENDATION]\n" +
                "Практическая рекомендация бухгалтеру.\n\n" +

                "[RISK_LEVEL]\n" +
                "Один вариант: Низкий риск, Умеренный риск, Повышенный риск, Недостаточно данных.";

            string report = await GigaChatAiService.AskAsync(systemPrompt, userPrompt);

            return CleanAiReportText(report);
        }

        private static string CleanAiReportText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            value = value.Replace("\r\n", "\n");

            value = System.Text.RegularExpressions.Regex.Replace(value, @"#{1,6}\s*", "");
            value = System.Text.RegularExpressions.Regex.Replace(value, @"\*\*(.*?)\*\*", "$1");
            value = System.Text.RegularExpressions.Regex.Replace(value, @"^\s*[-*]\s+", "• ", System.Text.RegularExpressions.RegexOptions.Multiline);
            value = System.Text.RegularExpressions.Regex.Replace(value, @"\n{3,}", "\n\n");

            string[] markers =
            {
        "[SUMMARY]",
        "[COUNTERPARTY_CARD]",
        "[SOURCES]",
        "[REGISTRATION_STATUS]",
        "[NPD_STATUS]",
        "[LABOUR_RISKS]",
        "[RNP_STATUS]",
        "[RISK_FACTORS]",
        "[REQUEST_DOCUMENTS]",
        "[MANUAL_CHECKS]",
        "[ACCOUNTANT_RECOMMENDATION]",
        "[RISK_LEVEL]"
    };

            foreach (string marker in markers)
            {
                value = value.Replace(marker, "\n\n" + marker + "\n");
            }

            value = System.Text.RegularExpressions.Regex.Replace(value, @"\n{3,}", "\n\n");

            return value.Trim();
        }
        private static async Task<CounterpartySourceCheckResult> CheckDaDataPartyAsync(string inn)
        {
            try
            {
                string apiKey = CounterpartyApiKeysService.GetDaDataApiKey();

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return new CounterpartySourceCheckResult
                    {
                        SourceName = "DaData / Организация по ИНН",
                        Status = "Ключ не указан",
                        IsRealDataReceived = false,
                        Summary = "API-ключ DaData не настроен.",
                        Details = "Откройте «Параметры → Источники проверок контрагентов» и введите ваш ключ DaData. Ключ хранится в защищённом Windows Credential Vault, в исходниках не сохраняется.",
                        TechnicalInfo = "Проверка не выполнялась."
                    };
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, DaDataPartyUrl);

                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                request.Headers.TryAddWithoutValidation("Authorization", "Token " + apiKey);

                string body = JsonSerializer.Serialize(new
                {
                    query = inn,
                    count = 1
                });

                request.Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json");

                using var response = await Http.SendAsync(request);

                string responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new CounterpartySourceCheckResult
                    {
                        SourceName = "DaData / Организация по ИНН",
                        Status = "Ошибка",
                        IsRealDataReceived = false,
                        Summary = "DaData вернула ошибку.",
                        Details = "Не удалось получить сведения по ИНН.",
                        TechnicalInfo =
                            $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}" +
                            Environment.NewLine +
                            Limit(responseText, 3000)
                    };
                }

                return ParseDaDataPartyResponse(inn, responseText);
            }
            catch (Exception ex)
            {
                return new CounterpartySourceCheckResult
                {
                    SourceName = "DaData / Организация по ИНН",
                    Status = "Ошибка",
                    IsRealDataReceived = false,
                    Summary = "Автоматическая проверка через DaData не выполнена.",
                    Details = "Возможные причины: нет интернета, неверный API-ключ, DaData временно недоступна.",
                    TechnicalInfo = ex.Message
                };
            }
        }
        private static async Task<CounterpartySourceCheckResult> CheckNpdStatusAsync(string inn)
        {
            inn = NormalizeInn(inn);

            if (string.IsNullOrWhiteSpace(inn))
            {
                return new CounterpartySourceCheckResult
                {
                    SourceName = "ФНС / Статус НПД",
                    Status = "Ошибка",
                    IsRealDataReceived = false,
                    Summary = "ИНН не указан.",
                    Details = "Проверка статуса самозанятого не выполнена."
                };
            }

            try
            {
                await NpdRequestSemaphore.WaitAsync();

                try
                {
                    TimeSpan sinceLastRequest = DateTime.Now - _lastNpdRequestAt;

                    if (_lastNpdRequestAt != DateTime.MinValue &&
                        sinceLastRequest.TotalSeconds < 31)
                    {
                        int delayMs = (int)((31 - sinceLastRequest.TotalSeconds) * 1000);

                        if (delayMs > 0)
                            await Task.Delay(delayMs);
                    }

                    _lastNpdRequestAt = DateTime.Now;

                    string requestDate = DateTime.Today.ToString("yyyy-MM-dd");

                    using var request = new HttpRequestMessage(HttpMethod.Post, NpdStatusUrl);

                    request.Headers.TryAddWithoutValidation("Accept", "application/json");

                    string body = JsonSerializer.Serialize(new
                    {
                        inn = inn,
                        requestDate = requestDate
                    });

                    request.Content = new StringContent(
                        body,
                        Encoding.UTF8,
                        "application/json");

                    using var response = await Http.SendAsync(request);

                    string responseText = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return ParseNpdErrorResponse(inn, requestDate, responseText, response.StatusCode);
                    }

                    return ParseNpdStatusResponse(inn, requestDate, responseText);
                }
                finally
                {
                    NpdRequestSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                return new CounterpartySourceCheckResult
                {
                    SourceName = "ФНС / Статус НПД",
                    Status = "Ошибка",
                    IsRealDataReceived = false,
                    Summary = "Проверка статуса НПД не выполнена.",
                    Details = "Возможные причины: нет интернета, сервис ФНС временно недоступен, превышен лимит запросов.",
                    TechnicalInfo = ex.Message
                };
            }
        }
        private static CounterpartySourceCheckResult ParseNpdStatusResponse(
    string inn,
    string requestDate,
    string responseJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);

                JsonElement root = doc.RootElement;

                bool status = false;

                if (root.TryGetProperty("status", out JsonElement statusElement) &&
                    statusElement.ValueKind == JsonValueKind.True)
                {
                    status = true;
                }

                string message = GetJsonString(root, "message");

                string summary = status
                    ? $"ИНН {inn} является плательщиком налога на профессиональный доход на дату {requestDate}."
                    : $"ИНН {inn} не является плательщиком налога на профессиональный доход на дату {requestDate}.";

                var details = new StringBuilder();

                details.AppendLine($"ИНН: {inn}");
                details.AppendLine($"Дата проверки: {requestDate}");
                details.AppendLine($"Статус НПД: {(status ? "является плательщиком НПД" : "не является плательщиком НПД")}");

                if (!string.IsNullOrWhiteSpace(message))
                    details.AppendLine($"Сообщение ФНС: {message}");

                return new CounterpartySourceCheckResult
                {
                    SourceName = "ФНС / Статус НПД",
                    Status = "Данные получены",
                    IsRealDataReceived = true,
                    Summary = summary,
                    Details = details.ToString().Trim(),
                    TechnicalInfo =
                        "Источник: публичный сервис ФНС России «Проверка статуса налогоплательщика НПД»." + Environment.NewLine +
                        "Метод: POST /api/v1/tracker/taxpayer_status."
                };
            }
            catch (Exception ex)
            {
                return new CounterpartySourceCheckResult
                {
                    SourceName = "ФНС / Статус НПД",
                    Status = "Ошибка разбора",
                    IsRealDataReceived = false,
                    Summary = "Ответ ФНС НПД получен, но приложение не смогло разобрать его структуру.",
                    Details = "Возможно, изменился формат ответа.",
                    TechnicalInfo = ex.Message + Environment.NewLine + Limit(responseJson, 3000)
                };
            }
        }

        private static CounterpartySourceCheckResult ParseNpdErrorResponse(
            string inn,
            string requestDate,
            string responseText,
            System.Net.HttpStatusCode statusCode)
        {
            string code = "";
            string message = "";

            try
            {
                using var doc = JsonDocument.Parse(responseText);

                JsonElement root = doc.RootElement;

                code = GetJsonString(root, "code");
                message = GetJsonString(root, "message");
            }
            catch (Exception ex)
            {
                // JSON не распарсился — используем сырой текст ответа как сообщение об ошибке
                AppLogger.LogWarning("CounterpartyCheckService.ParseErrorResponse", $"Не удалось разобрать JSON ответа: {ex.Message}");
                message = responseText;
            }

            var details = new StringBuilder();

            details.AppendLine($"ИНН: {inn}");
            details.AppendLine($"Дата проверки: {requestDate}");
            details.AppendLine($"HTTP статус: {(int)statusCode}");

            if (!string.IsNullOrWhiteSpace(code))
                details.AppendLine($"Код ошибки: {code}");

            if (!string.IsNullOrWhiteSpace(message))
                details.AppendLine($"Сообщение: {message}");

            string summary;

            if (code.Contains("limited", StringComparison.OrdinalIgnoreCase))
            {
                summary = "ФНС ограничила частоту запросов к сервису НПД.";
            }
            else if (code.Contains("validation", StringComparison.OrdinalIgnoreCase))
            {
                summary = "ФНС НПД вернула ошибку валидации входных данных.";
            }
            else
            {
                summary = "ФНС НПД вернула ошибку при проверке статуса.";
            }

            return new CounterpartySourceCheckResult
            {
                SourceName = "ФНС / Статус НПД",
                Status = "Ошибка",
                IsRealDataReceived = false,
                Summary = summary,
                Details = details.ToString().Trim(),
                TechnicalInfo = Limit(responseText, 3000)
            };
        }

        private static CounterpartySourceCheckResult ParseDaDataPartyResponse(string inn, string responseJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);

                JsonElement root = doc.RootElement;

                if (!root.TryGetProperty("suggestions", out JsonElement suggestions) ||
                    suggestions.ValueKind != JsonValueKind.Array ||
                    suggestions.GetArrayLength() == 0)
                {
                    return new CounterpartySourceCheckResult
                    {
                        SourceName = "DaData / Организация по ИНН",
                        Status = "Не найдено",
                        IsRealDataReceived = true,
                        Summary = $"По ИНН {inn} организация или ИП не найдены.",
                        TechnicalInfo = "DaData вернула пустой список suggestions."
                    };
                }

                JsonElement suggestion = suggestions[0];

                string value = GetJsonString(suggestion, "value");
                string unrestrictedValue = GetJsonString(suggestion, "unrestricted_value");

                if (!suggestion.TryGetProperty("data", out JsonElement data))
                {
                    return new CounterpartySourceCheckResult
                    {
                        SourceName = "DaData / Организация по ИНН",
                        Status = "Ошибка разбора",
                        IsRealDataReceived = false,
                        Summary = "DaData вернула результат без блока data.",
                        TechnicalInfo = Limit(responseJson, 3000)
                    };
                }

                var details = new StringBuilder();

                string partyType = GetJsonString(data, "type");
                string branchType = GetJsonString(data, "branch_type");

                string innValue = GetJsonString(data, "inn");
                string kpp = GetJsonString(data, "kpp");
                string ogrn = GetJsonString(data, "ogrn");
                string okpo = GetJsonString(data, "okpo");
                string okved = GetJsonString(data, "okved");

                string fullName = GetNestedJsonString(data, "name", "full_with_opf");
                string shortName = GetNestedJsonString(data, "name", "short_with_opf");

                string opfFull = GetNestedJsonString(data, "opf", "full");
                string opfShort = GetNestedJsonString(data, "opf", "short");

                string status = GetNestedJsonString(data, "state", "status");
                string statusCode = GetNestedJsonString(data, "state", "code");
                string actualityDate = FormatUnixMs(GetNestedJsonLong(data, "state", "actuality_date"));
                string registrationDate = FormatUnixMs(GetNestedJsonLong(data, "state", "registration_date"));
                string liquidationDate = FormatUnixMs(GetNestedJsonLong(data, "state", "liquidation_date"));

                string managementName = GetNestedJsonString(data, "management", "name");
                string managementPost = GetNestedJsonString(data, "management", "post");
                string managementDisqualified = GetNestedRawText(data, "management", "disqualified");

                string addressValue = GetNestedJsonString(data, "address", "value");
                string unrestrictedAddress = GetNestedJsonString(data, "address", "unrestricted_value");
                string addressInvalidity = GetNestedRawText(data, "address", "invalidity");

                string capitalType = GetNestedJsonString(data, "capital", "type");
                string capitalValue = FormatMoney(GetNestedJsonDecimal(data, "capital", "value"));

                string financeTaxSystem = GetNestedRawText(data, "finance", "tax_system");
                string financeIncome = FormatMoney(GetNestedJsonDecimal(data, "finance", "income"));
                string financeExpense = FormatMoney(GetNestedJsonDecimal(data, "finance", "expense"));
                string financeRevenue = FormatMoney(GetNestedJsonDecimal(data, "finance", "revenue"));
                string financeDebt = FormatMoney(GetNestedJsonDecimal(data, "finance", "debt"));
                string financePenalty = FormatMoney(GetNestedJsonDecimal(data, "finance", "penalty"));
                string financeYear = GetNestedRawText(data, "finance", "year");

                Append(details, "Наименование", FirstNotEmpty(shortName, fullName, value, unrestrictedValue));
                Append(details, "Полное наименование", fullName);
                Append(details, "Тип", partyType);
                Append(details, "Головная / филиал", branchType);
                Append(details, "ОПФ", FirstNotEmpty(opfShort, opfFull));
                Append(details, "ИНН", innValue);
                Append(details, "КПП", kpp);
                Append(details, "ОГРН / ОГРНИП", ogrn);
                Append(details, "ОКПО", okpo);
                Append(details, "ОКВЭД", okved);
                Append(details, "Статус", status);
                Append(details, "Код статуса", statusCode);
                Append(details, "Дата актуальности сведений", actualityDate);
                Append(details, "Дата регистрации", registrationDate);
                Append(details, "Дата ликвидации", liquidationDate);
                Append(details, "Руководитель", managementName);
                Append(details, "Должность руководителя", managementPost);
                Append(details, "Дисквалификация руководителя", managementDisqualified);
                Append(details, "Адрес", addressValue);
                Append(details, "Полный адрес", unrestrictedAddress);
                Append(details, "Недостоверность адреса", addressInvalidity);
                Append(details, "Уставный капитал", CombineCapital(capitalType, capitalValue));
                Append(details, "Система налогообложения", financeTaxSystem);
                Append(details, "Финансы — год", financeYear);
                Append(details, "Финансы — доходы", financeIncome);
                Append(details, "Финансы — расходы", financeExpense);
                Append(details, "Финансы — выручка", financeRevenue);
                Append(details, "Финансы — задолженность", financeDebt);
                Append(details, "Финансы — штрафы/пени", financePenalty);

                AppendOkveds(details, data);
                AppendAuthorities(details, data);
                AppendDocuments(details, data);

                string riskSummary = BuildDaDataRiskSummary(
                    status,
                    liquidationDate,
                    addressInvalidity,
                    managementDisqualified,
                    financeDebt,
                    financePenalty);

                string summary =
                    $"Найдена запись DaData: {FirstNotEmpty(shortName, fullName, value, "контрагент")}.";

                if (!string.IsNullOrWhiteSpace(status))
                    summary += $" Статус: {status}.";

                if (!string.IsNullOrWhiteSpace(riskSummary))
                    summary += " " + riskSummary;

                return new CounterpartySourceCheckResult
                {
                    SourceName = "DaData / Организация по ИНН",
                    Status = "Данные получены",
                    IsRealDataReceived = true,
                    Summary = summary,
                    Details = details.ToString().Trim(),
                    TechnicalInfo =
                        "Источник: DaData findById/party." + Environment.NewLine +
                        "Сведения получены автоматически по ИНН."
                };
            }
            catch (Exception ex)
            {
                return new CounterpartySourceCheckResult
                {
                    SourceName = "DaData / Организация по ИНН",
                    Status = "Ошибка разбора",
                    IsRealDataReceived = false,
                    Summary = "DaData вернула ответ, но приложение не смогло разобрать его структуру.",
                    Details = "Возможно, изменился формат ответа или часть полей имеет неожиданный тип.",
                    TechnicalInfo = ex.Message + Environment.NewLine + Limit(responseJson, 3000)
                };
            }
        }

        private static void AppendOkveds(StringBuilder sb, JsonElement data)
        {
            if (!data.TryGetProperty("okveds", out JsonElement okveds) ||
                okveds.ValueKind != JsonValueKind.Array ||
                okveds.GetArrayLength() == 0)
            {
                return;
            }

            sb.AppendLine();
            sb.AppendLine("ОКВЭДы:");

            int index = 0;

            foreach (JsonElement okved in okveds.EnumerateArray())
            {
                index++;

                string code = GetJsonString(okved, "code");
                string name = GetJsonString(okved, "name");
                bool main = GetJsonBool(okved, "main");

                sb.AppendLine($"- {(main ? "основной" : "доп.")} {code} — {name}");

                if (index >= 10)
                    break;
            }
        }

        private static void AppendAuthorities(StringBuilder sb, JsonElement data)
        {
            if (!data.TryGetProperty("authorities", out JsonElement authorities) ||
                authorities.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            sb.AppendLine();
            sb.AppendLine("Регистрационные органы:");

            AppendAuthority(sb, authorities, "fts_registration", "ФНС регистрации");
            AppendAuthority(sb, authorities, "fts_report", "ФНС отчётности");
            AppendAuthority(sb, authorities, "pf", "ПФР / СФР");
            AppendAuthority(sb, authorities, "sif", "ФСС / СФР");
        }

        private static void AppendAuthority(StringBuilder sb, JsonElement authorities, string propertyName, string title)
        {
            if (!authorities.TryGetProperty(propertyName, out JsonElement item) ||
                item.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            string code = GetJsonString(item, "code");
            string name = GetJsonString(item, "name");

            if (!string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(name))
                sb.AppendLine($"- {title}: {code} {name}".Trim());
        }

        private static void AppendDocuments(StringBuilder sb, JsonElement data)
        {
            if (!data.TryGetProperty("documents", out JsonElement documents) ||
                documents.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            sb.AppendLine();
            sb.AppendLine("Документы / реестры:");

            AppendDocumentInfo(sb, documents, "smb", "МСП");
            AppendDocumentInfo(sb, documents, "fts_registration", "Регистрация ФНС");
            AppendDocumentInfo(sb, documents, "fts_report", "Учёт в ФНС");
        }

        private static void AppendDocumentInfo(StringBuilder sb, JsonElement documents, string propertyName, string title)
        {
            if (!documents.TryGetProperty(propertyName, out JsonElement item) ||
                item.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            string type = GetJsonString(item, "type");
            string category = GetJsonString(item, "category");
            string issueDate = FormatUnixMs(GetJsonLong(item, "issue_date"));

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(type))
                parts.Add(type);

            if (!string.IsNullOrWhiteSpace(category))
                parts.Add("категория: " + category);

            if (!string.IsNullOrWhiteSpace(issueDate))
                parts.Add("дата: " + issueDate);

            if (parts.Count > 0)
                sb.AppendLine($"- {title}: {string.Join(", ", parts)}");
        }

        private static string BuildDaDataRiskSummary(
            string status,
            string liquidationDate,
            string addressInvalidity,
            string managementDisqualified,
            string financeDebt,
            string financePenalty)
        {
            var risks = new List<string>();

            if (!string.IsNullOrWhiteSpace(status) &&
                !status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                risks.Add("статус не ACTIVE");
            }

            if (!string.IsNullOrWhiteSpace(liquidationDate))
                risks.Add("есть дата ликвидации");

            if (!IsNullText(addressInvalidity))
                risks.Add("есть сведения о недостоверности адреса");

            if (!IsNullText(managementDisqualified))
                risks.Add("есть сведения по дисквалификации руководителя");

            if (!IsEmptyMoney(financeDebt))
                risks.Add("есть сведения о задолженности");

            if (!IsEmptyMoney(financePenalty))
                risks.Add("есть сведения о штрафах/пенях");

            if (risks.Count == 0)
                return "Явных риск-признаков в полученных данных не выделено.";

            return "Риск-признаки: " + string.Join("; ", risks) + ".";
        }

        private static string NormalizeInn(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            return new string(value.Where(char.IsDigit).ToArray());
        }

        private static void Append(StringBuilder sb, string title, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (IsNullText(value))
                return;

            sb.AppendLine($"{title}: {value.Trim()}");
        }

        private static string FirstNotEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value) && !IsNullText(value))
                    return value.Trim();
            }

            return "";
        }

        private static string CombineCapital(string capitalType, string capitalValue)
        {
            if (string.IsNullOrWhiteSpace(capitalType) && string.IsNullOrWhiteSpace(capitalValue))
                return "";

            if (string.IsNullOrWhiteSpace(capitalType))
                return capitalValue;

            if (string.IsNullOrWhiteSpace(capitalValue))
                return capitalType;

            return capitalType + " — " + capitalValue;
        }

        private static bool IsNullText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            string normalized = value.Trim();

            return normalized.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("\"\"", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEmptyMoney(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            return value.StartsWith("0,00", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatMoney(decimal? value)
        {
            if (!value.HasValue)
                return "";

            return value.Value.ToString("N2", new CultureInfo("ru-RU")) + " ₽";
        }

        private static string FormatUnixMs(long? value)
        {
            if (!value.HasValue || value.Value <= 0)
                return "";

            try
            {
                return DateTimeOffset
                    .FromUnixTimeMilliseconds(value.Value)
                    .LocalDateTime
                    .ToString("dd.MM.yyyy");
            }
            catch
            {
                return "";
            }
        }

        private static string GetJsonString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
                return "";

            return GetElementAsText(value);
        }

        private static string GetNestedJsonString(JsonElement element, string objectName, string propertyName)
        {
            if (!element.TryGetProperty(objectName, out JsonElement nested) ||
                nested.ValueKind != JsonValueKind.Object)
            {
                return "";
            }

            return GetJsonString(nested, propertyName);
        }

        private static string GetNestedRawText(JsonElement element, string objectName, string propertyName)
        {
            if (!element.TryGetProperty(objectName, out JsonElement nested) ||
                nested.ValueKind != JsonValueKind.Object)
            {
                return "";
            }

            if (!nested.TryGetProperty(propertyName, out JsonElement value))
                return "";

            if (value.ValueKind == JsonValueKind.Null)
                return "";

            return GetElementAsText(value);
        }

        private static long? GetJsonLong(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
                return null;

            if (value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt64(out long longValue))
            {
                return longValue;
            }

            if (long.TryParse(GetElementAsText(value), out long parsed))
                return parsed;

            return null;
        }

        private static long? GetNestedJsonLong(JsonElement element, string objectName, string propertyName)
        {
            if (!element.TryGetProperty(objectName, out JsonElement nested) ||
                nested.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return GetJsonLong(nested, propertyName);
        }

        private static decimal? GetNestedJsonDecimal(JsonElement element, string objectName, string propertyName)
        {
            if (!element.TryGetProperty(objectName, out JsonElement nested) ||
                nested.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!nested.TryGetProperty(propertyName, out JsonElement value))
                return null;

            if (value.ValueKind == JsonValueKind.Null)
                return null;

            if (value.ValueKind == JsonValueKind.Number &&
                value.TryGetDecimal(out decimal decimalValue))
            {
                return decimalValue;
            }

            if (decimal.TryParse(
                    GetElementAsText(value),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out decimal parsed))
            {
                return parsed;
            }

            return null;
        }

        private static bool GetJsonBool(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
                return false;

            return value.ValueKind == JsonValueKind.True;
        }

        private static string GetElementAsText(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? "",
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "",
                _ => element.ToString()
            };
        }

        private static string Limit(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength) + "...";
        }
    }
}