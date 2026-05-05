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




// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ClientAccountApp
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        // Главное окно приложения. Используется в ToolsPage и LegacyWorkspacePage
        // для получения дескриптора окна при открытии файловых диалогов.
        public static Window? MainAppWindow { get; private set; }

        public App()
        {
            InitializeComponent();
        }   

        /// <summary>
        /// Invoked when the application is launched. 
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            WriteAppLog("OnLaunched start");

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
        
    }


}