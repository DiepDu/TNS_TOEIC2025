// ChatHub.cs
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading.Tasks;
using TNS_TOEICAdmin.Models;

namespace TNS_TOEICTest.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> _connectionMapping = new ConcurrentDictionary<string, string>();

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public async Task InitializeConnection(string userKey = null, string memberKey = null)
        {
            var participantKey = !string.IsNullOrEmpty(userKey) ? userKey : (!string.IsNullOrEmpty(memberKey) ? memberKey : GetParticipantKey());
            if (string.IsNullOrEmpty(participantKey))
                return;

            if (_connectionMapping.ContainsKey(Context.ConnectionId))
                _connectionMapping.TryRemove(Context.ConnectionId, out _);
            _connectionMapping.AddOrUpdate(Context.ConnectionId, participantKey, (key, oldValue) => participantKey);

            var conversationKeys = await ChatAccessData.GetConversationKeysAsync(participantKey);
            foreach (var key in conversationKeys)
                await Groups.AddToGroupAsync(Context.ConnectionId, key);
            await Clients.Caller.SendAsync("ConnectionEstablished", participantKey);
        }

        public async Task JoinConversation(string conversationKey)
        {
            var participantKey = _connectionMapping.GetValueOrDefault(Context.ConnectionId);
            if (!string.IsNullOrEmpty(participantKey))
                await Groups.AddToGroupAsync(Context.ConnectionId, conversationKey);
        }

        public async Task LeaveConversation(string conversationKey)
        {
            var participantKey = _connectionMapping.GetValueOrDefault(Context.ConnectionId);
            if (!string.IsNullOrEmpty(participantKey))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationKey);
        }

        public async Task UpdatePinStatus(string conversationKey, string messageKey, bool isPinned)
        {
            var participantKey = _connectionMapping.GetValueOrDefault(Context.ConnectionId);
            if (string.IsNullOrEmpty(participantKey))
                return;

            var success = await ChatAccessData.PinMessageAsync(messageKey, participantKey);
            if (success)
            {
                await Clients.Group(conversationKey).SendAsync("PinResponse", conversationKey, messageKey, isPinned, true, null);
                await SendPinUpdate(conversationKey, messageKey, isPinned);
            }
            else
                await Clients.Caller.SendAsync("PinResponse", conversationKey, messageKey, isPinned, false, "Database update failed");
        }

        public async Task UpdateUnpinStatus(string conversationKey, string messageKey)
        {
            var participantKey = _connectionMapping.GetValueOrDefault(Context.ConnectionId);
            if (string.IsNullOrEmpty(participantKey))
                return;

            var success = await ChatAccessData.UnpinMessageAsync(messageKey, participantKey);
            if (success)
            {
                await Clients.Group(conversationKey).SendAsync("UnpinResponse", conversationKey, messageKey, true, null);
                await SendUnpinUpdate(conversationKey, messageKey);
            }
            else
                await Clients.Caller.SendAsync("UnpinResponse", conversationKey, messageKey, false, "Database update failed");
        }

        public async Task UpdateRecallStatus(string conversationKey, string messageKey)
        {
            var participantKey = _connectionMapping.GetValueOrDefault(Context.ConnectionId);
            if (string.IsNullOrEmpty(participantKey))
                return;

            var success = await ChatAccessData.RecallMessageAsync(messageKey, participantKey);
            if (success)
            {
                await Clients.Group(conversationKey).SendAsync("RecallResponse", conversationKey, messageKey, true, null);
            }
            else
            {
                await Clients.Caller.SendAsync("RecallResponse", conversationKey, messageKey, false, "Database update failed");
            }
        }

        public async Task SendPinUpdate(string conversationKey, string messageKey, bool isPinned)
        {
            await Clients.GroupExcept(conversationKey, new[] { Context.ConnectionId }).SendAsync("ReceivePinUpdate", messageKey, isPinned);
        }

        public async Task SendUnpinUpdate(string conversationKey, string messageKey)
        {
            await Clients.GroupExcept(conversationKey, new[] { Context.ConnectionId }).SendAsync("ReceiveUnpinUpdate", messageKey);
        }

        public async Task SendRecallUpdate(string conversationKey, string messageKey)
        {
            await Clients.GroupExcept(conversationKey, new[] { Context.ConnectionId }).SendAsync("ReceiveRecallUpdate", messageKey);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (_connectionMapping.TryRemove(Context.ConnectionId, out var participantKey) && !string.IsNullOrEmpty(participantKey))
            {
                var conversationKeys = await ChatAccessData.GetConversationKeysAsync(participantKey);
                foreach (var key in conversationKeys)
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, key);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task DisconnectSignal()
        {
            if (_connectionMapping.TryRemove(Context.ConnectionId, out var participantKey) && !string.IsNullOrEmpty(participantKey))
            {
                var conversationKeys = await ChatAccessData.GetConversationKeysAsync(participantKey);
                foreach (var key in conversationKeys)
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, key);
                await Clients.Caller.SendAsync("Disconnected");
            }
        }

        private string GetParticipantKey()
        {
            var claimsPrincipal = Context.User as ClaimsPrincipal;
            return claimsPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}