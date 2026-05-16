using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public sealed partial class ContractWizardPage : Page
    {
        private string? _lastGeneratedFilePath;

        public ContractWizardPage()
        {
            this.InitializeComponent();
            LoadRecentContracts();
        }

        // ─── Запуск мастера ────────────────────────────────────────────────
        private async void StartWizardButton_Click(object sender, RoutedEventArgs e)
        {
            var wizard = new ContractWizardDialog { XamlRoot = this.XamlRoot };
            await wizard.ShowAsync();

            if (!wizard.WizardCompleted ||
                wizard.ResultContract == null ||
                wizard.ResultParty1  == null ||
                wizard.ResultParty2  == null)
                return;

            await ProcessWizardResult(wizard);
        }

        private async Task ProcessWizardResult(ContractWizardDialog wizard)
        {
            try
            {
                StartWizardButton.IsEnabled = false;
                StartWizardButton.Content   = "Формирую договор...";

                var organization   = ActiveOrganizationService.GetRequired();
                var resultContract = wizard.ResultContract!;
                var party1         = wizard.ResultParty1!;
                var party2         = wizard.ResultParty2!;

                int resolvedClientId = party2.ClientInfoId ?? 0;
                if (resolvedClientId == 0)
                {
                    ShowResult(false, "Не удалось определить клиента. Используйте выбор из базы для стороны 2.", null, null);
                    return;
                }

                // Генерация docx
                string tempPath = ContractWordService.GenerateContractDocx(resultContract, party1, party2);

                using var db = new AppDbContext();
                var client = db.Clients.FirstOrDefault(c => c.Id == resolvedClientId);
                if (client == null)
                {
                    ShowResult(false, "Клиент не найден в базе данных.", null, null);
                    return;
                }

                var copyResult = ClientFileStorageService.CopyFileForClient(client, tempPath);
                string relativePath = copyResult.RelativePath?.ToString() ?? "";
                string fileName     = Path.GetFileName(tempPath);

                string contractNumber = string.IsNullOrWhiteSpace(resultContract.ContractNumber)
                    ? ExtractContractNumber(fileName, resolvedClientId, DateTime.Now)
                    : resultContract.ContractNumber;

                var contract = ClientContractService.GetOrCreateContract(db, organization.Id, resolvedClientId);
                contract.ContractType = resultContract.ContractType;
                contract.Subject      = resultContract.Subject;
                contract.Amount       = resultContract.Amount;
                contract.VatMode      = resultContract.VatMode;
                contract.City         = resultContract.City;
                contract.ValidFrom    = resultContract.ValidFrom;
                contract.ValidTo      = resultContract.ValidTo;
                contract.Party1Json   = party1.ToJson();
                contract.Party2Json   = party2.ToJson();

                db.ClientFiles.Add(new ClientFile
                {
                    ClientInfoId     = resolvedClientId,
                    OriginalFileName = fileName,
                    RelativePath     = relativePath,
                    FileSizeBytes    = copyResult.FileSizeBytes,
                    AddedAt          = DateTime.Now,
                    Category         = "Договор"
                });

                ClientContractService.MarkGenerated(db, contract, contractNumber, relativePath);

                if (!string.Equals(client.ContractStatus, "Договор подписан",
                        StringComparison.OrdinalIgnoreCase))
                    client.ContractStatus = "Договор сформирован";
                client.ContractGeneratedAt = DateTime.Now;
                db.SaveChanges();

                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch (Exception _exDel) { AppLogger.LogWarning("FileDelete", _exDel.Message); }

                _lastGeneratedFilePath = ClientFileStorageService.GetFullPath(relativePath);
                if (File.Exists(_lastGeneratedFilePath))
                    ClientFileStorageService.OpenFile(_lastGeneratedFilePath);

                var typeInfo = ContractTypeDefinitions.GetByKey(resultContract.ContractType);
                string msg = $"Договор №{contractNumber} · {typeInfo.DisplayName}\nКлиент: {client.Name}";
                ShowResult(true, msg, _lastGeneratedFilePath, contract);

                // Предлагаем платёжный документ если нужен
                if (typeInfo.OffersUPD || typeInfo.OffersInvoice)
                    await OfferPaymentDocumentAsync(client, contract, contractNumber, typeInfo);

                LoadRecentContracts();
            }
            catch (Exception ex)
            {
                ShowResult(false, $"Ошибка: {ex.Message}", null, null);
            }
            finally
            {
                StartWizardButton.IsEnabled = true;
                StartWizardButton.Content   = "Создать договор →";
            }
        }

        private async Task OfferPaymentDocumentAsync(
            ClientInfo client, ClientContract contract, string contractNumber, ContractTypeInfo typeInfo)
        {
            var postDialog = new ContentDialog
            {
                Title               = "Договор готов",
                Content             = $"Договор №{contractNumber} для «{client.Name}» сформирован.\n\nСоздать платёжный документ к договору?",
                PrimaryButtonText   = typeInfo.OffersUPD     ? "УПД"            : "",
                SecondaryButtonText = typeInfo.OffersInvoice ? "Счёт на оплату" : "",
                CloseButtonText     = "Позже",
                DefaultButton       = ContentDialogButton.Primary,
                XamlRoot            = this.XamlRoot
            };

            var result = await postDialog.ShowAsync();
            // При необходимости здесь можно добавить навигацию на BillingPage
        }

        // ─── UI хелперы ─────────────────────────────────────────────────────
        private void ShowResult(bool success, string message, string? filePath, ClientContract? contract)
        {
            LastResultCard.Visibility   = Visibility.Visible;
            LastResultText.Text         = message;
            LastResultCard.BorderBrush  = success
                ? (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["NiatecSuccessBrush"]
                : (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["NiatecDangerBrush"];

            OpenLastFileButton.IsEnabled = success && filePath != null && File.Exists(filePath);
            GoToContractsButton.Visibility = success ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenLastFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastGeneratedFilePath != null && File.Exists(_lastGeneratedFilePath))
                ClientFileStorageService.OpenFile(_lastGeneratedFilePath);
        }

        private void GoToContractsButton_Click(object sender, RoutedEventArgs e)
        {
            // Навигация на страницу договоров через shell
            if (this.Frame?.Parent is Frame parentFrame)
                parentFrame.Navigate(typeof(ContractsPage));
        }

        // ─── Список последних договоров ─────────────────────────────────────
        private void LoadRecentContracts()
        {
            try
            {
                using var db = new AppDbContext();
                OrganizationProfile? org;
                try { org = ActiveOrganizationService.GetRequired(); }
                catch { return; }
                if (org == null) return;

                var recent = db.ClientContracts
                    .AsNoTracking()
                    .Where(c => c.OrganizationProfileId == org.Id
                             && !string.IsNullOrEmpty(c.ContractType)
                             && c.ContractType != "services" || c.GeneratedAt != null)
                    .Include(c => c.ClientInfo)
                    .OrderByDescending(c => c.GeneratedAt ?? c.UpdatedAt)
                    .Take(10)
                    .ToList();

                if (recent.Count == 0)
                {
                    NoRecentContractsText.Visibility = Visibility.Visible;
                    RecentContractsList.Visibility   = Visibility.Collapsed;
                    return;
                }

                NoRecentContractsText.Visibility = Visibility.Collapsed;
                RecentContractsList.Visibility   = Visibility.Visible;

                RecentContractsList.ItemsSource = recent.Select(c =>
                {
                    string fullPath = string.IsNullOrWhiteSpace(c.DocumentRelativePath)
                        ? ""
                        : ClientFileStorageService.GetFullPath(c.DocumentRelativePath);
                    return new RecentContractItem
                    {
                        ContractId       = c.Id,
                        ClientName       = c.ClientInfo?.Name ?? "—",
                        ContractTypeName = ContractTypeDefinitions.GetByKey(c.ContractType ?? "services").DisplayName,
                        ContractNumber   = string.IsNullOrWhiteSpace(c.ContractNumber) ? "Без номера" : $"№{c.ContractNumber}",
                        DateDisplay      = (c.GeneratedAt ?? c.UpdatedAt).ToString("dd.MM.yyyy"),
                        FilePath         = fullPath,
                        HasFile          = File.Exists(fullPath)
                    };
                }).ToList();
            }
            catch { /* не критично */ }
        }

        private void RecentContractsList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (RecentContractsList.SelectedItem is not RecentContractItem item) return;

            if (item.HasFile && File.Exists(item.FilePath))
            {
                ClientFileStorageService.OpenFile(item.FilePath);
            }
            else
            {
                // Файл не найден — показываем сообщение
                _ = new ContentDialog
                {
                    Title       = "Файл не найден",
                    Content     = $"Файл договора «{item.ContractNumber}» не найден на диске.\nВозможно, он был перемещён или удалён.",
                    CloseButtonText = "OK",
                    XamlRoot    = this.XamlRoot
                }.ShowAsync();
            }
        }

        // ─── Вспомогательный метод генерации номера ─────────────────────────
        private static string ExtractContractNumber(string fileName, int clientId, DateTime date)
            => $"{date:yyMMdd}-{clientId:D3}";
    }

    // ViewModel для списка последних договоров
    internal sealed class RecentContractItem
    {
        public int     ContractId       { get; init; }
        public string  ClientName       { get; init; } = "";
        public string  ContractTypeName { get; init; } = "";
        public string  ContractNumber   { get; init; } = "";
        public string  DateDisplay      { get; init; } = "";
        public string  FilePath         { get; init; } = "";
        public bool    HasFile          { get; init; }
    }
}
