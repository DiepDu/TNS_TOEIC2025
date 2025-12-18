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
        private readonly GeminiApiKeyManager _apiKeyManager;

        public ChatWithAIController(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            IMemoryCache memoryCache,
            GeminiApiKeyManager apiKeyManager)
        {
            _promptService = new PromptEngineeringService();
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _cache = memoryCache;
            _apiKeyManager = apiKeyManager;
        }

        // ✅✅✅ RETRY LOGIC MỚI - GHI LOG MỖI LẦN GỌI
        private async Task<JObject> CallGeminiApiWithRetryAsync(
            HttpClient client,
            string apiUrl,
            object payload,
            int selectedKeyId, // ✅ THÊM THAM SỐ
            int maxRetries = 5)
        {
            int retryCount = 0;
            int delayMs = 2000;

            while (retryCount < maxRetries)
            {
                try
                {
                    var httpContent = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    var httpResponse = await client.PostAsync(apiUrl, httpContent);

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var jsonResponse = await httpResponse.Content.ReadAsStringAsync();

                        // ✅ GHI LOG THÀNH CÔNG
                        await _apiKeyManager.LogApiCallAttemptAsync(selectedKeyId, isSuccess: true);
                        Console.WriteLine($"✅ API call succeeded - Logged for KeyID {selectedKeyId}");

                        return JObject.Parse(jsonResponse);
                    }

                    // ✅ GHI LOG THẤT BẠI
                    await _apiKeyManager.LogApiCallAttemptAsync(selectedKeyId, isSuccess: false);
                    Console.WriteLine($"❌ API call failed - Logged for KeyID {selectedKeyId}");

                    var errorContent = await httpResponse.Content.ReadAsStringAsync();
                    var errorObj = JObject.Parse(errorContent);

                    var errorCode = errorObj["error"]?["code"]?.ToObject<int>();
                    if (errorCode == 503 || errorCode == 429)
                    {
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            Console.WriteLine($"[Gemini API] {errorCode} Error - Retry {retryCount}/{maxRetries} after {delayMs}ms");
                            await Task.Delay(delayMs);
                            delayMs = Math.Min(delayMs * 2, 30000);
                            continue; // ✅ Retry - sẽ ghi log lần nữa
                        }
                    }

                    throw new Exception($"API call failed: {errorContent}");
                }
                catch (HttpRequestException httpEx)
                {
                    // ✅ GHI LOG NETWORK ERROR
                    await _apiKeyManager.LogApiCallAttemptAsync(selectedKeyId, isSuccess: false);
                    Console.WriteLine($"❌ Network error - Logged for KeyID {selectedKeyId}");

                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        Console.WriteLine($"[Gemini API] Network error - Retry {retryCount}/{maxRetries} after {delayMs}ms");
                        await Task.Delay(delayMs);
                        delayMs = Math.Min(delayMs * 2, 30000);
                        continue; // ✅ Retry - sẽ ghi log lần nữa
                    }
                    throw new Exception($"Network error after {maxRetries} retries: {httpEx.Message}");
                }
            }

            throw new Exception($"Gemini API overloaded after {maxRetries} retries. Please try again later.");
        }

        private string GetTargetMemberKey(string? userKeyFromRequest)
        {
            if (!string.IsNullOrEmpty(userKeyFromRequest))
            {
                return userKeyFromRequest;
            }

            var memberCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            if (memberCookie == null) return null;

            var memberLogin = new MemberLogin_Info(memberCookie);
            return memberLogin.MemberKey;
        }

        [HttpGet("GetInitialData")]
        public async Task<IActionResult> GetInitialData([FromQuery] string? userKey = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(userKey))
                {
                    var adminKey = GetTargetMemberKey(userKey);
                    if (string.IsNullOrEmpty(adminKey)) return Unauthorized("Admin key not found.");

                    var cacheKey = $"ChatBackgroundData_Admin_{adminKey}";
                    if (!_cache.TryGetValue(cacheKey, out string backgroundData))
                    {
                        backgroundData = await ChatWithAIAccessData.LoadAdminOriginalDataAsync(adminKey);
                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15));
                        _cache.Set(cacheKey, backgroundData, cacheEntryOptions);
                    }

                    var initialData = await ChatWithAIAccessData.GetInitialChatDataAsync(adminKey);
                    return Ok(initialData);
                }
                else
                {
                    var memberKey = GetTargetMemberKey(null);
                    if (string.IsNullOrEmpty(memberKey)) return Unauthorized("Member key not found.");

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
        public async Task<IActionResult> CreateNewConversation([FromBody] UserKeyRequest? request)
        {
            try
            {
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

        [HttpGet("GetAllConversations")]
        public async Task<IActionResult> GetAllConversations([FromQuery] string? userKey = null)
        {
            try
            {
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

        // ✅✅✅ HANDLE MEMBER CHAT - HOÀN CHỈNH
        [HttpPost("HandleMemberChat")]
        public async Task<IActionResult> HandleMemberChat([FromBody] ChatRequest data)
        {
            try
            {
                var memberKey = GetTargetMemberKey(data.UserKey);
                if (string.IsNullOrEmpty(memberKey))
                    return Unauthorized(new { success = false, message = "Member key could not be determined." });

                // ✅ LẤY KEY KHẢ DỤNG
                var keyResult = await _apiKeyManager.GetAvailableApiKeyAsync();

                if (!keyResult.Success || keyResult.ApiKey == null)
                {
                    Console.WriteLine($"⚠️ No available API key - {keyResult.ErrorMessage}");
                    return StatusCode(429, new
                    {
                        success = false,
                        message = $"⚠️ {keyResult.ErrorMessage}",
                        isRetryable = false
                    });
                }

                var selectedKey = keyResult.ApiKey;
                var apiKey = selectedKey.ActualApiKey;
                Console.WriteLine($"✅ Using {selectedKey.KeyName} (KeyID: {selectedKey.KeyID}) - Current: {selectedKey.UsageCount}/{selectedKey.DailyLimit}");

                int optimalLimit = SmartContextService.GetOptimalLimit(data.Message);
                SmartContextService.LogTokenEstimate(optimalLimit, data.Message);

                var cacheKey = $"ChatBasicProfile_Member_{memberKey}";
                if (!_cache.TryGetValue(cacheKey, out string basicProfile))
                {
                    basicProfile = await ChatWithAIAccessData.LoadMemberBasicProfileAsync(memberKey);
                    _cache.Set(cacheKey, basicProfile, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));
                }

                var chatHistory = await ChatWithAIAccessData.GetMessageHistoryForApiAsync(data.ConversationId, optimalLimit);

                var initialParts = new List<object>();
                string textPrompt = _promptService.BuildPromptForMember(basicProfile, chatHistory, data.Message);
                initialParts.Add(new { text = textPrompt });

                // ✅ PROCESS ATTACHED FILES
                if (data.Files != null && data.Files.Count > 0)
                {
                    Console.WriteLine($"[HandleMemberChat] Processing {data.Files.Count} attached files");

                    foreach (var file in data.Files)
                    {
                        try
                        {
                            if (file.MimeType.StartsWith("image/"))
                            {
                                initialParts.Add(new
                                {
                                    inline_data = new
                                    {
                                        mime_type = file.MimeType,
                                        data = file.Base64Data
                                    }
                                });
                                Console.WriteLine($"[HandleMemberChat] ✅ Added image: {file.FileName}");
                            }
                            else if (file.MimeType == "application/pdf")
                            {
                                var pdfText = ExtractTextFromPdf(file.Base64Data);
                                initialParts.Add(new { text = $"\n\n--- Content from {file.FileName} ---\n{pdfText}\n--- End ---\n" });
                                Console.WriteLine($"[HandleMemberChat] ✅ Extracted PDF: {file.FileName}");
                            }
                            else if (file.MimeType.StartsWith("text/"))
                            {
                                var textContent = Encoding.UTF8.GetString(Convert.FromBase64String(file.Base64Data));
                                initialParts.Add(new { text = $"\n\n--- Content from {file.FileName} ---\n{textContent}\n--- End ---\n" });
                                Console.WriteLine($"[HandleMemberChat] ✅ Added text: {file.FileName}");
                            }
                            else if (file.MimeType.Contains("wordprocessingml"))
                            {
                                var docxText = ExtractTextFromDocx(file.Base64Data);
                                initialParts.Add(new { text = $"\n\n--- Content from {file.FileName} ---\n{docxText}\n--- End ---\n" });
                                Console.WriteLine($"[HandleMemberChat] ✅ Extracted DOCX: {file.FileName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[HandleMemberChat] ❌ Error: {file.FileName} - {ex.Message}");
                        }
                    }
                }

                var tools = new List<GeminiTool> {
                    new GeminiTool {
                        FunctionDeclarations = new List<GeminiFunctionDeclaration> {
                            new GeminiFunctionDeclaration {
                                Name = "get_my_performance_analysis",
                                Description = "Get detailed analysis of student's current ability including IRT analysis, part scores, and progress to target.",
                                Parameters = new GeminiSchema { Properties = new Dictionary<string, GeminiSchemaProperty> { } }
                            },
                            new GeminiFunctionDeclaration {
                                Name = "get_my_error_analysis",
                                Description = "Get detailed error analysis including top grammar/vocabulary/error type mistakes.",
                                Parameters = new GeminiSchema {
                                    Properties = new Dictionary<string, GeminiSchemaProperty> {
                                        { "limit", new GeminiSchemaProperty { Type = "NUMBER", Description = "Number of errors to analyze (default: 50)" } }
                                    }
                                }
                            },
                            new GeminiFunctionDeclaration {
                                Name = "get_my_recent_mistakes",
                                Description = "Get detailed list of recent incorrect questions with explanations.",
                                Parameters = new GeminiSchema {
                                    Properties = new Dictionary<string, GeminiSchemaProperty> {
                                        { "limit", new GeminiSchemaProperty { Type = "NUMBER", Description = "Number of mistakes to show (default: 10)" } }
                                    }
                                }
                            },
                            new GeminiFunctionDeclaration {
                                Name = "get_my_behavior_analysis",
                                Description = "Get analysis of test-taking behavior (time management, answer changes, etc.).",
                                Parameters = new GeminiSchema { Properties = new Dictionary<string, GeminiSchemaProperty> { } }
                            },
                            new GeminiFunctionDeclaration {
                                Name = "load_recent_feedbacks",
                                Description = "Get student's recent feedback/questions about test items.",
                                Parameters = new GeminiSchema { Properties = new Dictionary<string, GeminiSchemaProperty> { } }
                            },
                            new GeminiFunctionDeclaration {
                                Name = "get_recommended_questions",
                                Description = @"Get personalized practice questions based on error patterns and IRT ability.

**IMPORTANT RENDERING RULES:**
- For Part 3, 4, 6, 7: Questions may share the same passage/audio (ParentText/ParentAudioUrl)
- When displaying results, show the SHARED PASSAGE ONCE at the top, then list all questions below it
- DO NOT repeat the passage for each question",
                                Parameters = new GeminiSchema {
                                    Properties = new Dictionary<string, GeminiSchemaProperty> {
                                        { "part", new GeminiSchemaProperty { Type = "NUMBER", Description = "TOEIC part (1-7)" } },
                                        { "limit", new GeminiSchemaProperty { Type = "NUMBER", Description = "Number of questions (default: 10)" } }
                                    },
                                    Required = new List<string> { "part" }
                                }
                            },
                            new GeminiFunctionDeclaration {
                                Name = "get_test_analysis_by_date",
                                Description = "Get detailed error analysis for a specific test taken on a given date.",
                                Parameters = new GeminiSchema {
                                    Properties = new Dictionary<string, GeminiSchemaProperty> {
                                        { "test_date", new GeminiSchemaProperty { Type = "STRING", Description = "Date in 'yyyy-mm-dd' format" } },
                                        { "exact_score", new GeminiSchemaProperty { Type = "NUMBER", Description = "Optional: exact score" } },
                                        { "exact_time", new GeminiSchemaProperty { Type = "STRING", Description = "Optional: time in 'HH:mm' format" } }
                                    },
                                    Required = new List<string> { "test_date" }
                                }
                            },
                            new GeminiFunctionDeclaration {
                                Name = "get_my_incorrect_questions_by_part",
                                Description = "Get detailed list of recent incorrect questions for a specific TOEIC Part (1-7).",
                                Parameters = new GeminiSchema {
                                    Properties = new Dictionary<string, GeminiSchemaProperty> {
                                        { "part", new GeminiSchemaProperty { Type = "NUMBER", Description = "TOEIC part number (1-7)" } },
                                        { "limit", new GeminiSchemaProperty { Type = "NUMBER", Description = "Number of mistakes to show (default: 10)" } }
                                    },
                                    Required = new List<string> { "part" }
                                }
                            },
                            new GeminiFunctionDeclaration {
                                Name = "find_my_incorrect_questions_by_topics",
                                Description = @"Find incorrect questions by topic names. 
**IMPORTANT:** ALL database topics are in ENGLISH. 
You MUST translate Vietnamese keywords to English before calling this tool.",
                                Parameters = new GeminiSchema {
                                    Properties = new Dictionary<string, GeminiSchemaProperty> {
                                        {
                                            "grammar_topics",
                                            new GeminiSchemaProperty {
                                                Type = "ARRAY",
                                                Description = "English grammar topic names (e.g., 'Preposition', 'Tense')",
                                                Items = new GeminiSchemaProperty { Type = "STRING" }
                                            }
                                        },
                                        {
                                            "vocabulary_topics",
                                            new GeminiSchemaProperty {
                                                Type = "ARRAY",
                                                Description = "English vocabulary topic names (e.g., 'Marketing')",
                                                Items = new GeminiSchemaProperty { Type = "STRING" }
                                            }
                                        },
                                        {
                                            "categories",
                                            new GeminiSchemaProperty {
                                                Type = "ARRAY",
                                                Description = "English category names (e.g., 'Inference')",
                                                Items = new GeminiSchemaProperty { Type = "STRING" }
                                            }
                                        },
                                        {
                                            "error_types",
                                            new GeminiSchemaProperty {
                                                Type = "ARRAY",
                                                Description = "English error type names (e.g., 'Word Form Error')",
                                                Items = new GeminiSchemaProperty { Type = "STRING" }
                                            }
                                        },
                                        {
                                            "limit",
                                            new GeminiSchemaProperty {
                                                Type = "NUMBER",
                                                Description = "Max results (default: 10)"
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
                string finalAnswer = null;
                var contentsList = new List<object> { new { role = "user", parts = initialParts } };

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(2);

                    while (true)
                    {
                        var payload = new { contents = contentsList, tools };

                        // ✅ TRUYỀN KeyID VÀO ĐỂ GHI LOG
                        JObject responseJson = await CallGeminiApiWithRetryAsync(
                            client,
                            apiUrl,
                            payload,
                            selectedKey.KeyID // ✅ QUAN TRỌNG
                        );

                        var candidate = responseJson["candidates"]?[0];
                        var functionCallPart = candidate?["content"]?["parts"]?.FirstOrDefault(p => p["functionCall"] != null);

                        if (functionCallPart != null)
                        {
                            var functionCall = functionCallPart["functionCall"];
                            var functionName = functionCall["name"].ToString();
                            var args = functionCall["args"];

                            Console.WriteLine($"[AI Function Call] {functionName}");
                            object functionResult = null;

                            switch (functionName)
                            {
                                case "get_my_performance_analysis":
                                    var perfCacheKey = $"PerformanceAnalysis_{memberKey}";
                                    if (!_cache.TryGetValue(perfCacheKey, out object cachedPerf))
                                    {
                                        cachedPerf = await ChatWithAIAccessData.GetMyPerformanceAnalysisAsync(memberKey);
                                        _cache.Set(perfCacheKey, cachedPerf, TimeSpan.FromMinutes(15));
                                    }
                                    functionResult = cachedPerf;
                                    await RebuildPromptWithLimitedHistoryAsync(contentsList, data.ConversationId, basicProfile, data.Message, 5);
                                    break;

                                case "get_my_error_analysis":
                                    var errCacheKey = $"ErrorAnalysis_{memberKey}";
                                    if (!_cache.TryGetValue(errCacheKey, out object cachedErr))
                                    {
                                        int limit = args["limit"]?.ToObject<int?>() ?? 50;
                                        cachedErr = await ChatWithAIAccessData.GetMyErrorAnalysisAsync(memberKey, limit);
                                        _cache.Set(errCacheKey, cachedErr, TimeSpan.FromMinutes(15));
                                    }
                                    functionResult = cachedErr;
                                    await RebuildPromptWithLimitedHistoryAsync(contentsList, data.ConversationId, basicProfile, data.Message, 5);
                                    break;

                                case "get_my_recent_mistakes":
                                    int mistakesLimit = args["limit"]?.ToObject<int?>() ?? 10;
                                    functionResult = await ChatWithAIAccessData.GetMyRecentMistakesAsync(memberKey, mistakesLimit);
                                    await RebuildPromptWithLimitedHistoryAsync(contentsList, data.ConversationId, basicProfile, data.Message, 5);
                                    break;

                                case "get_my_behavior_analysis":
                                    var behavCacheKey = $"BehaviorAnalysis_{memberKey}";
                                    if (!_cache.TryGetValue(behavCacheKey, out object cachedBehav))
                                    {
                                        cachedBehav = await ChatWithAIAccessData.GetMyBehaviorAnalysisAsync(memberKey);
                                        _cache.Set(behavCacheKey, cachedBehav, TimeSpan.FromMinutes(15));
                                    }
                                    functionResult = cachedBehav;
                                    await RebuildPromptWithLimitedHistoryAsync(contentsList, data.ConversationId, basicProfile, data.Message, 5);
                                    break;

                                case "get_my_incorrect_questions_by_part":
                                    int part = args["part"]?.ToObject<int>() ?? 1;
                                    int partLimit = args["limit"]?.ToObject<int?>() ?? 10;
                                    functionResult = await ChatWithAIAccessData.GetMyIncorrectQuestionsByPartAsync(memberKey, part, partLimit);
                                    await RebuildPromptWithLimitedHistoryAsync(contentsList, data.ConversationId, basicProfile, data.Message, 5);
                                    break;

                                case "load_recent_feedbacks":
                                    functionResult = await ChatWithAIAccessData.LoadRecentFeedbacksAsync(memberKey);
                                    await RebuildPromptWithLimitedHistoryAsync(contentsList, data.ConversationId, basicProfile, data.Message, 5);
                                    break;

                                case "get_recommended_questions":
                                    try
                                    {
                                        int recPart = args["part"]?.ToObject<int>() ?? 1;
                                        int recLimit = args["limit"]?.ToObject<int?>() ?? 10;
                                        var result = await ChatWithAIAccessData.GetRecommendedQuestionsAsync(memberKey, recPart, recLimit);

                                        if (result == null || result.Count == 0)
                                        {
                                            functionResult = new
                                            {
                                                success = false,
                                                message = $"No suitable questions available for Part {recPart} at this time.",
                                                suggestions = new[] { "Try practicing other parts", "Complete more tests to generate personalized recommendations" }
                                            };
                                        }
                                        else
                                        {
                                            functionResult = result;

                                            if (recPart == 7)
                                            {
                                                Console.WriteLine($"[Part 7] Switching to LITE prompt (no history)...");
                                                string litePrompt = _promptService.BuildLitePromptForToolResult(basicProfile, data.Message, functionName, recPart);
                                                contentsList[0] = new { role = "user", parts = new[] { new { text = litePrompt } } };
                                                Console.WriteLine($"[Part 7] Lite prompt: {litePrompt.Length} chars");
                                            }
                                            else
                                            {
                                                await RebuildPromptWithLimitedHistoryAsync(contentsList, data.ConversationId, basicProfile, data.Message, 5);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"[get_recommended_questions ERROR]: {ex.Message}");
                                        functionResult = new { success = false, error = "Failed to retrieve recommendations", details = ex.Message };
                                    }
                                    break;

                                case "get_test_analysis_by_date":
                                    if (DateTime.TryParse(args["test_date"].ToString(), out var testDate))
                                    {
                                        int? score = args["exact_score"]?.ToObject<int?>();
                                        TimeSpan? time = null;
                                        if (args["exact_time"] != null && TimeSpan.TryParse(args["exact_time"].ToString(), out var parsedTime))
                                            time = parsedTime;
                                        functionResult = await ChatWithAIAccessData.GetTestAnalysisByDateAsync(memberKey, testDate, score, time);
                                    }
                                    else
                                    {
                                        functionResult = "Invalid date format. Use yyyy-mm-dd.";
                                    }
                                    await RebuildPromptWithLimitedHistoryAsync(contentsList, data.ConversationId, basicProfile, data.Message, 5);
                                    break;

                                case "find_my_incorrect_questions_by_topics":
                                    var grammarTopics = args["grammar_topics"]?.ToObject<List<string>>();
                                    var vocabularyTopics = args["vocabulary_topics"]?.ToObject<List<string>>();
                                    var categories = args["categories"]?.ToObject<List<string>>();
                                    var errorTypes = args["error_types"]?.ToObject<List<string>>();
                                    int topicLimit = args["limit"]?.ToObject<int?>() ?? 10;
                                    functionResult = await ChatWithAIAccessData.FindMyIncorrectQuestionsByTopicNamesAsync(
                                        memberKey, grammarTopics, vocabularyTopics, categories, errorTypes, topicLimit);
                                    await RebuildPromptWithLimitedHistoryAsync(contentsList, data.ConversationId, basicProfile, data.Message, 5);
                                    break;
                            }

                            var functionResponsePartObj = new
                            {
                                functionResponse = new { name = functionName, response = new { result = functionResult } }
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
                }

                if (!string.IsNullOrEmpty(finalAnswer))
                {
                    Console.WriteLine($"[HandleMemberChat] Final answer: {finalAnswer.Length} chars");
                }

                await ChatWithAIAccessData.SaveMessageAsync(data.ConversationId, "user", data.Message);
                await ChatWithAIAccessData.SaveMessageAsync(data.ConversationId, "AI", finalAnswer);

                return new ContentResult
                {
                    StatusCode = 200,
                    ContentType = "application/json; charset=utf-8",
                    Content = JsonConvert.SerializeObject(new { success = true, message = finalAnswer ?? "Sorry, I couldn't process the request." })
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HandleMemberChat Error]: {ex}");

                if (ex.Message.Contains("503") || ex.Message.Contains("overloaded"))
                {
                    return StatusCode(503, new { success = false, message = "⚠️ AI service is currently overloaded. Please try again in a few seconds.", isRetryable = true });
                }

                return StatusCode(500, new { success = false, message = $"An error occurred: {ex.Message}", isRetryable = false });
            }
        }

        // ✅ TƯƠNG TỰ CHO HandleAdminChat (logic giống HandleMemberChat)
        [HttpPost("HandleAdminChat")]
        public async Task<IActionResult> HandleAdminChat([FromBody] ChatRequest data)
        {
            try
            {
                var adminKey = GetTargetMemberKey(data.UserKey);
                if (string.IsNullOrEmpty(adminKey))
                    return Unauthorized(new { success = false, message = "Admin key not found." });

                var keyResult = await _apiKeyManager.GetAvailableApiKeyAsync();

                if (!keyResult.Success || keyResult.ApiKey == null)
                {
                    return StatusCode(429, new
                    {
                        success = false,
                        message = keyResult.ErrorMessage,
                        isRetryable = false
                    });
                }

                var selectedKey = keyResult.ApiKey;
                var apiKey = selectedKey.ActualApiKey;
                Console.WriteLine($"✅ [Admin] Using {selectedKey.KeyName} (KeyID: {selectedKey.KeyID})");

                var backgroundData = await ChatWithAIAccessData.LoadAdminOriginalDataAsync(adminKey);
                var chatHistory = await ChatWithAIAccessData.GetMessageHistoryForApiAsync(data.ConversationId, 10);
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
                                Name = "GetQuestionCounts",
                                Description = "Counts and categorizes all questions in the question bank.",
                                Parameters = new GeminiSchema
                                {
                                    Properties = new Dictionary<string, GeminiSchemaProperty> { },
                                    Required = new List<string> { }
                                }
                            },
                            new GeminiFunctionDeclaration
                            {
                                Name = "find_questions_by_criteria",
                                Description = "Finds questions in the bank based on their properties, including IRT analysis.",
                                Parameters = new GeminiSchema
                                {
                                    Properties = new Dictionary<string, GeminiSchemaProperty>
                                    {
                                        { "part", new GeminiSchemaProperty { Type = "NUMBER", Description = "The TOEIC part number (1-7)." } },
                                        { "correct_rate_condition", new GeminiSchemaProperty { Type = "STRING", Description = "Filter by correct answer rate (0-100 scale), e.g., '< 30' for hard questions." } },
                                        { "topic_name", new GeminiSchemaProperty { Type = "STRING", Description = "Filter by a grammar or vocabulary topic name." } },
                                        { "has_anomaly", new GeminiSchemaProperty { Type = "BOOLEAN", Description = "Filter for questions marked as anomalous (true/false)." } },
                                        { "min_feedback_count", new GeminiSchemaProperty { Type = "NUMBER", Description = "Minimum number of user feedbacks." } },
                                        { "sort_by", new GeminiSchemaProperty { Type = "STRING", Description = "Sorts results. Use 'IRT_DIFFICULTY_ASC' for easiest by IRT, 'IRT_DIFFICULTY_DESC' for hardest." } },
                                        { "limit", new GeminiSchemaProperty { Type = "NUMBER", Description = "Max number of questions to return." } },
                                        { "irt_difficulty_condition", new GeminiSchemaProperty { Type = "STRING", Description = "Filter by IRT difficulty (float 0-3), e.g., '> 1.5' for difficult questions." } },
                                        { "quality_filter", new GeminiSchemaProperty { Type = "STRING", Description = "Filter by question quality: 'Excellent', 'Good', 'Fair', 'Poor'." } }
                                    }
                                }
                            },
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

                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-exp:generateContent?key={apiKey}";
                string finalAnswer = null;

                var contentsList = new List<object>
                {
                    new { role = "user", parts = new[] { new { text = initialPrompt } } }
                };

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(2);

                    while (true)
                    {
                        var payload = new { contents = contentsList, tools };

                        // ✅ TRUYỀN KeyID
                        JObject responseJson = await CallGeminiApiWithRetryAsync(
                            client,
                            apiUrl,
                            payload,
                            selectedKey.KeyID
                        );

                        var candidate = responseJson["candidates"]?[0];
                        var functionCallPart = candidate?["content"]?["parts"]?.FirstOrDefault(p => p["functionCall"] != null);

                        if (functionCallPart != null)
                        {
                            var functionCall = functionCallPart["functionCall"];
                            var functionName = functionCall["name"].ToString();
                            var args = functionCall["args"];
                            object functionResult = null;

                            if (functionName == "get_member_summary")
                            {
                                var identifier = args["member_identifier"].ToString();
                                functionResult = await ChatWithAIAccessData.GetMemberSummaryAsync(identifier);
                            }
                            else if (functionName == "GetQuestionCounts")
                            {
                                functionResult = await ChatWithAIAccessData.GetQuestionCountsAsync();
                            }
                            else if (functionName == "find_questions_by_criteria")
                            {
                                functionResult = await ChatWithAIAccessData.FindQuestionsByCriteriaAsync(
                                    args["part"]?.ToObject<int?>(),
                                    args["correct_rate_condition"]?.ToString(),
                                    args["topic_name"]?.ToString(),
                                    args["has_anomaly"]?.ToObject<bool?>(),
                                    args["min_feedback_count"]?.ToObject<int?>(),
                                    args["sort_by"]?.ToString(),
                                    args["limit"]?.ToObject<int?>() ?? 10,
                                    args["irt_difficulty_condition"]?.ToString(),
                                    args["quality_filter"]?.ToString()
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
                }

                await ChatWithAIAccessData.SaveMessageAsync(data.ConversationId, "user", data.Message);
                await ChatWithAIAccessData.SaveMessageAsync(data.ConversationId, "AI", finalAnswer);

                return Ok(new { success = true, message = finalAnswer ?? "Sorry, I couldn't process the request." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HandleAdminChat Error]: {ex}");

                if (ex.Message.Contains("503") || ex.Message.Contains("overloaded"))
                {
                    return StatusCode(503, new
                    {
                        success = false,
                        message = "⚠️ AI service is currently overloaded. Please try again in a few seconds.",
                        isRetryable = true
                    });
                }

                return StatusCode(500, new
                {
                    success = false,
                    message = $"An error occurred: {ex.Message}",
                    isRetryable = false
                });
            }
        }

        [HttpGet("ApiKeysStatus")]
        public async Task<IActionResult> GetApiKeysStatus()
        {
            try
            {
                var keysStatus = await _apiKeyManager.GetAllKeysStatusAsync();
                return Ok(new
                {
                    success = true,
                    keys = keysStatus,
                    totalRemaining = keysStatus.Sum(k => k.RemainingQuota),
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetApiKeysStatus Error]: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Failed to get keys status" });
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

        // ═════════════════════════════════════════════════════════════
        // ✅ HELPER METHODS
        // ═════════════════════════════════════════════════════════════

        private string ExtractTextFromPdf(string base64Data)
        {
            try
            {
                var pdfBytes = Convert.FromBase64String(base64Data);
                using (var memoryStream = new MemoryStream(pdfBytes))
                using (var pdfDocument = PdfDocument.Open(memoryStream))
                {
                    var textBuilder = new StringBuilder();
                    foreach (var page in pdfDocument.GetPages())
                    {
                        textBuilder.AppendLine(page.Text);
                        textBuilder.AppendLine();
                    }
                    return textBuilder.ToString().Trim();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExtractTextFromPdf ERROR]: {ex.Message}");
                return $"[Error: Could not extract text from PDF - {ex.Message}]";
            }
        }

        private string ExtractTextFromDocx(string base64Data)
        {
            try
            {
                var docxBytes = Convert.FromBase64String(base64Data);
                using (var memoryStream = new MemoryStream(docxBytes))
                using (var wordDocument = WordprocessingDocument.Open(memoryStream, false))
                {
                    var body = wordDocument.MainDocumentPart?.Document?.Body;
                    if (body == null)
                        return "[Empty document]";

                    var textBuilder = new StringBuilder();
                    foreach (var text in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                    {
                        textBuilder.Append(text.Text);
                    }

                    return textBuilder.ToString().Trim();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExtractTextFromDocx ERROR]: {ex.Message}");
                return $"[Error: Could not extract text from DOCX - {ex.Message}]";
            }
        }

        private async Task RebuildPromptWithLimitedHistoryAsync(
            List<object> contentsList,
            Guid conversationId,
            string basicProfile,
            string originalMessage,
            int historyLimit = 5)
        {
            Console.WriteLine($"[Prompt Optimization] Rebuilding with {historyLimit} messages history...");

            var limitedHistory = await ChatWithAIAccessData.GetMessageHistoryForApiAsync(
                conversationId,
                historyLimit
            );

            string reducedPrompt = _promptService.BuildPromptForMember(
                basicProfile,
                limitedHistory,
                originalMessage
            );

            contentsList[0] = new
            {
                role = "user",
                parts = new[] { new { text = reducedPrompt } }
            };

            Console.WriteLine($"[Prompt Optimization] Completed - {historyLimit} messages included");
        }

        // ✅ DTO CLASSES
        public class ChatRequest
        {
            public Guid ConversationId { get; set; }
            public string Message { get; set; }
            public List<FileAttachmentDto>? Files { get; set; }
            public string? UserKey { get; set; }
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
            [JsonProperty("items")]
            public GeminiSchemaProperty Items { get; set; }
        }
    }
}