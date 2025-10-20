using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using TNS_Auth;
using TNS_TOEICAdmin.Models;

namespace TNS_TOEICAdmin.Pages.Manage
{
    [IgnoreAntiforgeryToken]
    public class EmployeeModel : PageModel
    {
        #region [Security]
        public TNS_Auth.UserLogin_Info UserLogin;
        public bool IsFullAdmin { get; private set; }

        private void CheckAuth()
        {
            UserLogin = new TNS_Auth.UserLogin_Info(User);
            var fullRole = new TNS_Auth.Role_Info(UserLogin.UserKey, "Full");
            IsFullAdmin = fullRole.GetCode() == "200";
            UserLogin.GetRole("Employees");
        }
        #endregion

        public IActionResult OnGet()
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role?.IsRead == true)
            {
                return Page();
            }
            TempData["Error"] = "ACCESS DENIED!!!";
            return RedirectToPage("/Index");
        }

        public async Task<IActionResult> OnGetGetEmployees([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string search = null, [FromQuery] string status = null)
        {
            var employees = await EmployeeAccessData.GetEmployeesAsync(page, pageSize, search, status);
            var totalCount = await EmployeeAccessData.GetTotalEmployeesCountAsync(search, status);

            return new JsonResult(new { data = employees, totalCount });
        }

        public async Task<IActionResult> OnGetGetDepartments()
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role?.IsRead == true)
            {
                try
                {
                    var departments = await EmployeeAccessData.GetDepartmentsAsync();
                    return new JsonResult(departments ?? new List<DepartmentEntity>());
                }
                catch (Exception ex)
                {
                    return new JsonResult(new { status = "ERROR", message = $"Error when getting department list: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { status = "ERROR", message = "You do not have permission to view departments!" }) { StatusCode = 403 };
        }

        public async Task<IActionResult> OnPostCreate([FromBody] EmployeeEntity employee)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role?.IsCreate == true)
            {
                try
                {
                    if (employee == null || string.IsNullOrEmpty(employee.EmployeeID) ||
                        string.IsNullOrEmpty(employee.LastName) || string.IsNullOrEmpty(employee.FirstName) ||
                        string.IsNullOrEmpty(employee.CompanyEmail))
                    {
                        return new JsonResult(new { success = false, message = "data invalid!" });
                    }

                    var userKeyString = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userKeyString) && Guid.TryParse(userKeyString, out Guid userKey))
                    {
                        employee.CreatedBy = userKey;
                        employee.ModifiedBy = userKey;
                    }
                    else
                    {
                        return new JsonResult(new { success = false, message = "Unable to identify user!" }) { StatusCode = 403 };
                    }

                    employee.CreatedOn = DateTime.Now;
                    employee.ModifiedOn = DateTime.Now;

                    bool result = await EmployeeAccessData.CreateEmployeeAsync(employee);
                    if (result)
                    {
                        return new JsonResult(new { success = true, message = "Add employee successfully!" });
                    }
                    return new JsonResult(new { success = false, message = "Add failed employee!" });
                }
                catch (Exception ex)
                {
                    return new JsonResult(new { success = false, message = $"Error when adding employee: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { success = false, message = "You do not have create permissions!" }) { StatusCode = 403 };
        }

        public async Task<IActionResult> OnPostUpdate([FromBody] EmployeeEntity employee)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role?.IsUpdate == true)
            {
                try
                {
                    if (employee.EmployeeKey == Guid.Empty || string.IsNullOrEmpty(employee.LastName) ||
                        string.IsNullOrEmpty(employee.FirstName) || string.IsNullOrEmpty(employee.CompanyEmail))
                    {
                        return new JsonResult(new { success = false, message = "Data is invalid!" }) { StatusCode = 400 };
                    }

                    employee.ModifiedBy = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
                    employee.ModifiedOn = DateTime.Now;

                    await EmployeeAccessData.UpdateEmployeeAsync(employee);
                    return new JsonResult(new { success = true, message = "Successfully updated employee!" });
                }
                catch (Exception ex)
                {
                    return new JsonResult(new { success = false, message = $"Error when updating employee: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { status = "ERROR", message = "You do not have update permission!" }) { StatusCode = 403 };
        }

        public async Task<IActionResult> OnGetDelete(Guid employeeKey)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role?.IsDelete == true)
            {
                try
                {
                    if (employeeKey == Guid.Empty)
                    {
                        return new JsonResult(new { success = false, message = "EmployeeKey is invalid!" }) { StatusCode = 400 };
                    }
                    await EmployeeAccessData.DeleteEmployeeAsync(employeeKey);
                    return new JsonResult(new { success = true, message = "successfully deleted employee!" });
                }
                catch (Exception ex)
                {
                    return new JsonResult(new { success = false, message = $"Error when deleting employee: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { status = "ERROR", message = "You do not have delete permissions!" }) { StatusCode = 403 };
        }

        public async Task<IActionResult> OnGetSoftDelete(Guid employeeKey)
        {
            CheckAuth();
            if (IsFullAdmin || UserLogin.Role?.IsDelete == true)
            {
                try
                {
                    if (employeeKey == Guid.Empty)
                    {
                        return new JsonResult(new { success = false, message = "EmployeeKey is invalid!" }) { StatusCode = 400 };
                    }
                    await EmployeeAccessData.SoftDeleteEmployeeAsync(employeeKey);
                    return new JsonResult(new { success = true, message = "successfully deleted employee!" });
                }
                catch (Exception ex)
                {
                    return new JsonResult(new { success = false, message = $"Error when deleting employee: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { status = "ERROR", message = "You do not have soft delete permissions!" }) { StatusCode = 403 };
        }
    }
}