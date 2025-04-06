using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data;
using System;
using TNS_TOEICPart3.Areas.TOEICPart3.Models;

namespace TNS_TOEICPart3.Areas.TOEICPart3.Pages
{
    [IgnoreAntiforgeryToken]
    public class QuestionSubListModel : PageModel
    {
        #region [ Security ]
        public TNS_Auth.UserLogin_Info UserLogin;
        private void CheckAuth()
        {
            UserLogin = new TNS_Auth.UserLogin_Info(User);
            UserLogin.GetRole("TOEIC_Part3");
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
