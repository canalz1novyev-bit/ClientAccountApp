namespace ClientAccountApp
{
    public sealed class DashboardClientNavigationRequest
    {
        public int ClientId { get; set; }

        public DashboardClientNavigationRequest()
        {
        }

        public DashboardClientNavigationRequest(int clientId)
        {
            ClientId = clientId;
        }
    }
}