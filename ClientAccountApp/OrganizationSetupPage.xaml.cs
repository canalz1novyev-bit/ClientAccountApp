using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
namespace ClientAccountApp
{
    public sealed partial class OrganizationSetupPage : Page
    {
        private int? _editingOrganizationId;
        private bool _forceNewOrganization;
        private string? _pendingLogoSourcePath;
        public OrganizationSetupPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _forceNewOrganization = string.Equals(e.Parameter?.ToString(), "new", StringComparison.OrdinalIgnoreCase);

            if (!_forceNewOrganization && ActiveOrganizationService.Current != null)
            {
                LoadOrganizationIntoForm(ActiveOrganizationService.Current);
                OrganizationSetupTitleTextBlock.Text = "Профиль организации";
                SaveOrganizationButton.Content = "Сохранить профиль организации";
            }
            else
            {
                PrepareNewOrganizationForm();
            }
        }

        private void PrepareNewOrganizationForm()
        {
            _editingOrganizationId = null;

            OrganizationSetupTitleTextBlock.Text = "Создание организации";
            SaveOrganizationButton.Content = "Сохранить и войти";

            OrganizationInnTextBox.Text = "";
            OrganizationNameTextBox.Text = "";
            OrganizationShortNameTextBox.Text = "";
            OrganizationKppTextBox.Text = "";
            OrganizationOgrnTextBox.Text = "";
            OrganizationLegalAddressTextBox.Text = "";
            OrganizationDirectorNameTextBox.Text = "";
            OrganizationDirectorPositionTextBox.Text = "Директор";

            OrganizationBankNameTextBox.Text = "";
            OrganizationSettlementAccountTextBox.Text = "";
            OrganizationBankBicTextBox.Text = "";
            OrganizationCorrespondentAccountTextBox.Text = "";

            OrganizationEmailTextBox.Text = "";
            OrganizationPhoneTextBox.Text = "";

            OrganizationSetupStatusTextBlock.Text = "Введите ИНН и нажмите «Заполнить по ИНН» или заполните данные вручную.";
            _pendingLogoSourcePath = null;
            OrganizationLogoPathTextBox.Text = "";
        }

        private void LoadOrganizationIntoForm(OrganizationProfile organization)
        {
            _editingOrganizationId = organization.Id;

            OrganizationInnTextBox.Text = organization.Inn;
            OrganizationNameTextBox.Text = organization.Name;
            OrganizationShortNameTextBox.Text = organization.ShortName;
            OrganizationKppTextBox.Text = organization.Kpp;
            OrganizationOgrnTextBox.Text = organization.Ogrn;
            OrganizationLegalAddressTextBox.Text = organization.LegalAddress;
            OrganizationDirectorNameTextBox.Text = organization.DirectorName;
            OrganizationDirectorPositionTextBox.Text = string.IsNullOrWhiteSpace(organization.DirectorPosition)
                ? "Директор"
                : organization.DirectorPosition;

            OrganizationBankNameTextBox.Text = organization.BankName;
            OrganizationSettlementAccountTextBox.Text = organization.SettlementAccount;
            OrganizationBankBicTextBox.Text = organization.BankBic;
            OrganizationCorrespondentAccountTextBox.Text = organization.CorrespondentAccount;

            OrganizationEmailTextBox.Text = organization.Email;
            OrganizationPhoneTextBox.Text = organization.Phone;

            OrganizationSetupStatusTextBlock.Text = $"Открыт профиль организации «{organization.Name}».";
            _pendingLogoSourcePath = null;

            OrganizationLogoPathTextBox.Text = string.IsNullOrWhiteSpace(organization.LogoRelativePath)
                ? ""
                : OrganizationLogoStorageService.GetLogoFullPath(organization.LogoRelativePath);
        }
        private async void PickOrganizationLogoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");

                var shell = ShellWindow.AppShell;

                if (shell == null)
                {
                    OrganizationSetupStatusTextBlock.Text = "Не удалось получить окно приложения для выбора файла.";
                    return;
                }

                IntPtr hwnd = WindowNative.GetWindowHandle(shell);
                InitializeWithWindow.Initialize(picker, hwnd);

                StorageFile? file = await picker.PickSingleFileAsync();

