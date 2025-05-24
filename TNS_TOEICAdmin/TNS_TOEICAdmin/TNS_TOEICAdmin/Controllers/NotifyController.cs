using Microsoft.AspNetCore.Mvc;

using TNS_TOEICAdmin.Models;

namespace TNS_TOEICAdmin.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotifyController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public NotifyController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] FeedbackNotificationDto dto)
        {
            // Kiểm tra ApiKey
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

            // Tạo FeedbackKey mới
            dto.FeedbackKey = Guid.NewGuid();

            var content = $"Feedback from {dto.Member}: {dto.Summary}";
            await NotificationAccessData.InsertNotificationAsync(
                "Feedback", content, dto.FeedbackKey, Guid.Empty, "Admin", "Admin");

            return Ok(new { success = true, message = "Thông báo đã được gửi." });
        }
    }
}