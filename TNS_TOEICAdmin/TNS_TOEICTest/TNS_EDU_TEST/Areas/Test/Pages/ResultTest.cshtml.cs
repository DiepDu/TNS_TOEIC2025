using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using TNS_EDU_TEST.Areas.Test.Models;
using TNS_EDU_TEST.Services;


namespace TNS_EDU_TEST.Areas.Test.Pages
{
    [IgnoreAntiforgeryToken]
    public class ResultTestModel : PageModel
    {
        private readonly IConfiguration _configuration; // Thêm IConfiguration
        private readonly GeminiApiKeyManager _apiKeyManager; // ✅ THÊM FIELD


        public ResultTestModel(
           IConfiguration configuration,
           GeminiApiKeyManager apiKeyManager) // ✅ INJECT
        {
            _configuration = configuration;
            _apiKeyManager = apiKeyManager;
        }



        public Guid TestKey { get; set; }
        public Guid ResultKey { get; set; }
        public int MaximumTime { get; set; }
        public string TestName { get; set; }
        public string Member { get; set; }
        public int TimeSpent { get; set; }
        public List<TestQuestion> Questions { get; set; }
        public string QuestionsJson { get; set; }
        public int ListeningScore { get; set; }
        public int ReadingScore { get; set; }
        public int TestScore { get; set; }
        public string ApiKey { get; set; }

        public async Task<IActionResult> OnGetAsync(string testKey, string resultKey)
        {
            if (string.IsNullOrEmpty(testKey) || string.IsNullOrEmpty(resultKey) ||
                !Guid.TryParse(testKey, out Guid testKeyGuid) || !Guid.TryParse(resultKey, out Guid resultKeyGuid))
            {
                return RedirectToPage("/Ready");
            }

            TestKey = testKeyGuid;
            ResultKey = resultKeyGuid;

            var (testInfo, questions) = await ResultTestAccessData.GetResultData(TestKey, ResultKey);
            if (testInfo == null || questions == null)
            {
                return RedirectToPage("/Ready");
            }

            MaximumTime = testInfo.MaximumTime;
            TestName = testInfo.TestName;
            Member = testInfo.Member;
            TimeSpent = testInfo.TimeSpent;
            ListeningScore = testInfo.ListeningScore;
            ReadingScore = testInfo.ReadingScore;
            TestScore = testInfo.TestScore;
            Questions = questions;

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            string rawJson = JsonSerializer.Serialize(Questions, jsonOptions);
            QuestionsJson = HttpUtility.JavaScriptStringEncode(rawJson, addDoubleQuotes: false);
            ApiKey = _configuration["ApiSettings:ApiKey"];

            // ✅ CHỈ KHAI BÁO 1 LẦN
            var memberKey = User.Claims.FirstOrDefault(c =>
                c.Type == "MemberKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(memberKey) && !string.IsNullOrEmpty(testKey))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var learningData = new LearningAccessData(_configuration, _apiKeyManager);
                        await learningData.TriggerAnalysisAsync(memberKey, testKey);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Background Analysis ERROR]: {ex.Message}");
                    }
                });
            }

            return Page();  // ✅ XÓA CODE DUPLICATE Ở DƯỚI
        }
        [HttpPost]
        public async Task<IActionResult> OnPostSubmitFeedbackAsync([FromBody] FeedbackRequest feedbackRequest)
        {
            try
            {
                if (feedbackRequest == null ||
                    string.IsNullOrWhiteSpace(feedbackRequest.QuestionKey) ||
                    string.IsNullOrWhiteSpace(feedbackRequest.FeedbackText))
                {
                    return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };
                }

                if (!Guid.TryParse(feedbackRequest.QuestionKey, out Guid questionKeyGuid))
                {
                    return new JsonResult(new { success = false, message = "QuestionKey không hợp lệ." }) { StatusCode = 400 };
                }

                Guid? parentKeyGuid = null;
                if (!string.IsNullOrEmpty(feedbackRequest.Parent))
                {
                    if (!Guid.TryParse(feedbackRequest.Parent, out Guid parsedParent))
                        return new JsonResult(new { success = false, message = "Parent key không hợp lệ." }) { StatusCode = 400 };
                    parentKeyGuid = parsedParent;
                }

                var memberKey = User.Claims.FirstOrDefault(c => c.Type == "MemberKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(memberKey) || !Guid.TryParse(memberKey, out Guid memberKeyGuid))
                {
                    return new JsonResult(new { success = false, message = "Người dùng chưa đăng nhập." }) { StatusCode = 401 };
                }

                var feedback = new QuestionFeedback
                {
                    FeedbackKey = Guid.NewGuid(),
                    QuestionKey = questionKeyGuid,
                    MemberKey = memberKeyGuid,
                    FeedbackText = feedbackRequest.FeedbackText,
                    CreatedOn = DateTime.Now,
                    Part = feedbackRequest.Part,
                    Parent = parentKeyGuid,
                    Status = 0 // Pending
                };

                await ResultTestAccessData.InsertFeedbackAsync(feedback);
                return new JsonResult(new { success = true, message = "Gửi phản hồi thành công." });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi xử lý phản hồi: " + ex.Message);
                return new JsonResult(new { success = false, message = "Đã xảy ra lỗi: " + ex.Message }) { StatusCode = 500 };
            }
        }

    }

    public class FeedbackRequest
    {
        public string ResultKey { get; set; }
        public string QuestionKey { get; set; }
        public string FeedbackText { get; set; }
        public int Part { get; set; }
        public string Parent { get; set; } // Thêm thuộc tính Parent
    }

    public class QuestionFeedback
    {
        public Guid FeedbackKey { get; set; }
        public Guid QuestionKey { get; set; }
        public Guid MemberKey { get; set; }
        public string FeedbackText { get; set; }
        public DateTime CreatedOn { get; set; }
        public int Part { get; set; }
        public Guid? Parent { get; set; } // Cột Parent có thể null
        public int Status { get; set; }
    }
}