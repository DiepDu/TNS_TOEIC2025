using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TNS_TOEICPart1.Areas.TOEICPart1.Services
{
    /// <summary>
    /// HTTP Client for Full IRT Python Service (3PL + EM Algorithm)
    /// </summary>
    public class IrtServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _serviceUrl;

        public IrtServiceClient(string serviceUrl = "http://localhost:5001")
        {
            _serviceUrl = serviceUrl;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5) // IRT analysis can take time
            };
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_serviceUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Analyze questions using Full IRT (3PL + EM Algorithm)
        /// </summary>
        public async Task<IrtAnalysisResult> AnalyzeAsync(List<IrtResponse> responses)
        {
            try
            {
                var requestData = new { data = responses };
                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_serviceUrl}/analyze", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();

                    // ✅ TRY TO PARSE PYTHON ERROR MESSAGE
                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(errorContent);
                        if (jsonDoc.RootElement.TryGetProperty("message", out var messageElement))
                        {
                            string pythonMessage = messageElement.GetString();
                            throw new Exception($"❌ Python IRT Service Error:\n\n{pythonMessage}\n\nHTTP Status: {response.StatusCode}");
                        }
                    }
                    catch (JsonException)
                    {
                        // If can't parse JSON, use raw content
                    }

                    throw new Exception($"❌ IRT Service Error ({response.StatusCode}):\n\n{errorContent}");
                }

                var resultJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<IrtAnalysisResult>(resultJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result;
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"❌ Cannot connect to IRT service at {_serviceUrl}\n\n" +
                                  $"Chi tiết: {ex.Message}\n\n" +
                                  $"Vui lòng:\n" +
                                  $"1. Kiểm tra Python service đã chạy: http://localhost:5001/health\n" +
                                  $"2. Kiểm tra firewall/antivirus không block port 5001\n" +
                                  $"3. Restart Python service nếu cần", ex);
            }
            catch (TaskCanceledException)
            {
                throw new Exception($"❌ IRT analysis timeout (>5 minutes)\n\n" +
                                  $"Sent {responses.Count} responses to analyze.\n\n" +
                                  $"Có thể:\n" +
                                  $"• Data quá lớn\n" +
                                  $"• Python service bị treo\n" +
                                  $"• Training không converge\n\n" +
                                  $"Giải pháp: Restart Python service và thử lại.");
            }
            catch (Exception ex) when (ex.Message.Contains("Python IRT Service Error"))
            {
                // Re-throw Python errors as-is
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"❌ Failed to call IRT service:\n\n{ex.Message}", ex);
            }
        }
    }

    #region DTOs

    /// <summary>
    /// Single response record for IRT analysis
    /// </summary>
    public class IrtResponse
    {
        public string memberKey { get; set; }
        public string questionKey { get; set; }
        public int isCorrect { get; set; }
    }

    /// <summary>
    /// Full IRT Analysis Result
    /// </summary>
    public class IrtAnalysisResult
    {
        public string Status { get; set; }
        public Dictionary<string, IrtQuestionParams> QuestionParams { get; set; }
        public Dictionary<string, double> MemberAbilities { get; set; }
        public IrtMetadata Metadata { get; set; }
    }

    /// <summary>
    /// IRT Parameters for a question (3PL Model)
    /// </summary>
    public class IrtQuestionParams
    {
        public double Difficulty { get; set; }          // b parameter (-3 to +3)
        public double Discrimination { get; set; }      // a parameter (0 to 2.5)
        public double Guessing { get; set; }            // c parameter (0 to 0.5)
        public string Quality { get; set; }             // "Tốt", "Cần xem lại", "Kém"
        public string ConfidenceLevel { get; set; }     // "High", "Medium", "Low"
        public int AttemptCount { get; set; }
        public bool Converged { get; set; }
    }

    /// <summary>
    /// Metadata from IRT analysis
    /// </summary>
    public class IrtMetadata
    {
        public int TotalQuestions { get; set; }
        public int TotalMembers { get; set; }
        public int TotalResponses { get; set; }
        public string ModelType { get; set; }
        public int Iterations { get; set; }
        public string Timestamp { get; set; }
    }

    #endregion
}