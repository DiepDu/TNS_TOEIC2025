using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using TNS_TOEICPart6.Areas.TOEICPart6.Models;

namespace TNS_TOEICPart6.Areas.TOEICPart6.Pages
{
    [IgnoreAntiforgeryToken]
    public class QuestionListModel : PageModel
    {
        #region [ Security ]
        public TNS_Auth.UserLogin_Info UserLogin;
        public bool IsFullAdmin { get; private set; }
        private void CheckAuth()
        {
            UserLogin = new TNS_Auth.UserLogin_Info(User);

            // Kiểm tra quyền Full trước
            var fullRole = new TNS_Auth.Role_Info(UserLogin.UserKey, "Full");
            if (fullRole.GetCode() == "200") // Có quyền Full trong DB
            {
                IsFullAdmin = true;
                UserLogin.GetRole("Questions"); // Vẫn lấy nhưng không ảnh hưởng
            }
            else
            {
                IsFullAdmin = false;
                UserLogin.GetRole("Questions"); // Lấy quyền Questions
            }

            // Đảm bảo Role được khởi tạo
            if (UserLogin.Role == null)
            {
                UserLogin.GetRole("Questions");
            }
        }
        #endregion

        public IActionResult OnGet()
        {
            CheckAuth();
            if (UserLogin.Role.IsRead || IsFullAdmin)
            {
                return Page();
            }
            else
            {
                TempData["Error"] = "ACCESS DENIED!!!";
                return Page();
            }
        }

        public IActionResult OnPostLoadData([FromBody] ItemRequest request)
        {
            CheckAuth();
            JsonResult zResult = new JsonResult("");
            if (UserLogin.Role.IsRead || IsFullAdmin)
            {
                DateTime zFromDate, zToDate;
                if (request.FromDate.Trim().Length > 0 && request.ToDate.Trim().Length > 0)
                {
                    zFromDate = DateTime.Parse(request.FromDate);
                    zToDate = DateTime.Parse(request.ToDate);
                    zResult = QuestionListDataAccess.GetList(request.Search, request.Level, zFromDate, zToDate, request.PageSize, request.PageNumber, request.StatusFilter);
                }
                else
                {
                    zResult = QuestionListDataAccess.GetList(request.Search, request.Level, request.PageSize, request.PageNumber, request.StatusFilter);
                }
            }
            else
            {
                zResult = new JsonResult(new { Status = "ERROR", Result = "ACCESS DENIED" });
            }
            return zResult;
        }

        public IActionResult OnPostTogglePublish([FromBody] ToggleRequest request)
        {
            CheckAuth();
            if (!(UserLogin.Role.IsUpdate || IsFullAdmin))
                return new JsonResult(new { status = "ERROR", message = "You do not have permission to approve questions!" });

            // --- Validation logic when TURNING ON question ---
            if (request.Publish) // Only validate when turning ON
            {
                // Load question record for validation
                var zValidationRecord = new QuestionAccessData.Part6_Question_Info(request.QuestionKey);
                if (zValidationRecord.Status != "OK")
                    return new JsonResult(new { status = "ERROR", message = "Passage not found for validation!" });

                List<string> errors = new List<string>();

                // === VALIDATE PARENT QUESTION (PASSAGE) ===
                // ✅ PART6: Kiểm tra QuestionText (thay vì Voice)
                if (string.IsNullOrWhiteSpace(zValidationRecord.QuestionText))
                {
                    errors.Add("Passage text cannot be empty.");
                }

                // ✅ PART6: KHÔNG kiểm tra Explanation (có thể NULL)
                // Explanation của passage có thể NULL vì dùng explanation của câu hỏi con

                if (zValidationRecord.SkillLevel <= 0)
                {
                    errors.Add("Level must be set.");
                }

                if (string.IsNullOrWhiteSpace(zValidationRecord.Category))
                {
                    errors.Add("Category cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(zValidationRecord.GrammarTopic))
                {
                    errors.Add("Grammar Topic cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(zValidationRecord.VocabularyTopic))
                {
                    errors.Add("Vocabulary Topic cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(zValidationRecord.ErrorType))
                {
                    errors.Add("Error Type cannot be empty.");
                }

                // === VALIDATE CHILD QUESTIONS (SUB QUESTIONS) ===
                // ✅ PART6: ValidateChildQuestions sẽ kiểm tra tối thiểu 4 câu hỏi con
                var childErrors = QuestionListDataAccess.ValidateChildQuestions(request.QuestionKey);
                if (childErrors.Count > 0)
                {
                    errors.AddRange(childErrors);
                }

                if (errors.Count > 0)
                {
                    return new JsonResult(new
                    {
                        status = "VALIDATION_ERROR",
                        message = "Cannot turn on passage due to missing information:",
                        errors = errors
                    });
                }
            }

            // === UPDATE STATUS ===
            var zRecord = new QuestionAccessData.Part6_Question_Info(request.QuestionKey);
            if (zRecord.Status != "OK")
                return new JsonResult(new { status = "ERROR", message = "Passage not found for update!" });

            zRecord.Publish = request.Publish;
            if (request.Publish)
                zRecord.RecordStatus = 0;

            zRecord.ModifiedBy = UserLogin.Employee.Key;
            zRecord.ModifiedName = UserLogin.Employee.Name;

            zRecord.Update();

            if (zRecord.Status != "OK")
                return new JsonResult(new { status = zRecord.Status, message = zRecord.Message });

            // Update child questions
            try
            {
                string connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = @"
                UPDATE [dbo].[TEC_Part6_Question]
                SET Publish = @Publish, 
                    RecordStatus = @RecordStatus,
                    ModifiedBy = @ModifiedBy, 
                    ModifiedName = @ModifiedName, 
                    ModifiedOn = GETDATE()
                WHERE Parent = @QuestionKey";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@QuestionKey", request.QuestionKey);
                        cmd.Parameters.AddWithValue("@Publish", request.Publish);
                        cmd.Parameters.AddWithValue("@RecordStatus", request.Publish ? 0 : zRecord.RecordStatus);
                        cmd.Parameters.AddWithValue("@ModifiedBy", UserLogin.Employee.Key);
                        cmd.Parameters.AddWithValue("@ModifiedName", UserLogin.Employee.Name);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { status = "ERROR", message = $"Failed to update sub-questions: {ex.Message}" });
            }

            return new JsonResult(new { status = "OK", message = "Updated successfully!" });
        }

        public class ToggleRequest
        {
            public string QuestionKey { get; set; }
            public bool Publish { get; set; }
        }
    }
}
