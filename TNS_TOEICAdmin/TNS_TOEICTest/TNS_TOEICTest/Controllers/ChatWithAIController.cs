using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Claims;
using System.Text;
using TNS.Member;
using TNS_TOEICTest.Models;
using TNS_TOEICTest.Services;
using UglyToad.PdfPig;

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
        private string GetTargetMemberKey(string? userKeyFromRequest)
        {
            // Ưu tiên key được gửi lên từ request (dành cho Admin)
            if (!string.IsNullOrEmpty(userKeyFromRequest))
            {
                return userKeyFromRequest;
            }

            // Nếu không có, lấy key từ cookie (dành cho Member tự chat)
            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            if (memberCookie == null) return null;

            var memberLogin = new MemberLogin_Info(memberCookie);
            return memberLogin.MemberKey;
        }

        // File: Controllers/ChatWithAIController.cs

        [HttpGet("GetInitialData")]
        public async Task<IActionResult> GetInitialData([FromQuery] string? userKey = null)
        {
            try
            {
                string cacheKey;
                string backgroundData;

                // KIỂM TRA XEM ĐÂY LÀ REQUEST TỪ ADMIN HAY MEMBER
                if (!string.IsNullOrEmpty(userKey))
                {
                    // === LUỒNG XỬ LÝ CHO ADMIN ===
                    var adminKey = GetTargetMemberKey(userKey);
                    if (string.IsNullOrEmpty(adminKey)) return Unauthorized("Admin key not found.");

                    cacheKey = $"ChatBackgroundData_Admin_{adminKey}";
                    if (!_cache.TryGetValue(cacheKey, out backgroundData))
                    {
                        // Gọi hàm mới dành cho Admin
                        backgroundData = await ChatWithAIAccessData.LoadAdminOriginalDataAsync(adminKey);
                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15));
                        _cache.Set(cacheKey, backgroundData, cacheEntryOptions);
                    }

                    // Lấy lịch sử chat của Admin
                    var initialData = await ChatWithAIAccessData.GetInitialChatDataAsync(adminKey);
                    return Ok(initialData);
                }
                else
                {
                    // === LUỒNG XỬ LÝ CHO MEMBER (NHƯ CŨ) ===
                    var memberKey = GetTargetMemberKey(null);
                    if (string.IsNullOrEmpty(memberKey)) return Unauthorized("Member key not found.");

                    cacheKey = $"ChatBackgroundData_Member_{memberKey}";
                    if (!_cache.TryGetValue(cacheKey, out backgroundData))
                    {
                        // Gọi hàm cũ dành cho Member
                        backgroundData = await ChatWithAIAccessData.LoadMemberOriginalDataAsync(memberKey);
                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15));
                        _cache.Set(cacheKey, backgroundData, cacheEntryOptions);
                    }

                    // Lấy lịch sử chat của Member
                    var initialData = await ChatWithAIAccessData.GetInitialChatDataAsync(memberKey);
                    return Ok(initialData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetInitialData Error]: {ex.Message}");
                return StatusCode(500, "An internal server error occurred.");
            }
        }
        [HttpPost("CreateNewConversation")]
        // THAY ĐỔI: Nhận một object chứa userKey tùy chọn từ body
        public async Task<IActionResult> CreateNewConversation([FromBody] UserKeyRequest? request)
        {
            try
            {
                // THAY ĐỔI: Sử dụng hàm helper
                var targetMemberKey = GetTargetMemberKey(request?.UserKey);

                if (string.IsNullOrEmpty(targetMemberKey))
                {
                    return Unauthorized(new { success = false, message = "Member/User key could not be determined." });
                }

                var newConversationId = await ChatWithAIAccessData.CreateNewConversationAsync(targetMemberKey);

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
        // THAY ĐỔI: Thêm tham số userKey tùy chọn từ URL (query string)
        public async Task<IActionResult> GetAllConversations([FromQuery] string? userKey = null)
        {
            try
            {
                // THAY ĐỔI: Sử dụng hàm helper
                var targetMemberKey = GetTargetMemberKey(userKey);

                if (string.IsNullOrEmpty(targetMemberKey))
                {
                    return Unauthorized("Member/User key could not be determined.");
                }

                var conversations = await ChatWithAIAccessData.GetConversationsWithAIAsync(targetMemberKey);
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

                var targetMemberKey = GetTargetMemberKey(data.UserKey);
                if (string.IsNullOrEmpty(targetMemberKey))
                    return Unauthorized(new { success = false, message = "Member/User key could not be determined." });


                var apiKey = _configuration["GeminiApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                    return StatusCode(500, new { success = false, message = "AI service not configured." });

                await ChatWithAIAccessData.SaveMessageAsync(data.ConversationId, "user", data.Message);
                var cacheKey = $"MemberData_{targetMemberKey}";
                if (!_cache.TryGetValue(cacheKey, out string backgroundData))
                {
                    backgroundData = await ChatWithAIAccessData.LoadMemberOriginalDataAsync(targetMemberKey);
                    _cache.Set(cacheKey, backgroundData, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));
                }
                string recentFeedbacks = await ChatWithAIAccessData.LoadRecentFeedbacksAsync(targetMemberKey);
                var chatHistoryForPrompt = await ChatWithAIAccessData.GetMessageHistoryForApiAsync(data.ConversationId);
                string textPrompt = _promptService.BuildPromptForMember(backgroundData, recentFeedbacks, chatHistoryForPrompt, data.Message);

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

                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
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




        // File: Controllers/ChatWithAIController.cs

        // File: Controllers/ChatWithAIController.cs

        // File: Controllers/ChatWithAIController.cs

        [HttpPost("HandleAdminChat")]
        public async Task<IActionResult> HandleAdminChat([FromBody] ChatRequest data)
        {
            try
            {
                var adminKey = GetTargetMemberKey(data.UserKey);
                if (string.IsNullOrEmpty(adminKey))
                    return Unauthorized(new { success = false, message = "Admin key not found." });

                var apiKey = _configuration["GeminiApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                    return StatusCode(500, new { success = false, message = "API key is not configured." });

                // --- B1: Chuẩn bị dữ liệu ---
                var backgroundData = await ChatWithAIAccessData.LoadAdminOriginalDataAsync(adminKey);
                var chatHistory = await ChatWithAIAccessData.GetMessageHistoryForApiAsync(data.ConversationId);
                string initialPrompt = _promptService.BuildPromptForAdmin(backgroundData, chatHistory, data.Message);

                var tools = new List<GeminiTool>
        {
            new GeminiTool
            {
                FunctionDeclarations = new List<GeminiFunctionDeclaration>
                {
                    new GeminiFunctionDeclaration
                    {
                        Name = "get_member_summary",
                        Description = "Get a full profile of a member using their exact MemberID or a partial MemberName.",
                        Parameters = new GeminiSchema
                        {
                            Properties = new Dictionary<string, GeminiSchemaProperty>
                            {
                                { "member_identifier", new GeminiSchemaProperty { Type = "STRING", Description = "The exact MemberID or a part of the MemberName." } }
                            },
                            Required = new List<string> { "member_identifier" }
                        }
                    },
                    new GeminiFunctionDeclaration
                    {
                        Name = "count_questions_by_part",
                        Description = "Count the total number of questions for a specific TOEIC part.",
                        Parameters = new GeminiSchema
                        {
                            Properties = new Dictionary<string, GeminiSchemaProperty>
                            {
                                { "part_number", new GeminiSchemaProperty { Type = "NUMBER", Description = "The part number (1 through 7)." } }
                            },
                            Required = new List<string> { "part_number" }
                        }
                    }
                }
            }
        };

                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
                string finalAnswer = null;

                // --- B2: Loop xử lý ---
                var contentsList = new List<object>
        {
            new { role = "user", parts = new[] { new { text = initialPrompt } } }
        };

                while (true)
                {
                    JObject responseJson;
                    using (var client = new HttpClient())
                    {
                        var payload = new { contents = contentsList, tools };
                        var httpContent = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                        var httpResponse = await client.PostAsync(apiUrl, httpContent);
                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            var errorContent = await httpResponse.Content.ReadAsStringAsync();
                            throw new Exception($"API call failed: {errorContent}");
                        }
                        var jsonResponse = await httpResponse.Content.ReadAsStringAsync();
                        responseJson = JObject.Parse(jsonResponse);
                    }

                    var candidate = responseJson["candidates"]?[0];
                    var functionCallPart = candidate?["content"]?["parts"]?.FirstOrDefault(p => p["functionCall"] != null);

                    if (functionCallPart != null)
                    {
                        // --- Có functionCall ---
                        var functionCall = functionCallPart["functionCall"];
                        var functionName = functionCall["name"].ToString();
                        var args = functionCall["args"];
                        object functionResult = null;

                        if (functionName == "get_member_summary")
                        {
                            var identifier = args["member_identifier"].ToString();
                            functionResult = await ChatWithAIAccessData.GetMemberSummaryAsync(identifier);
                        }
                        else if (functionName == "count_questions_by_part")
                        {
                            var partNumber = (int)args["part_number"];
                            functionResult = new { total = await ChatWithAIAccessData.CountQuestionsByPartAsync(partNumber) };
                        }

                        // Gửi kết quả function cho AI để nó reasoning tiếp
                        var functionResponsePartObj = new
                        {
                            functionResponse = new { name = functionName, response = functionResult }
                        };

                        contentsList.Add(candidate["content"]); // add original function call
                        contentsList.Add(new { role = "user", parts = new[] { functionResponsePartObj } });
                    }
                    else
                    {
                        // --- Không còn functionCall, lấy câu trả lời cuối ---
                        finalAnswer = candidate?["content"]?["parts"]?[0]?["text"]?.ToString();
                        break;
                    }
                }

                // --- B3: Lưu vào DB và trả về ---
                await ChatWithAIAccessData.SaveMessageAsync(data.ConversationId, "user", data.Message);
                await ChatWithAIAccessData.SaveMessageAsync(data.ConversationId, "AI", finalAnswer);

                return Ok(new { success = true, message = finalAnswer ?? "Sorry, I couldn't process the request." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HandleAdminChat Error]: {ex}");
                return StatusCode(500, new { success = false, message = $"An error occurred: {ex.Message}" });
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
            // THAY ĐỔI: Đã xóa ScreenData không còn dùng
            public List<FileAttachmentDto>? Files { get; set; }
            public string? UserKey { get; set; } // THAY ĐỔI: Thêm UserKey
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
        public class UserKeyRequest
        {
            public string? UserKey { get; set; }
        }
        public class GeminiTool
        {
            [JsonProperty("function_declarations")]
            public List<GeminiFunctionDeclaration> FunctionDeclarations { get; set; }
        }

        public class GeminiFunctionDeclaration
        {
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("description")]
            public string Description { get; set; }
            [JsonProperty("parameters")]
            public GeminiSchema Parameters { get; set; }
        }

        public class GeminiSchema
        {
            [JsonProperty("type")]
            public string Type { get; set; } = "OBJECT";
            [JsonProperty("properties")]
            public Dictionary<string, GeminiSchemaProperty> Properties { get; set; }
            [JsonProperty("required")]
            public List<string> Required { get; set; }
        }

        public class GeminiSchemaProperty
        {
            [JsonProperty("type")]
            public string Type { get; set; }
            [JsonProperty("description")]
            public string Description { get; set; }
        }
    }
}