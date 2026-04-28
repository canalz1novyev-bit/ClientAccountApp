using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

namespace ClientAccountApp
{
    public sealed partial class OrganizationSelectPage : Page
    {
        private readonly ObservableCollection<OrganizationProfile> _organizations = new();

        public OrganizationSelectPage()
        {
            this.InitializeComponent();

            OrganizationsListView.ItemsSource = _organizations;
            Loaded += OrganizationSelectPage_Loaded;
        }

        private void OrganizationSelectPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadOrganizations();
        }

        private void LoadOrganizations()
        {
            _organizations.Clear();

            var organizations = ActiveOrganizationService.GetActiveOrganizations();

            foreach (var organization in organizations)
            {
                _organizations.Add(organization);
            }

            if (_organizations.Count > 0)
            {
                OrganizationsListView.SelectedIndex = 0;
                OrganizationSelectStatusTextBlock.Text = "";
            }
            else
            {
                OrganizationSelectStatusTextBlock.Text = "Организаций пока нет. Добавьте первую организацию.";
            }
        }

        private void EnterOrganizationButton_Click(object sender, RoutedEventArgs e)
        {
            if (OrganizationsListView.SelectedItem is not OrganizationProfile organization)
            {
                OrganizationSelectStatusTextBlock.Text = "Сначала выберите организацию.";
                return;
            }

            ActiveOrganizationService.SetActiveOrganization(organization.Id);
            ShellWindow.AppShell?.EnterMainApplication();
        }

        private void AddOrganizationButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(OrganizationSetupPage), "new");
        }
    }
}