using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using TNS_EDU_STUDY.Areas.Study.Models.ContentProcessor;
using TNS_EDU_STUDY.Areas.Study.Services;

namespace TNS_EDU_STUDY.Areas.Study.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContentProcessorController : ControllerBase
    {
        private readonly IContentProcessorService _service;
        private readonly ILogger<ContentProcessorController> _logger;

        public ContentProcessorController(IContentProcessorService service, ILogger<ContentProcessorController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("health")]
        public async Task<IActionResult> CheckHealth()
        {
            var isHealthy = await _service.CheckHealthAsync();
            return Ok(new { Success = isHealthy, Message = isHealthy ? "Service is running" : "Service is not available" });
        }

        [HttpGet("limits")]
        public async Task<IActionResult> GetLimits()
        {
            var limits = await _service.GetLimitsAsync();
            return Ok(new { Success = limits != null, Data = limits });
        }

        [HttpPost("process/text")]
        public async Task<IActionResult> ProcessText([FromBody] TextProcessRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Content))
            {
                return BadRequest(new { Success = false, Message = "Content is required" });
            }

            var result = await _service.ProcessTextAsync(request.Content);
            return Ok(new { Success = result?.Success ?? false, Data = result, Message = result?.Success == true ? "Processed successfully" : "Processing failed" });
        }

        [HttpPost("process/file")]
        public async Task<IActionResult> ProcessFile(IFormFile file, [FromForm] string inputType)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { Success = false, Message = "File is required" });
            }

            if (string.IsNullOrWhiteSpace(inputType))
            {
                return BadRequest(new { Success = false, Message = "Input type is required" });
            }

            using var stream = file.OpenReadStream();
            var result = await _service.ProcessFileAsync(stream, file.FileName, inputType);
            return Ok(new { Success = result?.Success ?? false, Data = result, Message = result?.Success == true ? "Processed successfully" : "Processing failed" });
        }

        [HttpPost("process/youtube")]
        public async Task<IActionResult> ProcessYouTube([FromBody] YouTubeProcessRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Url))
            {
                return BadRequest(new { Success = false, Message = "URL is required" });
            }

            var result = await _service.ProcessYouTubeAsync(request.Url);
            return Ok(new { Success = result?.Success ?? false, Data = result, Message = result?.Success == true ? "Processed successfully" : "Processing failed" });
        }
    }

    public class TextProcessRequest
    {
        public string? Content { get; set; }
    }

    public class YouTubeProcessRequest
    {
        public string? Url { get; set; }
    }
}