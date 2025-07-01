using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TNS.Member;
using TNS_TOEICAdmin.Models;

namespace TNS_TOEICTest.Controllers
{
    [Route("api/conversations")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ChatController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpGet]
        public async Task<IActionResult> GetConversations([FromQuery] string userKey = null, [FromQuery] string memberKey = null)
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var currentMemberKey = memberLogin.MemberKey;

            if (string.IsNullOrEmpty(currentMemberKey) && string.IsNullOrEmpty(userKey) && string.IsNullOrEmpty(memberKey))
                return Unauthorized(new { success = false, message = "UserKey or MemberKey is required" });

            if (string.IsNullOrEmpty(userKey) && string.IsNullOrEmpty(memberKey))
                memberKey = currentMemberKey;

            var result = await ChatAccessData.GetConversationsAsync(userKey, memberKey, currentMemberKey);
            return Ok(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchContacts([FromQuery] string query)
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var memberKey = memberLogin.MemberKey;

            if (string.IsNullOrEmpty(memberKey))
                return Unauthorized(new { success = false, message = "MemberKey not found" });

            if (string.IsNullOrEmpty(query))
                return Ok(new List<Dictionary<string, object>>());

            var results = await ChatAccessData.SearchContactsAsync(query, memberKey);
            return Ok(results);
        }

        //[HttpGet("messages/{conversationKey}")]
        //public async Task<IActionResult> GetMessages(string conversationKey, [FromQuery] int skip = 0)
        //{
        //    var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
        //    var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
        //    var memberKey = memberLogin.MemberKey;

        //    if (string.IsNullOrEmpty(memberKey))
        //        return Unauthorized(new { success = false, message = "MemberKey not found" });

        //    var result = await ChatAccessData.GetConversationsAsync(null, memberKey);
        //    var conversations = result["conversations"] as List<Dictionary<string, object>>;
        //    if (conversations == null || !conversations.Exists(c => c["ConversationKey"].ToString() == conversationKey))
        //        return Unauthorized(new { success = false, message = "Access denied to this conversation" });

        //    var messages = await ChatAccessData.GetMessagesAsync(conversationKey, skip);
        //    return Ok(messages);
        //}

        [HttpPost("messages")]
        public async Task<IActionResult> SendMessage()
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var memberKey = memberLogin.MemberKey;

            if (string.IsNullOrEmpty(memberKey))
                return Unauthorized(new { success = false, message = "MemberKey not found" });

            var formData = Request.Form;
            var conversationKey = formData["ConversationKey"];
            var userType = formData["UserType"];
            var content = formData["Content"];
            var file = formData.Files["File"];

            // Giả định logic lưu tin nhắn (cần triển khai trong DB)
            // Đây là placeholder, cần thêm ChatAccessData.SendMessageAsync
            return Ok(new { success = true, message = "Message sent" });
        }

        [HttpGet("GetMemberKey")]
        public IActionResult GetMemberKey()
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var memberKey = memberLogin.MemberKey;
            if (string.IsNullOrEmpty(memberKey))
            {
                return NotFound("MemberKey not found in cookie. Please ensure user is logged in.");
            }
            return Ok(memberKey);
        }
    }
}