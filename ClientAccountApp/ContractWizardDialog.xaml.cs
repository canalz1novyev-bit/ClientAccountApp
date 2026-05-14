using Microsoft.EntityFrameworkCore;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClientAccountApp
{
    public sealed partial class ContractWizardDialog : ContentDialog
    {
        // ─────────────────────────────────────────────────────────────────────
        // Результат работы мастера
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Заполняется при успешном завершении мастера.</summary>
        public ClientContract? ResultContract { get; private set; }
        public ContractParty? ResultParty1 { get; private set; }
        public ContractParty? ResultParty2 { get; private set; }
        public bool WizardCompleted { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        // Внутреннее состояние
        // ─────────────────────────────────────────────────────────────────────

        private int _currentStep = 1;

        /// <summary>Если задан — предвыбирает клиента на шаге 3 (передаётся из строки списка).</summary>
        public int PreSelectClientId { get; set; } = 0;
        private string _selectedContractType = "services";

        private OrganizationProfile? _orgProfile;
        private ContractParty? _party1;
        private ContractParty? _party2;

        private List<ClientInfo> _allClients = new();
        private ClientInfo? _selectedClient;
        private BankAccount? _selectedBankAccount;

        // Карточки типов для управления выделением
        private readonly Dictionary<string, Border> _typeCards = new();

        // ─────────────────────────────────────────────────────────────────────
        // Конструктор
        // ─────────────────────────────────────────────────────────────────────

        public ContractWizardDialog()
        {
            this.InitializeComponent();
            LoadInitialData();
            BuildTypeCardDictionary();
            UpdateStepIndicator();
        }

        private void LoadInitialData()
        {
            try
            {
                _orgProfile = ActiveOrganizationService.GetRequired();
                FillOrgProfileDisplay();
            }
            catch
            {
                // Профиль не настроен — позволим ввести вручную
            }

            using var db = new AppDbContext();
            _allClients = db.Clients
                .AsNoTracking()
                .Where(c => c.Status != "Архив")
                .OrderBy(c => c.Name)
                .ToList();

            P2ClientComboBox.ItemsSource = _allClients;
            P2ClientComboBox.DisplayMemberPath = "Name";

            // Предвыбираем клиента если передан clientId (из строки списка)
            if (PreSelectClientId > 0)
            {
                var preSelected = _allClients.FirstOrDefault(c => c.Id == PreSelectClientId);
                if (preSelected != null)
                    P2ClientComboBox.SelectedItem = preSelected;
            }
        }

        private void BuildTypeCardDictionary()
        {
            _typeCards["services"] = TypeCard_services;
            _typeCards["supply"] = TypeCard_supply;
            _typeCards["lease"] = TypeCard_lease;
            _typeCards["work"] = TypeCard_work;
            _typeCards["agency"] = TypeCard_agency;
            _typeCards["nda"] = TypeCard_nda;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ШАГ 1: Выбор типа договора
        // ─────────────────────────────────────────────────────────────────────

        private void TypeCard_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Border card || card.Tag is not string key) return;

            _selectedContractType = key;
            HighlightSelectedTypeCard(key);
            CheckTemplateAvailability(key);
        }

        private void HighlightSelectedTypeCard(string selectedKey)
        {
            var appRes = Application.Current.Resources;
            var accentBrush  = (SolidColorBrush)appRes["NiatecAccentBrush"];
            var borderBrush  = (SolidColorBrush)appRes["NiatecBorderBrush"];

            foreach (var (key, card) in _typeCards)
            {
                bool isSelected = key == selectedKey;
                card.BorderBrush     = isSelected ? accentBrush : borderBrush;
                card.BorderThickness = new Thickness(isSelected ? 2 : 1);
            }
        }

        private void CheckTemplateAvailability(string key)
        {
            try
            {
                ContractTypeDefinitions.ResolveTemplatePath(key);
                TemplateMissingBanner.Visibility = Visibility.Collapsed;
            }
            catch (FileNotFoundException ex)
            {
                TemplateMissingText.Text = $"⚠ Специализированный шаблон не найден — будет использован базовый.\n{ex.Message}";
                TemplateMissingBanner.Visibility = Visibility.Visible;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ШАГ 2: Сторона 1
        // ─────────────────────────────────────────────────────────────────────

        private void FillOrgProfileDisplay()
        {
            if (_orgProfile == null) return;

            P1OrgNameBlock.Text = _orgProfile.Name;

            string innKpp = _orgProfile.Inn;
            if (!string.IsNullOrWhiteSpace(_orgProfile.Kpp))
                innKpp += " / " + _orgProfile.Kpp;
            P1OrgInnBlock.Text = innKpp;

            P1OrgSignerBlock.Text = string.IsNullOrWhiteSpace(_orgProfile.DirectorName)
                ? "— не указан —"
                : $"{_orgProfile.DirectorPosition}, {_orgProfile.DirectorName}";

            P1OrgBankBlock.Text = string.IsNullOrWhiteSpace(_orgProfile.BankName)
                ? "— не указан —"
                : _orgProfile.BankName;

            P1OrgAccountBlock.Text = string.IsNullOrWhiteSpace(_orgProfile.SettlementAccount)
                ? "—"
                : _orgProfile.SettlementAccount;
        }

        private void P1Source_Changed(object sender, RoutedEventArgs e)
        {
            // Guard: событие может сработать во время InitializeComponent до создания элементов
            if (P1OrgPanel == null || P1ManualPanel == null) return;

            bool fromOrg = P1FromOrgRadio.IsChecked == true;
            P1OrgPanel.Visibility = fromOrg ? Visibility.Visible : Visibility.Collapsed;
            P1ManualPanel.Visibility = fromOrg ? Visibility.Collapsed : Visibility.Visible;
        }

        private ContractParty BuildParty1()
        {
            if (P1FromOrgRadio.IsChecked == true && _orgProfile != null)
                return ContractParty.FromOrganizationProfile(_orgProfile);

            return new ContractParty
            {
                SourceType = ContractPartySourceType.Manual,
                Name = P1NameBox.Text.Trim(),
                ShortName = P1NameBox.Text.Trim(),
                Inn = P1InnBox.Text.Trim(),
                Kpp = P1KppBox.Text.Trim(),
                Ogrn = P1OgrnBox.Text.Trim(),
                Address = P1AddressBox.Text.Trim(),
                SignerFullName = P1SignerBox.Text.Trim(),
                SignerPosition = string.IsNullOrWhiteSpace(P1PositionBox.Text) ? "Директор" : P1PositionBox.Text.Trim(),
                SignerBasis = string.IsNullOrWhiteSpace(P1BasisBox.Text) ? "Устава" : P1BasisBox.Text.Trim(),
                BankName = P1BankNameBox.Text.Trim(),
                BankBic = P1BicBox.Text.Trim(),
                SettlementAccount = P1AccountBox.Text.Trim(),
                CorrespondentAccount = P1CorrAccountBox.Text.Trim()
            };
        }

        private string? ValidateParty1()
        {
            var p = BuildParty1();
            if (string.IsNullOrWhiteSpace(p.Name)) return "Укажите наименование стороны 1.";
            if (string.IsNullOrWhiteSpace(p.Inn)) return "Укажите ИНН стороны 1.";
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ШАГ 3: Сторона 2
        // ─────────────────────────────────────────────────────────────────────

        private void P2Source_Changed(object sender, RoutedEventArgs e)
        {
            // Guard: событие может сработать во время InitializeComponent до создания элементов
            if (P2DbPanel == null || P2ManualPanel == null) return;

            bool fromDb = P2FromDbRadio.IsChecked == true;
            P2DbPanel.Visibility = fromDb ? Visibility.Visible : Visibility.Collapsed;
            P2ManualPanel.Visibility = fromDb ? Visibility.Collapsed : Visibility.Visible;
        }

        private void P2ClientComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedClient = P2ClientComboBox.SelectedItem as ClientInfo;
            _selectedBankAccount = null;

            if (_selectedClient == null)
            {
                P2ClientDetailsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            // Заполняем реквизиты
            P2ClientNameBlock.Text = _selectedClient.Name;

            string innKpp = _selectedClient.Inn;
            if (!string.IsNullOrWhiteSpace(GetKpp(_selectedClient)))
                innKpp += " / " + GetKpp(_selectedClient);
            P2ClientInnBlock.Text = innKpp;

            P2ClientSignerBlock.Text = string.IsNullOrWhiteSpace(_selectedClient.DirectorFullName)
                ? "—"
                : _selectedClient.DirectorFullName;

            P2ClientAddressBlock.Text = string.IsNullOrWhiteSpace(_selectedClient.Address)
                ? "—"
                : _selectedClient.Address;

            // Загружаем банковские счета
            using var db = new AppDbContext();
            var banks = db.BankAccounts.AsNoTracking()
                .Where(b => b.ClientInfoId == _selectedClient.Id)
                .OrderBy(b => b.BankName)
                .ToList();

            P2BankAccountComboBox.ItemsSource = banks;
            P2BankAccountComboBox.DisplayMemberPath = "DisplayText";
            P2BankAccountComboBox.PlaceholderText = banks.Count == 0
                ? "Нет банковских счетов"
                : "Выберите счёт для реквизитов";

            if (banks.Count > 0)
                P2BankAccountComboBox.SelectedIndex = 0;

            P2ClientDetailsPanel.Visibility = Visibility.Visible;
        }

        private void P2BankAccountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedBankAccount = P2BankAccountComboBox.SelectedItem as BankAccount;
        }

        private ContractParty BuildParty2()
        {
            if (P2FromDbRadio.IsChecked == true && _selectedClient != null)
                return ContractParty.FromClientInfo(_selectedClient, _selectedBankAccount);

            return new ContractParty
            {
                SourceType = ContractPartySourceType.Manual,
                Name = P2NameBox.Text.Trim(),
                ShortName = P2NameBox.Text.Trim(),
                Inn = P2InnBox.Text.Trim(),
                Kpp = P2KppBox.Text.Trim(),
                Ogrn = P2OgrnBox.Text.Trim(),
                Address = P2AddressBox.Text.Trim(),
                SignerFullName = P2SignerBox.Text.Trim(),
                SignerPosition = string.IsNullOrWhiteSpace(P2PositionBox.Text) ? "Директор" : P2PositionBox.Text.Trim(),
                SignerBasis = string.IsNullOrWhiteSpace(P2BasisBox.Text) ? "Устава" : P2BasisBox.Text.Trim(),
                BankName = P2BankNameBox.Text.Trim(),
                BankBic = P2BicBox.Text.Trim(),
                SettlementAccount = P2AccountBox.Text.Trim(),
                CorrespondentAccount = P2CorrAccountBox.Text.Trim()
            };
        }

        private string? ValidateParty2()
        {
            if (P2FromDbRadio.IsChecked == true && _selectedClient == null)
                return "Выберите клиента из базы или переключитесь на ввод вручную.";

            var p = BuildParty2();
            if (string.IsNullOrWhiteSpace(p.Name)) return "Укажите наименование стороны 2.";
            if (string.IsNullOrWhiteSpace(p.Inn)) return "Укажите ИНН стороны 2.";
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ШАГ 4: Условия + итог
        // ─────────────────────────────────────────────────────────────────────

        private void EnterStep4()
        {
            var typeInfo = ContractTypeDefinitions.GetByKey(_selectedContractType);

            // Ставим дефолтный предмет если пусто
            if (string.IsNullOrWhiteSpace(SubjectBox.Text))
                SubjectBox.Text = typeInfo.DefaultSubject;

            // Дефолтные даты
            if (ValidFromPicker.Date == null)
                ValidFromPicker.Date = DateTimeOffset.Now;
            if (ValidToPicker.Date == null)
                ValidToPicker.Date = new DateTimeOffset(DateTime.Today.Year, 12, 31, 0, 0, 0, TimeSpan.Zero);

            // Меняем кнопку на "Создать договор"
            NextButton.Content = "Создать договор ✓";

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            var p1 = _party1 ?? BuildParty1();
            var p2 = _party2 ?? BuildParty2();
            var typeInfo = ContractTypeDefinitions.GetByKey(_selectedContractType);

            string p1Name = string.IsNullOrWhiteSpace(p1.Name) ? "—" : p1.Name;
            string p2Name = string.IsNullOrWhiteSpace(p2.Name) ? "—" : p2.Name;

            decimal amount = decimal.TryParse(AmountBox.Text.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var a) ? a : 0;

            string vatMode = (VatModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Без НДС";

            SummaryBlock.Text = $"Тип: {typeInfo.DisplayName}\n" +
                                $"{typeInfo.Party1Role}: {p1Name}\n" +
                                $"{typeInfo.Party2Role}: {p2Name}\n" +
                                $"Сумма: {amount:N2} руб. ({vatMode})";
        }

        private string? ValidateStep4()
        {
            if (string.IsNullOrWhiteSpace(SubjectBox.Text))
                return "Укажите предмет договора.";
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // НАВИГАЦИЯ
        // ─────────────────────────────────────────────────────────────────────

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < 4)
            {
                string? error = ValidateCurrentStep();
                if (error != null)
                {
                    ShowInlineError(error);
                    return;
                }

                // Сохраняем данные шага перед переходом
                SaveCurrentStepData();

                _currentStep++;
                ShowStep(_currentStep);
                UpdateStepIndicator();
            }
            else
            {
                // Финальный шаг — создаём договор
                string? error = ValidateStep4();
                if (error != null)
                {
                    Step4StatusBlock.Text = error;
                    Step4StatusBlock.Visibility = Visibility.Visible;
                    return;
                }

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
            if (_currentStep == 3) NextButton.Content = "Далее →";
            ShowStep(_currentStep);
            UpdateStepIndicator();
        }

        private string? ValidateCurrentStep()
        {
            return _currentStep switch
            {
                1 => null, // тип выбран по умолчанию
                2 => ValidateParty1(),
                3 => ValidateParty2(),
                4 => ValidateStep4(),
                _ => null
            };
        }

        private void SaveCurrentStepData()
        {
            if (_currentStep == 2) _party1 = BuildParty1();
            if (_currentStep == 3) _party2 = BuildParty2();
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
            var typeInfo = ContractTypeDefinitions.GetByKey(_selectedContractType);
            Step2TitleBlock.Text = $"Сторона 1 — {typeInfo.Party1Role}";

            // Если профиль не настроен — сразу переводим на ручной ввод
            if (_orgProfile == null)
            {
                P1ManualRadio.IsChecked = true;
                P1OrgPanel.Visibility = Visibility.Collapsed;
                P1ManualPanel.Visibility = Visibility.Visible;
            }
        }

        private void UpdateStep3Roles()
        {
            var typeInfo = ContractTypeDefinitions.GetByKey(_selectedContractType);
            Step3TitleBlock.Text = $"Сторона 2 — {typeInfo.Party2Role}";
        }

        private void UpdateStepIndicator()
        {
            StepCounterBlock.Text = $"Шаг {_currentStep} из 4";

            var appRes             = Application.Current.Resources;
            var activeBrush        = (SolidColorBrush)appRes["NiatecAccentBrush"];
            var inactiveBrush      = (SolidColorBrush)appRes["NiatecBorderBrush"];
            var activeLabelBrush   = activeBrush;
            var inactiveLabelBrush = (SolidColorBrush)appRes["NiatecTextMutedBrush"];
            var semiBold = Microsoft.UI.Text.FontWeights.SemiBold;
            var normal   = Microsoft.UI.Text.FontWeights.Normal;

            void SetDot(Ellipse dot, TextBlock label, int stepNum)
            {
                bool active = _currentStep >= stepNum;
                dot.Fill         = active ? activeBrush      : inactiveBrush;
                label.Foreground = active ? activeLabelBrush : inactiveLabelBrush;
                label.FontWeight = active ? semiBold         : normal;
            }

            SetDot(StepDot1, StepLabel1, 1);
            SetDot(StepDot2, StepLabel2, 2);
            SetDot(StepDot3, StepLabel3, 3);
            SetDot(StepDot4, StepLabel4, 4);
        }

        private void ShowInlineError(string message)
        {
            if (_currentStep == 4)
            {
                Step4StatusBlock.Text = message;
                Step4StatusBlock.Visibility = Visibility.Visible;
            }
            // На шагах 1-3 можно добавить аналогичный блок при необходимости
        }

        // ─────────────────────────────────────────────────────────────────────
        // ФИНАЛЬНАЯ СБОРКА РЕЗУЛЬТАТА
        // ─────────────────────────────────────────────────────────────────────

        private void BuildResult()
        {
            ResultParty1 = _party1 ?? BuildParty1();
            ResultParty2 = _party2 ?? BuildParty2();

            decimal amount = decimal.TryParse(
                AmountBox.Text.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var a) ? a : 0;

            string vatMode = (VatModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Без НДС";

            ResultContract = new ClientContract
            {
                ContractType = _selectedContractType,
                Subject = SubjectBox.Text.Trim(),
                Amount = amount,
                VatMode = vatMode,
                City = CityBox.Text.Trim(),
                ContractNumber = ContractNumberBox.Text.Trim(),
                ValidFrom = ValidFromPicker.Date?.DateTime,
                ValidTo = ValidToPicker.Date?.DateTime,
                Party1Json = ResultParty1.ToJson(),
                Party2Json = ResultParty2.ToJson()
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // ВСПОМОГАТЕЛЬНЫЕ
        // ─────────────────────────────────────────────────────────────────────

        private static string GetKpp(ClientInfo client)
        {
            var prop = client.GetType().GetProperty("Kpp") ?? client.GetType().GetProperty("KPP");
            return prop?.GetValue(client)?.ToString() ?? "";
        }
    }
}
