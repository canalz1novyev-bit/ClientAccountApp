using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace ClientAccountApp
{
    public sealed partial class ServiceCatalogPage : Page
    {
        // ViewModel для отображения в списке
        private class CatalogListItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Unit { get; set; } = "усл.";
            public string PriceText { get; set; } = "";
            public string VatText { get; set; } = "";
            public decimal DefaultPrice { get; set; }
            public decimal DefaultVatRate { get; set; }
            public string Comment { get; set; } = "";
        }

        private readonly ObservableCollection<CatalogListItem> _items = new();
        private int? _selectedServiceId;

        public ServiceCatalogPage()
        {
            InitializeComponent();
            CatalogListView.ItemsSource = _items;
            Loaded += (_, _) => LoadCatalog();
        }

        // ─── Загрузка ─────────────────────────────────────────────────────────

        private void LoadCatalog(int? selectId = null)
        {
            _items.Clear();

            using var db = new AppDbContext();

            var services = db.ServicesCatalog
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToList();

            foreach (var s in services)
            {
                _items.Add(new CatalogListItem
                {
                    Id = s.Id,
                    Name = s.Name,
                    Unit = string.IsNullOrWhiteSpace(s.Unit) ? "усл." : s.Unit,
                    DefaultPrice = s.DefaultPrice,
                    DefaultVatRate = s.DefaultVatRate,
                    Comment = s.Comment ?? "",
                    PriceText = s.DefaultPrice.ToString("N2", new CultureInfo("ru-RU")) + " ₽",
                    VatText = s.DefaultVatRate > 0 ? $"НДС {s.DefaultVatRate:0.##}%" : "Без НДС"
                });
            }

            CatalogCountTextBlock.Text = services.Count switch
            {
                0 => "Справочник пустой",
                1 => "1 услуга",
                var n when n % 10 == 1 && n % 100 != 11 => $"{n} услуга",
                var n when n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 10 || n % 100 >= 20) => $"{n} услуги",
                var n => $"{n} услуг"
            };

            if (selectId.HasValue)
            {
                var item = _items.FirstOrDefault(x => x.Id == selectId.Value);
                if (item != null)
                    CatalogListView.SelectedItem = item;
            }
        }

        // ─── Выбор услуги ─────────────────────────────────────────────────────

        private void CatalogListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CatalogListView.SelectedItem is not CatalogListItem item)
                return;

            _selectedServiceId = item.Id;
            LoadServiceIntoEditor(item.Id);
        }

        private void LoadServiceIntoEditor(int serviceId)
        {
            using var db = new AppDbContext();
            var service = db.ServicesCatalog.FirstOrDefault(x => x.Id == serviceId);
            if (service == null) return;

            ServiceNameTextBox.Text = service.Name;
            ServicePriceTextBox.Text = service.DefaultPrice.ToString("0.##");
            ServiceUnitTextBox.Text = string.IsNullOrWhiteSpace(service.Unit) ? "усл." : service.Unit;
            ServiceVatRateTextBox.Text = service.DefaultVatRate.ToString("0.##");
            ServiceCommentTextBox.Text = service.Comment ?? "";

            EditorTitleTextBlock.Text = "Редактирование";
            EditorHintTextBlock.Text = $"Услуга: {service.Name}";
            DeleteServiceButton.IsEnabled = true;
            CatalogStatusTextBlock.Text = $"Открыта услуга «{service.Name}».";
        }

        private void EditServiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement el || el.Tag is not int id) return;
            _selectedServiceId = id;
            LoadServiceIntoEditor(id);
            var item = _items.FirstOrDefault(x => x.Id == id);
            if (item != null) CatalogListView.SelectedItem = item;
        }

        // ─── Кнопки ───────────────────────────────────────────────────────────

        private void NewServiceButton_Click(object sender, RoutedEventArgs e)
        {
            ClearEditor();
        }

        private void SaveServiceButton_Click(object sender, RoutedEventArgs e)
        {
            string name = ServiceNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                CatalogStatusTextBlock.Text = "Укажи название услуги.";
                return;
            }

            if (!TryParseDecimal(ServicePriceTextBox.Text, out decimal price) || price < 0)
            {
                CatalogStatusTextBlock.Text = "Цена указана неверно.";
                return;
            }

            string unit = string.IsNullOrWhiteSpace(ServiceUnitTextBox.Text)
                ? "усл."
                : ServiceUnitTextBox.Text.Trim();

            if (!TryParseDecimal(ServiceVatRateTextBox.Text, out decimal vatRate) || vatRate < 0)
            {
                CatalogStatusTextBlock.Text = "Ставка НДС указана неверно.";
                return;
            }

            using var db = new AppDbContext();
            bool isNew = !_selectedServiceId.HasValue;
            ServiceCatalog service;

            if (isNew)
            {
                int nextSort = db.ServicesCatalog.Select(x => (int?)x.SortOrder).Max() ?? 0;
                service = new ServiceCatalog { SortOrder = nextSort + 1, CreatedAt = DateTime.Now };
                db.ServicesCatalog.Add(service);
            }
            else
            {
                service = db.ServicesCatalog.FirstOrDefault(x => x.Id == _selectedServiceId.Value);
                if (service == null) { CatalogStatusTextBlock.Text = "Услуга не найдена."; return; }
            }

            service.Name = name;
            service.DefaultPrice = price;
            service.Unit = unit;
            service.DefaultVatRate = vatRate;
            service.Comment = ServiceCommentTextBox.Text.Trim();
            service.IsActive = true;

            db.SaveChanges();
            _selectedServiceId = service.Id;

            LoadCatalog(service.Id);

            CatalogStatusTextBlock.Text = isNew
                ? $"Услуга «{service.Name}» добавлена."
                : $"Услуга «{service.Name}» обновлена.";

            EditorTitleTextBlock.Text = "Редактирование";
            EditorHintTextBlock.Text = $"Услуга: {service.Name}";
            DeleteServiceButton.IsEnabled = true;
        }

        private async void DeleteServiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedServiceId.HasValue) return;

            var dialog = new ContentDialog
            {
                Title = "Удалить услугу?",
                Content = "Услуга будет скрыта из справочника. Уже добавленные в счета строки останутся без изменений.",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            using var db = new AppDbContext();
            var service = db.ServicesCatalog.FirstOrDefault(x => x.Id == _selectedServiceId.Value);
            if (service == null) return;

            string deletedName = service.Name;
            service.IsActive = false;
            db.SaveChanges();

            ClearEditor();
            LoadCatalog();
            CatalogStatusTextBlock.Text = $"Услуга «{deletedName}» удалена.";
        }

        // ─── Вспомогательные ──────────────────────────────────────────────────

        private void ClearEditor()
        {
            _selectedServiceId = null;
            CatalogListView.SelectedItem = null;

            ServiceNameTextBox.Text = "";
            ServicePriceTextBox.Text = "";
            ServiceUnitTextBox.Text = "усл.";
            ServiceVatRateTextBox.Text = "0";
            ServiceCommentTextBox.Text = "";

            EditorTitleTextBlock.Text = "Новая услуга";
            EditorHintTextBlock.Text = "Заполни поля и нажми «Сохранить»";
            DeleteServiceButton.IsEnabled = false;
            CatalogStatusTextBlock.Text = "Заполни поля для новой услуги.";
        }

        private static bool TryParseDecimal(string? text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string normalized = text.Trim().Replace(" ", "").Replace(",", ".");
            return decimal.TryParse(normalized, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }
    }
}
