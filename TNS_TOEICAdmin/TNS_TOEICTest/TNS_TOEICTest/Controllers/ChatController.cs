// ChatController.cs
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TNS.Member;
using TNS_TOEICAdmin.Models;
using Microsoft.AspNetCore.SignalR;
using TNS_TOEICTest.Hubs;
using Newtonsoft.Json;
using static TNS_TOEICAdmin.Models.ChatAccessData;


namespace TNS_TOEICTest.Controllers
{
    [Route("api/conversations")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatController(IHttpContextAccessor httpContextAccessor, IHubContext<ChatHub> hubContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _hubContext = hubContext;
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

        [HttpGet("messages/{conversationKey}")]
        public async Task<IActionResult> GetMessages(string conversationKey, [FromQuery] int skip = 0)
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var memberKey = memberLogin.MemberKey;

            if (string.IsNullOrEmpty(memberKey))
                return Unauthorized(new { success = false, message = "MemberKey not found" });

            var result = await ChatAccessData.GetConversationsAsync(null, memberKey);
            var conversations = result["conversations"] as List<Dictionary<string, object>>;
            if (conversations == null || !conversations.Exists(c => c["ConversationKey"].ToString() == conversationKey))
                return Unauthorized(new { success = false, message = "Access denied to this conversation" });

            var messages = await ChatAccessData.GetMessagesAsync(conversationKey, skip);
            return Ok(messages);
        }

        [HttpGet("GetMemberKey")]
        public IActionResult GetMemberKey()
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var memberKey = memberLogin.MemberKey;
            if (string.IsNullOrEmpty(memberKey))
                return NotFound("MemberKey not found in cookie. Please ensure user is logged in.");
            return Ok(memberKey);
        }

        [HttpPut("pin/{messageKey}")]
        public async Task<IActionResult> PinMessage(string messageKey, [FromQuery] string conversationKey)
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var memberKey = memberLogin.MemberKey;

            if (string.IsNullOrEmpty(memberKey))
                return Unauthorized(new { success = false, message = "MemberKey not found" });

            if (string.IsNullOrEmpty(conversationKey))
                return BadRequest(new { success = false, message = "Conversation key is required" });

            var success = await ChatAccessData.PinMessageAsync(messageKey, memberKey);
            if (success)
            {
                await _hubContext.Clients.Group(conversationKey).SendAsync("PinResponse", conversationKey, messageKey, true, true, null);
                return Ok(new { success = true, message = "Pinned" });
            }
            return BadRequest(new { success = false, message = "Pinning failed" });
        }

        [HttpPut("unpin/{messageKey}")]
        public async Task<IActionResult> UnpinMessage(string messageKey, [FromQuery] string conversationKey)
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var memberKey = memberLogin.MemberKey;

            if (string.IsNullOrEmpty(memberKey))
                return Unauthorized(new { success = false, message = "MemberKey not found" });

            if (string.IsNullOrEmpty(conversationKey))
                return BadRequest(new { success = false, message = "Conversation key is required" });

            var success = await ChatAccessData.UnpinMessageAsync(messageKey, memberKey);
            if (success)
            {
                await _hubContext.Clients.Group(conversationKey).SendAsync("UnpinResponse", conversationKey, messageKey, true, null);
                return Ok(new { success = true, message = "Unpinned" });
            }
            return BadRequest(new { success = false, message = "Unpin failed" });
        }

        [HttpPut("recall/{messageKey}")]
        public async Task<IActionResult> RecallMessage(string messageKey, [FromQuery] string conversationKey)
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var memberKey = memberLogin.MemberKey;

            if (string.IsNullOrEmpty(memberKey))
                return Unauthorized(new { success = false, message = "MemberKey not found" });

            if (string.IsNullOrEmpty(conversationKey))
                return BadRequest(new { success = false, message = "Conversation key is required" });

            var success = await ChatAccessData.RecallMessageAsync(messageKey, memberKey);
            if (success)
            {
                await _hubContext.Clients.Group(conversationKey).SendAsync("RecallResponse", conversationKey, messageKey, true, null);
                return Ok(new { success = true, message = "Recalled" });
            }
            return BadRequest(new { success = false, message = "Recall failed" });
        }

        [HttpGet("GetUnthread")]
        public async Task<IActionResult> GetUnthread([FromQuery] string userKey = null, [FromQuery] string memberKey = null)
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var currentMemberKey = memberLogin.MemberKey;

            if (string.IsNullOrEmpty(currentMemberKey) && string.IsNullOrEmpty(userKey) && string.IsNullOrEmpty(memberKey))
                return Unauthorized(new { success = false, message = "UserKey or MemberKey is required" });

            if (string.IsNullOrEmpty(userKey) && string.IsNullOrEmpty(memberKey))
                memberKey = currentMemberKey;

            var result = await ChatAccessData.GetTotalUnreadCountAsync(memberKey ?? currentMemberKey);
            return Ok(new { totalUnreadCount = result });
        }
        [HttpGet("GetGroupMembers")]
        public async Task<IActionResult> GetGroupMembers(string memberKey)
        {
            var members = await ChatAccessData.GetGroupMembersAsync(memberKey);
            return Ok(members);
        }

        [HttpPost("createGroup")]
        public async Task<IActionResult> CreateGroup([FromForm] string groupName, [FromForm] IFormFile selectedAvatar, [FromForm] string users)
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var currentMemberKey = memberLogin.MemberKey;
            var currentMemberName = memberLogin.MemberName;

            if (string.IsNullOrEmpty(currentMemberKey))
                return Unauthorized(new { success = false, message = "MemberKey not found", conversationKey = (string)null });

            if (string.IsNullOrWhiteSpace(groupName))
                return BadRequest(new { success = false, message = "Group name cannot be empty or contain only whitespace", conversationKey = (string)null });

            if (selectedAvatar == null || !new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(selectedAvatar.FileName).ToLower()))
                return BadRequest(new { success = false, message = "Please select a valid image file (.jpg or .png)", conversationKey = (string)null });

            var usersList = JsonConvert.DeserializeObject<List<UserData>>(users ?? "[]");
            Console.WriteLine($"Deserialized Users Count: {usersList.Count}");

            if (usersList.Count == 0)
                return BadRequest(new { success = false, message = "Please select at least one member", conversationKey = (string)null });

            var result = await ChatAccessData.CreateGroupAsync(groupName, selectedAvatar, usersList, currentMemberKey, currentMemberName, HttpContext);
            // Giả định result là một Dictionary hoặc object chứa success, message, và conversationKey
            if (result != null && result.ContainsKey("success") && (bool)result["success"])
            {
                return Ok(new
                {
                    success = true,
                    message = "Group created successfully",
                    conversationKey = result.ContainsKey("conversationKey") ? result["conversationKey"] : null
                });
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.ContainsKey("message") ? result["message"] : "Failed to create group",
                    conversationKey = result.ContainsKey("conversationKey") ? result["conversationKey"] : null
                });
            }
        }

    }
}