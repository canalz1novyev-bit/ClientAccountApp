using Microsoft.EntityFrameworkCore;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace ClientAccountApp
{
    public sealed partial class ClientsPage : Page
    {
        public DateTimeOffset MinReminderDate { get; } = DateTimeOffset.Now;

        private readonly ObservableCollection<ClientInfo> _clients = new();
        private readonly ObservableCollection<DigitalSignature> _signatures = new();
        private readonly ObservableCollection<BankAccount> _bankAccounts = new();
        private readonly ObservableCollection<ClientNote> _notes = new();
        private readonly ObservableCollection<ClientFile> _clientFiles = new();
        private int? _pendingClientIdFromDashboard;
        private readonly MenuFlyout _clientContextFlyout = new();
        private ClientInfo? _contextClient;
        private MenuFlyoutItem? _markContractSignedMenuItem;
        private MenuFlyoutItem? _createContractMenuItem;
        private readonly MenuFlyout _clientFileContextFlyout = new();
        private ClientFile? _contextClientFile;
        private int _clientViewMode = 0;

        private bool _clientFormReady = false;
        private Border? _rightTappedClientBorder;
        private bool _workspaceStateReady = false;
        private int? _pendingSelectedClientIdFromState;
        private Border? _highlightedClientBorder;

        // ─────────────────────────────────────────────
        // БЫСТРЫЙ СЧЁТ: создание счёта из карточки клиента
        // ─────────────────────────────────────────────

        private void QuickNewInvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient)
            {
                StatusTextBlock.Text = "Сначала выберите клиента.";
                return;
            }

            try
            {
                int organizationId = ActiveOrganizationService.GetRequired().Id;

                using var db = new AppDbContext();

                string prefix = BuildQuickInvoicePrefix(db, organizationId);
                string invoiceNumber = GenerateQuickInvoiceNumber(db, prefix);

                var invoice = new Invoice
                {
                    OrganizationProfileId = organizationId,
                    ClientInfoId = selectedClient.Id,
                    InvoiceNumber = invoiceNumber,
                    InvoiceDate = DateTime.Today,
                    DueDate = DateTime.Today.AddDays(5),
                    Status = InvoiceStatusNames.Draft,
                    SourceType = InvoiceSourceTypeNames.Manual,
                    TotalWithoutVat = 0m,
                    VatAmount = 0m,
                    TotalWithVat = 0m,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.Invoices.Add(invoice);
                db.SaveChanges();

                int newInvoiceId = invoice.Id;

                StatusTextBlock.Text = $"Черновик {invoiceNumber} создан для «{selectedClient.Name}». Переход на страницу начислений...";

                BillingPage.PendingInvoiceId = newInvoiceId;
                ShellWindow.AppShell?.NavigateTo(typeof(BillingPage));
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Ошибка создания счёта: {ex.Message}";
            }
        }

        private static string BuildQuickInvoicePrefix(AppDbContext db, int organizationId)
        {
            var org = db.OrganizationProfiles
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == organizationId);

            if (org == null) return "СЧ";

            string name = System.Text.RegularExpressions.Regex.Replace(
                org.Name ?? org.ShortName ?? "",
                @"(ООО|ОАО|ИП|АО|ПАО|ЗАО|АНО|НКО)[""«»"" \.\,]",
                "", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                .Trim('"', '«', '»', ' ', '\u201C', '\u201D');

            var letters = name.Where(char.IsLetter).Take(3).ToArray();
            return letters.Length == 0 ? "СЧ" : new string(letters).ToUpperInvariant();
        }

        private static string GenerateQuickInvoiceNumber(AppDbContext db, string orgPrefix)
        {
            string prefix = $"{orgPrefix}-{DateTime.Today:yyyy}-";

            var existing = db.Invoices
                .AsNoTracking()
                .Where(x => x.InvoiceNumber.StartsWith(prefix))
                .Select(x => x.InvoiceNumber)
                .ToList();

            int next = 1;
            foreach (var num in existing)
            {
                if (num.Length > prefix.Length &&
                    int.TryParse(num.Substring(prefix.Length), out int parsed) &&
                    parsed >= next)
                    next = parsed + 1;
            }

            return $"{prefix}{next:0000}";
        }

        // ─────────────────────────────────────────────
        // ВСЁ ОСТАЛЬНОЕ БЕЗ ИЗМЕНЕНИЙ
        // ─────────────────────────────────────────────

        private async void FillByInnButton_Click(object sender, RoutedEventArgs e)
        {
            string inn = InnTextBox.Text.Trim();
            string currentType = GetCurrentFormClientType();

            if (!IsInnValidForCurrentType(inn, currentType))
            {
                StatusTextBlock.Text = IsEntrepreneurType(currentType)
                    ? "Для выбранного типа клиент ИНН должен содержать 12 цифр."
                    : "Для выбранного типа клиент ИНН должен содержать 10 цифр.";
                return;
            }

            try
            {
                FillByInnButton.IsEnabled = false;
                FillByInnButton.Content = "Поиск...";
                InnLookupResult result = await InnLookupService.FindByInnAsync(inn, currentType);
                ApplyInnLookupResultToForm(result);
                StatusTextBlock.Text = $"Данные по ИНН {inn} успешно заполнены.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Не удалось заполнить по ИНН: {ex.Message}";
            }
            finally
            {
                FillByInnButton.Content = "Заполнить по ИНН";
                ValidateClientForm();
            }
        }

        private void SaveWorkspaceState()
        {
            UiStateService.Save(state =>
            {
                state.LastPageKey = "Clients";
                state.ClientsPage.SearchText = SearchTextBox?.Text ?? "";
                state.ClientsPage.ViewMode = GetCurrentClientViewModeText();
                state.ClientsPage.SignatureFilter = GetComboBoxText(SignatureFilterComboBox, "Все клиенты");
                state.ClientsPage.StatusFilter = "Все статусы";
                state.ClientsPage.SelectedClientId = ClientsListView.SelectedItem is ClientInfo client
                    ? client.Id : null;
            });
        }

        private void RestoreWorkspaceState()
        {
            var state = UiStateService.Load().ClientsPage;
            if (SearchTextBox != null) SearchTextBox.Text = state.SearchText ?? "";
            SelectComboBoxItem(SignatureFilterComboBox, state.SignatureFilter, "Все клиенты");
            SelectComboBoxItem(StatusFilterComboBox, state.StatusFilter, "Все статусы");
            _clientViewMode = ParseClientViewMode(state.ViewMode);
            ApplyClientViewMode();
            _pendingSelectedClientIdFromState = state.SelectedClientId;
        }

        private string GetCurrentClientViewModeText() => _clientViewMode switch
        {
            1 => "Компактный",
            2 => "Табличный",
            _ => "Обычный"
        };

        private static int ParseClientViewMode(string? viewMode) => viewMode switch
        {
            "Компактный" => 1,
            "Табличный" => 2,
            _ => 0
        };

        private static string GetComboBoxText(ComboBox? comboBox, string fallback)
        {
            if (comboBox?.SelectedItem is ComboBoxItem item)
                return item.Content?.ToString() ?? fallback;
            return fallback;
        }

        private static void SelectComboBoxItem(ComboBox? comboBox, string? value, string fallback)
        {
            if (comboBox == null) return;
            string target = string.IsNullOrWhiteSpace(value) ? fallback : value;
            foreach (var rawItem in comboBox.Items)
            {
                if (rawItem is ComboBoxItem item &&
                    string.Equals(item.Content?.ToString(), target, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
            if (comboBox.Items.Count > 0) comboBox.SelectedIndex = 0;
        }

        private void ApplyInnLookupResultToForm(InnLookupResult result)
        {
            SetClientTypeComboBox(result.ClientType);
            ClientNameTextBox.Text = result.ClientName;
            DirectorTextBox.Text = result.DirectorName;
            OgrnTextBox.Text = result.Ogrn;
            AddressTextBox.Text = result.LegalAddress;
            UpdateClientFormLabels();
            ValidateClientForm();
            UpdateClientHeader(null);
        }

        private void RefreshSelectedClientHighlight()
        {
            ResetHighlightedClientCard();
            if (ClientsListView?.SelectedItem is not ClientInfo selectedClient) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                var border = FindClientCardBorder(selectedClient);
                if (border == null) return;
                _highlightedClientBorder = border;
                bool isLight = ThemeService.CurrentTheme == ThemeService.ThemeLight;
                if (isLight)
                {
                    _highlightedClientBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 249, 230));
                    _highlightedClientBorder.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 200, 146, 14));
                }
                else
                {
                    _highlightedClientBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 24, 32, 45));
                    _highlightedClientBorder.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 90, 145, 255));
                }
                _highlightedClientBorder.BorderThickness = _clientViewMode == 2 ? new Thickness(1) : new Thickness(2);
            });
        }

        private void ResetHighlightedClientCard()
        {
            if (_highlightedClientBorder == null) return;
            bool isLight = ThemeService.CurrentTheme == ThemeService.ThemeLight;
            if (isLight)
            {
                _highlightedClientBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 245, 247, 250));
                _highlightedClientBorder.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 221, 225, 234));
            }
            else
            {
                _highlightedClientBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 17, 21, 28));
                _highlightedClientBorder.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 36, 48, 66));
            }
            _highlightedClientBorder.BorderThickness = _clientViewMode == 2 ? new Thickness(0, 0, 0, 1) : new Thickness(1);
            _highlightedClientBorder = null;
        }

        private Border? FindClientCardBorder(ClientInfo client)
        {
            if (ClientsListView?.ContainerFromItem(client) is not ListViewItem container) return null;
            return FindClientCardBorder(container);
        }

        private static Border? FindClientCardBorder(DependencyObject root)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is Border border && string.Equals(border.Tag?.ToString(), "ClientCardRoot", StringComparison.Ordinal))
                    return border;
                var nested = FindClientCardBorder(child);
                if (nested != null) return nested;
            }
            return null;
        }

        private void SetClientTypeComboBox(string clientType)
        {
            foreach (var item in ClientTypeComboBox.Items)
            {
                if (item is ComboBoxItem comboBoxItem &&
                    string.Equals(comboBoxItem.Content?.ToString(), clientType, StringComparison.OrdinalIgnoreCase))
                {
                    ClientTypeComboBox.SelectedItem = comboBoxItem;
                    return;
                }
            }
        }

        public ClientsPage()
        {
            this.InitializeComponent();

            ClientsListView.ItemsSource = _clients;
            SignaturesListView.ItemsSource = _signatures;
            AccountsListView.ItemsSource = _bankAccounts;
            NotesListView.ItemsSource = _notes;
            ClientFilesListView.ItemsSource = _clientFiles;
            UpdateFileActionButtonsState();

            _clients.CollectionChanged += (_, __) => TrySelectPendingClientFromDashboard();
            Loaded += (_, __) => TrySelectPendingClientFromDashboard();

            if (ClientFileFilterComboBox != null) ClientFileFilterComboBox.SelectedIndex = 0;
            if (ClientStatusComboBox != null) ClientStatusComboBox.SelectedIndex = 0;

            RestoreWorkspaceState();
            _clientFormReady = true;
            UpdateClientFormLabels();
            ValidateClientForm();
            LoadClientsFromDatabase(_pendingSelectedClientIdFromState);
            _pendingSelectedClientIdFromState = null;
            _workspaceStateReady = true;
            SaveWorkspaceState();

            var openClientFolderMenuItem = new MenuFlyoutItem { Text = "Открыть папку клиента" };
            _createContractMenuItem = new MenuFlyoutItem { Text = "Заключить договор", IsEnabled = true };
            _createContractMenuItem.Click += CreateContractMenuItem_Click;
            _clientContextFlyout.Items.Add(_createContractMenuItem);

            _markContractSignedMenuItem = new MenuFlyoutItem { Text = "Отметить договор подписан", IsEnabled = false };
            _markContractSignedMenuItem.Click += MarkContractSignedMenuItem_Click;
            _clientContextFlyout.Items.Add(_markContractSignedMenuItem);

            openClientFolderMenuItem.Click += OpenClientFolderMenuItem_Click;
            _clientContextFlyout.Items.Add(openClientFolderMenuItem);

            var openFileMenuItem = new MenuFlyoutItem { Text = "Открыть файл" };
            openFileMenuItem.Click += OpenClientFileMenuItem_Click;
            _clientFileContextFlyout.Items.Add(openFileMenuItem);

            var revealFileMenuItem = new MenuFlyoutItem { Text = "Показать в папке" };
            revealFileMenuItem.Click += RevealClientFileMenuItem_Click;
            _clientFileContextFlyout.Items.Add(revealFileMenuItem);

            var openClientFolderFromFileMenuItem = new MenuFlyoutItem { Text = "Открыть папку клиента" };
            openClientFolderFromFileMenuItem.Click += OpenClientFolderFromFileMenuItem_Click;
            _clientFileContextFlyout.Items.Add(openClientFolderFromFileMenuItem);

            var convertToPdfMenuItem = new MenuFlyoutItem { Text = "Конвертировать в PDF" };
            convertToPdfMenuItem.Click += ConvertClientFileToPdfMenuItem_Click;
            _clientFileContextFlyout.Items.Add(convertToPdfMenuItem);

            var deleteFileMenuItem = new MenuFlyoutItem { Text = "Удалить файл" };
            deleteFileMenuItem.Click += DeleteClientFileMenuItem_Click;
            _clientFileContextFlyout.Items.Add(deleteFileMenuItem);
        }

        private void UpdateClientContextMenuState(ClientInfo client)
        {
            if (_createContractMenuItem == null || _markContractSignedMenuItem == null) return;
            try
            {
                var organization = ActiveOrganizationService.GetRequired();
                using var db = new AppDbContext();
                var contract = ClientContractService.GetOrCreateContract(db, organization.Id, client.Id);
                bool hasGeneratedContract = contract.GeneratedAt.HasValue || !string.IsNullOrWhiteSpace(contract.DocumentRelativePath);
                bool alreadySigned = string.Equals(contract.Status, "Договор подписан", StringComparison.OrdinalIgnoreCase);

                if (alreadySigned)
                {
                    _createContractMenuItem.Text = "Договор уже подписан";
                    _createContractMenuItem.IsEnabled = false;
                }
                else if (hasGeneratedContract)
                {
                    _createContractMenuItem.Text = "Договор уже сформирован";
                    _createContractMenuItem.IsEnabled = false;
                }
                else
                {
                    _createContractMenuItem.Text = "Заключить договор";
                    _createContractMenuItem.IsEnabled = true;
                }
                _markContractSignedMenuItem.IsEnabled = hasGeneratedContract && !alreadySigned;
            }
            catch
            {
                _createContractMenuItem.Text = "Заключить договор";
                _createContractMenuItem.IsEnabled = false;
                _markContractSignedMenuItem.IsEnabled = false;
            }
        }

        private void MarkContractSignedMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_contextClient == null) return;
            try
            {
                var organization = ActiveOrganizationService.GetRequired();
                using var db = new AppDbContext();
                var clientFromDb = db.Clients.FirstOrDefault(c => c.Id == _contextClient.Id);
                if (clientFromDb == null) { StatusTextBlock.Text = "Не удалось найти клиента в базе данных."; return; }
                var contract = ClientContractService.GetOrCreateContract(db, organization.Id, clientFromDb.Id);
                bool hasGeneratedContract = contract.GeneratedAt.HasValue || !string.IsNullOrWhiteSpace(contract.DocumentRelativePath);
                if (!hasGeneratedContract) { StatusTextBlock.Text = "Сначала сформируйте договор для этого клиента."; return; }
                if (string.Equals(contract.Status, "Договор подписан", StringComparison.OrdinalIgnoreCase))
                {
                    StatusTextBlock.Text = $"У клиента «{clientFromDb.Name}» договор уже отмечен как подписанный.";
                    return;
                }
                ClientContractService.MarkSigned(db, contract);
                LoadClientsFromDatabase(clientFromDb.Id);
                StatusTextBlock.Text = $"Договор клиента «{clientFromDb.Name}» отмечен как подписанный.";
            }
            catch (Exception ex) { StatusTextBlock.Text = $"Ошибка отметки договора: {ex.Message}"; }
        }

        private void CreateContractMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_contextClient == null) return;
            try
            {
                var organization = ActiveOrganizationService.GetRequired();
                using var db = new AppDbContext();
                var clientFromDb = db.Clients.FirstOrDefault(c => c.Id == _contextClient.Id);
                if (clientFromDb == null) { StatusTextBlock.Text = "Не удалось найти клиента в базе данных."; return; }
                var contract = ClientContractService.GetOrCreateContract(db, organization.Id, clientFromDb.Id);
                if (!string.IsNullOrWhiteSpace(contract.DocumentRelativePath))
                {
                    string existingFullPath = ClientFileStorageService.GetFullPath(contract.DocumentRelativePath);
                    if (File.Exists(existingFullPath))
                    {
                        ClientFileStorageService.OpenFile(existingFullPath);
                        StatusTextBlock.Text = $"Открыт уже сформированный договор клиента «{clientFromDb.Name}».";
                        return;
                    }
                }
                StatusTextBlock.Text = $"Формирую договор от имени организации «{organization.Name}»...";
                string tempContractPath = ContractWordService.GenerateContractDocx(clientFromDb.Id);
                var copyResult = ClientFileStorageService.CopyFileForClient(clientFromDb, tempContractPath);
                string contractRelativePath = Convert.ToString(copyResult.RelativePath) ?? "";
                long contractFileSizeBytes = Convert.ToInt64(copyResult.FileSizeBytes);
                string contractFileName = Path.GetFileName(tempContractPath);
                db.ClientFiles.Add(new ClientFile
                {
                    ClientInfoId = clientFromDb.Id,
                    OriginalFileName = contractFileName,
                    RelativePath = contractRelativePath,
                    FileSizeBytes = contractFileSizeBytes,
                    AddedAt = DateTime.Now,
                    Category = "Договор"
                });
                ClientContractService.MarkGenerated(db, contract, contractFileName, contractRelativePath);
                db.SaveChanges();
                LoadClientsFromDatabase(clientFromDb.Id);
                if (ClientsListView.SelectedItem is ClientInfo selectedClient && selectedClient.Id == clientFromDb.Id)
                    LoadFilesForSelectedClient();
                string finalFullPath = ClientFileStorageService.GetFullPath(contractRelativePath);
                StatusTextBlock.Text = $"Договор сформирован и добавлен в файлы клиента «{clientFromDb.Name}».";
                if (File.Exists(finalFullPath)) ClientFileStorageService.OpenFile(finalFullPath);
                try { if (File.Exists(tempContractPath)) File.Delete(tempContractPath); } catch { }
            }
            catch (Exception ex) { StatusTextBlock.Text = $"Ошибка формирования договора: {ex.Message}"; }
        }

        private void ClientFileCard_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ClientFile clientFile) return;
            _contextClientFile = clientFile;
            ClientFilesListView.SelectedItem = clientFile;
            _clientFileContextFlyout.ShowAt(element);
            e.Handled = true;
        }

        private async void BankBicTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (BankBicTextBox == null) return;
            string bic = new string(BankBicTextBox.Text.Trim().Where(char.IsDigit).ToArray());
            if (bic.Length != 9) return;
            try
            {
                var result = await BankLookupService.FindByBicAsync(bic);
                if (result == null) { StatusTextBlock.Text = $"Банк по БИК {bic} не найден."; return; }
                BankBicTextBox.Text = result.Bic;
                BankNameTextBox.Text = result.BankName;
                BankCorrespondentAccountTextBox.Text = result.CorrespondentAccount;
                StatusTextBlock.Text = $"Реквизиты банка заполнены по БИК {bic}.";
            }
            catch (Exception ex) { StatusTextBlock.Text = $"Ошибка поиска банка по БИК: {ex.Message}"; }
        }

        private void UpdateClientHeader(ClientInfo? client)
        {
            if (ClientHeaderTitleTextBlock == null || ClientHeaderMetaTextBlock == null ||
                ClientHeaderSignatureTextBlock == null || ClientHeaderStatusTextBlock == null) return;

            if (client == null)
            {
                ClientHeaderTitleTextBlock.Text = "Новый клиент";
                ClientHeaderMetaTextBlock.Text = "Заполните реквизиты или используйте автозаполнение по ИНН";
                ClientHeaderSignatureTextBlock.Text = "ЭЦП: —";
                ClientHeaderStatusTextBlock.Text = "Новый";
                return;
            }

            string status = string.IsNullOrWhiteSpace(client.Status) ? "Активный" : client.Status;
            string clientType = string.IsNullOrWhiteSpace(client.ClientType) ? "Тип не указан" : client.ClientType;
            string inn = string.IsNullOrWhiteSpace(client.Inn) ? "ИНН не указан" : $"ИНН {client.Inn}";

            ClientHeaderTitleTextBlock.Text = string.IsNullOrWhiteSpace(client.Name) ? "Клиент без названия" : client.Name;
            ClientHeaderMetaTextBlock.Text = $"{status} · {clientType} · {inn}";
            ClientHeaderSignatureTextBlock.Text = client.NearestSignatureExpiresDate.HasValue
                ? $"ЭЦП: ближайшая до {client.NearestSignatureExpiresDate.Value:dd.MM.yyyy}"
                : "ЭЦП: не добавлена";
            ClientHeaderStatusTextBlock.Text = status;
        }

        private void ClientFileCard_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ClientFile clientFile) return;
            _contextClientFile = clientFile;
            ClientFilesListView.SelectedItem = clientFile;
            OpenSelectedClientFileButton_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }

        private void OpenClientFileMenuItem_Click(object sender, RoutedEventArgs e) => OpenSelectedClientFileButton_Click(sender, e);
        private void RevealClientFileMenuItem_Click(object sender, RoutedEventArgs e) => RevealSelectedClientFileButton_Click(sender, e);
        private void OpenClientFolderFromFileMenuItem_Click(object sender, RoutedEventArgs e) => OpenClientFilesFolderButton_Click(sender, e);
        private void ConvertClientFileToPdfMenuItem_Click(object sender, RoutedEventArgs e) => ConvertSelectedClientFileToPdfButton_Click(sender, e);
        private void DeleteClientFileMenuItem_Click(object sender, RoutedEventArgs e) => DeleteSelectedClientFileButton_Click(sender, e);

        private void HighlightRightTappedClientCard(Border border)
        {
            ResetRightTappedClientCard();
            _rightTappedClientBorder = border;
            bool isLight = ThemeService.CurrentTheme == ThemeService.ThemeLight;
            if (isLight)
            {
                _rightTappedClientBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 249, 230));
                _rightTappedClientBorder.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 200, 146, 14));
            }
            else
            {
                _rightTappedClientBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 24, 32, 45));
                _rightTappedClientBorder.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 90, 145, 255));
            }
            _rightTappedClientBorder.BorderThickness = new Thickness(2);
        }

        private void ResetRightTappedClientCard()
        {
            if (_rightTappedClientBorder == null) return;
            bool isLight = ThemeService.CurrentTheme == ThemeService.ThemeLight;
            if (isLight)
            {
                _rightTappedClientBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 245, 247, 250));
                _rightTappedClientBorder.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 221, 225, 234));
            }
            else
            {
                _rightTappedClientBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 17, 21, 28));
                _rightTappedClientBorder.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 36, 48, 66));
            }
            _rightTappedClientBorder.BorderThickness = _clientViewMode == 2 ? new Thickness(0, 0, 0, 1) : new Thickness(1);
            _rightTappedClientBorder = null;
        }

        private void ClientCard_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ClientInfo client) return;
            _contextClient = client;
            ClientsListView.SelectedItem = client;
            RefreshSelectedClientHighlight();
            UpdateClientContextMenuState(client);
            _clientContextFlyout.ShowAt(element);
            e.Handled = true;
        }

        private void OpenClientFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_contextClient == null) return;
            ClientFileStorageService.OpenClientFolder(_contextClient);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is int clientId && clientId > 0)
            {
                _pendingClientIdFromDashboard = clientId;
                TrySelectPendingClientFromDashboard();
                return;
            }
            if (e.Parameter is DashboardClientNavigationRequest request && request.ClientId > 0)
            {
                _pendingClientIdFromDashboard = request.ClientId;
                TrySelectPendingClientFromDashboard();
            }
        }

        private void ClientViewModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _clientViewMode++;
            if (_clientViewMode > 2) _clientViewMode = 0;
            ResetHighlightedClientCard();
            ApplyClientViewMode();
            RefreshSelectedClientHighlight();
            if (_workspaceStateReady) SaveWorkspaceState();
        }

        private async Task ShowClientAiAnalysisDialogAsync(string clientName, string analysis)
        {
            var textBlock = new TextBlock
            {
                Text = analysis,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14
            };
            var scrollViewer = new ScrollViewer { Content = textBlock, Width = 720, Height = 520, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var dialog = new ContentDialog
            {
                Title = $"Проверка клиента: {clientName}",
                Content = scrollViewer,
                CloseButtonText = "Закрыть",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void ApplyClientViewMode()
        {
            if (ClientsListView == null) return;
            switch (_clientViewMode)
            {
                case 0:
                    ClientsListView.ItemTemplate = (DataTemplate)Resources["NormalClientItemTemplate"];
                    if (DenseClientsHeaderGrid != null) DenseClientsHeaderGrid.Visibility = Visibility.Collapsed;
                    ToolTipService.SetToolTip(ClientViewModeToggleButton, "Обычный вид списка");
                    break;
                case 1:
                    ClientsListView.ItemTemplate = (DataTemplate)Resources["CompactClientItemTemplate"];
                    if (DenseClientsHeaderGrid != null) DenseClientsHeaderGrid.Visibility = Visibility.Collapsed;
                    ToolTipService.SetToolTip(ClientViewModeToggleButton, "Компактный вид списка");
                    break;
                case 2:
                    ClientsListView.ItemTemplate = (DataTemplate)Resources["DenseClientItemTemplate"];
                    if (DenseClientsHeaderGrid != null) DenseClientsHeaderGrid.Visibility = Visibility.Visible;
                    ToolTipService.SetToolTip(ClientViewModeToggleButton, "Табличный вид списка");
                    break;
            }
        }

        private void TrySelectPendingClientFromDashboard()
        {
            if (!_pendingClientIdFromDashboard.HasValue) return;
            if (ClientsListView == null || _clients.Count == 0) return;
            var targetClient = _clients.FirstOrDefault(x => GetClientIdForDashboardNavigation(x) == _pendingClientIdFromDashboard.Value);
            if (targetClient == null) return;
            ClientsListView.SelectedItem = targetClient;
            ClientsListView.ScrollIntoView(targetClient);
            _pendingClientIdFromDashboard = null;
        }

        private static int GetClientIdForDashboardNavigation(ClientInfo client)
        {
            var type = client.GetType();
            var property = type.GetProperty("Id") ?? type.GetProperty("ClientId");
            if (property == null) return 0;
            var value = property.GetValue(client);
            if (value is int intValue) return intValue;
            return int.TryParse(value?.ToString(), out var parsed) ? parsed : 0;
        }

        private void LoadFilesForSelectedClient()
        {
            _clientFiles.Clear();
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) return;
            string selectedCategoryFilter = GetSelectedClientFileFilterCategory();
            string searchText = GetClientFileSearchText().ToLowerInvariant();
            using (var db = new AppDbContext())
            {
                var filesQuery = db.ClientFiles.AsNoTracking().Where(f => f.ClientInfoId == selectedClient.Id);
                if (selectedCategoryFilter != "Все файлы") filesQuery = filesQuery.Where(f => f.Category == selectedCategoryFilter);
                var filesFromDb = filesQuery.OrderByDescending(f => f.AddedAt).ToList();
                if (!string.IsNullOrWhiteSpace(searchText))
                    filesFromDb = filesFromDb.Where(f =>
                        (f.OriginalFileName?.ToLowerInvariant().Contains(searchText) ?? false) ||
                        (f.Category?.ToLowerInvariant().Contains(searchText) ?? false)).ToList();
                foreach (var file in filesFromDb) _clientFiles.Add(file);
                ClientFilesListView.SelectedItem = null;
                UpdateFileActionButtonsState();
            }
        }

        private void ClearClientSearchButton_Click(object sender, RoutedEventArgs e)
        {
            bool wasReady = _workspaceStateReady;
            _workspaceStateReady = false;
            if (SearchTextBox != null) SearchTextBox.Text = "";
            if (SignatureFilterComboBox != null) SignatureFilterComboBox.SelectedIndex = 0;
            if (StatusFilterComboBox != null) StatusFilterComboBox.SelectedIndex = 0;
            _workspaceStateReady = wasReady;
            LoadClientsFromDatabase();
            SaveWorkspaceState();
            StatusTextBlock.Text = "Поиск и фильтры клиентов очищены.";
        }

        private string GetCurrentFormClientType()
        {
            if (ClientTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
                return selectedItem.Content?.ToString() ?? "ООО";
            return "ООО";
        }

        private string GetSelectedClientStatus()
        {
            if (ClientStatusComboBox.SelectedItem is ComboBoxItem selectedItem)
                return selectedItem.Content?.ToString() ?? "Активный";
            return "Активный";
        }

        private void SetClientStatusComboBox(string status)
        {
            foreach (var item in ClientStatusComboBox.Items)
            {
                if (item is ComboBoxItem comboBoxItem &&
                    string.Equals(comboBoxItem.Content?.ToString(), status, StringComparison.OrdinalIgnoreCase))
                {
                    ClientStatusComboBox.SelectedItem = comboBoxItem;
                    return;
                }
            }
            ClientStatusComboBox.SelectedIndex = 0;
        }

        private bool IsEntrepreneurType(string clientType) => clientType == "ИП" || clientType == "ИПГКФХ";

        private void UpdateClientFormLabels()
        {
            if (!_clientFormReady) return;
            if (DirectorLabelTextBlock == null || OgrnLabelTextBlock == null || OgrnTextBox == null) return;
            string clientType = GetCurrentFormClientType();
            bool isEntrepreneur = IsEntrepreneurType(clientType);
            DirectorLabelTextBlock.Text = isEntrepreneur ? "ФИО предпринимателя" : "ФИО руководителя организации";
            OgrnLabelTextBlock.Text = isEntrepreneur ? "ОГРНИП" : "ОГРН";
            OgrnTextBox.PlaceholderText = isEntrepreneur ? "Введите ОГРНИП" : "Введите ОГРН";
        }

        private bool IsInnValidForCurrentType(string inn, string clientType)
        {
            if (string.IsNullOrWhiteSpace(inn) || !inn.All(char.IsDigit)) return false;
            return IsEntrepreneurType(clientType) ? inn.Length == 12 : inn.Length == 10;
        }

        private bool IsOgrnValidForCurrentType(string ogrn, string clientType)
        {
            if (string.IsNullOrWhiteSpace(ogrn)) return true;
            if (!ogrn.All(char.IsDigit)) return false;
            return IsEntrepreneurType(clientType) ? ogrn.Length == 15 : ogrn.Length == 13;
        }

        private bool ValidateClientForm()
        {
            if (!_clientFormReady) return false;
            if (ClientTypeComboBox == null || ClientNameTextBox == null || DirectorTextBox == null ||
                InnTextBox == null || OgrnTextBox == null || AddressTextBox == null ||
                ClientFormValidationTextBlock == null || SaveClientButton == null ||
                UpdateSelectedClientButton == null || FillByInnButton == null) return false;

            string clientType = GetCurrentFormClientType();
            string clientName = ClientNameTextBox.Text.Trim();
            string directorName = DirectorTextBox.Text.Trim();
            string inn = InnTextBox.Text.Trim();
            string ogrn = OgrnTextBox.Text.Trim();
            string address = AddressTextBox.Text.Trim();
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(clientName)) errors.Add("Укажи наименование организации / ФИО.");
            if (string.IsNullOrWhiteSpace(inn)) errors.Add("Укажи ИНН.");
            else if (!IsInnValidForCurrentType(inn, clientType))
                errors.Add(IsEntrepreneurType(clientType)
                    ? "Для ИП/ИПГКФХ ИНН должен содержать 12 цифр."
                    : "Для ООО/АНО ИНН должен содержать 10 цифр.");
            if (!string.IsNullOrWhiteSpace(ogrn) && !IsOgrnValidForCurrentType(ogrn, clientType))
                errors.Add(IsEntrepreneurType(clientType) ? "ОГРНИП должен содержать 15 цифр." : "ОГРН должен содержать 13 цифр.");
            if (!IsEntrepreneurType(clientType) && string.IsNullOrWhiteSpace(directorName))
                errors.Add("Укажи ФИО руководителя организации.");
            if (string.IsNullOrWhiteSpace(address)) errors.Add("Укажи юридический адрес.");

            bool isValid = errors.Count == 0;
            ClientFormValidationTextBlock.Text = isValid ? "Форма заполнена корректно." : string.Join(" ", errors);
            ClientFormValidationTextBlock.Foreground = isValid
                ? new SolidColorBrush(Colors.LightGreen)
                : new SolidColorBrush(Colors.Orange);
            SaveClientButton.IsEnabled = isValid;
            UpdateSelectedClientButton.IsEnabled = isValid;
            FillByInnButton.IsEnabled = IsInnValidForCurrentType(inn, clientType);
            return isValid;
        }

        private void ClientTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_clientFormReady) return;
            UpdateClientFormLabels();
            ValidateClientForm();
        }

        private void ClientFormField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_clientFormReady) return;
            ValidateClientForm();
        }

        private void ClientFileSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ClientFileSearchTextBox == null || ClientFilesListView == null) return;
            LoadFilesForSelectedClient();
        }

        private void ClearClientFileSearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientFileSearchTextBox != null) ClientFileSearchTextBox.Text = "";
            if (ClientFileFilterComboBox != null) ClientFileFilterComboBox.SelectedIndex = 0;
            LoadFilesForSelectedClient();
            StatusTextBlock.Text = "Поиск и фильтр файлов очищены.";
        }

        private void ClientFileFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClientFileFilterComboBox == null || ClientFilesListView == null) return;
            LoadFilesForSelectedClient();
        }

        private string GetSelectedClientFileCategory()
        {
            if (ClientFileCategoryComboBox.SelectedItem is ComboBoxItem selectedItem)
                return selectedItem.Content?.ToString() ?? "Прочее";
            return "Прочее";
        }

        private void ClearClientFileInputFields() => ClientFileCategoryComboBox.SelectedIndex = 5;

        private void RevealSelectedClientFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientFilesListView.SelectedItem is not ClientFile selectedFile)
            { StatusTextBlock.Text = "Сначала выбери файл."; return; }
            try
            {
                string fullPath = ClientFileStorageService.GetFullPath(selectedFile.RelativePath);
                if (!File.Exists(fullPath)) { StatusTextBlock.Text = "Файл не найден на диске."; return; }
                ClientFileStorageService.RevealFileInExplorer(fullPath);
                StatusTextBlock.Text = $"Файл показан в проводнике: {selectedFile.OriginalFileName}";
            }
            catch (Exception ex) { StatusTextBlock.Text = $"Ошибка открытия файла в проводнике: {ex.Message}"; }
        }

        private string GetSelectedClientFileFilterCategory()
        {
            if (ClientFileFilterComboBox.SelectedItem is ComboBoxItem selectedItem)
                return selectedItem.Content?.ToString() ?? "Все файлы";
            return "Все файлы";
        }

        private async void ClientFilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFileActionButtonsState();
            if (ClientFilesListView.SelectedItem is not ClientFile selectedFile) { ClearFilePreview(); return; }
            await LoadSelectedClientFilePreviewAsync(selectedFile);
        }

        private void UpdateFileActionButtonsState()
        {
            bool hasClient = ClientsListView?.SelectedItem is ClientInfo;
            bool hasFile = ClientFilesListView?.SelectedItem is ClientFile;
            SetFileActionState(AddClientFileTopButton, AddClientFileTopButtonHost, hasClient, "Прикрепить файл к выбранному клиенту", "Сначала выберите клиента");
            SetFileActionState(OpenClientFilesFolderTopButton, OpenClientFilesFolderTopButtonHost, hasClient, "Открыть папку файлов выбранного клиента", "Сначала выберите клиента");
            SetFileActionState(OpenSelectedClientFileTopButton, OpenSelectedClientFileTopButtonHost, hasFile, "Открыть выбранный файл", "Сначала выберите файл");
            SetFileActionState(RevealSelectedClientFileTopButton, RevealSelectedClientFileTopButtonHost, hasFile, "Показать выбранный файл в папке", "Сначала выберите файл");
            SetFileActionState(ConvertSelectedClientFileToPdfTopButton, ConvertSelectedClientFileToPdfTopButtonHost, hasFile, "Конвертировать выбранный файл в PDF", "Сначала выберите файл");
            SetFileActionState(DeleteSelectedClientFileTopButton, DeleteSelectedClientFileTopButtonHost, hasFile, "Удалить выбранный файл", "Сначала выберите файл");
        }

        private void SetFileActionState(Button? button, FrameworkElement? host, bool isEnabled, string enabledToolTip, string disabledToolTip)
        {
            if (button != null) button.IsEnabled = isEnabled;
            if (host != null) ToolTipService.SetToolTip(host, isEnabled ? enabledToolTip : disabledToolTip);
        }

        private async Task LoadSelectedClientFilePreviewAsync(ClientFile clientFile)
        {
            ClearFilePreview();
            string fullPath = ClientFileStorageService.GetFullPath(clientFile.RelativePath);
            if (!File.Exists(fullPath))
            {
                FilePreviewStatusTextBlock.Text = "Файл не найден на диске.";
                ShowFallbackPreview("Файл не найден", "Похоже, файл был удален или перемещен.");
                return;
            }
            string extension = Path.GetExtension(clientFile.OriginalFileName).ToLowerInvariant();
            try
            {
                if (IsImageExtension(extension)) { await ShowImagePreviewAsync(fullPath); FilePreviewStatusTextBlock.Text = $"Предпросмотр изображения: {clientFile.OriginalFileName}"; return; }
                if (extension == ".pdf") { ShowPdfPreview(fullPath); FilePreviewStatusTextBlock.Text = $"Предпросмотр PDF: {clientFile.OriginalFileName}"; return; }
                if (IsTextPreviewExtension(extension)) { await ShowTextPreviewAsync(fullPath); FilePreviewStatusTextBlock.Text = $"Предпросмотр текста: {clientFile.OriginalFileName}"; return; }
                ShowFallbackPreview("Предпросмотр пока недоступен", $"Тип файла: {clientFile.FileTypeDescription}.");
                FilePreviewStatusTextBlock.Text = $"Нет встроенного предпросмотра для файла: {clientFile.OriginalFileName}";
            }
            catch (Exception ex) { ShowFallbackPreview("Ошибка предпросмотра", ex.Message); FilePreviewStatusTextBlock.Text = $"Ошибка предпросмотра: {clientFile.OriginalFileName}"; }
        }

        private void ClearFilePreview()
        {
            FilePreviewImage.Source = null; FilePreviewImage.Visibility = Visibility.Collapsed;
            FilePreviewWebView.Source = null; FilePreviewWebView.Visibility = Visibility.Collapsed;
            FilePreviewTextBox.Text = ""; FilePreviewTextBox.Visibility = Visibility.Collapsed;
            FilePreviewFallbackPanel.Visibility = Visibility.Visible;
            FilePreviewFallbackTitleTextBlock.Text = "Предпросмотр недоступен";
            FilePreviewFallbackTextBlock.Text = "Выбери файл для предпросмотра.";
            FilePreviewStatusTextBlock.Text = "Выбери файл для предпросмотра.";
        }

        private async Task ShowImagePreviewAsync(string fullPath)
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(fullPath);
            using IRandomAccessStream stream = await storageFile.OpenAsync(FileAccessMode.Read);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            FilePreviewImage.Source = bitmap;
            FilePreviewImage.Visibility = Visibility.Visible;
            FilePreviewFallbackPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowPdfPreview(string fullPath)
        {
            FilePreviewWebView.Source = new Uri(fullPath);
            FilePreviewWebView.Visibility = Visibility.Visible;
            FilePreviewFallbackPanel.Visibility = Visibility.Collapsed;
        }

        private async Task ShowTextPreviewAsync(string fullPath)
        {
            string text = await File.ReadAllTextAsync(fullPath);
            const int maxLength = 20000;
            if (text.Length > maxLength) text = text.Substring(0, maxLength) + Environment.NewLine + Environment.NewLine + "... [текст обрезан]";
            FilePreviewTextBox.Text = text;
            FilePreviewTextBox.Visibility = Visibility.Visible;
            FilePreviewFallbackPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowFallbackPreview(string title, string message)
        {
            FilePreviewFallbackTitleTextBlock.Text = title;
            FilePreviewFallbackTextBlock.Text = message;
            FilePreviewFallbackPanel.Visibility = Visibility.Visible;
        }

        private bool IsImageExtension(string extension) => extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";
        private bool IsTextPreviewExtension(string extension) => extension is ".txt" or ".xml" or ".csv" or ".json" or ".log";

        private void OpenClientFilesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) { StatusTextBlock.Text = "Сначала выбери клиента."; return; }
            try { ClientFileStorageService.OpenClientFolder(selectedClient); StatusTextBlock.Text = $"Открыта папка файлов клиента «{selectedClient.Name}»."; }
            catch (Exception ex) { StatusTextBlock.Text = $"Ошибка открытия папки файлов клиента: {ex.Message}"; }
        }

        private void ClearFilesSection()
        {
            _clientFiles.Clear();
            ClientFilesListView.SelectedItem = null;
            ClearClientFileInputFields();
            ClearFilePreview();
            UpdateFileActionButtonsState();
        }

        private void ConvertSelectedClientFileToPdfButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) { StatusTextBlock.Text = "Сначала выбери клиента."; return; }
            if (ClientFilesListView.SelectedItem is not ClientFile selectedFile) { StatusTextBlock.Text = "Сначала выбери файл для конвертации."; return; }
            try
            {
                string sourceFullPath = ClientFileStorageService.GetFullPath(selectedFile.RelativePath);
                if (!File.Exists(sourceFullPath)) { StatusTextBlock.Text = "Исходный файл не найден на диске."; return; }
                string originalNameWithoutExtension = Path.GetFileNameWithoutExtension(selectedFile.OriginalFileName);
                var newPdfPath = ClientFileStorageService.GetNewPdfPathForClient(selectedClient, originalNameWithoutExtension + "_pdf");
                PdfConversionService.ConvertFileToPdf(sourceFullPath, newPdfPath.FullPath);
                var newPdfFile = new ClientFile
                {
                    ClientInfoId = selectedClient.Id,
                    OriginalFileName = Path.GetFileName(newPdfPath.FullPath),
                    RelativePath = newPdfPath.RelativePath,
                    FileSizeBytes = new FileInfo(newPdfPath.FullPath).Length,
                    AddedAt = DateTime.Now,
                    Category = selectedFile.Category
                };
                using (var db = new AppDbContext()) { db.ClientFiles.Add(newPdfFile); db.SaveChanges(); }
                LoadFilesForSelectedClient();
                StatusTextBlock.Text = $"Файл «{selectedFile.OriginalFileName}» конвертирован в PDF.";
            }
            catch (Exception ex) { StatusTextBlock.Text = $"Ошибка конвертации в PDF: {ex.Message}"; }
        }

        private string GetClientFileSearchText() => ClientFileSearchTextBox?.Text?.Trim() ?? "";

        private async void AddClientFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) { StatusTextBlock.Text = "Сначала выбери клиента."; return; }
            string selectedCategory = GetSelectedClientFileCategory();
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            IntPtr hwnd = WindowNative.GetWindowHandle(App.MainAppWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
            var pickedFile = await picker.PickSingleFileAsync();
            if (pickedFile == null) { StatusTextBlock.Text = "Выбор файла отменен."; return; }
            if (string.IsNullOrWhiteSpace(pickedFile.Path) || !File.Exists(pickedFile.Path)) { StatusTextBlock.Text = "Не удалось получить путь к выбранному файлу."; return; }
            var copyResult = ClientFileStorageService.CopyFileForClient(selectedClient, pickedFile.Path);
            var clientFile = new ClientFile
            {
                ClientInfoId = selectedClient.Id,
                OriginalFileName = Path.GetFileName(pickedFile.Path),
                RelativePath = copyResult.RelativePath,
                FileSizeBytes = copyResult.FileSizeBytes,
                AddedAt = DateTime.Now,
                Category = selectedCategory
            };
            using (var db = new AppDbContext()) { db.ClientFiles.Add(clientFile); db.SaveChanges(); }
            LoadFilesForSelectedClient();
            ClearClientFileInputFields();
            StatusTextBlock.Text = $"Файл «{clientFile.OriginalFileName}» прикреплен к клиенту «{selectedClient.Name}».";
        }

        private void OpenSelectedClientFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientFilesListView.SelectedItem is not ClientFile selectedFile) { StatusTextBlock.Text = "Сначала выбери файл в списке."; return; }
            string fullPath = ClientFileStorageService.GetFullPath(selectedFile.RelativePath);
            if (!File.Exists(fullPath)) { StatusTextBlock.Text = "Файл не найден на диске."; return; }
            ClientFileStorageService.OpenFile(fullPath);
        }

        private void DeleteSelectedClientFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientFilesListView.SelectedItem is not ClientFile selectedFile) { StatusTextBlock.Text = "Сначала выбери файл для удаления."; return; }
            ClientInfo? currentClient = ClientsListView.SelectedItem as ClientInfo;
            using (var db = new AppDbContext())
            {
                var fileFromDb = db.ClientFiles.FirstOrDefault(f => f.Id == selectedFile.Id);
                if (fileFromDb == null) { StatusTextBlock.Text = "Не удалось найти файл в базе данных."; return; }
                string fullPath = ClientFileStorageService.GetFullPath(fileFromDb.RelativePath);
                db.ClientFiles.Remove(fileFromDb);
                db.SaveChanges();
                ClientFileStorageService.DeleteFileIfExists(fullPath);
            }
            if (currentClient != null) ClientFileStorageService.DeleteClientFolderIfEmpty(currentClient);
            LoadFilesForSelectedClient();
            StatusTextBlock.Text = "Выбранный файл удален.";
        }

        private string GetSelectedStatusFilter()
        {
            if (StatusFilterComboBox?.SelectedItem is ComboBoxItem selectedItem)
                return selectedItem.Content?.ToString() ?? "Все статусы";
            return "Все статусы";
        }

        private static void ApplyActiveOrganizationContractStateToClient(ClientInfo client, Dictionary<int, ClientContract> contractMap)
        {
            if (contractMap.TryGetValue(client.Id, out var contract))
            {
                client.ContractStatus = string.IsNullOrWhiteSpace(contract.Status) ? "Требует договора" : contract.Status;
                client.ContractGeneratedAt = contract.GeneratedAt;
                client.ContractSignedAt = contract.SignedAt;
                return;
            }
            client.ContractStatus = "Требует договора";
            client.ContractGeneratedAt = null;
            client.ContractSignedAt = null;
        }

        private void LoadClientsFromDatabase(int? selectedClientId = null)
        {
            ResetRightTappedClientCard();
            _clients.Clear();
            ContractSchemaService.EnsureContractTables();
            ClientContractService.EnsureContractsForActiveOrganization();
            int organizationId = ActiveOrganizationService.GetRequired().Id;
            using (var db = new AppDbContext())
            {
                var contractMap = db.ClientContracts.AsNoTracking().Where(x => x.OrganizationProfileId == organizationId).ToDictionary(x => x.ClientInfoId);
                var clientsFromDb = db.Clients.AsNoTracking().OrderBy(c => c.Name).ToList();
                foreach (var client in clientsFromDb)
                {
                    client.SignatureCount = db.DigitalSignatures.Count(s => s.ClientInfoId == client.Id);
                    client.AccountCount = db.BankAccounts.Count(a => a.ClientInfoId == client.Id);
                    client.NoteCount = db.ClientNotes.Count(n => n.ClientInfoId == client.Id);
                    var clientSignatures = db.DigitalSignatures.AsNoTracking().Where(s => s.ClientInfoId == client.Id).OrderBy(s => s.ExpiresDate).ToList();
                    FillClientSignatureInfo(client, clientSignatures);
                    var latestNote = db.ClientNotes.AsNoTracking().Where(n => n.ClientInfoId == client.Id).OrderByDescending(n => n.CreatedAt).FirstOrDefault();
                    if (latestNote != null) client.LatestNoteCreatedAt = latestNote.CreatedAt;
                    var primaryBank = db.BankAccounts.AsNoTracking().Where(a => a.ClientInfoId == client.Id).OrderBy(a => a.BankName).FirstOrDefault();
                    if (primaryBank != null) client.PrimaryBankName = primaryBank.BankName;
                    ApplyActiveOrganizationContractStateToClient(client, contractMap);
                    if (!MatchesCurrentFilter(client)) continue;
                    _clients.Add(client);
                }
                ClientFileStorageService.EnsureClientFoldersNamed(_clients);
            }
            if (selectedClientId.HasValue)
            {
                var clientToSelect = _clients.FirstOrDefault(c => c.Id == selectedClientId.Value);
                if (clientToSelect != null) { ClientsListView.SelectedItem = clientToSelect; ClientsListView.ScrollIntoView(clientToSelect); }
            }
            if (_clients.Count == 0) StatusTextBlock.Text = "По текущему фильтру клиенты не найдены.";
        }

        private void FillClientSignatureInfo(ClientInfo client, List<DigitalSignature> signatures)
        {
            client.SignatureCount = signatures.Count;
            if (signatures.Count == 0) { client.NearestSignatureExpiresDate = null; client.SignatureWarningText = "ЭЦП не добавлена"; return; }
            var nearestSignature = signatures.OrderBy(s => s.ExpiresDate).First();
            client.NearestSignatureExpiresDate = nearestSignature.ExpiresDate;
            int daysLeft = (nearestSignature.ExpiresDate.Date - DateTime.Today).Days;
            if (daysLeft < 0) client.SignatureWarningText = $"ПРОСРОЧЕНО: до {nearestSignature.ExpiresDate:dd.MM.yyyy}";
            else if (daysLeft <= 7) client.SignatureWarningText = $"СРОЧНО: истекает через {daysLeft} дн.";
            else if (daysLeft <= 30) client.SignatureWarningText = $"ВНИМАНИЕ: истекает через {daysLeft} дн.";
            else client.SignatureWarningText = $"Норма: действует до {nearestSignature.ExpiresDate:dd.MM.yyyy}";
        }

        private void ShowClientsSectionButton_Click(object sender, RoutedEventArgs e) => ClientsSectionGrid.Visibility = Visibility.Visible;
        private void ShowProblemSignaturesSectionButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(ProblemSignaturesPage));
        private void OpenBackupsPageButton_Click(object sender, RoutedEventArgs e) => Frame?.Navigate(typeof(BackupsPage));

        private void LoadSignaturesForSelectedClient()
        {
            _signatures.Clear();
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) { UpdateSignatureSummary(); return; }
            using (var db = new AppDbContext())
            {
                var signaturesFromDb = db.DigitalSignatures.AsNoTracking().Where(s => s.ClientInfoId == selectedClient.Id).OrderBy(s => s.ExpiresDate).ToList();
                foreach (var sig in signaturesFromDb) _signatures.Add(sig);
            }
            UpdateSignatureSummary();
        }

        private void LoadBankAccountsForSelectedClient()
        {
            _bankAccounts.Clear();
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) { UpdateBankAccountSummary(); return; }
            using (var db = new AppDbContext())
            {
                var accountsFromDb = db.BankAccounts.AsNoTracking().Where(a => a.ClientInfoId == selectedClient.Id).OrderBy(a => a.BankName).ThenBy(a => a.AccountNumber).ToList();
                foreach (var acc in accountsFromDb) _bankAccounts.Add(acc);
            }
            UpdateBankAccountSummary();
        }

        private void LoadNotesForSelectedClient()
        {
            _notes.Clear();
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) { UpdateNoteSummary(); return; }
            using (var db = new AppDbContext())
            {
                var notesFromDb = db.ClientNotes.AsNoTracking().Where(n => n.ClientInfoId == selectedClient.Id).OrderByDescending(n => n.CreatedAt).ToList();
                foreach (var note in notesFromDb) _notes.Add(note);
            }
            UpdateNoteSummary();
        }

        private void SignatureFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_workspaceStateReady) return;
            if (SignatureFilterComboBox == null || ClientsListView == null) return;
            int? selectedClientId = ClientsListView.SelectedItem is ClientInfo sc ? sc.Id : (int?)null;
            SaveWorkspaceState();
            LoadClientsFromDatabase(selectedClientId);
        }

        private void StatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_workspaceStateReady) return;
            if (StatusFilterComboBox == null || ClientsListView == null) return;
            int? selectedClientId = ClientsListView.SelectedItem is ClientInfo sc ? sc.Id : (int?)null;
            SaveWorkspaceState();
            LoadClientsFromDatabase(selectedClientId);
        }

        private void SaveClientButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateClientForm()) { StatusTextBlock.Text = "Исправь ошибки в форме перед сохранением."; return; }
            using (var db = new AppDbContext())
            {
                var client = new ClientInfo
                {
                    ClientType = GetCurrentFormClientType(),
                    Status = GetSelectedClientStatus(),
                    Name = ClientNameTextBox.Text.Trim(),
                    DirectorFullName = DirectorTextBox.Text.Trim(),
                    Inn = InnTextBox.Text.Trim(),
                    Ogrn = OgrnTextBox.Text.Trim(),
                    Address = AddressTextBox.Text.Trim(),
                    ContractStatus = "Требует договора"
                };
                db.Clients.Add(client);
                db.SaveChanges();
                LoadClientsFromDatabase(client.Id);
                StatusTextBlock.Text = $"Клиент «{client.Name}» сохранен.";
            }
        }

        private void UpdateSelectedClientButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) { StatusTextBlock.Text = "Сначала выбери клиента для обновления."; return; }
            if (!ValidateClientForm()) { StatusTextBlock.Text = "Исправь ошибки в форме перед обновлением."; return; }
            using (var db = new AppDbContext())
            {
                var clientFromDb = db.Clients.FirstOrDefault(c => c.Id == selectedClient.Id);
                if (clientFromDb == null) { StatusTextBlock.Text = "Не удалось найти клиента в базе данных."; return; }
                clientFromDb.ClientType = GetCurrentFormClientType();
                clientFromDb.Status = GetSelectedClientStatus();
                clientFromDb.Name = ClientNameTextBox.Text.Trim();
                clientFromDb.DirectorFullName = DirectorTextBox.Text.Trim();
                clientFromDb.Inn = InnTextBox.Text.Trim();
                clientFromDb.Ogrn = OgrnTextBox.Text.Trim();
                clientFromDb.Address = AddressTextBox.Text.Trim();
                db.SaveChanges();
            }
            LoadClientsFromDatabase(selectedClient.Id);
            StatusTextBlock.Text = $"Клиент «{ClientNameTextBox.Text.Trim()}» успешно обновлен.";
        }

        private bool MatchesCurrentFilter(ClientInfo client)
        {
            string searchText = SearchTextBox?.Text?.Trim().ToLowerInvariant() ?? "";
            int filterIndex = SignatureFilterComboBox?.SelectedIndex ?? 0;
            if (filterIndex < 0) filterIndex = 0;
            bool matchesSearch = string.IsNullOrWhiteSpace(searchText) ||
                (client.Name?.ToLowerInvariant().Contains(searchText) ?? false) ||
                (client.DirectorFullName?.ToLowerInvariant().Contains(searchText) ?? false) ||
                (client.Inn?.ToLowerInvariant().Contains(searchText) ?? false) ||
                (client.Address?.ToLowerInvariant().Contains(searchText) ?? false);
            if (!matchesSearch) return false;
            DateTime today = DateTime.Today;
            DateTime? nearestExpires = client.NearestSignatureExpiresDate;
            return filterIndex switch
            {
                0 => true,
                1 => nearestExpires.HasValue && nearestExpires.Value.Date >= today && nearestExpires.Value.Date <= today.AddDays(30),
                2 => nearestExpires.HasValue && nearestExpires.Value.Date >= today && nearestExpires.Value.Date <= today.AddDays(7),
                3 => nearestExpires.HasValue && nearestExpires.Value.Date < today,
                _ => true
            };
        }

        private void DeleteSelectedClientButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) { StatusTextBlock.Text = "Сначала выбери клиента для удаления."; return; }
            string deletedClientName = selectedClient.Name;
            int clientId = selectedClient.Id;
            try
            {
                using (var db = new AppDbContext())
                {
                    var clientFromDb = db.Clients.FirstOrDefault(c => c.Id == clientId);
                    if (clientFromDb == null) { StatusTextBlock.Text = "Не удалось найти клиента в базе данных."; return; }

                    var clientFiles = db.ClientFiles.Where(f => f.ClientInfoId == clientId).ToList();
                    foreach (var clientFile in clientFiles) ClientFileStorageService.DeleteFileIfExists(ClientFileStorageService.GetFullPath(clientFile.RelativePath));

                    // Удаление зависимых строк строго в порядке "дети → родители",
                    // иначе SQLite с включёнными FK constraint падает на SaveChanges.
                    // Порядок:
                    //   1) InvoiceItems  → ссылаются на Invoice
                    //   2) InvoiceDocuments → ссылаются на Invoice и на ClientInfo напрямую
                    //   3) Invoices → ссылаются на ClientInfo
                    //   4) ClientContracts, ClientRecurringServices → ссылаются на ClientInfo
                    //   5) DigitalSignatures, BankAccounts, ClientNotes, ClientFiles
                    //   6) сам ClientInfo

                    var clientInvoiceIds = db.Invoices
                        .Where(i => i.ClientInfoId == clientId)
                        .Select(i => i.Id)
                        .ToList();

                    if (clientInvoiceIds.Count > 0)
                    {
                        db.InvoiceItems.RemoveRange(db.InvoiceItems.Where(it => clientInvoiceIds.Contains(it.InvoiceId)));
                        db.InvoiceDocuments.RemoveRange(db.InvoiceDocuments.Where(d => clientInvoiceIds.Contains(d.InvoiceId)));
                    }

                    db.InvoiceDocuments.RemoveRange(db.InvoiceDocuments.Where(d => d.ClientInfoId == clientId));
                    db.Invoices.RemoveRange(db.Invoices.Where(i => i.ClientInfoId == clientId));
                    db.ClientContracts.RemoveRange(db.ClientContracts.Where(c => c.ClientInfoId == clientId));
                    db.ClientRecurringServices.RemoveRange(db.ClientRecurringServices.Where(r => r.ClientInfoId == clientId));

                    db.DigitalSignatures.RemoveRange(db.DigitalSignatures.Where(s => s.ClientInfoId == clientId));
                    db.BankAccounts.RemoveRange(db.BankAccounts.Where(a => a.ClientInfoId == clientId));
                    db.ClientNotes.RemoveRange(db.ClientNotes.Where(n => n.ClientInfoId == clientId));
                    db.ClientFiles.RemoveRange(clientFiles);

                    db.Clients.Remove(clientFromDb);
                    db.SaveChanges();
                }
                ClientFileStorageService.DeleteClientFolderIfEmpty(selectedClient);
                LoadClientsFromDatabase();
                StatusTextBlock.Text = $"Клиент «{deletedClientName}» удален.";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ClientsPage.DeleteSelectedClientButton_Click", ex);
                StatusTextBlock.Text = $"Ошибка удаления клиента: {ex.Message}";
            }
        }

        private void AddNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) { StatusTextBlock.Text = "Сначала выбери клиента."; return; }
            string noteText = NoteTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(noteText)) { StatusTextBlock.Text = "Введи текст заметки."; return; }
            DateTime? reminderDate = null;
            if (NoteReminderCheckBox.IsChecked == true && NoteReminderDatePicker != null) reminderDate = NoteReminderDatePicker.Date.Date;
            var note = new ClientNote { ClientInfoId = selectedClient.Id, NoteText = noteText, CreatedAt = DateTime.Now, ReminderDate = reminderDate };
            using (var db = new AppDbContext()) { db.ClientNotes.Add(note); db.SaveChanges(); }
            LoadClientsFromDatabase(selectedClient.Id);
            ClearNoteInputFields();
            StatusTextBlock.Text = $"Заметка добавлена клиенту «{selectedClient.Name}».";
        }

        private void DeleteSelectedNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (NotesListView.SelectedItem is not ClientNote selectedNote) { StatusTextBlock.Text = "Сначала выбери заметку для удаления."; return; }
            int? selectedClientId = ClientsListView.SelectedItem is ClientInfo sc ? sc.Id : (int?)null;
            using (var db = new AppDbContext())
            {
                var noteFromDb = db.ClientNotes.FirstOrDefault(n => n.Id == selectedNote.Id);
                if (noteFromDb == null) { StatusTextBlock.Text = "Не удалось найти заметку в базе данных."; return; }
                db.ClientNotes.Remove(noteFromDb);
                db.SaveChanges();
            }
            LoadClientsFromDatabase(selectedClientId);
            StatusTextBlock.Text = "Выбранная заметка удалена.";
        }

        private void AddBankAccountButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) { StatusTextBlock.Text = "Сначала выбери клиента."; return; }
            string bankName = BankNameTextBox.Text.Trim();
            string accountNumber = BankAccountNumberTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(bankName) || string.IsNullOrWhiteSpace(accountNumber)) { StatusTextBlock.Text = "Укажи банк и расчетный счет."; return; }
            var bankAccount = new BankAccount
            {
                ClientInfoId = selectedClient.Id,
                BankName = bankName,
                BIC = BankBicTextBox.Text.Trim(),
                CorrespondentAccount = BankCorrespondentAccountTextBox.Text.Trim(),
                AccountNumber = accountNumber,
                Comment = BankCommentTextBox.Text.Trim()
            };
            using (var db = new AppDbContext()) { db.BankAccounts.Add(bankAccount); db.SaveChanges(); }
            LoadClientsFromDatabase(selectedClient.Id);
            ClearBankAccountInputFields();
            StatusTextBlock.Text = $"Банковский счет добавлен клиенту «{selectedClient.Name}».";
        }

        private void DeleteSelectedBankAccountButton_Click(object sender, RoutedEventArgs e)
        {
            if (AccountsListView.SelectedItem is not BankAccount selectedAccount) { StatusTextBlock.Text = "Сначала выбери счет для удаления."; return; }
            int? selectedClientId = ClientsListView.SelectedItem is ClientInfo sc ? sc.Id : (int?)null;
            using (var db = new AppDbContext())
            {
                var accountFromDb = db.BankAccounts.FirstOrDefault(a => a.Id == selectedAccount.Id);
                if (accountFromDb == null) { StatusTextBlock.Text = "Не удалось найти счет в базе данных."; return; }
                db.BankAccounts.Remove(accountFromDb);
                db.SaveChanges();
            }
            LoadClientsFromDatabase(selectedClientId);
            StatusTextBlock.Text = "Выбранный счет удален.";
        }

        private void AddSignatureButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) { StatusTextBlock.Text = "Сначала выбери клиента."; return; }
            string authority = SignatureAuthorityTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(authority)) { StatusTextBlock.Text = "Укажи удостоверяющий центр."; return; }
            DateTime issuedDate = SignatureIssuedDatePicker.Date.DateTime;
            DateTime expiresDate = SignatureExpiresDatePicker.Date.DateTime;
            if (expiresDate < issuedDate) { StatusTextBlock.Text = "Дата окончания ЭЦП не может быть раньше даты получения."; return; }
            var signature = new DigitalSignature { ClientInfoId = selectedClient.Id, CertificationAuthority = authority, Comment = SignatureCommentTextBox.Text.Trim(), IssuedDate = issuedDate, ExpiresDate = expiresDate };
            using (var db = new AppDbContext()) { db.DigitalSignatures.Add(signature); db.SaveChanges(); }
            LoadClientsFromDatabase(selectedClient.Id);
            ClearSignatureInputFields();
            StatusTextBlock.Text = $"ЭЦП добавлена клиенту «{selectedClient.Name}».";
        }

        private void DeleteSelectedSignatureButton_Click(object sender, RoutedEventArgs e)
        {
            if (SignaturesListView.SelectedItem is not DigitalSignature selectedSignature) { StatusTextBlock.Text = "Сначала выбери ЭЦП для удаления."; return; }
            int? selectedClientId = ClientsListView.SelectedItem is ClientInfo sc ? sc.Id : (int?)null;
            using (var db = new AppDbContext())
            {
                var signatureFromDb = db.DigitalSignatures.FirstOrDefault(s => s.Id == selectedSignature.Id);
                if (signatureFromDb == null) { StatusTextBlock.Text = "Не удалось найти ЭЦП в базе данных."; return; }
                db.DigitalSignatures.Remove(signatureFromDb);
                db.SaveChanges();
            }
            LoadClientsFromDatabase(selectedClientId);
            StatusTextBlock.Text = "Выбранная ЭЦП удалена.";
        }

        private void ClearFormButton_Click(object sender, RoutedEventArgs e)
        {
            ClientsListView.SelectedItem = null;
            ClearInputFields(); ClearClientCard(); ClearSignatureSection(); ClearBankAccountsSection(); ClearNotesSection();
            FormModeTextBlock.Text = "Режим: добавление нового клиента";
            StatusTextBlock.Text = "Форма очищена.";
        }

        private void ClientsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClientsListView.SelectedItem is ClientInfo client)
            {
                ShowClientCard(client);
                LoadClientIntoForm(client);
                LoadSignaturesForSelectedClient();
                LoadBankAccountsForSelectedClient();
                LoadNotesForSelectedClient();
                LoadFilesForSelectedClient();
                FormModeTextBlock.Text = $"Режим: редактирование клиента «{client.Name}»";
                RefreshSelectedClientHighlight();

                // ★ Активируем кнопку быстрого счёта
                if (QuickNewInvoiceButton != null) QuickNewInvoiceButton.IsEnabled = true;
            }
            else
            {
                ResetHighlightedClientCard();
                ClearClientCard(); ClearSignatureSection(); ClearBankAccountsSection(); ClearNotesSection(); ClearFilesSection();
                FormModeTextBlock.Text = "Режим: добавление нового клиента";

                // ★ Деактивируем кнопку если клиент не выбран
                if (QuickNewInvoiceButton != null) QuickNewInvoiceButton.IsEnabled = false;
            }
            UpdateFileActionButtonsState();
            if (_workspaceStateReady) SaveWorkspaceState();
        }

        private void LoadClientIntoForm(ClientInfo client)
        {
            SetClientTypeComboBox(client.ClientType);
            SetClientStatusComboBox(client.Status);
            ClientNameTextBox.Text = client.Name;
            DirectorTextBox.Text = client.DirectorFullName;
            InnTextBox.Text = client.Inn;
            OgrnTextBox.Text = client.Ogrn;
            AddressTextBox.Text = client.Address;
        }

        private void ClearInputFields()
        {
            ClientTypeComboBox.SelectedIndex = 0; ClientStatusComboBox.SelectedIndex = 0;
            ClientNameTextBox.Text = ""; DirectorTextBox.Text = ""; InnTextBox.Text = "";
            OgrnTextBox.Text = ""; AddressTextBox.Text = "";
            UpdateClientFormLabels(); ValidateClientForm();
        }

        private void ClearSignatureInputFields()
        {
            SignatureAuthorityTextBox.Text = ""; SignatureCommentTextBox.Text = "";
            SignatureIssuedDatePicker.Date = new DateTimeOffset(DateTime.Now);
            SignatureExpiresDatePicker.Date = new DateTimeOffset(DateTime.Now.AddYears(1));
        }

        private void ClearBankAccountInputFields()
        {
            BankNameTextBox.Text = ""; BankBicTextBox.Text = "";
            BankCorrespondentAccountTextBox.Text = ""; BankAccountNumberTextBox.Text = ""; BankCommentTextBox.Text = "";
        }

        private void NoteReminderCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (NoteReminderDatePicker == null) return;
            bool isChecked = NoteReminderCheckBox.IsChecked == true;
            NoteReminderDatePicker.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
            if (isChecked) NoteReminderDatePicker.Date = DateTimeOffset.Now;
        }

        private void ClearNoteInputFields()
        {
            NoteTextBox.Text = ""; NoteReminderCheckBox.IsChecked = false;
            NoteReminderDatePicker.Visibility = Visibility.Collapsed;
        }

        private void ShowClientCard(ClientInfo client)
        {
            UpdateClientHeader(client);
            SelectedTypeTextBlock.Text = client.ClientType;
            SelectedStatusTextBlock.Text = string.IsNullOrWhiteSpace(client.Status) ? "Активный" : client.Status;
            SelectedNameTextBlock.Text = client.Name;
            SelectedDirectorTextBlock.Text = string.IsNullOrWhiteSpace(client.DirectorFullName) ? "—" : client.DirectorFullName;
            SelectedInnTextBlock.Text = client.Inn;
            SelectedOgrnTextBlock.Text = string.IsNullOrWhiteSpace(client.Ogrn) ? "—" : client.Ogrn;
            SelectedAddressTextBlock.Text = client.Address;
            SelectedSignatureCountTextBlock.Text = client.SignatureCount.ToString();
            SelectedNearestSignatureTextBlock.Text = client.NearestSignatureExpiresDate.HasValue ? client.NearestSignatureExpiresDate.Value.ToString("dd.MM.yyyy") : "—";
            SelectedAccountCountTextBlock.Text = client.AccountCount.ToString();
            SelectedPrimaryBankTextBlock.Text = client.PrimaryBankName;
            SelectedNoteCountTextBlock.Text = client.NoteCount.ToString();
            SelectedLatestNoteTextBlock.Text = client.LatestNoteCreatedAt.HasValue ? client.LatestNoteCreatedAt.Value.ToString("dd.MM.yyyy HH:mm") : "—";
        }

        private void UpdateSignatureSummary()
        {
            SelectedSignatureCountTextBlock.Text = _signatures.Count.ToString();
            if (_signatures.Count == 0) { SelectedNearestSignatureTextBlock.Text = "—"; return; }
            SelectedNearestSignatureTextBlock.Text = _signatures.OrderBy(s => s.ExpiresDate).First().ExpiresDate.ToString("dd.MM.yyyy");
        }

        private void UpdateBankAccountSummary()
        {
            SelectedAccountCountTextBlock.Text = _bankAccounts.Count.ToString();
            SelectedPrimaryBankTextBlock.Text = _bankAccounts.Count == 0 ? "—" : _bankAccounts.First().BankName;
        }

        private void UpdateNoteSummary()
        {
            SelectedNoteCountTextBlock.Text = _notes.Count.ToString();
            if (_notes.Count == 0) { SelectedLatestNoteTextBlock.Text = "—"; return; }
            SelectedLatestNoteTextBlock.Text = _notes.OrderByDescending(n => n.CreatedAt).First().CreatedAt.ToString("dd.MM.yyyy HH:mm");
        }

        private void ClearClientCard()
        {
            UpdateClientHeader(null);
            SelectedTypeTextBlock.Text = "—"; SelectedStatusTextBlock.Text = "—"; SelectedNameTextBlock.Text = "—";
            SelectedDirectorTextBlock.Text = "—"; SelectedInnTextBlock.Text = "—"; SelectedOgrnTextBlock.Text = "—";
            SelectedAddressTextBlock.Text = "—"; SelectedSignatureCountTextBlock.Text = "—";
            SelectedNearestSignatureTextBlock.Text = "—"; SelectedAccountCountTextBlock.Text = "—";
            SelectedPrimaryBankTextBlock.Text = "—"; SelectedNoteCountTextBlock.Text = "—"; SelectedLatestNoteTextBlock.Text = "—";
        }

        private void ClearSignatureSection()
        {
            _signatures.Clear(); ClearSignatureInputFields();
            SelectedSignatureCountTextBlock.Text = "—"; SelectedNearestSignatureTextBlock.Text = "—";
        }

        private void ClearBankAccountsSection()
        {
            _bankAccounts.Clear(); ClearBankAccountInputFields();
            SelectedAccountCountTextBlock.Text = "—"; SelectedPrimaryBankTextBlock.Text = "—";
        }

        private void ClearNotesSection()
        {
            _notes.Clear(); ClearNoteInputFields();
            SelectedNoteCountTextBlock.Text = "—"; SelectedLatestNoteTextBlock.Text = "—";
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_workspaceStateReady) return;
            if (SearchTextBox == null || ClientsListView == null) return;
            int? selectedClientId = ClientsListView.SelectedItem is ClientInfo sc ? sc.Id : (int?)null;
            SaveWorkspaceState();
            LoadClientsFromDatabase(selectedClientId);
        }

        private async void DownloadEgrulExtractButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) { StatusTextBlock.Text = "Сначала выбери клиента."; return; }
            try
            {
                StatusTextBlock.Text = "Запрашиваю выписку ФНС...";
                var result = await EgrulExtractService.DownloadExtractAsync(selectedClient, headless: false);
                var copyResult = ClientFileStorageService.CopyFileForClient(selectedClient, result.TempPdfPath);
                var clientFile = new ClientFile
                {
                    ClientInfoId = selectedClient.Id,
                    OriginalFileName = result.SuggestedFileName,
                    RelativePath = copyResult.RelativePath,
                    FileSizeBytes = copyResult.FileSizeBytes,
                    AddedAt = DateTime.Now,
                    Category = "Выписка"
                };
                using (var db = new AppDbContext()) { db.ClientFiles.Add(clientFile); db.SaveChanges(); }
                LoadFilesForSelectedClient();
                StatusTextBlock.Text = $"Выписка ФНС получена и добавлена в файлы клиента «{selectedClient.Name}».";
            }
            catch (Exception ex) { StatusTextBlock.Text = $"Ошибка получения выписки ФНС: {ex.Message}"; }
        }

        private void ExportClientCardToWordButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsListView.SelectedItem is not ClientInfo selectedClient) { StatusTextBlock.Text = "Сначала выбери клиента."; return; }
            try
            {
                string exportPath = ExportService.ExportClientCardToWord(selectedClient.Id);
                StatusTextBlock.Text = $"Карточка клиента выгружена в Word: {exportPath}";
                string? folderPath = Path.GetDirectoryName(exportPath);
                if (!string.IsNullOrWhiteSpace(folderPath))
                    Process.Start(new ProcessStartInfo { FileName = folderPath, UseShellExecute = true });
            }
            catch (Exception ex) { StatusTextBlock.Text = $"Ошибка экспорта в Word: {ex.Message}"; }
        }
    }
}