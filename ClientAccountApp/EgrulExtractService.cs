using Microsoft.Playwright;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public static class EgrulExtractService
    {
        public static async Task<EgrulExtractResult> DownloadExtractAsync(
            ClientInfo client,
            bool headless = false,
            CancellationToken cancellationToken = default)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            string query = BuildSearchQuery(client);

            if (string.IsNullOrWhiteSpace(query))
                throw new InvalidOperationException("У клиента не заполнены ОГРН/ОГРНИП и ИНН. Нечего отправлять на сервис ФНС.");

            string tempFolder = Path.Combine(AppPaths.AppDataFolder, "Temp", "Egrul");
            Directory.CreateDirectory(tempFolder);

            using var playwright = await Playwright.CreateAsync();

            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = headless,
                SlowMo = headless ? 0 : 150
            });

            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                AcceptDownloads = true,
                Locale = "ru-RU"
            });

            context.SetDefaultTimeout(45_000);
            context.SetDefaultNavigationTimeout(45_000);

            var page = await context.NewPageAsync();

            await page.GotoAsync("https://egrul.nalog.ru/index.html", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load
            });

            cancellationToken.ThrowIfCancellationRequested();

            await FillSearchQueryAsync(page, query);
            await ClickFindAsync(page);
            await WaitForSearchOutcomeAsync(page, cancellationToken);

            var getDocumentControl = await ResolveGetDocumentControlAsync(page);

            var download = await page.RunAndWaitForDownloadAsync(
                async () => { await getDocumentControl.ClickAsync(); },
                new PageRunAndWaitForDownloadOptions
                {
                    Timeout = 90_000
                });

            cancellationToken.ThrowIfCancellationRequested();

            var failure = await download.FailureAsync();
            if (!string.IsNullOrWhiteSpace(failure))
                throw new InvalidOperationException($"ФНС вернула ошибку скачивания: {failure}");

            string suggestedFileName = SanitizeFileName(download.SuggestedFilename);
            if (string.IsNullOrWhiteSpace(suggestedFileName))
            {
                suggestedFileName = $"Выписка_{query}.pdf";
            }
            else if (!suggestedFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                suggestedFileName += ".pdf";
            }

            string targetTempPath = GetUniqueFilePath(tempFolder, suggestedFileName);
            await download.SaveAsAsync(targetTempPath);

            return new EgrulExtractResult
            {
                TempPdfPath = targetTempPath,
                QueryUsed = query,
                SuggestedFileName = Path.GetFileName(targetTempPath),
                RegistryKind = InferRegistryKind(client)
            };
        }

        private static string BuildSearchQuery(ClientInfo client)
        {
            string ogrn = DigitsOnly(client.Ogrn);
            string inn = DigitsOnly(client.Inn);

            if (!string.IsNullOrWhiteSpace(ogrn))
                return ogrn;

            if (!string.IsNullOrWhiteSpace(inn))
                return inn;

            return string.Empty;
        }

        private static string InferRegistryKind(ClientInfo client)
        {
            string ogrn = DigitsOnly(client.Ogrn);

            if (ogrn.Length == 15)
                return "ЕГРИП";

            if (ogrn.Length == 13)
                return "ЕГРЮЛ";

            if (client.ClientType == "ИП" || client.ClientType == "ИПГКФХ")
                return "ЕГРИП";

            return "ЕГРЮЛ";
        }

        private static string DigitsOnly(string? value)
        {
            return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        }

        private static async Task FillSearchQueryAsync(IPage page, string query)
        {
            var input = await PickFirstAvailableAsync(
                page.GetByLabel("Поисковый запрос").First,
                page.GetByLabel("Поисковый запрос:*").First,
                page.Locator("input[type='text']").First);

            await input.ClickAsync();
            await input.FillAsync(query);
        }

        private static async Task ClickFindAsync(IPage page)
        {
            var button = await PickFirstAvailableAsync(
                page.GetByRole(AriaRole.Button, new() { Name = "Найти" }).First,
                page.GetByText("Найти", new PageGetByTextOptions { Exact = true }).First);

            await button.ClickAsync();
        }

        private static async Task WaitForSearchOutcomeAsync(IPage page, CancellationToken cancellationToken)
        {
            var started = DateTime.UtcNow;

            while (DateTime.UtcNow - started < TimeSpan.FromSeconds(45))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await HasVisibleGetDocumentControlAsync(page))
                    return;

                if (await ContainsVisibleTextAsync(page, "По заданным критериям поиска данных не найдено."))
                    throw new InvalidOperationException("По этому запросу ФНС ничего не нашла.");

                await page.WaitForTimeoutAsync(500);
            }

            throw new TimeoutException("ФНС не вернула результаты поиска вовремя.");
        }

        private static async Task<bool> HasVisibleGetDocumentControlAsync(IPage page)
        {
            return await HasVisibleAsync(
                page.GetByRole(AriaRole.Button, new() { Name = "Получить выписку" }),
                page.GetByRole(AriaRole.Button, new() { Name = "Получить справку" }),
                page.GetByText("Получить выписку"),
                page.GetByText("Получить справку"));
        }

        private static async Task<ILocator> ResolveGetDocumentControlAsync(IPage page)
        {
            return await PickFirstVisibleAsync(
                page.GetByRole(AriaRole.Button, new() { Name = "Получить выписку" }),
                page.GetByRole(AriaRole.Button, new() { Name = "Получить справку" }),
                page.GetByText("Получить выписку"),
                page.GetByText("Получить справку"));
        }

        private static async Task<bool> ContainsVisibleTextAsync(IPage page, string text)
        {
            var locator = page.GetByText(text, new PageGetByTextOptions { Exact = false });
            return await HasVisibleAsync(locator);
        }

        private static async Task<bool> HasVisibleAsync(params ILocator[] locators)
        {
            foreach (var locator in locators)
            {
                try
                {
                    if (await locator.CountAsync() == 0)
                        continue;

                    var first = locator.First;
                    if (await first.IsVisibleAsync())
                        return true;
                }
                catch
                {
                    // Playwright бросает исключение если элемент не найден — это ожидаемо, пробуем следующий
                }
            }

            return false;
        }

        private static async Task<ILocator> PickFirstAvailableAsync(params ILocator[] locators)
        {
            foreach (var locator in locators)
            {
                try
                {
                    if (await locator.CountAsync() > 0)
                        return locator.First;
                }
                catch
                {
                    // Playwright бросает исключение если элемент не найден — это ожидаемо, пробуем следующий
                }
            }

            throw new InvalidOperationException("Не удалось найти нужный элемент на странице ФНС.");
        }

        private static async Task<ILocator> PickFirstVisibleAsync(params ILocator[] locators)
        {
            foreach (var locator in locators)
            {
                try
                {
                    if (await locator.CountAsync() == 0)
                        continue;

                    var first = locator.First;
                    if (await first.IsVisibleAsync())
                        return first;
                }
                catch
                {
                    // Playwright бросает исключение если элемент не найден — это ожидаемо, пробуем следующий
                }
            }

            throw new InvalidOperationException("Не удалось найти кнопку получения выписки на странице ФНС.");
        }

        private static string GetUniqueFilePath(string folderPath, string fileName)
        {
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            string candidate = Path.Combine(folderPath, fileName);
            int counter = 1;

            while (File.Exists(candidate))
            {
                candidate = Path.Combine(folderPath, $"{baseName}_{counter}{extension}");
                counter++;
            }

            return candidate;
        }

        private static string SanitizeFileName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string fileName = value.Trim();

            foreach (char ch in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(ch, '_');
            }

            while (fileName.Contains("__"))
            {
                fileName = fileName.Replace("__", "_");
            }

            return fileName.Trim(' ', '.');
        }
    }
}