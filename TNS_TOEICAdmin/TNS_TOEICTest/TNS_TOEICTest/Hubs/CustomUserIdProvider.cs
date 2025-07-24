using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace TNS_TOEICTest.Hubs
{
    public class CustomUserIdProvider : IUserIdProvider
    {
        public string GetUserId(HubConnectionContext connection)
        {
            var claimsPrincipal = connection.User as ClaimsPrincipal;
            if (claimsPrincipal == null) return null;

            // Kiểm tra MemberKey trước (dự án Test/Member)
            var memberKey = claimsPrincipal.FindFirst("MemberKey")?.Value;
            if (!string.IsNullOrEmpty(memberKey)) return memberKey;

            // Nếu không có MemberKey, kiểm tra UserKey (dự án Admin)
            var userKey = claimsPrincipal.FindFirst("UserKey")?.Value;
            return userKey ?? null;
        }
    }
}
