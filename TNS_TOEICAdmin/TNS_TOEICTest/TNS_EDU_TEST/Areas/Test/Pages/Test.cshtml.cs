using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TNS_EDU_TEST.Areas.Test.Pages
{
    [Authorize]
    public class TestModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
