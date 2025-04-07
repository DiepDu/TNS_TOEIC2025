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

        public string QuestionKey;
        public IActionResult OnGet(string Key)
        {
            CheckAuth();
            if (UserLogin.Role.IsRead || IsFullAdmin)
            {
                QuestionKey = Key;
                return Page();
            }
            else
              {
                TempData["Error"] = "ACCESS DENIED!!!";
                return Page();
            }
        }
        public IActionResult OnPostLoadData([FromBody] ItemRequest request)
        {
            CheckAuth();
            JsonResult zResult = new JsonResult("");
            if (UserLogin.Role.IsRead || IsFullAdmin)
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
