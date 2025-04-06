using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using TNS_TOEICAdmin.Extensions;

namespace TNS_TOEICAdmin.Pages.Account
{
    [IgnoreAntiforgeryToken]
    public class LoginModel : PageModel
    {
        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public string ReturnUrl { get; private set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Username is required.")]
            public string UserName { get; set; }

            [Required(ErrorMessage = "Password is required.")]
            [DataType(DataType.Password)]
            public string Password { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            ReturnUrl = returnUrl ?? "/Admin/Index"; // Mặc định chuyển hướng về Admin nếu không có returnUrl
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? "/Admin/Index";

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var zUser = await AuthenticateUser(Input.UserName, Input.Password);

            if (!zUser.Successed)
            {
                ModelState.AddModelError(string.Empty, zUser.Message);
                return Page();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, zUser.UserKey),
                new Claim("UserName", zUser.UserName),
                new Claim("EmployeeKey", zUser.EmployeeKey)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            return LocalRedirect(Url.GetLocalUrl(ReturnUrl));
        }

        private async Task<TNS_Auth.CheckUserLogin> AuthenticateUser(string userName, string password)
        {
            
            await Task.Delay(500);
            return new TNS_Auth.CheckUserLogin(userName, password);
        }
    }
}