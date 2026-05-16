using ClientAccountApp.Models;
using ClientAccountApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ClientAccountApp
{
    // ── ViewModel для таблицы сотрудников ─────────────────────────────────────

    public class RsvEmployeeVm
    {
        public int     LocalIndex      { get; set; }
        public string  FullName        { get; set; } = "";
        public string  SNILS           { get; set; } = "";
        public string  NrPayDisplay    { get; set; } = "";
        public string  PvPayDisplay    { get; set; } = "";
        public string  CategoryCode    { get; set; } = "";
        public string  NrContribDisplay{ get; set; } = "";
        public string  PvContribDisplay{ get; set; } = "";
        public string  ContribDisplay  { get; set; } = "";
        public bool    HasPV           { get; set; }
        public RsvEmployee Source      { get; set; } = null!;
    }

    public sealed partial class RsvPage : Page
    {
        private List<RsvEmployee>? _empM1, _empM2, _empM3;
        private List<RsvEmployee>  _employees = new();
        private string?            _currentSessionId;

        private string _outputFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ClientAccountApp", "РСВ");

        public RsvPage()
        {
            this.InitializeComponent();

            int cy = DateTime.Now.Year;
            for (int y = cy; y >= cy - 3; y--)
                RsvYearCombo.Items.Add(new ComboBoxItem { Content = y.ToString(), Tag = y });
            RsvYearCombo.SelectedIndex = 0;

            TryLoadOrgFromProfile();
            RefreshSessionsList();
        }

        // ── Профиль организации ───────────────────────────────────────────────

        private void TryLoadOrgFromProfile()
        {
            try
            {
                using var db = new AppDbContext();
                var org = db.OrganizationProfiles.FirstOrDefault();
                if (org == null) return;
                if (string.IsNullOrEmpty(RsvInnBox.Text))   RsvInnBox.Text   = org.Inn  ?? "";
                if (string.IsNullOrEmpty(RsvKppBox.Text))   RsvKppBox.Text   = org.Kpp  ?? "";
                if (string.IsNullOrEmpty(RsvNameBox.Text))  RsvNameBox.Text  = org.Name ?? "";
                if (string.IsNullOrEmpty(RsvPhoneBox.Text)) RsvPhoneBox.Text = org.Phone ?? "";
                if (string.IsNullOrEmpty(RsvDirSurnameBox.Text) &&
                    !string.IsNullOrEmpty(org.DirectorName))
                {
                    var p = org.DirectorName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 1) RsvDirSurnameBox.Text    = p[0];
                    if (p.Length >= 2) RsvDirNameBox.Text       = p[1];
                    if (p.Length >= 3) RsvDirPatronymicBox.Text = p[2];
                }
            }
            catch (Exception _ex)
            {
                AppLogger.LogError("RsvPage.TryLoadOrgFromProfile", _ex);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // СЕССИИ
        // ═════════════════════════════════════════════════════════════════════

        private void RefreshSessionsList()
        {
            var sessions = RsvSessionService.GetAll();
            if (sessions.Count == 0)
            {
                RsvSessionsCard.Visibility = Visibility.Collapsed;
                return;
            }
            RsvSessionsCard.Visibility    = Visibility.Visible;
            RsvSessionsListView.ItemsSource = sessions;
        }

        private void RsvHideSessionsButton_Click(object sender, RoutedEventArgs e)
            => RsvSessionsCard.Visibility = Visibility.Collapsed;

        private void RsvLoadSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string filePath) return;
            if (!File.Exists(filePath))
            {
                PfsStatusText.Text = "Файл сессии не найден.";
                RefreshSessionsList();
                return;
            }
            try
            {
                var (emps, settings, id) = RsvSessionService.Load(filePath);
                _employees        = emps;
                _currentSessionId = id;
                ApplySettingsToUI(settings);
                RecalcAndRefresh();
                ShowResultCards();
                PfsStatusText.Text = $"Сессия загружена: {emps.Count} сотрудников.";
            }
            catch (Exception ex)
            {
                PfsStatusText.Text = $"Ошибка загрузки сессии: {ex.Message}";
            }
        }

        private void RsvDeleteSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string filePath) return;
            RsvSessionService.Delete(filePath);
            RefreshSessionsList();
        }

        private void RsvSaveSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_employees.Count == 0)
            { RsvGenerateStatusText.Text = "Нет данных для сохранения."; return; }
            try
            {
                var settings = CollectSettings();
                _currentSessionId = RsvSessionService.Save(
                    _employees, settings, _currentSessionId);
                RsvGenerateStatusText.Text =
                    $"✓ Сессия сохранена ({_employees.Count} сотр.)";
                RefreshSessionsList();
            }
            catch (Exception ex)
            {
                RsvGenerateStatusText.Text = $"Ошибка сохранения: {ex.Message}";
            }
        }

        private void ApplySettingsToUI(RsvSettings s)
        {
            RsvInnBox.Text  = s.OrgINN;
            RsvKppBox.Text  = s.OrgKPP;
            RsvNameBox.Text = s.OrgName;
            RsvPhoneBox.Text = s.Phone;
            RsvHeadcountBox.Text = s.AvgHeadcount.ToString();
            RsvDirSurnameBox.Text    = s.DirectorSurname;
            RsvDirNameBox.Text       = s.DirectorName;
            RsvDirPatronymicBox.Text = s.DirectorPatronymic;
            RsvKodNoBox.Text  = s.KodNO;
            RsvOktmoBox.Text  = s.OKTMO;
            RsvKbkBox.Text    = s.KBK;
            if (RsvMrotBox != null)
                RsvMrotBox.Text = s.MROTPerMonth.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

            var yearItem = RsvYearCombo.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(ci => (int)(ci.Tag ?? 0) == s.Year);
            if (yearItem != null) RsvYearCombo.SelectedItem = yearItem;
            if (s.Quarter >= 1 && s.Quarter <= 4)
                RsvQuarterCombo.SelectedIndex = s.Quarter - 1;

            // Тариф
            var tariffItem = RsvTariffCombo.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(ci =>
                    (ci.Tag?.ToString() ?? "").StartsWith(s.NrTariffCode + "|"));
            if (tariffItem != null) RsvTariffCombo.SelectedItem = tariffItem;
        }

        // ═════════════════════════════════════════════════════════════════════
        // ПФС-ФАЙЛЫ
        // ═════════════════════════════════════════════════════════════════════

        private async void PfsBrowse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            int slot = int.Parse(btn.Tag?.ToString() ?? "1");
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".xml");
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker,
                WindowNative.GetWindowHandle(App.MainAppWindow));
            var file = await picker.PickSingleFileAsync();
            if (file != null) LoadPfsFile(slot, file.Path);
        }

        private void LoadPfsFile(int slot, string path)
        {
            try
            {
                var (info, emps) = RsvGeneratorService.ParsePfs(path);
                string nrCat = GetNrCatCode();
                foreach (var e in emps) e.CategoryCodeNR = nrCat;

                switch (slot)
                {
                    case 1: _empM1 = emps;
                        PfsFile1TextBox.Text = $"[{info.MonthNum:D2}/{info.Year}] {Path.GetFileName(path)} — {info.EmployeeCount} сотр."; break;
                    case 2: _empM2 = emps;
                        PfsFile2TextBox.Text = $"[{info.MonthNum:D2}/{info.Year}] {Path.GetFileName(path)} — {info.EmployeeCount} сотр."; break;
                    case 3: _empM3 = emps;
                        PfsFile3TextBox.Text = $"[{info.MonthNum:D2}/{info.Year}] {Path.GetFileName(path)} — {info.EmployeeCount} сотр."; break;
                }

                // Всегда обновляем реквизиты из ПФС
                if (!string.IsNullOrEmpty(info.OrgINN))  RsvInnBox.Text   = info.OrgINN;
                if (!string.IsNullOrEmpty(info.OrgKPP))  RsvKppBox.Text   = info.OrgKPP;
                if (!string.IsNullOrEmpty(info.OrgName)) RsvNameBox.Text  = info.OrgName;
                if (!string.IsNullOrEmpty(info.KodNO))   RsvKodNoBox.Text = info.KodNO;

                var yi = RsvYearCombo.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(ci => (int)(ci.Tag ?? 0) == info.Year);
                if (yi != null) RsvYearCombo.SelectedItem = yi;

                PfsStatusText.Text = $"Файл {slot}: {info.StatusText}";
            }
            catch (Exception ex)
            {
                PfsStatusText.Text = $"Ошибка файла {slot}: {ex.Message}";
            }
        }

        private void PfsClear_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            int slot = int.Parse(btn.Tag?.ToString() ?? "1");
            switch (slot)
            {
                case 1: _empM1 = null; PfsFile1TextBox.Text = ""; break;
                case 2: _empM2 = null; PfsFile2TextBox.Text = ""; break;
                case 3: _empM3 = null; PfsFile3TextBox.Text = ""; break;
            }
        }

        private void ProcessPfsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_empM1 == null && _empM2 == null && _empM3 == null)
            { PfsStatusText.Text = "Загрузите хотя бы один ПФС-файл."; return; }

            _employees        = RsvGeneratorService.MergeEmployees(_empM1, _empM2, _empM3);
            _currentSessionId = null;

            // Авто-разбивка НР/ПВ по 1.5×МРОТ (если включена)
            ApplyAutoSplit();

            RecalcAndRefresh();
            ShowResultCards();
            PfsStatusText.Text = $"Объединено: {_employees.Count} сотр. | " +
                $"Порог НР/ПВ: {GetMrotThreshold():N2} ₽/мес.";
        }

        private void ApplyAutoSplit()
        {
            decimal threshold = GetMrotThreshold();
            if (threshold <= 0) return;
            RsvGeneratorService.AutoSplitNrPv(_employees, GetMrot());
        }

        private decimal GetMrot()
        {
            if (decimal.TryParse(RsvMrotBox?.Text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var m) && m > 0)
                return m;
            return 27093m;
        }

        private decimal GetMrotThreshold() =>
            Math.Round(GetMrot() * 1.5m, 2);

        private void RsvMrotBox_Changed(object sender, TextChangedEventArgs e)
        {
            decimal threshold = GetMrotThreshold();
            if (RsvMrotThresholdText != null)
                RsvMrotThresholdText.Text = $"1.5×МРОТ = {threshold:N2} ₽";

            // Если данные уже загружены — пересчитать разбивку
            if (_employees.Count > 0)
            {
                ApplyAutoSplit();
                RecalcAndRefresh();
            }
        }

        private void ShowResultCards()
        {
            RsvSettingsCard.Visibility  = Visibility.Visible;
            RsvEmployeesCard.Visibility = Visibility.Visible;
            RsvGenerateCard.Visibility  = Visibility.Visible;
        }

        // ═════════════════════════════════════════════════════════════════════
        // РАСЧЁТ И ОТОБРАЖЕНИЕ
        // ═════════════════════════════════════════════════════════════════════

        private void RecalcAndRefresh()
        {
            decimal nrRate = GetNrRate();
            string  nrCat  = GetNrCatCode();
            foreach (var e in _employees) e.CategoryCodeNR = nrCat;

            RsvGeneratorService.CalculateContributions(_employees, nrRate, 0.15m);

            var vms = _employees.Select((e, i) => new RsvEmployeeVm
            {
                LocalIndex       = i + 1,
                FullName         = e.FullName,
                SNILS            = e.SNILS,
                NrPayDisplay     = $"{e.PayNR1:N2} / {e.PayNR2:N2} / {e.PayNR3:N2}",
                PvPayDisplay     = e.HasPvPayments
                    ? $"{e.PayPV1:N2} / {e.PayPV2:N2} / {e.PayPV3:N2}" : "—",
                CategoryCode     = e.CategoryCodeNR,
                NrContribDisplay = $"{e.TotalContribNR:N2} ₽",
                PvContribDisplay = e.HasPvPayments ? $"{e.TotalContribPV:N2} ₽" : "—",
                ContribDisplay   = $"{e.TotalContrib:N2} ₽",
                HasPV            = e.HasPvPayments,
                Source           = e,
            }).ToList();

            RsvEmployeesListView.ItemsSource = vms;

            decimal totalNrBase  = _employees.Sum(x => x.TotalBaseNR);
            decimal totalPvBase  = _employees.Sum(x => x.TotalBasePV);
            decimal totalNrContr = _employees.Sum(x => x.TotalContribNR);
            decimal totalPvContr = _employees.Sum(x => x.TotalContribPV);
            decimal totalContr   = totalNrContr + totalPvContr;

            RsvTotalEmpText.Text     = _employees.Count.ToString();
            RsvTotalNrPayText.Text   = $"{totalNrBase:N2} ₽";
            RsvTotalPvPayText.Text   = totalPvBase > 0 ? $"{totalPvBase:N2} ₽" : "—";
            RsvTotalNrContribText.Text = $"{totalNrContr:N2} ₽";
            RsvTotalPvContribText.Text = totalPvBase > 0 ? $"{totalPvContr:N2} ₽" : "—";
            RsvTotalContribText.Text = $"{totalContr:N2} ₽";

            int pvCount = _employees.Count(x => x.HasPvPayments);
            RsvGenerateStatusText.Text = pvCount > 0
                ? $"ℹ {pvCount} сотр. с ПВ-выплатами — будет сформирован блок тарифа 32 (15%)."
                : "";
        }

        // ═════════════════════════════════════════════════════════════════════
        // ТАРИФ
        // ═════════════════════════════════════════════════════════════════════

        private void RsvTariffCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (RsvItBenefitCheckBox == null) return; // инициализация ещё не завершена

            bool isIT = GetNrTariffCode() == "06";
            RsvItBenefitCheckBox.Visibility =
                isIT ? Visibility.Visible : Visibility.Collapsed;
            if (RsvItBenefitPanel != null)
                RsvItBenefitPanel.Visibility = Visibility.Collapsed;

            bool hasPV = _employees.Any(x => x.HasPvPayments);
            if (RsvAgrBenefitPanel != null)
                RsvAgrBenefitPanel.Visibility =
                    hasPV ? Visibility.Visible : Visibility.Collapsed;

            if (_employees.Count > 0) RecalcAndRefresh();
        }

        // FIX: InvariantCulture — не заменяем точку на запятую
        private decimal GetNrRate()
        {
            if (RsvTariffCombo?.SelectedItem is ComboBoxItem ci)
            {
                var p = (ci.Tag?.ToString() ?? "").Split('|');
                if (p.Length >= 2 && decimal.TryParse(
                    p[1],
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var r)) return r;
            }
            return 0.30m;
        }

        private string GetNrCatCode()
        {
            if (RsvTariffCombo?.SelectedItem is ComboBoxItem ci)
            {
                var p = (ci.Tag?.ToString() ?? "").Split('|');
                if (p.Length >= 3 && !string.IsNullOrEmpty(p[2])) return p[2];
            }
            return "НР";
        }

        private string GetNrTariffCode()
        {
            if (RsvTariffCombo?.SelectedItem is ComboBoxItem ci)
            {
                var p = (ci.Tag?.ToString() ?? "").Split('|');
                if (p.Length >= 1 && p[0] != "custom") return p[0];
            }
            return "01";
        }

        // ═════════════════════════════════════════════════════════════════════
        // IT-ЛЬГОТА
        // ═════════════════════════════════════════════════════════════════════

        private void RsvItBenefit_Changed(object sender, RoutedEventArgs e)
        {
            if (RsvItBenefitPanel != null)
                RsvItBenefitPanel.Visibility =
                    RsvItBenefitCheckBox.IsChecked == true
                        ? Visibility.Visible : Visibility.Collapsed;
        }

        // ═════════════════════════════════════════════════════════════════════
        // РЕДАКТИРОВАНИЕ СОТРУДНИКА
        // ═════════════════════════════════════════════════════════════════════

        private async void RsvEditEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (RsvEmployeesListView.SelectedItem is not RsvEmployeeVm vm) return;
            await ShowEmployeeEditDialog(vm.Source);
            RecalcAndRefresh();
        }

        private async Task ShowEmployeeEditDialog(RsvEmployee emp)
        {
            var root = new StackPanel { Spacing = 12, Width = 520 };

            TextBlock MakeHeader(string text) => new TextBlock
            {
                Text = text, FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)
                    Application.Current.Resources["NiatecAccentBrush"],
            };
            TextBox MakeBox(string header, decimal val) =>
                new TextBox { Header = header, Text = val.ToString("0.00") };

            root.Children.Add(new TextBlock
            {
                Text = emp.FullName, FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)
                    Application.Current.Resources["NiatecTextPrimaryBrush"],
            });
            root.Children.Add(new TextBlock
            {
                Text = $"СНИЛС: {emp.SNILS}  ИНН: {emp.INN}", FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)
                    Application.Current.Resources["NiatecTextSecondaryBrush"],
            });

            // НР выплаты
            root.Children.Add(MakeHeader("НР-выплаты (оклад, премии) — основной тариф"));
            var nrGrid = MakeGrid3();
            var nr1 = MakeBox("НР М1, ₽", emp.PayNR1);
            var nr2 = MakeBox("НР М2, ₽", emp.PayNR2);
            var nr3 = MakeBox("НР М3, ₽", emp.PayNR3);
            AddToGrid3(nrGrid, nr1, nr2, nr3);
            root.Children.Add(nrGrid);

            // Необлагаемые
            root.Children.Add(MakeHeader("Необлагаемые из НР (больничные, матпомощь)"));
            var exGrid = MakeGrid3();
            var ex1 = MakeBox("Необлаг. М1, ₽", emp.ExemptNR1);
            var ex2 = MakeBox("Необлаг. М2, ₽", emp.ExemptNR2);
            var ex3 = MakeBox("Необлаг. М3, ₽", emp.ExemptNR3);
            AddToGrid3(exGrid, ex1, ex2, ex3);
            root.Children.Add(exGrid);

            // ПВ выплаты
            root.Children.Add(MakeHeader("ПВ-выплаты (сезонные) — льготный тариф 15%"));
            var pvGrid = MakeGrid3();
            var pv1 = MakeBox("ПВ М1, ₽", emp.PayPV1);
            var pv2 = MakeBox("ПВ М2, ₽", emp.PayPV2);
            var pv3 = MakeBox("ПВ М3, ₽", emp.PayPV3);
            AddToGrid3(pvGrid, pv1, pv2, pv3);
            root.Children.Add(pvGrid);

            // Доп. сведения
            root.Children.Add(MakeHeader("Доп. сведения (опционально)"));
            var detGrid = MakeGrid3();
            var bdBox = new TextBox { Header = "Дата рождения", Text = emp.BirthDate, PlaceholderText = "дд.мм.гггг" };
            var genCombo = new ComboBox { Header = "Пол", HorizontalAlignment = HorizontalAlignment.Stretch };
            genCombo.Items.Add(new ComboBoxItem { Content = "1 — Муж.", Tag = "1" });
            genCombo.Items.Add(new ComboBoxItem { Content = "2 — Жен.", Tag = "2" });
            genCombo.SelectedItem = genCombo.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(c => c.Tag?.ToString() == emp.Gender);
            var docBox = new TextBox { Header = "Серия и номер паспорта", Text = emp.DocSeries };
            AddToGrid3(detGrid, bdBox, genCombo, docBox);
            root.Children.Add(detGrid);

            var dialog = new ContentDialog
            {
                Title = $"Данные: {emp.FullName}",
                Content = root,
                PrimaryButtonText = "Сохранить",
                CloseButtonText   = "Отмена",
                XamlRoot = this.XamlRoot,
                DefaultButton = ContentDialogButton.Primary,
                RequestedTheme = ThemeService.IsLightTheme
                    ? ElementTheme.Light : ElementTheme.Default,
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                emp.PayNR1    = P(nr1.Text); emp.PayNR2 = P(nr2.Text); emp.PayNR3 = P(nr3.Text);
                emp.ExemptNR1 = P(ex1.Text); emp.ExemptNR2 = P(ex2.Text); emp.ExemptNR3 = P(ex3.Text);
                emp.PayPV1    = P(pv1.Text); emp.PayPV2 = P(pv2.Text); emp.PayPV3 = P(pv3.Text);
                emp.BirthDate = bdBox.Text.Trim();
                emp.Gender    = (genCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                emp.DocSeries = docBox.Text.Trim();
            }
        }

        private Grid MakeGrid3()
        {
            var g = new Grid { ColumnSpacing = 10 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            return g;
        }

        private void AddToGrid3(Grid g, FrameworkElement c0, FrameworkElement c1, FrameworkElement c2)
        {
            Grid.SetColumn(c0, 0); Grid.SetColumn(c1, 1); Grid.SetColumn(c2, 2);
            g.Children.Add(c0); g.Children.Add(c1); g.Children.Add(c2);
        }

        private decimal P(string s)
        {
            decimal.TryParse(s,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v);
            return v;
        }

        // ═════════════════════════════════════════════════════════════════════
        // ГЕНЕРАЦИЯ XML
        // ═════════════════════════════════════════════════════════════════════

        private RsvSettings CollectSettings()
        {
            int year = RsvYearCombo.SelectedItem is ComboBoxItem yi && yi.Tag is int y
                ? y : DateTime.Now.Year;
            int qtr  = RsvQuarterCombo.SelectedIndex + 1;
            int.TryParse(RsvHeadcountBox.Text, out int hc);
            if (hc <= 0) hc = _employees.Count;

            decimal.TryParse(RsvItIncomeTotalBox?.Text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal itTotal);
            decimal.TryParse(RsvItIncomeItBox?.Text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal itIt);
            decimal.TryParse(RsvAgrIncomeBox?.Text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal agrInc);

            bool hasPV  = _employees.Any(x => x.HasPvPayments);
            string nrCode = GetNrTariffCode();

            return new RsvSettings
            {
                OrgINN = RsvInnBox.Text.Trim(),
                OrgKPP = RsvKppBox.Text.Trim(),
                OrgName = RsvNameBox.Text.Trim(),
                Phone   = RsvPhoneBox.Text.Trim(),
                AvgHeadcount = hc,
                DirectorSurname    = RsvDirSurnameBox.Text.Trim(),
                DirectorName       = RsvDirNameBox.Text.Trim(),
                DirectorPatronymic = RsvDirPatronymicBox.Text.Trim(),
                Year = year, Quarter = qtr,
                KodNO = RsvKodNoBox.Text.Trim(),
                NrTariffCode = nrCode,
                NrRate       = GetNrRate(),
                NrCatCode    = GetNrCatCode(),
                PvTariffCode = "32",
                PvRate       = 0.15m,
                AutoSplitPv  = true,
                MROTPerMonth = GetMrot(),
                OKTMO = RsvOktmoBox.Text.Trim(),
                KBK   = RsvKbkBox.Text.Trim(),
                HasItBenefit     = RsvItBenefitCheckBox.IsChecked == true,
                ItBenefitRegDate = RsvItRegDateBox?.Text.Trim() ?? "",
                ItBenefitRegNum  = RsvItRegNumBox?.Text.Trim() ?? "",
                ItIncomePer      = itTotal,
                ItItIncomePer    = itIt,
                HasAgrBenefit    = hasPV,
                AgrIncomePer     = agrInc,
                AgrIncomeBasePer = agrInc,
            };
        }

        private void RsvGenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_employees.Count == 0)
            { RsvGenerateStatusText.Text = "Нет сотрудников."; return; }
            if (string.IsNullOrEmpty(RsvInnBox.Text.Trim()))
            { RsvGenerateStatusText.Text = "Заполните ИНН организации."; return; }

            var settings = CollectSettings();
            try
            {
                string path = RsvGeneratorService.GenerateRsv(_employees, settings, _outputFolder);
                // Авто-сохраняем сессию после генерации
                _currentSessionId = RsvSessionService.Save(
                    _employees, settings, _currentSessionId);
                RefreshSessionsList();
                RsvGenerateStatusText.Text = $"✓ РСВ XML сформирован: {path}";
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                RsvGenerateStatusText.Text = $"Ошибка: {ex.Message}";
            }
        }

        private void RsvOpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(_outputFolder);
            Process.Start(new ProcessStartInfo
                { FileName = _outputFolder, UseShellExecute = true });
        }
    }
}
