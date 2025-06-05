using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TNS_TOEICAdmin.Hubs;
using TNS_TOEICAdmin.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TNS_TOEICAdmin.Controllers
{
    [Route("[controller]")]
    [IgnoreAntiforgeryToken]
    public class NotificationHandler : Controller
    {
        private readonly IHttpContextAccessor _httpContextAccessor; private readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase; private readonly IHubContext _hubContext;

        public NotificationHandler(IHttpContextAccessor httpContextAccessor, IHubContext<NotificationHub> hubContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _hubContext = (IHubContext?)hubContext;
        }

        [HttpPost("ProcessNotification")]
        public async Task<IActionResult> ProcessNotification([FromBody] NotificationContentDto contentDto)
        {
            var userCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var userLogin = new TNS_Auth.UserLogin_Info(userCookie ?? new ClaimsPrincipal());
            var userKey = userLogin.UserKey;

            if (string.IsNullOrEmpty(userKey))
            {
                return Unauthorized(new { success = false, message = "UserKey not found." });
            }

            await _hubContext.Clients.All.SendAsync("ReceiveNotification", contentDto.Content);
            return Ok(new { success = true });
        }

        [HttpGet("GetNotifications")]
        public async Task<IActionResult> GetNotifications([FromQuery] int skip = 0, [FromQuery] int take = 30)
        {
            var userCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var userLogin = new TNS_Auth.UserLogin_Info(userCookie ?? new ClaimsPrincipal());
            var userKey = userLogin.UserKey;

            if (string.IsNullOrEmpty(userKey))
            {
                return Unauthorized(new { success = false, message = "UserKey not found." });
            }

            var notifications = await NotificationAccessData.GetNotificationsAsync(userKey, "Admin", skip, take);
            var totalCount = await NotificationAccessData.GetTotalCountAsync(userKey, "Admin");

            return Ok(new { notifications, totalCount });
        }

        [HttpGet("GetUnreadCount")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var userLogin = new TNS_Auth.UserLogin_Info(userCookie ?? new ClaimsPrincipal());
            var userKey = userLogin.UserKey;

            if (string.IsNullOrEmpty(userKey))
            {
                return Unauthorized(new { success = false, message = "UserKey not found." });
            }

            var count = await NotificationAccessData.GetUnreadCountAsync(userKey, "Admin");
            return Ok(new { count });
        }

        [HttpPost("MarkAsRead")]
        public async Task<IActionResult> MarkAsRead([FromBody] Guid[] notificationIds)
        {
            var userCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var userLogin = new TNS_Auth.UserLogin_Info(userCookie ?? new ClaimsPrincipal());
            var userKey = userLogin.UserKey;

            if (string.IsNullOrEmpty(userKey))
            {
                return Unauthorized(new { success = false, message = "UserKey not found." });
            }

            await NotificationAccessData.MarkAsReadAsync(userKey, "Admin", notificationIds);
            return Ok(new { success = true });
        }

        [HttpGet("GetFeedbacks")]
        public async Task<IActionResult> GetFeedbacks([FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            var userCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var userLogin = new TNS_Auth.UserLogin_Info(userCookie ?? new ClaimsPrincipal());
            var userKey = userLogin.UserKey;

            if (string.IsNullOrEmpty(userKey))
            {
                return Unauthorized(new { success = false, message = "UserKey not found." });
            }

            var feedbacks = await NotificationAccessData.GetFeedbacksAsync(skip, take);
            var totalCount = await NotificationAccessData.GetFeedbackTotalCountAsync();

            return Json(new { feedbacks, totalCount, count = feedbacks.Count });
        }

        [HttpPost("MarkFeedbackAsResolved")]
        public async Task<IActionResult> MarkFeedbackAsResolved([FromBody] Guid feedbackId)
        {
            var userCookie = _httpContextAccessor.HttpContext?.User as ClaimsPrincipal;
            var userLogin = new TNS_Auth.UserLogin_Info(userCookie ?? new ClaimsPrincipal());
            var userKey = userLogin.UserKey;

            if (string.IsNullOrEmpty(userKey))
            {
                return Unauthorized(new { success = false, message = "UserKey not found." });
            }

            var success = await NotificationAccessData.MarkFeedbackAsResolvedAsync(feedbackId);
            if (success)
            {
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"Feedback {feedbackId} marked as resolved.");
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Feedback not found." });
        }
    }

    public class NotificationContentDto
    {
        public string Content { get; set; }
    }

}