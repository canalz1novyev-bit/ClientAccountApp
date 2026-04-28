using Microsoft.EntityFrameworkCore;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.IO;


namespace ClientAccountApp
{
    public sealed partial class ContractsPage : Page
    {
        private readonly ObservableCollection<ContractsListItemViewModel> _items = new();
        private bool _pageReady = false;
        public bool CanToggleSigned { get; set; }
        public string SignToggleButtonText { get; set; } = "Отметить договор подписанным";


        public ContractsPage()
        {
            this.InitializeComponent();

            if (ContractsListView != null)
            {
                ContractsListView.ItemsSource = _items;
            }

            Loaded += ContractsPage_Loaded;
            
        }

        private void ContractsPage_Loaded(object sender, RoutedEventArgs e)
        {
            ContractSchemaService.EnsureContractTables();
            ClientContractService.EnsureContractsForActiveOrganization();

           

            LoadContracts();
        }
        private void SavePageState()
        {
            UiStateService.Save(state =>
            {
                state.ContractsPage.SearchText = ContractsSearchTextBox?.Text ?? "";
                state.ContractsPage.StatusFilter = GetComboBoxValue(ContractStatusFilterComboBox, "В работе");
                state.ContractsPage.SortMode = GetComboBoxValue(ContractsSortComboBox, "Сначала требующие внимания");
            });
        }

        private void RestorePageState()
        {
            var state = UiStateService.Load().ContractsPage;

            if (ContractsSearchTextBox != null)
                ContractsSearchTextBox.Text = state.SearchText ?? "";

            SelectComboBoxValue(ContractStatusFilterComboBox, state.StatusFilter, "В работе");
            SelectComboBoxValue(ContractsSortComboBox, state.SortMode, "Сначала требующие внимания");
        }

        private static string GetComboBoxValue(ComboBox? comboBox, string fallback)
        {
            if (comboBox?.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? fallback;
            }

            return fallback;
        }

        private static void SelectComboBoxValue(ComboBox? comboBox, string? value, string fallback)
        {
            if (comboBox == null)
                return;

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

            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }
        private void RefreshContractsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_pageReady)
                return;

