using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        public async Task<IActionResult> OnGetAsync(string testKey, string resultKey)
        {
            // Validate tham số
            if (string.IsNullOrEmpty(testKey) || string.IsNullOrEmpty(resultKey) ||
                !Guid.TryParse(testKey, out Guid testKeyGuid) || !Guid.TryParse(resultKey, out Guid resultKeyGuid))
            {
                return RedirectToPage("/Ready");
            }

            TestKey = testKeyGuid;
            ResultKey = resultKeyGuid;

            // Lấy thời gian còn lại và danh sách câu hỏi
            var (endTime, questions) = await TestAccessData.GetTestData(TestKey, ResultKey);

            if (endTime == null || questions == null)
            {
                return RedirectToPage("/Ready");
            }

            // Tính thời gian còn lại
            TimeRemaining = endTime.Value - DateTime.Now;
            if (TimeRemaining <= TimeSpan.Zero)
            {
                // Hết giờ, chuyển hướng về trang kết quả (tùy bạn muốn xử lý)
                return RedirectToPage("/Result", new { resultKey = ResultKey.ToString() });
            }

            Questions = questions;

            // Kiểm tra số lượng câu hỏi
            if (Questions.Count < 200)
            {
                Console.WriteLine($"Warning: Only {Questions.Count} questions loaded, expected 200.");
            }

            return Page();
        }
    }
}