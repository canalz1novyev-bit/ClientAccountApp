using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using Windows.Storage.Pickers;
using WinRT.Interop;


namespace ClientAccountApp
{
    public sealed partial class DataStorageSettingsPage : Page
    {
        public DataStorageSettingsPage()
        {
            this.InitializeComponent();
            RefreshStorageInfo();
        }

        private void RefreshStorageInfo()
        {
            CurrentStorageRootTextBox.Text = AppPaths.StorageRoot;
            DefaultStorageRootTextBox.Text = AppPaths.DefaultStorageRoot;

            StorageModeTextBlock.Text = AppPaths.IsCustomStorage
                ? "Сейчас используется внешнее хранилище данных."
                : "Сейчас используется локальное хранилище приложения.";

            NewStorageRootTextBox.Text = StorageLocationService.GetCustomStorageRoot() ?? "";
        }

        private void OpenCurrentStorageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.StorageRoot);

                Process.Start(new ProcessStartInfo
                {
                    FileName = AppPaths.StorageRoot,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StorageStatusTextBlock.Text = "Не удалось открыть папку хранения: " + ex.Message;
            }
        }

        private void OpenStorageSettingsFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string settingsFile = StorageLocationService.GetSettingsFilePath();
                string settingsFolder = Path.GetDirectoryName(settingsFile) ?? "";

                Directory.CreateDirectory(settingsFolder);

                Process.Start(new ProcessStartInfo
                {
                    FileName = settingsFolder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StorageStatusTextBlock.Text = "Не удалось открыть папку файла настройки: " + ex.Message;
            }
        }
        private async void PickStorageFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };

                picker.FileTypeFilter.Add("*");

                var ownerWindow = ShellWindow.CurrentWindow;

                if (ownerWindow == null)
                {
                    StorageStatusTextBlock.Text = "Не удалось получить главное окно приложения для выбора папки.";
                    return;
                }

                var hwnd = WindowNative.GetWindowHandle(ownerWindow);
                InitializeWithWindow.Initialize(picker, hwnd);

                var folder = await picker.PickSingleFolderAsync();

                if (folder == null)
                {
                    StorageStatusTextBlock.Text = "Выбор папки отменён.";
                    return;
                }

                NewStorageRootTextBox.Text = folder.Path;

                StorageStatusTextBlock.Text =
                    "Папка выбрана. Теперь нажми «Перенести текущую базу и файлы» или «Сохранить новую папку».";
            }
            catch (Exception ex)
            {
                StorageStatusTextBlock.Text = "Не удалось выбрать папку: " + ex.Message;
            }
        }
        private void SaveNewStorageRootButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = (NewStorageRootTextBox.Text ?? "").Trim().Trim('"');

                if (string.IsNullOrWhiteSpace(path))
                {
                    StorageStatusTextBlock.Text = "Укажи путь к новой папке хранения.";
                    return;
                }

                StorageLocationService.SaveCustomStorageRoot(path);

                StorageStatusTextBlock.Text =
                    "Новая папка хранения сохранена. Перезапусти приложение, чтобы оно начало работать с этой папкой.";

                RefreshStorageInfo();
            }
            catch (Exception ex)
            {
                StorageStatusTextBlock.Text = "Не удалось сохранить новую папку хранения: " + ex.Message;
            }
        }
        private void CopyCurrentStorageToNewRootButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = (NewStorageRootTextBox.Text ?? "").Trim().Trim('"');

                if (string.IsNullOrWhiteSpace(path))
                {
                    StorageStatusTextBlock.Text = "Укажи путь к новой папке хранения.";
                    return;
                }

                StorageMigrationService.CopyCurrentStorageTo(path);
                StorageLocationService.SaveCustomStorageRoot(path);

                StorageStatusTextBlock.Text =
                    "Текущая база, файлы клиентов и резервные копии перенесены в новую папку. " +
                    "Перезапусти приложение, чтобы оно начало работать с этим хранилищем.";

                RefreshStorageInfo();
            }
            catch (Exception ex)
            {
                StorageStatusTextBlock.Text = "Не удалось перенести хранилище: " + ex.Message;
            }
        }
        private void ResetToLocalStorageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StorageLocationService.ClearCustomStorageRoot();

                StorageStatusTextBlock.Text =
                    "Настройка внешнего хранилища сброшена. Перезапусти приложение, чтобы вернуться к локальной базе.";

                RefreshStorageInfo();
            }
            catch (Exception ex)
            {
                StorageStatusTextBlock.Text = "Не удалось сбросить настройку хранения: " + ex.Message;
            }
        }
    }
}