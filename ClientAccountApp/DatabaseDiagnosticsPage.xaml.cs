using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public sealed partial class DatabaseDiagnosticsPage : Page
    {
        public DatabaseDiagnosticsPage()
        {
            this.InitializeComponent();

            DiagnosticsStatusTextBlock.Text = "Диагностика готова к запуску.";
            DiagnosticsOutputTextBox.Text = "";
        }

        private async void RunDatabaseDiagnosticsButton_Click(object sender, RoutedEventArgs e)
        {
            await RunDiagnosticsAsync();
        }
        private async void SaveDiagnosticsReportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string reportText = DiagnosticsOutputTextBox.Text;

                if (string.IsNullOrWhiteSpace(reportText))
                {
                    DiagnosticsStatusTextBlock.Text = "Сначала выполните диагностику.";
                    return;
                }

                string reportsFolder = Path.Combine(
                    @"C:\NIATEC",
                    "DiagnosticsReports");

                Directory.CreateDirectory(reportsFolder);

                string fileName = "DatabaseDiagnostics_" +
                                  DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") +
                                  ".txt";

                string filePath = Path.Combine(reportsFolder, fileName);

                await File.WriteAllTextAsync(filePath, reportText);

                DiagnosticsStatusTextBlock.Text = $"Отчёт сохранён: {filePath}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = reportsFolder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                DiagnosticsStatusTextBlock.Text = "Ошибка сохранения отчёта: " + ex.Message;
            }
        }
        private async Task RunDiagnosticsAsync()
        {
            try
            {
                RunDatabaseDiagnosticsButton.IsEnabled = false;
                DiagnosticsStatusTextBlock.Text = "Выполняется диагностика базы и файлов...";
                DiagnosticsOutputTextBox.Text = "Пожалуйста, подождите. Идёт проверка базы, документов и файлового хранилища.";

                string report = await FileIntegrityDiagnosticsService.BuildReportAsync();

                DiagnosticsOutputTextBox.Text = report;

                if (report.Contains("Критических проблем с файлами не найдено."))
                {
                    DiagnosticsStatusTextBlock.Text = "Проверка завершена: критических проблем не найдено.";
                }
                else
                {
                    DiagnosticsStatusTextBlock.Text = "Проверка завершена: есть замечания, смотри отчёт ниже.";
                }
            }
            catch (Exception ex)
            {
                DiagnosticsStatusTextBlock.Text = "Ошибка диагностики.";

                DiagnosticsOutputTextBox.Text =
                    "Во время диагностики произошла ошибка:" +
                    Environment.NewLine +
                    Environment.NewLine +
                    ex.Message;
            }
            finally
            {
                RunDatabaseDiagnosticsButton.IsEnabled = true;
            }
        }
    }
}