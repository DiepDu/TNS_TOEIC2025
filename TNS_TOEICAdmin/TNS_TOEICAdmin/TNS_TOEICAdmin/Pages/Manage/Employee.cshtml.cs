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

        public async Task<IActionResult> OnGetGetEmployees(int offset = 0, int pageSize = 10, string search = "", string status = "")
        {
            try
            {
                Console.WriteLine($"Received offset: {offset}, pageSize: {pageSize}, search: {search}, status: {status}");
                var (employees, totalRecords) = await EmployeeAccessData.GetEmployeesAsync(offset, pageSize, search, status);
                bool hasMore = offset + employees.Count < totalRecords; // Kiểm tra có dữ liệu để tải thêm không
                return new JsonResult(new { data = employees, totalRecords, hasMore });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = "Lỗi khi lấy danh sách nhân viên: " + ex.Message }) { StatusCode = 500 };
            }
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
                    return new JsonResult(new { status = "ERROR", message = $"Lỗi khi lấy danh sách phòng ban: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { status = "ERROR", message = "Bạn không có quyền xem phòng ban!" }) { StatusCode = 403 };
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
                        return new JsonResult(new { success = false, message = "Dữ liệu nhân viên không hợp lệ!" });
                    }

                    var userKeyString = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userKeyString) && Guid.TryParse(userKeyString, out Guid userKey))
                    {
                        employee.CreatedBy = userKey;
                        employee.ModifiedBy = userKey;
                    }
                    else
                    {
                        return new JsonResult(new { success = false, message = "Không thể xác định người dùng!" }) { StatusCode = 403 };
                    }

                    employee.CreatedOn = DateTime.Now;
                    employee.ModifiedOn = DateTime.Now;

                    bool result = await EmployeeAccessData.CreateEmployeeAsync(employee);
                    if (result)
                    {
                        return new JsonResult(new { success = true, message = "Thêm nhân viên thành công!" });
                    }
                    return new JsonResult(new { success = false, message = "Thêm nhân viên thất bại!" });
                }
                catch (Exception ex)
                {
                    return new JsonResult(new { success = false, message = $"Lỗi khi thêm nhân viên: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { success = false, message = "Bạn không có quyền thêm nhân viên!" }) { StatusCode = 403 };
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
                        return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ!" }) { StatusCode = 400 };
                    }

                    employee.ModifiedBy = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
                    employee.ModifiedOn = DateTime.Now;

                    await EmployeeAccessData.UpdateEmployeeAsync(employee);
                    return new JsonResult(new { success = true, message = "Cập nhật nhân viên thành công!" });
                }
                catch (Exception ex)
                {
                    return new JsonResult(new { success = false, message = $"Lỗi khi cập nhật nhân viên: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { status = "ERROR", message = "Bạn không có quyền sửa!" }) { StatusCode = 403 };
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
                        return new JsonResult(new { success = false, message = "EmployeeKey không hợp lệ!" }) { StatusCode = 400 };
                    }
                    await EmployeeAccessData.DeleteEmployeeAsync(employeeKey);
                    return new JsonResult(new { success = true, message = "Xóa nhân viên thành công!" });
                }
                catch (Exception ex)
                {
                    return new JsonResult(new { success = false, message = $"Lỗi khi xóa nhân viên: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { status = "ERROR", message = "Bạn không có quyền xóa!" }) { StatusCode = 403 };
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
                        return new JsonResult(new { success = false, message = "EmployeeKey không hợp lệ!" }) { StatusCode = 400 };
                    }
                    await EmployeeAccessData.SoftDeleteEmployeeAsync(employeeKey);
                    return new JsonResult(new { success = true, message = "Xóa mềm nhân viên thành công!" });
                }
                catch (Exception ex)
                {
                    return new JsonResult(new { success = false, message = $"Lỗi khi xóa mềm nhân viên: {ex.Message}" }) { StatusCode = 500 };
                }
            }
            return new JsonResult(new { status = "ERROR", message = "Bạn không có quyền xóa mềm!" }) { StatusCode = 403 };
        }
    }
}