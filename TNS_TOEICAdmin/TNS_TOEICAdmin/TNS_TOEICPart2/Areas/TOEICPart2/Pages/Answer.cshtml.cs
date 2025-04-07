using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System;
using Microsoft.Data.SqlClient;
using TNS_TOEICPart2.Areas.TOEICPart2.Models;

namespace TNS_TOEICPart2.Areas.TOEICPart2.Pages
{
    [IgnoreAntiforgeryToken]
    public class AnswerModel : PageModel
    {
        #region [Security]
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

        public string AnswerKey { get; set; }
        public string QuestionKey { get; set; }

        private readonly IWebHostEnvironment _environment;

        public AnswerModel(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public IActionResult OnGet(string key = null, string questionKey = null)
        {
            CheckAuth();
            if (!UserLogin.Role.IsRead || !IsFullAdmin)
            {
                TempData["Error"] = "ACCESS DENIED!!!";
                return Page();
            }

            AnswerKey = key?.Trim();
            QuestionKey = questionKey?.Trim();

            if (string.IsNullOrEmpty(QuestionKey) || QuestionKey.Length != 36)
            {
                return RedirectToPage("/QuestionList");
            }

            return Page();
        }

        public IActionResult OnPostRead([FromBody] ItemRequest request)
        {
            CheckAuth();
            if (!UserLogin.Role.IsRead || IsFullAdmin)
                return new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });

            if (string.IsNullOrEmpty(request.QuestionKey) || request.QuestionKey.Length != 36)
                return new JsonResult(new { Status = "ERROR", Message = "Invalid QuestionKey" });

