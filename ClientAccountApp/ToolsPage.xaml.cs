using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using ClientAccountApp.Models;
using ClientAccountApp.Services;

namespace ClientAccountApp
{
    public sealed partial class ToolsPage : Page
    {
        // ─── PDF ──────────────────────────────────────────────────────────────
        private string? _sourceFilePath;
        private string? _targetPdfPath;

        // ─── НДС ─────────────────────────────────────────────────────────────
        private enum VatCalculationMode { ExtractFromGross, AddToNet }
        private VatCalculationMode _vatMode = VatCalculationMode.ExtractFromGross;

        // ─── Выписка — static: данные сохраняются при уходе/возврате на страницу ─
        private static BankStatement? _currentStatement;
        private static List<BankStatementOperation> _allOperations = new();
        private static string _loadedFilePath = "";
        private static string _savedOrgInn = "";
        private static string _savedOrgKpp = "";
        private static string _savedOrgName = "";
        private static string? _currentSessionId = null; // ID текущей сохранённой сессии

        // ─────────────────────────────────────────────────────────────────────
        public ToolsPage()
        {
            this.InitializeComponent();
            UpdateButtonsState();
            UpdateVatModeButtons();
            RecalculateVat();
            InitializeCivil395Calculator();
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Всегда обновляем список сессий
            DispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () =>
                {
                    RefreshSessionsList();
                    if (_currentStatement != null)
                        RestoreStatementUI();
                });
        }
        private void RsvToolFrame_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Frame frame)
                return;

            if (frame.CurrentSourcePageType != typeof(RsvPage))
            {
                frame.Navigate(typeof(RsvPage));
            }
        }
        
        private void RestoreStatementUI()
        {
            StmtFilePathTextBox.Text = _loadedFilePath;
            StmtLoadButton.IsEnabled = false;
            StmtClearButton.Visibility = Visibility.Visible;
            StmtSummaryBorder.Visibility = Visibility.Visible;
            StmtOperationsCard.Visibility = Visibility.Visible;
            StmtExportCard.Visibility = Visibility.Visible;
            StmtLoadStatusText.Text = $"Загружено: {_currentStatement!.OperationCount} операций за период {_currentStatement.PeriodDisplay}.";

            ShowStatementSummary();
            ApplyStatementFilters();
            InitExportYearCombo();

            // Предзаполняем реквизиты организации
            TryFillOrgInfoFromSettings();
            // Поверх накладываем ранее введённые/подтверждённые значения
            if (!string.IsNullOrEmpty(_savedOrgInn)) StmtExportInnBox.Text = _savedOrgInn;
            if (!string.IsNullOrEmpty(_savedOrgKpp)) StmtExportKppBox.Text = _savedOrgKpp;
            if (!string.IsNullOrEmpty(_savedOrgName)) StmtExportNameBox.Text = _savedOrgName;
        }

        // ═════════════════════════════════════════════════════════════════════
        // PDF
        // ═════════════════════════════════════════════════════════════════════

        private static void OpenUrl(string url) =>
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

        private async void PickSourceFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add("*");
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainAppWindow));

                StorageFile? file = await picker.PickSingleFileAsync();
                if (file == null) { ToolsStatusTextBlock.Text = "Выбор исходного файла отменён."; return; }
                if (string.IsNullOrWhiteSpace(file.Path) || !File.Exists(file.Path))
                { ToolsStatusTextBlock.Text = "Не удалось получить путь к выбранному файлу."; return; }

                ApplySelectedSourceFile(file.Path);
            }
            catch (Exception ex) { ToolsStatusTextBlock.Text = $"Ошибка выбора исходного файла: {ex.Message}"; }
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
                SourceDropZoneBorder.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 90, 145, 255));
            }
            else e.AcceptedOperation = DataPackageOperation.None;
        }

        private async void SourceDropZoneBorder_Drop(object sender, DragEventArgs e)
        {
            try
            {
                SourceDropZoneBorder.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 43, 49, 64));
                if (!e.DataView.Contains(StandardDataFormats.StorageItems))
                { ToolsStatusTextBlock.Text = "Перетащенный объект не является файлом."; return; }

                var items = await e.DataView.GetStorageItemsAsync();
                if (items == null || items.Count == 0)
                { ToolsStatusTextBlock.Text = "Не удалось получить файл из drag & drop."; return; }
                if (items[0] is not StorageFile file)
                { ToolsStatusTextBlock.Text = "Нужно перетащить именно файл, а не папку."; return; }
                if (string.IsNullOrWhiteSpace(file.Path) || !File.Exists(file.Path))
                { ToolsStatusTextBlock.Text = "Не удалось получить путь к перетащенному файлу."; return; }

                ApplySelectedSourceFile(file.Path);
            }
            catch (Exception ex) { ToolsStatusTextBlock.Text = $"Ошибка drag & drop: {ex.Message}"; }
        }

        private void ApplySelectedSourceFile(string path)
        {
            _sourceFilePath = path;
            SourceFileTextBox.Text = path;
            if (string.IsNullOrWhiteSpace(_targetPdfPath))
            {
                string folder = Path.GetDirectoryName(path) ?? "";
                _targetPdfPath = Path.Combine(folder, Path.GetFileNameWithoutExtension(path) + ".pdf");
                TargetPdfTextBox.Text = _targetPdfPath;
            }
            ToolsStatusTextBlock.Text = $"Выбран файл: {Path.GetFileName(path)}";
            UpdateButtonsState();
        }

        private async void PickTargetPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
                picker.FileTypeChoices.Add("PDF документ", new[] { ".pdf" });
                picker.SuggestedFileName = !string.IsNullOrWhiteSpace(_sourceFilePath)
                    ? Path.GetFileNameWithoutExtension(_sourceFilePath) + ".pdf" : "Новый_файл.pdf";
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainAppWindow));

                StorageFile? target = await picker.PickSaveFileAsync();
                if (target == null) { ToolsStatusTextBlock.Text = "Выбор пути сохранения отменён."; return; }

                _targetPdfPath = target.Path;
                TargetPdfTextBox.Text = _targetPdfPath;
                ToolsStatusTextBlock.Text = $"PDF будет сохранён как: {Path.GetFileName(_targetPdfPath)}";
                UpdateButtonsState();
            }
            catch (Exception ex) { ToolsStatusTextBlock.Text = $"Ошибка выбора пути сохранения: {ex.Message}"; }
        }

        private void ConvertToPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_sourceFilePath) || !File.Exists(_sourceFilePath))
                { ToolsStatusTextBlock.Text = "Исходный файл не найден."; return; }
                if (string.IsNullOrWhiteSpace(_targetPdfPath))
                { ToolsStatusTextBlock.Text = "Сначала выбери путь сохранения PDF."; return; }

                string? folder = Path.GetDirectoryName(_targetPdfPath);
                if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                PdfConversionService.ConvertFileToPdf(_sourceFilePath, _targetPdfPath);
                ToolsStatusTextBlock.Text = $"Файл успешно конвертирован: {Path.GetFileName(_targetPdfPath)}";
                OpenConvertedPdfButton.IsEnabled = File.Exists(_targetPdfPath);
            }
            catch (Exception ex) { ToolsStatusTextBlock.Text = $"Ошибка конвертации: {ex.Message}"; }
        }

        private void OpenConvertedPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_targetPdfPath) || !File.Exists(_targetPdfPath))
                { ToolsStatusTextBlock.Text = "PDF-файл ещё не найден на диске."; return; }
                Process.Start(new ProcessStartInfo { FileName = _targetPdfPath, UseShellExecute = true });
            }
            catch (Exception ex) { ToolsStatusTextBlock.Text = $"Ошибка открытия PDF: {ex.Message}"; }
        }

        private void UpdateButtonsState()
        {
            bool canConvert = !string.IsNullOrWhiteSpace(_sourceFilePath) && !string.IsNullOrWhiteSpace(_targetPdfPath);
            ConvertToPdfButton.IsEnabled = canConvert;
            OpenConvertedPdfButton.IsEnabled = !string.IsNullOrWhiteSpace(_targetPdfPath) && File.Exists(_targetPdfPath);
        }

        // ═════════════════════════════════════════════════════════════════════
        // НДС
        // ═════════════════════════════════════════════════════════════════════

        private void VatExtractModeButton_Click(object sender, RoutedEventArgs e)
        { _vatMode = VatCalculationMode.ExtractFromGross; UpdateVatModeButtons(); RecalculateVat(); }

        private void VatAddModeButton_Click(object sender, RoutedEventArgs e)
        { _vatMode = VatCalculationMode.AddToNet; UpdateVatModeButtons(); RecalculateVat(); }

        private void VatInputs_TextChanged(object sender, TextChangedEventArgs e) => RecalculateVat();

        private void VatPresetRateButton_Click(object sender, RoutedEventArgs e)
        { if (sender is Button b && b.Tag is string t) VatRateTextBox.Text = t; }

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
            string mode = _vatMode == VatCalculationMode.ExtractFromGross ? "Выделение НДС из суммы" : "Начисление НДС сверху";
            var pkg = new DataPackage();
            pkg.SetText($"{mode}\r\nСтавка: {VatRateTextBox.Text}%\r\n{VatResultVatTextBox.Text}\r\n{VatResultNetTextBox.Text}\r\n{VatResultGrossTextBox.Text}");
            Clipboard.SetContent(pkg);
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

        private static void SetVatModeButtonState(Button btn, bool active)
        {
            if (btn == null)
                return;

            bool isLightTheme = ThemeService.CurrentTheme == ThemeService.ThemeLight;

            if (isLightTheme)
            {
                if (active)
                {
                    btn.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 217, 154, 0));
                    btn.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 184, 134, 11));
                    btn.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 20, 24, 35));
                }
                else
                {
                    btn.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 248, 250, 252));
                    btn.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 210, 218, 230));
                    btn.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 40, 50, 70));
                }
            }
            else
            {
                if (active)
                {
                    btn.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 184, 134, 11));
                    btn.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 217, 154, 0));
                    btn.Foreground = new SolidColorBrush(Colors.Black);
                }
                else
                {
                    btn.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 23, 28, 38));
                    btn.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 43, 49, 64));
                    btn.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 210, 218, 230));
                }
            }

            btn.BorderThickness = active
                ? new Thickness(2)
                : new Thickness(1);

            btn.CornerRadius = new CornerRadius(8);
        }
        private void RecalculateVat()
        {
            if (!TryParseDecimal(VatAmountTextBox.Text, out decimal amount)) { SetVatEmptyState("Введи корректную сумму."); return; }
            if (!TryParseDecimal(VatRateTextBox.Text, out decimal ratePercent) || ratePercent < 0)
            { SetVatEmptyState("Введи корректную ставку НДС."); return; }

            decimal rate = ratePercent / 100m, vat, net, gross;

            if (_vatMode == VatCalculationMode.ExtractFromGross)
            {
                gross = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
                if (ratePercent == 0) { net = gross; vat = 0m; }
                else { net = Math.Round(gross / (1m + rate), 2, MidpointRounding.AwayFromZero); vat = Math.Round(gross - net, 2, MidpointRounding.AwayFromZero); }
            }
            else
            {
                net = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
                vat = Math.Round(net * rate, 2, MidpointRounding.AwayFromZero);
                gross = Math.Round(net + vat, 2, MidpointRounding.AwayFromZero);
            }

            VatResultVatTextBox.Text = $"НДС: {FormatMoney(vat)}";
            VatResultNetTextBox.Text = $"Без НДС: {FormatMoney(net)}";
            VatResultGrossTextBox.Text = $"С НДС: {FormatMoney(gross)}";
            VatStatusTextBlock.Text = _vatMode == VatCalculationMode.ExtractFromGross ? "НДС выделен из введённой суммы." : "НДС начислен сверху.";
        }

        private void SetVatEmptyState(string msg)
        {
            VatResultVatTextBox.Text = "НДС: —"; VatResultNetTextBox.Text = "Без НДС: —"; VatResultGrossTextBox.Text = "С НДС: —";
            VatStatusTextBlock.Text = msg;
        }

        // ═════════════════════════════════════════════════════════════════════
        // 395 ГК РФ
        // ═════════════════════════════════════════════════════════════════════

        private static readonly CultureInfo Civil395Culture = new("ru-RU");
        private string _civil395ResultText = "";

        private void InitializeCivil395Calculator()
        {
            if (Civil395StartDatePicker == null || Civil395EndDatePicker == null) return;
            Civil395StartDatePicker.Date = new DateTimeOffset(DateTime.Today.AddDays(-30));
            Civil395EndDatePicker.Date = new DateTimeOffset(DateTime.Today);
            Civil395RateTextBox.Text = "15";
            Civil395IncludeEndDateCheckBox.IsChecked = true;
            RecalculateCivil395();
        }

        private void Civil395Inputs_TextChanged(object sender, TextChangedEventArgs e) => RecalculateCivil395();
        private void Civil395DatePicker_DateChanged(object sender, DatePickerValueChangedEventArgs e) => RecalculateCivil395();
        private void Civil395IncludeEndDateCheckBox_Changed(object sender, RoutedEventArgs e) => RecalculateCivil395();
        private void CalculateCivil395Button_Click(object sender, RoutedEventArgs e) => RecalculateCivil395();

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
            if (string.IsNullOrWhiteSpace(_civil395ResultText)) { Civil395StatusTextBlock.Text = "Сначала выполните расчёт."; return; }
            var pkg = new DataPackage();
            pkg.SetText(_civil395ResultText);
            Clipboard.SetContent(pkg);
            Civil395StatusTextBlock.Text = "Результат скопирован.";
        }

        private void RecalculateCivil395()
        {
            if (Civil395DebtTextBox == null || Civil395RateTextBox == null ||
                Civil395StartDatePicker == null || Civil395EndDatePicker == null) return;

            if (!TryParseCivil395Decimal(Civil395DebtTextBox.Text, out decimal debt) || debt <= 0)
            { SetCivil395EmptyResult("Введите сумму задолженности."); return; }
            if (!TryParseCivil395Decimal(Civil395RateTextBox.Text, out decimal rate) || rate < 0)
            { SetCivil395EmptyResult("Введите корректную ставку."); return; }

            DateTime start = Civil395StartDatePicker.Date.Date;
            DateTime end = Civil395EndDatePicker.Date.Date;
            if (end < start) { SetCivil395EmptyResult("Дата окончания не может быть раньше даты начала."); return; }

            int days = (end - start).Days + (Civil395IncludeEndDateCheckBox.IsChecked == true ? 1 : 0);
            if (days <= 0) { SetCivil395EmptyResult("Период просрочки должен быть больше нуля."); return; }

            decimal interest = debt * rate / 100m / 365m * days;
            decimal total = debt + interest;

            Civil395DaysResultTextBox.Text = $"Дней просрочки: {days}";
            Civil395InterestResultTextBox.Text = $"Проценты: {FormatCivil395Money(interest)}";
            Civil395TotalResultTextBox.Text = $"Итого к взысканию: {FormatCivil395Money(total)}";
            Civil395FormulaResultTextBox.Text = $"Формула: {FormatCivil395Money(debt)} × {rate.ToString("0.##", Civil395Culture)}% / 365 × {days} дн. = {FormatCivil395Money(interest)}";

            _civil395ResultText =
                $"Расчёт процентов по ст. 395 ГК РФ{Environment.NewLine}" +
                $"Сумма долга: {FormatCivil395Money(debt)}{Environment.NewLine}" +
                $"Период: {start:dd.MM.yyyy} — {end:dd.MM.yyyy}{Environment.NewLine}" +
                $"Дней просрочки: {days}{Environment.NewLine}" +
                $"Ставка: {rate.ToString("0.##", Civil395Culture)}% годовых{Environment.NewLine}" +
                $"Проценты: {FormatCivil395Money(interest)}{Environment.NewLine}" +
                $"Итого к взысканию: {FormatCivil395Money(total)}";

            Civil395StatusTextBlock.Text = "Расчёт выполнен.";
        }

        private void SetCivil395EmptyResult(string msg)
        {
            Civil395DaysResultTextBox.Text = "Дней просрочки: —";
            Civil395InterestResultTextBox.Text = "Проценты: —";
            Civil395TotalResultTextBox.Text = "Итого к взысканию: —";
            Civil395FormulaResultTextBox.Text = "Формула: —";
            _civil395ResultText = "";
            Civil395StatusTextBlock.Text = msg;
        }

        private static bool TryParseCivil395Decimal(string? text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string n = text.Replace("₽", "").Replace(" ", "").Replace("\u00A0", "").Trim();
            if (decimal.TryParse(n, NumberStyles.Number, new CultureInfo("ru-RU"), out value)) return true;
            return decimal.TryParse(n.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private static string FormatCivil395Money(decimal v) => v.ToString("N2", new CultureInfo("ru-RU")) + " ₽";

        // ═════════════════════════════════════════════════════════════════════
        // ВЫПИСКА БАНКА — Загрузка
        // ═════════════════════════════════════════════════════════════════════

        private async void StmtBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add(".1c");
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainAppWindow));

            StorageFile? file = await picker.PickSingleFileAsync();
            if (file is null) return;

            StmtFilePathTextBox.Text = file.Path;
            StmtLoadButton.IsEnabled = true;
            StmtLoadStatusText.Text = "Файл выбран. Нажмите «Загрузить».";
        }

        private void StmtLoadButton_Click(object sender, RoutedEventArgs e)
        {
            string path = StmtFilePathTextBox.Text;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            { StmtLoadStatusText.Text = "Файл не найден. Выберите файл заново."; return; }

            try
            {
                _currentStatement = BankStatementParser.Parse(path);
                _allOperations = _currentStatement.Operations;
                _loadedFilePath = path;   // сохраняем путь для восстановления

                TryMatchClientsToOperations();
                ShowStatementSummary();
                ApplyStatementFilters();
                InitExportYearCombo();
                TryFillOrgInfoFromSettings();

                StmtSummaryBorder.Visibility = Visibility.Visible;
                StmtOperationsCard.Visibility = Visibility.Visible;
                StmtExportCard.Visibility = Visibility.Visible;
                StmtClearButton.Visibility = Visibility.Visible;
                StmtLoadStatusText.Text = $"Загружено: {_currentStatement.OperationCount} операций за период {_currentStatement.PeriodDisplay}.";
            }
            catch (Exception ex) { StmtLoadStatusText.Text = $"Ошибка разбора файла: {ex.Message}"; }
        }

        private void StmtClearButton_Click(object sender, RoutedEventArgs e)
        {
            _currentStatement = null;
            _allOperations = new();
            _loadedFilePath = "";
            _currentSessionId = null;

            StmtFilePathTextBox.Text = "";
            StmtLoadButton.IsEnabled = false;
            StmtLoadStatusText.Text = "";
            StmtSummaryBorder.Visibility = Visibility.Collapsed;
            StmtOperationsCard.Visibility = Visibility.Collapsed;
            StmtExportCard.Visibility = Visibility.Collapsed;
            StmtClearButton.Visibility = Visibility.Collapsed;
            StmtOperationsListView.ItemsSource = null;

            RefreshSessionsList();
        }

        // ═════════════════════════════════════════════════════════════════════
        // ВЫПИСКА БАНКА — Сессии
        // ═════════════════════════════════════════════════════════════════════

        private void RefreshSessionsList()
        {
            var sessions = BankStatementSessionService.GetAll();
            if (sessions.Count == 0)
            {
                StmtSessionsCard.Visibility = Visibility.Collapsed;
                return;
            }
            StmtSessionsCard.Visibility = Visibility.Visible;
            StmtSessionsListView.ItemsSource = sessions;
        }

        private void StmtHideSessionsButton_Click(object sender, RoutedEventArgs e)
        {
            StmtSessionsCard.Visibility = Visibility.Collapsed;
        }

        private void StmtLoadSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string filePath) return;
            if (!File.Exists(filePath))
            {
                StmtLoadStatusText.Text = "Файл сессии не найден. Возможно, он был удалён.";
                RefreshSessionsList();
                return;
            }

            try
            {
                var (stmt, ops, inn, kpp, name, id) = BankStatementSessionService.Load(filePath);
                _currentStatement  = stmt;
                _allOperations     = ops;
                _loadedFilePath    = stmt.AccountNumber; // путь к файлу утерян — пишем номер счёта
                _currentSessionId  = id;
                _savedOrgInn       = inn;
                _savedOrgKpp       = kpp;
                _savedOrgName      = name;

                TryMatchClientsToOperations();
                ShowStatementSummary();
                ApplyStatementFilters();
                InitExportYearCombo();

                // Заполняем Шаг 3
                if (!string.IsNullOrEmpty(inn))  StmtExportInnBox.Text  = inn;
                if (!string.IsNullOrEmpty(kpp))  StmtExportKppBox.Text  = kpp;
                if (!string.IsNullOrEmpty(name)) StmtExportNameBox.Text = name;

                StmtFilePathTextBox.Text = $"[Сессия] {stmt.AccountNumber} | {stmt.PeriodDisplay}";
                StmtSummaryBorder.Visibility  = Visibility.Visible;
                StmtOperationsCard.Visibility = Visibility.Visible;
                StmtExportCard.Visibility     = Visibility.Visible;
                StmtClearButton.Visibility    = Visibility.Visible;
                StmtLoadButton.IsEnabled      = false;

                int marked = ops.Count(o => o.IsMarkedForVatBook);
                StmtLoadStatusText.Text = $"Сессия загружена: {ops.Count} операций, {marked} размечено.";
            }
            catch (Exception ex)
            {
                StmtLoadStatusText.Text = $"Ошибка загрузки сессии: {ex.Message}";
            }
        }

        private void StmtDeleteSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string filePath) return;
            BankStatementSessionService.Delete(filePath);
            RefreshSessionsList();
        }

        private void StmtSaveSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStatement == null || _allOperations.Count == 0)
            {
                StmtLoadStatusText.Text = "Нет загруженных операций для сохранения.";
                return;
            }

            try
            {
                string inn  = StmtExportInnBox.Text.Trim();
                string kpp  = StmtExportKppBox.Text.Trim();
                string name = StmtExportNameBox.Text.Trim();

                // Обновляем сохранённые реквизиты
                if (!string.IsNullOrEmpty(inn))  _savedOrgInn  = inn;
                if (!string.IsNullOrEmpty(kpp))  _savedOrgKpp  = kpp;
                if (!string.IsNullOrEmpty(name)) _savedOrgName = name;

                BankStatementSessionService.Save(
                    _currentStatement, _allOperations, _loadedFilePath,
                    _savedOrgInn, _savedOrgKpp, _savedOrgName,
                    _currentSessionId);

                // Если это новая сессия — запомним её ID для обновлений
                if (_currentSessionId == null)
                {
                    var sessions = BankStatementSessionService.GetAll();
                    _currentSessionId = sessions.FirstOrDefault()?.Id;
                }

                int marked = _allOperations.Count(o => o.IsMarkedForVatBook);
                StmtLoadStatusText.Text = $"Сессия сохранена. {marked} операций размечено.";
                RefreshSessionsList();
            }
            catch (Exception ex)
            {
                StmtLoadStatusText.Text = $"Ошибка сохранения сессии: {ex.Message}";
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // ВЫПИСКА БАНКА — Сопоставление с клиентами
        // ═════════════════════════════════════════════════════════════════════

        private void TryMatchClientsToOperations()
        {
            try
            {
                using var db = new AppDbContext();
                var byInn = db.Clients
                    .Where(c => c.Inn != null && c.Inn.Length > 0)
                    .ToDictionary(c => c.Inn!, c => c);

                foreach (var op in _allOperations)
                {
                    if (!string.IsNullOrEmpty(op.CounterpartyINN) && byInn.TryGetValue(op.CounterpartyINN, out var cl))
                    {
                        op.MatchedClientId = cl.Id;
                        op.MatchedClientName = cl.Name ?? "";
                    }
                }
            }
            catch { /* не критично */ }
        }

        // ═════════════════════════════════════════════════════════════════════
        // ВЫПИСКА БАНКА — Сводка и фильтрация
        // ═════════════════════════════════════════════════════════════════════

        private void ShowStatementSummary()
        {
            if (_currentStatement is null) return;
            var s = _currentStatement;
            StmtPeriodText.Text = s.PeriodDisplay;
            StmtAccountText.Text = s.AccountNumber.Length > 12
                ? s.AccountNumber[..4] + "…" + s.AccountNumber[^4..] : s.AccountNumber;
            StmtCreditText.Text = $"+{s.TotalCredit:N2} ₽";
            StmtDebitText.Text = $"-{s.TotalDebit:N2} ₽";
            StmtCountText.Text = $"{s.OperationCount} (▲{s.CreditCount} / ▼{s.DebitCount})";
        }

        private void StmtTypeFilter_Changed(object s, SelectionChangedEventArgs e) => ApplyStatementFilters();
        private void StmtSearch_Changed(object s, TextChangedEventArgs e) => ApplyStatementFilters();
        private void StmtCheck_Changed(object s, RoutedEventArgs e) => ApplyStatementFilters();

        private void ApplyStatementFilters()
        {
            if (_allOperations.Count == 0) return;
            // Защита от вызова до полной инициализации визуального дерева
            if (StmtSearchBox is null || StmtTypeFilterCombo is null || StmtOnlyMarkedCheckBox is null) return;

            var q = _allOperations.AsEnumerable();

            int typeIdx = StmtTypeFilterCombo.SelectedIndex;
            if (typeIdx == 1) q = q.Where(o => o.OperationType == BankOperationType.Credit);
            if (typeIdx == 2) q = q.Where(o => o.OperationType == BankOperationType.Debit);

            string search = StmtSearchBox.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(search))
                q = q.Where(o =>
                    o.CounterpartyName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    o.CounterpartyINN.Contains(search) ||
                    o.PaymentPurpose.Contains(search, StringComparison.OrdinalIgnoreCase));

            if (StmtOnlyMarkedCheckBox.IsChecked == true)
                q = q.Where(o => o.IsMarkedForVatBook);

            var filtered = q.ToList();
            StmtOperationsListView.ItemsSource = filtered;

            decimal credit = filtered.Where(o => o.OperationType == BankOperationType.Credit).Sum(o => o.Amount);
            decimal debit = filtered.Where(o => o.OperationType == BankOperationType.Debit).Sum(o => o.Amount);
            int marked = _allOperations.Count(o => o.IsMarkedForVatBook);

            StmtFilterResultText.Text = $"Найдено: {filtered.Count} из {_allOperations.Count}";
            StmtShownCountText.Text = $"{filtered.Count} шт.";
            StmtShownCreditText.Text = $"+{credit:N2} ₽";
            StmtShownDebitText.Text = $"-{debit:N2} ₽";
            StmtMarkedCountText.Text = $"{marked} шт.";

            // Период в нижней панели (требует обновлённого ToolsPage.xaml с x:Name="StmtFooterPeriodText")
            if (_currentStatement != null)
                if (FindName("StmtFooterPeriodText") is TextBlock tb)
                    tb.Text = _currentStatement.PeriodDisplay;
        }

        // ═════════════════════════════════════════════════════════════════════
        // ВЫПИСКА БАНКА — Диалог разметки НДС (одиночная операция)
        // ═════════════════════════════════════════════════════════════════════

        private async void StmtMarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            int idx = Convert.ToInt32(btn.Tag);
            var op = _allOperations.FirstOrDefault(o => o.LocalIndex == idx);
            if (op is null) return;
            await ShowVatMarkDialogAsync(op);
            ApplyStatementFilters();
        }

        private async Task ShowVatMarkDialogAsync(BankStatementOperation op)
        {
            var root = new StackPanel { Spacing = 14, Width = 430 };

            var infoBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["NiatecSurfaceAltBrush"],
                BorderBrush = (Brush)Application.Current.Resources["NiatecBorderBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
            };
            var infoStack = new StackPanel { Spacing = 4 };
            infoStack.Children.Add(new TextBlock
            {
                Text = $"{op.TypeDisplay}   {op.AmountDisplay} ₽   ·   {op.Date:dd.MM.yyyy}",
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["NiatecTextPrimaryBrush"],
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(op.CounterpartyINN)
                    ? op.CounterpartyName
                    : $"{op.CounterpartyName}  (ИНН {op.CounterpartyINN})",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["NiatecTextSecondaryBrush"],
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = op.PurposeShort,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["NiatecTextMutedBrush"],
            });
            infoBorder.Child = infoStack;
            root.Children.Add(infoBorder);

            var rbPurchases = new RadioButton { Content = "Книга покупок (приход = мы купили)", GroupName = "VatBook" };
            var rbSales = new RadioButton { Content = "Книга продаж (приход = нам заплатили)", GroupName = "VatBook" };
            rbPurchases.IsChecked = op.VatBookType != "Продажи";
            rbSales.IsChecked = op.VatBookType == "Продажи";
            var bookPanel = new StackPanel { Spacing = 6 };
            bookPanel.Children.Add(rbPurchases);
            bookPanel.Children.Add(rbSales);
            root.Children.Add(bookPanel);

            var sfGrid = new Grid { ColumnSpacing = 12 };
            sfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            string autoSfNum = op.IsMarkedForVatBook && !string.IsNullOrEmpty(op.VatInvoiceNumber)
                ? op.VatInvoiceNumber
                : ExtractInvoiceNumberFromPurpose(op.PaymentPurpose);
            var sfNumBox = new TextBox { Header = "Номер счёта-фактуры", PlaceholderText = "Например: 125", Text = autoSfNum };
            var sfDatePicker = new DatePicker { Header = "Дата счёта-фактуры" };
            sfDatePicker.Date = op.VatInvoiceDate.HasValue
                ? new DateTimeOffset(op.VatInvoiceDate.Value)
                : (op.Date != default ? new DateTimeOffset(op.Date) : DateTimeOffset.Now);
            Grid.SetColumn(sfNumBox, 0);
            Grid.SetColumn(sfDatePicker, 1);
            sfGrid.Children.Add(sfNumBox);
            sfGrid.Children.Add(sfDatePicker);
            root.Children.Add(sfGrid);

            var rateCombo = new ComboBox { Header = "Ставка НДС", HorizontalAlignment = HorizontalAlignment.Stretch };
            rateCombo.Items.Add(new ComboBoxItem { Content = "22%", Tag = 22m });
            rateCombo.Items.Add(new ComboBoxItem { Content = "20%", Tag = 20m });
            rateCombo.Items.Add(new ComboBoxItem { Content = "10%", Tag = 10m });
            rateCombo.Items.Add(new ComboBoxItem { Content = "0%", Tag = 0m });
            rateCombo.SelectedIndex = op.VatRate == 20m ? 1 : op.VatRate == 10m ? 2 : op.VatRate == 0m ? 3 : 0;
            root.Children.Add(rateCombo);

            decimal defaultRate = op.IsMarkedForVatBook ? op.VatRate : 22m;
            string initVat = op.VatAmount > 0
                ? op.VatAmount.ToString("F2")
                : CalcVatFromTotal(op.Amount, defaultRate).ToString("F2");
            var vatAmtBox = new TextBox
            {
                Header = $"Сумма НДС, ₽  (авторасчёт из {op.Amount:N2} ₽)",
                Text = initVat,
            };
            root.Children.Add(vatAmtBox);

            rateCombo.SelectionChanged += (s, _) =>
            {
                if (rateCombo.SelectedItem is ComboBoxItem ci && ci.Tag is decimal r)
                    vatAmtBox.Text = CalcVatFromTotal(op.Amount, r).ToString("F2");
            };

            root.Children.Add(new TextBlock
            {
                Text = "Авторасчёт: НДС выделяется из суммы платежа. Для «начислить» введите сумму вручную.",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
            });

            // ── Опция: сформировать УПД ───────────────────────────────────────
            var cbUpd = new CheckBox
            {
                Content = "📄 Сформировать УПД (статус 1) после сохранения",
                IsChecked = false,
            };
            root.Children.Add(cbUpd);

            var dialog = new ContentDialog
            {
                Title = "Разметка операции для НДС-книги",
                Content = root,
                PrimaryButtonText = "Сохранить разметку",
                SecondaryButtonText = op.IsMarkedForVatBook ? "Снять разметку" : null,
                CloseButtonText = "Отмена",
                XamlRoot = this.XamlRoot,
                DefaultButton = ContentDialogButton.Primary,
                RequestedTheme = ThemeService.IsLightTheme ? ElementTheme.Light : ElementTheme.Default,
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                decimal.TryParse(vatAmtBox.Text.Replace(',', '.'),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out decimal vatAmt);

                op.IsMarkedForVatBook = true;
                op.VatBookType = rbSales.IsChecked == true ? "Продажи" : "Покупки";
                op.VatInvoiceNumber = sfNumBox.Text.Trim();
                op.VatInvoiceDate = sfDatePicker.Date.DateTime;
                op.VatRate = rateCombo.SelectedItem is ComboBoxItem ci && ci.Tag is decimal r ? r : 22m;
                op.VatAmount = vatAmt;

                if (cbUpd.IsChecked == true)
                    DoGenerateUpdForOperation(op);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                op.IsMarkedForVatBook = false;
                op.VatBookType = "";
                op.VatInvoiceNumber = "";
                op.VatInvoiceDate = null;
                op.VatAmount = 0;
            }
        }

        private void DoGenerateUpdForOperation(BankStatementOperation op)
        {
            string inn = StmtExportInnBox.Text.Trim();
            string kpp = StmtExportKppBox.Text.Trim();
            string name = StmtExportNameBox.Text.Trim();

            if (string.IsNullOrEmpty(inn) || string.IsNullOrEmpty(name))
            { StmtLoadStatusText.Text = "Для УПД заполните ИНН и наименование в Шаге 3."; return; }

            // Дополнительные реквизиты из профиля организации
            string sellerAddress = "";
            string directorName = "";
            try
            {
                using var db = new AppDbContext();
                var org = db.OrganizationProfiles.FirstOrDefault(o => o.Inn == inn)
                          ?? db.OrganizationProfiles.FirstOrDefault();
                if (org != null)
                {
                    sellerAddress = org.LegalAddress ?? "";
                    directorName  = org.DirectorName ?? "";
                }
            }
            catch { /* не критично */ }

            try
            {
                string sfNum  = string.IsNullOrEmpty(op.VatInvoiceNumber) ? op.DocNumber : op.VatInvoiceNumber;
                DateTime sfDt = op.VatInvoiceDate ?? op.Date;

                var data = new BillingUpdPdfData
                {
                    Status         = "1",
                    DocumentNumber = sfNum,
                    DocumentDate   = sfDt,

                    SellerName     = name,
                    SellerInn      = inn,
                    SellerKpp      = kpp,
                    SellerAddress  = sellerAddress,
                    SellerDirector = directorName,

                    BuyerName      = op.CounterpartyName ?? "",
                    BuyerInn       = op.CounterpartyINN  ?? "",
                    BuyerKpp       = op.CounterpartyKPP  ?? "",

                    ShipperName    = "он же",
                    ConsigneeName  = op.CounterpartyName ?? "",

                    Lines = new System.Collections.Generic.List<BillingUpdPdfLine>
                    {
                        new BillingUpdPdfLine
                        {
                            Name             = op.PaymentPurpose,
                            UnitName         = "усл.",
                            Quantity         = 1,
                            PriceWithoutVat  = op.AmountWithoutVat,
                            VatRatePercent   = op.VatRate,
                        }
                    }
                };

                string outDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "ClientAccountApp", "УПД");
                Directory.CreateDirectory(outDir);

                string safe  = sfNum.Replace("/", "-").Replace("\\", "-");
                string fname = $"УПД_{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string fpath = Path.Combine(outDir, fname);

                new BillingUpdPdfService().Generate(data, fpath);

                StmtLoadStatusText.Text = $"УПД сформирован: {fpath}";
                Process.Start(new ProcessStartInfo { FileName = fpath, UseShellExecute = true });
            }
            catch (Exception ex) { StmtLoadStatusText.Text = $"Ошибка УПД: {ex.Message}"; }
        }

        /// <summary>НДС выделением из общей суммы: VAT = Total × Rate / (100 + Rate)</summary>
        private static decimal CalcVatFromTotal(decimal total, decimal rate) =>
            rate == 0 ? 0 : Math.Round(total * rate / (100m + rate), 2);

        // ═════════════════════════════════════════════════════════════════════
        // ВЫПИСКА БАНКА — Массовая разметка
        // ═════════════════════════════════════════════════════════════════════

        private async void StmtMarkAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_allOperations.Count == 0) { StmtLoadStatusText.Text = "Нет загруженных операций."; return; }
            await ShowBulkMarkDialogAsync();
            ApplyStatementFilters();
        }

        private async Task ShowBulkMarkDialogAsync()
        {
            var root = new StackPanel { Spacing = 12, Width = 480 };

            root.Children.Add(new TextBlock
            {
                Text = "Приходы  →  Книга продаж        Расходы  →  Книга покупок\nНомер СФ будет извлечён из назначения платежа, дата СФ = дата операции.",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });

            var rateCombo = new ComboBox { Header = "Ставка НДС для всех операций", HorizontalAlignment = HorizontalAlignment.Stretch };
            rateCombo.Items.Add(new ComboBoxItem { Content = "22%", Tag = 22m });
            rateCombo.Items.Add(new ComboBoxItem { Content = "20%", Tag = 20m });
            rateCombo.Items.Add(new ComboBoxItem { Content = "10%", Tag = 10m });
            rateCombo.Items.Add(new ComboBoxItem { Content = "0%", Tag = 0m });
            rateCombo.SelectedIndex = 0;
            root.Children.Add(rateCombo);

            root.Children.Add(new TextBlock
            {
                Text = "Исключить из разметки:",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });

            var exclusionDefs = new (ExclusionCategory Cat, string Label)[]
            {
                (ExclusionCategory.Loans,          "Займы и кредиты (займ, кредит, ссуда, погашение)"),
                (ExclusionCategory.Payroll,        "Зарплатные выплаты (зарплата, аванс, командировки, больничные, подотчёт, алименты, мат. помощь)"),
                (ExclusionCategory.Budget,         "Платежи в бюджет (Казначейство, ФНС, ИФНС, налоги, взносы, штрафы, пени)"),
                (ExclusionCategory.Transfers,      "Переводы между своими счетами"),
                (ExclusionCategory.Accountable,    "Возврат неиспользованных подотчётных средств"),
                (ExclusionCategory.CreditIssuance, "Выдача кредитных / заёмных средств"),
                (ExclusionCategory.Deposit,        "Депозиты и вклады (депозит, вклад, размещение, проценты по депозиту)"),
                (ExclusionCategory.NoVat,          "Операции с пометкой «НДС не облагается» / «без НДС»"),
            };

            var cbMap = new Dictionary<CheckBox, ExclusionCategory>();
            var excPanel = new Border
            {
                // NiatecSurfaceBrush: белый в светлой теме, тёмный в тёмной — контраст правильный в обеих
                Background = (Brush)Application.Current.Resources["NiatecSurfaceAltBrush"],
                BorderBrush = (Brush)Application.Current.Resources["NiatecBorderBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10, 12, 10),
            };
            var excStack = new StackPanel { Spacing = 8 };
            foreach (var (cat, label) in exclusionDefs)
            {
                var cb = new CheckBox { IsChecked = true };
                // Явно задаём TextBlock как Content — гарантирует видимость в любой теме
                cb.Content = new TextBlock
                {
                    Text = label,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)Application.Current.Resources["NiatecTextPrimaryBrush"],
                };
                cbMap[cb] = cat;
                excStack.Children.Add(cb);
            }
            excPanel.Child = excStack;
            root.Children.Add(excPanel);

            var cbSkipMarked = new CheckBox { IsChecked = true };
            cbSkipMarked.Content = new TextBlock
            {
                Text = "Не перезаписывать уже размеченные операции",
                Foreground = (Brush)Application.Current.Resources["NiatecTextPrimaryBrush"],
            };
            root.Children.Add(cbSkipMarked);

            var previewText = new TextBlock
            {
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["NiatecAccentBrush"],
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            };
            root.Children.Add(previewText);

            HashSet<ExclusionCategory> GetEnabledCats() =>
                new(cbMap.Where(kv => kv.Key.IsChecked == true).Select(kv => kv.Value));

            void UpdatePreview()
            {
                var (cr, db, ex) = GetOperationsForBulkMark(GetEnabledCats(), cbSkipMarked.IsChecked == true);
                previewText.Text = $"Будет размечено: {cr} приходов + {db} расходов = {cr + db} операций." +
                    (ex > 0 ? $"\nИсключено: {ex} шт." : "");
            }

            foreach (var cb in cbMap.Keys)
            {
                cb.Checked += (s, e) => UpdatePreview();
                cb.Unchecked += (s, e) => UpdatePreview();
            }
            cbSkipMarked.Checked += (s, e) => UpdatePreview();
            cbSkipMarked.Unchecked += (s, e) => UpdatePreview();
            UpdatePreview();

            var dialog = new ContentDialog
            {
                Title = "Массовая разметка операций",
                Content = root,
                PrimaryButtonText = "Разметить",
                CloseButtonText = "Отмена",
                XamlRoot = this.XamlRoot,
                DefaultButton = ContentDialogButton.Primary,
                RequestedTheme = ThemeService.IsLightTheme ? ElementTheme.Light : ElementTheme.Default,
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            var enabledCats = GetEnabledCats();
            bool skipMark = cbSkipMarked.IsChecked == true;
            decimal vat = rateCombo.SelectedItem is ComboBoxItem ci && ci.Tag is decimal r ? r : 22m;

            int count = 0;
            foreach (var op in _allOperations)
            {
                if (skipMark && op.IsMarkedForVatBook) continue;
                if (ShouldExclude(op, enabledCats)) continue;

                op.IsMarkedForVatBook = true;
                op.VatBookType = op.OperationType == BankOperationType.Credit ? "Продажи" : "Покупки";
                op.VatRate = vat;
                op.VatAmount = CalcVatFromTotal(op.Amount, vat);

                if (string.IsNullOrEmpty(op.VatInvoiceNumber))
                    op.VatInvoiceNumber = ExtractInvoiceNumberFromPurpose(op.PaymentPurpose);
                if (!op.VatInvoiceDate.HasValue)
                    op.VatInvoiceDate = op.Date;

                count++;
            }

            StmtLoadStatusText.Text = $"Массовая разметка выполнена: {count} операций размечено.";
        }

        private (int credits, int debits, int excluded) GetOperationsForBulkMark(
            HashSet<ExclusionCategory> excludedCats, bool skipMarked)
        {
            int cr = 0, db = 0, ex = 0;
            foreach (var op in _allOperations)
            {
                if (skipMarked && op.IsMarkedForVatBook) continue;
                if (ShouldExclude(op, excludedCats)) { ex++; continue; }
                if (op.OperationType == BankOperationType.Credit) cr++; else db++;
            }
            return (cr, db, ex);
        }

        // ═════════════════════════════════════════════════════════════════════
        // ВЫПИСКА БАНКА — Экспорт XML
        // ═════════════════════════════════════════════════════════════════════

        private void InitExportYearCombo()
        {
            StmtExportYearCombo.Items.Clear();
            int cy = DateTime.Now.Year;
            for (int y = cy; y >= cy - 4; y--)
                StmtExportYearCombo.Items.Add(new ComboBoxItem { Content = y.ToString(), Tag = y });
            StmtExportYearCombo.SelectedIndex = 0;

            if (_currentStatement is not null)
            {
                int q = (_currentStatement.DateFrom.Month - 1) / 3 + 1;
                StmtExportQuarterCombo.SelectedIndex = q - 1;
            }
        }

        private void TryFillOrgInfoFromSettings()
        {
            // Не перезаписываем, если поля уже заполнены
            if (!string.IsNullOrEmpty(StmtExportInnBox.Text)) return;

            try
            {
                using var db = new AppDbContext();
                // Читаем активную организацию
                // Подставьте реальные имена полей из OrganizationProfile (Inn/Kpp/Name)
                var org = db.OrganizationProfiles.FirstOrDefault();
                if (org is null) return;

                StmtExportInnBox.Text = org.Inn ?? "";
                StmtExportKppBox.Text = org.Kpp ?? "";
                StmtExportNameBox.Text = org.Name ?? "";
            }
            catch { /* Поля можно заполнить вручную */ }
        }

        private void StmtExportPurchases_Click(object sender, RoutedEventArgs e) => DoExportXml(isPurchases: true);
        private void StmtExportSales_Click(object sender, RoutedEventArgs e) => DoExportXml(isPurchases: false);

        private void DoExportXml(bool isPurchases)
        {
            if (_allOperations.Count == 0) { StmtExportStatusText.Text = "Нет загруженных операций."; return; }

            string inn = StmtExportInnBox.Text.Trim();
            string kpp = StmtExportKppBox.Text.Trim();
            string name = StmtExportNameBox.Text.Trim();

            if (string.IsNullOrEmpty(inn) || string.IsNullOrEmpty(name))
            { StmtExportStatusText.Text = "Заполните ИНН и наименование организации."; return; }

            // Сохраняем введённые реквизиты — восстановятся при навигации
            _savedOrgInn = inn;
            _savedOrgKpp = kpp;
            _savedOrgName = name;

            int year = StmtExportYearCombo.SelectedItem is ComboBoxItem yi && yi.Tag is int y ? y : DateTime.Now.Year;
            int qtr = StmtExportQuarterCombo.SelectedIndex + 1;

            string bookName = isPurchases ? "Покупки" : "Продажи";
            int marked = _allOperations.Count(o => o.IsMarkedForVatBook && o.VatBookType == bookName);

            if (marked == 0)
            { StmtExportStatusText.Text = $"Нет строк, размеченных для книги «{bookName}». Разметьте операции на Шаге 2."; return; }

            try
            {
                string outDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "ClientAccountApp", "ВыпискаНДС");

                var org = new BankStatementVatXmlExporter.OrgInfo(inn, kpp, name);
                string filePath = isPurchases
                    ? BankStatementVatXmlExporter.ExportKnigaPokupok(_allOperations, org, year, qtr, outDir)
                    : BankStatementVatXmlExporter.ExportKnigaProdazh(_allOperations, org, year, qtr, outDir);

                StmtExportStatusText.Text = $"XML сформирован ({marked} строк): {filePath}";
                Process.Start(new ProcessStartInfo { FileName = outDir, UseShellExecute = true });
            }
            catch (Exception ex) { StmtExportStatusText.Text = $"Ошибка формирования XML: {ex.Message}"; }
        }

        private void StmtExportKudir_Click(object sender, RoutedEventArgs e) => DoExportKudir();

        private void DoExportKudir()
        {
            if (_allOperations.Count == 0) { StmtExportStatusText.Text = "Нет загруженных операций."; return; }

            string inn = StmtExportInnBox.Text.Trim();
            string kpp = StmtExportKppBox.Text.Trim();
            string name = StmtExportNameBox.Text.Trim();

            if (string.IsNullOrEmpty(inn) || string.IsNullOrEmpty(name))
            { StmtExportStatusText.Text = "Заполните ИНН и наименование организации."; return; }

            _savedOrgInn = inn;
            _savedOrgKpp = kpp;
            _savedOrgName = name;

            int year = StmtExportYearCombo.SelectedItem is ComboBoxItem yi && yi.Tag is int y ? y : DateTime.Now.Year;
            int qtr = StmtExportQuarterCombo.SelectedIndex + 1;

            var usnType = (FindName("StmtUsnTypeCombo") is ComboBox usnCombo && usnCombo.SelectedIndex == 1)
                ? BankStatementUsnXmlExporter.UsnType.IncomeMinus15
                : BankStatementUsnXmlExporter.UsnType.Income6;

            bool incl15 = usnType == BankStatementUsnXmlExporter.UsnType.IncomeMinus15;
            int total = _allOperations.Count(o => o.IsMarkedForVatBook &&
                (o.VatBookType == "Продажи" || (incl15 && o.VatBookType == "Покупки")));

            if (total == 0)
            {
                StmtExportStatusText.Text = "Нет размеченных операций для КУДиР. Разметьте: приходы → «Книга продаж» (доходы), расходы → «Книга покупок» (расходы 15%).";
                return;
            }

            try
            {
                string outDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "ClientAccountApp", "ВыпискаНДС");

                // Банковские реквизиты из профиля организации (для блока <Банки>)
                string bankName = "", bankAccount = "", bankBic = "";
                try
                {
                    using var db = new AppDbContext();
                    var orgPr = db.OrganizationProfiles.FirstOrDefault(o => o.Inn == inn)
                                ?? db.OrganizationProfiles.FirstOrDefault();
                    if (orgPr != null)
                    {
                        bankName    = orgPr.BankName ?? "";
                        bankAccount = orgPr.SettlementAccount ?? "";
                        bankBic     = orgPr.BankBic ?? "";
                    }
                }
                catch { }

                var org = new BankStatementUsnXmlExporter.OrgInfo(
                    inn, kpp, name, bankName, bankAccount, bankBic);
                string filePath = BankStatementUsnXmlExporter.ExportKudir(
                    _allOperations, org, year, qtr, usnType, outDir);

                StmtExportStatusText.Text = $"КУДиР XML сформирован ({total} строк): {filePath}";
                Process.Start(new ProcessStartInfo { FileName = outDir, UseShellExecute = true });
            }
            catch (Exception ex) { StmtExportStatusText.Text = $"Ошибка формирования КУДиР: {ex.Message}"; }
        }

        private async void StmtExportKudirExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_allOperations.Count == 0)
            {
                await ShowKudirEmptyDialog();
                return;
            }
            DoExportKudirExcel();
        }

        private void DoExportKudirExcel()
        {
            if (_allOperations.Count == 0) { StmtExportStatusText.Text = "Нет загруженных операций."; return; }

            string inn = StmtExportInnBox.Text.Trim();
            string kpp = StmtExportKppBox.Text.Trim();
            string name = StmtExportNameBox.Text.Trim();

            if (string.IsNullOrEmpty(inn) || string.IsNullOrEmpty(name))
            { StmtExportStatusText.Text = "Заполните ИНН и наименование организации."; return; }

            _savedOrgInn = inn;
            _savedOrgKpp = kpp;
            _savedOrgName = name;

            int year = StmtExportYearCombo.SelectedItem is ComboBoxItem yi && yi.Tag is int y ? y : DateTime.Now.Year;
            int qtr = StmtExportQuarterCombo.SelectedIndex + 1;

            var usnType = (FindName("StmtUsnTypeCombo") is ComboBox usnCombo && usnCombo.SelectedIndex == 1)
                ? BankStatementUsnXmlExporter.UsnType.IncomeMinus15
                : BankStatementUsnXmlExporter.UsnType.Income6;

            bool incl15 = usnType == BankStatementUsnXmlExporter.UsnType.IncomeMinus15;
            int total = _allOperations.Count(o => o.IsMarkedForVatBook &&
                (o.VatBookType == "Продажи" || (incl15 && o.VatBookType == "Покупки")));

            if (total == 0)
            {
                _ = ShowKudirEmptyDialog();
                return;
            }

            try
            {
                string outDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "ClientAccountApp", "ВыпискаНДС");

                var org = new BankStatementUsnXmlExporter.OrgInfo(inn, kpp, name);
                string account = _currentStatement?.AccountNumber ?? "";
                string filePath = BankStatementUsnExcelExporter.ExportKudirToExcel(
                    _allOperations, org, account, year, qtr, usnType, outDir);

                StmtExportStatusText.Text = $"КУДиР Excel сформирован ({total} строк): {filePath}";
                Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
            }
            catch (Exception ex) { StmtExportStatusText.Text = $"Ошибка формирования КУДиР Excel: {ex.Message}"; }
        }

        private async Task ShowKudirEmptyDialog()
        {
            var dlg = new ContentDialog
            {
                Title = "Нет данных для КУДиР",
                Content = "Операции ещё не размечены.\n\n" +
                                  "Шаг 2 → нажмите «⚡ Разметить всё».\n\n" +
                                  "Разметка для КУДиР:\n" +
                                  "• Приходы → «Книга продаж» (Доходы)\n" +
                                  "• Расходы → «Книга покупок» (Расходы, УСН 15%)",
                CloseButtonText = "Понятно",
                XamlRoot = this.XamlRoot,
            };
            await dlg.ShowAsync();
        }

        // ═════════════════════════════════════════════════════════════════════
        // ВЫПИСКА БАНКА — Категории исключений
        // ═════════════════════════════════════════════════════════════════════

        private enum ExclusionCategory
        {
            Loans, Payroll, Budget, Transfers, Accountable, CreditIssuance, Deposit, NoVat
        }

        private static readonly Dictionary<ExclusionCategory, string[]> ExclusionKeywords = new()
        {
            [ExclusionCategory.Loans] = new[]
            {
                "займ", "заем", "кредит", "ссуда", "возврат займ", "погашен",
            },
            [ExclusionCategory.Payroll] = new[]
            {
                "зарплат", "заработн", "з/п", " зп ", "аванс по зарплат", "аванс по заработн",
                "материальн", "мат.помощ", "мат. помощ", "материальная помощ",
                "командировочн", "командировк",
                "больничн", "листок нетрудоспособ",
                "выплата сотрудник", "выплата работник",
                "под отчет", "подотчет", "в подотчет", "подотчетн",
                "алимент",
            },
            [ExclusionCategory.Budget] = new[]
            {
                // Проверяется и по назначению платежа, и по наименованию контрагента
                "казначейство", "фнс", "ифнс", "федеральная налоговая",
                "единый налоговый", "налог на прибыл", "налог на имущ", "налог на добавл",
                "страховые взносы", "страховой взнос", "уплата взносов",
                "пенсионный фонд", "пфр ", "фсс ", "ффомс", " омс ",
                "штраф", " пени ", "госпошлин",
                "уплата налог", "перечисление налог",
            },
            [ExclusionCategory.Transfers] = new[]
            {
                "перевод между счет", "перевод собственных средств",
                "пополнение счета", "пополнение р/с",
                "перечисление собственных", "перевод на р/с",
            },
            [ExclusionCategory.Accountable] = new[]
            {
                "возврат подотчет", "возврат неиспользованн", "неиспользованный остат",
            },
            [ExclusionCategory.CreditIssuance] = new[]
            {
                "выдача кредит", "выдача займ", "выдача заем",
                "предоставление займ", "предоставление кредит", "выдача ссуд",
            },
            [ExclusionCategory.NoVat] = new[]
            {
                "без ндс", "ндс не облагается", "не облагается ндс",
                "не является объектом ндс", "освобождено от ндс", "ндс не предусмотрен",
                "ндс не предусм", "без налога",
            },
        };

        /// <summary>
        /// Проверяет назначение платежа И наименование контрагента.
        /// ФНС/Казначейство часто определяется по имени контрагента.
        /// </summary>
        private static bool ShouldExclude(BankStatementOperation op, HashSet<ExclusionCategory> cats)
        {
            if (cats.Count == 0) return false;
            string purpose = op.PaymentPurpose.ToLower();
            string counterparty = op.CounterpartyName.ToLower();

            foreach (var cat in cats)
            {
                if (!ExclusionKeywords.TryGetValue(cat, out var kw)) continue;
                if (kw.Any(k => purpose.Contains(k) || counterparty.Contains(k)))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Извлекает номер счёта из назначения платежа.
        /// Ищет паттерны: "по счету №125", "сч. №125", "счет-фактура №125", "№125".
        /// </summary>
        private static string ExtractInvoiceNumberFromPurpose(string purpose)
        {
            if (string.IsNullOrWhiteSpace(purpose)) return "";

            var m = Regex.Match(purpose,
                @"(?:счет[уа]?[-\s]*фактур[уа]?|счет[уа]?|сч\.?)\s*[№N]?\s*(\w[\w\/\-]*)",
                RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.Trim();

            m = Regex.Match(purpose, @"[№N]\s*(\w[\w\/\-]*)");
            if (m.Success) return m.Groups[1].Value.Trim();

            return "";
        }

        // ═════════════════════════════════════════════════════════════════════
        // Общие хелперы
        // ═════════════════════════════════════════════════════════════════════

        private static bool TryParseDecimal(string? text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string n = text.Trim().Replace(" ", "").Replace("\u00A0", "");
            if (decimal.TryParse(n, NumberStyles.Number, new CultureInfo("ru-RU"), out value)) return true;
            return decimal.TryParse(n.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private static string FormatMoney(decimal v) => v.ToString("N2", new CultureInfo("ru-RU")) + " ₽";
    }
}