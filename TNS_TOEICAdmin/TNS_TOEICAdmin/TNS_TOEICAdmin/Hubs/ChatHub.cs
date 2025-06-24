using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace TNS_TOEICAdmin.Hubs
{
    public class ChatHub : Hub
    {
        // Kết nối client vào nhóm dựa trên ConversationKey
        public async Task JoinConversation(string conversationKey)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, conversationKey);
        }

        // Rời nhóm khi client ngắt kết nối
        public async Task LeaveConversation(string conversationKey)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationKey);
        }

        // Gửi tin nhắn đến nhóm (ConversationKey)
        public async Task SendMessage(string conversationKey, string userKey, string message)
        {
            await Clients.Group(conversationKey).SendAsync("ReceiveMessage", userKey, message);
        }

        // Cập nhật số tin chưa đọc
        public async Task UpdateUnreadCount(string conversationKey, string userKey, int unreadCount)
        {
            await Clients.User(userKey).SendAsync("ReceiveUnreadCount", conversationKey, unreadCount);
        }

        // Thông báo duyệt thành viên vào nhóm Private
        public async Task NotifyApproval(string conversationKey, string userKey)
        {
            await Clients.User(userKey).SendAsync("ReceiveApproval", conversationKey);
        }

        // Xử lý khi client kết nối
        public override async Task OnConnectedAsync()
        {
            // Lấy UserKey từ xác thực (nếu có)
            string userKey = Context.User?.FindFirst("UserKey")?.Value;
            if (!string.IsNullOrEmpty(userKey))
            {
                await Clients.Caller.SendAsync("ConnectionEstablished", userKey);
            }
            await base.OnConnectedAsync();
        }

        // Xử lý khi client ngắt kết nối
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}