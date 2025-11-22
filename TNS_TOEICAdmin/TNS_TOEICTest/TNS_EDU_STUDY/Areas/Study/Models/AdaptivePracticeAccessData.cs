using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TNS_EDU_STUDY.Areas.Study.Models
{
    /// <summary>
    /// Data access for Adaptive Practice page - Retrieves MemberLearningProfile analysis
    /// </summary>
    public class AdaptivePracticeAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        /// <summary>
        /// Get learning profile for a specific Part (1-7)
        /// Returns null if no analysis data exists
        /// </summary>
        public static async Task<PartAnalysisDto> GetPartAnalysisAsync(string memberKey, int part)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                    SELECT TOP 1 
                        Part,
                        SpeedScore,
                        DecisivenessScore,
                        AccuracyScore,
                        AvgTimeSpent,
                        WeakTopicsJSON,
                        Advice,
                        AbilityTemporary,
                        LastAnalyzed
                    FROM [dbo].[MemberLearningProfile]
                    WHERE MemberKey = @MemberKey AND Part = @Part
                    ORDER BY LastAnalyzed DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                cmd.Parameters.AddWithValue("@Part", part);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var weakTopicsJson = reader["WeakTopicsJSON"]?.ToString();
                    var weakTopics = !string.IsNullOrEmpty(weakTopicsJson)
                        ? JsonConvert.DeserializeObject<WeakTopicsDto>(weakTopicsJson)
                        : new WeakTopicsDto();

                    return new PartAnalysisDto
                    {
                        Part = Convert.ToInt32(reader["Part"]),
                        SpeedScore = reader["SpeedScore"] != DBNull.Value ? Convert.ToSingle(reader["SpeedScore"]) : 0,
                        DecisivenessScore = reader["DecisivenessScore"] != DBNull.Value ? Convert.ToSingle(reader["DecisivenessScore"]) : 0,
                        AccuracyScore = reader["AccuracyScore"] != DBNull.Value ? Convert.ToSingle(reader["AccuracyScore"]) : 0,
                        AvgTimeSpent = reader["AvgTimeSpent"] != DBNull.Value ? Convert.ToSingle(reader["AvgTimeSpent"]) : 0,
                        WeakTopics = weakTopics,
                        Advice = reader["Advice"]?.ToString(),
                        AbilityTemporary = reader["AbilityTemporary"] != DBNull.Value ? Convert.ToSingle(reader["AbilityTemporary"]) : 0,
                        LastAnalyzed = reader["LastAnalyzed"] != DBNull.Value ? Convert.ToDateTime(reader["LastAnalyzed"]) : DateTime.MinValue
                    };
                }

                return null; // No data found
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdaptivePracticeAccessData ERROR]: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get analysis status for all 7 parts (for overview cards)
        /// </summary>
        public static async Task<Dictionary<int, bool>> GetAllPartsStatusAsync(string memberKey)
        {
            var status = new Dictionary<int, bool>();
            for (int i = 1; i <= 7; i++)
            {
                status[i] = false; // Default: no data
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                    SELECT Part
                    FROM [dbo].[MemberLearningProfile]
                    WHERE MemberKey = @MemberKey
                    GROUP BY Part";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@MemberKey", memberKey);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int part = Convert.ToInt32(reader["Part"]);
                    if (part >= 1 && part <= 7)
                    {
                        status[part] = true; // Has data
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAllPartsStatus ERROR]: {ex.Message}");
            }

            return status;
        }
    }

    // DTO Classes
    public class PartAnalysisDto
    {
        public int Part { get; set; }
        public float SpeedScore { get; set; }
        public float DecisivenessScore { get; set; }
        public float AccuracyScore { get; set; }
        public float AvgTimeSpent { get; set; }
        public WeakTopicsDto WeakTopics { get; set; }
        public string Advice { get; set; } // Markdown from Gemini
        public float AbilityTemporary { get; set; } // Theta
        public DateTime LastAnalyzed { get; set; }
    }

    public class WeakTopicsDto
    {
        [JsonProperty("grammar")]
        public List<string> Grammar { get; set; } = new List<string>();

        [JsonProperty("vocab")]
        public List<string> Vocab { get; set; } = new List<string>();

        [JsonProperty("categories")]
        public List<string> Categories { get; set; } = new List<string>();

        [JsonProperty("errors")]
        public List<string> Errors { get; set; } = new List<string>();

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("behavioralPattern")]
        public string BehavioralPattern { get; set; }

        [JsonProperty("actionableRecommendations")]
        public List<string> ActionableRecommendations { get; set; } = new List<string>();
    }
}