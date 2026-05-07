using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Microsoft.Data.SqlClient;

namespace ClientAccountApp
{
    public sealed partial class ShellWindow : Window
    {
        public static Window? CurrentWindow { get; private set; }
        public static ShellWindow? AppShell { get; private set; }

        /// <summary>Навигация из любой страницы.</summary>
        public void NavigateTo(Type pageType)
        {
            if (RootFrame.CurrentSourcePageType != pageType)
                RootFrame.Navigate(pageType);
        }

        public ShellWindow()
        {
            CurrentWindow = this;
            AppShell = this;

            this.InitializeComponent();

            if (PaneFooterContent != null && !AppNavigationView.IsPaneOpen)
            {
                PaneFooterContent.Visibility = Visibility.Collapsed;
            }

            ActiveOrganizationService.Initialize();

            ClientFileStorageService.MigrateLegacyClientFoldersOnce();

            this.Title = "ClientAccountApp — Версия 2";

            RefreshOrganizationFooter();
            RefreshDatabaseFooter();
            if (ActiveOrganizationService.Current == null)
            {
                if (ActiveOrganizationService.HasAnyOrganizations())
                {
                    RootFrame.Navigate(typeof(OrganizationSelectPage));
                }
                else
                {
                    RootFrame.Navigate(typeof(OrganizationSetupPage), "new");
                }

                return;
            }

            EnterMainApplication();
        }
        public void RefreshDatabaseFooter()
        {
            if (DatabaseModeFooterTextBlock == null)
                return;

            var settings = DatabaseConnectionSettingsService.Load();

            if (settings.ProviderMode == DatabaseProviderMode.SqlServer)
            {
                string databaseName = "SQL Server";

                try
                {
                    var builder = new SqlConnectionStringBuilder(settings.SqlServerConnectionString);

                    if (!string.IsNullOrWhiteSpace(builder.InitialCatalog))
                    {
                        databaseName = builder.InitialCatalog;
                    }
                }
                catch
                {
                    databaseName = "SQL Server";
                }

                DatabaseModeFooterTextBlock.Text =
                    $"База: SQL Server\n{databaseName}";

                return;
            }

            DatabaseModeFooterTextBlock.Text =
                "База: SQLite\nЛокальная база";
        }
        public void EnterMainApplication()
        {
            ActiveOrganizationService.RefreshCurrent();
            RefreshOrganizationFooter();
            RefreshDatabaseFooter();
            ClientContractService.EnsureContractsForActiveOrganization();

            DashboardNavItem.IsSelected = true;

            if (RootFrame.CurrentSourcePageType != typeof(DashboardPage))
            {
                RootFrame.Navigate(typeof(DashboardPage));
            }
        }

        private void AppNavigationView_PaneOpening(NavigationView sender, object args)
        {
            if (PaneFooterContent != null)
            {
                PaneFooterContent.Visibility = Visibility.Visible;
            }
        }

        private void AppNavigationView_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        {
            if (PaneFooterContent != null)
            {
                PaneFooterContent.Visibility = Visibility.Collapsed;
            }
        }

        public void RefreshOrganizationFooter()
        {
            if (ActiveOrganizationFooterTextBlock == null)
                return;

            var organization = ActiveOrganizationService.Current;

            if (organization == null)
            {
                ActiveOrganizationFooterTextBlock.Text = "Организация не выбрана";
                return;
            }

            string innText = string.IsNullOrWhiteSpace(organization.Inn)
                ? ""
                : $"ИНН {organization.Inn}";

            ActiveOrganizationFooterTextBlock.Text = string.IsNullOrWhiteSpace(innText)
                ? organization.Name
                : $"{organization.Name}\n{innText}";
        }

        private void OpenOrganizationProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveOrganizationService.Current == null)
            {
                RootFrame.Navigate(typeof(OrganizationSetupPage), "new");
                return;
            }

            RootFrame.Navigate(typeof(OrganizationSetupPage));
        }

        private void SwitchOrganizationButton_Click(object sender, RoutedEventArgs e)
        {
            RootFrame.Navigate(typeof(OrganizationSelectPage));
        }

        private void AppNavigationView_SelectionChanged(
            NavigationView sender,
            NavigationViewSelectionChangedEventArgs args)
        {
            // Settings должен открываться всегда,
            // даже если рабочая организация ещё не выбрана.
            if (args.IsSettingsSelected)
            {
                if (RootFrame.CurrentSourcePageType != typeof(SettingsPage))
                {
                    RootFrame.Navigate(typeof(SettingsPage));
                }

                return;
            }

            // Остальные разделы требуют выбранной рабочей организации.
            if (ActiveOrganizationService.Current == null)
            {
                if (ActiveOrganizationService.HasAnyOrganizations())
                {
                    RootFrame.Navigate(typeof(OrganizationSelectPage));
                }
                else
                {
                    RootFrame.Navigate(typeof(OrganizationSetupPage), "new");
                }

                return;
            }

            if (args.SelectedItemContainer is not NavigationViewItem selectedItem)
                return;

            string tag = selectedItem.Tag?.ToString() ?? string.Empty;

            Type targetPage = tag switch
            {
                "dashboard" => typeof(DashboardPage),
                "clients" => typeof(LegacyWorkspacePage),
                "contracts" => typeof(ContractsPage),
                "problem-signatures" => typeof(ProblemSignaturesPage),
                "tools" => typeof(ToolsPage),
                "billing" => typeof(BillingPage),
                "catalog" => typeof(ServiceCatalogPage),
                _ => typeof(DashboardPage)
            };

            if (RootFrame.CurrentSourcePageType != targetPage)
            {
                RootFrame.Navigate(targetPage);
            }
        }
    }
}