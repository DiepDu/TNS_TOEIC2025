using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using System.Security.Claims;
using System.Text;
using TNS.Member;
using TNS_TOEICTest.Models;
using TNS_TOEICTest.Services;

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
                var newConversationId = await ChatWithAIAccessData.CreateConversationAsync(currentMemberKey);

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
                // === BƯỚC 1: XÁC THỰC VÀ LẤY THÔNG TIN CƠ BẢN ===
                var memberCookie = _httpContextAccessor.HttpContext.User as ClaimsPrincipal;
                var memberId = memberCookie?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(memberId))
                    return Unauthorized(new { success = false, message = "User not authenticated." });

                var apiKey = _configuration["GeminiApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("[ERROR] GeminiApiKey is not configured.");
                    return StatusCode(500, new { success = false, message = "AI service is not configured." });
                }

                // === BƯỚC 2: TỐI ƯU HÓA - LẤY DỮ LIỆU NỀN TỪ CACHE ===
                var cacheKey = $"MemberData_{memberId}";
                if (!_cache.TryGetValue(cacheKey, out string backgroundData))
                {
                    backgroundData = await ChatWithAIAccessData.LoadMemberOriginalDataAsync(memberId);
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(30));
                    _cache.Set(cacheKey, backgroundData, cacheEntryOptions);
                }

                // === BƯỚC 3: LƯU TIN NHẮN MỚI VÀ XÂY DỰNG PROMPT ===
                await ChatWithAIAccessData.SaveMessageAsync(data.ConversationId, "user", data.Message);
                var chatHistoryForPrompt = await ChatWithAIAccessData.GetMessageHistoryForApiAsync(data.ConversationId);

                string finalPrompt = _promptService.BuildPromptForMember(
                    backgroundData,
                    chatHistoryForPrompt,
                    data.Message,
                    data.ScreenData
                );

                // === BƯỚC 4: GỌI API VỚI ĐÚNG MODEL BẠN YÊU CẦU ===
                string botMessage;
                // Cập nhật model thành "gemini-2.5-flash" theo yêu cầu của bạn
                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

                var payload = new
                {
                    contents = new[]
                    {
                new { role = "user", parts = new[] { new { text = finalPrompt } } }
            }
                };

                using (var client = new HttpClient())
                {
                    var jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                    var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var httpResponse = await client.PostAsync(apiUrl, httpContent);

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var jsonResponse = await httpResponse.Content.ReadAsStringAsync();
                        var parsedResponse = JObject.Parse(jsonResponse);
                        botMessage = parsedResponse["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString()
                                     ?? "Sorry, I received an empty response.";
                    }
                    else
                    {
                        var errorContent = await httpResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"[API Call Error - {httpResponse.StatusCode}]: {errorContent}");
                        botMessage = "I'm having trouble connecting to my brain right now. Please try again in a moment.";
                    }
                }

                // === BƯỚC 5: LƯU VÀ TRẢ VỀ ===
                await ChatWithAIAccessData.SaveMessageAsync(data.ConversationId, "AI", botMessage);
                return Ok(new { success = true, message = botMessage });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HandleMemberChat Error]: {ex.ToString()}");
                return StatusCode(500, new { success = false, message = $"An internal server error occurred: {ex.Message}" });
            }
        }
        public class ChatRequest
        {
            public Guid ConversationId { get; set; }
            public string Message { get; set; } = "";
            public string? ScreenData { get; set; }
        }
    }
}