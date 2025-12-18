using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TNS_EDU_STUDY.Areas.Study.Models.ContentProcessor;

namespace TNS_EDU_STUDY.Areas.Study.Services
{
    public class ContentProcessorService : IContentProcessorService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ContentProcessorService> _logger;
        private readonly string _baseUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        public ContentProcessorService(HttpClient httpClient, ILogger<ContentProcessorService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["ContentProcessor:BaseUrl"] ?? "http://localhost:5004";

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Content Processor service health check failed");
                return false;
            }
        }

        public async Task<InputLimits?> GetLimitsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/limits");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<InputLimits>(json, _jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get limits");
                return null;
            }
        }

        public async Task<ProcessResult?> ProcessTextAsync(string content)
        {
            try
            {
                var request = new { content };
                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/process/text", httpContent);
                var responseJson = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Process text response: {Response}", responseJson);

                return JsonSerializer.Deserialize<ProcessResult>(responseJson, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing text");
                return new ProcessResult
                {
                    Success = false,
                    Errors = new List<ErrorInfo> { new() { Code = "PROCESS_ERROR", Message = ex.Message } }
                };
            }
        }

        public async Task<ProcessResult?> ProcessFileAsync(Stream fileStream, string fileName, string inputType)
        {
            try
            {
                using var formData = new MultipartFormDataContent();

                // Copy stream to byte array
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();

                // Add file
                var fileContent = new ByteArrayContent(fileBytes);
                var contentType = GetContentType(fileName);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                formData.Add(fileContent, "file", fileName);

                // Add input_type as form field (snake_case to match Python)
                formData.Add(new StringContent(inputType.ToLower()), "input_type");

                _logger.LogInformation("Sending file to Python: FileName={FileName}, InputType={InputType}, Size={Size}bytes",
                    fileName, inputType, fileBytes.Length);

                // Call the correct endpoint: /process/file
                var response = await _httpClient.PostAsync($"{_baseUrl}/process/file", formData);
                var responseJson = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Process {InputType} response status: {Status}, body: {Response}",
                    inputType, response.StatusCode, responseJson);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Python service returned error: {Status} - {Body}", response.StatusCode, responseJson);
                    return new ProcessResult
                    {
                        Success = false,
                        Errors = new List<ErrorInfo> { new() { Code = "API_ERROR", Message = $"Python service error: {response.StatusCode}" } }
                    };
                }

                return JsonSerializer.Deserialize<ProcessResult>(responseJson, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file {FileName}", fileName);
                return new ProcessResult
                {
                    Success = false,
                    Errors = new List<ErrorInfo> { new() { Code = "PROCESS_ERROR", Message = ex.Message } }
                };
            }
        }

        public async Task<ProcessResult?> ProcessYouTubeAsync(string url)
        {
            try
            {
                var request = new { url };
                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/process/youtube", httpContent);
                var responseJson = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Process YouTube response: {Response}", responseJson);

                return JsonSerializer.Deserialize<ProcessResult>(responseJson, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing YouTube URL");
                return new ProcessResult
                {
                    Success = false,
                    Errors = new List<ErrorInfo> { new() { Code = "PROCESS_ERROR", Message = ex.Message } }
                };
            }
        }

        private static string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                // Images
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".bmp" => "image/bmp",
                ".tiff" or ".tif" => "image/tiff",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                // Documents
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                // Audio
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".m4a" => "audio/mp4",
                ".ogg" => "audio/ogg",
                ".flac" => "audio/flac",
                // Video
                ".mp4" => "video/mp4",
                ".mov" => "video/quicktime",
                ".avi" => "video/x-msvideo",
                ".mkv" => "video/x-matroska",
                ".webm" => "video/webm",
                _ => "application/octet-stream"
            };
        }
    }
}