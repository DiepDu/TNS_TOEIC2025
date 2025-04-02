using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.SqlClient;
using System.Security.Claims;
using TNS.Member;
using TNS_TOEICTest.DataAccess;

namespace TNS_TOEICTest.Pages.Account
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

        private readonly ProfileAccessData _dataAccess;

        public ChangePasswordModel(ProfileAccessData dataAccess)
        {
            _dataAccess = dataAccess;
        }

        public IActionResult OnGet()
        {
            string memberKey = User.Claims.FirstOrDefault(c => c.Type == "MemberKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(memberKey))
            {
                return RedirectToPage("/Login");
            }
            return Page();
        }

        public IActionResult OnPostChangePassword()
        {
            string memberKey = User.Claims.FirstOrDefault(c => c.Type == "MemberKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(memberKey))
            {
                return RedirectToPage("/Login");
            }

            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "New Password and Confirm Password do not match.";
                return Page();
            }

            // Lấy mật khẩu cũ từ database
            string storedPassword = _dataAccess.GetUserPassword(memberKey);
            if (string.IsNullOrEmpty(storedPassword))
            {
                ErrorMessage = "User not found.";
                return Page();
            }

            // Kiểm tra mật khẩu cũ
            if (!MyCryptography.VerifyHash(OldPassword, storedPassword))
            {
                ErrorMessage = "Old Password is incorrect.";
                return Page();
            }

            // Mã hóa mật khẩu mới
            string newHashedPassword = MyCryptography.HashPass(NewPassword);

            // Cập nhật mật khẩu mới vào database
            bool isUpdated = _dataAccess.UpdateUserPassword(memberKey, newHashedPassword);
            if (!isUpdated)
            {
                ErrorMessage = "Failed to update password. Please try again.";
                return Page();
            }

            TempData["SuccessMessage"] = "Password changed successfully.";
            return RedirectToPage("/Account/ViewProfile");
        }
    }
}