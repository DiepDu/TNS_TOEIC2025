using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace TNS_TOEICAdmin.Hubs
{
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendNotification(string content)
        {
            await Clients.All.SendAsync("ReceiveNotification", content);
        }
    }
}