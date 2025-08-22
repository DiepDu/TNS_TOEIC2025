// File: Hubs/ChatHub.cs

using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Threading.Tasks;
using TNS.Member;
using TNS_TOEICAdmin.Models;
using TNS_TOEICTest.Services;

namespace TNS_TOEICTest.Hubs
{
    public class ChatHub : Hub
    {
        // HOÀN TOÀN LOẠI BỎ DÒNG SAU:
        // private static readonly ConcurrentDictionary<string, string> _connectionMapping = new ConcurrentDictionary<string, string>();

        private readonly IUserConnectionManager _userConnectionManager;

        public ChatHub(IUserConnectionManager userConnectionManager)
        {
            _userConnectionManager = userConnectionManager;
        }

        // Hàm này đã đúng, lấy MemberKey khi kết nối
        public override Task OnConnectedAsync()
        {
            var memberKey = GetCurrentMemberKey();
            if (!string.IsNullOrEmpty(memberKey))
            {
                _userConnectionManager.AddConnection(memberKey, Context.ConnectionId);
            }
            return base.OnConnectedAsync();
        }

        // Hàm này đã đúng, xóa kết nối
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _userConnectionManager.RemoveConnection(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        // Hàm này đã đúng, dùng để đồng bộ khi F5
        public async Task InitializeConnection(string userKey, string memberKey)
        {
            var currentKey = GetCurrentMemberKey(userKey, memberKey);

            if (!string.IsNullOrEmpty(currentKey))
            {
                _userConnectionManager.AddConnection(currentKey, Context.ConnectionId);

                var conversationKeys = await ChatAccessData.GetConversationKeysAsync(currentKey);
                foreach (var key in conversationKeys)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, key);
                }
                await Clients.Caller.SendAsync("ConnectionEstablished", currentKey);
            }
        }

        // --- SỬA LẠI CÁC HÀM BÊN DƯỚI ĐỂ KHÔNG DÙNG _connectionMapping ---

        public async Task JoinConversation(string conversationKey)
        {
            // Lấy key trực tiếp, không qua mapping cũ
            var memberKey = GetCurrentMemberKey();
            if (!string.IsNullOrEmpty(memberKey))
                await Groups.AddToGroupAsync(Context.ConnectionId, conversationKey);
        }

        public async Task LeaveConversation(string conversationKey)
        {
            var memberKey = GetCurrentMemberKey();
            if (!string.IsNullOrEmpty(memberKey))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationKey);
        }

        public async Task UpdatePinStatus(string conversationKey, string messageKey, bool isPinned)
        {
            var memberKey = GetCurrentMemberKey();
            if (string.IsNullOrEmpty(memberKey)) return;

            var success = await ChatAccessData.PinMessageAsync(messageKey, memberKey);
            if (success)
            {
                await Clients.Group(conversationKey).SendAsync("PinResponse", conversationKey, messageKey, isPinned, true, null);
            }
            else
            {
                await Clients.Caller.SendAsync("PinResponse", conversationKey, messageKey, isPinned, false, "Database update failed");
            }
        }

        public async Task UpdateUnpinStatus(string conversationKey, string messageKey)
        {
            var memberKey = GetCurrentMemberKey();
            if (string.IsNullOrEmpty(memberKey)) return;

            var success = await ChatAccessData.UnpinMessageAsync(messageKey, memberKey);
            if (success)
            {
                await Clients.Group(conversationKey).SendAsync("UnpinResponse", conversationKey, messageKey, true, null);
            }
            else
            {
                await Clients.Caller.SendAsync("UnpinResponse", conversationKey, messageKey, false, "Database update failed");
            }
        }

        public async Task UpdateRecallStatus(string conversationKey, string messageKey)
        {
            var memberKey = GetCurrentMemberKey();
            if (string.IsNullOrEmpty(memberKey)) return;

            var success = await ChatAccessData.RecallMessageAsync(messageKey, memberKey);
            if (success)
            {
                await Clients.Group(conversationKey).SendAsync("RecallResponse", conversationKey, messageKey, true, null);
            }
            else
            {
                await Clients.Caller.SendAsync("RecallResponse", conversationKey, messageKey, false, "Database update failed");
            }
        }

        public async Task NotifyGroupCreated(string conversationKey, string[] userKeys)
        {
            foreach (var userKey in userKeys)
            {
                // Sử dụng service mới để lấy tất cả các connection ID của user
                var connectionIds = _userConnectionManager.GetConnectionIds(userKey);
                foreach (var connectionId in connectionIds)
                {
                    await Clients.Client(connectionId).SendAsync("ReloadConversations", conversationKey);
                    await Groups.AddToGroupAsync(connectionId, conversationKey);
                }
            }
        }

        public async Task NotifyAvatarUpdate(string conversationKey, string newAvatarUrl)
        {
            await Clients.Group(conversationKey)
                .SendAsync("UpdateGroupAvatar", conversationKey, newAvatarUrl);
        }

        public async Task NotifyGroupNameUpdate(string conversationKey, string newGroupName, string memberName)
        {
            await Clients.Group(conversationKey)
                .SendAsync("UpdateGroupName", conversationKey, newGroupName, memberName);
        }

        private string GetCurrentMemberKey(string userKey = null, string memberKey = null)
        {
            var httpContext = Context.GetHttpContext();
            if (httpContext == null) return null;

            // Ưu tiên key được truyền vào
            if (!string.IsNullOrEmpty(memberKey)) return memberKey;
            if (!string.IsNullOrEmpty(userKey)) return userKey;

            // Nếu không có, lấy từ cookie
            var claimsPrincipal = httpContext.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(claimsPrincipal ?? new ClaimsPrincipal());
            return memberLogin.MemberKey;
        }
    }
}