using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Threading;
using WinRT;

namespace ClientAccountApp
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            ComWrappersSupport.InitializeComWrappers();

            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());

                SynchronizationContext.SetSynchronizationContext(context);

                new App();
            });
        }
    }
}