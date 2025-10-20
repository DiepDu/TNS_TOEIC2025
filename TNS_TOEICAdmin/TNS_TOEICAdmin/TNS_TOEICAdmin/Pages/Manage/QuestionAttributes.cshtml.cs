using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TNS_TOEICAdmin.Models;

namespace TNS_TOEICAdmin.Pages.Manage
{
    [IgnoreAntiforgeryToken]
    public class QuestionAttributesModel : PageModel
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
                UserLogin.GetRole("Questions");
            }
            else
            {
                IsFullAdmin = false;
                UserLogin.GetRole("Questions");
            }

            if (UserLogin.Role == null)
            {
                UserLogin.GetRole("Questions");
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

        #region [GET Data Endpoints]
        public async Task<IActionResult> OnGetGetAttributes([FromQuery] string type, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string search = "")
        {
            CheckAuth();
            if (!(IsFullAdmin || UserLogin.Role.IsRead))
            {
                return new JsonResult(new { status = "ERROR", message = "You do not have permission to view the list!" }) { StatusCode = 403 };
            }

            try
            {
                var (data, totalItems) = await QuestionAttributesAccessData.GetAttributesAsync(type, page, pageSize, search);
                return new JsonResult(new { status = "OK", data, totalItems });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new JsonResult(new { status = "ERROR", message = "Unable to load attribute list." }) { StatusCode = 500 };
            }
        }
        #endregion

        #region [CREATE Endpoints]
        public async Task<IActionResult> OnPostCreate([FromBody] AttributeRequest request)
        {
            CheckAuth();
            if (!(IsFullAdmin || UserLogin.Role.IsCreate))
            {
                return new JsonResult(new { status = "ERROR", message = "You do not have permission to add attributes!" }) { StatusCode = 403 };
            }

            if (request == null || string.IsNullOrEmpty(request.Type) || string.IsNullOrEmpty(request.Name))
            {
                return new JsonResult(new { status = "ERROR", message = "Invalid data." }) { StatusCode = 400 };
            }

            try
            {
                await QuestionAttributesAccessData.CreateAttributeAsync(request.Type, request.Name);
                return new JsonResult(new { status = "OK", message = "Attribute added successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding attribute: {ex.Message}");
                return new JsonResult(new { status = "ERROR", message = $"Add failed: {ex.Message}" }) { StatusCode = 500 };
            }
        }
        #endregion

        #region [UPDATE Endpoints]
        public async Task<IActionResult> OnPostUpdate([FromBody] AttributeRequest request)
        {
            CheckAuth();
            if (!(IsFullAdmin || UserLogin.Role.IsUpdate))
            {
                return new JsonResult(new { status = "ERROR", message = "You do not have permission to update attributes!" }) { StatusCode = 403 };
            }

            if (request == null || string.IsNullOrEmpty(request.Type) || string.IsNullOrEmpty(request.Name))
            {
                return new JsonResult(new { status = "ERROR", message = "Invalid data." }) { StatusCode = 400 };
            }

            try
            {
                await QuestionAttributesAccessData.UpdateAttributeAsync(request.Type, request.Key, request.Name);
                return new JsonResult(new { status = "OK", message = "Attribute updated successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating attribute: {ex.Message}");
                return new JsonResult(new { status = "ERROR", message = $"Update failed: {ex.Message}" }) { StatusCode = 500 };
            }
        }
        #endregion

        #region [DELETE Endpoints]
        public async Task<IActionResult> OnGetDelete([FromQuery] string type, [FromQuery] string key)
        {
            CheckAuth();
            if (!(IsFullAdmin || UserLogin.Role.IsDelete))
            {
                return new JsonResult(new { status = "ERROR", message = "You do not have permission to delete attributes!" }) { StatusCode = 403 };
            }

            try
            {
                await QuestionAttributesAccessData.DeleteAttributeAsync(type, key);
                return new JsonResult(new { status = "OK", message = "Attribute deleted successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting attribute: {ex.Message}");
                return new JsonResult(new { status = "ERROR", message = "Delete failed." }) { StatusCode = 500 };
            }
        }
        #endregion
    }

    public class AttributeRequest
    {
        public string Type { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
    }
}