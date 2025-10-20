using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TNS_TOEICAdmin.Models;

namespace TNS_TOEICAdmin.Pages.Manage
{
    [IgnoreAntiforgeryToken]
    public class DepartmentModel : PageModel
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
                UserLogin.GetRole("Departments");
            }
            else
            {
                IsFullAdmin = false;
                UserLogin.GetRole("Departments");
            }

            if (UserLogin.Role == null)
            {
                UserLogin.GetRole("Departments");
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

        public async Task<IActionResult> OnGetGetDepartments([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string search = "")
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsRead)
            {
                try
                {
                    Console.WriteLine($"Received request - Page: {page}, PageSize: {pageSize}, Search: '{search}'");
                    var departments = await DepartmentAccessData.GetDepartmentsAsync(page, pageSize, search);
                    var totalItems = await DepartmentAccessData.GetDepartmentCountAsync(search);
                    Console.WriteLine($"Response - Page: {page}, Items Returned: {departments.Count}, Total Items: {totalItems}");
                    return new JsonResult(new { status = "OK", data = departments, totalItems });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    return new JsonResult(new { status = "ERROR", message = "Unable to load department list." }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { status = "ERROR", message = "You do not have permission to view the list!" }) { StatusCode = 403 };
        }

        public async Task<IActionResult> OnPostCreate([FromBody] Department department)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsCreate)
            {
                if (department == null || string.IsNullOrEmpty(department.DepartmentID) || string.IsNullOrEmpty(department.DepartmentName))
                {
                    return new JsonResult(new { status = "ERROR", message = "Invalid data." }) { StatusCode = 400 };
                }

                try
                {
                    if (department.DepartmentKey == Guid.Empty)
                    {
                        department.DepartmentKey = Guid.NewGuid();
                    }
                    await DepartmentAccessData.AddDepartmentAsync(department);
                    return new JsonResult(new { status = "OK", message = "Department added successfully!" });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error adding department: {ex.Message}\nStack Trace: {ex.StackTrace}");
                    return new JsonResult(new { status = "ERROR", message = $"Add failures: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { status = "ERROR", message = "You do not have permission to add departments!" }) { StatusCode = 403 };
        }

        public async Task<IActionResult> OnPostUpdate([FromBody] Department department)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsUpdate)
            {
                if (department == null || department.DepartmentKey == Guid.Empty)
                {
                    return new JsonResult(new { status = "ERROR", message = "Invalid data." }) { StatusCode = 400 };
                }

                try
                {
                    await DepartmentAccessData.UpdateDepartmentAsync(department);
                    return new JsonResult(new { status = "OK", message = "Department updated successfully!" });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating department: {ex.Message}\nStack Trace: {ex.StackTrace}");
                    return new JsonResult(new { status = "ERROR", message = $"Update failed: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { status = "ERROR", message = "You do not have permission to update departments!" }) { StatusCode = 403 };
        }

        public async Task<IActionResult> OnGetDelete([FromQuery] Guid departmentKey)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role.IsDelete)
            {
                try
                {
                    await DepartmentAccessData.DeleteDepartmentAsync(departmentKey);
                    return new JsonResult(new { status = "OK", message = "Department deleted successfully!" });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting department: {ex.Message}");
                    return new JsonResult(new { status = "ERROR", message = "Delete failed." }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { status = "ERROR", message = "You do not have permission to delete departments!" }) { StatusCode = 403 };
        }
    }
}