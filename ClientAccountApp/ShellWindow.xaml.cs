using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Microsoft.Data.SqlClient;
using Windows.UI;

namespace ClientAccountApp
{
    public sealed partial class ShellWindow : Window
    {
        public static Window? CurrentWindow { get; private set; }
        public static ShellWindow? AppShell { get; private set; }

        // Цвета панели навигации
        private static readonly Color NavDark = Color.FromArgb(255, 11, 15, 23); // #0B0F17
        private static readonly Color NavLight = Color.FromArgb(255, 26, 39, 68); // #1A2744 Navy
        private static readonly Color NavMilitary = Color.FromArgb(255, 20, 40, 12); // #14280C тёмно-оливковый

        private static readonly string[] NavKeys =
        {
            "NavigationViewExpandedPaneBackground",
            "NavigationViewDefaultPaneBackground",
            "NavigationViewTopPaneBackground"
        };

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

            ThemeService.ThemeChanged += OnThemeChanged;

            // ★ Применяем тему заново при каждой навигации
            RootFrame.Navigated += (s, e) =>
            {
                RootFrame.RequestedTheme = ThemeService.CurrentTheme == ThemeService.ThemeLight
                    ? ElementTheme.Light
                    : ElementTheme.Dark;
            };

            if (PaneFooterContent != null && !AppNavigationView.IsPaneOpen)
                PaneFooterContent.Visibility = Visibility.Collapsed;

            ActiveOrganizationService.Initialize();
            ClientFileStorageService.MigrateLegacyClientFoldersOnce();

            this.Title = "NIATEC.Client v1.1";

            ApplyFullTheme(ThemeService.CurrentTheme);

            RefreshOrganizationFooter();
            RefreshDatabaseFooter();

            if (ActiveOrganizationService.Current == null)
            {
                if (ActiveOrganizationService.HasAnyOrganizations())
                    RootFrame.Navigate(typeof(OrganizationSelectPage));
                else
                    RootFrame.Navigate(typeof(OrganizationSetupPage), "new");
                return;
            }

            EnterMainApplication();
        }

        private void OnThemeChanged(string theme)
        {
            DispatcherQueue.TryEnqueue(() => ApplyFullTheme(theme));
        }

        private void ApplyFullTheme(string theme)
        {
            // Системные WinUI контролы (TextBox, ComboBox и т.д.)
            RootFrame.RequestedTheme = theme == ThemeService.ThemeLight
                ? ElementTheme.Light
                : ElementTheme.Dark;

            // Цвет панели навигации
            UpdateNavPaneColor(theme);

            // Камуфляжный слой
            UpdateCamoOverlay(theme);
        }

        // Меняем Color у существующих SolidColorBrush — работает надёжно
        private void UpdateNavPaneColor(string theme)
        {
            Color color = theme switch
            {
                ThemeService.ThemeLight => NavLight,
                ThemeService.ThemeMilitary => NavMilitary,
                _ => NavDark
            };

            foreach (var key in NavKeys)
            {
                if (AppNavigationView.Resources.TryGetValue(key, out var obj) &&
                    obj is SolidColorBrush brush)
                {
                    brush.Color = color;
                }
            }
        }

        // Камуфляжный паттерн поверх тёмно-оливкового фона
        private void UpdateCamoOverlay(string theme)
        {
            if (NavCamoImage == null) return;

            if (theme == ThemeService.ThemeMilitary)
            {
                NavCamoImage.Source = CamoThemeHelper.CreateM81Bitmap();
                NavCamoImage.Opacity = 0.55; // полупрозрачный поверх оливкового
                NavCamoImage.Visibility = Visibility.Visible;
            }
            else
            {
                NavCamoImage.Visibility = Visibility.Collapsed;
                NavCamoImage.Source = null;
            }
        }

        public void RefreshDatabaseFooter()
        {
            if (DatabaseModeFooterTextBlock == null) return;
            var settings = DatabaseConnectionSettingsService.Load();
            if (settings.ProviderMode == DatabaseProviderMode.SqlServer)
            {
                string db = "SQL Server";
                try
                {
                    var b = new SqlConnectionStringBuilder(settings.SqlServerConnectionString);
                    if (!string.IsNullOrWhiteSpace(b.InitialCatalog)) db = b.InitialCatalog;
                }
                catch { }
                DatabaseModeFooterTextBlock.Text = $"База: SQL Server\n{db}";
                return;
            }
            DatabaseModeFooterTextBlock.Text = "База: SQLite\nЛокальная база";
        }

        public void EnterMainApplication()
        {
            ActiveOrganizationService.RefreshCurrent();
            RefreshOrganizationFooter();
            RefreshDatabaseFooter();
            ClientContractService.EnsureContractsForActiveOrganization();
            DashboardNavItem.IsSelected = true;
            if (RootFrame.CurrentSourcePageType != typeof(DashboardPage))
                RootFrame.Navigate(typeof(DashboardPage));
        }

        private void AppNavigationView_PaneOpening(NavigationView sender, object args)
        {
            if (PaneFooterContent != null) PaneFooterContent.Visibility = Visibility.Visible;
        }

        private void AppNavigationView_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        {
            if (PaneFooterContent != null) PaneFooterContent.Visibility = Visibility.Collapsed;
        }

        public void RefreshOrganizationFooter()
        {
            if (ActiveOrganizationFooterTextBlock == null) return;
            var org = ActiveOrganizationService.Current;
            if (org == null) { ActiveOrganizationFooterTextBlock.Text = "Организация не выбрана"; return; }
            string inn = string.IsNullOrWhiteSpace(org.Inn) ? "" : $"ИНН {org.Inn}";
            ActiveOrganizationFooterTextBlock.Text = string.IsNullOrWhiteSpace(inn)
                ? org.Name : $"{org.Name}\n{inn}";
        }

        private void OpenOrganizationProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveOrganizationService.Current == null)
            { RootFrame.Navigate(typeof(OrganizationSetupPage), "new"); return; }
            RootFrame.Navigate(typeof(OrganizationSetupPage));
        }

        private void SwitchOrganizationButton_Click(object sender, RoutedEventArgs e)
        {
            RootFrame.Navigate(typeof(OrganizationSelectPage));
        }

        private void AppNavigationView_SelectionChanged(
            NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                if (RootFrame.CurrentSourcePageType != typeof(SettingsPage))
                    RootFrame.Navigate(typeof(SettingsPage));
                return;
            }
            if (ActiveOrganizationService.Current == null)
            {
                if (ActiveOrganizationService.HasAnyOrganizations())
                    RootFrame.Navigate(typeof(OrganizationSelectPage));
                else
                    RootFrame.Navigate(typeof(OrganizationSetupPage), "new");
                return;
            }
            if (args.SelectedItemContainer is not NavigationViewItem item) return;
            string tag = item.Tag?.ToString() ?? string.Empty;
            Type page = tag switch
            {
                "dashboard" => typeof(DashboardPage),
                "clients" => typeof(ClientsPage),
                "contracts" => typeof(ContractsPage),
                "problem-signatures" => typeof(ProblemSignaturesPage),
                "tools" => typeof(ToolsPage),
                "rsv"   => typeof(RsvPage),
                "billing" => typeof(BillingPage),
                "catalog" => typeof(ServiceCatalogPage),
                _ => typeof(DashboardPage)
            };
            if (RootFrame.CurrentSourcePageType != page) RootFrame.Navigate(page);
        }
    }
}