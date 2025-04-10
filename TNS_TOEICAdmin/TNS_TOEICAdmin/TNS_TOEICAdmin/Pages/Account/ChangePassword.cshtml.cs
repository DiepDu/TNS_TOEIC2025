using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Security.Claims;
using TNS_Auth;
using TNS_TOEICAdmin.DataAccess;

namespace TNS_TOEICAdmin.Pages.Account
{
    [IgnoreAntiforgeryToken]
    public class ChangePasswordModel : PageModel
    {
        [BindProperty]
        public string OldPassword { get; set; }

        [BindProperty]
        public string NewPassword { get; set; }

        [BindProperty]
        public string ConfirmPassword { get; set; }

        public string ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            string employeeKey = HttpContext.User.Claims.FirstOrDefault(c => c.Type == "EmployeeKey")?.Value;
            if (string.IsNullOrEmpty(employeeKey))
            {
                return RedirectToPage("/Login");
            }
            return Page();
        }

        public IActionResult OnPostChangePassword()
        {
            string employeeKey = HttpContext.User.Claims.FirstOrDefault(c => c.Type == "EmployeeKey")?.Value;
            if (string.IsNullOrEmpty(employeeKey))
            {
                return RedirectToPage("/Login");
            }

            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "Mật khẩu mới và xác nhận mật khẩu không khớp.";
                return Page();
            }

            string storedPassword = EmployeeProfileAccessData.GetUserPassword(employeeKey);
            if (string.IsNullOrEmpty(storedPassword))
            {
                ErrorMessage = "Không tìm thấy thông tin người dùng trong SYS_Users.";
                return Page();
            }

            if (!MyCryptography.VerifyHash(OldPassword, storedPassword))
            {
                ErrorMessage = "Mật khẩu cũ không đúng.";
                return Page();
            }

            string newHashedPassword = MyCryptography.HashPass(NewPassword);

            bool isUpdated = EmployeeProfileAccessData.UpdateUserPassword(employeeKey, newHashedPassword);
            if (!isUpdated)
            {
                ErrorMessage = "Không thể cập nhật mật khẩu trong SYS_Users. Vui lòng thử lại.";
                return Page();
            }

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công.";
            return RedirectToPage("/Account/EmployeeProfile");
        }
    }
}