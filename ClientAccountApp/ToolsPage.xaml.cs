using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Linq;




namespace ClientAccountApp
{
    public sealed partial class ToolsPage : Page
    {
        private string? _sourceFilePath;
        private string? _targetPdfPath;
        private string? _taxRequirementFilePath;
        private string _taxRequirementExtractedText = "";
        private readonly ObservableCollection<ClientInfo> _taxRequirementClients = new();
        private enum VatCalculationMode
        {
            ExtractFromGross,
            AddToNet
        }

        private VatCalculationMode _vatMode = VatCalculationMode.ExtractFromGross;

        public ToolsPage()
        {
            this.InitializeComponent();
            TaxRequirementClientComboBox.ItemsSource = _taxRequirementClients;
            LoadTaxRequirementClients();

            UpdateButtonsState();
            UpdateVatModeButtons();
            RecalculateVat();
            InitializeCivil395Calculator();

            LoadAiSettingsIntoForm();
        }
        private void LoadTaxRequirementClients()
        {
            _taxRequirementClients.Clear();

            using var db = new AppDbContext();

            var clients = db.Clients
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToList();

            foreach (var client in clients)
            {
                if (string.IsNullOrWhiteSpace(client.Name))
                    client.Name = "Клиент без названия";

                _taxRequirementClients.Add(client);
            }

            if (_taxRequirementClients.Count > 0 && TaxRequirementClientComboBox.SelectedItem == null)
            {
                TaxRequirementClientComboBox.SelectedIndex = 0;
            }
        }

        private void RefreshTaxRequirementClientsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadTaxRequirementClients();
            AiStatusTextBlock.Text = "Список клиентов обновлён.";
        }

        private void VatExtractModeButton_Click(object sender, RoutedEventArgs e)
        {
            _vatMode = VatCalculationMode.ExtractFromGross;
            UpdateVatModeButtons();
            RecalculateVat();
        }
        private void LoadAiSettingsIntoForm()
        {
            var settings = AiSettingsService.Load();

            GigaChatEnabledCheckBox.IsChecked = settings.IsEnabled;
            GigaChatOAuthUrlTextBox.Text = settings.GigaChatOAuthUrl;
            GigaChatApiUrlTextBox.Text = settings.GigaChatApiUrl;
            GigaChatModelTextBox.Text = settings.GigaChatModel;

            SelectGigaChatScope(settings.GigaChatScope);

            GigaChatAuthorizationKeyPasswordBox.Password = AiSettingsService.HasGigaChatAuthorizationKey()
                ? "********"
                : "";

            AiStatusTextBlock.Text = AiSettingsService.HasGigaChatAuthorizationKey()
                ? "GigaChat настроен. Можно проверить подключение."
                : "Введите Authorization Key GigaChat и сохраните настройки.";
        }
        private static readonly CultureInfo Civil395Culture = new("ru-RU");

        private string _civil395ResultText = "";

        private void InitializeCivil395Calculator()
        {
            if (Civil395StartDatePicker == null || Civil395EndDatePicker == null)
                return;

            Civil395StartDatePicker.Date = new DateTimeOffset(DateTime.Today.AddDays(-30));
            Civil395EndDatePicker.Date = new DateTimeOffset(DateTime.Today);

            Civil395RateTextBox.Text = "15";
            Civil395IncludeEndDateCheckBox.IsChecked = true;

            RecalculateCivil395();
        }

        private void Civil395Inputs_TextChanged(object sender, TextChangedEventArgs e)
        {
            RecalculateCivil395();
        }

        private void Civil395DatePicker_DateChanged(object sender, DatePickerValueChangedEventArgs args)
        {
            RecalculateCivil395();
        }

