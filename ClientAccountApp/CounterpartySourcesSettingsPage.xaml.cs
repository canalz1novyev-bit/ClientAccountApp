using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public sealed partial class CounterpartySourcesSettingsPage : Page
    {
        public CounterpartySourcesSettingsPage()
        {
            this.InitializeComponent();
            LoadSettingsIntoForm();
        }

        private void LoadSettingsIntoForm()
        {
            var settings = CounterpartyProviderSettingsService.Load();

            // DaData
            DaDataApiKeyPasswordBox.Password = CounterpartyApiKeysService.HasDaDataApiKey() ? "********" : "";
            DaDataApiKeyStatusTextBlock.Text = CounterpartyApiKeysService.HasDaDataApiKey()
                ? "✓ Ключ DaData сохранён в защищённом хранилище."
                : "Ключ DaData не настроен — автопроверка по ИНН недоступна.";

            KadApiEnabledCheckBox.IsChecked = settings.KadApiEnabled;
            KadProviderNameTextBox.Text = settings.KadProviderName;
            KadApiUrlTextBox.Text = settings.KadApiUrl;
            KadApiKeyHeaderNameTextBox.Text = string.IsNullOrWhiteSpace(settings.KadApiKeyHeaderName)
                ? "Authorization"
                : settings.KadApiKeyHeaderName;

            KadApiKeyPrefixTextBox.Text = string.IsNullOrWhiteSpace(settings.KadApiKeyPrefix)
                ? "Bearer"
                : settings.KadApiKeyPrefix;

            SelectHttpMethod(settings.KadHttpMethod);

            KadApiKeyPasswordBox.Password = CounterpartyProviderSettingsService.HasKadApiKey()
                ? "********"
                : "";

            CounterpartySourcesStatusTextBlock.Text = settings.KadApiEnabled
                ? "API-поставщик КАД включён."
                : "API-поставщик КАД выключен. Будет использоваться экспериментальная прямая проверка.";
        }

        private void SaveKadProviderSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            CounterpartySourcesStatusTextBlock.Text = "Настройки API-поставщика КАД сохранены.";
        }

        private void TestKadProviderSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            CounterpartySourcesStatusTextBlock.Text =
                "Проверка через API-поставщика КАД сейчас отключена.\n\n" +
                "Текущий режим проверки контрагента использует DaData по ИНН.\n\n" +
                "КАД, ФССП, Федресурс и ГИР БО временно убраны из автоматической проверки, чтобы сначала стабильно запустить проверку через DaData.";
        }

        private void SaveSettings()
        {
            var settings = CounterpartyProviderSettingsService.Load();

            settings.KadApiEnabled = KadApiEnabledCheckBox.IsChecked == true;
            settings.KadProviderName = KadProviderNameTextBox.Text.Trim();
            settings.KadApiUrl = KadApiUrlTextBox.Text.Trim();
            settings.KadHttpMethod = GetSelectedHttpMethod();
            settings.KadApiKeyHeaderName = KadApiKeyHeaderNameTextBox.Text.Trim();
            settings.KadApiKeyPrefix = KadApiKeyPrefixTextBox.Text.Trim();

            string apiKey = KadApiKeyPasswordBox.Password.Trim();

            if (!string.IsNullOrWhiteSpace(apiKey) && apiKey != "********")
            {
                settings.KadApiKey = apiKey;
            }

            CounterpartyProviderSettingsService.Save(settings);
        }

        private string GetSelectedHttpMethod()
        {
            if (KadHttpMethodComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Content?.ToString() ?? "POST";
            }

            return "POST";
        }

        private void SelectHttpMethod(string? method)
        {
            string target = string.IsNullOrWhiteSpace(method)
                ? "POST"
                : method.Trim();

            foreach (var rawItem in KadHttpMethodComboBox.Items)
            {
                if (rawItem is ComboBoxItem item &&
                    string.Equals(item.Content?.ToString(), target, StringComparison.OrdinalIgnoreCase))
                {
                    KadHttpMethodComboBox.SelectedItem = item;
                    return;
                }
            }

            KadHttpMethodComboBox.SelectedIndex = 1;
        }

        private void SaveDaDataApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string raw = DaDataApiKeyPasswordBox.Password ?? "";

                // Маска "********" значит "ключ уже сохранён, не трогать"
                if (raw == "********")
                {
                    DaDataApiKeyStatusTextBlock.Text = "Ключ DaData оставлен без изменений.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(raw))
                {
                    DaDataApiKeyStatusTextBlock.Text = "Введите ключ DaData или нажмите «Удалить».";
                    return;
                }

                CounterpartyApiKeysService.SaveDaDataApiKey(raw.Trim());
                DaDataApiKeyPasswordBox.Password = "********";
                DaDataApiKeyStatusTextBlock.Text = "✓ Ключ DaData сохранён в защищённом хранилище Windows.";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("CounterpartySourcesSettingsPage.SaveDaDataApiKey", ex);
                DaDataApiKeyStatusTextBlock.Text = "Не удалось сохранить ключ: " + ex.Message;
            }
        }

        private void ClearDaDataApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CounterpartyApiKeysService.SaveDaDataApiKey("");
                DaDataApiKeyPasswordBox.Password = "";
                DaDataApiKeyStatusTextBlock.Text = "Ключ DaData удалён.";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("CounterpartySourcesSettingsPage.ClearDaDataApiKey", ex);
                DaDataApiKeyStatusTextBlock.Text = "Не удалось удалить ключ: " + ex.Message;
            }
        }

        private void BackToSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }
    }
}