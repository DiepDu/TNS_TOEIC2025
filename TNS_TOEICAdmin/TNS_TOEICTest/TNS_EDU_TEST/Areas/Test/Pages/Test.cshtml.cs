using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using TNS_EDU_TEST.Areas.Test.Models;

namespace TNS_EDU_TEST.Areas.Test.Pages
{
    [IgnoreAntiforgeryToken]
    [Authorize]
    public class TestModel : PageModel
    {
        public Guid TestKey { get; set; }
        public Guid ResultKey { get; set; }
        public TimeSpan TimeRemaining { get; set; }
        public List<TestQuestion> Questions { get; set; }
        public string QuestionsJson { get; set; }

        public async Task<IActionResult> OnGetAsync(string testKey, string resultKey)
        {
            if (string.IsNullOrEmpty(testKey) || string.IsNullOrEmpty(resultKey) ||
                !Guid.TryParse(testKey, out Guid testKeyGuid) || !Guid.TryParse(resultKey, out Guid resultKeyGuid))
            {
                return RedirectToPage("/Ready");
            }

            TestKey = testKeyGuid;
            ResultKey = resultKeyGuid;

            var (endTime, questions) = await TestAccessData.GetTestData(TestKey, ResultKey);
            if (endTime == null || questions == null)
            {
                return RedirectToPage("/Ready");
            }

            TimeRemaining = endTime.Value - DateTime.Now;
            //if (TimeRemaining <= TimeSpan.Zero)
            //{
            //    return RedirectToPage("/Result", new { resultKey = ResultKey.ToString() });
            //}

            Questions = questions;
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            string rawJson = JsonSerializer.Serialize(Questions, jsonOptions);
            QuestionsJson = HttpUtility.JavaScriptStringEncode(rawJson, addDoubleQuotes: false);

            Console.WriteLine($"Questions Count: {Questions.Count}");
            foreach (var q in Questions)
            {
                Console.WriteLine($"QuestionKey={q.QuestionKey}, Part={q.Part}, Parent={q.Parent}, Children={q.Children.Count}");
            }
            Console.WriteLine("Serialized JSON: " + rawJson);

            if (Questions.Count < 200)
            {
                Console.WriteLine($"Warning: Only {Questions.Count} questions loaded, expected 200.");
            }

            return Page();
        }

        [HttpPost("SaveFlaggedQuestion")]
        public async Task<IActionResult> OnPostSaveFlaggedQuestion([FromBody] FlaggedQuestionDto dto)
        {
            if (string.IsNullOrEmpty(dto.ResultKey) || string.IsNullOrEmpty(dto.QuestionKey) ||
                !Guid.TryParse(dto.ResultKey, out Guid resultKey) || !Guid.TryParse(dto.QuestionKey, out Guid questionKey))
            {
                return new JsonResult(new { success = false, message = "Invalid ResultKey or QuestionKey" }) { StatusCode = 400 };
            }

            await TestAccessData.SaveFlaggedQuestion(resultKey, questionKey, dto.IsFlagged);
            return new JsonResult(new { success = true }) { StatusCode = 200 };
        }

        [HttpGet("GetFlaggedQuestions")]
        public async Task<IActionResult> OnGetFlaggedQuestions(string resultKey)
        {
            if (string.IsNullOrEmpty(resultKey) || !Guid.TryParse(resultKey, out Guid resultKeyGuid))
            {
                return new JsonResult(new { success = false, message = "Invalid ResultKey" }) { StatusCode = 400 };
            }

            var flaggedQuestions = await TestAccessData.GetFlaggedQuestions(resultKeyGuid);
            return new JsonResult(flaggedQuestions) { StatusCode = 200 };
        }

        [HttpPost("SaveAnswer")]
        public async Task<IActionResult> OnPostSaveAnswer([FromBody] AnswerDto dto)
        {
            if (string.IsNullOrEmpty(dto.ResultKey) || string.IsNullOrEmpty(dto.QuestionKey) ||
                !Guid.TryParse(dto.ResultKey, out Guid resultKey) || !Guid.TryParse(dto.QuestionKey, out Guid questionKey))
            {
                return new JsonResult(new { success = false, message = "Invalid ResultKey or QuestionKey" }) { StatusCode = 400 };
            }

            Guid? selectAnswerKey = null;
            if (!string.IsNullOrEmpty(dto.SelectAnswerKey) && Guid.TryParse(dto.SelectAnswerKey, out Guid parsedAnswerKey))
            {
                selectAnswerKey = parsedAnswerKey;
            }

            if (dto.Part < 1 || dto.Part > 7)
            {
                return new JsonResult(new { success = false, message = "Invalid Part value" }) { StatusCode = 400 };
            }

            await TestAccessData.SaveAnswer(
                resultKey,
                questionKey,
                selectAnswerKey, // Có thể là null khi bỏ chọn
                dto.TimeSpent,
                DateTime.Now,
                dto.Part
            );

            return new JsonResult(new { success = true }) { StatusCode = 200 };

        }
        [HttpPost("SubmitTest")]
        public async Task<IActionResult> OnPostSubmitTest([FromBody] SubmitTestDto dto)
        {
            if (string.IsNullOrEmpty(dto.TestKey) || string.IsNullOrEmpty(dto.ResultKey) ||
                !Guid.TryParse(dto.TestKey, out Guid testKey) || !Guid.TryParse(dto.ResultKey, out Guid resultKey))
            {
                return new JsonResult(new { success = false, message = "Invalid TestKey or ResultKey" }) { StatusCode = 400 };
            }

            if (dto.RemainingMinutes < 0)
            {
                dto.RemainingMinutes = 0;
            }

            // Lấy userKey từ Claims
            string userKey = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userKey))
            {
                return new JsonResult(new { success = false, message = "User is not authenticated or UserKey is missing" }) { StatusCode = 401 };
            }

            try
            {
                await TestAccessData.SubmitTest(testKey, resultKey, dto.RemainingMinutes, userKey);
                return new JsonResult(new { success = true }) { StatusCode = 200 };
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        public class SubmitTestDto
        {
            public string TestKey { get; set; }
            public string ResultKey { get; set; }
            public int RemainingMinutes { get; set; }
        }

        public class FlaggedQuestionDto
        {
            public string ResultKey { get; set; }
            public string QuestionKey { get; set; }
            public bool IsFlagged { get; set; }
        }

        public class AnswerDto
        {
            public string ResultKey { get; set; }
            public string QuestionKey { get; set; }
            public string SelectAnswerKey { get; set; }
            public int TimeSpent { get; set; }
            public int Part { get; set; }
        }
    }
}