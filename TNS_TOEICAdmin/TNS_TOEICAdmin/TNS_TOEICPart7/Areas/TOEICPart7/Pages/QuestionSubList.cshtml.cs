using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TNS_TOEICPart7.Areas.TOEICPart7.Models;

namespace TNS_TOEICPart7.Areas.TOEICPart7.Pages
{
    [IgnoreAntiforgeryToken]
    public class QuestionSubListModel : PageModel
    {
        #region [ Security ]
        public TNS.Auth.UserLogin_Info UserLogin;
        private void CheckAuth()
        {
            UserLogin = new TNS.Auth.UserLogin_Info(User);
            UserLogin.GetRole("TOEIC_Part7");
            //For Testing
            UserLogin.Role.IsRead = true;
            UserLogin.Role.IsCreate = true;
            UserLogin.Role.IsUpdate = true;
            UserLogin.Role.IsDelete = true;
        }
        #endregion

        public string QuestionKey;
        public IActionResult OnGet(string Key)
        {
            CheckAuth();
            if (UserLogin.Role.IsRead)
            {
                QuestionKey = Key;
                return Page();
            }
            else
                return LocalRedirect("~/Warning?id=403");
        }
        public IActionResult OnPostLoadData([FromBody] ItemRequest request)
        {
            CheckAuth();
            JsonResult zResult = new JsonResult("");
            if (UserLogin.Role.IsRead)
            {
                zResult = QuestionSubListAccessData.GetList(request.QuestionKey);
            }
            else
            {
                zResult = new JsonResult(new { Status = "ERROR", Result = "ACCESS DENIED" });
            }
            return zResult;
        }

        public class ItemRequest
        {
            public string QuestionKey { get; set; }
        }

    }
}
