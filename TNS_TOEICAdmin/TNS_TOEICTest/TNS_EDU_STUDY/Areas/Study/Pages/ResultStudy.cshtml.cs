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

            ApiKey = _configuration.GetValue<string>("ApiKey");
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
                var memberKeyClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(memberKeyClaim, out var memberKeyGuid))
                {
                    return new JsonResult(new { success = false, message = "User not found." }) { StatusCode = 401 };
                }

                Guid? parentKeyGuid = null;
                if (!string.IsNullOrEmpty(feedbackRequest.Parent) && Guid.TryParse(feedbackRequest.Parent, out Guid parsedGuid))
                {
                    parentKeyGuid = parsedGuid;
                }

                var feedback = new QuestionFeedback
                {
                    FeedbackKey = Guid.NewGuid(),
                    QuestionKey = Guid.Parse(feedbackRequest.QuestionKey),
                    MemberKey = memberKeyGuid,
                    FeedbackText = feedbackRequest.FeedbackText,
                    CreatedOn = DateTime.Now,
                    Part = feedbackRequest.Part,
                    Parent = parentKeyGuid,
                    Status = 0 // Pending
                };

                await ResultTestAccessData.InsertFeedbackAsync(feedback);
                return new JsonResult(new { success = true, message = "Feedback submitted successfully." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = "An error occurred: " + ex.Message }) { StatusCode = 500 };
            }
        }
    }
}