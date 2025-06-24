using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace TNS_TOEICTest.Hubs
{
    public class ChatHub : Hub
    {
        public async Task JoinConversation(string conversationKey)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, conversationKey);
        }

        public async Task LeaveConversation(string conversationKey)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationKey);
        }

        public async Task SendMessage(string conversationKey, string userKey, string message)
        {
            await Clients.Group(conversationKey).SendAsync("ReceiveMessage", userKey, message);
        }

        public async Task UpdateUnreadCount(string conversationKey, string userKey, int unreadCount)
        {
            await Clients.User(userKey).SendAsync("ReceiveUnreadCount", conversationKey, unreadCount);
        }

        public async Task NotifyApproval(string conversationKey, string userKey)
        {
            await Clients.User(userKey).SendAsync("ReceiveApproval", conversationKey);
        }

        public override async Task OnConnectedAsync()
        {
            string userKey = Context.User?.FindFirst("UserKey")?.Value;
            if (!string.IsNullOrEmpty(userKey))
            {
                await Clients.Caller.SendAsync("ConnectionEstablished", userKey);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}