                if (file == null)
                {
                    OrganizationSetupStatusTextBlock.Text = "Выбор логотипа отменён.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(file.Path) || !File.Exists(file.Path))
                {
                    OrganizationSetupStatusTextBlock.Text = "Не удалось получить путь к выбранному логотипу.";
                    return;
                }

                _pendingLogoSourcePath = file.Path;
                OrganizationLogoPathTextBox.Text = file.Path;

                OrganizationSetupStatusTextBlock.Text = "Логотип выбран. Нажмите «Сохранить», чтобы применить его.";
            }
            catch (Exception ex)
            {
                OrganizationSetupStatusTextBlock.Text = $"Ошибка выбора логотипа: {ex.Message}";
            }
        }
        private async void FillOrganizationByInnButton_Click(object sender, RoutedEventArgs e)
        {
            string inn = OrganizationInnTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(inn))
            {
                OrganizationSetupStatusTextBlock.Text = "Введите ИНН организации.";
                return;
            }

            string lookupType = inn.Length == 12 ? "ИП" : "ООО";

            try
            {
                FillOrganizationByInnButton.IsEnabled = false;
                FillOrganizationByInnButton.Content = "Поиск...";

                InnLookupResult result = await InnLookupService.FindByInnAsync(inn, lookupType);

                OrganizationNameTextBox.Text = result.ClientName;
                OrganizationShortNameTextBox.Text = result.ClientName;
                OrganizationOgrnTextBox.Text = result.Ogrn;
                OrganizationLegalAddressTextBox.Text = result.LegalAddress;
                OrganizationDirectorNameTextBox.Text = result.DirectorName;

                OrganizationSetupStatusTextBlock.Text = $"Данные по ИНН {inn} заполнены.";
            }
            catch (Exception ex)
            {
                OrganizationSetupStatusTextBlock.Text = $"Не удалось заполнить по ИНН: {ex.Message}";
            }
            finally
            {
                FillOrganizationByInnButton.IsEnabled = true;
                FillOrganizationByInnButton.Content = "Заполнить по ИНН";
            }
        }

        private void SaveOrganizationButton_Click(object sender, RoutedEventArgs e)
        {
            string name = OrganizationNameTextBox.Text.Trim();
            string inn = OrganizationInnTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                OrganizationSetupStatusTextBlock.Text = "Укажите наименование организации.";
                return;
            }

            if (string.IsNullOrWhiteSpace(inn))
            {
                OrganizationSetupStatusTextBlock.Text = "Укажите ИНН организации.";
                return;
            }


            using var db = new AppDbContext();

            bool isNew = !_editingOrganizationId.HasValue;
            OrganizationProfile organization;

            if (isNew)
            {
                organization = new OrganizationProfile
                {
                    CreatedAt = DateTime.Now
                };

                db.OrganizationProfiles.Add(organization);
            }
            else
            {
                organization = db.OrganizationProfiles.FirstOrDefault(x => x.Id == _editingOrganizationId.Value);

                if (organization == null)
                {
                    OrganizationSetupStatusTextBlock.Text = "Не удалось найти организацию в базе данных.";
                    return;
                }
            }

            organization.Name = name;
            organization.ShortName = OrganizationShortNameTextBox.Text.Trim();
            organization.Inn = inn;
            organization.Kpp = OrganizationKppTextBox.Text.Trim();
            organization.Ogrn = OrganizationOgrnTextBox.Text.Trim();
            organization.LegalAddress = OrganizationLegalAddressTextBox.Text.Trim();

            organization.DirectorName = OrganizationDirectorNameTextBox.Text.Trim();
            organization.DirectorPosition = string.IsNullOrWhiteSpace(OrganizationDirectorPositionTextBox.Text)
                ? "Директор"
                : OrganizationDirectorPositionTextBox.Text.Trim();

            organization.BankName = OrganizationBankNameTextBox.Text.Trim();
            organization.BankBic = OrganizationBankBicTextBox.Text.Trim();
            organization.SettlementAccount = OrganizationSettlementAccountTextBox.Text.Trim();
            organization.CorrespondentAccount = OrganizationCorrespondentAccountTextBox.Text.Trim();

            organization.Email = OrganizationEmailTextBox.Text.Trim();
            organization.Phone = OrganizationPhoneTextBox.Text.Trim();

            organization.IsActive = true;
            organization.UpdatedAt = DateTime.Now;

            db.SaveChanges();

            if (!string.IsNullOrWhiteSpace(_pendingLogoSourcePath))
            {
                organization.LogoRelativePath = OrganizationLogoStorageService.SaveLogoForOrganization(
                    organization.Id,
                    _pendingLogoSourcePath);

                organization.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                _pendingLogoSourcePath = null;
            }

            ActiveOrganizationService.SetActiveOrganization(organization.Id);

            ShellWindow.AppShell?.EnterMainApplication();

            OrganizationSetupStatusTextBlock.Text = isNew
                ? $"Организация «{organization.Name}» создана."
                : $"Профиль организации «{organization.Name}» обновлен.";
        }

        private void OpenOrganizationSelectButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(OrganizationSelectPage));
        }
        private async void OrganizationBankBicTextBox_LostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OrganizationBankBicTextBox == null) return;

            string bic = new string(
                OrganizationBankBicTextBox.Text.Trim().Where(char.IsDigit).ToArray());

            if (bic.Length != 9) return;

            try
            {
                var result = await BankLookupService.FindByBicAsync(bic);
                if (result == null) return;

                OrganizationBankBicTextBox.Text         = result.Bic;
                OrganizationBankNameTextBox.Text         = result.BankName;
                OrganizationCorrespondentAccountTextBox.Text = result.CorrespondentAccount;
            }
            catch
            {
                // Тихая ошибка — пользователь заполнит вручную
            }
        }

    }
}