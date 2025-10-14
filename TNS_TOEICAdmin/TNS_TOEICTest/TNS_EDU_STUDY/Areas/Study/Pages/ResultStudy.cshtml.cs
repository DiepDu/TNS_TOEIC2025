using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using TNS_EDU_STUDY.Areas.Study.Models;
using TNS_EDU_TEST.Areas.Test.Models;
using TNS_EDU_TEST.Areas.Test.Pages;

namespace TNS_EDU_STUDY.Areas.Study.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class ResultStudyModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public ResultStudyModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public Guid TestKey { get; set; }

        [BindProperty(SupportsGet = true)]
        public Guid ResultKey { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PartSelect { get; set; }

        public StudyResultInfo StudyInfo { get; set; }
        public string QuestionsJson { get; set; }
        public string ApiKey { get; set; }
        public string Member { get; set; }
        public string StudyInfoJson { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (TestKey == Guid.Empty || ResultKey == Guid.Empty || PartSelect < 1 || PartSelect > 7)
            {
                return RedirectToPage("/Index");
            }

            ApiKey = _configuration["ApiSettings:ApiKey"];
            var (info, questions) = await ResultStudyAccessData.GetStudyResultData(TestKey, ResultKey, PartSelect);

            if (info == null || questions == null)
            {
                return NotFound("Could not find the study result.");
            }

            // --- LOGIC MỚI: XỬ LÝ TÊN BÀI THI ---
            if (!string.IsNullOrEmpty(info.TestName))
            {
                // Bỏ "TOEIC STUDY " và phần GUID
                info.TestName = info.TestName.Replace("TOEIC STUDY ", "").Split(" - ")[0].Trim();
            }

            StudyInfo = info;
            Member = info.MemberName;

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            QuestionsJson = JsonSerializer.Serialize(questions, jsonOptions);
            StudyInfoJson = JsonSerializer.Serialize(info, jsonOptions);

            return Page();
        }

        // Handler cho việc gửi Feedback
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
}