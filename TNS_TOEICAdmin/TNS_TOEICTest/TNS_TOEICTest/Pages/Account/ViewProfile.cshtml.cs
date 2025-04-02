using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using TNS_TOEICTest.DataAccess;

namespace TNS_TOEICTest.Pages.Account
{
    [IgnoreAntiforgeryToken]
    public class ViewProfileModel : PageModel
    {
        [BindProperty]
        public string MemberName { get; set; }

        [BindProperty]
        public string MemberID { get; set; }

        [BindProperty]
        public int Gender { get; set; }

        [BindProperty]
        public int YearOld { get; set; }

        [BindProperty]
        public DateTime CreatedOn { get; set; }

        [BindProperty]
        public string Avatar { get; set; }

        public string ErrorMessage { get; set; }

        private readonly ProfileAccessData _dataAccess;

        public ViewProfileModel(ProfileAccessData dataAccess)
        {
            _dataAccess = dataAccess;
        }

        public void OnGet()
        {
            string memberKey = User.Claims.FirstOrDefault(c => c.Type == "MemberKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(memberKey))
            {
                Response.Redirect("/Login");
                return;
            }

            var userInfo = _dataAccess.GetUserProfile(memberKey);
            if (userInfo != null)
            {
                MemberName = userInfo.MemberName;
                MemberID = userInfo.MemberID;
                Gender = userInfo.Gender;
                YearOld = userInfo.YearOld;
                CreatedOn = userInfo.CreatedOn;
                Avatar = string.IsNullOrEmpty(userInfo.Avatar) ? "/images/avatar/default.jpg" : userInfo.Avatar;
            }
            else
            {
                Response.Redirect("/Login");
            }
        }

        public IActionResult OnPostSaveChanges()
        {
            string memberKey = User.Claims.FirstOrDefault(c => c.Type == "MemberKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(memberKey))
            {
                return RedirectToPage("/Login");
            }

            // Kiểm tra Email trùng lặp (trừ chính user hiện tại)
            if (_dataAccess.CheckEmailExists(MemberID, memberKey))
            {
                ErrorMessage = "Email đã tồn tại. Vui lòng sử dụng Email khác.";
                OnGet(); // Tải lại thông tin để hiển thị
                return Page();
            }

            bool isUpdated = _dataAccess.UpdateUserProfile(memberKey, MemberName, Gender, YearOld);
            if (isUpdated)
            {
                TempData["SuccessMessage"] = "Profile updated successfully!";
            }
            else
            {
                ErrorMessage = "Failed to update profile. Please try again.";
            }

            OnGet(); // Tải lại thông tin
            return Page();
        }

        public async Task<IActionResult> OnPostUploadImage(IFormFile image)
        {
            string memberKey = User.Claims.FirstOrDefault(c => c.Type == "MemberKey" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(memberKey))
            {
                return new JsonResult(new { success = false, errorMessage = "User not logged in." });
            }

            if (image == null || image.Length == 0)
            {
                return new JsonResult(new { success = false, errorMessage = "No image uploaded." });
            }

            string avatarFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "avatar");
            if (!Directory.Exists(avatarFolderPath))
            {
                Directory.CreateDirectory(avatarFolderPath);
            }

            // Xóa ảnh cũ nếu có
            string oldAvatarPath = _dataAccess.GetUserAvatar(memberKey);
            if (!string.IsNullOrEmpty(oldAvatarPath) && oldAvatarPath != "/images/avatar/default.jpg")
            {
                string oldAvatarFullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", oldAvatarPath.TrimStart('/'));
                if (System.IO.File.Exists(oldAvatarFullPath))
                {
                    System.IO.File.Delete(oldAvatarFullPath);
                }
            }

            // Tạo tên file mới
            string newFileName = $"{memberKey}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
            string newFilePath = Path.Combine(avatarFolderPath, newFileName);
            string newAvatarPath = $"/images/avatar/{newFileName}";

            // Lưu ảnh mới
            using (var stream = new FileStream(newFilePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            // Cập nhật đường dẫn ảnh vào database
            bool isUpdated = _dataAccess.UpdateUserAvatar(memberKey, newAvatarPath);
            if (isUpdated)
            {
                // Bổ sung: Cập nhật claim Avatar trong cookie
                var user = HttpContext.User;
                var identity = user.Identity as ClaimsIdentity;
                if (identity != null)
                {
                    // Xóa claim Avatar cũ (nếu có)
                    var existingAvatarClaim = identity.FindFirst("Avatar");
                    if (existingAvatarClaim != null)
                    {
                        identity.RemoveClaim(existingAvatarClaim);
                    }

                    // Thêm claim Avatar mới
                    identity.AddClaim(new Claim("Avatar", newAvatarPath));

                    // Tạo ClaimsPrincipal mới
                    var newPrincipal = new ClaimsPrincipal(identity);

                    // Cập nhật cookie xác thực
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, newPrincipal, new AuthenticationProperties
                    {
                        IsPersistent = true, // Giữ cookie sau khi đóng trình duyệt (tùy thuộc vào cấu hình ban đầu của bạn)
                        ExpiresUtc = user.FindFirst(ClaimTypes.Expiration)?.Value != null
                            ? DateTimeOffset.Parse(user.FindFirst(ClaimTypes.Expiration).Value)
                            : DateTimeOffset.UtcNow.AddDays(7) // Thời gian hết hạn của cookie
                    });
                }

                return new JsonResult(new { success = true });
            }
            else
            {
                return new JsonResult(new { success = false, errorMessage = "Failed to update avatar." });
            }
        }
    }

    public class UserProfile
    {
        public string MemberName { get; set; }
        public string MemberID { get; set; }
        public int Gender { get; set; }
        public int YearOld { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Avatar { get; set; }
    }
}