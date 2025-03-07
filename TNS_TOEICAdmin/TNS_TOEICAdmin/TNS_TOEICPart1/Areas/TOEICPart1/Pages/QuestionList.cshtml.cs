using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Data;
using System.Data.SqlClient;
using static System.Net.Mime.MediaTypeNames;
using System.Linq;
using TNS_TOEICPart1.Areas.TOEICPart1.Models;

namespace TNS_TOEICPart1.Areas.TOEICPart1.Pages
{
    [IgnoreAntiforgeryToken]
    public class QuestionListModel : PageModel
    {
        #region [ Security ]
        public TNS.Auth.UserLogin_Info UserLogin;

        private void CheckAuth()
        {
            UserLogin = new TNS.Auth.UserLogin_Info(User);
            UserLogin.GetRole("TOEIC_Part1");
            // For Testing
            UserLogin.Role.IsRead = true;
            UserLogin.Role.IsCreate = true;
            UserLogin.Role.IsUpdate = true;
            UserLogin.Role.IsDelete = true;
        }
        #endregion

        public IActionResult OnGet()
        {
            CheckAuth();
            if (UserLogin.Role.IsRead)
            {
                return Page();
            }
            else
            {
                return LocalRedirect("~/Warning?id=403");
            }
        }

        public IActionResult OnPostLoadData([FromBody] ItemRequest request)
        {
            CheckAuth();
            JsonResult zResult = new JsonResult("");
            if (UserLogin.Role.IsRead)
            {
                DateTime zFromDate, zToDate;
                if (request.FromDate.Trim().Length > 0 && request.ToDate.Trim().Length > 0)
                {
                    zFromDate = DateTime.Parse(request.FromDate);
                    zToDate = DateTime.Parse(request.ToDate);
                    zResult = QuestionListDataAccess.GetList(request.Search, request.Level, zFromDate, zToDate, request.PageSize, request.PageNumber, request.StatusFilter);
                }
                else
                {
                    zResult = QuestionListDataAccess.GetList(request.Search, request.Level, request.PageSize, request.PageNumber, request.StatusFilter);
                }
            }
            else
            {
                zResult = new JsonResult(new { Status = "ERROR", Result = "ACCESS DENIED" });
            }
            return zResult;
        }

     
    }
}