using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TNS_TOEICAdmin.Hubs;
using TNS_TOEICAdmin.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;

namespace TNS_TOEICAdmin.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotifyController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public NotifyController(IConfiguration configuration, IHubContext<NotificationHub> hubContext)
        {
            _configuration = configuration;
            _hubContext = hubContext;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] FeedbackNotificationDto dto)
        {
            var apiKey = Request.Headers["ApiKey"].FirstOrDefault();
            var expectedApiKey = _configuration["ApiSettings:ApiKey"];
            if (string.IsNullOrEmpty(apiKey) || apiKey != expectedApiKey)
            {
                return Unauthorized(new { success = false, message = "Key không hợp lệ." });
            }

            if (dto == null || string.IsNullOrEmpty(dto.Summary) || string.IsNullOrEmpty(dto.Member))
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            dto.FeedbackKey = Guid.NewGuid();
            var content = $"Feedback from {dto.Member}: {dto.Summary}";
            await NotificationAccessData.InsertNotificationAsync(
                "Feedback", content, dto.FeedbackKey, Guid.Empty, "Admin", "Admin");

            // Gửi SignalR đến tất cả client
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", content);

            return Ok(new { success = true, message = "Thông báo đã được gửi." });
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetNotifications([FromQuery] int skip = 0, [FromQuery] int take = 30)
        {
            return Ok(new { notifications = new List<Notification>(), totalCount = 0 });
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            return Ok(new { count = 0 });
        }

        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            return Ok(new { success = true });
        }
    }
}