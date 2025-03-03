using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TNS_TOEICAdmin.Pages.Account
{
    public class SignedOutModel : PageModel
    {
        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity.IsAuthenticated)
            {            
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            }

            return Page();
        }
    }
}
