using Microsoft.EntityFrameworkCore;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Reflection;
using ClientAccountApp.Services;



namespace ClientAccountApp
{
    public sealed partial class BillingPage : Page
    {
        private sealed class BillingListItem
        {
            public int InvoiceId { get; set; }
            public int ClientInfoId { get; set; }

            public string InvoiceNumber { get; set; } = "";
            public string ClientName { get; set; } = "";
            public string InvoiceDateText { get; set; } = "";
            public string Status { get; set; } = "";
            public string SourceType { get; set; } = "";
            public string PeriodText { get; set; } = "";
            public string TotalText { get; set; } = "";
            public string DocumentStateText { get; set; } = "";

            public bool HasDocument { get; set; }
            public bool CanMarkPaid { get; set; }

            public Brush StatusBackgroundBrush { get; set; } =
                new SolidColorBrush(ColorHelper.FromArgb(255, 47, 79, 111));

            public Brush StatusForegroundBrush { get; set; } =
                new SolidColorBrush(ColorHelper.FromArgb(255, 255, 255, 255));
        }

        private readonly ObservableCollection<BillingListItem> _invoices = new();
        private readonly ObservableCollection<ClientInfo> _clients = new();
        private readonly ObservableCollection<InvoiceItem> _invoiceItems = new();
        private readonly ObservableCollection<ServiceCatalog> _servicesCatalog = new();
        private int? _selectedServiceCatalogId;
        private readonly ObservableCollection<InvoiceDocumentListItem> _invoiceDocuments = new();

        private bool _pageReady = false;
        private int? _selectedInvoiceId;
        private int? _selectedInvoiceItemId;
        private bool _isInvoicesCompactView = false;
        private bool _syncingInvoiceSelection = false;


        public BillingPage()
        {
            BillingSchemaService.EnsureBillingTables();
            InvoiceDocumentSchemaService.EnsureInvoiceDocumentTables();
            this.InitializeComponent();
            InvoiceDocumentsListView.ItemsSource = _invoiceDocuments;
            InvoicesListView.ItemsSource = _invoices;
            InvoicesCompactListView.ItemsSource = _invoices;
            InvoiceClientComboBox.ItemsSource = _clients;
            InvoiceItemsListView.ItemsSource = _invoiceItems;
            InvoiceServiceCatalogComboBox.ItemsSource = _servicesCatalog;
            ServicesCatalogListView.ItemsSource = _servicesCatalog;

            Loaded += BillingPage_Loaded;
        }
        private int GetRequiredActiveOrganizationId()
        {
            var organization = ActiveOrganizationService.GetRequired();
            return organization.Id;
        }
        private static string FormatMoney(decimal value)
        {
            return value.ToString("N2", new CultureInfo("ru-RU")) + " ₽";
        }
        private void OpenInvoiceListItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element ||
                element.DataContext is not BillingListItem item)
            {
                return;
            }

