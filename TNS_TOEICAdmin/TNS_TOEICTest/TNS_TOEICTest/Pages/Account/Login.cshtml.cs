using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using TNS.Member;
using TNS_TOEICTest.Extensions;

namespace TNS_TOEICTest.Pages.Account
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public string ReturnUrl { get; private set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Member ID is required.")]
            public string MemberId { get; set; }

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
            ReturnUrl = returnUrl ?? "/Index"; // Mặc định chuyển hướng về Index của TNS_TOEICTest
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? "/Index";

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var zMember = await AuthenticateMember(Input.MemberId, Input.Password);

            if (!zMember.Successed)
            {
                ModelState.AddModelError(string.Empty, zMember.Message);
                return Page();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, zMember.MemberKey),
                new Claim("MemberName", zMember.MemberName),
                new Claim("Avatar", zMember.Avatar)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            return LocalRedirect(Url.GetLocalUrl(ReturnUrl));
        }

        private async Task<CheckMemberLogin> AuthenticateMember(string memberId, string password)
        {
            await Task.Delay(500); // Giữ nguyên độ trễ giả lập
            return new CheckMemberLogin(memberId, password);
        }
    }
}