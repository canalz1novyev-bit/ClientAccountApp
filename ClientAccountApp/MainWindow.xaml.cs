using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace ClientAccountApp
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // 🔥 Открываем приложение сразу на весь экран (правильный способ WinUI 3)
            this.Activated += (sender, args) =>
            {
                if (args.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated)
                {
                    var appWindow = this.AppWindow;
                    if (appWindow != null)
                    {
                        // Правильный способ — используем OverlappedPresenter
                        if (appWindow.Presenter is OverlappedPresenter presenter)
                        {
                            presenter.Maximize();
                        }
                    }
                }
            };
        }
    }
}