            InvoicesListView.SelectedItem = item;
            LoadInvoiceIntoForm(item.InvoiceId);
        }
        private void AttachLegacyInvoicesToCurrentOrganization()
        {
            int organizationId = GetRequiredActiveOrganizationId();

            using var db = new AppDbContext();

            var legacyInvoices = db.Invoices
                .Where(x => !x.OrganizationProfileId.HasValue)
                .ToList();

            if (legacyInvoices.Count == 0)
                return;

            foreach (var invoice in legacyInvoices)
            {
                invoice.OrganizationProfileId = organizationId;
                invoice.UpdatedAt = DateTime.Now;
            }

            db.SaveChanges();
        }
        private void LoadInvoiceDocuments(int? invoiceId)
        {
            _invoiceDocuments.Clear();

            if (OpenInvoiceDocumentButton != null)
                OpenInvoiceDocumentButton.IsEnabled = false;

            if (!invoiceId.HasValue)
                return;

            InvoiceDocumentSchemaService.EnsureInvoiceDocumentTables();

            using var db = new AppDbContext();

            var documents = db.InvoiceDocuments
                .AsNoTracking()
                .Where(x => x.InvoiceId == invoiceId.Value)
                .OrderByDescending(x => x.UpdatedAt)
                .ToList();

            foreach (var document in documents)
            {
                string fullPath = ClientFileStorageService.GetFullPath(document.RelativePath);
                bool exists = File.Exists(fullPath);

                _invoiceDocuments.Add(new InvoiceDocumentListItem
                {
                    Id = document.Id,
                    Title = $"{document.DocumentType} · {document.DocumentFormat}",
                    MetaText = $"{document.OriginalFileName} · {document.UpdatedAt:dd.MM.yyyy HH:mm}",
                    RelativePath = document.RelativePath,
                    CanOpen = exists,
                    StatusText = exists ? "Файл найден" : "Файл не найден"
                });
            }
        }

        private void InvoiceDocumentsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OpenInvoiceDocumentButton.IsEnabled =
                InvoiceDocumentsListView.SelectedItem is InvoiceDocumentListItem item &&
                item.CanOpen;
        }

        private void OpenInvoiceDocumentButton_Click(object sender, RoutedEventArgs e)
        {
            if (InvoiceDocumentsListView.SelectedItem is not InvoiceDocumentListItem item)
            {
                BillingStatusTextBlock.Text = "Сначала выберите документ счёта.";
                return;
            }

            string fullPath = ClientFileStorageService.GetFullPath(item.RelativePath);

            if (!File.Exists(fullPath))
            {
                BillingStatusTextBlock.Text = "Файл документа не найден.";
                return;
            }

            ClientFileStorageService.OpenFile(fullPath);
            BillingStatusTextBlock.Text = $"Открыт документ: {item.Title}.";
        }
        private void GenerateInvoiceWordListItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element ||
                element.DataContext is not BillingListItem item)
            {
                return;
            }

            InvoicesListView.SelectedItem = item;
            LoadInvoiceIntoForm(item.InvoiceId);

            GenerateInvoiceWordButton_Click(sender, e);
        }

        private void OpenInvoiceDocumentListItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element ||
                element.DataContext is not BillingListItem item)
            {
                return;
            }

            InvoicesListView.SelectedItem = item;
            LoadInvoiceIntoForm(item.InvoiceId);

            OpenInvoiceWordButton_Click(sender, e);
        }

        private void MarkInvoicePaidListItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element ||
                element.DataContext is not BillingListItem item)
            {
                return;
            }

            using var db = new AppDbContext();

            var invoice = db.Invoices.FirstOrDefault(x => x.Id == item.InvoiceId);
            if (invoice == null)
            {
                BillingStatusTextBlock.Text = "Не удалось найти счет в базе данных.";
                return;
            }

            invoice.Status = InvoiceStatusNames.Paid;
            invoice.UpdatedAt = DateTime.Now;

            db.SaveChanges();

            LoadInvoices(invoice.Id);
            LoadInvoiceIntoForm(invoice.Id);

            BillingStatusTextBlock.Text = $"Счет {invoice.InvoiceNumber} отмечен как оплаченный.";
        }
        private static Brush GetInvoiceStatusBrush(string status)
        {
            return status switch
            {
                "Черновик" => new SolidColorBrush(ColorHelper.FromArgb(255, 47, 79, 111)),
                "Выставлен" => new SolidColorBrush(ColorHelper.FromArgb(255, 140, 110, 40)),
                "Оплачен" => new SolidColorBrush(ColorHelper.FromArgb(255, 58, 110, 72)),
                "Отменен" => new SolidColorBrush(ColorHelper.FromArgb(255, 100, 55, 55)),
                "Отменён" => new SolidColorBrush(ColorHelper.FromArgb(255, 100, 55, 55)),
                _ => new SolidColorBrush(ColorHelper.FromArgb(255, 80, 80, 80))
            };
        }
        private void UpdateInvoiceDocumentActionsState()
        {
            bool hasInvoice = _selectedInvoiceId.HasValue;

            GenerateInvoiceWordButton.IsEnabled = hasInvoice;
            OpenInvoiceWordButton.IsEnabled = false;

            if (!hasInvoice)
            {
                InvoiceQuickSummaryTextBlock.Text = "Черновик счета еще не создан.";
                return;
            }

            using var db = new AppDbContext();

            var invoice = db.Invoices
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == _selectedInvoiceId.Value);

            if (invoice == null)
            {
                InvoiceQuickSummaryTextBlock.Text = "Счет не найден.";
                return;
            }

            var clientName = db.Clients
                .AsNoTracking()
                .Where(c => c.Id == invoice.ClientInfoId)
                .Select(c => string.IsNullOrWhiteSpace(c.Name) ? "Клиент без названия" : c.Name)
                .FirstOrDefault() ?? "Клиент";

            bool hasDocument = false;

            if (!string.IsNullOrWhiteSpace(invoice.DocumentRelativePath))
            {
                string fullPath = ClientFileStorageService.GetFullPath(invoice.DocumentRelativePath);
                hasDocument = File.Exists(fullPath);
            }

            OpenInvoiceWordButton.IsEnabled = hasDocument;

            InvoiceQuickSummaryTextBlock.Text =
                $"{invoice.InvoiceNumber} • {clientName} • {invoice.TotalWithVat:N2} ₽ • {invoice.Status}" +
                (hasDocument ? " • Документ сформирован" : " • Документ не сформирован");
        }

        private void GenerateInvoiceWordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedInvoiceId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала сохрани счет.";
                return;
            }

            try
            {
                using var db = new AppDbContext();

                var invoice = db.Invoices.FirstOrDefault(x => x.Id == _selectedInvoiceId.Value);
                if (invoice == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти счет в базе данных.";
                    return;
                }

                var client = db.Clients.FirstOrDefault(x => x.Id == invoice.ClientInfoId);
                if (client == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти клиента счета.";
                    return;
                }

                var itemCount = db.InvoiceItems.Count(x => x.InvoiceId == invoice.Id);
                if (itemCount == 0)
                {
                    BillingStatusTextBlock.Text = "Сначала добавь хотя бы одну строку услуги.";
                    return;
                }

                string tempPath = InvoiceWordService.GenerateInvoiceDocx(invoice.Id);

                var copyResult = ClientFileStorageService.CopyFileForClient(client, tempPath);

                string invoiceRelativePath = Convert.ToString(copyResult.RelativePath) ?? "";
                long invoiceFileSizeBytes = Convert.ToInt64(copyResult.FileSizeBytes);
                int clientId = client.Id;

                invoice.DocumentRelativePath = invoiceRelativePath;
                invoice.UpdatedAt = DateTime.Now;

                bool fileEntryExists = db.ClientFiles.Any(f =>
                    f.ClientInfoId == clientId &&
                    f.RelativePath == invoiceRelativePath);

                if (!fileEntryExists)
                {
                    db.ClientFiles.Add(new ClientFile
                    {
                        ClientInfoId = clientId,
                        OriginalFileName = Path.GetFileName(tempPath),
                        RelativePath = invoiceRelativePath,
                        FileSizeBytes = invoiceFileSizeBytes,
                        AddedAt = DateTime.Now,
                        Category = ClientFileCategoryNames.Invoice
                    });
                }

                db.SaveChanges();

                string fullPath = ClientFileStorageService.GetFullPath(invoiceRelativePath);

                LoadInvoices(invoice.Id);
                LoadInvoiceIntoForm(invoice.Id);

                BillingStatusTextBlock.Text = $"Word-счет {invoice.InvoiceNumber} сформирован, сохранен в файлы клиента и открыт.";

                if (File.Exists(fullPath))
                {
                    ClientFileStorageService.OpenFile(fullPath);
                }
                else
                {
                    BillingStatusTextBlock.Text = "Word-счет сформирован, но файл не найден на диске для открытия.";
                }

                ClientFileStorageService.OpenFile(fullPath);

                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                    // временный файл не критичен
                }
            }
            catch (Exception ex)
            {
                BillingStatusTextBlock.Text = $"Ошибка формирования Word-счета: {ex.Message}";
            }
        }
        private void LoadOrganizationSettingsIntoForm()
        {
            var settings = OrganizationSettingsService.Load();

            OrganizationNameTextBox.Text = settings.Name;
            OrganizationInnTextBox.Text = settings.Inn;
            OrganizationKppTextBox.Text = settings.Kpp;
            OrganizationAddressTextBox.Text = settings.Address;

            OrganizationBankNameTextBox.Text = settings.BankName;
            OrganizationBankAccountTextBox.Text = settings.BankAccount;
            OrganizationBikTextBox.Text = settings.Bik;
            OrganizationCorrespondentAccountTextBox.Text = settings.CorrespondentAccount;

            OrganizationDirectorTextBox.Text = settings.Director;
            OrganizationEmailTextBox.Text = settings.Email;
            OrganizationPhoneTextBox.Text = settings.Phone;
        }

        private void SaveOrganizationSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = new OrganizationSettings
            {
                Name = OrganizationNameTextBox.Text.Trim(),
                Inn = OrganizationInnTextBox.Text.Trim(),
                Kpp = OrganizationKppTextBox.Text.Trim(),
                Address = OrganizationAddressTextBox.Text.Trim(),

                BankName = OrganizationBankNameTextBox.Text.Trim(),
                BankAccount = OrganizationBankAccountTextBox.Text.Trim(),
                Bik = OrganizationBikTextBox.Text.Trim(),
                CorrespondentAccount = OrganizationCorrespondentAccountTextBox.Text.Trim(),

                Director = OrganizationDirectorTextBox.Text.Trim(),
                Email = OrganizationEmailTextBox.Text.Trim(),
                Phone = OrganizationPhoneTextBox.Text.Trim()
            };

            OrganizationSettingsService.Save(settings);

            BillingStatusTextBlock.Text = "Настройки организации сохранены. Они будут использоваться при формировании новых Word-счетов.";
        }
        private void OpenInvoiceWordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedInvoiceId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала открой счет.";
                return;
            }

            try
            {
                using var db = new AppDbContext();

                var invoice = db.Invoices
                    .AsNoTracking()
                    .FirstOrDefault(x => x.Id == _selectedInvoiceId.Value);

                if (invoice == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти счет в базе данных.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(invoice.DocumentRelativePath))
                {
                    BillingStatusTextBlock.Text = "У этого счета еще не сформирован документ.";
                    return;
                }

                string fullPath = ClientFileStorageService.GetFullPath(invoice.DocumentRelativePath);

                if (!File.Exists(fullPath))
                {
                    BillingStatusTextBlock.Text = "Файл счета не найден на диске.";
                    return;
                }

                ClientFileStorageService.OpenFile(fullPath);
                BillingStatusTextBlock.Text = "Документ счета открыт.";
            }
            catch (Exception ex)
            {
                BillingStatusTextBlock.Text = $"Ошибка открытия счета: {ex.Message}";
            }
        }
        private void GenerateInvoiceFacturaPdfButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedInvoiceId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала выберите счёт.";
                return;
            }

            GenerateAndOpenInvoiceFacturaPdfForInvoice(_selectedInvoiceId.Value);
        }

        private void GenerateAndOpenInvoiceFacturaPdfForInvoice(int invoiceId)
        {
            try
            {
                using var db = new AppDbContext();

                var invoice = db.Invoices.FirstOrDefault(x => x.Id == invoiceId);
                if (invoice == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти счёт для формирования счёт-фактуры PDF.";
                    return;
                }

                var client = db.Clients.FirstOrDefault(x => x.Id == invoice.ClientInfoId);
                if (client == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти клиента для формирования счёт-фактуры PDF.";
                    return;
                }

                string tempPath = BillingInvoiceFacturaPdfService.Generate(invoice.Id);

                var copyResult = ClientFileStorageService.CopyFileForClient(client, tempPath);

                string relativePath = Convert.ToString(copyResult.RelativePath) ?? "";
                long fileSizeBytes = Convert.ToInt64(copyResult.FileSizeBytes);
                string fileName = Path.GetFileName(tempPath);

                db.ClientFiles.Add(new ClientFile
                {
                    ClientInfoId = client.Id,
                    OriginalFileName = fileName,
                    RelativePath = relativePath,
                    FileSizeBytes = fileSizeBytes,
                    AddedAt = DateTime.Now,
                    Category = "Счёт-фактура"
                });

                InvoiceDocumentService.RegisterInvoiceDocument(
                    db,
                    invoice,
                    client,
                    "Счёт-фактура",
                    "PDF",
                    fileName,
                    relativePath,
                    fileSizeBytes);

                db.SaveChanges();

                LoadInvoiceDocuments(invoice.Id);

                string finalFullPath = ClientFileStorageService.GetFullPath(relativePath);

                BillingStatusTextBlock.Text =
                    $"Счёт-фактура PDF по счёту {invoice.InvoiceNumber} сформирована.";

                if (File.Exists(finalFullPath))
                    ClientFileStorageService.OpenFile(finalFullPath);
            }
            catch (Exception ex)
            {
                BillingStatusTextBlock.Text = $"Ошибка формирования счёт-фактуры PDF: {ex.Message}";
            }
        }
        private async void GenerateUpdPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await GenerateAndOpenUpdPdfForInvoice();
            }
            catch (Exception ex)
            {
                var details = ex.ToString();

                await ShowMessageAsync(
                    "УПД PDF",
                    "Не удалось сформировать УПД PDF:\n\n" + details);
            }
        }

        private async Task GenerateAndOpenUpdPdfForInvoice()
        {
            if (!_selectedInvoiceId.HasValue)
            {
                await ShowMessageAsync(
                    "УПД PDF",
                    "Сначала выбери счёт в реестре начислений.");

                return;
            }

            string? tempPath = null;

            try
            {
                using var db = new AppDbContext();

                var invoice = db.Invoices.FirstOrDefault(x => x.Id == _selectedInvoiceId.Value);
                if (invoice == null)
                {
                    await ShowMessageAsync(
                        "УПД PDF",
                        "Не удалось найти выбранный счёт в базе данных.");

                    return;
                }

                var client = db.Clients.FirstOrDefault(x => x.Id == invoice.ClientInfoId);
                if (client == null)
                {
                    await ShowMessageAsync(
                        "УПД PDF",
                        "Не удалось найти клиента выбранного счёта.");

                    return;
                }

                var items = db.InvoiceItems
                    .AsNoTracking()
                    .Where(x => x.InvoiceId == invoice.Id)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .ToList();

                if (items.Count == 0)
                {
                    await ShowMessageAsync(
                        "УПД PDF",
                        "Сначала добавь хотя бы одну строку услуги в счёт.");

                    return;
                }

                var organizationSettings = OrganizationSettingsService.Load();

                var data = new BillingUpdPdfData
                {
                    Status = "1",
                    DocumentNumber = invoice.InvoiceNumber,
                    DocumentDate = invoice.InvoiceDate,

                    SellerName = organizationSettings.Name,
                    SellerInn = organizationSettings.Inn,
                    SellerKpp = organizationSettings.Kpp,
                    SellerAddress = organizationSettings.Address,
                    SellerDirector = organizationSettings.Director,
                    SellerAccountant = "",

                    BuyerName = GetStringValue(
    client,
    "Name",
    "OrganizationName",
    "FullName",
    "ShortName"),

                    BuyerInn = GetStringValue(
    client,
    "Inn",
    "INN",
    "TaxNumber"),

                    BuyerKpp = GetStringValue(
    client,
    "Kpp",
    "KPP"),

                    BuyerAddress = GetStringValue(
    client,
    "LegalAddress",
    "Address",
    "RegistrationAddress",
    "ActualAddress",
    "JuridicalAddress"),

                    ShipperName = "-",
                    ConsigneeName = "-",
                    ContractInfo = "Счёт № " + invoice.InvoiceNumber + " от " + invoice.InvoiceDate.ToString("dd.MM.yyyy"),

                    DefaultVatRatePercent = 22m
                };

                foreach (var item in items)
                {
                    data.Lines.Add(new BillingUpdPdfLine
                    {
                        Code = "Услуга",
                        Name = string.IsNullOrWhiteSpace(item.ServiceName) ? "Услуга" : item.ServiceName,
                        ProductTypeCode = "-",
                        UnitCode = "-",
                        UnitName = string.IsNullOrWhiteSpace(item.Unit) ? "усл." : item.Unit,
                        Quantity = item.Quantity <= 0 ? 1 : item.Quantity,
                        PriceWithoutVat = item.UnitPrice,
                        VatRatePercent = item.VatRate,
                        CountryCode = "-",
                        CountryName = "-",
                        CustomsNumber = "-"
                    });
                }

                string safeInvoiceNumber = MakeSafeFileName(invoice.InvoiceNumber);

                tempPath = Path.Combine(
                    Path.GetTempPath(),
                    "УПД_" + safeInvoiceNumber + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pdf");

                var service = new BillingUpdPdfService();
                service.Generate(data, tempPath);

                var copyResult = ClientFileStorageService.CopyFileForClient(client, tempPath);

                string relativePath = Convert.ToString(copyResult.RelativePath) ?? "";
                long fileSizeBytes = Convert.ToInt64(copyResult.FileSizeBytes);
                string fileName = Path.GetFileName(tempPath);

                db.ClientFiles.Add(new ClientFile
                {
                    ClientInfoId = client.Id,
                    OriginalFileName = fileName,
                    RelativePath = relativePath,
                    FileSizeBytes = fileSizeBytes,
                    AddedAt = DateTime.Now,
                    Category = "УПД"
                });

                InvoiceDocumentService.RegisterInvoiceDocument(
                    db,
                    invoice,
                    client,
                    "УПД",
                    "PDF",
                    fileName,
                    relativePath,
                    fileSizeBytes);

                db.SaveChanges();

                LoadInvoices(invoice.Id);
                LoadInvoiceIntoForm(invoice.Id);
                LoadInvoiceDocuments(invoice.Id);

                string finalFullPath = ClientFileStorageService.GetFullPath(relativePath);

                BillingStatusTextBlock.Text =
                    $"УПД PDF по счёту {invoice.InvoiceNumber} сформирован и добавлен в файлы клиента «{client.Name}».";

                if (File.Exists(finalFullPath))
                {
                    ClientFileStorageService.OpenFile(finalFullPath);
                }
                else
                {
                    await ShowMessageAsync(
                        "УПД PDF",
                        "УПД PDF сформирован, но файл не найден на диске для открытия.");
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync(
                    "УПД PDF",
                    "Не удалось сформировать УПД PDF:\n\n" + ex);
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                    // временный файл не критичен
                }
            }
        }
        private (int InvoiceId, int ClientId) TryResolveInvoiceIdentity(int invoiceId, string invoiceNumber)
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    var columns = GetSqliteTableColumns(db, "Invoices");

                    var idColumn = FindColumn(
                        columns,
                        "Id",
                        "InvoiceId",
                        "BillingInvoiceId",
                        "DocumentId");

                    var numberColumn = FindColumn(
                        columns,
                        "InvoiceNumber",
                        "Number",
                        "DocumentNumber",
                        "Title");

                    var clientColumn = FindColumn(
                        columns,
                        "ClientId",
                        "ClientID",
                        "ClientInfoId",
                        "ClientInfoID",
                        "CustomerId",
                        "CustomerID",
                        "CounterpartyId",
                        "CounterpartyID",
                        "BuyerId",
                        "BuyerID");

                    if (string.IsNullOrWhiteSpace(idColumn) || string.IsNullOrWhiteSpace(clientColumn))
                        return (0, 0);

                    var connection = db.Database.GetDbConnection();

                    if (connection.State != System.Data.ConnectionState.Open)
                        connection.Open();

                    if (invoiceId > 0)
                    {
                        var byId = ExecuteInvoiceIdentityQuery(
                            connection,
                            $"SELECT \"{idColumn}\", \"{clientColumn}\" FROM \"Invoices\" WHERE \"{idColumn}\" = @value LIMIT 1;",
                            invoiceId);

                        if (byId.InvoiceId > 0 && byId.ClientId > 0)
                            return byId;
                    }

                    if (!string.IsNullOrWhiteSpace(invoiceNumber) && !string.IsNullOrWhiteSpace(numberColumn))
                    {
                        var byNumber = ExecuteInvoiceIdentityQuery(
                            connection,
                            $"SELECT \"{idColumn}\", \"{clientColumn}\" FROM \"Invoices\" WHERE \"{numberColumn}\" = @value LIMIT 1;",
                            invoiceNumber);

                        if (byNumber.InvoiceId > 0 && byNumber.ClientId > 0)
                            return byNumber;
                    }
                }
            }
            catch
            {
                // Если не получилось определить через базу — вернём нули.
            }

            return (0, 0);
        }
        private static (int InvoiceId, int ClientId) ExecuteInvoiceIdentityQuery(
    System.Data.Common.DbConnection connection,
    string sql,
    object value)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@value";
                parameter.Value = value;
                command.Parameters.Add(parameter);

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return (0, 0);

                    var invoiceIdText = reader.GetValue(0)?.ToString();
                    var clientIdText = reader.GetValue(1)?.ToString();

                    int.TryParse(invoiceIdText, out var invoiceId);
                    int.TryParse(clientIdText, out var clientId);

                    return (invoiceId, clientId);
                }
            }
        }
        private int TryResolveClientIdByInvoice(int invoiceId, string invoiceNumber)
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    var columns = GetSqliteTableColumns(db, "Invoices");

                    var idColumn = FindColumn(
                        columns,
                        "Id",
                        "InvoiceId",
                        "BillingInvoiceId",
                        "DocumentId");

                    var numberColumn = FindColumn(
                        columns,
                        "InvoiceNumber",
                        "Number",
                        "DocumentNumber",
                        "Title");

                    var clientColumn = FindColumn(
                        columns,
                        "ClientId",
                        "ClientID",
                        "ClientInfoId",
                        "ClientInfoID",
                        "CustomerId",
                        "CustomerID",
                        "CounterpartyId",
                        "CounterpartyID",
                        "BuyerId",
                        "BuyerID");

                    if (string.IsNullOrWhiteSpace(clientColumn))
                        return 0;

                    var connection = db.Database.GetDbConnection();

                    if (connection.State != System.Data.ConnectionState.Open)
                        connection.Open();

                    if (invoiceId > 0 && !string.IsNullOrWhiteSpace(idColumn))
                    {
                        var resultById = ExecuteIntScalar(
                            connection,
                            $"SELECT \"{clientColumn}\" FROM \"Invoices\" WHERE \"{idColumn}\" = @value LIMIT 1;",
                            invoiceId);

                        if (resultById > 0)
                            return resultById;
                    }

                    if (!string.IsNullOrWhiteSpace(invoiceNumber) && !string.IsNullOrWhiteSpace(numberColumn))
                    {
                        var resultByNumber = ExecuteIntScalar(
                            connection,
                            $"SELECT \"{clientColumn}\" FROM \"Invoices\" WHERE \"{numberColumn}\" = @value LIMIT 1;",
                            invoiceNumber);

                        if (resultByNumber > 0)
                            return resultByNumber;
                    }
                }
            }
            catch
            {
                // Если не получилось определить клиента через базу — вернём 0.
            }

            return 0;
        }
        private static HashSet<string> GetSqliteTableColumns(AppDbContext db, string tableName)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var connection = db.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var columnName = reader["name"]?.ToString();

                        if (!string.IsNullOrWhiteSpace(columnName))
                            result.Add(columnName);
                    }
                }
            }

            return result;
        }

        private static string FindColumn(HashSet<string> columns, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (columns.Contains(candidate))
                    return candidate;
            }

            return string.Empty;
        }

        private static int ExecuteIntScalar(System.Data.Common.DbConnection connection, string sql, object value)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@value";
                parameter.Value = value;
                command.Parameters.Add(parameter);

                var result = command.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                    return 0;

                if (int.TryParse(result.ToString(), out var intValue))
                    return intValue;

                return 0;
            }
        }
        private async Task ShowMessageAsync(string title, string message)
        {
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "ОК",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }
        private void RegisterUpdPdfInDatabase(int invoiceId, int clientId, string filePath, string fileName)
        {
            using (var db = new AppDbContext())
            {
                if (!RecordExistsById(db, "Invoices", invoiceId))
                    throw new InvalidOperationException("В таблице Invoices не найден счёт с Id = " + invoiceId + ".");

                if (!RecordExistsById(db, "Clients", clientId))
                    throw new InvalidOperationException("В таблице Clients не найден клиент с Id = " + clientId + ".");
                var clientFile = new ClientFile();

                SetPropertyIfExists(clientFile, "ClientId", clientId);
                SetPropertyIfExists(clientFile, "FileName", fileName);
                SetPropertyIfExists(clientFile, "OriginalFileName", fileName);
                SetPropertyIfExists(clientFile, "StoredFileName", fileName);
                SetPropertyIfExists(clientFile, "FilePath", filePath);
                SetPropertyIfExists(clientFile, "Path", filePath);
                SetPropertyIfExists(clientFile, "Category", "УПД");
                SetPropertyIfExists(clientFile, "FileCategory", "УПД");
                SetPropertyIfExists(clientFile, "CreatedAt", DateTime.Now);
                SetPropertyIfExists(clientFile, "UploadedAt", DateTime.Now);

                db.ClientFiles.Add(clientFile);
                db.SaveChanges();

                var clientFileId = GetIntValue(clientFile, "Id", "ClientFileId");

                var invoiceDocument = new InvoiceDocument();

                SetPropertyIfExists(invoiceDocument, "InvoiceId", invoiceId);
                SetPropertyIfExists(invoiceDocument, "ClientId", clientId);
                SetPropertyIfExists(invoiceDocument, "ClientFileId", clientFileId);
                SetPropertyIfExists(invoiceDocument, "DocumentType", "УПД");
                SetPropertyIfExists(invoiceDocument, "DocumentFormat", "PDF");
                SetPropertyIfExists(invoiceDocument, "FileName", fileName);
                SetPropertyIfExists(invoiceDocument, "FilePath", filePath);
                SetPropertyIfExists(invoiceDocument, "Path", filePath);
                SetPropertyIfExists(invoiceDocument, "CreatedAt", DateTime.Now);
                SetPropertyIfExists(invoiceDocument, "GeneratedAt", DateTime.Now);

                db.InvoiceDocuments.Add(invoiceDocument);
                db.SaveChanges();
            }
        }
        private static bool RecordExistsById(AppDbContext db, string tableName, int id)
        {
            if (id <= 0)
                return false;

            var columns = GetSqliteTableColumns(db, tableName);

            var idColumn = FindColumn(
                columns,
                "Id",
                "ClientId",
                "InvoiceId",
                "ClientInfoId");

            if (string.IsNullOrWhiteSpace(idColumn))
                return false;

            var connection = db.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT COUNT(1) FROM \"{tableName}\" WHERE \"{idColumn}\" = @id LIMIT 1;";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@id";
                parameter.Value = id;
                command.Parameters.Add(parameter);

                var result = command.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                    return false;

                return Convert.ToInt32(result) > 0;
            }
        }
        private static void SetPropertyIfExists(object target, string propertyName, object value)
        {
            if (target == null)
                return;

            var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (property == null || !property.CanWrite)
                return;

            if (value == null)
            {
                property.SetValue(target, null);
                return;
            }

            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            try
            {
                if (targetType == typeof(string))
                    property.SetValue(target, value.ToString());
                else if (targetType == typeof(int))
                    property.SetValue(target, Convert.ToInt32(value));
                else if (targetType == typeof(decimal))
                    property.SetValue(target, Convert.ToDecimal(value));
                else if (targetType == typeof(DateTime))
                    property.SetValue(target, Convert.ToDateTime(value));
                else
                    property.SetValue(target, value);
            }
            catch
            {
                // Если свойство есть, но тип не совпал — не ломаем генерацию документа.
            }
        }

        private static int GetIntValue(object source, params string[] propertyNames)
        {
            if (source == null)
                return 0;

            foreach (var propertyName in propertyNames)
            {
                var property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                    continue;

                var value = property.GetValue(source);
                if (value == null)
                    continue;

                if (int.TryParse(value.ToString(), out var result))
                    return result;
            }

            return 0;
        }

        private static string GetStringValue(object source, params string[] propertyNames)
        {
            if (source == null)
                return string.Empty;

            foreach (var propertyName in propertyNames)
            {
                var property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                    continue;

                var value = property.GetValue(source);
                if (value == null)
                    continue;

                var text = value.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return string.Empty;
        }

        private static decimal GetDecimalValue(object source, params string[] propertyNames)
        {
            if (source == null)
                return 0m;

            foreach (var propertyName in propertyNames)
            {
                var property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                    continue;

                var value = property.GetValue(source);
                if (value == null)
                    continue;

                if (decimal.TryParse(value.ToString(), out var result))
                    return result;
            }

            return 0m;
        }

        private static DateTime GetDateValue(object source, params string[] propertyNames)
        {
            if (source == null)
                return DateTime.Today;

            foreach (var propertyName in propertyNames)
            {
                var property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                    continue;

                var value = property.GetValue(source);
                if (value == null)
                    continue;

                if (DateTime.TryParse(value.ToString(), out var result))
                    return result;
            }

            return DateTime.Today;
        }

        private static string MakeSafeFileName(string fileName)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');

            return fileName;
        }
        private void ToggleInvoicesViewButton_Click(object sender, RoutedEventArgs e)
        {
            SetInvoicesListViewMode(!_isInvoicesCompactView);
        }

        private void SetInvoicesListViewMode(bool compact)
        {
            _isInvoicesCompactView = compact;

            InvoicesListView.Visibility = compact
                ? Visibility.Collapsed
                : Visibility.Visible;

            InvoicesCompactListView.Visibility = compact
                ? Visibility.Visible
                : Visibility.Collapsed;
            ToolTipService.SetToolTip(
    ToggleInvoicesViewButton,
    compact ? "Обычный вид списка" : "Компактный вид списка");

            if (_selectedInvoiceId.HasValue)
            {
                var selectedItem = _invoices.FirstOrDefault(x => x.InvoiceId == _selectedInvoiceId.Value);

                if (selectedItem != null)
                {
                    _syncingInvoiceSelection = true;

                    InvoicesListView.SelectedItem = selectedItem;
                    InvoicesCompactListView.SelectedItem = selectedItem;

                    _syncingInvoiceSelection = false;

                    if (compact)
                        InvoicesCompactListView.ScrollIntoView(selectedItem);
                    else
                        InvoicesListView.ScrollIntoView(selectedItem);
                }
            }
        }
        private void UpdateInvoiceActionButtonsState()
        {
            bool hasInvoice = _selectedInvoiceId.HasValue;
            string status = GetSelectedComboBoxText(InvoiceStatusEditorComboBox, InvoiceStatusNames.Draft);

            DuplicateInvoiceButton.IsEnabled = hasInvoice;
            DeleteInvoiceButton.IsEnabled = hasInvoice;

            MarkInvoiceIssuedButton.IsEnabled =
                hasInvoice &&
                !string.Equals(status, InvoiceStatusNames.Issued, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, InvoiceStatusNames.Paid, StringComparison.OrdinalIgnoreCase);

            MarkInvoicePaidButton.IsEnabled =
                hasInvoice &&
                !string.Equals(status, InvoiceStatusNames.Paid, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateInvoiceQuickSummary(string text)
        {
            InvoiceQuickSummaryTextBlock.Text = text;
        }

        private void MarkInvoiceStatus(string newStatus, string successMessage)
        {
            if (!_selectedInvoiceId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала открой счет.";
                return;
            }

            using var db = new AppDbContext();

            var invoice = db.Invoices.FirstOrDefault(x => x.Id == _selectedInvoiceId.Value);
            if (invoice == null)
            {
                BillingStatusTextBlock.Text = "Не удалось найти счет в базе данных.";
                return;
            }

            invoice.Status = newStatus;
            invoice.UpdatedAt = DateTime.Now;

            db.SaveChanges();

            LoadInvoices(invoice.Id);
            LoadInvoiceIntoForm(invoice.Id);

            BillingStatusTextBlock.Text = successMessage;
        }

        private async void MarkInvoiceIssuedButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedInvoiceId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала открой счет, который нужно отметить выставленным.";
                return;
            }

            try
            {
                using var db = new AppDbContext();

                var invoice = db.Invoices.FirstOrDefault(x => x.Id == _selectedInvoiceId.Value);

                if (invoice == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти счет в базе данных.";
                    return;
                }

                invoice.Status = InvoiceStatusNames.Issued;
                invoice.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                _selectedInvoiceId = invoice.Id;

                LoadInvoices(invoice.Id);
                LoadInvoiceIntoForm(invoice.Id);

                BillingStatusTextBlock.Text = $"Счет {invoice.InvoiceNumber} отмечен как выставленный.";

                await ShowPostInvoiceIssuedDialogAsync(invoice.Id);
            }
            catch (Exception ex)
            {
                BillingStatusTextBlock.Text = $"Ошибка отметки счета как выставленного: {ex.Message}";
            }
        }

        private async Task ShowPostInvoiceIssuedDialogAsync(int invoiceId)
        {
            using var db = new AppDbContext();

            var invoice = db.Invoices.FirstOrDefault(x => x.Id == invoiceId);

            if (invoice == null)
            {
                BillingStatusTextBlock.Text = "Счет выставлен, но не удалось найти его для формирования документов.";
                return;
            }

            string vatHint = invoice.VatAmount > 0
                ? "В счёте есть НДС. Можно сформировать УПД XML для загрузки в Saby/СБИС и УПД PDF для печати/просмотра."
                : "В счёте НДС равен 0. Можно сформировать УПД XML и УПД PDF.";

            var dialogContent = new StackPanel
            {
                Spacing = 10
            };

            dialogContent.Children.Add(new TextBlock
            {
                Text = $"Счёт {invoice.InvoiceNumber} отмечен как выставленный.",
                TextWrapping = TextWrapping.Wrap
            });

            dialogContent.Children.Add(new TextBlock
            {
                Text = vatHint,
                TextWrapping = TextWrapping.Wrap
            });

            dialogContent.Children.Add(new TextBlock
            {
                Text = "Что сформировать дальше?",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });

            var dialog = new ContentDialog
            {
                Title = "Счёт выставлен",
                Content = dialogContent,
                PrimaryButtonText = "УПД XML",
                SecondaryButtonText = "УПД PDF",
                CloseButtonText = "Позже",
                XamlRoot = this.XamlRoot
            };

            ContentDialogResult result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                GenerateAndOpenUpdXmlForInvoice(invoice.Id);
                return;
            }

            if (result == ContentDialogResult.Secondary)
            {
                await GenerateAndOpenUpdPdfForInvoice();
                return;
            }

            BillingStatusTextBlock.Text =
                $"Счет {invoice.InvoiceNumber} выставлен. Формирование документов отложено.";
        }
        private void GenerateAndOpenActForInvoice(int invoiceId)
        {
            try
            {
                using var db = new AppDbContext();

                var invoice = db.Invoices.FirstOrDefault(x => x.Id == invoiceId);

                if (invoice == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти счёт для формирования акта.";
                    return;
                }

                var client = db.Clients.FirstOrDefault(x => x.Id == invoice.ClientInfoId);

                if (client == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти клиента для формирования акта.";
                    return;
                }

                string tempActPath = BillingActWordService.GenerateActDocx(invoice.Id);

                var copyResult = ClientFileStorageService.CopyFileForClient(client, tempActPath);

                string relativePath = Convert.ToString(copyResult.RelativePath) ?? "";
                long fileSizeBytes = Convert.ToInt64(copyResult.FileSizeBytes);
                string fileName = Path.GetFileName(tempActPath);

                db.ClientFiles.Add(new ClientFile
                {
                    ClientInfoId = client.Id,
                    OriginalFileName = fileName,
                    RelativePath = relativePath,
                    FileSizeBytes = fileSizeBytes,
                    AddedAt = DateTime.Now,
                    Category = "Акт"
                });
                InvoiceDocumentService.RegisterInvoiceDocument(
    db,
    invoice,
    client,
    "Акт",
    "Word",
    fileName,
    relativePath,
    fileSizeBytes);

                db.SaveChanges();
                LoadInvoiceDocuments(invoice.Id);

                string finalFullPath = ClientFileStorageService.GetFullPath(relativePath);

                BillingStatusTextBlock.Text =
                    $"Акт по счету {invoice.InvoiceNumber} сформирован и добавлен в файлы клиента «{client.Name}».";

                if (File.Exists(finalFullPath))
                {
                    ClientFileStorageService.OpenFile(finalFullPath);
                }

                try
                {
                    if (File.Exists(tempActPath))
                        File.Delete(tempActPath);
                }
                catch
                {
                    // временный файл не критичен
                }
            }
            catch (Exception ex)
            {
                BillingStatusTextBlock.Text = $"Ошибка формирования акта: {ex.Message}";
            }
        }
        private void GenerateAndOpenInvoiceFacturaForInvoice(int invoiceId)
        {
            try
            {
                using var db = new AppDbContext();

                var invoice = db.Invoices.FirstOrDefault(x => x.Id == invoiceId);

                if (invoice == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти счёт для формирования счёт-фактуры.";
                    return;
                }

                var client = db.Clients.FirstOrDefault(x => x.Id == invoice.ClientInfoId);

                if (client == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти клиента для формирования счёт-фактуры.";
                    return;
                }

                string tempPath = BillingInvoiceFacturaWordService.Generate(invoice.Id);

                var copyResult = ClientFileStorageService.CopyFileForClient(client, tempPath);

                string relativePath = Convert.ToString(copyResult.RelativePath) ?? "";
                long fileSizeBytes = Convert.ToInt64(copyResult.FileSizeBytes);
                string fileName = Path.GetFileName(tempPath);

                db.ClientFiles.Add(new ClientFile
                {
                    ClientInfoId = client.Id,
                    OriginalFileName = fileName,
                    RelativePath = relativePath,
                    FileSizeBytes = fileSizeBytes,
                    AddedAt = DateTime.Now,
                    Category = "Счёт-фактура"
                });
                InvoiceDocumentService.RegisterInvoiceDocument(
    db,
    invoice,
    client,
    "Счёт-фактура",
    "Word",
    fileName,
    relativePath,
    fileSizeBytes);
                db.SaveChanges();
                LoadInvoiceDocuments(invoice.Id);

                string finalFullPath = ClientFileStorageService.GetFullPath(relativePath);

                BillingStatusTextBlock.Text =
                    $"Счёт-фактура по счёту {invoice.InvoiceNumber} сформирована и добавлена в файлы клиента «{client.Name}».";

                if (File.Exists(finalFullPath))
                {
                    ClientFileStorageService.OpenFile(finalFullPath);
                }

                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                    // временный файл не критичен
                }
            }
            catch (Exception ex)
            {
                BillingStatusTextBlock.Text = $"Ошибка формирования счёт-фактуры: {ex.Message}";
            }
        }
        private void GenerateAndOpenActXmlForInvoice(int invoiceId)
        {
            try
            {
                using var db = new AppDbContext();

                var invoice = db.Invoices.FirstOrDefault(x => x.Id == invoiceId);
                if (invoice == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти счёт для формирования XML акта.";
                    return;
                }

                var client = db.Clients.FirstOrDefault(x => x.Id == invoice.ClientInfoId);
                if (client == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти клиента для формирования XML акта.";
                    return;
                }

                string tempPath = BillingSellerXmlService.GenerateActSellerXml(invoice.Id);

                var copyResult = ClientFileStorageService.CopyFileForClient(client, tempPath);

                string relativePath = Convert.ToString(copyResult.RelativePath) ?? "";
                long fileSizeBytes = Convert.ToInt64(copyResult.FileSizeBytes);
                string fileName = Path.GetFileName(tempPath);

                db.ClientFiles.Add(new ClientFile
                {
                    ClientInfoId = client.Id,
                    OriginalFileName = fileName,
                    RelativePath = relativePath,
                    FileSizeBytes = fileSizeBytes,
                    AddedAt = DateTime.Now,
                    Category = "Акт XML"
                });

                InvoiceDocumentService.RegisterInvoiceDocument(
                    db,
                    invoice,
                    client,
                    "Акт",
                    "XML",
                    fileName,
                    relativePath,
                    fileSizeBytes);

                db.SaveChanges();

                LoadInvoiceDocuments(invoice.Id);

                string finalFullPath = ClientFileStorageService.GetFullPath(relativePath);

                BillingStatusTextBlock.Text =
                    $"XML акта по счёту {invoice.InvoiceNumber} сформирован и добавлен в файлы клиента «{client.Name}».";

                if (File.Exists(finalFullPath))
                {
                    ClientFileStorageService.OpenFile(finalFullPath);
                }

                TryDeleteTempFile(tempPath);
            }
            catch (Exception ex)
            {
                BillingStatusTextBlock.Text = $"Ошибка формирования XML акта: {ex.Message}";
            }
        }
        private void GenerateActWordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedInvoiceId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала выберите счёт.";
                return;
            }

            GenerateAndOpenActForInvoice(_selectedInvoiceId.Value);
        }

        private void GenerateInvoiceFacturaWordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedInvoiceId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала выберите счёт.";
                return;
            }

            GenerateAndOpenInvoiceFacturaForInvoice(_selectedInvoiceId.Value);
        }

        private void GenerateActXmlButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedInvoiceId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала выберите счёт.";
                return;
            }

            GenerateAndOpenActXmlForInvoice(_selectedInvoiceId.Value);
        }

        private void GenerateInvoiceFacturaXmlButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedInvoiceId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала выберите счёт.";
                return;
            }

            GenerateAndOpenInvoiceFacturaXmlForInvoice(_selectedInvoiceId.Value);
        }
        private void GenerateUpdXmlButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedInvoiceId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала выберите счёт.";
                return;
            }

            GenerateAndOpenUpdXmlForInvoice(_selectedInvoiceId.Value);
        }
        private void GenerateAndOpenInvoiceFacturaXmlForInvoice(int invoiceId)
        {
            try
            {
                using var db = new AppDbContext();

                var invoice = db.Invoices.FirstOrDefault(x => x.Id == invoiceId);
                if (invoice == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти счёт для формирования XML счёт-фактуры.";
                    return;
                }

                var client = db.Clients.FirstOrDefault(x => x.Id == invoice.ClientInfoId);
                if (client == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти клиента для формирования XML счёт-фактуры.";
                    return;
                }

                string tempPath = BillingSellerXmlService.GenerateInvoiceFacturaSellerXml(invoice.Id);

                var copyResult = ClientFileStorageService.CopyFileForClient(client, tempPath);

                string relativePath = Convert.ToString(copyResult.RelativePath) ?? "";
                long fileSizeBytes = Convert.ToInt64(copyResult.FileSizeBytes);
                string fileName = Path.GetFileName(tempPath);

                db.ClientFiles.Add(new ClientFile
                {
                    ClientInfoId = client.Id,
                    OriginalFileName = fileName,
                    RelativePath = relativePath,
                    FileSizeBytes = fileSizeBytes,
                    AddedAt = DateTime.Now,
                    Category = "Счёт-фактура XML"
                });

                InvoiceDocumentService.RegisterInvoiceDocument(
                    db,
                    invoice,
                    client,
                    "Счёт-фактура",
                    "XML",
                    fileName,
                    relativePath,
                    fileSizeBytes);

                db.SaveChanges();

                LoadInvoiceDocuments(invoice.Id);

                string finalFullPath = ClientFileStorageService.GetFullPath(relativePath);

                BillingStatusTextBlock.Text =
                    $"XML счёт-фактуры по счёту {invoice.InvoiceNumber} сформирован и добавлен в файлы клиента «{client.Name}».";

                if (File.Exists(finalFullPath))
                {
                    ClientFileStorageService.OpenFile(finalFullPath);
                }

                TryDeleteTempFile(tempPath);
            }
            catch (Exception ex)
            {
                BillingStatusTextBlock.Text = $"Ошибка формирования XML счёт-фактуры: {ex.Message}";
            }
        }
        private void GenerateAndOpenUpdXmlForInvoice(int invoiceId)
        {
            string tempPath = "";

            try
            {
                using var db = new AppDbContext();

                var invoice = db.Invoices.FirstOrDefault(x => x.Id == invoiceId);
                if (invoice == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти счёт для формирования УПД XML.";
                    return;
                }

                var client = db.Clients.FirstOrDefault(x => x.Id == invoice.ClientInfoId);
                if (client == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти клиента для формирования УПД XML.";
                    return;
                }

                tempPath = BillingSellerXmlService.GenerateUpdSellerXml(invoice.Id);

                var copyResult = ClientFileStorageService.CopyFileForClient(client, tempPath);

                string relativePath = Convert.ToString(copyResult.RelativePath) ?? "";
                long fileSizeBytes = Convert.ToInt64(copyResult.FileSizeBytes);
                string fileName = Path.GetFileName(tempPath);

                db.ClientFiles.Add(new ClientFile
                {
                    ClientInfoId = client.Id,
                    OriginalFileName = fileName,
                    RelativePath = relativePath,
                    FileSizeBytes = fileSizeBytes,
                    AddedAt = DateTime.Now,
                    Category = "УПД XML"
                });

                InvoiceDocumentService.RegisterInvoiceDocument(
                    db,
                    invoice,
                    client,
                    "УПД",
                    "XML",
                    fileName,
                    relativePath,
                    fileSizeBytes);

                db.SaveChanges();

                LoadInvoiceDocuments(invoice.Id);

                string finalFullPath = ClientFileStorageService.GetFullPath(relativePath);

                BillingStatusTextBlock.Text =
                    $"УПД XML по счёту {invoice.InvoiceNumber} сформирован и добавлен в файлы клиента «{client.Name}».";

                if (File.Exists(finalFullPath))
                {
                    ClientFileStorageService.OpenFile(finalFullPath);
                }

                TryDeleteTempFile(tempPath);
            }
            catch (Exception ex)
            {
                BillingStatusTextBlock.Text = $"Ошибка формирования УПД XML: {ex.Message}";
            }
        }
        private static void TryDeleteTempFile(string tempPath)
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // временный файл не критичен
            }
        }
        private void MarkInvoicePaidButton_Click(object sender, RoutedEventArgs e)
        {
            MarkInvoiceStatus(InvoiceStatusNames.Paid, "Счет отмечен как оплаченный.");
        }

        private void DuplicateInvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedInvoiceId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала открой счет, который нужно дублировать.";
                return;
            }

            using var db = new AppDbContext();

            var sourceInvoice = db.Invoices
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == _selectedInvoiceId.Value);

            if (sourceInvoice == null)
            {
                BillingStatusTextBlock.Text = "Не удалось найти исходный счет.";
                return;
            }

            var sourceItems = db.InvoiceItems
                .AsNoTracking()
                .Where(x => x.InvoiceId == sourceInvoice.Id)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToList();

            var newInvoice = new Invoice
            {
                OrganizationProfileId = GetRequiredActiveOrganizationId(),
                ClientInfoId = sourceInvoice.ClientInfoId,
                InvoiceNumber = GenerateNextInvoiceNumber(),
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(5),
                PeriodText = sourceInvoice.PeriodText,
                Status = InvoiceStatusNames.Draft,
                SourceType = sourceInvoice.SourceType,
                Comment = sourceInvoice.Comment,
                TotalWithoutVat = 0m,
                VatAmount = 0m,
                TotalWithVat = 0m,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            db.Invoices.Add(newInvoice);
            db.SaveChanges();

            foreach (var sourceItem in sourceItems)
            {
                db.InvoiceItems.Add(new InvoiceItem
                {
                    InvoiceId = newInvoice.Id,
                    ServiceCatalogId = sourceItem.ServiceCatalogId,
                    ServiceName = sourceItem.ServiceName,
                    Quantity = sourceItem.Quantity,
                    Unit = sourceItem.Unit,
                    UnitPrice = sourceItem.UnitPrice,
                    VatRate = sourceItem.VatRate,
                    AmountWithoutVat = sourceItem.AmountWithoutVat,
                    VatAmount = sourceItem.VatAmount,
                    AmountWithVat = sourceItem.AmountWithVat,
                    SortOrder = sourceItem.SortOrder
                });
            }

            db.SaveChanges();

            RecalculateInvoiceTotalsInDatabase(newInvoice.Id);
            LoadInvoices(newInvoice.Id);
            LoadInvoiceIntoForm(newInvoice.Id);

            BillingStatusTextBlock.Text = $"Создана копия счета {newInvoice.InvoiceNumber}.";
        }
        private void BillingPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadClients();
            LoadServicesCatalog();

            AttachLegacyInvoicesToCurrentOrganization();

            PrepareNewInvoiceForm();
            LoadInvoices();

            _pageReady = true;
        }
        private void LoadServicesCatalog(int? selectServiceId = null)
        {
            _servicesCatalog.Clear();

            using var db = new AppDbContext();

            var services = db.ServicesCatalog
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToList();

            foreach (var service in services)
            {
                _servicesCatalog.Add(service);
            }

            if (selectServiceId.HasValue)
            {
                var selected = _servicesCatalog.FirstOrDefault(x => x.Id == selectServiceId.Value);
                if (selected != null)
                {
                    ServicesCatalogListView.SelectedItem = selected;
                    ServicesCatalogListView.ScrollIntoView(selected);
                }
            }
        }

        private void ClearServiceCatalogEditor()
        {
            _selectedServiceCatalogId = null;
            ServicesCatalogListView.SelectedItem = null;

            ServiceCatalogNameTextBox.Text = "";
            ServiceCatalogPriceTextBox.Text = "0";
            ServiceCatalogUnitTextBox.Text = "усл.";
            ServiceCatalogVatRateTextBox.Text = "0";
        }

        private void ServicesCatalogListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServicesCatalogListView.SelectedItem is not ServiceCatalog service)
                return;

            _selectedServiceCatalogId = service.Id;

            ServiceCatalogNameTextBox.Text = service.Name;
            ServiceCatalogPriceTextBox.Text = service.DefaultPrice.ToString("0.##");
            ServiceCatalogUnitTextBox.Text = string.IsNullOrWhiteSpace(service.Unit) ? "усл." : service.Unit;
            ServiceCatalogVatRateTextBox.Text = service.DefaultVatRate.ToString("0.##");
        }

        private void NewServiceCatalogButton_Click(object sender, RoutedEventArgs e)
        {
            ClearServiceCatalogEditor();
            BillingStatusTextBlock.Text = "Заполни новую типовую услугу и сохрани.";
        }

        private void SaveServiceCatalogButton_Click(object sender, RoutedEventArgs e)
        {
            string name = ServiceCatalogNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                BillingStatusTextBlock.Text = "Укажи название услуги.";
                return;
            }

            if (!TryParseDecimal(ServiceCatalogPriceTextBox.Text, out decimal price) || price < 0)
            {
                BillingStatusTextBlock.Text = "Цена услуги указана неверно.";
                return;
            }

            string unit = string.IsNullOrWhiteSpace(ServiceCatalogUnitTextBox.Text)
                ? "усл."
                : ServiceCatalogUnitTextBox.Text.Trim();

            if (!TryParseDecimal(ServiceCatalogVatRateTextBox.Text, out decimal vatRate) || vatRate < 0)
            {
                BillingStatusTextBlock.Text = "Ставка НДС указана неверно.";
                return;
            }

            using var db = new AppDbContext();

            bool isNew = !_selectedServiceCatalogId.HasValue;
            ServiceCatalog service;

            if (isNew)
            {
                int nextSortOrder = db.ServicesCatalog
                    .Select(x => (int?)x.SortOrder)
                    .Max() ?? 0;

                service = new ServiceCatalog
                {
                    SortOrder = nextSortOrder + 1,
                    CreatedAt = DateTime.Now
                };

                db.ServicesCatalog.Add(service);
            }
            else
            {
                service = db.ServicesCatalog.FirstOrDefault(x => x.Id == _selectedServiceCatalogId.Value);

                if (service == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти услугу в справочнике.";
                    return;
                }
            }

            service.Name = name;
            service.DefaultPrice = price;
            service.Unit = unit;
            service.DefaultVatRate = vatRate;
            service.IsActive = true;

            db.SaveChanges();

            _selectedServiceCatalogId = service.Id;
            LoadServicesCatalog(service.Id);

            BillingStatusTextBlock.Text = isNew
                ? $"Услуга «{service.Name}» добавлена в справочник."
                : $"Услуга «{service.Name}» обновлена.";
        }

        private void DeleteServiceCatalogButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedServiceCatalogId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала выбери услугу из справочника.";
                return;
            }

            using var db = new AppDbContext();

            var service = db.ServicesCatalog.FirstOrDefault(x => x.Id == _selectedServiceCatalogId.Value);
            if (service == null)
            {
                BillingStatusTextBlock.Text = "Не удалось найти услугу в справочнике.";
                return;
            }

            service.IsActive = false;
            db.SaveChanges();

            string deletedName = service.Name;

            ClearServiceCatalogEditor();
            LoadServicesCatalog();

            BillingStatusTextBlock.Text = $"Услуга «{deletedName}» удалена из активного справочника.";
        }

        private void InvoiceServiceCatalogComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InvoiceServiceCatalogComboBox.SelectedItem is not ServiceCatalog service)
                return;

            InvoiceItemNameTextBox.Text = service.Name;
            InvoiceItemUnitTextBox.Text = string.IsNullOrWhiteSpace(service.Unit) ? "усл." : service.Unit;
            InvoiceItemUnitPriceTextBox.Text = service.DefaultPrice.ToString("0.##");
            InvoiceItemVatRateTextBox.Text = service.DefaultVatRate.ToString("0.##");

            BillingStatusTextBlock.Text = $"Подставлена услуга «{service.Name}».";
        }
        private void LoadInvoiceItems(int invoiceId, int? selectItemId = null)
        {
            _invoiceItems.Clear();

            using var db = new AppDbContext();

            var items = db.InvoiceItems
                .AsNoTracking()
                .Where(x => x.InvoiceId == invoiceId)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToList();

            foreach (var item in items)
            {
                _invoiceItems.Add(item);
            }

            UpdateInvoiceTotalsFromItems();

            if (selectItemId.HasValue)
            {
                var itemToSelect = _invoiceItems.FirstOrDefault(x => x.Id == selectItemId.Value);
                if (itemToSelect != null)
                {
                    _syncingInvoiceSelection = true;

                    InvoicesListView.SelectedItem = itemToSelect;
                    InvoicesCompactListView.SelectedItem = itemToSelect;

                    _syncingInvoiceSelection = false;

                    if (_isInvoicesCompactView)
                        InvoicesCompactListView.ScrollIntoView(itemToSelect);
                    else
                        InvoicesListView.ScrollIntoView(itemToSelect);
                }
            }
        }

        private void ClearInvoiceItemsEditor()
        {
            _selectedInvoiceItemId = null;
            InvoiceItemsListView.SelectedItem = null;
            InvoiceServiceCatalogComboBox.SelectedItem = null;

            InvoiceItemNameTextBox.Text = "";
            InvoiceItemQuantityTextBox.Text = "1";
            InvoiceItemUnitTextBox.Text = "усл.";
            InvoiceItemUnitPriceTextBox.Text = "0";
            InvoiceItemVatRateTextBox.Text = "0";
        }

        private void UpdateInvoiceTotalsFromItems()
        {
            decimal totalWithoutVat = _invoiceItems.Sum(x => x.AmountWithoutVat);
            decimal vatAmount = _invoiceItems.Sum(x => x.VatAmount);
            decimal totalWithVat = _invoiceItems.Sum(x => x.AmountWithVat);

            InvoiceTotalWithoutVatTextBlock.Text = $"Без НДС: {FormatMoney(totalWithoutVat)}";
            InvoiceVatAmountTextBlock.Text = $"НДС: {FormatMoney(vatAmount)}";
            InvoiceTotalWithVatTextBlock.Text = $"С НДС: {FormatMoney(totalWithVat)}";
        }

        private void RecalculateInvoiceTotalsInDatabase(int invoiceId)
        {
            using var db = new AppDbContext();

            var invoice = db.Invoices.FirstOrDefault(x => x.Id == invoiceId);
            if (invoice == null)
                return;

            var items = db.InvoiceItems
                .Where(x => x.InvoiceId == invoiceId)
                .ToList();

            invoice.TotalWithoutVat = items.Sum(x => x.AmountWithoutVat);
            invoice.VatAmount = items.Sum(x => x.VatAmount);
            invoice.TotalWithVat = items.Sum(x => x.AmountWithVat);
            invoice.UpdatedAt = DateTime.Now;

            db.SaveChanges();
        }

        private void InvoiceItemsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InvoiceItemsListView.SelectedItem is not InvoiceItem selectedItem)
                return;

            _selectedInvoiceItemId = selectedItem.Id;
            InvoiceServiceCatalogComboBox.SelectedItem =
    selectedItem.ServiceCatalogId.HasValue
        ? _servicesCatalog.FirstOrDefault(x => x.Id == selectedItem.ServiceCatalogId.Value)
        : null;
            InvoiceItemNameTextBox.Text = selectedItem.ServiceName;
            InvoiceItemQuantityTextBox.Text = selectedItem.Quantity.ToString();
            InvoiceItemUnitTextBox.Text = selectedItem.Unit;
            InvoiceItemUnitPriceTextBox.Text = selectedItem.UnitPrice.ToString();
            InvoiceItemVatRateTextBox.Text = selectedItem.VatRate.ToString();
        }

        private void NewInvoiceItemButton_Click(object sender, RoutedEventArgs e)
        {
            ClearInvoiceItemsEditor();
            BillingStatusTextBlock.Text = "Заполни строку услуги и сохрани ее.";
        }
        private bool EnsureInvoiceDraftExists()
        {
            if (_selectedInvoiceId.HasValue)
                return true;

            if (InvoiceClientComboBox.SelectedItem is not ClientInfo selectedClient)
            {
                BillingStatusTextBlock.Text = "Сначала выбери клиента.";
                return false;
            }

            using var db = new AppDbContext();

            var invoice = new Invoice
            {
                OrganizationProfileId = GetRequiredActiveOrganizationId(),
                ClientInfoId = selectedClient.Id,
                InvoiceNumber = string.IsNullOrWhiteSpace(InvoiceNumberTextBox.Text)
                    ? GenerateNextInvoiceNumber()
                    : InvoiceNumberTextBox.Text.Trim(),
                InvoiceDate = InvoiceDatePicker.Date.DateTime,
                DueDate = InvoiceDueDatePicker.Date.DateTime,
                PeriodText = InvoicePeriodTextBox.Text.Trim(),
                Status = GetSelectedComboBoxText(InvoiceStatusEditorComboBox, InvoiceStatusNames.Draft),
                SourceType = InvoiceSourceTypeNames.Manual,
                Comment = InvoiceCommentTextBox.Text.Trim(),
                TotalWithoutVat = 0m,
                VatAmount = 0m,
                TotalWithVat = 0m,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            db.Invoices.Add(invoice);
            db.SaveChanges();

            _selectedInvoiceId = invoice.Id;
            LoadInvoices(invoice.Id);

            BillingStatusTextBlock.Text = $"Черновик счета {invoice.InvoiceNumber} создан автоматически.";
            return true;
        }
        private void SaveInvoiceItemButton_Click(object sender, RoutedEventArgs e)
        {
            string serviceName = InvoiceItemNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                BillingStatusTextBlock.Text = "Укажи название услуги.";
                return;
            }

            if (!TryParseDecimal(InvoiceItemQuantityTextBox.Text, out decimal quantity) || quantity <= 0)
            {
                BillingStatusTextBlock.Text = "Количество должно быть больше нуля.";
                return;
            }

            string unit = string.IsNullOrWhiteSpace(InvoiceItemUnitTextBox.Text)
                ? "усл."
                : InvoiceItemUnitTextBox.Text.Trim();

            if (!TryParseDecimal(InvoiceItemUnitPriceTextBox.Text, out decimal unitPrice) || unitPrice < 0)
            {
                BillingStatusTextBlock.Text = "Цена указана неверно.";
                return;
            }

            if (!TryParseDecimal(InvoiceItemVatRateTextBox.Text, out decimal vatRate) || vatRate < 0)
            {
                BillingStatusTextBlock.Text = "Ставка НДС указана неверно.";
                return;
            }

            if (!EnsureInvoiceDraftExists())
                return;

            using var db = new AppDbContext();

            bool isNew = !_selectedInvoiceItemId.HasValue;
            InvoiceItem item;

            if (isNew)
            {
                int nextSortOrder = db.InvoiceItems
                    .Where(x => x.InvoiceId == _selectedInvoiceId.Value)
                    .Select(x => (int?)x.SortOrder)
                    .Max() ?? 0;

                item = new InvoiceItem
                {
                    InvoiceId = _selectedInvoiceId.Value,
                    ServiceCatalogId = null,
                    SortOrder = nextSortOrder + 1
                };

                db.InvoiceItems.Add(item);
            }
            else
            {
                item = db.InvoiceItems.FirstOrDefault(x => x.Id == _selectedInvoiceItemId.Value);

                if (item == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти строку счета.";
                    return;
                }
            }
            item.ServiceCatalogId = InvoiceServiceCatalogComboBox.SelectedItem is ServiceCatalog selectedService
    ? selectedService.Id
    : null;
            item.ServiceName = serviceName;
            item.Quantity = quantity;
            item.Unit = unit;
            item.UnitPrice = unitPrice;
            item.VatRate = vatRate;

            item.AmountWithoutVat = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
            item.VatAmount = Math.Round(item.AmountWithoutVat * vatRate / 100m, 2, MidpointRounding.AwayFromZero);
            item.AmountWithVat = Math.Round(item.AmountWithoutVat + item.VatAmount, 2, MidpointRounding.AwayFromZero);

            db.SaveChanges();

            RecalculateInvoiceTotalsInDatabase(_selectedInvoiceId.Value);
            LoadInvoiceItems(_selectedInvoiceId.Value, item.Id);
            LoadInvoices(_selectedInvoiceId.Value);

            BillingStatusTextBlock.Text = isNew
                ? "Строка услуги добавлена."
                : "Строка услуги обновлена.";
        }

        private void DeleteInvoiceItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedInvoiceId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала открой счет.";
                return;
            }

            if (!_selectedInvoiceItemId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала выбери строку услуги.";
                return;
            }

            using var db = new AppDbContext();

            var item = db.InvoiceItems.FirstOrDefault(x => x.Id == _selectedInvoiceItemId.Value);
            if (item == null)
            {
                BillingStatusTextBlock.Text = "Не удалось найти строку счета.";
                return;
            }

            db.InvoiceItems.Remove(item);
            db.SaveChanges();

            RecalculateInvoiceTotalsInDatabase(_selectedInvoiceId.Value);
            ClearInvoiceItemsEditor();
            LoadInvoiceItems(_selectedInvoiceId.Value);
            LoadInvoices(_selectedInvoiceId.Value);

            BillingStatusTextBlock.Text = "Строка услуги удалена.";
        }

        private static bool TryParseDecimal(string? text, out decimal value)
        {
            value = 0m;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string normalized = text
                .Trim()
                .Replace(" ", "")
                .Replace(",", ".");

            return decimal.TryParse(
                normalized,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }
        private void LoadClients()
        {
            _clients.Clear();

            using var db = new AppDbContext();
            int organizationId = GetRequiredActiveOrganizationId();

            var clients = db.Clients
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToList();

            foreach (var client in clients)
            {
                _clients.Add(client);
            }
        }

        private void LoadInvoices(int? selectInvoiceId = null)
        {
            _invoices.Clear();

            using var db = new AppDbContext();
            int organizationId = GetRequiredActiveOrganizationId();
            var invoices = db.Invoices
    .AsNoTracking()
    .Where(x => x.OrganizationProfileId == organizationId)
    .OrderByDescending(x => x.InvoiceDate)
    .ThenByDescending(x => x.Id)
    .ToList();

            var clients = db.Clients
                .AsNoTracking()
                .ToList();

            var clientMap = clients.ToDictionary(
                c => c.Id,
                c => string.IsNullOrWhiteSpace(c.Name)
                    ? "Клиент без названия"
                    : c.Name);

            string searchText = BillingSearchTextBox?.Text?.Trim().ToLowerInvariant() ?? "";
            string statusFilter = GetSelectedComboBoxText(BillingStatusFilterComboBox, "Все статусы");
            string sourceFilter = GetSelectedComboBoxText(BillingSourceFilterComboBox, "Все");

            var items = new List<BillingListItem>();

            foreach (var invoice in invoices)
            {
                string status = string.IsNullOrWhiteSpace(invoice.Status)
                    ? InvoiceStatusNames.Draft
                    : invoice.Status;

                string sourceType = string.IsNullOrWhiteSpace(invoice.SourceType)
                    ? InvoiceSourceTypeNames.Manual
                    : invoice.SourceType;

                string clientName = clientMap.TryGetValue(invoice.ClientInfoId, out var mappedClientName)
                    ? mappedClientName
                    : "Клиент удален";

                string periodText = string.IsNullOrWhiteSpace(invoice.PeriodText)
                    ? "Период не указан"
                    : invoice.PeriodText;

                string invoiceNumber = string.IsNullOrWhiteSpace(invoice.InvoiceNumber)
                    ? $"Счет #{invoice.Id}"
                    : invoice.InvoiceNumber;

                bool matchesSearch =
                    string.IsNullOrWhiteSpace(searchText) ||
                    invoiceNumber.ToLowerInvariant().Contains(searchText) ||
                    clientName.ToLowerInvariant().Contains(searchText) ||
                    periodText.ToLowerInvariant().Contains(searchText);

                if (!matchesSearch)
                    continue;

                if (statusFilter != "Все статусы" &&
                    !string.Equals(status, statusFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (sourceFilter != "Все" &&
                    !string.Equals(sourceType, sourceFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool hasDocument = false;

                if (!string.IsNullOrWhiteSpace(invoice.DocumentRelativePath))
                {
                    string fullPath = ClientFileStorageService.GetFullPath(invoice.DocumentRelativePath);
                    hasDocument = File.Exists(fullPath);
                }

                items.Add(new BillingListItem
                {
                    InvoiceId = invoice.Id,
                    ClientInfoId = invoice.ClientInfoId,
                    InvoiceNumber = invoiceNumber,
                    ClientName = clientName,
                    InvoiceDateText = invoice.InvoiceDate.ToString("dd.MM.yyyy"),
                    Status = status,
                    SourceType = sourceType,
                    PeriodText = periodText,
                    TotalText = FormatMoney(invoice.TotalWithVat),
                    HasDocument = hasDocument,
                    DocumentStateText = hasDocument
                        ? "Документ сформирован"
                        : "Документ не сформирован",
                    CanMarkPaid =
                        !string.Equals(status, InvoiceStatusNames.Paid, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(status, InvoiceStatusNames.Cancelled, StringComparison.OrdinalIgnoreCase),
                    StatusBackgroundBrush = GetInvoiceStatusBrush(status)
                });
            }

            foreach (var item in items)
            {
                _invoices.Add(item);
            }

            if (selectInvoiceId.HasValue)
            {
                var itemToSelect = _invoices.FirstOrDefault(x => x.InvoiceId == selectInvoiceId.Value);

                if (itemToSelect != null)
                {
                    InvoicesListView.SelectedItem = itemToSelect;
                    InvoicesListView.ScrollIntoView(itemToSelect);
                }
            }

            if (_invoices.Count == 0)
            {
                BillingStatusTextBlock.Text = invoices.Count == 0
                    ? "Счетов пока нет. Создай первый счет или добавь строку услуги."
                    : "По текущим фильтрам счета не найдены.";
            }
        }

        private void PrepareNewInvoiceForm()
        {
            _selectedInvoiceId = null;

            if (_clients.Count > 0)
            {
                InvoiceClientComboBox.SelectedIndex = 0;
            }
            else
            {
                InvoiceClientComboBox.SelectedItem = null;
            }

            InvoiceNumberTextBox.Text = GenerateNextInvoiceNumber();
            InvoiceDatePicker.Date = DateTimeOffset.Now;
            InvoiceDueDatePicker.Date = DateTimeOffset.Now.AddDays(5);

            if (InvoiceStatusEditorComboBox.Items.Count > 0)
            {
                InvoiceStatusEditorComboBox.SelectedIndex = 0;
            }

            InvoiceSourceTextBlock.Text = InvoiceSourceTypeNames.Manual;
            InvoicePeriodTextBox.Text = "";
            InvoiceCommentTextBox.Text = "";

            BillingStatusTextBlock.Text = "Заполни шапку счета и сохрани черновик.";
            _invoiceItems.Clear();
            ClearInvoiceItemsEditor();
            UpdateInvoiceTotalsFromItems();
            UpdateInvoiceDocumentActionsState();
            LoadInvoiceDocuments(null);
        }

        private string GenerateNextInvoiceNumber()
        {
            using var db = new AppDbContext();
            int organizationId = GetRequiredActiveOrganizationId();

            string prefix = $"СЧ-{DateTime.Today:yyyy}-";
            var existingNumbers = db.Invoices
                .AsNoTracking()
                .Where(x => x.InvoiceNumber.StartsWith(prefix))
                .Select(x => x.InvoiceNumber)
                .ToList();

            int nextNumber = 1;

            foreach (var number in existingNumbers)
            {
                if (number.Length <= prefix.Length)
                    continue;

                string tail = number.Substring(prefix.Length);

                if (int.TryParse(tail, out int parsed) && parsed >= nextNumber)
                {
                    nextNumber = parsed + 1;
                }
            }

            return $"{prefix}{nextNumber:0000}";
        }

        private static string GetSelectedComboBoxText(ComboBox? comboBox, string fallback)
        {
            if (comboBox?.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? fallback;
            }

            return fallback;
        }

        private void BillingSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_pageReady)
                return;

            LoadInvoices(_selectedInvoiceId);
        }

        private void BillingStatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_pageReady)
                return;

            LoadInvoices(_selectedInvoiceId);
        }

        private void BillingSourceFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_pageReady)
                return;

            LoadInvoices(_selectedInvoiceId);
        }

        private void NewInvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            _syncingInvoiceSelection = true;

            InvoicesListView.SelectedItem = null;
            InvoicesCompactListView.SelectedItem = null;

            _syncingInvoiceSelection = false;
            PrepareNewInvoiceForm();
        }

        private void InvoicesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingInvoiceSelection)
                return;

            if (InvoicesListView.SelectedItem is not BillingListItem selectedItem)
                return;

            _syncingInvoiceSelection = true;
            InvoicesCompactListView.SelectedItem = selectedItem;
            _syncingInvoiceSelection = false;

            LoadInvoiceIntoForm(selectedItem.InvoiceId);
        }

        private void InvoicesCompactListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingInvoiceSelection)
                return;

            if (InvoicesCompactListView.SelectedItem is not BillingListItem selectedItem)
                return;

            _syncingInvoiceSelection = true;
            InvoicesListView.SelectedItem = selectedItem;
            _syncingInvoiceSelection = false;

            LoadInvoiceIntoForm(selectedItem.InvoiceId);
        }

        private void LoadInvoiceIntoForm(int invoiceId)
        {
            using var db = new AppDbContext();

            var invoice = db.Invoices
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == invoiceId);

            if (invoice == null)
            {
                BillingStatusTextBlock.Text = "Не удалось найти счет в базе данных.";
                return;

            }

            _selectedInvoiceId = invoice.Id;

            var client = _clients.FirstOrDefault(x => x.Id == invoice.ClientInfoId);
            InvoiceClientComboBox.SelectedItem = client;

            InvoiceNumberTextBox.Text = invoice.InvoiceNumber;
            InvoiceDatePicker.Date = new DateTimeOffset(invoice.InvoiceDate);
            InvoiceDueDatePicker.Date = new DateTimeOffset(invoice.DueDate ?? invoice.InvoiceDate);

            SelectInvoiceStatus(invoice.Status);

            InvoiceSourceTextBlock.Text = string.IsNullOrWhiteSpace(invoice.SourceType)
                ? InvoiceSourceTypeNames.Manual
                : invoice.SourceType;

            InvoicePeriodTextBox.Text = invoice.PeriodText ?? "";
            InvoiceCommentTextBox.Text = invoice.Comment ?? "";

            BillingStatusTextBlock.Text = $"Открыт счет {invoice.InvoiceNumber}.";
            LoadInvoiceItems(invoice.Id);
            ClearInvoiceItemsEditor();
            string clientName = client?.Name ?? "Клиент не выбран";
            UpdateInvoiceQuickSummary(
                $"Счет {invoice.InvoiceNumber} • {clientName} • {invoice.TotalWithVat:N2} ₽ • {invoice.Status}");

            UpdateInvoiceActionButtonsState();
            UpdateInvoiceDocumentActionsState();
            LoadInvoiceDocuments(invoiceId);
        }

        private void SelectInvoiceStatus(string? status)
        {
            string targetStatus = string.IsNullOrWhiteSpace(status)
                ? InvoiceStatusNames.Draft
                : status;

            foreach (var rawItem in InvoiceStatusEditorComboBox.Items)
            {
                if (rawItem is ComboBoxItem item &&
                    string.Equals(item.Content?.ToString(), targetStatus, StringComparison.OrdinalIgnoreCase))
                {
                    InvoiceStatusEditorComboBox.SelectedItem = item;
                    return;
                }
            }

            if (InvoiceStatusEditorComboBox.Items.Count > 0)
            {
                InvoiceStatusEditorComboBox.SelectedIndex = 0;
            }
        }

        private void SaveInvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (InvoiceClientComboBox.SelectedItem is not ClientInfo selectedClient)
            {
                BillingStatusTextBlock.Text = "Сначала выбери клиента.";
                return;
            }

            using var db = new AppDbContext();

            bool isNew = !_selectedInvoiceId.HasValue;
            Invoice invoice;

            if (isNew)
            {
                invoice = new Invoice
                {
                    CreatedAt = DateTime.Now
                };

                db.Invoices.Add(invoice);
            }
            else
            {
                invoice = db.Invoices.FirstOrDefault(x => x.Id == _selectedInvoiceId.Value);

                if (invoice == null)
                {
                    BillingStatusTextBlock.Text = "Не удалось найти счет для сохранения.";
                    return;
                }
            }
            if (isNew || !invoice.OrganizationProfileId.HasValue)
            {
                invoice.OrganizationProfileId = GetRequiredActiveOrganizationId();
            }

            invoice.ClientInfoId = selectedClient.Id;
            invoice.InvoiceNumber = string.IsNullOrWhiteSpace(InvoiceNumberTextBox.Text)
                ? GenerateNextInvoiceNumber()
                : InvoiceNumberTextBox.Text.Trim();

            invoice.InvoiceDate = InvoiceDatePicker.Date.DateTime;
            invoice.DueDate = InvoiceDueDatePicker.Date.DateTime;
            invoice.PeriodText = InvoicePeriodTextBox.Text.Trim();
            invoice.Status = GetSelectedComboBoxText(InvoiceStatusEditorComboBox, InvoiceStatusNames.Draft);
            invoice.SourceType = InvoiceSourceTypeNames.Manual;
            invoice.Comment = InvoiceCommentTextBox.Text.Trim();

            invoice.TotalWithoutVat = 0m;
            invoice.VatAmount = 0m;
            invoice.TotalWithVat = 0m;

            invoice.UpdatedAt = DateTime.Now;

            db.SaveChanges();

            _selectedInvoiceId = invoice.Id;




            LoadInvoices(invoice.Id);
            LoadInvoiceIntoForm(invoice.Id);
            UpdateInvoiceDocumentActionsState();

            BillingStatusTextBlock.Text = isNew
                ? $"Черновик счета {invoice.InvoiceNumber} создан."
                : $"Счет {invoice.InvoiceNumber} обновлен.";
        }

        private void DeleteInvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedInvoiceId.HasValue)
            {
                BillingStatusTextBlock.Text = "Сначала открой счет, который нужно удалить.";
                return;
            }

            using var db = new AppDbContext();

            var invoice = db.Invoices.FirstOrDefault(x => x.Id == _selectedInvoiceId.Value);
            if (invoice == null)
            {
                BillingStatusTextBlock.Text = "Не удалось найти счет в базе данных.";
                return;
            }

            var items = db.InvoiceItems
                .Where(x => x.InvoiceId == invoice.Id)
                .ToList();

            db.InvoiceItems.RemoveRange(items);
            db.Invoices.Remove(invoice);
            db.SaveChanges();

            InvoicesListView.SelectedItem = null;
            PrepareNewInvoiceForm();
            LoadInvoices();
            UpdateInvoiceActionButtonsState();

            BillingStatusTextBlock.Text = $"Счет {invoice.InvoiceNumber} удален.";
            _invoiceItems.Clear();
            ClearInvoiceItemsEditor();
            UpdateInvoiceTotalsFromItems();
        }
    }
}