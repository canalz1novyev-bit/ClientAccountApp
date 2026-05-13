using Microsoft.EntityFrameworkCore;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ClientAccountApp
{
    public sealed partial class DashboardPage : Page
    {
        private static readonly CultureInfo RuCulture = new("ru-RU");

        public DashboardPage()
        {
            this.InitializeComponent();
            Loaded += DashboardPage_Loaded;
        }

        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDashboard();
        }

        private void RefreshDashboardButton_Click(object sender, RoutedEventArgs e)
        {
            LoadDashboard();
        }

        private void LoadDashboard()
        {
            try
            {
                var organization = ActiveOrganizationService.Current;

                ActiveOrganizationTextBlock.Text = organization == null
                    ? "Рабочая организация: не выбрана"
                    : $"Рабочая организация: {organization.Name} · ИНН {organization.Inn}";

                DashboardUpdatedAtTextBlock.Text = $"Обновлено: {DateTime.Now:dd.MM.yyyy HH:mm}";

                if (organization != null)
                {
                    ClientContractService.EnsureContractsForActiveOrganization();
                }

                using var db = new AppDbContext();

                int organizationId = organization?.Id ?? 0;
                DateTime today = DateTime.Today;

                var clients = db.Clients
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToList();

                var clientMap = clients.ToDictionary(
                    c => c.Id,
                    c => string.IsNullOrWhiteSpace(c.Name) ? "Клиент без названия" : c.Name);

                var signatures = db.DigitalSignatures
                    .AsNoTracking()
                    .ToList();

                var attentionSignatures = signatures
                    .Where(s => s.ExpiresDate.Date <= today.AddDays(30))
                    .OrderBy(s => s.ExpiresDate)
                    .ToList();

                var contracts = organizationId > 0
                    ? db.ClientContracts
                        .AsNoTracking()
                        .Where(c => c.OrganizationProfileId == organizationId)
                        .ToList()
                    : new List<ClientContract>();

                var contractsInWork = contracts
                    .Where(c => !string.Equals(c.Status, "Договор подписан", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var invoices = organizationId > 0
                    ? db.Invoices
                        .AsNoTracking()
                        .Where(i => i.OrganizationProfileId == organizationId)
                        .ToList()
                    : db.Invoices
                        .AsNoTracking()
                        .ToList();

                var unpaidInvoices = invoices
                    .Where(i =>
                        !string.Equals(i.Status, "Оплачен", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(i.Status, "Отменен", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(i.Status, "Отменён", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                decimal unpaidAmount = unpaidInvoices.Sum(i => i.TotalWithVat);

                ClientsTotalTextBlock.Text = clients.Count.ToString();
                SignaturesAttentionTextBlock.Text = attentionSignatures.Count.ToString();
                ContractsWorkTextBlock.Text = contractsInWork.Count.ToString();
                UnpaidInvoicesTextBlock.Text = unpaidInvoices.Count.ToString();
                UnpaidInvoicesAmountTextBlock.Text = $"К оплате: {FormatMoney(unpaidAmount)}";

                FillCategoryAnalyticsPanel(clients);
                FillAttentionPanel(attentionSignatures, contractsInWork, unpaidInvoices, clientMap, today);
                FillRecentPanel(clients, invoices, contracts, clientMap);
            }
            catch (Exception ex)
            {
                AttentionPanel.Children.Clear();
                AttentionPanel.Children.Add(CreateInfoRow(
                    "Ошибка загрузки сводки",
                    ex.Message,
                    "#7A2E2E"));
            }
        }

        private void FillCategoryAnalyticsPanel(List<ClientInfo> clients)
        {
            CategoryAnalyticsPanel.Children.Clear();

            int totalClients = clients.Count;

            if (CategoryAnalyticsTotalTextBlock != null)
                CategoryAnalyticsTotalTextBlock.Text = $"Всего: {totalClients}";

            if (totalClients == 0)
            {
                CategoryAnalyticsPanel.Children.Add(CreateInfoRow(
                    "Пока нет клиентов",
                    "После добавления клиентов здесь появится распределение по категориям бизнеса.",
                    "#2F4F6F"));

                return;
            }

            var categoryGroups = clients
                .GroupBy(c => string.IsNullOrWhiteSpace(c.BusinessCategory)
                    ? "Без категории"
                    : c.BusinessCategory.Trim())
                .Select(g => new
                {
                    Category = g.Key,
                    Count = g.Count(),
                    Percent = totalClients == 0
                        ? 0
                        : Math.Round((decimal)g.Count() * 100m / totalClients, 1)
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Category)
                .Take(8)
                .ToList();

            foreach (var item in categoryGroups)
            {
                CategoryAnalyticsPanel.Children.Add(
                    CreateCategoryAnalyticsRow(item.Category, item.Count, item.Percent, totalClients));
            }
        }

        private Border CreateCategoryAnalyticsRow(string category, int count, decimal percent, int totalClients)
        {
            double barWidth = 40;

            if (totalClients > 0)
            {
                double calculated = 320.0 * count / totalClients;
                barWidth = Math.Max(40, calculated);
            }

            var titleText = new TextBlock
            {
                Text = category,
                Foreground = ThemeBrush("NiatecTextPrimaryBrush", "#FFFFFF"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            };

            var countText = new TextBlock
            {
                Text = $"{count} клиент(ов) · {percent:N1}%",
                Foreground = ThemeBrush("NiatecTextSecondaryBrush", "#B8B8B8"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };

            var bar = new Border
            {
                Width = barWidth,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = BusinessCategoryBrush(category),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var barBack = new Border
            {
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = ThemeBrush("NiatecBackgroundBrush", "#0F1115"),
                Child = bar
            };

            var textGrid = new Grid
            {
                ColumnSpacing = 12
            };

            textGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            textGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(titleText, 0);
            Grid.SetColumn(countText, 1);

            textGrid.Children.Add(titleText);
            textGrid.Children.Add(countText);

            var panel = new StackPanel
            {
                Spacing = 8
            };

            panel.Children.Add(textGrid);
            panel.Children.Add(barBack);

            return new Border
            {
                Background = ThemeBrush("NiatecSurfaceAltBrush", "#11151C"),
                BorderBrush = ThemeBrush("NiatecBorderBrush", "#243042"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Child = panel
            };
        }

        private static SolidColorBrush BusinessCategoryBrush(string category)
        {
            if (category.Contains("Торговля", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(ColorHelper.FromArgb(255, 38, 96, 150));

            if (category.Contains("Перевозки", StringComparison.OrdinalIgnoreCase) ||
                category.Contains("логистика", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(ColorHelper.FromArgb(255, 97, 76, 150));

            if (category.Contains("С/Х", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(ColorHelper.FromArgb(255, 72, 125, 58));

            if (category.Contains("IT", StringComparison.OrdinalIgnoreCase) ||
                category.Contains("связь", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(ColorHelper.FromArgb(255, 35, 118, 145));

            if (category.Contains("Строительство", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(ColorHelper.FromArgb(255, 158, 99, 32));

            if (category.Contains("Производство", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(ColorHelper.FromArgb(255, 94, 103, 120));

            if (category.Contains("общепит", StringComparison.OrdinalIgnoreCase) ||
                category.Contains("Гостиницы", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(ColorHelper.FromArgb(255, 156, 83, 45));

            if (category.Contains("консалтинг", StringComparison.OrdinalIgnoreCase) ||
                category.Contains("бух", StringComparison.OrdinalIgnoreCase) ||
                category.Contains("Юр", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(ColorHelper.FromArgb(255, 122, 88, 160));

            if (category.Contains("Недвижимость", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(ColorHelper.FromArgb(255, 82, 98, 150));

            if (category.Contains("Медицина", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(ColorHelper.FromArgb(255, 48, 132, 110));

            if (category.Contains("Образование", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(ColorHelper.FromArgb(255, 120, 104, 45));

            if (category.Contains("Без категории", StringComparison.OrdinalIgnoreCase) ||
                category.Contains("Не указано", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(ColorHelper.FromArgb(255, 90, 96, 110));

            return new SolidColorBrush(ColorHelper.FromArgb(255, 80, 90, 110));
        }

        private void FillAttentionPanel(
            List<DigitalSignature> attentionSignatures,
            List<ClientContract> contractsInWork,
            List<Invoice> unpaidInvoices,
            Dictionary<int, string> clientMap,
            DateTime today)
        {
            AttentionPanel.Children.Clear();

            bool hasItems = false;

            foreach (var signature in attentionSignatures.Take(4))
            {
                hasItems = true;

                clientMap.TryGetValue(signature.ClientInfoId, out string? clientName);

                int daysLeft = (signature.ExpiresDate.Date - today).Days;

                string status = daysLeft < 0
                    ? $"ЭЦП просрочена на {Math.Abs(daysLeft)} дн."
                    : $"ЭЦП истекает через {daysLeft} дн.";

                string color = daysLeft < 0 ? "#7A2E2E" : "#7A5A22";

                AttentionPanel.Children.Add(CreateInfoRow(
                    clientName ?? "Клиент",
                    status,
                    color));
            }

            if (contractsInWork.Count > 0)
            {
                hasItems = true;

                AttentionPanel.Children.Add(CreateInfoRow(
                    "Договоры",
                    $"В работе: {contractsInWork.Count}. Проверьте договоры, которые требуют формирования или подписания.",
                    "#7A5A22"));
            }

            if (unpaidInvoices.Count > 0)
            {
                hasItems = true;

                decimal amount = unpaidInvoices.Sum(i => i.TotalWithVat);

                AttentionPanel.Children.Add(CreateInfoRow(
                    "Начисления",
                    $"Не оплачено счетов: {unpaidInvoices.Count}. Сумма: {FormatMoney(amount)}.",
                    "#7A5A22"));
            }

            if (!hasItems)
            {
                AttentionPanel.Children.Add(CreateInfoRow(
                    "Всё спокойно",
                    "На сегодня критичных задач не найдено.",
                    "#2F6F46"));
            }
        }

        private void FillRecentPanel(
            List<ClientInfo> clients,
            List<Invoice> invoices,
            List<ClientContract> contracts,
            Dictionary<int, string> clientMap)
        {
            RecentPanel.Children.Clear();

            bool hasItems = false;

            foreach (var client in clients.OrderByDescending(c => c.Id).Take(3))
            {
                hasItems = true;

                string category = string.IsNullOrWhiteSpace(client.BusinessCategory)
                    ? "Без категории"
                    : client.BusinessCategory;

                RecentPanel.Children.Add(CreateInfoRow(
                    "Клиент",
                    $"{client.Name} · ИНН {client.Inn} · {category}",
                    "#2F4F6F"));
            }

            foreach (var invoice in invoices.OrderByDescending(i => i.Id).Take(2))
            {
                hasItems = true;

                clientMap.TryGetValue(invoice.ClientInfoId, out string? clientName);

                RecentPanel.Children.Add(CreateInfoRow(
                    "Счёт",
                    $"{invoice.InvoiceNumber} · {clientName ?? "Клиент"} · {FormatMoney(invoice.TotalWithVat)}",
                    "#2F4F6F"));
            }

            foreach (var contract in contracts.OrderByDescending(c => c.UpdatedAt).Take(2))
            {
                hasItems = true;

                clientMap.TryGetValue(contract.ClientInfoId, out string? clientName);

                RecentPanel.Children.Add(CreateInfoRow(
                    "Договор",
                    $"{clientName ?? "Клиент"} · {contract.Status}",
                    "#2F4F6F"));
            }

            if (!hasItems)
            {
                RecentPanel.Children.Add(CreateInfoRow(
                    "Пока нет событий",
                    "После работы с клиентами, счетами и договорами здесь появятся последние действия.",
                    "#2F4F6F"));
            }
        }

        private Border CreateInfoRow(string title, string description, string colorHex)
        {
            return new Border
            {
                Background = ThemeBrush("NiatecSurfaceAltBrush", "#11151C"),
                BorderBrush = ThemeBrush("NiatecBorderBrush", "#243042"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Child = new Grid
                {
                    ColumnSpacing = 10,
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(6) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    },
                    Children =
                    {
                        CreateAccentBar(colorHex),
                        CreateTextStack(title, description)
                    }
                }
            };
        }

        private Border CreateAccentBar(string colorHex)
        {
            var border = new Border
            {
                Background = BrushFromHex(colorHex),
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            Grid.SetColumn(border, 0);
            return border;
        }

        private StackPanel CreateTextStack(string title, string description)
        {
            var panel = new StackPanel
            {
                Spacing = 4
            };

            panel.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = ThemeBrush("NiatecTextPrimaryBrush", "#FFFFFF"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });

            panel.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = ThemeBrush("NiatecTextSecondaryBrush", "#B8B8B8"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });

            Grid.SetColumn(panel, 1);
            return panel;
        }

        private static Brush ThemeBrush(string resourceKey, string fallbackHex)
        {
            if (Application.Current.Resources.TryGetValue(resourceKey, out object value) &&
                value is Brush brush)
            {
                return brush;
            }

            return BrushFromHex(fallbackHex);
        }

        private static SolidColorBrush BrushFromHex(string hex)
        {
            hex = hex.Replace("#", "");

            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);

            return new SolidColorBrush(ColorHelper.FromArgb(255, r, g, b));
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("N2", RuCulture) + " ₽";
        }

        private void OpenClientsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ClientsPage));
        }

        private void OpenSignaturesButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ProblemSignaturesPage));
        }

        private void OpenContractsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ContractsPage));
        }

        private void OpenBillingButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(BillingPage));
        }

        private void OpenToolsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ToolsPage));
        }
    }
}