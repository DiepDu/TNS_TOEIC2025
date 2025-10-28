using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using TNS_TOEICPart2.Areas.TOEICPart2.Models;

namespace TNS_TOEICPart2.Areas.TOEICPart2.Pages
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
            if (!(IsFullAdmin && UserLogin.Role.IsApproval))
                return new JsonResult(new { status = "ERROR", message = "Bạn không có quyền phê duyệt câu hỏi!" });

            // --- Logic kiểm tra khi BẬT câu hỏi ---
            if (request.Publish) // Chỉ kiểm tra khi BẬT (Publish = true)
            {
                // Tải bản ghi câu hỏi để kiểm tra
                var zValidationRecord = new QuestionAccessData.Part2_Question_Info(request.QuestionKey);
                if (zValidationRecord.Status != "OK")
                    return new JsonResult(new { status = "ERROR", message = "Không tìm thấy câu hỏi để kiểm tra!" });

                List<string> errors = new List<string>();

                // 1. Kiểm tra số lượng đáp án (gọi hàm mới)
                int answerCount = QuestionListDataAccess.GetAnswerCount(request.QuestionKey);
                if (answerCount != 3)
                {
                    errors.Add($"Phải có đủ 4 đáp án (hiện có {answerCount}).");
                }

                // 2. Kiểm tra các trường không được trống
                // (Giả sử tên thuộc tính trên zValidationRecord khớp với tên cột trong DB)
                if (string.IsNullOrWhiteSpace(zValidationRecord.QuestionVoice))
                {
                    errors.Add("Voice không được trống.");
                }
             



                // User yêu cầu "Voice Explanation" -> Explanation
                if (string.IsNullOrWhiteSpace(zValidationRecord.Explanation))
                {
                    errors.Add("Voice Explanation (Explanation) không được trống.");
                }

                // User yêu cầu "Level" -> SkillLevel
                if (zValidationRecord.SkillLevel <= 0) // Giả sử 0 là chưa set
                {
                    errors.Add("Level (SkillLevel) phải được cài đặt.");
                }

                // User yêu cầu "Category"
                if (string.IsNullOrWhiteSpace(zValidationRecord.Category))
                {
                    errors.Add("Category không được trống.");
                }

                // User yêu cầu "Grammar Topic"
                if (string.IsNullOrWhiteSpace(zValidationRecord.GrammarTopic))
                {
                    errors.Add("Grammar Topic không được trống.");
                }

                // User yêu cầu "Vocabulary Topic"
                if (string.IsNullOrWhiteSpace(zValidationRecord.VocabularyTopic))
                {
                    errors.Add("Vocabulary Topic không được trống.");
                }

                // User yêu cầu "Error Type"
                if (string.IsNullOrWhiteSpace(zValidationRecord.ErrorType))
                {
                    errors.Add("Error Type không được trống.");
                }

                // 3. Trả về lỗi nếu có
                if (errors.Count > 0)
                {
                    return new JsonResult(new
                    {
                        status = "VALIDATION_ERROR",
                        message = "Không thể bật câu hỏi do thiếu thông tin:",
                        errors = errors
                    });
                }
            }

            // === PHẦN BỊ THIẾU BẮT ĐẦU TỪ ĐÂY ===

            // Nếu không BẬT, hoặc nếu đã BẬT và vượt qua kiểm tra -> chạy logic cập nhật
            var zRecord = new QuestionAccessData.Part2_Question_Info(request.QuestionKey);
            if (zRecord.Status != "OK")
                return new JsonResult(new { status = "ERROR", message = "Không tìm thấy câu hỏi để cập nhật!" });

            zRecord.Publish = request.Publish;
            if (request.Publish)
                zRecord.RecordStatus = 0; // Khi bật, đảm bảo RecordStatus là 0 (đang sử dụng)

            zRecord.ModifiedBy = UserLogin.Employee.Key;
            zRecord.ModifiedName = UserLogin.Employee.Name;

            zRecord.Update();

            // Đây là lệnh return cho trường hợp thành công (hoặc thất bại khi update)
            return new JsonResult(new { status = zRecord.Status, message = zRecord.Message });
        }

        public class ToggleRequest
        {
            public string QuestionKey { get; set; }
            public bool Publish { get; set; }
        }
    }
}
