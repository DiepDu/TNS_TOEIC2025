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

                var memberKey = GetTargetMemberKey(data.UserKey);
                if (string.IsNullOrEmpty(memberKey))
                    return Unauthorized(new { success = false, message = "Member key could not be determined." });

                var apiKey = _configuration["GeminiApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                    return StatusCode(500, new { success = false, message = "AI service not configured." });

                var cacheKey = $"ChatBackgroundData_Member_{memberKey}";
                if (!_cache.TryGetValue(cacheKey, out string backgroundData))
                {
                    backgroundData = await ChatWithAIAccessData.LoadMemberOriginalDataAsync(memberKey);
                    _cache.Set(cacheKey, backgroundData, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));
                }
                string recentFeedbacks = await ChatWithAIAccessData.LoadRecentFeedbacksAsync(memberKey);
                var chatHistoryForPrompt = await ChatWithAIAccessData.GetMessageHistoryForApiAsync(data.ConversationId);
                string initialPrompt = _promptService.BuildPromptForMember(backgroundData, recentFeedbacks, chatHistoryForPrompt, data.Message);

                // === BƯỚC 1: CẬP NHẬT KHAI BÁO TOOL ===
                var tools = new List<GeminiTool> {
            new GeminiTool {
                FunctionDeclarations = new List<GeminiFunctionDeclaration> {
                    new GeminiFunctionDeclaration {
                        Name = "get_test_analysis_by_date",
                        Description = "Retrieves a detailed error analysis for a test completed on a specific date.",
                        Parameters = new GeminiSchema {
                            Properties = new Dictionary<string, GeminiSchemaProperty> {
                                { "test_date", new GeminiSchemaProperty { Type = "STRING", Description = "The date of the test to analyze, in 'yyyy-mm-dd' format." } }
                            },
                            Required = new List<string> { "test_date" }
                        }
                    },
                    new GeminiFunctionDeclaration {
                        Name = "find_my_incorrect_questions_by_topic",
                        Description = "Finds questions the user answered incorrectly related to a specific grammar or vocabulary topic.",
                        Parameters = new GeminiSchema {
                            Properties = new Dictionary<string, GeminiSchemaProperty> {
                                { "topic_name", new GeminiSchemaProperty { Type = "STRING", Description = "The name of the topic to search for (e.g., 'prepositions', 'tenses')." } },
                                { "limit", new GeminiSchemaProperty { Type = "NUMBER", Description = "The maximum number of questions to return. Defaults to 5." } }
                            },
                            Required = new List<string> { "topic_name" }
                        }
                    }
                }
            }
        };

                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
                string finalAnswer = null;
                var contentsList = new List<object> {
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
                        var functionCall = functionCallPart["functionCall"];
                        var functionName = functionCall["name"].ToString();
                        var args = functionCall["args"];
                        object functionResult = null;

                        // === BƯỚC 2: THÊM LOGIC GỌI HÀM MỚI ===
                        if (functionName == "get_test_analysis_by_date")
                        {
                            if (DateTime.TryParse(args["test_date"].ToString(), out var testDate))
                            {
                                functionResult = await ChatWithAIAccessData.GetTestAnalysisByDateAsync(memberKey, testDate);
                            }
                            else
                            {
                                functionResult = "Ngày không hợp lệ. Vui lòng sử dụng định dạng yyyy-mm-dd.";
                            }
                        }
                        else if (functionName == "find_my_incorrect_questions_by_topic")
                        {
                            functionResult = await ChatWithAIAccessData.FindMyIncorrectQuestionsByTopicAsync(
                                memberKey,
                                args["topic_name"].ToString(),
                                args["limit"]?.ToObject<int?>() ?? 5
                            );
                        }

                        var functionResponsePartObj = new
                        {
                            functionResponse = new
                            {
                                name = functionName,
                                response = new { result = functionResult }
                            }
                        };

                        contentsList.Add(candidate["content"]);
                        contentsList.Add(new { role = "user", parts = new[] { functionResponsePartObj } });
                    }
                    else
                    {
                        finalAnswer = candidate?["content"]?["parts"]?[0]?["text"]?.ToString();
                        break;
                    }
                }

                await ChatWithAIAccessData.SaveMessageAsync(data.ConversationId, "user", data.Message);
                await ChatWithAIAccessData.SaveMessageAsync(data.ConversationId, "AI", finalAnswer);

                return Ok(new { success = true, message = finalAnswer ?? "Sorry, I couldn't process the request." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HandleMemberChat Error]: {ex}");
                return StatusCode(500, new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }





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
                        Name = "GetQuestionCounts", // <-- Tên mới
                        Description = "Counts and categorizes all questions in the question bank.", // <-- Mô tả mới
                        Parameters = new GeminiSchema
                        {
                            // Tham số rỗng vì hàm mới không cần tham số
                            Properties = new Dictionary<string, GeminiSchemaProperty> { },
                            Required = new List<string> { }
                        }
                    },
               // ...
