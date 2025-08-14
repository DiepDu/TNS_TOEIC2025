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
using Microsoft.AspNetCore.Authorization;
using TNS_TOEICTest.Models.Chat;


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


        [HttpGet("GetGroupDetails/{conversationKey}")]
        public async Task<IActionResult> GetGroupDetails(string conversationKey)
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var currentMemberKey = memberLogin.MemberKey;

            var groupDetails = await ChatAccessData.GetGroupDetailsAsync(conversationKey);
            if (groupDetails == null)
            {
                return NotFound(new { success = false, message = "Group not found." });
            }

            // Trả thêm currentMemberKey về cho client
            return Ok(new
            {
                success = true,
                currentMemberKey,
                data = groupDetails
            });
        }


        [HttpPost("UpdateGroupAvatar")]
        [Authorize]
        public async Task<IActionResult> UpdateGroupAvatar([FromForm] IFormCollection formData)
        {
            var conversationKey = formData["ConversationKey"];
            var file = formData.Files["File"];

            if (string.IsNullOrEmpty(conversationKey) || file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Invalid input" });

            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var currentMemberKey = memberLogin.MemberKey;

            if (string.IsNullOrEmpty(currentMemberKey))
                return Unauthorized(new { success = false, message = "MemberKey not found" });

            var result = await ChatAccessData.UpdateGroupAvatarAsync(conversationKey, currentMemberKey, file, HttpContext);
            if (result["success"].ToString() == "True")
            {
                string newAvatarUrl = result["newAvatarUrl"].ToString();

                // Gửi tín hiệu đến tất cả client trong group (bao gồm cả người thay đổi)
                await _hubContext.Clients.Group(conversationKey)
                    .SendAsync("UpdateGroupAvatar", conversationKey, newAvatarUrl);
            }

            return Ok(result);
        }

        [HttpPost("UpdateGroupName")]
        [Authorize]
        public async Task<IActionResult> UpdateGroupName([FromForm] IFormCollection formData)
        {
            // 1. Lấy dữ liệu từ form và loại bỏ khoảng trắng
            string conversationKey = formData["ConversationKey"].ToString()?.Trim();
            string newGroupName = formData["NewGroupName"].ToString()?.Trim();

            if (string.IsNullOrEmpty(conversationKey) || string.IsNullOrEmpty(newGroupName))
                return BadRequest(new { success = false, message = "Invalid input" });

            // 2. Lấy thông tin user hiện tại
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var currentMemberKey = memberLogin.MemberKey;
            var currentMemberName = memberLogin.MemberName;

            if (string.IsNullOrEmpty(currentMemberKey))
                return Unauthorized(new { success = false, message = "MemberKey not found" });

            // 3. Cập nhật tên nhóm trong DB
            var result = await ChatAccessData.UpdateGroupNameAsync(
                conversationKey,
                currentMemberKey,
                currentMemberName,
                newGroupName,
                HttpContext
            );

            if (result != null && result["success"]?.ToString() == "True")
            {
                // 4. Bắn sự kiện cập nhật tên nhóm cho tất cả client trong group
                await _hubContext.Clients.Group(conversationKey)
                    .SendAsync("UpdateGroupName", conversationKey, result["newGroupName"]?.ToString(), currentMemberName);

                // 5. Nếu có tin nhắn system, gửi realtime đến các client
                if (result.ContainsKey("messageKey") &&
                    result.ContainsKey("systemContent") &&
                    result.ContainsKey("createdOn"))
                {
                    DateTime createdOn;
                    if (!DateTime.TryParse(result["createdOn"]?.ToString(), out createdOn))
                        createdOn = DateTime.UtcNow;

                    var messageObj = new
                    {
                        MessageKey = result["messageKey"]?.ToString(),
                        ConversationKey = conversationKey,
                        SenderKey = (string)null,
                        SenderName = (string)null,
                        SenderAvatar = (string)null,
                        MessageType = "Text",
                        Content = result["systemContent"]?.ToString(),
                        ParentMessageKey = (string)null,
                        CreatedOn = createdOn,
                        Status = 1,
                        IsPinned = false,
                        IsSystemMessage = true,
                        Url = (string)null
                    };

                    await _hubContext.Clients.Group(conversationKey)
                        .SendAsync("ReceiveMessage", messageObj);
                }

                // 6. Trả về kết quả cho client gọi API
                return Ok(new { success = true, data = result });
            }

            return Ok(new { success = false, message = "Update failed" });
        }

        [HttpPost("RemoveMember")]
        [Authorize]
        public async Task<IActionResult> RemoveMember([FromBody] RemoveMemberRequest request)
        {
            string conversationKey = request.ConversationKey?.Trim();
            string targetUserKey = request.TargetUserKey?.Trim();
            string targetUserName = request.TargetUserName?.Trim();

            if (string.IsNullOrEmpty(conversationKey) || string.IsNullOrEmpty(targetUserKey) || string.IsNullOrEmpty(targetUserName))
                return BadRequest(new { success = false, message = "Invalid input" });

            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var currentMemberKey = memberLogin.MemberKey;
            var currentMemberName = memberLogin.MemberName;

            if (string.IsNullOrEmpty(currentMemberKey))
                return Unauthorized(new { success = false, message = "MemberKey not found" });

            var result = await ChatAccessData.RemoveMemberAsync(
                conversationKey,
                currentMemberKey,
                currentMemberName,
                targetUserKey,
                targetUserName,
                HttpContext
            );

            if (result != null && result["success"]?.ToString() == "True")
            {
                await _hubContext.Clients.Group(conversationKey)
                    .SendAsync("MemberRemoved", conversationKey, targetUserKey, currentMemberName);

                if (result.ContainsKey("messageKey") &&
                    result.ContainsKey("systemContent") &&
                    result.ContainsKey("createdOn"))
                {
                    DateTime createdOn;
                    if (!DateTime.TryParse(result["createdOn"]?.ToString(), out createdOn))
                        createdOn = DateTime.UtcNow;

                    var messageObj = new
                    {
                        MessageKey = result["messageKey"]?.ToString(),
                        ConversationKey = conversationKey,
                        SenderKey = (string)null,
                        SenderName = (string)null,
                        SenderAvatar = (string)null,
                        MessageType = "Text",
                        Content = result["systemContent"]?.ToString(),
                        ParentMessageKey = (string)null,
                        CreatedOn = createdOn,
                        Status = 1,
                        IsPinned = false,
                        IsSystemMessage = true,
                        Url = (string)null
                    };

                    await _hubContext.Clients.Group(conversationKey)
                        .SendAsync("ReceiveMessage", messageObj);
                }

                return Ok(new { success = true, data = result });
            }

            return Ok(new { success = false, message = result["message"]?.ToString() ?? "Remove failed" });
        }
        [HttpPost("GetAddableMembers")]
        public async Task<IActionResult> GetAddableMembers([FromBody] AddableMembersRequest request)
        {
            if (string.IsNullOrEmpty(request.ConversationKey) || request.ExcludeKeys == null)
                return BadRequest(new { success = false, message = "Invalid input" });

            var result = await ChatAccessData.GetAddableMembersAsync(request.ConversationKey, request.ExcludeKeys);
            return Ok(new { success = true, items = result });
        }
        [HttpPost("AddMembers")]
        [Authorize]
        public async Task<IActionResult> AddMembers([FromBody] AddMembersRequest request)
        {
            string conversationKey = request.ConversationKey?.Trim();
            var newMembers = request.NewMembers ?? new List<NewMemberInfo>();

            if (string.IsNullOrEmpty(conversationKey) || newMembers.Count == 0)
                return BadRequest(new { success = false, message = "Invalid input" });

            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var currentMemberKey = memberLogin.MemberKey;
            var currentMemberName = memberLogin.MemberName;

            if (string.IsNullOrEmpty(currentMemberKey))
                return Unauthorized(new { success = false, message = "MemberKey not found" });

            var result = await ChatAccessData.AddMembersAsync(
                conversationKey,
                currentMemberKey,
                currentMemberName,
                newMembers,
                HttpContext
            );

            if (result != null && result.ContainsKey("success") && result["success"]?.ToString() == "True")
            {
                // Gửi realtime MemberAdded (cập nhật UI nhóm)
                foreach (var mem in newMembers)
                {
                    await _hubContext.Clients.Group(conversationKey)
                        .SendAsync("MemberAdded", conversationKey, mem.UserKey, currentMemberName);
                }

                // Gửi các system message vừa insert
                if (result.ContainsKey("messages") && result["messages"] is List<Dictionary<string, object>> msgList)
                {
                    foreach (var msg in msgList)
                    {
                        DateTime createdOn;
                        if (!DateTime.TryParse(msg["createdOn"]?.ToString(), out createdOn))
                            createdOn = DateTime.UtcNow;

                        var messageObj = new
                        {
                            MessageKey = msg["messageKey"]?.ToString(),
                            ConversationKey = conversationKey,
                            SenderKey = (string)null,
                            SenderName = (string)null,
                            SenderAvatar = (string)null,
                            MessageType = "Text",
                            Content = msg["systemContent"]?.ToString(),
                            ParentMessageKey = (string)null,
                            CreatedOn = createdOn,
                            Status = 1,
                            IsPinned = false,
                            IsSystemMessage = true,
                            Url = (string)null
                        };

                        await _hubContext.Clients.Group(conversationKey)
                            .SendAsync("ReceiveMessage", messageObj);
                    }
                }

                return Ok(new { success = true, data = result });
            }

            return Ok(new { success = false, message = result["message"]?.ToString() ?? "Add members failed" });
        }
      
        public class AddableMembersRequest
        {
            public string ConversationKey { get; set; }
            public List<string> ExcludeKeys { get; set; }
        }

        public class RemoveMemberRequest
        {
            public string ConversationKey { get; set; }
            public string TargetUserKey { get; set; }
            public string TargetUserName { get; set; }
        }

    }
}