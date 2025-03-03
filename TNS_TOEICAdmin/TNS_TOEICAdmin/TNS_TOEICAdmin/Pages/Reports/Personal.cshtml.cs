using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TNS_TOEICAdmin.Pages.Reports
{
    public class PersonalModel : PageModel
    {
        private TNS.Auth.UserLogin_Info _UserLogin;
        public IActionResult OnGet()
        {
            CheckAuth();
            Models.Personal zPersonal = new Models.Personal(_UserLogin.UserKey, _UserLogin.Employee.Key);

            return new JsonResult(zPersonal);
        }
        private void CheckAuth()
        {

            _UserLogin = new TNS.Auth.UserLogin_Info(User);
        }
    }
}
