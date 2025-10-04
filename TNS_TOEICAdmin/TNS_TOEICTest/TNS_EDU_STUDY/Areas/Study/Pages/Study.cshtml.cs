using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web; // Cần thiết cho JavaScriptStringEncode
using TNS_EDU_STUDY.Areas.Study.Models;
using TNS_EDU_TEST.Areas.Test.Models;

namespace TNS_EDU_STUDY.Areas.Study.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class StudyModel : PageModel
    {
        private readonly IMemoryCache _cache;

        // Sử dụng Constructor Injection giống hệt TestModel
        public StudyModel(IMemoryCache memoryCache)
        {
            _cache = memoryCache;
        }

        // Các thuộc tính được đồng bộ hóa với TestModel
        [BindProperty(SupportsGet = true)]
        public Guid TestKey { get; set; }

        [BindProperty(SupportsGet = true)]
        public Guid ResultKey { get; set; }

        [BindProperty(SupportsGet = true)]
        public int SelectedPart { get; set; }

        public TimeSpan TimeRemaining { get; set; } // Dùng TimeSpan cho nhất quán
        public List<TestQuestion> Questions { get; set; }

        public string QuestionsJson { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            if (TestKey == Guid.Empty || ResultKey == Guid.Empty || SelectedPart < 1 || SelectedPart > 7)
            {
                return RedirectToPage("/Index");
            }

            // *** BƯỚC THAY ĐỔI LOGIC CHÍNH NẰM Ở ĐÂY ***

            // 1. Gọi hàm lấy dữ liệu TRƯỚC TIÊN
            var sessionData = await StudyAccessData.GetStudySessionData(TestKey, ResultKey, SelectedPart);
            if (sessionData == null)
            {
                return RedirectToPage("/Index");
            }

            // 2. KIỂM TRA ĐIỀU KIỆN HOÀN THÀNH TỪ DỮ LIỆU VỪA LẤY
            // Lệnh gọi hàm không tồn tại đã được xóa bỏ
            if (sessionData.TestScore.HasValue) // Kiểm tra xem TestScore có giá trị không
            {
                // Nếu đã có điểm, tức là đã hoàn thành, chuyển về trang Index
                return RedirectToPage("/Index");
            }

            // 3. Nếu chưa hoàn thành, tiếp tục xử lý như bình thường
            long totalDurationSeconds = sessionData.Duration * 60;
            long timeSpentSeconds = sessionData.TimeSpent * 60;
           

            // Xử lý JSON an toàn (Đảm bảo bạn đã thêm lại logic này)
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            string rawJson = JsonSerializer.Serialize(sessionData.Questions, jsonOptions);
            QuestionsJson = HttpUtility.JavaScriptStringEncode(rawJson);

            return Page();
        }

        #region Handlers Kế Thừa Trực Tiếp Từ TestModel

        [HttpPost("Study/SaveAnswer")] // Route được cập nhật cho Study
        public async Task<IActionResult> OnPostSaveAnswer([FromBody] AnswerDto dto)
        {
            if (string.IsNullOrEmpty(dto.ResultKey) || string.IsNullOrEmpty(dto.QuestionKey) ||
                 !Guid.TryParse(dto.ResultKey, out Guid resultKey) || !Guid.TryParse(dto.QuestionKey, out Guid questionKey))
            {
                return new JsonResult(new { success = false, message = "Invalid ResultKey or QuestionKey" }) { StatusCode = 400 };
            }

            Guid? selectAnswerKey = Guid.TryParse(dto.SelectAnswerKey, out var parsedKey) ? parsedKey : (Guid?)null;

            await TestAccessData.SaveAnswer(resultKey, questionKey, selectAnswerKey, dto.TimeSpent, DateTime.Now, dto.Part);
            return new JsonResult(new { success = true });
        }

        [HttpPost("Study/SaveFlaggedQuestion")] // Route được cập nhật cho Study
        public async Task<IActionResult> OnPostSaveFlaggedQuestion([FromBody] FlaggedQuestionDto dto)
        {
            if (!Guid.TryParse(dto.ResultKey, out var resultKey) || !Guid.TryParse(dto.QuestionKey, out var questionKey))
            {
                return new JsonResult(new { success = false, message = "Invalid Keys" }) { StatusCode = 400 };
            }
            await TestAccessData.SaveFlaggedQuestion(resultKey, questionKey, dto.IsFlagged);
            return new JsonResult(new { success = true });
        }

        [HttpGet("Study/GetFlaggedQuestions")] // Route được cập nhật cho Study
        public async Task<IActionResult> OnGetFlaggedQuestions(string resultKey)
        {
            if (!Guid.TryParse(resultKey, out var resultKeyGuid))
            {
                return new JsonResult(new { success = false, message = "Invalid ResultKey" }) { StatusCode = 400 };
            }
            var flaggedQuestions = await TestAccessData.GetFlaggedQuestions(resultKeyGuid);
            return new JsonResult(flaggedQuestions);
        }

        #endregion

        #region Handlers Đặc Thù Của Chức Năng Study

        [HttpPost("Study/UpdateTimeSpent")] // Route được cập nhật cho Study
        public async Task<IActionResult> OnPostUpdateTimeSpentAsync([FromBody] UpdateTimeRequestDto dto)
        {
            if (!Guid.TryParse(dto.ResultKey, out var resultKeyGuid)) return BadRequest();
            try
            {
                // Cập nhật tổng thời gian đã làm (tính bằng phút)
                //await StudyAccessData.UpdateTimeSpent(resultKeyGuid, dto.TotalMinutesSpent);
                return new JsonResult(new { success = true });
            }
            catch { return new JsonResult(new { success = false }) { StatusCode = 500 }; }
        }

        [HttpPost("Study/SubmitStudy")] // Route được cập nhật cho Study
        public async Task<IActionResult> OnPostSubmitStudyAsync([FromBody] SubmitStudyDto dto)
        {
            var memberKey = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(memberKey) || !Guid.TryParse(dto.ResultKey, out var resultKeyGuid))
            {
                return new JsonResult(new { success = false, message = "User not authenticated or invalid data." }) { StatusCode = 401 };
            }
            try
            {
                await StudyAccessData.SubmitStudySession(resultKeyGuid, Guid.Parse(memberKey));

                // Xóa cache sau khi nộp bài, giống hệt TestModel
                var cacheKey = $"ChatBackgroundData_Member_{memberKey}";
                _cache.Remove(cacheKey);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = ex.Message }) { StatusCode = 500 }; }
        }

        #endregion

        #region DTOs (Data Transfer Objects) - Tổ chức giống TestModel

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

        public class UpdateTimeRequestDto
        {
            public string ResultKey { get; set; }
            public int TotalMinutesSpent { get; set; }
        }

        public class SubmitStudyDto
        {
            public string ResultKey { get; set; }
        }

        #endregion
    }
}