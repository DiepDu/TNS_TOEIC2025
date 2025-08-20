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
            // --- VALIDATION & SETUP ---
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

            // --- THỰC HIỆN LOGIC THÊM THÀNH VIÊN ---
            // Gọi vào Data Access Layer để xử lý, bao gồm cả việc kiểm tra quyền (permission check)
            var result = await ChatAccessData.AddMembersAsync(
                conversationKey,
                currentMemberKey,
                currentMemberName,
                newMembers,
                HttpContext
            );

            // --- KIỂM TRA KẾT QUẢ TRƯỚC KHI GỬI SIGNALR ---
            bool success = result.ContainsKey("success") && result["success"] is bool s && s;
            string message = result.ContainsKey("message") && result["message"] != null
                ? result["message"].ToString()
                : (success ? "Members added successfully" : "Add members failed");

            // *** SỬA LỖI: CHỈ GỬI SIGNALR KHI THAO TÁC THÀNH CÔNG ***
            if (success)
            {
                // Kiểm tra xem ChatAccessData có trả về danh sách tin nhắn hệ thống đã được tạo sẵn không
                if (result.ContainsKey("messages") && result["messages"] is IList<object> msgList && msgList.Count > 0)
                {
                    // Nếu có, định dạng lại và gửi đi
                    var messageObjects = msgList.Select(msg => new
                    {
                        MessageKey = msg.GetType().GetProperty("MessageKey")?.GetValue(msg)?.ToString(),
                        ConversationKey = msg.GetType().GetProperty("ConversationKey")?.GetValue(msg)?.ToString() ?? conversationKey,
                        SenderKey = msg.GetType().GetProperty("SenderKey")?.GetValue(msg)?.ToString(),
                        SenderName = msg.GetType().GetProperty("SenderName")?.GetValue(msg)?.ToString(),
                        SenderAvatar = msg.GetType().GetProperty("SenderAvatar")?.GetValue(msg)?.ToString(),
                        MessageType = msg.GetType().GetProperty("MessageType")?.GetValue(msg)?.ToString() ?? "Text",
                        Content = msg.GetType().GetProperty("Content")?.GetValue(msg)?.ToString(),
                        ParentMessageKey = msg.GetType().GetProperty("ParentMessageKey")?.GetValue(msg)?.ToString(),
                        CreatedOn = DateTime.TryParse(msg.GetType().GetProperty("CreatedOn")?.GetValue(msg)?.ToString(), out var createdOn) ? createdOn : DateTime.UtcNow,
                        Status = int.TryParse(msg.GetType().GetProperty("Status")?.GetValue(msg)?.ToString(), out var status) ? status : 1,
                        IsPinned = bool.TryParse(msg.GetType().GetProperty("IsPinned")?.GetValue(msg)?.ToString(), out var isPinned) ? isPinned : false,
                        IsSystemMessage = bool.TryParse(msg.GetType().GetProperty("IsSystemMessage")?.GetValue(msg)?.ToString(), out var isSystem) ? isSystem : true,
                        Url = msg.GetType().GetProperty("Url")?.GetValue(msg)?.ToString()
                    }).ToList();

                    await _hubContext.Clients.Group(conversationKey)
                        .SendAsync("ReceiveMultipleMessages", messageObjects);
                }
                else // Nếu không, tự tạo tin nhắn hệ thống mặc định
                {
                    var messageObjects = newMembers.Select(mem => new
                    {
                        MessageKey = Guid.NewGuid().ToString(),
                        ConversationKey = conversationKey,
                        SenderKey = (string)null,
                        SenderName = (string)null,
                        SenderAvatar = (string)null,
                        MessageType = "Text",
                        Content = $"{mem.UserName} added to group by {currentMemberName}",
                        ParentMessageKey = (string)null,
                        CreatedOn = DateTime.UtcNow,
                        Status = 1,
                        IsPinned = false,
                        IsSystemMessage = true,
                        Url = (string)null
                    }).ToList();

                    await _hubContext.Clients.Group(conversationKey)
                        .SendAsync("ReceiveMultipleMessages", messageObjects);
                }
            }

            // --- TRẢ VỀ KẾT QUẢ HTTP REQUEST ---
            return Ok(new { success, message });
        }
        [HttpPost("LeaveGroup")]
        [Authorize]
        public async Task<IActionResult> LeaveGroup([FromBody] LeaveGroupRequest request)
        {
            string conversationKey = request.ConversationKey?.Trim();

            if (string.IsNullOrEmpty(conversationKey))
                return BadRequest(new { success = false, message = "Invalid input" });

            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var currentMemberKey = memberLogin.MemberKey;
            var currentMemberName = memberLogin.MemberName;

            if (string.IsNullOrEmpty(currentMemberKey))
                return Unauthorized(new { success = false, message = "MemberKey not found" });

            var result = await ChatAccessData.LeaveGroupAsync(
                conversationKey,
                currentMemberKey,
                currentMemberName,
                HttpContext
            );

            if (result != null && (bool)result["success"])
            {
                // Nếu nhóm bị xóa (không còn thành viên)
                if (result["message"].ToString() == "Group deleted as no members remain")
                {
                    // Gửi thông báo reload danh sách conversation cho tất cả client
                    await _hubContext.Clients.Group(conversationKey)
                        .SendAsync("ReloadConversations", conversationKey);
                }
                else
                {
                    // Gửi thông báo rời nhóm
                    await _hubContext.Clients.Group(conversationKey)
                        .SendAsync("MemberRemoved", conversationKey, currentMemberKey, currentMemberName);

                    // Gửi các tin nhắn hệ thống qua SignalR
                    if (result.ContainsKey("messages") && result["messages"] is IList<object> msgList && msgList.Count > 0)
                    {
                        var messageObjects = msgList.Select(msg => new
                        {
                            MessageKey = msg.GetType().GetProperty("MessageKey")?.GetValue(msg)?.ToString(),
                            ConversationKey = msg.GetType().GetProperty("ConversationKey")?.GetValue(msg)?.ToString() ?? conversationKey,
                            SenderKey = msg.GetType().GetProperty("SenderKey")?.GetValue(msg)?.ToString(),
                            SenderName = msg.GetType().GetProperty("SenderName")?.GetValue(msg)?.ToString(),
                            SenderAvatar = msg.GetType().GetProperty("SenderAvatar")?.GetValue(msg)?.ToString(),
                            MessageType = msg.GetType().GetProperty("MessageType")?.GetValue(msg)?.ToString() ?? "Text",
                            Content = msg.GetType().GetProperty("Content")?.GetValue(msg)?.ToString(),
                            ParentMessageKey = msg.GetType().GetProperty("ParentMessageKey")?.GetValue(msg)?.ToString(),
                            CreatedOn = DateTime.TryParse(msg.GetType().GetProperty("CreatedOn")?.GetValue(msg)?.ToString(), out var createdOn) ? createdOn : DateTime.UtcNow,
                            Status = int.TryParse(msg.GetType().GetProperty("Status")?.GetValue(msg)?.ToString(), out var status) ? status : 1,
                            IsPinned = bool.TryParse(msg.GetType().GetProperty("IsPinned")?.GetValue(msg)?.ToString(), out var isPinned) ? isPinned : false,
                            IsSystemMessage = bool.TryParse(msg.GetType().GetProperty("IsSystemMessage")?.GetValue(msg)?.ToString(), out var isSystem) ? isSystem : true,
                            Url = msg.GetType().GetProperty("Url")?.GetValue(msg)?.ToString()
                        }).ToList();

                        await _hubContext.Clients.Group(conversationKey)
                            .SendAsync("ReceiveMultipleMessages", messageObjects);
                    }
                }

                return Ok(new { success = true, message = result["message"] });
            }

            return Ok(new { success = false, message = result["message"]?.ToString() ?? "Leave group failed" });
        }
        [HttpPost("ToggleBlockUser/{conversationKey}")]
        public async Task<IActionResult> ToggleBlockUser(string conversationKey, [FromBody] BlockUserRequest request)
        {
            // Lấy thông tin người dùng đang đăng nhập
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var currentUserKey = memberLogin.MemberKey;

            // Kiểm tra thông tin đầu vào
            if (string.IsNullOrEmpty(currentUserKey) || string.IsNullOrEmpty(request?.TargetUserKey))
            {
                return BadRequest(new { success = false, message = "Invalid request data." });
            }

            // Gọi hàm từ AccessData để cập nhật DB
            var (success, isBanned) = await ChatAccessData.ToggleBlockUserAsync(conversationKey, request.TargetUserKey, currentUserKey);

            if (success)
            {
                // Trả về thông báo thành công dựa trên kết quả
                return Ok(new
                {
                    success = true,
                    message = isBanned ? "User blocked successfully." : "User unblocked successfully."
                });
            }
            else
            {
                return StatusCode(500, new { success = false, message = "An error occurred. Please try again." });
            }
        }
        [HttpGet("GetBanStatus")]
        public async Task<IActionResult> GetBanStatus([FromQuery] string conversationKey, [FromQuery] string targetUserKey)
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var currentMemberKey = memberLogin.MemberKey;

            if (string.IsNullOrEmpty(currentMemberKey) || string.IsNullOrEmpty(conversationKey) || string.IsNullOrEmpty(targetUserKey))
            {
                return BadRequest(new { success = false, message = "Invalid request parameters." });
            }

            // Kiểm tra người dùng hiện tại có trong cuộc hội thoại không
            var isInConversation = await ChatAccessData.CheckUserInConversationAsync(currentMemberKey, conversationKey);
            if (!isInConversation)
            {
                return Unauthorized(new { success = false, message = "You do not have access to this conversation." });
            }

            var isBanned = await ChatAccessData.GetParticipantBanStatusAsync(conversationKey, targetUserKey);
            return Ok(new { success = true, isBanned });
        }
        // Đặt hàm này vào bên trong class ChatController, ví dụ như sau hàm GetUnthread
        [HttpPost("markAsRead/{conversationKey}")]
        public async Task<IActionResult> MarkConversationAsRead(string conversationKey)
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var memberKey = memberLogin.MemberKey;

            if (string.IsNullOrEmpty(memberKey))
            {
                return Unauthorized(new { success = false, message = "MemberKey not found." });
            }

            if (string.IsNullOrEmpty(conversationKey))
            {
                return BadRequest(new { success = false, message = "ConversationKey is required." });
            }

            var success = await ChatAccessData.MarkConversationAsReadAsync(conversationKey, memberKey);

            if (success)
            {
                // Gửi sự kiện qua SignalR đến tất cả thành viên trong nhóm,
                // để client của họ có thể cập nhật trạng thái tin nhắn (từ 1 thành 2 dấu tích)
                await _hubContext.Clients.Group(conversationKey).SendAsync("MessagesRead", conversationKey, memberKey);

                return Ok(new { success = true, message = "Conversation marked as read." });
            }

            return StatusCode(500, new { success = false, message = "An error occurred while marking messages as read." });
        }
        // Đặt hàm này vào bên trong class ChatController

        // TÌM VÀ THAY THẾ TOÀN BỘ HÀM NÀY TRONG FILE ChatController.cs

        [HttpPost("markMessagesAsRead")]
        public async Task<IActionResult> MarkMessagesAsRead([FromBody] MarkMessagesRequest request)
        {
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var memberKey = memberLogin.MemberKey;

            if (string.IsNullOrEmpty(memberKey))
            {
                return Unauthorized(new { success = false, message = "MemberKey not found." });
            }

            if (request == null || request.MessageKeys == null || !request.MessageKeys.Any() || string.IsNullOrEmpty(request.ConversationKey))
            {
                return BadRequest(new { success = false, message = "MessageKeys and ConversationKey are required." });
            }

            // --- THAY ĐỔI: Truyền memberKey vào hàm AccessData ---
            var (success, errorMessage) = await ChatAccessData.MarkSpecificMessagesAsReadAsync(request.MessageKeys, request.ConversationKey, memberKey);

            if (success)
            {
                // Vẫn gửi sự kiện "MessagesRead" như cũ
                await _hubContext.Clients.Group(request.ConversationKey).SendAsync("MessagesRead", request.ConversationKey, memberKey);
                return Ok(new { success = true });
            }

            return StatusCode(500, new { success = false, message = "An error occurred while marking messages as read.", error = errorMessage });
        }


        // TÌM VÀ THAY THẾ TOÀN BỘ HÀM NÀY TRONG FILE ChatController.cs

        [HttpPost("messages")]
        [Authorize]
        public async Task<IActionResult> SendMessage(
            // --- BẮT ĐẦU SỬA LỖI: Thêm giá trị mặc định "= null" để biến các tham số thành tùy chọn ---
            [FromForm] string conversationKey,
            [FromForm] string content,
            [FromForm] string userKey = null,                 // Receiver Key
            [FromForm] string userType = null,                // Receiver Type
            [FromForm] string parentMessageKey = null,
            [FromForm] string parentMessageContent = null,
            [FromForm] IFormFile file = null
        // --- KẾT THÚC SỬA LỖI ---
        )
        {
            // --- Lấy thông tin người gửi từ cookie ---
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var memberLogin = new MemberLogin_Info(memberCookie ?? new ClaimsPrincipal());
            var senderKey = memberLogin.MemberKey;
            var senderName = memberLogin.MemberName;
            var senderAvatar = memberLogin.Avatar;

            if (string.IsNullOrEmpty(senderKey))
            {
                return Unauthorized(new { success = false, message = "Sender not authenticated." });
            }

            // Thêm kiểm tra conversationKey để đảm bảo an toàn
            if (string.IsNullOrEmpty(conversationKey))
            {
                return BadRequest(new { success = false, message = "ConversationKey is required." });
            }

            // --- Gọi hàm xử lý logic trong AccessData ---
            var messageObject = await ChatAccessData.SendMessageAsync(
                conversationKey,
                senderKey,
                "Member", // SenderType, giả sử người gửi luôn là Member
                senderName,
                senderAvatar,
                userKey,
                userType,
                content,
                parentMessageKey,
                parentMessageContent,
                file,
                HttpContext
            );

            if (messageObject != null)
            {
                // --- Gửi tin nhắn real-time qua SignalR cho tất cả client trong group ---
                await _hubContext.Clients.Group(conversationKey).SendAsync("ReceiveMessage", messageObject);
                return Ok(new { success = true, data = messageObject });
            }

            return StatusCode(500, new { success = false, message = "An error occurred while sending the message." });
        }
        public class MarkMessagesRequest
        {
            public List<string> MessageKeys { get; set; }
            // Thêm ConversationKey để có thể gửi SignalR đến đúng group
            public string ConversationKey { get; set; }
        }
        public class BlockUserRequest
        {
            public string TargetUserKey { get; set; }
        }
        public class LeaveGroupRequest
        {
            public string ConversationKey { get; set; }
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