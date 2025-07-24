using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;
using TNS_TOEICAdmin.Models;

public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> _connectionMapping = new ConcurrentDictionary<string, string>();

    public override async Task OnConnectedAsync()
    {
        // Chỉ thiết lập kết nối cơ bản, chưa tham gia nhóm
        await base.OnConnectedAsync();
    }

    public async Task InitializeConnection(string userKey = null, string memberKey = null)
    {
        var participantKey = !string.IsNullOrEmpty(userKey) ? userKey :
                           (!string.IsNullOrEmpty(memberKey) ? memberKey : GetParticipantKey());
        if (string.IsNullOrEmpty(participantKey))
            return;

        try
        {
            _connectionMapping.TryAdd(Context.ConnectionId, participantKey);
            var conversationKeys = await ChatAccessData.GetConversationKeysAsync(participantKey);
            foreach (var key in conversationKeys)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, key);
            }
            await Clients.Caller.SendAsync("ConnectionEstablished", participantKey);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InitializeConnection] Error: {ex.Message}");
            await Clients.Caller.SendAsync("ConnectionEstablished", null);
        }
    }

    public async Task JoinConversation(string conversationKey)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, conversationKey);
    }

    public async Task LeaveConversation(string conversationKey)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationKey);
    }

    public async Task UpdatePinStatus(string conversationKey, string messageKey, bool isPinned)
    {
        var participantKey = _connectionMapping.GetValueOrDefault(Context.ConnectionId);
        if (string.IsNullOrEmpty(participantKey))
            return;

        await Clients.Caller.SendAsync("PinResponse", conversationKey, messageKey, isPinned, true, null);
        await SendPinUpdate(conversationKey, messageKey, isPinned);
    }

    public async Task UpdateUnpinStatus(string conversationKey, string messageKey)
    {
        var participantKey = _connectionMapping.GetValueOrDefault(Context.ConnectionId);
        if (string.IsNullOrEmpty(participantKey))
            return;

        await Clients.Caller.SendAsync("UnpinResponse", conversationKey, messageKey, true, null);
        await SendUnpinUpdate(conversationKey, messageKey);
    }

    public async Task UpdateRecallStatus(string conversationKey, string messageKey)
    {
        var participantKey = _connectionMapping.GetValueOrDefault(Context.ConnectionId);
        if (string.IsNullOrEmpty(participantKey))
            return;

        await Clients.Caller.SendAsync("RecallResponse", conversationKey, messageKey, true, null);
        await SendRecallUpdate(conversationKey, messageKey);
    }

    public async Task SendPinUpdate(string conversationKey, string messageKey, bool isPinned)
    {
        var participantKey = _connectionMapping.GetValueOrDefault(Context.ConnectionId);
        if (string.IsNullOrEmpty(participantKey))
            return;

        await Clients.Group(conversationKey).SendAsync("ReceivePinUpdate", messageKey, isPinned);
    }

    public async Task SendUnpinUpdate(string conversationKey, string messageKey)
    {
        var participantKey = _connectionMapping.GetValueOrDefault(Context.ConnectionId);
        if (string.IsNullOrEmpty(participantKey))
            return;

        await Clients.Group(conversationKey).SendAsync("ReceiveUnpinUpdate", messageKey);
    }

    public async Task SendRecallUpdate(string conversationKey, string messageKey)
    {
        var participantKey = _connectionMapping.GetValueOrDefault(Context.ConnectionId);
        if (string.IsNullOrEmpty(participantKey))
            return;

        await Clients.Group(conversationKey).SendAsync("ReceiveRecallUpdate", messageKey);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        if (_connectionMapping.TryRemove(Context.ConnectionId, out var participantKey) && !string.IsNullOrEmpty(participantKey))
        {
            var conversationKeys = await ChatAccessData.GetConversationKeysAsync(participantKey);
            foreach (var key in conversationKeys)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, key);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
    public async Task DisconnectSignal()
    {
        if (_connectionMapping.TryRemove(Context.ConnectionId, out var participantKey) && !string.IsNullOrEmpty(participantKey))
        {
            var conversationKeys = await ChatAccessData.GetConversationKeysAsync(participantKey);
            foreach (var key in conversationKeys)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, key);
            }
            await Clients.Caller.SendAsync("Disconnected");
        }
    }
    private string GetParticipantKey()
    {
        var claimsPrincipal = Context.User as ClaimsPrincipal;
        if (claimsPrincipal == null)
            return null;

        var nameIdentifier = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return nameIdentifier;
    }
}