using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.EntityFrameworkCore;



// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ClientAccountApp
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        public static Window? MainWindow { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }
        public static Window? MainAppWindow { get; private set; }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            WriteAppLog("OnLaunched start");

            try
            {
                EnsureDatabaseSchema();
                WriteAppLog("EnsureDatabaseSchema ok");
            }
            catch (Exception ex)
            {
                WriteAppLog("EnsureDatabaseSchema error: " + ex);
            }

            try
            {
                WriteAppLog("Before ShellWindow");

                MainAppWindow = new ShellWindow();

                WriteAppLog("After ShellWindow");

                MainAppWindow.Activate();

                WriteAppLog("After Activate");
            }
            catch (Exception ex)
            {
                WriteAppLog("ShellWindow error: " + ex);
                throw;
            }
        }

        private static void WriteAppLog(string message)
        {
            try
            {
                string folder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ClientAccountApp",
                    "Logs");

                Directory.CreateDirectory(folder);

                string path = System.IO.Path.Combine(folder, "startup-log.txt");

                File.AppendAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message + Environment.NewLine);
            }
            catch
            {
                // логирование не должно ломать запуск
            }
        }
        private void EnsureDatabaseSchema()
        {
            using var db = new AppDbContext();

            db.Database.EnsureCreated();

            string providerName = db.Database.ProviderName ?? "";

            if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                db.Database.ExecuteSqlRaw(@"
ALTER TABLE Clients
ADD COLUMN Ogrn TEXT NOT NULL DEFAULT '';
");
            }
            catch
            {
            }
        }
    }
    
    
}
