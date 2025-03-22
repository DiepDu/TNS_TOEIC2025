using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TNS_EDU_TEST.Areas.Test.Models;

namespace TNS_EDU_TEST.Areas.Test.Pages
{
    public class ReadyModel : PageModel
    {
        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostStart()
        {
            // 1. Kiểm tra đăng nhập
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Account/Login");
            }

            // Lấy MemberKey và MemberName từ claims/cookies
            var memberKey = User.Claims.FirstOrDefault(c => c.Type == "MemberKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            var memberName = User.FindFirst("MemberName")?.Value;

            if (string.IsNullOrEmpty(memberKey) || string.IsNullOrEmpty(memberName))
            {
                return RedirectToPage("/Account/Login");
            }

            // 2. Kiểm tra bài thi gần nhất
            var (testKey, resultKey, isTestScoreNull) = await ReadyAccessData.CheckLatestTest(Guid.Parse(memberKey));
            if (testKey != null && resultKey != null && isTestScoreNull)
            {
                return RedirectToPage("/Test", new { area = "Test", testKey = testKey.ToString(), resultKey = resultKey.ToString() });
            }

            // 3. Tạo bài thi mới
            var newTestKey = Guid.NewGuid();
            var testName = "TOEIC Full Test";
            var description = $"{memberName} - {DateTime.Now}";
            var totalQuestion = 200;
            var duration = 120;

            await ReadyAccessData.InsertTest(newTestKey, testName, description, totalQuestion, duration,
                Guid.Parse(memberKey), memberName);

            // 4. Insert ResultOfUserForTest
            var newResultKey = Guid.NewGuid();
            var startTime = DateTime.Now;
            var endTime = startTime.AddMinutes(120);

            await ReadyAccessData.InsertResultOfUserForTest(newResultKey, newTestKey, Guid.Parse(memberKey),
                memberName, startTime, endTime);

            // 5. Lấy cấu hình TOEIC và SkillLevelDistribution
            var (config, distributions) = await ReadyAccessData.GetToeicConfiguration();

            // 6. Tạo nội dung đề thi và lưu vào ContentOfTest
            await ReadyAccessData.GenerateTestContent(newTestKey, newResultKey, config, distributions);

            // Chuyển hướng tới trang Test với TestKey và ResultKey
            return RedirectToPage("/Test", new { area = "Test", testKey = newTestKey.ToString(), resultKey = newResultKey.ToString() });
        }

        public class ToeicConfig
        {
            public Guid ConfigKey { get; set; }
            public int NumberOfPart1 { get; set; }
            public int NumberOfPart2 { get; set; }
            public int NumberOfPart3 { get; set; }
            public int NumberOfPart4 { get; set; }
            public int NumberOfPart5 { get; set; }
            public int NumberOfPart6 { get; set; }
            public int NumberOfPart7 { get; set; }
            public int Duration { get; set; }
        }

        public class SkillLevelDistribution
        {
            public Guid DistributionKey { get; set; }
            public int Part { get; set; }
            public int? SkillLevel1 { get; set; }
            public int? SkillLevel2 { get; set; }
            public int? SkillLevel3 { get; set; }
            public int? SkillLevel4 { get; set; }
            public int? SkillLevel5 { get; set; }
        }
    }
}