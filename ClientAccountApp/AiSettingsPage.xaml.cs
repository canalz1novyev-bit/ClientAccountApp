using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ClientAccountApp
{
    public sealed partial class AiSettingsPage : Page
    {
        public AiSettingsPage()
        {
            this.InitializeComponent();
            LoadAiSettingsIntoForm();
        }

        private void LoadAiSettingsIntoForm()
        {
            var settings = AiSettingsService.Load();

            GigaChatEnabledCheckBox.IsChecked = settings.IsEnabled;
            GigaChatOAuthUrlTextBox.Text = settings.GigaChatOAuthUrl;
            GigaChatApiUrlTextBox.Text = settings.GigaChatApiUrl;
            GigaChatModelTextBox.Text = settings.GigaChatModel;

            SelectGigaChatScope(settings.GigaChatScope);

            GigaChatAuthorizationKeyPasswordBox.Password = AiSettingsService.HasGigaChatAuthorizationKey()
                ? "********"
                : "";

            AiSettingsStatusTextBlock.Text = AiSettingsService.HasGigaChatAuthorizationKey()
                ? "GigaChat настроен. Можно проверить подключение."
                : "Введите Authorization Key GigaChat и сохраните настройки.";
        }

        private void SaveAiSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = AiSettingsService.Load();

            settings.Provider = "GigaChat";
            settings.IsEnabled = GigaChatEnabledCheckBox.IsChecked == true;
            settings.GigaChatOAuthUrl = GigaChatOAuthUrlTextBox.Text.Trim();
            settings.GigaChatApiUrl = GigaChatApiUrlTextBox.Text.Trim();
            settings.GigaChatModel = GigaChatModelTextBox.Text.Trim();
            settings.GigaChatScope = GetSelectedGigaChatScope();

            AiSettingsService.Save(settings);

            string authorizationKey = GigaChatAuthorizationKeyPasswordBox.Password.Trim();

            if (!string.IsNullOrWhiteSpace(authorizationKey) && authorizationKey != "********")
            {
                AiSettingsService.SaveGigaChatAuthorizationKey(authorizationKey);
            }

            AiSettingsStatusTextBlock.Text = "Настройки GigaChat сохранены.";
        }

        private async void TestGigaChatButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveAiSettingsButton_Click(sender, e);

                AiSettingsStatusTextBlock.Text = "Проверяю подключение к GigaChat...";

                string response = await GigaChatAiService.TestConnectionAsync();

                AiSettingsStatusTextBlock.Text = $"Подключение работает. Ответ: {response}";
            }
            catch (Exception ex)
            {
                AiSettingsStatusTextBlock.Text = $"Ошибка проверки GigaChat: {ex.Message}";
            }
        }

        private string GetSelectedGigaChatScope()
        {
            if (GigaChatScopeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Content?.ToString() ?? "GIGACHAT_API_PERS";
            }

            return "GIGACHAT_API_PERS";
        }

        private void SelectGigaChatScope(string? scope)
        {
            string target = string.IsNullOrWhiteSpace(scope)
                ? "GIGACHAT_API_PERS"
                : scope;

            foreach (var rawItem in GigaChatScopeComboBox.Items)
            {
                if (rawItem is ComboBoxItem item &&
                    string.Equals(item.Content?.ToString(), target, StringComparison.OrdinalIgnoreCase))
                {
                    GigaChatScopeComboBox.SelectedItem = item;
                    return;
                }
            }

            if (GigaChatScopeComboBox.Items.Count > 0)
                GigaChatScopeComboBox.SelectedIndex = 0;
        }

        private void BackToSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }
    }
}