        private void Civil395IncludeEndDateCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            RecalculateCivil395();
        }

        private void CalculateCivil395Button_Click(object sender, RoutedEventArgs e)
        {
            RecalculateCivil395();
        }

        private void ClearCivil395Button_Click(object sender, RoutedEventArgs e)
        {
            Civil395DebtTextBox.Text = "";
            Civil395RateTextBox.Text = "15";
            Civil395StartDatePicker.Date = new DateTimeOffset(DateTime.Today.AddDays(-30));
            Civil395EndDatePicker.Date = new DateTimeOffset(DateTime.Today);
            Civil395IncludeEndDateCheckBox.IsChecked = true;

            RecalculateCivil395();
        }

        private void CopyCivil395ResultButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_civil395ResultText))
            {
                Civil395StatusTextBlock.Text = "Сначала выполните расчёт.";
                return;
            }

            var package = new DataPackage();
            package.SetText(_civil395ResultText);
            Clipboard.SetContent(package);

            Civil395StatusTextBlock.Text = "Результат скопирован.";
        }

        private void RecalculateCivil395()
        {
            if (Civil395DebtTextBox == null ||
                Civil395RateTextBox == null ||
                Civil395StartDatePicker == null ||
                Civil395EndDatePicker == null)
            {
                return;
            }

            if (!TryParseCivil395Decimal(Civil395DebtTextBox.Text, out decimal debt) || debt <= 0)
            {
                SetCivil395EmptyResult("Введите сумму задолженности.");
                return;
            }

            if (!TryParseCivil395Decimal(Civil395RateTextBox.Text, out decimal rate) || rate < 0)
            {
                SetCivil395EmptyResult("Введите корректную ставку.");
                return;
            }

            DateTime startDate = Civil395StartDatePicker.Date.Date;
            DateTime endDate = Civil395EndDatePicker.Date.Date;

            if (endDate < startDate)
            {
                SetCivil395EmptyResult("Дата окончания не может быть раньше даты начала.");
                return;
            }

            bool includeEndDate = Civil395IncludeEndDateCheckBox.IsChecked == true;

            int days = (endDate - startDate).Days;

            if (includeEndDate)
                days += 1;

            if (days <= 0)
            {
                SetCivil395EmptyResult("Период просрочки должен быть больше нуля.");
                return;
            }

            decimal interest = debt * rate / 100m / 365m * days;
            decimal total = debt + interest;

            string debtText = FormatCivil395Money(debt);
            string interestText = FormatCivil395Money(interest);
            string totalText = FormatCivil395Money(total);

            Civil395DaysResultTextBox.Text = $"Дней просрочки: {days}";
            Civil395InterestResultTextBox.Text = $"Проценты: {interestText}";
            Civil395TotalResultTextBox.Text = $"Итого к взысканию: {totalText}";

            Civil395FormulaResultTextBox.Text =
                $"Формула: {debtText} × {rate.ToString("0.##", Civil395Culture)}% / 365 × {days} дн. = {interestText}";

            _civil395ResultText =
                $"Расчёт процентов по ст. 395 ГК РФ{Environment.NewLine}" +
                $"Сумма долга: {debtText}{Environment.NewLine}" +
                $"Период: {startDate:dd.MM.yyyy} — {endDate:dd.MM.yyyy}{Environment.NewLine}" +
                $"Дней просрочки: {days}{Environment.NewLine}" +
                $"Ставка: {rate.ToString("0.##", Civil395Culture)}% годовых{Environment.NewLine}" +
                $"Проценты: {interestText}{Environment.NewLine}" +
                $"Итого к взысканию: {totalText}";

            Civil395StatusTextBlock.Text = "Расчёт выполнен.";
        }

        private void SetCivil395EmptyResult(string statusText)
        {
            Civil395DaysResultTextBox.Text = "Дней просрочки: —";
            Civil395InterestResultTextBox.Text = "Проценты: —";
            Civil395TotalResultTextBox.Text = "Итого к взысканию: —";
            Civil395FormulaResultTextBox.Text = "Формула: —";
            _civil395ResultText = "";
            Civil395StatusTextBlock.Text = statusText;
        }

        private static bool TryParseCivil395Decimal(string? text, out decimal value)
        {
            value = 0m;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string normalized = text
                .Replace("₽", "")
                .Replace(" ", "")
                .Replace("\u00A0", "")
                .Trim();

            if (decimal.TryParse(
                    normalized,
                    NumberStyles.Number,
                    Civil395Culture,
                    out value))
            {
                return true;
            }

            normalized = normalized.Replace(",", ".");

            return decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static string FormatCivil395Money(decimal value)
        {
            return value.ToString("N2", Civil395Culture) + " ₽";
        }
        private void SaveAiSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = AiSettingsService.Load();

            settings.Provider = "GigaChat";
            settings.IsEnabled = GigaChatEnabledCheckBox.IsChecked == true;
            settings.GigaChatOAuthUrl = GigaChatOAuthUrlTextBox.Text.Trim();
            settings.GigaChatApiUrl = GigaChatApiUrlTextBox.Text.Trim();
            settings.GigaChatModel = GigaChatModelTextBox.Text.Trim();
            settings.GigaChatScope = GetSelectedGigaChatScope();

            AiSettingsService.Save(settings);

            string authorizationKey = GigaChatAuthorizationKeyPasswordBox.Password.Trim();

            if (!string.IsNullOrWhiteSpace(authorizationKey) && authorizationKey != "********")
            {
                AiSettingsService.SaveGigaChatAuthorizationKey(authorizationKey);
            }

            AiStatusTextBlock.Text = "Настройки GigaChat сохранены.";
        }

        private async void TestGigaChatButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveAiSettingsButton_Click(sender, e);

                AiStatusTextBlock.Text = "Проверяю подключение к GigaChat...";

                string response = await GigaChatAiService.TestConnectionAsync();

                AiStatusTextBlock.Text = $"Подключение работает. Ответ: {response}";
            }
            catch (Exception ex)
            {
                AiStatusTextBlock.Text = $"Ошибка проверки GigaChat: {ex.Message}";
            }
        }

        private string GetSelectedGigaChatScope()
        {
            if (GigaChatScopeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Content?.ToString() ?? "GIGACHAT_API_PERS";
            }

            return "GIGACHAT_API_PERS";
        }

        private void SelectGigaChatScope(string? scope)
        {
            string target = string.IsNullOrWhiteSpace(scope)
                ? "GIGACHAT_API_PERS"
                : scope;

            foreach (var rawItem in GigaChatScopeComboBox.Items)
            {
                if (rawItem is ComboBoxItem item &&
                    string.Equals(item.Content?.ToString(), target, StringComparison.OrdinalIgnoreCase))
                {
                    GigaChatScopeComboBox.SelectedItem = item;
                    return;
                }
            }

            if (GigaChatScopeComboBox.Items.Count > 0)
                GigaChatScopeComboBox.SelectedIndex = 0;
        }
        private async void PickTaxRequirementFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();

                picker.FileTypeFilter.Add(".pdf");
                picker.FileTypeFilter.Add(".docx");
                picker.FileTypeFilter.Add(".doc");
                picker.FileTypeFilter.Add(".txt");
                picker.FileTypeFilter.Add(".xml");
                picker.FileTypeFilter.Add("*");

                var shell = ShellWindow.AppShell;

                if (shell == null)
                {
                    AiStatusTextBlock.Text = "Не удалось получить окно приложения.";
                    return;
                }

                IntPtr hwnd = WindowNative.GetWindowHandle(shell);
                InitializeWithWindow.Initialize(picker, hwnd);

                StorageFile? file = await picker.PickSingleFileAsync();

                if (file == null)
                {
                    AiStatusTextBlock.Text = "Выбор требования отменён.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(file.Path) || !File.Exists(file.Path))
                {
                    AiStatusTextBlock.Text = "Не удалось получить путь к файлу требования.";
                    return;
                }

                _taxRequirementFilePath = file.Path;
                TaxRequirementFileTextBox.Text = _taxRequirementFilePath;

                AiStatusTextBlock.Text = "Извлекаю текст из требования...";

                _taxRequirementExtractedText =
                    TaxRequirementTextExtractorService.ExtractText(_taxRequirementFilePath);

                const int previewLimit = 20000;

                TaxRequirementPreviewTextBox.Text =
                    _taxRequirementExtractedText.Length > previewLimit
                        ? _taxRequirementExtractedText.Substring(0, previewLimit) +
                          Environment.NewLine +
                          Environment.NewLine +
                          "... [текст обрезан в предпросмотре]"
                        : _taxRequirementExtractedText;

                AiStatusTextBlock.Text =
                    $"Текст требования извлечён. Символов: {_taxRequirementExtractedText.Length}.";
            }
            catch (Exception ex)
            {
                _taxRequirementExtractedText = "";
                TaxRequirementPreviewTextBox.Text = "";

                AiStatusTextBlock.Text = $"Ошибка извлечения текста требования: {ex.Message}";
            }
        }

        private async void GenerateTaxRequirementResponseButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaxRequirementClientComboBox.SelectedItem is not ClientInfo selectedClient)
            {
                AiStatusTextBlock.Text = "Сначала выберите клиента, по которому готовится ответ на требование.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_taxRequirementFilePath))
            {
                AiStatusTextBlock.Text = "Сначала выберите файл требования ФНС.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_taxRequirementExtractedText))
            {
                try
                {
                    _taxRequirementExtractedText =
                        TaxRequirementTextExtractorService.ExtractText(_taxRequirementFilePath);

                    TaxRequirementPreviewTextBox.Text = _taxRequirementExtractedText;
                }
                catch (Exception ex)
                {
                    AiStatusTextBlock.Text = $"Не удалось извлечь текст требования: {ex.Message}";
                    return;
                }
            }

            try
            {
                var organization = ActiveOrganizationService.GetRequired();

                AiStatusTextBlock.Text =
                    $"Отправляю требование в GigaChat. Клиент: {selectedClient.Name}...";

                string systemPrompt = BuildTaxRequirementSystemPrompt();

                string userPrompt = BuildTaxRequirementUserPrompt(
                    organization,
                    selectedClient,
                    _taxRequirementExtractedText);

                string aiResponse = await GigaChatAiService.AskAsync(systemPrompt, userPrompt);

                string wordPath = TaxRequirementResponseWordService.GenerateResponseDocx(
                    organization,
                    selectedClient,
                    _taxRequirementFilePath,
                    aiResponse);

                AiStatusTextBlock.Text = $"Word-ответ сформирован: {Path.GetFileName(wordPath)}";

                ClientFileStorageService.OpenFile(wordPath);
            }
            catch (Exception ex)
            {
                AiStatusTextBlock.Text = $"Ошибка формирования ответа через GigaChat: {ex.Message}";
            }
        }
        private static string BuildTaxRequirementSystemPrompt()
        {
            return
                "Ты помощник бухгалтера в Российской Федерации. " +
                "Твоя задача — подготовить официальный деловой черновик ответа на требование ФНС. " +
                "Не выдумывай факты, номера документов, суммы, даты и приложения, если их нет в тексте требования. " +
                "Если данных недостаточно, прямо укажи, какие сведения или документы нужно добавить вручную. " +
                "Стиль ответа: официальный, спокойный, юридически осторожный. " +
                "Не используй Markdown-разметку, списки со звездочками и разговорный стиль. " +
                "Сформируй только текст самого ответа, пригодный для вставки в Word.";
        }

        private static string BuildTaxRequirementUserPrompt(
     OrganizationProfile organization,
     ClientInfo client,
     string requirementText)
        {
            const int maxRequirementLength = 28000;

            string safeRequirementText = requirementText.Length > maxRequirementLength
                ? requirementText.Substring(0, maxRequirementLength) +
                  "\n\n[Текст требования был обрезан из-за ограничения размера запроса.]"
                : requirementText;

            return
                "Подготовь черновик ответа на требование ФНС.\n\n" +

                "ВАЖНО:\n" +
                "Ответ готовится по выбранному клиенту как налогоплательщику. " +
                "Данные рабочей организации используй только как внутренний контекст программы, " +
                "но не подставляй её как налогоплательщика, если требование относится к клиенту.\n\n" +

                "Данные клиента / налогоплательщика:\n" +
                $"Тип клиента: {EmptyForPrompt(client.ClientType)}\n" +
                $"Наименование / ФИО: {EmptyForPrompt(client.Name)}\n" +
                $"ИНН: {EmptyForPrompt(client.Inn)}\n" +
                $"ОГРН / ОГРНИП: {EmptyForPrompt(client.Ogrn)}\n" +
                $"Юридический адрес: {EmptyForPrompt(client.Address)}\n" +
                $"Руководитель / предприниматель: {EmptyForPrompt(client.DirectorFullName)}\n\n" +

                "Рабочая организация, через которую пользователь работает в программе:\n" +
                $"Наименование: {EmptyForPrompt(organization.Name)}\n" +
                $"ИНН: {EmptyForPrompt(organization.Inn)}\n" +
                $"КПП: {EmptyForPrompt(organization.Kpp)}\n" +
                $"ОГРН / ОГРНИП: {EmptyForPrompt(organization.Ogrn)}\n" +
                $"Адрес: {EmptyForPrompt(organization.LegalAddress)}\n" +
                $"Руководитель: {EmptyForPrompt(organization.DirectorName)}\n\n" +

                "Текст требования ФНС:\n" +
                safeRequirementText + "\n\n" +

                "Сформируй официальный черновик ответа. " +
                "Если из требования понятно, какие документы нужно приложить, укажи это в тексте ответа. " +
                "Если номер, дата требования, ИФНС или конкретные документы не распознаны, оставь аккуратные места для ручного заполнения. " +
                "Не выдумывай факты, суммы, даты, номера документов и приложения.";
        }

        private static string EmptyForPrompt(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "не указано"
                : value.Trim();
        }
        private void VatAddModeButton_Click(object sender, RoutedEventArgs e)
        {
            _vatMode = VatCalculationMode.AddToNet;
            UpdateVatModeButtons();
            RecalculateVat();
        }

        private void VatInputs_TextChanged(object sender, TextChangedEventArgs e)
        {
            RecalculateVat();
        }

        private void VatPresetRateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string rateText)
            {
                VatRateTextBox.Text = rateText;
            }
        }

        private void ClearVatButton_Click(object sender, RoutedEventArgs e)
        {
            _vatMode = VatCalculationMode.ExtractFromGross;
            VatAmountTextBox.Text = "";
            VatRateTextBox.Text = "22";

            UpdateVatModeButtons();
            RecalculateVat();
        }

        private void CopyVatResultButton_Click(object sender, RoutedEventArgs e)
        {
            string modeText = _vatMode == VatCalculationMode.ExtractFromGross
                ? "Выделение НДС из суммы"
                : "Начисление НДС сверху";

            string text =
                $"{modeText}\r\n" +
                $"Ставка: {VatRateTextBox.Text}%\r\n" +
                $"{VatResultVatTextBox.Text}\r\n" +
$"{VatResultNetTextBox.Text}\r\n" +
$"{VatResultGrossTextBox.Text}";

            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            Clipboard.Flush();

            VatStatusTextBlock.Text = "Результат скопирован в буфер обмена.";
        }

        private void UpdateVatModeButtons()
        {
            bool isExtract = _vatMode == VatCalculationMode.ExtractFromGross;

            SetVatModeButtonState(VatExtractModeButton, isExtract);
            SetVatModeButtonState(VatAddModeButton, !isExtract);

            VatModeHintTextBlock.Text = isExtract
                ? "Режим: выделение НДС из суммы с налогом"
                : "Режим: начисление НДС сверху на сумму без налога";
        }

        private static void SetVatModeButtonState(Button button, bool isActive)
        {
            if (button == null)
                return;

            button.Background = new SolidColorBrush(
                isActive
                    ? ColorHelper.FromArgb(255, 47, 79, 111)
                    : ColorHelper.FromArgb(255, 17, 21, 28));

            button.BorderBrush = new SolidColorBrush(
                isActive
                    ? ColorHelper.FromArgb(255, 90, 145, 255)
                    : ColorHelper.FromArgb(255, 43, 49, 64));
        }

        private void RecalculateVat()
        {
            if (!TryParseDecimal(VatAmountTextBox.Text, out decimal amount))
            {
                SetVatEmptyState("Введи корректную сумму.");
                return;
            }

            if (!TryParseDecimal(VatRateTextBox.Text, out decimal ratePercent) || ratePercent < 0)
            {
                SetVatEmptyState("Введи корректную ставку НДС.");
                return;
            }

            decimal vatAmount;
            decimal amountWithoutVat;
            decimal amountWithVat;
            decimal rate = ratePercent / 100m;

            if (_vatMode == VatCalculationMode.ExtractFromGross)
            {
                amountWithVat = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

                if (ratePercent == 0)
                {
                    amountWithoutVat = amountWithVat;
                    vatAmount = 0m;
                }
                else
                {
                    amountWithoutVat = Math.Round(amountWithVat / (1m + rate), 2, MidpointRounding.AwayFromZero);
                    vatAmount = Math.Round(amountWithVat - amountWithoutVat, 2, MidpointRounding.AwayFromZero);
                }
            }
            else
            {
                amountWithoutVat = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
                vatAmount = Math.Round(amountWithoutVat * rate, 2, MidpointRounding.AwayFromZero);
                amountWithVat = Math.Round(amountWithoutVat + vatAmount, 2, MidpointRounding.AwayFromZero);
            }

            VatResultVatTextBox.Text = $"НДС: {FormatMoney(vatAmount)}";
            VatResultNetTextBox.Text = $"Без НДС: {FormatMoney(amountWithoutVat)}";
            VatResultGrossTextBox.Text = $"С НДС: {FormatMoney(amountWithVat)}";

            VatStatusTextBlock.Text = _vatMode == VatCalculationMode.ExtractFromGross
                ? "НДС выделен из введённой суммы."
                : "НДС начислен сверху.";
        }

        private void SetVatEmptyState(string message)
        {
            VatResultVatTextBox.Text = "НДС: —";
            VatResultNetTextBox.Text = "Без НДС: —";
            VatResultGrossTextBox.Text = "С НДС: —";
            VatStatusTextBlock.Text = message;
        }

        private static bool TryParseDecimal(string? text, out decimal value)
        {
            value = 0m;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string normalized = text
                .Trim()
                .Replace(" ", "")
                .Replace("\u00A0", "");

            if (decimal.TryParse(
                normalized,
                NumberStyles.Number,
                new CultureInfo("ru-RU"),
                out value))
            {
                return true;
            }

            normalized = normalized.Replace(",", ".");

            return decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("N2", new CultureInfo("ru-RU")) + " ₽";
        }

        private async void PickSourceFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add("*");

                IntPtr hwnd = WindowNative.GetWindowHandle(App.MainAppWindow);
                InitializeWithWindow.Initialize(picker, hwnd);

                StorageFile? pickedFile = await picker.PickSingleFileAsync();
                if (pickedFile == null)
                {
                    ToolsStatusTextBlock.Text = "Выбор исходного файла отменён.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(pickedFile.Path) || !File.Exists(pickedFile.Path))
                {
                    ToolsStatusTextBlock.Text = "Не удалось получить путь к выбранному файлу.";
                    return;
                }

                ApplySelectedSourceFile(pickedFile.Path);
            }
            catch (Exception ex)
            {
                ToolsStatusTextBlock.Text = $"Ошибка выбора исходного файла: {ex.Message}";
            }
        }
        private void SourceDropZoneBorder_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Отпустите файл для загрузки";
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsContentVisible = true;
                e.DragUIOverride.IsGlyphVisible = true;

                SourceDropZoneBorder.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(255, 90, 145, 255));
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private async void SourceDropZoneBorder_Drop(object sender, DragEventArgs e)
        {
            try
            {
                SourceDropZoneBorder.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(255, 43, 49, 64));

                if (!e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    ToolsStatusTextBlock.Text = "Перетащенный объект не является файлом.";
                    return;
                }

                var items = await e.DataView.GetStorageItemsAsync();
                if (items == null || items.Count == 0)
                {
                    ToolsStatusTextBlock.Text = "Не удалось получить файл из drag & drop.";
                    return;
                }

                if (items[0] is not StorageFile file)
                {
                    ToolsStatusTextBlock.Text = "Нужно перетащить именно файл, а не папку.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(file.Path) || !File.Exists(file.Path))
                {
                    ToolsStatusTextBlock.Text = "Не удалось получить путь к перетащенному файлу.";
                    return;
                }

                ApplySelectedSourceFile(file.Path);
            }
            catch (Exception ex)
            {
                ToolsStatusTextBlock.Text = $"Ошибка drag & drop: {ex.Message}";
            }
        }
        private void ApplySelectedSourceFile(string sourceFilePath)
        {
            _sourceFilePath = sourceFilePath;
            SourceFileTextBox.Text = _sourceFilePath;

            if (string.IsNullOrWhiteSpace(_targetPdfPath))
            {
                string folder = Path.GetDirectoryName(_sourceFilePath) ?? "";
                string fileName = Path.GetFileNameWithoutExtension(_sourceFilePath) + ".pdf";
                _targetPdfPath = Path.Combine(folder, fileName);
                TargetPdfTextBox.Text = _targetPdfPath;
            }

            ToolsStatusTextBlock.Text = $"Выбран файл: {Path.GetFileName(_sourceFilePath)}";
            UpdateButtonsState();
        }
        private async void PickTargetPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };

                picker.FileTypeChoices.Add("PDF документ", new[] { ".pdf" });

                if (!string.IsNullOrWhiteSpace(_sourceFilePath))
                {
                    picker.SuggestedFileName = Path.GetFileNameWithoutExtension(_sourceFilePath) + ".pdf";
                }
                else
                {
                    picker.SuggestedFileName = "Новый_файл.pdf";
                }

                IntPtr hwnd = WindowNative.GetWindowHandle(App.MainAppWindow);
                InitializeWithWindow.Initialize(picker, hwnd);

                StorageFile? targetFile = await picker.PickSaveFileAsync();
                if (targetFile == null)
                {
                    ToolsStatusTextBlock.Text = "Выбор пути сохранения отменён.";
                    return;
                }

                _targetPdfPath = targetFile.Path;
                TargetPdfTextBox.Text = _targetPdfPath;

                ToolsStatusTextBlock.Text = $"PDF будет сохранён как: {Path.GetFileName(_targetPdfPath)}";
                UpdateButtonsState();
            }
            catch (Exception ex)
            {
                ToolsStatusTextBlock.Text = $"Ошибка выбора пути сохранения: {ex.Message}";
            }
        }

        private void ConvertToPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_sourceFilePath) || !File.Exists(_sourceFilePath))
                {
                    ToolsStatusTextBlock.Text = "Исходный файл не найден.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(_targetPdfPath))
                {
                    ToolsStatusTextBlock.Text = "Сначала выбери путь сохранения PDF.";
                    return;
                }

                string? targetFolder = Path.GetDirectoryName(_targetPdfPath);
                if (!string.IsNullOrWhiteSpace(targetFolder) && !Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                PdfConversionService.ConvertFileToPdf(_sourceFilePath, _targetPdfPath);

                ToolsStatusTextBlock.Text = $"Файл успешно конвертирован в PDF: {Path.GetFileName(_targetPdfPath)}";
                OpenConvertedPdfButton.IsEnabled = File.Exists(_targetPdfPath);
            }
            catch (Exception ex)
            {
                ToolsStatusTextBlock.Text = $"Ошибка конвертации в PDF: {ex.Message}";
            }
        }

        private void OpenConvertedPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_targetPdfPath) || !File.Exists(_targetPdfPath))
                {
                    ToolsStatusTextBlock.Text = "PDF-файл ещё не найден на диске.";
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = _targetPdfPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ToolsStatusTextBlock.Text = $"Ошибка открытия PDF: {ex.Message}";
            }
        }

        private void UpdateButtonsState()
        {
            bool canConvert =
                !string.IsNullOrWhiteSpace(_sourceFilePath) &&
                !string.IsNullOrWhiteSpace(_targetPdfPath);

            ConvertToPdfButton.IsEnabled = canConvert;
            OpenConvertedPdfButton.IsEnabled =
                !string.IsNullOrWhiteSpace(_targetPdfPath) &&
                File.Exists(_targetPdfPath);
        }
    }
}