using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using TNS.Auth;
using TNS_TOEICAdmin.Models;

namespace TNS_TOEICAdmin.Pages.Manage
{
    [IgnoreAntiforgeryToken]
    public class UsersModel : PageModel
    {
        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnGetGetUsers()
        {
            var users = await UserAccessData.GetUsersAsync();
            return new JsonResult(users);
        }

        public async Task<IActionResult> OnGetGetAllRoles()
        {
            var roles = await UserAccessData.GetAllRolesAsync();
            return new JsonResult(roles);
        }

        public async Task<IActionResult> OnGetGetEmployees()
        {
            var employees = await UserAccessData.GetEmployeesAsync();
            return new JsonResult(employees);
        }

        public async Task<IActionResult> OnPostCreate([FromBody] User user)
        {
            if (string.IsNullOrEmpty(user.UserName) || (string.IsNullOrEmpty(user.Password) && user.UserKey == Guid.Empty))
                return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };

            if (user.UserKey == Guid.Empty) user.UserKey = Guid.NewGuid();
            user.CreatedBy = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value);
            if (!string.IsNullOrEmpty(user.Password)) user.Password = MyCryptography.HashPass(user.Password);
            await UserAccessData.AddUserAsync(user);
            return new JsonResult(new { success = true, message = "Thêm người dùng thành công!" });
        }

        public async Task<IActionResult> OnPostUpdate([FromBody] User user)
        {
            if (user.UserKey == Guid.Empty || string.IsNullOrEmpty(user.UserName))
                return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };

            user.ModifiedBy = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value);
            if (!string.IsNullOrEmpty(user.Password)) user.Password = MyCryptography.HashPass(user.Password);
            await UserAccessData.UpdateUserAsync(user);
            return new JsonResult(new { success = true, message = "Cập nhật người dùng thành công!" });
        }

        public async Task<IActionResult> OnGetDelete(Guid userKey)
        {
            await UserAccessData.DeleteUserAsync(userKey);
            return new JsonResult(new { success = true, message = "Xóa người dùng thành công!" });
        }

        public async Task<IActionResult> OnPostUpdateRoles([FromBody] UserRoleUpdateRequest request)
        {
            await UserAccessData.UpdateUserRolesAsync(request.UserKey, request.Roles);
            return new JsonResult(new { success = true, message = "Cập nhật vai trò thành công!" });
        }
        public class UserRoleUpdateRequest
        {
            public Guid UserKey { get; set; }
            public List<UserRole> Roles { get; set; }
        }
    }
}