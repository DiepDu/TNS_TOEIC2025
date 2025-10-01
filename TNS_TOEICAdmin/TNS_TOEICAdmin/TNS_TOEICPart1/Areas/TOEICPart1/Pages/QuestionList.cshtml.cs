using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using TNS_TOEICPart1.Areas.TOEICPart1.Models;

namespace TNS_TOEICPart1.Areas.TOEICPart1.Pages
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
            if (IsFullAdmin || UserLogin.Role.IsRead)
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
            if (IsFullAdmin || UserLogin.Role.IsRead)
            {
                DateTime zFromDate, zToDate;
                if (request.FromDate.Trim().Length > 0 && request.ToDate.Trim().Length > 0)
                {
                    zFromDate = DateTime.Parse(request.FromDate);
                    zToDate = DateTime.Parse(request.ToDate);
                    return QuestionListDataAccess.GetList(request.Search, request.Level, zFromDate, zToDate, request.PageSize, request.PageNumber, request.StatusFilter);
                }
                else
                {
                    return QuestionListDataAccess.GetList(request.Search, request.Level, request.PageSize, request.PageNumber, request.StatusFilter);
                }
            }
            else
            {
                return new JsonResult(new { Status = "ERROR", Result = "Bạn không có quyền xem danh sách!" });
            }
        }

        public IActionResult OnPostUpdateStatistics()
        {
            CheckAuth();
            if (!IsFullAdmin)
                return new JsonResult(new { status = "ERROR", message = "Bạn không có quyền cập nhật thống kê!" });

            try
            {
                // Gọi hàm cập nhật độ khó
                QuestionListDataAccess.UpdateDifficulty();
                // Gọi hàm phân tích bất thường
                QuestionListDataAccess.UpdateAnomaly();

                return new JsonResult(new { status = "OK", message = "Cập nhật thống kê (Độ khó & Bất thường) thành công!" });
            }
            catch (Exception ex)
            {
                // Ghi lại lỗi chi tiết hơn nếu cần (ví dụ: vào một file log)
                return new JsonResult(new { status = "ERROR", message = "Đã xảy ra lỗi trong quá trình cập nhật: " + ex.Message });
            }
        }

        public IActionResult OnPostTogglePublish([FromBody] ToggleRequest request)
        {
            CheckAuth();
            if (!(IsFullAdmin && UserLogin.Role.IsApproval))
                return new JsonResult(new { status = "ERROR", message = "Bạn không có quyền phê duyệt câu hỏi!" });

            var zRecord = new QuestionAccessData.Part1_Question_Info(request.QuestionKey);
            if (zRecord.Status != "OK")
                return new JsonResult(new { status = "ERROR", message = "Không tìm thấy câu hỏi!" });

            zRecord.Publish = request.Publish;
            if (request.Publish)
                zRecord.RecordStatus = 0;

            zRecord.ModifiedBy = UserLogin.Employee.Key;
            zRecord.ModifiedName = UserLogin.Employee.Name;

            zRecord.Update();
            return new JsonResult(new { status = zRecord.Status, message = zRecord.Message });
        }

        public class ToggleRequest
        {
            public string QuestionKey { get; set; }
            public bool Publish { get; set; }
        }
    }
}