using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.IO;
using TNS_TOEICAdmin.DataAccess;

namespace TNS_TOEICAdmin.Pages.Account
{
    [IgnoreAntiforgeryToken]
    public class EmployeeProfileModel : PageModel
    {
        [BindProperty]
        public string EmployeeID { get; set; }

        [BindProperty]
        public string LastName { get; set; }

        [BindProperty]
        public string FirstName { get; set; }

        [BindProperty]
        public Guid? DepartmentKey { get; set; }

        [BindProperty]
        public string DepartmentName { get; set; }

        [BindProperty]
        public string CompanyEmail { get; set; }

        [BindProperty]
        public DateTime? StartingDate { get; set; }

        [BindProperty]
        public DateTime? LeavingDate { get; set; }

        [BindProperty]
        public string Avatar { get; set; }

        public string ErrorMessage { get; set; }

        private readonly EmployeeProfileAccessData _dataAccess;

        public EmployeeProfileModel(EmployeeProfileAccessData dataAccess)
        {
            _dataAccess = dataAccess;
        }

        public void OnGet()
        {
            string employeeKey = User.Claims.FirstOrDefault(c => c.Type == "EmployeeKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(employeeKey))
            {
                Response.Redirect("/Login");
                return;
            }

            var employeeInfo = _dataAccess.GetEmployeeProfile(employeeKey);
            if (employeeInfo != null)
            {
                EmployeeID = employeeInfo.EmployeeID;
                LastName = employeeInfo.LastName;
                FirstName = employeeInfo.FirstName;
                DepartmentKey = employeeInfo.DepartmentKey;
                DepartmentName = employeeInfo.DepartmentName;
                CompanyEmail = employeeInfo.CompanyEmail;
                StartingDate = employeeInfo.StartingDate;
                LeavingDate = employeeInfo.LeavingDate;
                Avatar = string.IsNullOrEmpty(employeeInfo.Avatar) ? "/images/avatar/default.jpg" : employeeInfo.Avatar;
            }
            else
            {
                Response.Redirect("/Login");
            }
        }

        public IActionResult OnPostSaveChanges()
        {
            string employeeKey = User.Claims.FirstOrDefault(c => c.Type == "EmployeeKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(employeeKey))
            {
                return RedirectToPage("/Login");
            }

            bool isUpdated = _dataAccess.UpdateEmployeeProfile(employeeKey, LastName, FirstName);
            if (isUpdated)
            {
                TempData["SuccessMessage"] = "Hồ sơ đã được cập nhật thành công!";
            }
            else
            {
                ErrorMessage = "Không thể cập nhật hồ sơ. Vui lòng thử lại.";
            }

            OnGet(); // Tải lại thông tin
            return Page();
        }

        public async Task<IActionResult> OnPostUploadImage(IFormFile image)
        {
            string employeeKey = User.Claims.FirstOrDefault(c => c.Type == "EmployeeKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(employeeKey))
            {
                return new JsonResult(new { success = false, errorMessage = "Người dùng chưa đăng nhập." });
            }

            if (image == null || image.Length == 0)
            {
                return new JsonResult(new { success = false, errorMessage = "Không có ảnh được tải lên." });
            }

            string avatarFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "avatar");
            if (!Directory.Exists(avatarFolderPath))
            {
                Directory.CreateDirectory(avatarFolderPath);
            }

            // Xóa ảnh cũ nếu có
            string oldAvatarPath = _dataAccess.GetEmployeeAvatar(employeeKey);
            if (!string.IsNullOrEmpty(oldAvatarPath) && oldAvatarPath != "/images/avatar/default.jpg")
            {
                string oldAvatarFullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", oldAvatarPath.TrimStart('/'));
                if (System.IO.File.Exists(oldAvatarFullPath))
                {
                    System.IO.File.Delete(oldAvatarFullPath);
                }
            }

            // Tạo tên file mới
            string newFileName = $"{employeeKey}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
            string newFilePath = Path.Combine(avatarFolderPath, newFileName);
            string newAvatarPath = $"/images/avatar/{newFileName}";

            // Lưu ảnh mới
            using (var stream = new FileStream(newFilePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            // Cập nhật đường dẫn ảnh vào database
            bool isUpdated = _dataAccess.UpdateEmployeeAvatar(employeeKey, newAvatarPath);
            if (isUpdated)
            {
                var user = HttpContext.User;
                var identity = user.Identity as ClaimsIdentity;
                if (identity != null)
                {
                    var existingAvatarClaim = identity.FindFirst("Avatar");
                    if (existingAvatarClaim != null)
                    {
                        identity.RemoveClaim(existingAvatarClaim);
                    }
                    identity.AddClaim(new Claim("Avatar", newAvatarPath));
                    var newPrincipal = new ClaimsPrincipal(identity);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, newPrincipal, new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = user.FindFirst(ClaimTypes.Expiration)?.Value != null
                            ? DateTimeOffset.Parse(user.FindFirst(ClaimTypes.Expiration).Value)
                            : DateTimeOffset.UtcNow.AddDays(7)
                    });
                }
                return new JsonResult(new { success = true });
            }
            else
            {
                return new JsonResult(new { success = false, errorMessage = "Không thể cập nhật ảnh." });
            }
        }
    }

    public class EmployeeProfile
    {
        public string EmployeeID { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public Guid? DepartmentKey { get; set; }
        public string DepartmentName { get; set; }
        public string CompanyEmail { get; set; }
        public DateTime? StartingDate { get; set; }
        public DateTime? LeavingDate { get; set; }
        public string Avatar { get; set; }
    }
}