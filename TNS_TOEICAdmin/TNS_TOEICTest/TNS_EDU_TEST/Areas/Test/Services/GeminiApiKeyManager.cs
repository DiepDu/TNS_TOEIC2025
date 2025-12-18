using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace TNS_EDU_TEST.Services
{
    /// <summary>
    /// API Key Rotation Manager for Gemini API
    /// Manages multiple API keys with daily quota tracking
    /// </summary>
    public class GeminiApiKeyManager
    {
        private readonly string _connectionString;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiApiKeyManager> _logger;

        public GeminiApiKeyManager(
            IConfiguration configuration,
            ILogger<GeminiApiKeyManager> logger)
        {
            _configuration = configuration;
            _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase; // ✅ Dùng chung connection string
            _logger = logger;
        }

        /// <summary>
        /// Lấy API key khả dụng với rotation logic
        /// </summary>
        public async Task<ApiKeyUsageResult> GetAvailableApiKeyAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var today = DateTime.Today;

                // ✅ Query với UPDLOCK để tránh race condition
                var query = @"
                    SELECT TOP 1 
                        KeyID, KeyName, Email, DailyLimit, LastUsedDate
                    FROM APIKey WITH (UPDLOCK, ROWLOCK)
                    WHERE 1=1
                    ORDER BY 
                        -- Ưu tiên key chưa dùng hôm nay
                        CASE WHEN LastUsedDate IS NULL OR LastUsedDate < @Today 
                             THEN 0 ELSE 1 END,
                        -- Sau đó ưu tiên key có ít usage nhất
                        (SELECT COUNT(*) FROM APIUsageLog 
                         WHERE KeyID_FK = APIKey.KeyID 
                         AND CAST(UsedAt AS DATE) = @Today) ASC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Today", today);

                GeminiApiKey apiKey = null;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        _logger.LogWarning("⚠️ No API keys found in database");
                        return new ApiKeyUsageResult
                        {
                            Success = false,
                            ErrorMessage = "No API keys configured in system"
                        };
                    }

                    apiKey = new GeminiApiKey
                    {
                        KeyID = reader.GetInt32(0),
                        KeyName = reader.GetString(1),
                        Email = reader.GetString(2),
                        DailyLimit = reader.GetInt32(3),
                        LastUsedDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                    };
                }

                // ✅ Lấy usage count hôm nay
                apiKey.UsageCount = await GetTodayUsageCountAsync(connection, apiKey.KeyID, today);

                // ✅ Kiểm tra quota
                if (apiKey.UsageCount >= apiKey.DailyLimit)
                {
                    _logger.LogWarning($"⚠️ Key {apiKey.KeyName} exhausted ({apiKey.UsageCount}/{apiKey.DailyLimit})");
                    return await GetAvailableApiKeyAsync(); // Recursive - tìm key khác
                }

                // ✅ Lấy actual API key từ User Secrets
                apiKey.ActualApiKey = _configuration[apiKey.KeyName];

                if (string.IsNullOrEmpty(apiKey.ActualApiKey))
                {
                    _logger.LogError($"❌ Key '{apiKey.KeyName}' not found in User Secrets");
                    return new ApiKeyUsageResult
                    {
                        Success = false,
                        ErrorMessage = $"Configuration error: {apiKey.KeyName} not in secrets.json"
                    };
                }

                // ✅ Tính remaining quota
                apiKey.RemainingQuota = apiKey.DailyLimit - apiKey.UsageCount;

                _logger.LogInformation(
                    "✅ [LearningAnalysis] Selected {KeyName} - Current usage: {Usage}/{Limit} - Remaining: {Remaining}",
                    apiKey.KeyName,
                    apiKey.UsageCount,
                    apiKey.DailyLimit,
                    apiKey.RemainingQuota
                );

                return new ApiKeyUsageResult
                {
                    Success = true,
                    ApiKey = apiKey
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting API key");
                return new ApiKeyUsageResult
                {
                    Success = false,
                    ErrorMessage = $"Database error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Ghi log mỗi lần gọi API (kể cả retry và fail)
        /// </summary>
        public async Task LogApiCallAttemptAsync(int keyId, bool isSuccess)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var today = DateTime.Today;

                // Cập nhật LastUsedDate
                var updateQuery = @"
                    UPDATE APIKey 
                    SET LastUsedDate = @Today 
                    WHERE KeyID = @KeyID";

                using var updateCmd = new SqlCommand(updateQuery, connection);
                updateCmd.Parameters.AddWithValue("@Today", today);
                updateCmd.Parameters.AddWithValue("@KeyID", keyId);
                await updateCmd.ExecuteNonQueryAsync();

                // Ghi log usage
                var logQuery = @"
                    INSERT INTO APIUsageLog (KeyID_FK, UsedAt, IsSuccess) 
                    VALUES (@KeyID, GETDATE(), @IsSuccess)";

                using var logCmd = new SqlCommand(logQuery, connection);
                logCmd.Parameters.AddWithValue("@KeyID", keyId);
                logCmd.Parameters.AddWithValue("@IsSuccess", isSuccess);
                await logCmd.ExecuteNonQueryAsync();

                _logger.LogInformation(
                    "✅ [LearningAnalysis] Logged API call for KeyID {KeyID} - Success: {IsSuccess}",
                    keyId, isSuccess
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to log API call for KeyID {KeyID}", keyId);
                // Don't throw - logging failure shouldn't break the flow
            }
        }

        /// <summary>
        /// Đếm số lần đã dùng hôm nay
        /// </summary>
        private async Task<int> GetTodayUsageCountAsync(SqlConnection connection, int keyId, DateTime today)
        {
            var countQuery = @"
                SELECT COUNT(*) 
                FROM APIUsageLog 
                WHERE KeyID_FK = @KeyID 
                AND CAST(UsedAt AS DATE) = @Today";

            using var countCmd = new SqlCommand(countQuery, connection);
            countCmd.Parameters.AddWithValue("@KeyID", keyId);
            countCmd.Parameters.AddWithValue("@Today", today);

            return (int)await countCmd.ExecuteScalarAsync();
        }

        /// <summary>
        /// Lấy trạng thái tất cả keys (cho admin monitoring)
        /// </summary>
        public async Task<List<GeminiApiKey>> GetAllKeysStatusAsync()
        {
            var keys = new List<GeminiApiKey>();

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var today = DateTime.Today;

                var query = @"
                    SELECT 
                        k.KeyID, 
                        k.KeyName, 
                        k.Email, 
                        k.DailyLimit, 
                        k.LastUsedDate,
                        ISNULL((SELECT COUNT(*) FROM APIUsageLog 
                                WHERE KeyID_FK = k.KeyID 
                                AND CAST(UsedAt AS DATE) = @Today), 0) AS TodayUsage,
                        ISNULL((SELECT COUNT(*) FROM APIUsageLog 
                                WHERE KeyID_FK = k.KeyID 
                                AND CAST(UsedAt AS DATE) = @Today
                                AND IsSuccess = 1), 0) AS SuccessCount
                    FROM APIKey k
                    ORDER BY k.KeyName";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Today", today);

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int usageCount = reader.GetInt32(5);
                    int successCount = reader.GetInt32(6);
                    int dailyLimit = reader.GetInt32(3);

                    keys.Add(new GeminiApiKey
                    {
                        KeyID = reader.GetInt32(0),
                        KeyName = reader.GetString(1),
                        Email = reader.GetString(2).Substring(0, Math.Min(15, reader.GetString(2).Length)) + "...",
                        DailyLimit = dailyLimit,
                        LastUsedDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                        UsageCount = usageCount,
                        SuccessCount = successCount,
                        RemainingQuota = dailyLimit - usageCount
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting keys status");
            }

            return keys;
        }
    }
}
public class GeminiApiKey
{
    public int KeyID { get; set; }
    public string KeyName { get; set; }
    public string Email { get; set; }
    public int DailyLimit { get; set; }
    public DateTime? LastUsedDate { get; set; }

    // Computed properties
    public int UsageCount { get; set; } // Tổng số lần gọi (kể cả retry/fail)
    public int SuccessCount { get; set; } // Số lần thành công
    public int RemainingQuota { get; set; }
    public string ActualApiKey { get; set; } // Từ User Secrets
}

/// <summary>
/// Kết quả từ GetAvailableApiKeyAsync
/// </summary>
public class ApiKeyUsageResult
{
    public bool Success { get; set; }
    public GeminiApiKey ApiKey { get; set; }
    public string ErrorMessage { get; set; }
}