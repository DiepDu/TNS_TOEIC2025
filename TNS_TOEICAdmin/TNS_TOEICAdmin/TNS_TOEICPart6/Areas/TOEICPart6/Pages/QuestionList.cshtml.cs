using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using TNS_TOEICPart6.Areas.TOEICPart6.Models;

namespace TNS_TOEICPart6.Areas.TOEICPart6.Pages
{
    [IgnoreAntiforgeryToken]
    public class QuestionListModel : PageModel
    {
        #region [ Security ]
        public TNS.Auth.UserLogin_Info UserLogin;

        private void CheckAuth()
        {
            UserLogin = new TNS.Auth.UserLogin_Info(User);
            UserLogin.GetRole("TOEIC_Part6");
            // For Testing
            UserLogin.Role.IsRead = true;
            UserLogin.Role.IsCreate = true;
            UserLogin.Role.IsUpdate = true;
            UserLogin.Role.IsDelete = true;
        }
        #endregion

        public IActionResult OnGet()
        {
            CheckAuth();
            if (UserLogin.Role.IsRead)
            {
                return Page();
            }
            else
            {
                return LocalRedirect("~/Warning?id=403");
            }
        }

        public IActionResult OnPostLoadData([FromBody] ItemRequest request)
        {
            CheckAuth();
            JsonResult zResult = new JsonResult("");
            if (UserLogin.Role.IsRead)
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
            if (!UserLogin.Role.IsUpdate)
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED" });

            var zRecord = new QuestionAccessData.Part6_Question_Info(request.QuestionKey);
            if (zRecord.Status != "OK")
                return new JsonResult(new { status = "ERROR", message = "Question not found" });

            zRecord.Publish = request.Publish;
            if (request.Publish) // Khi bật
                zRecord.RecordStatus = 0; // Đặt RecordStatus = 0
            // Khi tắt, giữ nguyên RecordStatus (không thay đổi trừ khi bạn muốn RecordStatus = 99)

            zRecord.ModifiedBy = UserLogin.Employee.Key;
            zRecord.ModifiedName = UserLogin.Employee.Name;

            zRecord.Update();
            if (zRecord.Status != "OK")
                return new JsonResult(new { status = zRecord.Status, message = zRecord.Message });

            // Cập nhật tất cả câu hỏi con
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
                        cmd.Parameters.AddWithValue("@RecordStatus", request.Publish ? 0 : zRecord.RecordStatus); // Khi bật: 0, khi tắt: giữ nguyên
                        cmd.Parameters.AddWithValue("@ModifiedBy", UserLogin.Employee.Key);
                        cmd.Parameters.AddWithValue("@ModifiedName", UserLogin.Employee.Name);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        // Có thể ghi log số lượng câu hỏi con được cập nhật nếu muốn
                    }
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { status = "ERROR", message = $"Failed to update sub-questions: {ex.Message}" });
            }

            return new JsonResult(new { status = zRecord.Status, message = zRecord.Message });
        }

        public class ToggleRequest
        {
            public string QuestionKey { get; set; }
            public bool Publish { get; set; }
        }
    }
}
