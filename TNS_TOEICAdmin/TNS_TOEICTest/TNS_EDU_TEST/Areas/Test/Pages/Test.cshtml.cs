using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using TNS_EDU_TEST.Areas.Test.Models;

namespace TNS_EDU_TEST.Areas.Test.Pages
{
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
            if (TimeRemaining <= TimeSpan.Zero)
            {
                return RedirectToPage("/Result", new { resultKey = ResultKey.ToString() });
            }

            Questions = questions;
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase // Chuyển tên thuộc tính thành camelCase
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
    }
}