            SavePageState();
            LoadContracts();
        }

        private void ContractsSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_pageReady)
                return;

            SavePageState();
            LoadContracts();
        }

        private void ContractStatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_pageReady)
                return;

            SavePageState();
            LoadContracts();
        }

        private void ContractsSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_pageReady)
                return;

            SavePageState();
            LoadContracts();
        }

        private string GetSelectedContractStatusFilter()
        {
            if (ContractStatusFilterComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Content?.ToString() ?? "Все";
            }

            return "Все";
        }
        private bool MatchesContractsFilter(ContractsListItemViewModel item)
        {
            string searchText = ContractsSearchTextBox?.Text?.Trim().ToLowerInvariant() ?? "";
            string statusFilter = GetSelectedContractStatusFilter();

            bool matchesSearch =
                string.IsNullOrWhiteSpace(searchText) ||
                (item.ClientName?.ToLowerInvariant().Contains(searchText) ?? false) ||
                (item.ClientMetaText?.ToLowerInvariant().Contains(searchText) ?? false) ||
                (item.ContractNumber?.ToLowerInvariant().Contains(searchText) ?? false);

            if (!matchesSearch)
                return false;

            return statusFilter switch
            {
                "В работе" => !string.Equals(item.ContractStatusText, "Договор подписан", StringComparison.OrdinalIgnoreCase),
                "Все" => true,
                "Требует договора" => string.Equals(item.ContractStatusText, "Требует договора", StringComparison.OrdinalIgnoreCase),
                "Договор сформирован" => string.Equals(item.ContractStatusText, "Договор сформирован", StringComparison.OrdinalIgnoreCase),
                "Договор подписан" => string.Equals(item.ContractStatusText, "Договор подписан", StringComparison.OrdinalIgnoreCase),
                _ => true
            };
        }

        private void LoadContracts()
        {
            _items.Clear();

            ContractSchemaService.EnsureContractTables();
            ClientContractService.EnsureContractsForActiveOrganization();

            var organization = ActiveOrganizationService.GetRequired();

            using var db = new AppDbContext();

            var clients = db.Clients
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToList();

            var contracts = db.ClientContracts
                .AsNoTracking()
                .Where(x => x.OrganizationProfileId == organization.Id)
                .ToList();

            var contractMap = contracts.ToDictionary(x => x.ClientInfoId);

            var allItems = new List<ContractsListItemViewModel>();

            foreach (var client in clients)
            {
                contractMap.TryGetValue(client.Id, out var contract);

                string contractStatus = contract == null || string.IsNullOrWhiteSpace(contract.Status)
                    ? "Требует договора"
                    : contract.Status;

                bool hasContractFile = false;
                string contractNumber = "—";

                if (contract != null)
                {
                    if (!string.IsNullOrWhiteSpace(contract.ContractNumber))
                        contractNumber = contract.ContractNumber;

                    if (!string.IsNullOrWhiteSpace(contract.DocumentRelativePath))
                    {
                        string fullPath = ClientFileStorageService.GetFullPath(contract.DocumentRelativePath);
                        hasContractFile = File.Exists(fullPath);
                    }
                }

                // Резервная проверка: если в новой таблице путь ещё не заполнен,
                // но старый файл договора есть в ClientFiles.
                if (!hasContractFile)
                {
                    var latestContractFile = db.ClientFiles
                        .AsNoTracking()
                        .Where(f => f.ClientInfoId == client.Id && f.Category == "Договор")
                        .OrderByDescending(f => f.AddedAt)
                        .FirstOrDefault();

                    if (latestContractFile != null)
                    {
                        string fullPath = ClientFileStorageService.GetFullPath(latestContractFile.RelativePath);

                        if (File.Exists(fullPath))
                        {
                            hasContractFile = true;

                            if (contractNumber == "—")
                                contractNumber = ExtractContractNumber(latestContractFile.OriginalFileName);
                        }
                    }
                }

                var item = new ContractsListItemViewModel
                {
                    ClientId = client.Id,
                    ClientName = string.IsNullOrWhiteSpace(client.Name) ? "Клиент без названия" : client.Name,
                    ClientMetaText = BuildClientMeta(client),
                    ContractStatusText = contractStatus,
                    ContractNumber = contractNumber,
                    GeneratedAtText = contract?.GeneratedAt.HasValue == true
                        ? contract.GeneratedAt.Value.ToString("dd.MM.yyyy HH:mm")
                        : "—",
                    SignedAtText = contract?.SignedAt.HasValue == true
                        ? contract.SignedAt.Value.ToString("dd.MM.yyyy HH:mm")
                        : "—",
                    HasContractFile = hasContractFile,
                    ContractFileButtonText = hasContractFile ? "Открыть договор" : "Сформировать договор",
                    CanToggleSigned = string.Equals(contractStatus, "Договор сформирован", StringComparison.OrdinalIgnoreCase),
                    SignToggleButtonText = string.Equals(contractStatus, "Договор подписан", StringComparison.OrdinalIgnoreCase)
                        ? "Договор подписан"
                        : "Отметить договор подписанным"
                };

                ApplyStatusStyle(item, contractStatus);
                allItems.Add(item);
            }

            foreach (var item in allItems.Where(MatchesContractsFilter))
            {
                _items.Add(item);
            }

            ContractsTotalCountTextBlock.Text = allItems.Count.ToString();
            ContractsNeedCountTextBlock.Text = allItems.Count(x => x.ContractStatusText == "Требует договора").ToString();
            ContractsGeneratedCountTextBlock.Text = allItems.Count(x => x.ContractStatusText == "Договор сформирован").ToString();
            ContractsSignedCountTextBlock.Text = allItems.Count(x => x.ContractStatusText == "Договор подписан").ToString();

            string currentFilter = GetSelectedContractStatusFilter();

            ContractsEmptyStateTextBlock.Text = _items.Count == 0
                ? currentFilter == "В работе"
                    ? "В рабочей очереди сейчас нет договоров по выбранной организации."
                    : "По текущему фильтру договоры не найдены."
                : $"Договоры показаны по организации: {organization.Name}";
        }


        private void OpenClientButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element ||
                element.DataContext is not ContractsListItemViewModel item)
            {
                return;
            }

            Frame?.Navigate(typeof(LegacyWorkspacePage), item.ClientId);
        }

        private void OpenContractButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element ||
                element.DataContext is not ContractsListItemViewModel item)
            {
                return;
            }

            try
            {
                ContractSchemaService.EnsureContractTables();

                var organization = ActiveOrganizationService.GetRequired();

                using var db = new AppDbContext();

                var client = db.Clients.FirstOrDefault(c => c.Id == item.ClientId);
                if (client == null)
                {
                    ContractsEmptyStateTextBlock.Text = "Не удалось найти клиента в базе данных.";
                    return;
                }

                var contract = ClientContractService.GetOrCreateContract(
                    db,
                    organization.Id,
                    client.Id);

                // Если договор уже есть в новой таблице и файл существует — открываем.
                if (!string.IsNullOrWhiteSpace(contract.DocumentRelativePath))
                {
                    string existingFullPath = ClientFileStorageService.GetFullPath(contract.DocumentRelativePath);

                    if (File.Exists(existingFullPath))
                    {
                        ClientFileStorageService.OpenFile(existingFullPath);
                        ContractsEmptyStateTextBlock.Text = $"Открыт договор клиента «{item.ClientName}».";
                        return;
                    }
                }

                // Резервный вариант: ищем старый файл договора в ClientFiles.
                var latestContractFile = db.ClientFiles
                    .Where(f => f.ClientInfoId == item.ClientId && f.Category == "Договор")
                    .OrderByDescending(f => f.AddedAt)
                    .FirstOrDefault();

                if (latestContractFile != null)
                {
                    string oldFullPath = ClientFileStorageService.GetFullPath(latestContractFile.RelativePath);

                    if (File.Exists(oldFullPath))
                    {
                        contract.ContractNumber = latestContractFile.OriginalFileName;
                        contract.DocumentRelativePath = latestContractFile.RelativePath;

                        if (!contract.GeneratedAt.HasValue)
                            contract.GeneratedAt = latestContractFile.AddedAt;

                        if (!string.Equals(contract.Status, "Договор подписан", StringComparison.OrdinalIgnoreCase))
                            contract.Status = "Договор сформирован";

                        contract.UpdatedAt = DateTime.Now;

                        db.SaveChanges();

                        ClientFileStorageService.OpenFile(oldFullPath);
                        LoadContracts();

                        ContractsEmptyStateTextBlock.Text = $"Открыт договор клиента «{item.ClientName}».";
                        return;
                    }
                }

                // Если договора нет — формируем новый.
                ContractsEmptyStateTextBlock.Text = $"Формирую договор для клиента «{item.ClientName}» от имени организации «{organization.Name}»...";

                string tempContractPath = ContractWordService.GenerateContractDocx(item.ClientId);

                var copyResult = ClientFileStorageService.CopyFileForClient(client, tempContractPath);

                string contractRelativePath = Convert.ToString(copyResult.RelativePath) ?? "";
                long contractFileSizeBytes = Convert.ToInt64(copyResult.FileSizeBytes);
                string contractFileName = Path.GetFileName(tempContractPath);

                db.ClientFiles.Add(new ClientFile
                {
                    ClientInfoId = client.Id,
                    OriginalFileName = contractFileName,
                    RelativePath = contractRelativePath,
                    FileSizeBytes = contractFileSizeBytes,
                    AddedAt = DateTime.Now,
                    Category = "Договор"
                });

                ClientContractService.MarkGenerated(
                    db,
                    contract,
                    contractFileName,
                    contractRelativePath);

                // Временно синхронизируем старые поля, чтобы старый список клиентов не потерял галочки.
                if (!string.Equals(client.ContractStatus, "Договор подписан", StringComparison.OrdinalIgnoreCase))
                {
                    client.ContractStatus = "Договор сформирован";
                }

                client.ContractGeneratedAt = DateTime.Now;
                db.SaveChanges();

                string finalFullPath = ClientFileStorageService.GetFullPath(contractRelativePath);

                LoadContracts();

                ContractsEmptyStateTextBlock.Text = $"Договор клиента «{item.ClientName}» сформирован.";

                if (File.Exists(finalFullPath))
                {
                    ClientFileStorageService.OpenFile(finalFullPath);
                }

                try
                {
                    if (File.Exists(tempContractPath))
                        File.Delete(tempContractPath);
                }
                catch
                {
                    // временный файл не критичен
                }
            }
            catch (Exception ex)
            {
                ContractsEmptyStateTextBlock.Text = $"Ошибка работы с договором: {ex.Message}";
            }
        }

        private void OpenClientFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element ||
                element.DataContext is not ContractsListItemViewModel item)
            {
                return;
            }

            try
            {
                using var db = new AppDbContext();

                var client = db.Clients
                    .AsNoTracking()
                    .FirstOrDefault(c => c.Id == item.ClientId);

                if (client == null)
                {
                    ContractsEmptyStateTextBlock.Text = "Не удалось найти клиента для открытия папки.";
                    return;
                }

                ClientFileStorageService.OpenClientFolder(client);
                ContractsEmptyStateTextBlock.Text = $"Открыта папка клиента «{item.ClientName}».";
            }
            catch (Exception ex)
            {
                ContractsEmptyStateTextBlock.Text = $"Ошибка открытия папки клиента: {ex.Message}";
            }
        }

        private void MarkContractSignedButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element ||
                element.DataContext is not ContractsListItemViewModel item)
            {
                return;
            }

            try
            {
                ContractSchemaService.EnsureContractTables();

                var organization = ActiveOrganizationService.GetRequired();

                using var db = new AppDbContext();

                var client = db.Clients.FirstOrDefault(c => c.Id == item.ClientId);
                if (client == null)
                {
                    ContractsEmptyStateTextBlock.Text = "Не удалось найти клиента в базе данных.";
                    return;
                }

                var contract = ClientContractService.GetOrCreateContract(
                    db,
                    organization.Id,
                    client.Id);

                bool hasContractFile = false;

                if (!string.IsNullOrWhiteSpace(contract.DocumentRelativePath))
                {
                    string fullPath = ClientFileStorageService.GetFullPath(contract.DocumentRelativePath);
                    hasContractFile = File.Exists(fullPath);
                }

                if (!hasContractFile && !contract.GeneratedAt.HasValue)
                {
                    ContractsEmptyStateTextBlock.Text = "Сначала сформируй договор для этого клиента.";
                    return;
                }

                if (string.Equals(contract.Status, "Договор подписан", StringComparison.OrdinalIgnoreCase))
                {
                    ContractsEmptyStateTextBlock.Text = $"У клиента «{client.Name}» договор уже подписан по организации «{organization.Name}».";
                    return;
                }

                ClientContractService.MarkSigned(db, contract);

                // Временно синхронизируем старые поля клиента для старых экранов.
                client.ContractStatus = "Договор подписан";
                client.ContractSignedAt = DateTime.Now;

                if (!client.ContractGeneratedAt.HasValue)
                    client.ContractGeneratedAt = DateTime.Now;

                db.SaveChanges();

                LoadContracts();

                ContractsEmptyStateTextBlock.Text =
                    GetSelectedContractStatusFilter() == "В работе"
                        ? $"Договор клиента «{client.Name}» отмечен как подписанный. Если он исчез из списка — это нормально: фильтр «В работе» скрывает подписанные договоры."
                        : $"Договор клиента «{client.Name}» отмечен как подписанный.";
            }
            catch (Exception ex)
            {
                ContractsEmptyStateTextBlock.Text = $"Ошибка отметки договора: {ex.Message}";
            }
        }

        private static string BuildClientMeta(ClientInfo client)
        {
            string type = string.IsNullOrWhiteSpace(client.ClientType) ? "Клиент" : client.ClientType;
            string inn = string.IsNullOrWhiteSpace(client.Inn) ? "ИНН не указан" : $"ИНН {client.Inn}";
            return $"{type} | {inn}";
        }

        private static string ExtractContractNumber(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "—";

            return fileName;
        }

        private static void ApplyStatusStyle(ContractsListItemViewModel item, string contractStatus)
        {

            switch (contractStatus)
            {
                case "Договор подписан":
                    item.StatusIcon = "✓";
                    item.StatusBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 58, 110, 72));
                    item.StatusTextBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 115, 201, 145));
                    break;

                case "Договор сформирован":
                    item.StatusIcon = "●";
                    item.StatusBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 140, 110, 40));
                    item.StatusTextBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 215, 186, 125));
                    break;

                default:
                    item.StatusIcon = "•";
                    item.StatusBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 80, 80, 80));
                    item.StatusTextBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 184, 184, 184));
                    break;
            }

        }
    }
}