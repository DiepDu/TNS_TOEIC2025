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
        public string PhotoPath { get; set; }

        public string ErrorMessage { get; set; }

        public EmployeeProfileModel() { }

        public void OnGet()
        {
            string employeeKey = HttpContext.User.Claims.FirstOrDefault(c => c.Type == "EmployeeKey")?.Value;
            if (string.IsNullOrEmpty(employeeKey))
            {
                Console.WriteLine("EmployeeKey not found in claims, redirecting to Login.");
                Response.Redirect("/Login");
                return;
            }

            Console.WriteLine($"Fetching profile for EmployeeKey: {employeeKey}");
            var employeeInfo = EmployeeProfileAccessData.GetEmployeeProfile(employeeKey);
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
                PhotoPath = string.IsNullOrEmpty(employeeInfo.PhotoPath) ? "/images/avatar/default.jpg" : employeeInfo.PhotoPath;
                Console.WriteLine($"Profile loaded: EmployeeID={EmployeeID}, PhotoPath={PhotoPath}");
            }
            else
            {
                Console.WriteLine($"No profile found for EmployeeKey: {employeeKey}, redirecting to Login.");
                Response.Redirect("/Login");
            }
        }

        public IActionResult OnPostSaveChanges()
        {
            string employeeKey = HttpContext.User.Claims.FirstOrDefault(c => c.Type == "EmployeeKey")?.Value;
            if (string.IsNullOrEmpty(employeeKey))
            {
                return RedirectToPage("/Login");
            }

            Console.WriteLine($"Updating profile for EmployeeKey: {employeeKey}, LastName: {LastName}, FirstName: {FirstName}");
            bool isUpdated = EmployeeProfileAccessData.UpdateEmployeeProfile(employeeKey, LastName, FirstName);
            if (isUpdated)
            {
                TempData["SuccessMessage"] = "Hồ sơ đã được cập nhật thành công!";
                Console.WriteLine("Profile updated successfully.");
            }
            else
            {
                ErrorMessage = "Không thể cập nhật hồ sơ. Vui lòng thử lại.";
                Console.WriteLine("Profile update failed.");
            }

            OnGet();
            return Page();
        }

        public async Task<IActionResult> OnPostUploadImage(IFormFile image)
        {
            string employeeKey = HttpContext.User.Claims.FirstOrDefault(c => c.Type == "EmployeeKey")?.Value;
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

            string oldPhotoPath = EmployeeProfileAccessData.GetEmployeeAvatar(employeeKey);
            if (!string.IsNullOrEmpty(oldPhotoPath) && oldPhotoPath != "/images/avatar/default.jpg")
            {
                string oldPhotoFullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", oldPhotoPath.TrimStart('/'));
                if (System.IO.File.Exists(oldPhotoFullPath))
                {
                    System.IO.File.Delete(oldPhotoFullPath);
                    Console.WriteLine($"Deleted old photo: {oldPhotoFullPath}");
                }
            }

            string newFileName = $"{employeeKey}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
            string newFilePath = Path.Combine(avatarFolderPath, newFileName);
            string newPhotoPath = $"/images/avatar/{newFileName}";

            using (var stream = new FileStream(newFilePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }
            Console.WriteLine($"Saved new photo: {newFilePath}");

            bool isUpdated = EmployeeProfileAccessData.UpdateEmployeeAvatar(employeeKey, newPhotoPath);
            if (isUpdated)
            {
                var user = HttpContext.User;
                var identity = user.Identity as ClaimsIdentity;
                if (identity != null)
                {
                    var existingPhotoClaim = identity.FindFirst("PhotoPath");
                    if (existingPhotoClaim != null)
                    {
                        identity.RemoveClaim(existingPhotoClaim);
                    }
                    identity.AddClaim(new Claim("PhotoPath", newPhotoPath));
                    var newPrincipal = new ClaimsPrincipal(identity);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, newPrincipal, new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = user.FindFirst(ClaimTypes.Expiration)?.Value != null
                            ? DateTimeOffset.Parse(user.FindFirst(ClaimTypes.Expiration).Value)
                            : DateTimeOffset.UtcNow.AddDays(7)
                    });
                    Console.WriteLine($"Updated claim with new PhotoPath: {newPhotoPath}");
                }
                return new JsonResult(new { success = true });
            }
            else
            {
                Console.WriteLine("Failed to update PhotoPath in database.");
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
        public string PhotoPath { get; set; }
    }
}