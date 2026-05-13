using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Reflection;
using Windows.System;
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

        private static ElementTheme ElementThemeForAppTheme(string theme) =>
            theme == ThemeService.ThemeLight ? ElementTheme.Light : ElementTheme.Dark;

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

            RootFrame.Navigated += RootFrame_Navigated;

            if (PaneFooterContent != null && !AppNavigationView.IsPaneOpen)
                PaneFooterContent.Visibility = Visibility.Collapsed;

            ActiveOrganizationService.Initialize();
            ClientFileStorageService.MigrateLegacyClientFoldersOnce();

            RegisterGlobalAccelerators();
            UpdateWindowTitle();

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
            RootFrame.RequestedTheme = ElementThemeForAppTheme(theme);

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
            if (DatabaseModeFooterTextBlock == null)
                return;

            var settings = DatabaseConnectionSettingsService.Load();

            if (settings.ProviderMode == DatabaseProviderMode.SqlServer)
            {
                string db = SqlConnectionStringDisplay.TryGetInitialCatalogOrNull(settings.SqlServerConnectionString)
                    ?? "SQL Server";

                DatabaseModeFooterTextBlock.Text =
                    "Режим: Совместная работа" +
                    Environment.NewLine +
                    db;

                return;
            }

            DatabaseModeFooterTextBlock.Text =
                "Режим: Локальный" +
                Environment.NewLine +
                "Сервер не используется";
        }

        public void EnterMainApplication()
        {
            ActiveOrganizationService.RefreshCurrent();
            RefreshOrganizationFooter();
            RefreshDatabaseFooter();
            UpdateWindowTitle();
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
                "billing" => typeof(BillingPage),
                "catalog" => typeof(ServiceCatalogPage),
                _ => typeof(DashboardPage)
            };
            if (RootFrame.CurrentSourcePageType != page) RootFrame.Navigate(page);
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Заголовок окна и горячие клавиши навигации
        // ──────────────────────────────────────────────────────────────────────

        private static readonly Lazy<string> AppVersion = new(() =>
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(info))
                {
                    int plus = info.IndexOf('+');
                    return plus > 0 ? info.Substring(0, plus) : info;
                }
                return asm.GetName().Version?.ToString(3) ?? "1.0";
            }
            catch { return "1.0"; }
        });

        private void RootFrame_Navigated(object sender, NavigationEventArgs e)
        {
            RootFrame.RequestedTheme = ElementThemeForAppTheme(ThemeService.CurrentTheme);
            UpdateWindowTitle();
        }

        public void UpdateWindowTitle()
        {
            string section = SectionNameFor(RootFrame.CurrentSourcePageType);
            string org = ActiveOrganizationService.Current?.Name?.Trim() ?? "";
            string version = AppVersion.Value;

            string title = string.IsNullOrEmpty(section) ? "NIATEC.Client" : $"{section} — NIATEC.Client";
            if (!string.IsNullOrEmpty(org))
                title = $"{section} — {org} — NIATEC.Client";
            this.Title = $"{title} v{version}";
        }

        private static string SectionNameFor(Type? pageType)
        {
            if (pageType == null) return "";
            if (pageType == typeof(DashboardPage)) return "Главная";
            if (pageType == typeof(ClientsPage)) return "Клиенты";
            if (pageType == typeof(ContractsPage)) return "Договоры";
            if (pageType == typeof(BillingPage)) return "Начисления";
            if (pageType == typeof(ProblemSignaturesPage)) return "ЭЦП";
            if (pageType == typeof(ServiceCatalogPage)) return "Справочник";
            if (pageType == typeof(ToolsPage)) return "Инструменты";
            if (pageType == typeof(SettingsPage)) return "Настройки";
            if (pageType == typeof(OrganizationSelectPage)) return "Выбор организации";
            if (pageType == typeof(OrganizationSetupPage)) return "Профиль организации";
            return "";
        }

        private void RegisterGlobalAccelerators()
        {
            // Ctrl+, → Настройки. Привязываем к корневому Frame, чтобы работало на всех страницах.
            var openSettings = new KeyboardAccelerator { Key = VirtualKey.OemComma, Modifiers = VirtualKeyModifiers.Control };
            openSettings.Invoked += (s, args) =>
            {
                args.Handled = true;
                if (RootFrame.CurrentSourcePageType != typeof(SettingsPage))
                    RootFrame.Navigate(typeof(SettingsPage));
            };
            RootFrame.KeyboardAccelerators.Add(openSettings);
        }

        private void NavigateByTag(string tag)
        {
            foreach (var item in AppNavigationView.MenuItems)
            {
                if (item is NavigationViewItem nvi && string.Equals(nvi.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    nvi.IsSelected = true;
                    return;
                }
            }
        }

        private void NavAccelerator_Dashboard_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { args.Handled = true; NavigateByTag("dashboard"); }
        private void NavAccelerator_Clients_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { args.Handled = true; NavigateByTag("clients"); }
        private void NavAccelerator_Contracts_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { args.Handled = true; NavigateByTag("contracts"); }
        private void NavAccelerator_Billing_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { args.Handled = true; NavigateByTag("billing"); }
        private void NavAccelerator_Signatures_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { args.Handled = true; NavigateByTag("problem-signatures"); }
        private void NavAccelerator_Catalog_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { args.Handled = true; NavigateByTag("catalog"); }
        private void NavAccelerator_Tools_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { args.Handled = true; NavigateByTag("tools"); }
    }
}