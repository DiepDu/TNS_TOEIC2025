using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TNS_TOEICPart3.Areas.TOEICPart3.Models;

namespace TNS_TOEICPart3.Areas.TOEICPart3.Pages
{
    [IgnoreAntiforgeryToken]
    public class AnswerListModel : PageModel
    {
        #region [Security]
        public TNS_Auth.UserLogin_Info UserLogin;
        public bool IsFullAdmin { get; private set; }
        private void CheckAuth()
        {
            UserLogin = new TNS_Auth.UserLogin_Info(User);

            // Kiểm tra quyền Full trước
            var fullRole = new TNS_Auth.Role_Info(UserLogin.UserKey, "Full");
            if (fullRole.GetCode() == "200") // Có quyền Full trong DB
            {
                IsFullAdmin = true;
                UserLogin.GetRole("Questions"); // Vẫn lấy nhưng không ảnh hưởng
            }
            else
            {
                IsFullAdmin = false;
                UserLogin.GetRole("Questions"); // Lấy quyền Questions
            }

            // Đảm bảo Role được khởi tạo
            if (UserLogin.Role == null)
            {
                UserLogin.GetRole("Questions");
            }
        }
        #endregion

        public string QuestionKey { get; set; }

        public IActionResult OnGet(string key)
        {

            CheckAuth();
            if (UserLogin.Role.IsRead || IsFullAdmin)
            {
                QuestionKey = key;
                if (string.IsNullOrEmpty(QuestionKey))
                    return LocalRedirect("~/TOEICPart3/QuestionList");
                return Page();
            }
            else
            {
                TempData["Error"] = "ACCESS DENIED!!!";
                return Page();
            }
        }

        public IActionResult OnPostLoadData([FromBody] ItemRequestKey request)
        {
            CheckAuth();
            JsonResult zResult = new JsonResult("");
            if (UserLogin.Role.IsRead || IsFullAdmin)
            {
                zResult = AnswerListDataAccess.GetList(request.QuestionKey);
            }
            else
            {
                zResult = new JsonResult(new { Status = "ERROR", Result = "ACCESS DENIED" });
            }
            return zResult;
        }
    }

    public class ItemRequestKey
    {
        public string QuestionKey { get; set; }
    }
}
