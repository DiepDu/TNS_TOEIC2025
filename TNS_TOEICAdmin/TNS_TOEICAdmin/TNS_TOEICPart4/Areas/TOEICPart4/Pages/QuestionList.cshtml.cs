using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using TNS_TOEICPart4.Areas.TOEICPart4.Models;

namespace TNS_TOEICPart4.Areas.TOEICPart4.Pages
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
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED" });

            var zRecord = new QuestionAccessData.Part4_Question_Info(request.QuestionKey);
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
                UPDATE [dbo].[TEC_Part4_Question]
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
