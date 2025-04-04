using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TNS_TOEICAdmin.Models;

namespace TNS_TOEICAdmin.Pages.Manage
{
    [IgnoreAntiforgeryToken]
    public class DepartmentModel : PageModel
    {
        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnGetGetDepartments(int page = 1, int pageSize = 10, string search = "")
        {
            try
            {
                var departments = await DepartmentAccessData.GetDepartmentsAsync(page, pageSize, search);
                var totalItems = await DepartmentAccessData.GetDepartmentCountAsync(search);
                return new JsonResult(new { data = departments, totalItems });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new JsonResult(new { success = false, message = "Không thể tải danh sách phòng ban." }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostCreate([FromBody] Department department)
        {
            if (department == null || string.IsNullOrEmpty(department.DepartmentID) || string.IsNullOrEmpty(department.DepartmentName))
            {
                return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };
            }

            try
            {
                if (department.DepartmentKey == Guid.Empty)
                {
                    department.DepartmentKey = Guid.NewGuid();
                }
                await DepartmentAccessData.AddDepartmentAsync(department);
                return new JsonResult(new { success = true, message = "Thêm phòng ban thành công!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi thêm phòng ban: {ex.Message}\nStack Trace: {ex.StackTrace}");
                return new JsonResult(new { success = false, message = $"Thêm phòng ban thất bại: {ex.Message}" }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostUpdate([FromBody] Department department)
        {
            if (department == null || department.DepartmentKey == Guid.Empty)
            {
                return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ." }) { StatusCode = 400 };
            }

            try
            {
                await DepartmentAccessData.UpdateDepartmentAsync(department);
                return new JsonResult(new { success = true, message = "Cập nhật phòng ban thành công!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi cập nhật phòng ban: {ex.Message}\nStack Trace: {ex.StackTrace}");
                return new JsonResult(new { success = false, message = $"Cập nhật phòng ban thất bại: {ex.Message}" }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnGetDelete(Guid departmentKey)
        {
            try
            {
                //bool canDelete = await DepartmentAccessData.CanDeleteDepartmentAsync(departmentKey);
                //if (!canDelete)
                //{
                //    return new JsonResult(new { success = false, message = "Không thể xóa vì phòng ban có phòng ban con hoặc nhân viên." });
                //}

                await DepartmentAccessData.DeleteDepartmentAsync(departmentKey);
                return new JsonResult(new { success = true, message = "Xóa phòng ban thành công!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new JsonResult(new { success = false, message = "Xóa phòng ban thất bại." }) { StatusCode = 500 };
            }
        }
    }
}