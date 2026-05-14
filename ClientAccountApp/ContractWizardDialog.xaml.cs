using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public sealed partial class ContractWizardDialog : ContentDialog
    {
        // ─── Результат ─────────────────────────────────────────────────────
        public ClientContract? ResultContract { get; private set; }
        public ContractParty? ResultParty1 { get; private set; }
        public ContractParty? ResultParty2 { get; private set; }
        public bool WizardCompleted { get; private set; }

        // ─── Внутреннее состояние ──────────────────────────────────────────
        private int _currentStep = 1;
        private string _selectedContractType = "services";

        /// <summary>Если задан — предвыбирает клиента на шаге 3.</summary>
        public int PreSelectClientId { get; set; } = 0;

        private OrganizationProfile? _orgProfile;
        private List<ClientInfo> _allClients = new();

        // Стороны из базы
        private ClientInfo? _p1SelectedClient;
        private BankAccount? _p1SelectedBank;
        private ClientInfo? _p2SelectedClient;
        private BankAccount? _p2SelectedBank;

        private readonly Dictionary<string, Border> _typeCards = new();

        // ─── Конструктор ───────────────────────────────────────────────────
        public ContractWizardDialog()
        {
            this.InitializeComponent();
            LoadInitialData();
            BuildTypeCardDictionary();
            UpdateStepIndicator();
        }

        private void LoadInitialData()
        {
            try { _orgProfile = ActiveOrganizationService.GetRequired(); FillOrgProfileDisplay(); }
            catch { /* профиль не настроен — покажем ручной ввод */ }

            using var db = new AppDbContext();
            _allClients = db.Clients.AsNoTracking()
                .Where(c => c.Status != "Архив")
                .OrderBy(c => c.Name)
                .ToList();

            // Настраиваем AutoSuggestBox
            P1SearchBox.ItemsSource = _allClients;
            P2SearchBox.ItemsSource = _allClients;

            // Предвыбор клиента на шаге 3
            if (PreSelectClientId > 0)
            {
                var pre = _allClients.FirstOrDefault(c => c.Id == PreSelectClientId);
                if (pre != null) ApplyP2Client(pre);
            }
        }

        private void BuildTypeCardDictionary()
        {
            _typeCards["services"] = TypeCard_services;
            _typeCards["supply"]   = TypeCard_supply;
            _typeCards["lease"]    = TypeCard_lease;
            _typeCards["work"]     = TypeCard_work;
            _typeCards["agency"]   = TypeCard_agency;
            _typeCards["nda"]      = TypeCard_nda;
        }

        // ═══════════════════════════════════════════════════════════════════
        // ШАГ 1: Тип договора
        // ═══════════════════════════════════════════════════════════════════

        private void TypeCard_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Border card || card.Tag is not string key) return;
            _selectedContractType = key;
            HighlightSelectedTypeCard(key);
        }

        private void HighlightSelectedTypeCard(string selectedKey)
        {
            var appRes     = Application.Current.Resources;
            var accentBrush = (Microsoft.UI.Xaml.Media.SolidColorBrush)appRes["NiatecAccentBrush"];
            var borderBrush = (Microsoft.UI.Xaml.Media.SolidColorBrush)appRes["NiatecBorderBrush"];

            foreach (var (key, card) in _typeCards)
            {
                bool sel = key == selectedKey;
                card.BorderBrush     = sel ? accentBrush : borderBrush;
                card.BorderThickness = new Thickness(sel ? 2 : 1);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // ШАГ 2: Сторона 1
        // ═══════════════════════════════════════════════════════════════════

        private void FillOrgProfileDisplay()
        {
            if (_orgProfile == null) return;
            P1OrgNameBlock.Text = _orgProfile.Name;
            string innKpp = _orgProfile.Inn +
                (string.IsNullOrWhiteSpace(_orgProfile.Kpp) ? "" : " / " + _orgProfile.Kpp);
            P1OrgInnBlock.Text = innKpp;
            P1OrgSignerBlock.Text = string.IsNullOrWhiteSpace(_orgProfile.DirectorName)
                ? "— не указан —"
                : $"{_orgProfile.DirectorPosition}, {_orgProfile.DirectorName}";
            P1OrgBankBlock.Text = string.IsNullOrWhiteSpace(_orgProfile.BankName)
                ? "— не указан —" : _orgProfile.BankName;
            P1OrgAccountBlock.Text = string.IsNullOrWhiteSpace(_orgProfile.SettlementAccount)
                ? "—" : _orgProfile.SettlementAccount;
        }

        private void P1Source_Changed(object sender, RoutedEventArgs e)
        {
            if (P1OrgPanel == null || P1DbPanel == null || P1ManualPanel == null) return;
            bool fromOrg    = P1FromOrgRadio.IsChecked == true;
            bool fromDb     = P1FromDbRadio.IsChecked == true;
            bool fromManual = P1ManualRadio.IsChecked == true;

            P1OrgPanel.Visibility    = fromOrg    ? Visibility.Visible : Visibility.Collapsed;
            P1DbPanel.Visibility     = fromDb     ? Visibility.Visible : Visibility.Collapsed;
            P1ManualPanel.Visibility = fromManual ? Visibility.Visible : Visibility.Collapsed;
        }

        // AutoSuggestBox — Сторона 1 из базы
        private void P1SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var q = sender.Text.Trim().ToLowerInvariant();
            sender.ItemsSource = string.IsNullOrWhiteSpace(q)
                ? _allClients
                : _allClients.Where(c =>
                    c.Name.ToLowerInvariant().Contains(q) ||
                    c.Inn.Contains(q)).ToList();
        }

        private void P1SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is not ClientInfo client) return;
            sender.Text = client.Name;
            ApplyP1Client(client);
        }

        private void ApplyP1Client(ClientInfo client)
        {
            _p1SelectedClient = client;
            P1DbNameBlock.Text = client.Name;
            P1DbInnBlock.Text  = client.Inn;
            P1DbSignerBlock.Text = string.IsNullOrWhiteSpace(client.DirectorFullName)
                ? "—" : client.DirectorFullName;

            using var db = new AppDbContext();
            var banks = db.BankAccounts.AsNoTracking()
                .Where(b => b.ClientInfoId == client.Id).OrderBy(b => b.BankName).ToList();

            P1BankAccountComboBox.ItemsSource = banks;
            P1BankAccountComboBox.DisplayMemberPath = "DisplayText";

            if (banks.Count > 0)
            {
                P1BankAccountComboBox.Visibility = Visibility.Visible;
                P1BankAccountComboBox.SelectedIndex = 0;
                P1NoBankPanel.Visibility = Visibility.Collapsed;
                P1AddBankForm.Visibility = Visibility.Collapsed;
            }
            else
            {
                P1BankAccountComboBox.Visibility = Visibility.Collapsed;
                P1NoBankPanel.Visibility = Visibility.Visible;
                P1AddBankForm.Visibility = Visibility.Collapsed;
            }

            P1DbDetailsPanel.Visibility = Visibility.Visible;
        }

        private void P1BankAccountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => _p1SelectedBank = P1BankAccountComboBox.SelectedItem as BankAccount;

        // ── Добавление счёта к стороне 1 ──────────────────────────────────
        private void P1AddBankButton_Click(object sender, RoutedEventArgs e)
        {
            P1AddBankForm.Visibility    = Visibility.Visible;
            P1NoBankPanel.Visibility    = Visibility.Collapsed;
        }

        private void P1CancelAddBankButton_Click(object sender, RoutedEventArgs e)
        {
            P1AddBankForm.Visibility = Visibility.Collapsed;
            P1NoBankPanel.Visibility = Visibility.Visible;
            P1NewBicBox.Text = P1NewAccountBox.Text = P1NewBankNameBox.Text = P1NewCorrAccountBox.Text = "";
            P1NewBankStatusBlock.Text = "";
        }

        private async void P1NewBicLookupButton_Click(object sender, RoutedEventArgs e) { } // не используется

        private void P1SaveNewBankButton_Click(object sender, RoutedEventArgs e)
        {
            if (_p1SelectedClient == null) return;
            if (string.IsNullOrWhiteSpace(P1NewAccountBox.Text))
            {
                P1NewBankStatusBlock.Text = "Укажите расчётный счёт.";
                P1NewBankStatusBlock.Foreground = (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["NiatecDangerBrush"];
                return;
            }
            try
            {
                using var db = new AppDbContext();
                var bank = new BankAccount
                {
                    ClientInfoId         = _p1SelectedClient.Id,
                    BIC                  = P1NewBicBox.Text.Trim(),
                    AccountNumber        = P1NewAccountBox.Text.Trim(),
                    BankName             = P1NewBankNameBox.Text.Trim(),
                    CorrespondentAccount = P1NewCorrAccountBox.Text.Trim()
                };
                db.BankAccounts.Add(bank);
                db.SaveChanges();

                var banks = db.BankAccounts.AsNoTracking()
                    .Where(b => b.ClientInfoId == _p1SelectedClient.Id).OrderBy(b => b.BankName).ToList();
                P1BankAccountComboBox.ItemsSource = banks;
                P1BankAccountComboBox.DisplayMemberPath = "DisplayText";
                P1BankAccountComboBox.SelectedIndex = banks.Count - 1;
                _p1SelectedBank = banks.LastOrDefault();

                P1BankAccountComboBox.Visibility = Visibility.Visible;
                P1AddBankForm.Visibility = Visibility.Collapsed;
                P1NoBankPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                P1NewBankStatusBlock.Text = $"Ошибка сохранения: {ex.Message}";
                P1NewBankStatusBlock.Foreground = (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["NiatecDangerBrush"];
            }
        }

        // Ручной ввод — ИНН автозаполнение
        private void P1InnBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool canLookup = P1InnBox.Text.Trim().Length >= 10;
            P1FillByInnButton.IsEnabled = canLookup;
        }

        private async void P1FillByInnButton_Click(object sender, RoutedEventArgs e)
        {
            string inn = P1InnBox.Text.Trim();
            P1FillByInnButton.IsEnabled = false;
            P1FillByInnButton.Content = "Поиск...";
            P1InnStatusBlock.Text = "";
            try
            {
                var result = await InnLookupService.FindByInnAsync(inn, "ООО");
                P1NameBox.Text    = result.ClientName;
                P1OgrnBox.Text    = result.Ogrn;
                P1AddressBox.Text = result.LegalAddress;
                P1SignerBox.Text  = result.DirectorName;
                P1InnStatusBlock.Text = $"✓ Данные по ИНН {inn} заполнены.";
                P1InnStatusBlock.Foreground = (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["NiatecSuccessBrush"];
            }
            catch (Exception ex)
            {
                P1InnStatusBlock.Text = $"Ошибка: {ex.Message}";
                P1InnStatusBlock.Foreground = (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["NiatecDangerBrush"];
            }
            finally
            {
                P1FillByInnButton.Content   = "Заполнить по ИНН";
                P1FillByInnButton.IsEnabled = true;
            }
        }

        // Ручной ввод — БИК автозаполнение (P1 manual)
        private async void P1BicBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string bic = new string(P1BicBox.Text.Where(char.IsDigit).ToArray());
            if (bic.Length == 9)
                await LookupBicAsync(P1BicBox, P1BankNameBox, P1CorrAccountBox, P1InnStatusBlock);
        }

        // Форма нового счёта стороны 1
        private async void P1NewBicBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string bic = new string(P1NewBicBox.Text.Where(char.IsDigit).ToArray());
            if (bic.Length == 9)
                await LookupBicAsync(P1NewBicBox, P1NewBankNameBox, P1NewCorrAccountBox, P1NewBankStatusBlock);
        }

        // ═══════════════════════════════════════════════════════════════════
        // ШАГ 3: Сторона 2
        // ═══════════════════════════════════════════════════════════════════

        private void P2Source_Changed(object sender, RoutedEventArgs e)
        {
            if (P2DbPanel == null || P2ManualPanel == null) return;
            bool fromDb = P2FromDbRadio.IsChecked == true;
            P2DbPanel.Visibility     = fromDb  ? Visibility.Visible : Visibility.Collapsed;
            P2ManualPanel.Visibility = fromDb  ? Visibility.Collapsed : Visibility.Visible;
        }

        private void P2SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var q = sender.Text.Trim().ToLowerInvariant();
            sender.ItemsSource = string.IsNullOrWhiteSpace(q)
                ? _allClients
                : _allClients.Where(c =>
                    c.Name.ToLowerInvariant().Contains(q) ||
                    c.Inn.Contains(q)).ToList();
        }

        private void P2SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is not ClientInfo client) return;
            sender.Text = client.Name;
            ApplyP2Client(client);
        }

        private void ApplyP2Client(ClientInfo client)
        {
            _p2SelectedClient = client;
            P2ClientNameBlock.Text    = client.Name;
            P2ClientInnBlock.Text     = client.Inn;
            P2ClientSignerBlock.Text  = string.IsNullOrWhiteSpace(client.DirectorFullName)
                ? "—" : client.DirectorFullName;
            P2ClientAddressBlock.Text = string.IsNullOrWhiteSpace(client.Address)
                ? "—" : client.Address;

            using var db = new AppDbContext();
            var banks = db.BankAccounts.AsNoTracking()
                .Where(b => b.ClientInfoId == client.Id).OrderBy(b => b.BankName).ToList();

            P2BankAccountComboBox.ItemsSource = banks;
            P2BankAccountComboBox.DisplayMemberPath = "DisplayText";

            if (banks.Count > 0)
            {
                P2BankAccountComboBox.Visibility = Visibility.Visible;
                P2BankAccountComboBox.SelectedIndex = 0;
                P2NoBankPanel.Visibility = Visibility.Collapsed;
                P2AddBankForm.Visibility = Visibility.Collapsed;
            }
            else
            {
                P2BankAccountComboBox.Visibility = Visibility.Collapsed;
                P2NoBankPanel.Visibility = Visibility.Visible;
                P2AddBankForm.Visibility = Visibility.Collapsed;
            }

            P2ClientDetailsPanel.Visibility = Visibility.Visible;

            if (P2SearchBox != null && string.IsNullOrWhiteSpace(P2SearchBox.Text))
                P2SearchBox.Text = client.Name;
        }

        private void P2BankAccountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => _p2SelectedBank = P2BankAccountComboBox.SelectedItem as BankAccount;

        // ── Добавление счёта к стороне 2 ──────────────────────────────────
        private void P2AddBankButton_Click(object sender, RoutedEventArgs e)
        {
            P2AddBankForm.Visibility = Visibility.Visible;
            P2NoBankPanel.Visibility = Visibility.Collapsed;
        }

        private void P2CancelAddBankButton_Click(object sender, RoutedEventArgs e)
        {
            P2AddBankForm.Visibility = Visibility.Collapsed;
            P2NoBankPanel.Visibility = Visibility.Visible;
            P2NewBicBox.Text = P2NewAccountBox.Text = P2NewBankNameBox.Text = P2NewCorrAccountBox.Text = "";
            P2NewBankStatusBlock.Text = "";
        }

        private async void P2NewBicLookupButton_Click(object sender, RoutedEventArgs e) { } // не используется

        private void P2SaveNewBankButton_Click(object sender, RoutedEventArgs e)
        {
            if (_p2SelectedClient == null) return;
            if (string.IsNullOrWhiteSpace(P2NewAccountBox.Text))
            {
                P2NewBankStatusBlock.Text = "Укажите расчётный счёт.";
                P2NewBankStatusBlock.Foreground = (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["NiatecDangerBrush"];
                return;
            }
            try
            {
                using var db = new AppDbContext();
                var bank = new BankAccount
                {
                    ClientInfoId         = _p2SelectedClient.Id,
                    BIC                  = P2NewBicBox.Text.Trim(),
                    AccountNumber        = P2NewAccountBox.Text.Trim(),
                    BankName             = P2NewBankNameBox.Text.Trim(),
                    CorrespondentAccount = P2NewCorrAccountBox.Text.Trim()
                };
                db.BankAccounts.Add(bank);
                db.SaveChanges();

                var banks = db.BankAccounts.AsNoTracking()
                    .Where(b => b.ClientInfoId == _p2SelectedClient.Id).OrderBy(b => b.BankName).ToList();
                P2BankAccountComboBox.ItemsSource = banks;
                P2BankAccountComboBox.DisplayMemberPath = "DisplayText";
                P2BankAccountComboBox.SelectedIndex = banks.Count - 1;
                _p2SelectedBank = banks.LastOrDefault();

                P2BankAccountComboBox.Visibility = Visibility.Visible;
                P2AddBankForm.Visibility = Visibility.Collapsed;
                P2NoBankPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                P2NewBankStatusBlock.Text = $"Ошибка сохранения: {ex.Message}";
                P2NewBankStatusBlock.Foreground = (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["NiatecDangerBrush"];
            }
        }

        private void P2InnBox_TextChanged(object sender, TextChangedEventArgs e)
            => P2FillByInnButton.IsEnabled = P2InnBox.Text.Trim().Length >= 10;

        private async void P2FillByInnButton_Click(object sender, RoutedEventArgs e)
        {
            string inn = P2InnBox.Text.Trim();
            P2FillByInnButton.IsEnabled = false;
            P2FillByInnButton.Content = "Поиск...";
            P2InnStatusBlock.Text = "";
            try
            {
                var result = await InnLookupService.FindByInnAsync(inn, "ООО");
                P2NameBox.Text    = result.ClientName;
                P2OgrnBox.Text    = result.Ogrn;
                P2AddressBox.Text = result.LegalAddress;
                P2SignerBox.Text  = result.DirectorName;
                P2InnStatusBlock.Text = $"✓ Данные по ИНН {inn} заполнены.";
                P2InnStatusBlock.Foreground = (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["NiatecSuccessBrush"];
            }
            catch (Exception ex)
            {
                P2InnStatusBlock.Text = $"Ошибка: {ex.Message}";
                P2InnStatusBlock.Foreground = (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["NiatecDangerBrush"];
            }
            finally
            {
                P2FillByInnButton.Content   = "Заполнить по ИНН";
                P2FillByInnButton.IsEnabled = true;
            }
        }

        // Ручной ввод — БИК автозаполнение (P2 manual)
        private async void P2BicBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string bic = new string(P2BicBox.Text.Where(char.IsDigit).ToArray());
            if (bic.Length == 9)
                await LookupBicAsync(P2BicBox, P2BankNameBox, P2CorrAccountBox, P2InnStatusBlock);
        }

        // Форма нового счёта стороны 2
        private async void P2NewBicBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string bic = new string(P2NewBicBox.Text.Where(char.IsDigit).ToArray());
            if (bic.Length == 9)
                await LookupBicAsync(P2NewBicBox, P2NewBankNameBox, P2NewCorrAccountBox, P2NewBankStatusBlock);
        }

        // ─── Общий метод поиска банка по БИК ──────────────────────────────
        private static async Task LookupBicAsync(
            TextBox bicBox, TextBox bankNameBox, TextBox corrBox, TextBlock statusBlock)
        {
            string bic = new string(bicBox.Text.Trim().Where(char.IsDigit).ToArray());
            if (bic.Length != 9) return;
            try
            {
                var result = await BankLookupService.FindByBicAsync(bic);
                if (result == null) { statusBlock.Text = $"Банк по БИК {bic} не найден."; return; }
                bicBox.Text      = result.Bic;
                bankNameBox.Text = result.BankName;
                corrBox.Text     = result.CorrespondentAccount;
                statusBlock.Text = $"✓ Реквизиты банка заполнены по БИК {bic}.";
                statusBlock.Foreground = (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["NiatecSuccessBrush"];
            }
            catch (Exception ex)
            {
                statusBlock.Text = $"Ошибка БИК: {ex.Message}";
                statusBlock.Foreground = (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["NiatecDangerBrush"];
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // ШАГ 4: Условия
        // ═══════════════════════════════════════════════════════════════════

        private void EnterStep4()
        {
            var typeInfo = ContractTypeDefinitions.GetByKey(_selectedContractType);
            if (string.IsNullOrWhiteSpace(SubjectBox.Text))
                SubjectBox.Text = typeInfo.DefaultSubject;
            if (ValidFromPicker.Date == null)
                ValidFromPicker.Date = DateTimeOffset.Now;
            if (ValidToPicker.Date == null)
                ValidToPicker.Date = new DateTimeOffset(DateTime.Today.Year, 12, 31, 0, 0, 0, TimeSpan.Zero);

            NextButton.Content = "Создать договор ✓";
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            var p1 = BuildParty1();
            var p2 = BuildParty2();
            var ti = ContractTypeDefinitions.GetByKey(_selectedContractType);

            decimal amount = decimal.TryParse(
                AmountBox.Text.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var a) ? a : 0;
            string vat = (VatModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Без НДС";

            SummaryBlock.Text =
                $"Тип: {ti.DisplayName}\n" +
                $"{ti.Party1Role}: {(string.IsNullOrWhiteSpace(p1?.Name) ? "—" : p1!.Name)}\n" +
                $"{ti.Party2Role}: {(string.IsNullOrWhiteSpace(p2?.Name) ? "—" : p2!.Name)}\n" +
                $"Сумма: {amount:N2} руб. ({vat})";
        }

        // ═══════════════════════════════════════════════════════════════════
        // Навигация
        // ═══════════════════════════════════════════════════════════════════

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < 4)
            {
                string? error = ValidateCurrentStep();
                if (error != null) { ShowStepError(error); return; }

                _currentStep++;
                ShowStep(_currentStep);
                UpdateStepIndicator();
            }
            else
            {
                string? error = ValidateStep4();
                if (error != null) { Step4StatusBlock.Text = error; Step4StatusBlock.Visibility = Visibility.Visible; return; }
                Step4StatusBlock.Visibility = Visibility.Collapsed;
                BuildResult();
                WizardCompleted = true;
                this.Hide();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep <= 1) return;
            _currentStep--;
            if (_currentStep < 4) NextButton.Content = "Далее →";
            ShowStep(_currentStep);
            UpdateStepIndicator();
        }

        private void ShowStep(int step)
        {
            Step1Panel.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
            Step4Panel.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;

            BackButton.IsEnabled = step > 1;

            if (step == 2) UpdateStep2Roles();
            if (step == 3) UpdateStep3Roles();
            if (step == 4) EnterStep4();
        }

        private void UpdateStep2Roles()
        {
            var ti = ContractTypeDefinitions.GetByKey(_selectedContractType);
            Step2TitleBlock.Text = $"Сторона 1 — {ti.Party1Role}";
            if (_orgProfile == null && P1ManualRadio != null)
            {
                P1ManualRadio.IsChecked = true;
                P1Source_Changed(this, new RoutedEventArgs());
            }
        }

        private void UpdateStep3Roles()
        {
            var ti = ContractTypeDefinitions.GetByKey(_selectedContractType);
            Step3TitleBlock.Text = $"Сторона 2 — {ti.Party2Role}";
        }

        private void UpdateStepIndicator()
        {
            StepCounterBlock.Text = $"Шаг {_currentStep} из 4";

            var appRes    = Application.Current.Resources;
            var active    = (Microsoft.UI.Xaml.Media.SolidColorBrush)appRes["NiatecAccentBrush"];
            var inactive  = (Microsoft.UI.Xaml.Media.SolidColorBrush)appRes["NiatecBorderBrush"];
            var mutedText = (Microsoft.UI.Xaml.Media.SolidColorBrush)appRes["NiatecTextMutedBrush"];
            var bold      = Microsoft.UI.Text.FontWeights.SemiBold;
            var normal    = Microsoft.UI.Text.FontWeights.Normal;

            void Set(Ellipse dot, TextBlock lbl, int n)
            {
                bool on = _currentStep >= n;
                dot.Fill         = on ? active   : inactive;
                lbl.Foreground   = on ? active   : mutedText;
                lbl.FontWeight   = on ? bold     : normal;
            }

            Set(StepDot1, StepLabel1, 1);
            Set(StepDot2, StepLabel2, 2);
            Set(StepDot3, StepLabel3, 3);
            Set(StepDot4, StepLabel4, 4);
        }

        private void ShowStepError(string msg)
        {
            // На шаге 4 есть отдельный блок, на остальных — через InfoBar можно добавить
            if (_currentStep == 4) { Step4StatusBlock.Text = msg; Step4StatusBlock.Visibility = Visibility.Visible; }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Валидация
        // ═══════════════════════════════════════════════════════════════════

        private string? ValidateCurrentStep() => _currentStep switch
        {
            1 => null,
            2 => ValidateParty1(),
            3 => ValidateParty2(),
            4 => ValidateStep4(),
            _ => null
        };

        private string? ValidateParty1()
        {
            if (P1FromOrgRadio.IsChecked == true)
            {
                if (_orgProfile == null) return "Профиль организации не настроен. Используйте другой источник.";
                return null;
            }
            if (P1FromDbRadio.IsChecked == true)
            {
                if (_p1SelectedClient == null) return "Выберите клиента из базы или переключитесь на ручной ввод.";
                return null;
            }
            // Ручной
            if (string.IsNullOrWhiteSpace(P1NameBox.Text)) return "Укажите наименование стороны 1.";
            if (string.IsNullOrWhiteSpace(P1InnBox.Text))  return "Укажите ИНН стороны 1.";
            return null;
        }

        private string? ValidateParty2()
        {
            if (P2FromDbRadio.IsChecked == true)
            {
                if (_p2SelectedClient == null) return "Выберите клиента из базы или переключитесь на ручной ввод.";
                return null;
            }
            if (string.IsNullOrWhiteSpace(P2NameBox.Text)) return "Укажите наименование стороны 2.";
            if (string.IsNullOrWhiteSpace(P2InnBox.Text))  return "Укажите ИНН стороны 2.";
            return null;
        }

        private string? ValidateStep4()
        {
            if (string.IsNullOrWhiteSpace(SubjectBox.Text)) return "Укажите предмет договора.";
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Построение сторон и результата
        // ═══════════════════════════════════════════════════════════════════

        private ContractParty? BuildParty1()
        {
            if (P1FromOrgRadio.IsChecked == true && _orgProfile != null)
                return ContractParty.FromOrganizationProfile(_orgProfile);

            if (P1FromDbRadio.IsChecked == true && _p1SelectedClient != null)
                return ContractParty.FromClientInfo(_p1SelectedClient, _p1SelectedBank);

            return new ContractParty
            {
                SourceType       = ContractPartySourceType.Manual,
                Name             = P1NameBox.Text.Trim(),
                ShortName        = P1NameBox.Text.Trim(),
                Inn              = P1InnBox.Text.Trim(),
                Kpp              = P1KppBox.Text.Trim(),
                Ogrn             = P1OgrnBox.Text.Trim(),
                Address          = P1AddressBox.Text.Trim(),
                SignerFullName   = P1SignerBox.Text.Trim(),
                SignerPosition   = string.IsNullOrWhiteSpace(P1PositionBox.Text) ? "Директор" : P1PositionBox.Text.Trim(),
                SignerBasis      = string.IsNullOrWhiteSpace(P1BasisBox.Text)    ? "Устава"    : P1BasisBox.Text.Trim(),
                BankName         = P1BankNameBox.Text.Trim(),
                BankBic          = P1BicBox.Text.Trim(),
                SettlementAccount    = P1AccountBox.Text.Trim(),
                CorrespondentAccount = P1CorrAccountBox.Text.Trim()
            };
        }

        private ContractParty? BuildParty2()
        {
            if (P2FromDbRadio.IsChecked == true && _p2SelectedClient != null)
                return ContractParty.FromClientInfo(_p2SelectedClient, _p2SelectedBank);

            return new ContractParty
            {
                SourceType       = ContractPartySourceType.Manual,
                Name             = P2NameBox.Text.Trim(),
                ShortName        = P2NameBox.Text.Trim(),
                Inn              = P2InnBox.Text.Trim(),
                Kpp              = P2KppBox.Text.Trim(),
                Ogrn             = P2OgrnBox.Text.Trim(),
                Address          = P2AddressBox.Text.Trim(),
                SignerFullName   = P2SignerBox.Text.Trim(),
                SignerPosition   = string.IsNullOrWhiteSpace(P2PositionBox.Text) ? "Директор" : P2PositionBox.Text.Trim(),
                SignerBasis      = string.IsNullOrWhiteSpace(P2BasisBox.Text)    ? "Устава"    : P2BasisBox.Text.Trim(),
                BankName         = P2BankNameBox.Text.Trim(),
                BankBic          = P2BicBox.Text.Trim(),
                SettlementAccount    = P2AccountBox.Text.Trim(),
                CorrespondentAccount = P2CorrAccountBox.Text.Trim()
            };
        }

        private void BuildResult()
        {
            ResultParty1 = BuildParty1() ?? ContractParty.Empty();
            ResultParty2 = BuildParty2() ?? ContractParty.Empty();

            decimal amount = decimal.TryParse(
                AmountBox.Text.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var a) ? a : 0;

            ResultContract = new ClientContract
            {
                ContractType   = _selectedContractType,
                Subject        = SubjectBox.Text.Trim(),
                Amount         = amount,
                VatMode        = (VatModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Без НДС",
                City           = CityBox.Text.Trim(),
                ContractNumber = ContractNumberBox.Text.Trim(),
                ValidFrom      = ValidFromPicker.Date?.DateTime,
                ValidTo        = ValidToPicker.Date?.DateTime,
                Party1Json     = ResultParty1.ToJson(),
                Party2Json     = ResultParty2.ToJson()
            };

            // Сохраняем новых клиентов в базу если отмечено
            SaveManualPartiesIfRequested();
        }

        private void SaveManualPartiesIfRequested()
        {
            if (P1ManualRadio?.IsChecked == true && P1SaveToDbCheckBox?.IsChecked == true && ResultParty1 != null)
                SavePartyAsClient(ResultParty1);
            if (P2ManualRadio?.IsChecked == true && P2SaveToDbCheckBox?.IsChecked == true && ResultParty2 != null)
                SavePartyAsClient(ResultParty2);
        }

        private static void SavePartyAsClient(ContractParty party)
        {
            if (string.IsNullOrWhiteSpace(party.Name) || string.IsNullOrWhiteSpace(party.Inn)) return;
            try
            {
                using var db = new AppDbContext();
                bool exists = db.Clients.Any(c => c.Inn == party.Inn);
                if (exists) return;

                var client = new ClientInfo
                {
                    Name             = party.Name,
                    Inn              = party.Inn,
                    Ogrn             = party.Ogrn,
                    Address          = party.Address,
                    DirectorFullName = party.SignerFullName,
                    Status           = "Активный"
                };
                db.Clients.Add(client);
                db.SaveChanges();

                if (!string.IsNullOrWhiteSpace(party.BankBic) || !string.IsNullOrWhiteSpace(party.SettlementAccount))
                {
                    db.BankAccounts.Add(new BankAccount
                    {
                        ClientInfoId         = client.Id,
                        BankName             = party.BankName,
                        BIC                  = party.BankBic,
                        AccountNumber        = party.SettlementAccount,
                        CorrespondentAccount = party.CorrespondentAccount
                    });
                    db.SaveChanges();
                }
            }
            catch { /* не прерываем создание договора из-за ошибки сохранения клиента */ }
        }
    }
}