new GeminiFunctionDeclaration
{
    Name = "find_questions_by_criteria",
    Description = "Finds questions in the bank based on their properties.",
    Parameters = new GeminiSchema
    {
        Properties = new Dictionary<string, GeminiSchemaProperty>
        {
            { "part", new GeminiSchemaProperty { Type = "NUMBER", Description = "The TOEIC part number (1-7)." } },
            // === SỬA DÒNG MÔ TẢ NÀY ===
            { "correct_rate_condition", new GeminiSchemaProperty { Type = "STRING", Description = "Filter by correct answer rate (0-100 scale), e.g., '< 30' for hard questions or '> 80' for easy ones." } },
            { "topic_name", new GeminiSchemaProperty { Type = "STRING", Description = "Filter by a grammar or vocabulary topic name." } },
            { "has_anomaly", new GeminiSchemaProperty { Type = "BOOLEAN", Description = "Filter for questions marked as anomalous." } },
            { "min_feedback_count", new GeminiSchemaProperty { Type = "NUMBER", Description = "Minimum number of user feedbacks." } },
            { "sort_by", new GeminiSchemaProperty { Type = "STRING", Description = "Sorts results. Use 'CorrectRate_ASC' for easiest, 'CorrectRate_DESC' for hardest, 'FeedbackCount_DESC' for most feedback." } },
            { "limit", new GeminiSchemaProperty { Type = "NUMBER", Description = "Max number of questions to return." } }
        }
    }
},
// ...
                    new GeminiFunctionDeclaration
                    {
                        Name = "get_unresolved_feedbacks",
                        Description = "Retrieves the latest unresolved user feedbacks about questions.",
                        Parameters = new GeminiSchema
                        {
                            Properties = new Dictionary<string, GeminiSchemaProperty>
                            {
                                { "limit", new GeminiSchemaProperty { Type = "NUMBER", Description = "Max number of feedbacks to return." } }
                            }
                        }
                    },
                    new GeminiFunctionDeclaration
                    {
                        Name = "get_system_activity_summary",
                        Description = "Provides a summary of system activity over a date range.",
                        Parameters = new GeminiSchema
                        {
                            Properties = new Dictionary<string, GeminiSchemaProperty>
                            {
                                { "start_date", new GeminiSchemaProperty { Type = "STRING", Description = "The start date in 'yyyy-mm-dd' format." } },
                                { "end_date", new GeminiSchemaProperty { Type = "STRING", Description = "The end date in 'yyyy-mm-dd' format." } }
                            },
                            Required = new List<string> { "start_date", "end_date" }
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
                        Console.WriteLine($"[DEBUG] Gemini Function Call: {functionCallPart.ToString()}");

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
                        else if (functionName == "GetQuestionCounts") // <-- Kiểm tra tên hàm mới
                        {
                            // Gọi hàm mới không cần tham số và lấy kết quả dictionary
                            functionResult = await ChatWithAIAccessData.GetQuestionCountsAsync();
                        }
                        else if (functionName == "find_members_by_criteria")
                        {
                            functionResult = await ChatWithAIAccessData.FindMembersByCriteriaAsync(
                                args["score_condition"]?.ToString(),
                                args["last_login_before"]?.ToString(),
                                args["min_tests_completed"]?.ToObject<int?>(),
                                args["sort_by"]?.ToString() ?? "LastLoginDate",
                                args["limit"]?.ToObject<int?>() ?? 10
                            );
                        }
                        else if (functionName == "find_questions_by_criteria")
                        {
                            // === ĐOẠN CODE ĐÃ SỬA LỖI ===
                            functionResult = await ChatWithAIAccessData.FindQuestionsByCriteriaAsync(
                                args["part"]?.ToObject<int?>(),
                                args["correct_rate_condition"]?.ToString(),
                                args["topic_name"]?.ToString(),
                                args["has_anomaly"]?.ToObject<bool?>(),
                                args["min_feedback_count"]?.ToObject<int?>(),
                                args["sort_by"]?.ToString(), // Tham số thứ 6 (string)
                                args["limit"]?.ToObject<int?>() ?? 10 // Tham số thứ 7 (int)
                            );
                        }
                        else if (functionName == "get_unresolved_feedbacks")
                        {
                            functionResult = await ChatWithAIAccessData.GetUnresolvedFeedbacksAsync(
                                args["limit"]?.ToObject<int?>() ?? 10
                            );
                        }
                        else if (functionName == "get_system_activity_summary")
                        {
                            var startDate = DateTime.Parse(args["start_date"].ToString());
                            var endDate = DateTime.Parse(args["end_date"].ToString());
                            functionResult = await ChatWithAIAccessData.GetSystemActivitySummaryAsync(startDate, endDate);
                        }
                        var functionResponsePartObj = new
                        {
                            functionResponse = new
                            {
                                name = functionName,
                                response = new { result = functionResult } // <-- Gói kết quả vào trong một đối tượng mới
                            }
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