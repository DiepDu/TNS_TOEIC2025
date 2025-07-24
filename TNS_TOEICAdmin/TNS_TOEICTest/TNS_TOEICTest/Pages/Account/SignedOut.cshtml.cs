using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using TNS_TOEICTest.Hubs;
using System.Security.Claims; // Đảm bảo namespace của ChatHub đúng

namespace TNS_TOEICTest.Pages.Account
{
    public class SignedOutModel : PageModel
    {
        private readonly IHubContext<ChatHub> _hubContext;

        public SignedOutModel(IHubContext<ChatHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task<RedirectToPageResult> OnGetAsync()
        {
            // Lấy connectionId của user hiện tại (nếu có)
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                // Gửi tín hiệu đến client để ngắt kết nối
                await _hubContext.Clients.User(userId).SendAsync("DisconnectSignal");
            }

            // Đăng xuất khỏi authentication
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Xóa session (nếu sử dụng)
            // Ví dụ: HttpContext.Session.Clear();

            return RedirectToPage("/Index");
        }
    }
}