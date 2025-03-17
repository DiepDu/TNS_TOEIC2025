using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TNS_TOEICPart4.Areas.TOEICPart4.Models;

namespace TNS_TOEICPart4.Areas.TOEICPart4.Pages
{
    [IgnoreAntiforgeryToken]
    public class AnswerListModel : PageModel
    {
        #region [Security]
        public TNS.Auth.UserLogin_Info UserLogin;

        private void CheckAuth()
        {
            UserLogin = new TNS.Auth.UserLogin_Info(User);
            UserLogin.GetRole("TOEIC_Part4");
            // For Testing
            UserLogin.Role.IsRead = true;
            UserLogin.Role.IsCreate = true;
            UserLogin.Role.IsUpdate = true;
            UserLogin.Role.IsDelete = true;
        }
        #endregion

        public string QuestionKey { get; set; }

        public IActionResult OnGet(string key)
        {

            CheckAuth();
            if (UserLogin.Role.IsRead)
            {
                QuestionKey = key;
                if (string.IsNullOrEmpty(QuestionKey))
                    return LocalRedirect("~/TOEICPart4/QuestionList");
                return Page();
            }
            else
            {
                return LocalRedirect("~/Warning?id=403");
            }
        }

        public IActionResult OnPostLoadData([FromBody] ItemRequestKey request)
        {
            CheckAuth();
            JsonResult zResult = new JsonResult("");
            if (UserLogin.Role.IsRead)
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
