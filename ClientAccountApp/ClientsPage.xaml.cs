using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ClientAccountApp
{
    public sealed partial class ClientsPage : Page
    {
        public ClientsPage()
        {
            this.InitializeComponent();
            Loaded += ClientsPage_Loaded;
        }

        private void ClientsPage_Loaded(object sender, RoutedEventArgs e)
        {
            UiStateService.Save(state =>
            {
                state.LastPageKey = "Clients";
            });
        }

        private void OpenLegacyWorkspaceButton_Click(object sender, RoutedEventArgs e)
        {
            UiStateService.Save(state =>
            {
                state.LastPageKey = "Clients";
            });

            Frame.Navigate(typeof(LegacyWorkspacePage));
        }
    }
}