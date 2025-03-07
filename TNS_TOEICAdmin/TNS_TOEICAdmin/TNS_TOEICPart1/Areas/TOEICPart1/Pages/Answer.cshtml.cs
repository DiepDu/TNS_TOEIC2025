using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TNS_TOEICPart1.Areas.TOEICPart1.Models;
using System;

namespace TNS_TOEICPart1.Areas.TOEICPart1.Pages
{
    [IgnoreAntiforgeryToken]
    public class AnswerModel : PageModel
    {
        #region [Security]
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

        public string AnswerKey { get; set; }
        public string QuestionKey { get; set; }

        public IActionResult OnGet(string Key, string QuestionKey)
        {
            CheckAuth();
            if (UserLogin.Role.IsRead)
            {
                this.AnswerKey = Key;
                // Kiểm tra định dạng GUID cho QuestionKey
                if (!string.IsNullOrEmpty(QuestionKey) && !Guid.TryParse(QuestionKey, out _))
                {
                    return RedirectToPage("/QuestionList", new { error = "InvalidQuestionKey" });
                }
                this.QuestionKey = QuestionKey;
                if (string.IsNullOrEmpty(this.QuestionKey))
                    return RedirectToPage("/QuestionList"); // Redirect nếu không có QuestionKey
                return Page();
            }
            else
            {
                return LocalRedirect("~/Warning?id=403");
            }
        }

        #region [Record CRUD]
        public IActionResult OnPostRecordRead([FromBody] ItemRequestAnswer request)
        {
            CheckAuth();
            JsonResult zResult;
            if (UserLogin.Role.IsRead)
            {
                AnswerDataAccess.Part1_Answer_Info zRecord;
                if (string.IsNullOrEmpty(request.AnswerKey) || request.AnswerKey.Length != 36)
                    zRecord = new AnswerDataAccess.Part1_Answer_Info();
                else
                    zRecord = new AnswerDataAccess.Part1_Answer_Info(request.AnswerKey);
                zResult = new JsonResult(zRecord);
            }
            else
            {
                zResult = new JsonResult(new { Status = "ERROR", Result = "ACCESS DENIED" });
            }
            return zResult;
        }

        public IActionResult OnPostRecordCreate([FromBody] AnswerDataAccess.Part1_Answer_Info request)
        {
            CheckAuth();
            JsonResult zResult;
            if (UserLogin.Role.IsCreate)
            {
                AnswerDataAccess.Part1_Answer_Info zRecord = request;
                if (string.IsNullOrEmpty(zRecord.QuestionKey) || !Guid.TryParse(zRecord.QuestionKey, out _))
                {
                    zResult = new JsonResult(new { status = "ERROR", message = "Invalid QuestionKey format" });
                    return zResult;
                }
                zRecord.CreatedBy = UserLogin.Employee.Key;
                zRecord.CreatedName = UserLogin.Employee.Name;
                zRecord.Create();
                zResult = new JsonResult(new { status = zRecord.Status, message = zRecord.Message });
            }
            else
            {
                zResult = new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });
            }
            return zResult;
        }

        public IActionResult OnPostRecordUpdate([FromBody] AnswerDataAccess.Part1_Answer_Info request)
        {
            CheckAuth();
            JsonResult zResult;
            if (UserLogin.Role.IsUpdate)
            {
                AnswerDataAccess.Part1_Answer_Info zRecord = request;
                if (string.IsNullOrEmpty(zRecord.QuestionKey) || !Guid.TryParse(zRecord.QuestionKey, out _))
                {
                    zResult = new JsonResult(new { status = "ERROR", message = "Invalid QuestionKey format" });
                    return zResult;
                }
                zRecord.ModifiedBy = UserLogin.Employee.Key;
                zRecord.ModifiedName = UserLogin.Employee.Name;
                zRecord.Update();
                zResult = new JsonResult(new { status = zRecord.Status, message = zRecord.Message });
            }
            else
            {
                zResult = new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });
            }
            return zResult;
        }

        public IActionResult OnPostRecordDel([FromBody] ItemRequestAnswer request)
        {
            CheckAuth();
            JsonResult zResult;
            if (UserLogin.Role.IsDelete)
            {
                AnswerDataAccess.Part1_Answer_Info zRecord = new AnswerDataAccess.Part1_Answer_Info();
                zRecord.AnswerKey = request.AnswerKey;
                zRecord.Delete();
                zResult = new JsonResult(new { status = zRecord.Status, message = zRecord.Message });
            }
            else
            {
                zResult = new JsonResult(new { Status = "ERROR", Message = "ACCESS DENIED" });
            }
            return zResult;
        }
        #endregion
    }

    public class ItemRequestAnswer
    {
        public string AnswerKey { get; set; }
    }
}