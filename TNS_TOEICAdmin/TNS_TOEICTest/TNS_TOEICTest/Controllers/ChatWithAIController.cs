using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using System.Security.Claims;
using System.Text;
using TNS.Member;
using TNS_TOEICTest.Models;
using TNS_TOEICTest.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using DocumentFormat.OpenXml.Packaging;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

namespace TNS_TOEICTest.Controllers
{
    [Route("api/ChatWithAI")]
    [ApiController]
    public class ChatWithAIController : ControllerBase
    {
        private readonly PromptEngineeringService _promptService;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMemoryCache _cache;

        public ChatWithAIController(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
        {
            _promptService = new PromptEngineeringService();
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _cache = memoryCache;
        }
        [HttpGet("GetInitialData")]
        public async Task<IActionResult> GetInitialData()
        {
            try
            {
                var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
                if (memberCookie == null) return Unauthorized("User principal not found.");

                var memberLogin = new MemberLogin_Info(memberCookie);
                var currentMemberKey = memberLogin.MemberKey;

                if (string.IsNullOrEmpty(currentMemberKey))
                {
                    return Unauthorized("Member not authenticated.");
                }
                // === LOGIC MỚI: CHỦ ĐỘNG CACHING DỮ LIỆU NỀN ===
                string cacheKey = $"ChatBackgroundData_{currentMemberKey}";
                // Lần đầu mở chat, chúng ta sẽ tải dữ liệu nền và lưu vào cache
                // để các lần gửi tin nhắn sau có thể dùng ngay mà không cần tải lại.
                if (!_cache.TryGetValue(cacheKey, out _))
                {
                    var backgroundData = await ChatWithAIAccessData.LoadMemberOriginalDataAsync(currentMemberKey);
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(15));
                    _cache.Set(cacheKey, backgroundData, cacheEntryOptions);
                }
                // ===============================================

                var initialData = await ChatWithAIAccessData.GetInitialChatDataAsync(currentMemberKey);
                return Ok(initialData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetInitialData Error]: {ex.Message}");
                return StatusCode(500, "An internal server error occurred.");
            }
        }
        [HttpPost("CreateNewConversation")] 
        public async Task<IActionResult> CreateNewConversation()
        {
            try
            {              
                var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
                if (memberCookie == null)
                {
                    return Unauthorized(new { success = false, message = "Could not retrieve user principal." });
                }
                var memberLogin = new MemberLogin_Info(memberCookie);
                var currentMemberKey = memberLogin.MemberKey;

                if (string.IsNullOrEmpty(currentMemberKey))
                {
                    return Unauthorized(new { success = false, message = "Member not authenticated." });
                }

                // === Bước 2: Gọi hàm AccessData để tạo một bản ghi mới trong CSDL ===
                var newConversationId = await ChatWithAIAccessData.CreateNewConversationAsync(currentMemberKey);

                // === Bước 3: Trả về ID của cuộc hội thoại vừa được tạo ===
                return Ok(new { success = true, conversationId = newConversationId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CreateNewConversation Error]: {ex.Message}");
                return StatusCode(500, new { success = false, message = "An internal server error occurred while creating conversation." });
            }
        }
        [HttpGet("GetMoreMessages")]
        public async Task<IActionResult> GetMoreMessages(Guid conversationId, int skipCount)
        {
            if (conversationId == Guid.Empty)
            {
                return BadRequest("Invalid ConversationId.");
            }

            try
            {
                var messages = await ChatWithAIAccessData.GetMoreMessagesAsync(conversationId, skipCount);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetMoreMessages Error]: {ex.Message}");
                return StatusCode(500, "An internal server error occurred.");
            }
        }
        /// <summary>
        /// API Endpoint để lấy danh sách tóm tắt tất cả các cuộc hội thoại.
        /// </summary>
        [HttpGet("GetAllConversations")]
        public async Task<IActionResult> GetAllConversations()
        {
            try
            {
                var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
                if (memberCookie == null) return Unauthorized("User principal not found.");

                var memberLogin = new MemberLogin_Info(memberCookie);
                var currentMemberKey = memberLogin.MemberKey;

                if (string.IsNullOrEmpty(currentMemberKey))
                {
                    return Unauthorized("Member not authenticated.");
                }

                var conversations = await ChatWithAIAccessData.GetConversationsWithAIAsync(currentMemberKey);
                return Ok(conversations);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAllConversations Error]: {ex.Message}");
                return StatusCode(500, "An internal server error occurred.");
            }
        }
        [HttpPost("HandleMemberChat")]
        public async Task<IActionResult> HandleMemberChat([FromBody] ChatRequest data)
        {
            try
            {
                if (data == null || data.ConversationId == Guid.Empty)
                    return BadRequest(new { success = false, message = "Invalid request data." });

                var memberCookie = _httpContextAccessor.HttpContext.User as ClaimsPrincipal;
                var memberLogin = new MemberLogin_Info(memberCookie);
                var memberId = memberLogin.MemberKey;
                if (string.IsNullOrEmpty(memberId))
                    return Unauthorized(new { success = false, message = "User not authenticated." });

                var apiKey = _configuration["GeminiApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                    return StatusCode(500, new { success = false, message = "AI service not configured." });

                await ChatWithAIAccessData.SaveMessageAsync(data.ConversationId, "user", data.Message);

                var cacheKey = $"MemberData_{memberId}";
                if (!_cache.TryGetValue(cacheKey, out string backgroundData))
                {
                    backgroundData = await ChatWithAIAccessData.LoadMemberOriginalDataAsync(memberId);
                    _cache.Set(cacheKey, backgroundData, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));
                }
                string recentFeedbacks = await ChatWithAIAccessData.LoadRecentFeedbacksAsync(memberId);
                var chatHistoryForPrompt = await ChatWithAIAccessData.GetMessageHistoryForApiAsync(data.ConversationId);
                string textPrompt = _promptService.BuildPromptForMember(backgroundData, recentFeedbacks, chatHistoryForPrompt, data.Message, data.ScreenData);

                var parts = new List<object>();
                var extractedTextFromFiles = new StringBuilder();

                if (data.Files != null && data.Files.Any())
                {
                    foreach (var file in data.Files)
                    {
                        // Chuyển đổi Base64 ngược lại thành byte array
                        var fileBytes = Convert.FromBase64String(file.Base64Data);

                        if (file.MimeType.StartsWith("image/"))
                        {
                            parts.Add(new { inline_data = new { mime_type = file.MimeType, data = file.Base64Data } });
                        }
                        else
                        {
                            string fileText = "";
                            using var memoryStream = new MemoryStream(fileBytes);
                            if (file.MimeType == "application/pdf")
                            {
                                using (var doc = PdfDocument.Open(memoryStream)) { fileText = string.Join("\n", doc.GetPages().Select(p => p.Text)); }
                            }
                            else if (file.MimeType.StartsWith("text/plain"))
                            {
                                memoryStream.Position = 0;
                                using var reader = new StreamReader(memoryStream); fileText = await reader.ReadToEndAsync();
                            }
                            else if (file.MimeType.Contains("wordprocessingml"))
                            {
                                using (var doc = WordprocessingDocument.Open(memoryStream, false)) { fileText = doc.MainDocumentPart.Document.Body.InnerText; }
                            }
                            if (!string.IsNullOrEmpty(fileText)) { extractedTextFromFiles.AppendLine($"\n\n--- CONTENT FROM FILE: {file.FileName} ---\n{fileText}"); }
                        }
                    }
                }

                if (extractedTextFromFiles.Length > 0) { textPrompt += extractedTextFromFiles.ToString(); }
                parts.Insert(0, new { text = textPrompt });

                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key={apiKey}";
                var payload = new { contents = new[] { new { parts } } };
                string botMessage;

                using (var client = new HttpClient())
                {
                    var jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                    var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var httpResponse = await client.PostAsync(apiUrl, httpContent);
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var jsonResponse = await httpResponse.Content.ReadAsStringAsync();
                        var parsedResponse = JObject.Parse(jsonResponse);
                        botMessage = parsedResponse["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? "Sorry, I received an empty response.";
                    }
                    else
                    {
                        botMessage = "I'm having trouble connecting to my brain right now. Please try again in a moment.";
                    }
                }

                await ChatWithAIAccessData.SaveMessageAsync(data.ConversationId, "AI", botMessage);
                return Ok(new { success = true, message = botMessage });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"An internal server error occurred: {ex.Message}" });
            }
        }
        [HttpDelete("DeleteConversation/{conversationId}")]
        public async Task<IActionResult> DeleteConversation(Guid conversationId)
        {
            try
            {
                await ChatWithAIAccessData.DeleteConversationAsync(conversationId);
                return Ok(new { success = true, message = "Conversation deleted successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteConversation Error]: {ex.ToString()}");
                return StatusCode(500, new { success = false, message = "Failed to delete conversation." });
            }
        }
        [HttpPut("RenameConversation")]
        public async Task<IActionResult> RenameConversation([FromBody] RenameRequest data)
        {
            try
            {
                await ChatWithAIAccessData.RenameConversationAsync(data.ConversationId, data.NewTitle);
                return Ok(new { success = true, message = "Conversation renamed successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RenameConversation Error]: {ex.ToString()}");
                return StatusCode(500, new { success = false, message = "Failed to rename conversation." });
            }
        }
        public class ChatRequest
        {
            public Guid ConversationId { get; set; }
            public string Message { get; set; }
            public string? ScreenData { get; set; }
            public List<FileAttachmentDto>? Files { get; set; }
        }
        public class FileAttachmentDto
        {
            public string FileName { get; set; }
            public string MimeType { get; set; }
            public string Base64Data { get; set; }
        }
        public class RenameRequest
        {
            public Guid ConversationId { get; set; }
            public string NewTitle { get; set; }
        }
    }
}