using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using TNS_Auth;
using TNS_TOEICAdmin.Models;

namespace TNS_TOEICAdmin.Pages.Manage
{
    [IgnoreAntiforgeryToken]
    public class UsersModel : PageModel
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
                UserLogin.GetRole("Users");
            }
            else
            {
                IsFullAdmin = false;
                UserLogin.GetRole("Users");
            }

            if (UserLogin.Role == null)
            {
                UserLogin.GetRole("Users");
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

        public async Task<IActionResult> OnGetGetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string search = null, [FromQuery] string activate = null)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsRead)
            {
                var users = await UserAccessData.GetUsersAsync(page, pageSize, search, activate);
                var totalCount = await UserAccessData.GetTotalUsersCountAsync(search, activate); // ← THÊM DÒNG NÀY

                return new JsonResult(new { users, totalCount }); // ← SỬA DÒNG NÀY
            }
            else
            {
                return new JsonResult(new { status = "ERROR", message = "Bạn không có quyền xem danh sách!" }) { StatusCode = 403 };
            }
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
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsCreate)
            {
                if (string.IsNullOrEmpty(user.UserName) || (string.IsNullOrEmpty(user.Password) && user.UserKey == Guid.Empty))
                    return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };

                if (user.UserKey == Guid.Empty) user.UserKey = Guid.NewGuid();
                user.CreatedBy = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value);

                //// Mã hóa mật khẩu chỉ khi người dùng nhập mật khẩu mới
                //if (!string.IsNullOrEmpty(user.Password))
                //    user.Password = MyCryptography.HashPass(user.Password);  // Hash mật khẩu

                await UserAccessData.AddUserAsync(user);
                return new JsonResult(new { success = true, message = "Thêm người dùng thành công!" });

            }
            else
            {
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED!!!" }) { StatusCode = 403 };
            }
        }

        public async Task<IActionResult> OnPostUpdate([FromBody] User user)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsUpdate)
            {
                if (user.UserKey == Guid.Empty || string.IsNullOrEmpty(user.UserName))
                    return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };

                user.ModifiedBy = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value);

                //// Mã hóa mật khẩu chỉ khi người dùng nhập mật khẩu mới
                //if (!string.IsNullOrEmpty(user.Password))
                //    user.Password = MyCryptography.HashPass(user.Password);  // Hash mật khẩu

                await UserAccessData.UpdateUserAsync(user);
                return new JsonResult(new { success = true, message = "Cập nhật người dùng thành công!" });

            }
            else
            {
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED!!!" }) { StatusCode = 403 };
            }
        }


        public async Task<IActionResult> OnGetDelete(Guid userKey)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsDelete)
            {
                await UserAccessData.DeleteUserAsync(userKey);
                return new JsonResult(new { success = true, message = "Xóa người dùng thành công!" });
            }
            else
            {
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED!!!" }) { StatusCode = 403 };
            }
        }

        public async Task<IActionResult> OnPostUpdateRoles([FromBody] UserRoleUpdateRequest request)
        {
            CheckAuth();
            if(IsFullAdmin || UserLogin.Role.IsApproval)
            {
                await UserAccessData.UpdateUserRolesAsync(request.UserKey, request.Roles);
                return new JsonResult(new { success = true, message = "Cập nhật vai trò thành công!" });
            }
            else
            {
                return new JsonResult(new { status = "ERROR", message = "ACCESS DENIED!!!" }) { StatusCode = 403 };
            }
        }
        public class UserRoleUpdateRequest
        {
            public Guid UserKey { get; set; }
            public List<UserRole> Roles { get; set; }
        }
    }
}