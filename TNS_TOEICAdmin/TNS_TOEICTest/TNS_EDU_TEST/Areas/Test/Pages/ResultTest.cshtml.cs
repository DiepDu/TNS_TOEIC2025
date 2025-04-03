using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using TNS_EDU_TEST.Areas.Test.Models;

namespace TNS_EDU_TEST.Areas.Test.Pages
{
    [IgnoreAntiforgeryToken]
    [Authorize]
    public class ResultTestModel : PageModel
    {
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

            return Page();
        }
    }
}