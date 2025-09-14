using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

namespace TNS_TOEICAdmin.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GetAdminKeyController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetCurrentUserKey()
        {
            // Lấy ClaimsPrincipal trực tiếp từ HttpContext của Controller
            var userCookie = HttpContext.User;

            // Kiểm tra xem user đã đăng nhập chưa
            if (userCookie == null || !userCookie.Identity.IsAuthenticated)
            {
                return Unauthorized(new { message = "Admin not authenticated." });
            }

            // Trích xuất UserKey
            var userKey = userCookie.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userKey))
            {
                return NotFound(new { message = "UserKey not found in claims." });
            }

            // Trả về JSON với key viết thường để JavaScript dễ bắt
            return Ok(new { userKey = userKey });
        }
    }
}