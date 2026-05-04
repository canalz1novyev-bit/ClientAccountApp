using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

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
        private void OrganizationsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateOrganizationSelectionVisual();

            if (OrganizationsListView.SelectedItem is OrganizationProfile selectedOrganization)
            {
                OrganizationSelectStatusTextBlock.Text = $"Выбрана организация: {selectedOrganization.Name}";
            }
            else
            {
                OrganizationSelectStatusTextBlock.Text = "";
            }
        }

        private void UpdateOrganizationSelectionVisual()
        {
            foreach (var item in OrganizationsListView.Items)
            {
                if (OrganizationsListView.ContainerFromItem(item) is not ListViewItem container)
                    continue;

                var cardBorder = FindDescendantByName<Border>(container, "OrganizationCardBorder");
                if (cardBorder == null)
                    continue;

                bool isSelected = ReferenceEquals(item, OrganizationsListView.SelectedItem);

                if (isSelected)
                {
                    cardBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 30, 39, 56));
                    cardBorder.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 215, 186, 125));
                    cardBorder.BorderThickness = new Thickness(2);
                }
                else
                {
                    cardBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 23, 26, 33));
                    cardBorder.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 43, 49, 64));
                    cardBorder.BorderThickness = new Thickness(1);
                }
            }
        }

        private static T? FindDescendantByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T element && element.Name == name)
                    return element;

                var result = FindDescendantByName<T>(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }
        private void EnterOrganizationButton_Click(object sender, RoutedEventArgs e)
        {
            EnterSelectedOrganization();
        }
        private void OrganizationCardBorder_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is OrganizationProfile organization)
            {
                OrganizationsListView.SelectedItem = organization;
                EnterSelectedOrganization();
            }
        }
        private void EnterSelectedOrganization()
        {
            if (OrganizationsListView.SelectedItem is not OrganizationProfile organization)
            {
                OrganizationSelectStatusTextBlock.Text = "Сначала выберите организацию.";
                return;
            }

            ActiveOrganizationService.SetCurrentOrganization(organization.Id);

            if (ShellWindow.AppShell != null)
            {
                ShellWindow.AppShell.EnterMainApplication();
            }
            else
            {
                Frame.Navigate(typeof(DashboardPage));
            }
        }
        private void AddOrganizationButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(OrganizationSetupPage), "new");
        }
    }
}