            AnswerAccessData.Part2_Answer_Info record;
            try
            {
                if (string.IsNullOrEmpty(request.AnswerKey) || request.AnswerKey.Length != 36)
                {
                    record = new AnswerAccessData.Part2_Answer_Info(request.QuestionKey);
                    record.Message = "No existing record, initialized as new.";
                }
                else
                {
                    record = new AnswerAccessData.Part2_Answer_Info(request.AnswerKey, request.QuestionKey);
                    if (record.Status == "ERROR" && record.Message == "No record found")
                    {
                        record = new AnswerAccessData.Part2_Answer_Info(request.QuestionKey);
                        record.AnswerKey = Guid.NewGuid().ToString();
                        record.IsNewRecord = true;
                        record.Message = "No record found, initialized as new.";
                    }
                }
                return new JsonResult(new { Status = "OK", Record = record, Message = record.Message });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { Status = "ERROR", Message = $"Error loading record: {ex.Message}" });
            }
        }

        public IActionResult OnPostGetInfo([FromBody] ItemRequest request)
        {
            CheckAuth();
            if (!UserLogin.Role.IsRead || !IsFullAdmin)
                return new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });

            if (string.IsNullOrEmpty(request.AnswerKey) || request.AnswerKey.Length != 36 ||
                string.IsNullOrEmpty(request.QuestionKey) || request.QuestionKey.Length != 36)
                return new JsonResult(new { Status = "ERROR", Message = "Invalid AnswerKey or QuestionKey" });

            try
            {
                var record = new AnswerAccessData.Part2_Answer_Info(request.AnswerKey, request.QuestionKey);
                if (record.Status == "ERROR")
                {
                    return new JsonResult(new
                    {
                        CreatedOn = (string)null,
                        CreatedBy = (string)null,
                        ModifiedOn = (string)null,
                        ModifiedBy = (string)null,
                        AnswerCorrect = false
                    });
                }

                return new JsonResult(new
                {
                    CreatedOn = record.CreatedOn?.ToString("yyyy-MM-dd HH:mm:ss"),
                    CreatedBy = record.CreatedName,
                    ModifiedOn = record.ModifiedOn?.ToString("yyyy-MM-dd HH:mm:ss"),
                    ModifiedBy = record.ModifiedName,
                    AnswerCorrect = record.AnswerCorrect
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { Status = "ERROR", Message = $"Error loading info: {ex.Message}" });
            }
        }
        public IActionResult OnPostCreate([FromBody] AnswerAccessData.Part2_Answer_Info request)
        {
            CheckAuth();
            if (!UserLogin.Role.IsCreate || !IsFullAdmin)
                return new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });

            if (request == null)
                return new JsonResult(new { Status = "ERROR", Message = "Request body is null" });

            if (string.IsNullOrEmpty(request.QuestionKey) || request.QuestionKey.Length != 36)
                return new JsonResult(new { Status = "ERROR", Message = "Invalid QuestionKey" });

            try
            {
                var record = request;
                record.CreatedBy = UserLogin.Employee.Key;
                record.CreatedName = UserLogin.Employee.Name;
                record.Create();

                if (record.Status == "ERROR")
                    return new JsonResult(new { Status = "ERROR", Message = record.Message });

                if (record.AnswerCorrect)
                {
                    UpdateOtherAnswersToFalse(record.QuestionKey, record.AnswerKey);
                }

                return new JsonResult(new { Status = "OK", Message = "Answer created", Record = record });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { Status = "ERROR", Message = $"Error creating record: {ex.Message}" });
            }
        }

        public IActionResult OnPostUpdate([FromBody] AnswerAccessData.Part2_Answer_Info request)
        {
            CheckAuth();
            if (!UserLogin.Role.IsUpdate || !IsFullAdmin)
                return new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });

            if (request == null)
                return new JsonResult(new { Status = "ERROR", Message = "Request body is null" });

            if (string.IsNullOrEmpty(request.AnswerKey) || request.AnswerKey.Length != 36)
                return new JsonResult(new { Status = "ERROR", Message = "Invalid AnswerKey" });

            try
            {
                var record = request;
                record.ModifiedBy = UserLogin.Employee.Key;
                record.ModifiedName = UserLogin.Employee.Name;
                record.Update();

                if (record.Status == "ERROR")
                    return new JsonResult(new { Status = "ERROR", Message = record.Message });

                if (record.AnswerCorrect)
                {
                    UpdateOtherAnswersToFalse(record.QuestionKey, record.AnswerKey);
                }

                return new JsonResult(new { Status = "OK", Message = "Answer updated", Record = record });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { Status = "ERROR", Message = $"Error updating record: {ex.Message}" });
            }
        }

        public IActionResult OnPostDelete([FromBody] ItemRequest request)
        {
            CheckAuth();
            if (!UserLogin.Role.IsDelete || !IsFullAdmin)
                return new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });

            if (string.IsNullOrEmpty(request.AnswerKey) || request.AnswerKey.Length != 36)
                return new JsonResult(new { Status = "ERROR", Message = "Invalid AnswerKey" });

            try
            {
                var record = new AnswerAccessData.Part2_Answer_Info(request.AnswerKey, null);
                record.Delete();

                if (record.Status == "ERROR")
                    return new JsonResult(new { Status = "ERROR", Message = record.Message });

                return new JsonResult(new { Status = "OK", Message = "Answer deleted" });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { Status = "ERROR", Message = $"Error deleting record: {ex.Message}" });
            }
        }

        [HttpPost]
        public IActionResult OnPostUpdateOtherAnswers([FromBody] UpdateOtherAnswersRequest request)
        {
            CheckAuth();
            if (!UserLogin.Role.IsUpdate || !IsFullAdmin)
                return new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });

            if (string.IsNullOrEmpty(request.QuestionKey) || request.QuestionKey.Length != 36)
                return new JsonResult(new { Status = "ERROR", Message = "Invalid QuestionKey" });

            try
            {
                UpdateOtherAnswersToFalse(request.QuestionKey, request.CurrentAnswerKey);
                return new JsonResult(new { Status = "OK", Message = "Other answers updated successfully" });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { Status = "ERROR", Message = $"Error updating other answers: {ex.Message}" });
            }
        }

        [HttpPost]
        public IActionResult OnPostLoadDropdowns()
        {
            CheckAuth();
            if (!UserLogin.Role.IsRead || !IsFullAdmin)
                return new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });

            try
            {
                string connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                var categories = LoadDropdown(conn, "TEC_Category", "CategoryKey", "CategoryName");
                var grammarTopics = LoadDropdown(conn, "GrammarTopics", "GrammarTopicID", "TopicName");
                var errorTypes = LoadDropdown(conn, "ErrorTypes", "ErrorTypeID", "ErrorDescription");

                return new JsonResult(new { categories, grammarTopics, errorTypes });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { Status = "ERROR", Message = $"Error loading dropdowns: {ex.Message}" });
            }
        }

        private void UpdateOtherAnswersToFalse(string questionKey, string currentAnswerKey)
        {
            string sql = @"UPDATE [dbo].[TEC_Part2_Answer] 
                           SET AnswerCorrect = 0, ModifiedOn = GETDATE(), ModifiedBy = @ModifiedBy, ModifiedName = @ModifiedName
                           WHERE QuestionKey = @QuestionKey AND AnswerKey != @CurrentAnswerKey AND RecordStatus != 99";

            string connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@QuestionKey", Guid.Parse(questionKey));
                    cmd.Parameters.AddWithValue("@CurrentAnswerKey", Guid.Parse(currentAnswerKey));
                    cmd.Parameters.AddWithValue("@ModifiedBy", UserLogin.Employee.Key);
                    cmd.Parameters.AddWithValue("@ModifiedName", UserLogin.Employee.Name);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private List<object> LoadDropdown(SqlConnection conn, string table, string keyField, string valueField)
        {
            var result = new List<object>();
            using var cmd = new SqlCommand($"SELECT {keyField}, {valueField} FROM [dbo].[{table}]", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new { Key = reader[keyField].ToString(), Value = reader[valueField].ToString() });
            }
            return result;
        }
        public IActionResult OnPostCountAnswers([FromBody] ItemRequest request)
        {
            CheckAuth();
            if (!UserLogin.Role.IsRead || !IsFullAdmin)
                return new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });

            string sql = "SELECT COUNT(*) FROM [dbo].[TEC_Part2_Answer] WHERE QuestionKey = @QuestionKey AND RecordStatus != 99";
            string connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@QuestionKey", Guid.Parse(request.QuestionKey));
                    int count = (int)cmd.ExecuteScalar();
                    return new JsonResult(new { count });
                }
            }
        }

        public IActionResult OnPostCheckRanking([FromBody] CheckRankingRequest request)
        {
            CheckAuth();
            if (!UserLogin.Role.IsRead)
                return new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });

            string sql = @"
        SELECT COUNT(*) 
        FROM [dbo].[TEC_Part2_Answer] 
        WHERE QuestionKey = @QuestionKey 
        AND Ranking = @Ranking 
        AND RecordStatus != 99
        AND (@AnswerKey IS NULL OR AnswerKey != @AnswerKey)";
            string connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@QuestionKey", Guid.Parse(request.QuestionKey));
                    cmd.Parameters.AddWithValue("@Ranking", request.Ranking);
                    cmd.Parameters.AddWithValue("@AnswerKey", string.IsNullOrEmpty(request.AnswerKey) ? DBNull.Value : Guid.Parse(request.AnswerKey));
                    int count = (int)cmd.ExecuteScalar();
                    return new JsonResult(new { exists = count > 0 });
                }
            }
        }

        public class CheckRankingRequest
        {
            public string QuestionKey { get; set; }
            public int Ranking { get; set; }
            public string AnswerKey { get; set; } // Để loại trừ chính bản ghi khi update
        }
        public class ItemRequest
        {
            public string AnswerKey { get; set; }
            public string QuestionKey { get; set; }
        }

        public class UpdateOtherAnswersRequest
        {
            public string QuestionKey { get; set; }
            public string CurrentAnswerKey { get; set; }
        }
    }
}
