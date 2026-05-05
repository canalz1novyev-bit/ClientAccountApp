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

        public ShellWindow()
        {
            CurrentWindow = this;
            AppShell = this;

            this.InitializeComponent();

            if (PaneFooterContent != null && !AppNavigationView.IsPaneOpen)
            {
                PaneFooterContent.Visibility = Visibility.Collapsed;
            }

            bool startupOk = InitializeStartupServicesSafely();

            this.Title = "ClientAccountApp — Версия 2";

            try
            {
                RefreshOrganizationFooter();
            }
            catch
            {
            }

            try
            {
                RefreshDatabaseFooter();
            }
            catch
            {
            }

            if (!startupOk)
            {
                RootFrame.Navigate(typeof(SettingsPage));
                return;
            }

            if (ActiveOrganizationService.Current == null)
            {
                bool hasOrganizations = false;

                try
                {
                    hasOrganizations = ActiveOrganizationService.HasAnyOrganizations();
                }
                catch
                {
                    RootFrame.Navigate(typeof(SettingsPage));
                    return;
                }

                if (hasOrganizations)
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
        private bool InitializeStartupServicesSafely()
        {
            try
            {
                OrganizationSchemaService.EnsureOrganizationTables();
            }
            catch (Exception ex)
            {
                WriteShellStartupLog("OrganizationSchemaService error: " + ex);
                return false;
            }

            try
            {
                ActiveOrganizationService.Initialize();
            }
            catch (Exception ex)
            {
                WriteShellStartupLog("ActiveOrganizationService error: " + ex);
                return false;
            }

            try
            {
                ContractSchemaService.EnsureContractTables();
            }
            catch (Exception ex)
            {
                WriteShellStartupLog("ContractSchemaService error: " + ex);
                return false;
            }

            try
            {
                ClientFileStorageService.MigrateLegacyClientFoldersOnce();
            }
            catch (Exception ex)
            {
                WriteShellStartupLog("ClientFileStorageService migration warning: " + ex);
                // Это не критично для запуска окна.
            }

            return true;
        }

        private static void WriteShellStartupLog(string message)
        {
            try
            {
                string folder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ClientAccountApp",
                    "Logs");

                System.IO.Directory.CreateDirectory(folder);

                string path = System.IO.Path.Combine(folder, "startup-log.txt");

                System.IO.File.AppendAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message + Environment.NewLine);
            }
            catch
            {
            }
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
                _ => typeof(DashboardPage)
            };

            if (RootFrame.CurrentSourcePageType != targetPage)
            {
                RootFrame.Navigate(targetPage);
            }
        }
    }
}