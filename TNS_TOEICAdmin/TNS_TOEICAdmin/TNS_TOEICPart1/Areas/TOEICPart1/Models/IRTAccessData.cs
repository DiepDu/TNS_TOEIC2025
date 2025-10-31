
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using TNS_TOEICPart1.Areas.TOEICPart1.Services;

namespace TNS_TOEICPart1.Areas.TOEICPart1.Models
{
    /// <summary>
    /// Data Access Layer for Full IRT Analysis (3PL + EM Algorithm)
    /// Handles ALL 7 PARTS of TOEIC questions
    /// </summary>
    public static class IrtDataAccess
    {
        /// <summary>
        /// Main method: Update Full IRT statistics for ALL 7 PARTS
        /// Phase 1: Basic IRT - Let Python service decide data sufficiency
        /// </summary>
        public static async Task<(bool Success, string Message)> UpdateFullIrtAsync()
        {
            List<IrtResponse> responses = null; // ✅ KHAI BÁO Ở ĐẦU để dùng trong catch block

            try
            {
                // Step 1: Get all responses from database (ALL PARTS)
                responses = GetAllIrtResponsesAllParts();

                // ✅ BỎ C# CHECK >= 100 - ĐỂ PYTHON QUYẾT ĐỊNH
                // Lý do: Python có logic filter phức tạp hơn (per-question, per-member)
                if (responses.Count == 0)
                {
                    return (false, "❌ Không có dữ liệu responses nào trong database!\n\n" +
                                   "Vui lòng kiểm tra:\n" +
                                   "• Bảng UserAnswers có dữ liệu không?\n" +
                                   "• ResultOfUserForTest có link với EDU_Member không?");
                }

                // Step 2: Call Python IRT service
                var irtClient = new IrtServiceClient();

                // Health check first
                bool isHealthy = await irtClient.HealthCheckAsync();
                if (!isHealthy)
                {
                    return (false, "❌ Python IRT service is not running!\n\n" +
"📋 STARTUP INSTRUCTIONS:\n\n" +
"1️⃣ Open a new PowerShell/Terminal\n" +
"2️⃣ cd D:\\Document\\TNS_TOEIC2025\\TNS_TOEICAdmin\\TNS_TOEICAdmin\\PythonServices\n" +
"3️⃣ irt_env\\Scripts\\activate\n" +
"4️⃣ python full_irt_service.py\n\n" +
"When you see the message:\n" +
" * Running on http://127.0.0.1:5001\n" +
"→ Come back here and click 'Update Full IRT' again.\n\n" +
"🔗 Test health: http://localhost:5001/health");
                }

                // Analyze
                var result = await irtClient.AnalyzeAsync(responses);

                if (result.Status != "OK")
                {
                    return (false, $"❌ IRT analysis failed!\n\nStatus: {result.Status}");
                }

                // Step 3: Update database with IRT parameters (ALL PARTS)
                int updatedQuestions = UpdateIrtParametersToDatabaseAllParts(result);

                // Step 4: Update member abilities
                int updatedMembers = UpdateMemberAbilities(result);

                // Format success message with details
                string successMessage = $"✅ IRT update successful!\n\n" +
$"📊 Parsing from Python service:\n" +
$" • Total Questions Analyzed: {result.Metadata.TotalQuestions}\n" +
$" • Total Members: {result.Metadata.TotalMembers}\n" +
$" • Total Responses Processed: {result.Metadata.TotalResponses}\n" +
$" • Input Data Sent: {responses.Count} responses\n\n" +
$"💾 Update to Database:\n" +
$" • Questions Updated: {updatedQuestions} (across 7 parts)\n" +
$" • Members Updated: {updatedMembers}\n\n" +
$"🔬 Model: {result.Metadata.ModelType}\n" +
$"⏱️ Training Iterations: {result.Metadata.Iterations}\n" +
$"📅 Timestamp: {result.Metadata.Timestamp}";

                return (true, successMessage);
            }
            catch (HttpRequestException ex)
            {
                return (false, $"❌ Unable to connect to Python service!\n\n" +
$"Error details:\n{ex.Message}\n\n" +
$"Please:\n" +
$"1. Check if Python service is running\n" +
$"2. Check if port 5001 is occupied by another app\n" +
$"3. Try restarting Python service");
            }
            catch (TaskCanceledException)
            {
                int responseCount = responses?.Count ?? 0; // ✅ NULL-SAFE
                return (false, $"❌ Timeout! IRT analysis took more than 5 minutes.\n\n" +
$"Maybe because:\n" +
$"• Data is too large ({responseCount} responses)\n" +
$"• Python service is frozen\n\n" +
$"Solution:\n" +
$"• Restart Python service\n" +
$"• Try again with less data");
            }
            catch (Exception ex)
            {
                return (false, $"❌ Unknown error:\n\n{ex.Message}\n\n" +
$"Stack Trace:\n{ex.StackTrace}");
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Get all user responses for IRT analysis (ALL 7 PARTS)
        /// Returns: List of {memberKey, questionKey, isCorrect}
        /// </summary>
        private static List<IrtResponse> GetAllIrtResponsesAllParts()
        {
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            var responses = new List<IrtResponse>();

            // Query gets ALL responses from UserAnswers (all 7 parts)
            string zSQL = @"
                SELECT 
                    r.MemberKey,
                    ua.QuestionKey,
                    ua.IsCorrect
                FROM [dbo].[UserAnswers] ua
                INNER JOIN [dbo].[ResultOfUserForTest] r ON ua.ResultKey = r.ResultKey
                WHERE ua.Part BETWEEN 1 AND 7
                AND r.MemberKey IS NOT NULL
                AND ua.QuestionKey IS NOT NULL
                AND ua.IsCorrect IS NOT NULL";

            using (SqlConnection zConnect = new SqlConnection(zConnectionString))
            {
                zConnect.Open();
                using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                {
                    using (SqlDataReader reader = zCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            responses.Add(new IrtResponse
                            {
                                memberKey = reader["MemberKey"].ToString(),
                                questionKey = reader["QuestionKey"].ToString(),
                                isCorrect = Convert.ToInt32(reader["IsCorrect"])
                            });
                        }
                    }
                }
            }

            return responses;
        }

        /// <summary>
        /// Update IRT parameters to ALL 7 PART tables
        /// Part 3,4,6,7: Only update questions with Parent IS NOT NULL
        /// </summary>
        private static int UpdateIrtParametersToDatabaseAllParts(IrtAnalysisResult result)
        {
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            int updatedCount = 0;

            using (SqlConnection zConnect = new SqlConnection(zConnectionString))
            {
                zConnect.Open();

                foreach (var kvp in result.QuestionParams)
                {
                    string questionKey = kvp.Key;
                    var irtParams = kvp.Value;

                    // Determine which part this question belongs to
                    int part = GetQuestionPart(questionKey, zConnect);

                    if (part == 0)
                        continue; // Skip if part not found

                    string tableName = $"[dbo].[TEC_Part{part}_Question]";

                    // Build UPDATE SQL
                    string updateSQL = $@"
                        UPDATE {tableName}
                        SET 
                            IrtDifficulty = @Difficulty,
                            IrtDiscrimination = @Discrimination,
                            IrtGuessing = @Guessing,
                            Quality = @Quality,
                            ConfidenceLevel = @ConfidenceLevel,
                            LastAnalyzed = GETDATE()
                        WHERE QuestionKey = @QuestionKey";

                    // ✅ KEY LOGIC: For Part 3,4,6,7 - Only update if Parent IS NOT NULL
                    if (part == 3 || part == 4 || part == 6 || part == 7)
                    {
                        updateSQL += " AND Parent IS NOT NULL";
                    }

                    using (SqlCommand updateCommand = new SqlCommand(updateSQL, zConnect))
                    {
                        updateCommand.Parameters.AddWithValue("@QuestionKey", questionKey);
                        updateCommand.Parameters.AddWithValue("@Difficulty", irtParams.Difficulty);
                        updateCommand.Parameters.AddWithValue("@Discrimination", irtParams.Discrimination);
                        updateCommand.Parameters.AddWithValue("@Guessing", irtParams.Guessing);
                        updateCommand.Parameters.AddWithValue("@Quality", irtParams.Quality ?? "");
                        updateCommand.Parameters.AddWithValue("@ConfidenceLevel", irtParams.ConfidenceLevel ?? "");

                        int rowsAffected = updateCommand.ExecuteNonQuery();
                        updatedCount += rowsAffected;
                    }
                }
            }

            return updatedCount;
        }

        /// <summary>
        /// Helper: Determine which part a question belongs to
        /// Checks all 7 part tables to find the question
        /// </summary>
        private static int GetQuestionPart(string questionKey, SqlConnection connection)
        {
            // Try each part table (1-7)
            for (int part = 1; part <= 7; part++)
            {
                string checkSQL = $"SELECT COUNT(*) FROM [dbo].[TEC_Part{part}_Question] WHERE QuestionKey = @QuestionKey";

                using (SqlCommand cmd = new SqlCommand(checkSQL, connection))
                {
                    cmd.Parameters.AddWithValue("@QuestionKey", questionKey);
                    int count = (int)cmd.ExecuteScalar();

                    if (count > 0)
                        return part;
                }
            }

            return 0; // Not found in any part
        }

        /// <summary>
        /// Update member abilities (theta) to EDU_Member table
        /// </summary>
        private static int UpdateMemberAbilities(IrtAnalysisResult result)
        {
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            int updatedCount = 0;

            using (SqlConnection zConnect = new SqlConnection(zConnectionString))
            {
                zConnect.Open();

                foreach (var kvp in result.MemberAbilities)
                {
                    string memberKey = kvp.Key;
                    double theta = kvp.Value;

                    string updateSQL = @"
                        UPDATE [dbo].[EDU_Member]
                        SET 
                            IrtAbility = @Theta,
                            IrtUpdatedOn = GETDATE()
                        WHERE MemberKey = @MemberKey";

                    using (SqlCommand updateCommand = new SqlCommand(updateSQL, zConnect))
                    {
                        updateCommand.Parameters.AddWithValue("@MemberKey", memberKey);
                        updateCommand.Parameters.AddWithValue("@Theta", theta);

                        int rowsAffected = updateCommand.ExecuteNonQuery();
                        updatedCount += rowsAffected;
                    }
                }
            }

            return updatedCount;
        }

        #endregion
    }
}