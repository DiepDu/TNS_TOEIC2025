using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using TNS_Auth;
using TNS_TOEICAdmin.Models;

namespace TNS_TOEICAdmin.Pages.Manage
{
    [IgnoreAntiforgeryToken]
    public class MembersModel : PageModel
    {
        #region [Security]
        public TNS_Auth.UserLogin_Info UserLogin;
        public bool IsFullAdmin { get; private set; }
        private void CheckAuth()
        {
            UserLogin = new TNS_Auth.UserLogin_Info(User);
            var fullRole = new TNS_Auth.Role_Info(UserLogin.UserKey, "Full");
            if (fullRole.GetCode() == "200")
            {
                IsFullAdmin = true;
                UserLogin.GetRole("Members");
            }
            else
            {
                IsFullAdmin = false;
                UserLogin.GetRole("Members");
            }
            if (UserLogin.Role == null)
            {
                UserLogin.GetRole("Members");
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

        public async Task<IActionResult> OnGetGetMembers([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string search = null, [FromQuery] string activate = null, [FromQuery] string testStatus = null)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsRead)
            {
                try
                {
                    var members = await MembersAccessData.GetMembersAsync(page, pageSize, search, activate, testStatus);
                    var totalItems = await MembersAccessData.GetTotalMembersAsync(search, activate, testStatus); // Thêm hàm mới
                    return new JsonResult(new { members, totalItems });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in OnGetGetMembers: {ex.Message}");
                    return new JsonResult(new { status = "ERROR", message = "Lỗi hệ thống khi tải danh sách học viên." }) { StatusCode = 500 };
                }
            }
            else
            {
                return new JsonResult(new { status = "ERROR", message = "Bạn không có quyền xem danh sách!" }) { StatusCode = 403 };
            }
        }

        public async Task<IActionResult> OnGetGetDepartments()
        {
            var departments = await MembersAccessData.GetDepartmentsAsync();
            return new JsonResult(departments);
        }

        public async Task<IActionResult> OnGetGetTestDetails(Guid memberKey)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsRead)
            {
                var tests = await MembersAccessData.GetTestDetailsAsync(memberKey);
                return new JsonResult(tests);
            }
            else
            {
                return new JsonResult(new { status = "ERROR", message = "Bạn không có quyền xem chi tiết!" }) { StatusCode = 403 };
            }
        }
        public async Task<IActionResult> OnGetGetTestScoreHistory(Guid memberKey)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsRead)
            {
                try
                {
                    var history = await MembersAccessData.GetTestScoreHistoryAsync(memberKey);
                    return new JsonResult(history);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in OnGetGetTestScoreHistory: {ex.Message}");
                    return new JsonResult(new { status = "ERROR", message = "Lỗi khi tải lịch sử điểm thi." }) { StatusCode = 500 };
                }
            }
            else
            {
                return new JsonResult(new { status = "ERROR", message = "Bạn không có quyền xem lịch sử điểm thi!" }) { StatusCode = 403 };
            }
        }
        public async Task<IActionResult> OnPostCreate([FromBody] Member member)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsCreate)
            {
                if (string.IsNullOrEmpty(member.MemberID) || string.IsNullOrEmpty(member.MemberName) || (string.IsNullOrEmpty(member.Password) && member.MemberKey == Guid.Empty))
                    return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };

                if (member.ToeicScoreStudy.HasValue && (member.ToeicScoreStudy < 0 || member.ToeicScoreStudy > 990))
                    return new JsonResult(new { success = false, message = "Điểm học phải từ 0 đến 990." }) { StatusCode = 400 };
                if (member.ToeicScoreExam.HasValue && (member.ToeicScoreExam < 0 || member.ToeicScoreExam > 990))
                    return new JsonResult(new { success = false, message = "Điểm thi phải từ 0 đến 990." }) { StatusCode = 400 };

                if (member.MemberKey == Guid.Empty) member.MemberKey = Guid.NewGuid();
                member.CreatedBy = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value);
                if (!string.IsNullOrEmpty(member.Password))
                    member.Password = MyCryptography.HashPass(member.Password);

                await MembersAccessData.AddMemberAsync(member);
                return new JsonResult(new { success = true, message = "Thêm học viên thành công!" });
            }
            else
            {
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED!!!" }) { StatusCode = 403 };
            }
        }

        public async Task<IActionResult> OnPostUpdate([FromBody] Member member)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsUpdate)
            {
                if (member.MemberKey == Guid.Empty || string.IsNullOrEmpty(member.MemberID) || string.IsNullOrEmpty(member.MemberName))
                    return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ: MemberKey, MemberID hoặc MemberName không được để trống." }) { StatusCode = 400 };

                if (member.ToeicScoreStudy.HasValue && (member.ToeicScoreStudy < 0 || member.ToeicScoreStudy > 990))
                    return new JsonResult(new { success = false, message = "Điểm học phải từ 0 đến 990." }) { StatusCode = 400 };
                if (member.ToeicScoreExam.HasValue && (member.ToeicScoreExam < 0 || member.ToeicScoreExam > 990))
                    return new JsonResult(new { success = false, message = "Điểm thi phải từ 0 đến 990." }) { StatusCode = 400 };

                try
                {
                    member.ModifiedBy = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value);
                    await MembersAccessData.UpdateMemberAsync(member);
                    return new JsonResult(new { success = true, message = "Cập nhật học viên thành công!" });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in OnPostUpdate: Message={ex.Message}, StackTrace={ex.StackTrace}");
                    return new JsonResult(new { success = false, message = $"Cập nhật thất bại: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            else
            {
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED!!!" }) { StatusCode = 403 };
            }
        }

        public async Task<IActionResult> OnGetDelete(Guid memberKey)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsDelete)
            {
                await MembersAccessData.DeleteMemberAsync(memberKey);
                return new JsonResult(new { success = true, message = "Xóa học viên thành công!" });
            }
            else
            {
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED!!!" }) { StatusCode = 403 };
            }
        }

        public async Task<IActionResult> OnPostUpdateTestScore([FromBody] UpdateTestScoreRequest request)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsUpdate)
            {
                try
                {
                    if (request.TestScore < 0 || request.TestScore > 1000)
                        return new JsonResult(new { success = false, message = "Điểm không hợp lệ (0-1000)." }) { StatusCode = 400 };

                    await MembersAccessData.UpdateTestScoreAsync(request.ResultKey, request.TestScore);
                    return new JsonResult(new { success = true, message = "Cập nhật điểm thành công!" });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in OnPostUpdateTestScore: Message={ex.Message}, StackTrace={ex.StackTrace}");
                    return new JsonResult(new { success = false, message = $"Cập nhật điểm thất bại: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            else
            {
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED!!!" }) { StatusCode = 403 };
            }
        }

        public async Task<IActionResult> OnPostCancelTest([FromBody] CancelTestRequest request)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsUpdate)
            {
                try
                {
                    await MembersAccessData.CancelTestAsync(request.ResultKey);
                    return new JsonResult(new { success = true, message = "Hủy bài thi thành công!" });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in OnPostCancelTest: Message={ex.Message}, StackTrace={ex.StackTrace}");
                    return new JsonResult(new { success = false, message = $"Hủy bài thi thất bại: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            else
            {
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED!!!" }) { StatusCode = 403 };
            }
        }

        public class UpdateTestScoreRequest
        {
            public Guid ResultKey { get; set; }
            public int TestScore { get; set; }
        }

        public class CancelTestRequest
        {
            public Guid ResultKey { get; set; }
        }
    }
}