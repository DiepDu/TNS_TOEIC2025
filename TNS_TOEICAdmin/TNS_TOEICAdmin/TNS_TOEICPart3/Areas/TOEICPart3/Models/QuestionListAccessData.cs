using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace TNS_TOEICPart3.Areas.TOEICPart3.Models
{
    public class ItemRequest
    {
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public string Search { get; set; }
        public int Level { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public string? StatusFilter { get; set; }
    }

    public static class QuestionListDataAccess
    {
        // ===== HELPER METHODS =====
        private static string BuildWhereClause(bool hasDateFilter)
        {
            string where = hasDateFilter
                ? "WHERE (CreatedOn >= @FromDate AND CreatedOn <= @ToDate) AND QuestionText LIKE @Search AND Parent IS NULL AND (QuestionKey IS NOT NULL) "
                : "WHERE QuestionText LIKE @Search AND Parent IS NULL AND (QuestionKey IS NOT NULL) ";
            return where;
        }

        private static string BuildStatusFilter(string statusFilter)
        {
            if (string.IsNullOrEmpty(statusFilter)) return "";

            if (statusFilter == "Using")
                return " AND Publish = 1 AND RecordStatus != 99 ";
            else if (statusFilter == "Unpublished")
                return " AND Publish = 0 ";
            else if (statusFilter == "Deleted")
                return " AND RecordStatus = 99 ";

            return "";
        }

        // ===== METHOD WITHOUT DATE - Returns data + totalCount =====
        public static JsonResult GetList(string Search, int Level, int PageSize, int PageNumber, string StatusFilter)
        {
            string zMessage = "";

            // ✅ BỎ CorrectRate và Anomaly - CHỈ LẤY CÁC CỘT CẦN THIẾT
            string baseColumns = "QuestionKey, QuestionText, QuestionVoice, SkillLevel, AmountAccess, Publish, RecordStatus";

            // Count query
            string zCountSQL = $"SELECT COUNT(*) FROM [dbo].[TEC_Part3_Question] {BuildWhereClause(false)}";
            if (Level > 0)
                zCountSQL += " AND SkillLevel = @Level ";
            zCountSQL += BuildStatusFilter(StatusFilter);

            // Data query with pagination
            string zDataSQL = $@"SELECT {baseColumns} 
                       FROM [dbo].[TEC_Part3_Question] {BuildWhereClause(false)}";

            if (Level > 0)
                zDataSQL += " AND SkillLevel = @Level ";

            zDataSQL += BuildStatusFilter(StatusFilter);
            zDataSQL += " ORDER BY CreatedOn DESC ";
            zDataSQL += " OFFSET @PageSize * (@PageNumber - 1) ROWS FETCH NEXT @PageSize ROWS ONLY OPTION(RECOMPILE)";

            DataTable zTable = new DataTable();
            int totalCount = 0;
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

            try
            {
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();

                    // Get total count
                    using (SqlCommand zCountCommand = new SqlCommand(zCountSQL, zConnect))
                    {
                        zCountCommand.CommandType = CommandType.Text;
                        zCountCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        zCountCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        totalCount = (int)zCountCommand.ExecuteScalar();
                    }

                    // Get data
                    using (SqlCommand zCommand = new SqlCommand(zDataSQL, zConnect))
                    {
                        zCommand.CommandType = CommandType.Text;
                        zCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        zCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        zCommand.Parameters.Add("@PageSize", SqlDbType.Int).Value = PageSize;
                        zCommand.Parameters.Add("@PageNumber", SqlDbType.Int).Value = PageNumber;
                        using (SqlDataAdapter zAdapter = new SqlDataAdapter(zCommand))
                        {
                            zAdapter.Fill(zTable);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                zMessage = ex.ToString();
            }

            // ✅ BỎ CorrectRate và Anomaly khỏi anonymous object
            var zDataList = zTable.AsEnumerable().Select(row => new
            {
                QuestionKey = row["QuestionKey"].ToString(),
                QuestionText = row["QuestionText"].ToString() ?? "",
                QuestionVoice = row["QuestionVoice"].ToString() ?? "",
                SkillLevel = row["SkillLevel"] != DBNull.Value ? Convert.ToInt32(row["SkillLevel"]) : 0,
                AmountAccess = row["AmountAccess"] != DBNull.Value ? Convert.ToInt32(row["AmountAccess"]) : 0,
                Publish = row["Publish"] != DBNull.Value ? Convert.ToBoolean(row["Publish"]) : false,
                RecordStatus = row["RecordStatus"] != DBNull.Value ? Convert.ToInt32(row["RecordStatus"]) : 0
            }).ToList();

            return new JsonResult(new { data = zDataList, totalCount = totalCount });
        }

        // ===== METHOD WITH DATE - Returns data + totalCount =====
        public static JsonResult GetList(string Search, int Level, DateTime FromDate, DateTime ToDate, int PageSize, int PageNumber, string StatusFilter)
        {
            string zMessage = "";

            // ✅ BỎ CorrectRate và Anomaly - CHỈ LẤY CÁC CỘT CẦN THIẾT
            string baseColumns = "QuestionKey, QuestionText, QuestionVoice, SkillLevel, AmountAccess, Publish, RecordStatus";

            // Count query
            string zCountSQL = $"SELECT COUNT(*) FROM [dbo].[TEC_Part3_Question] {BuildWhereClause(true)}";
            if (Level > 0)
                zCountSQL += " AND SkillLevel = @Level ";
            zCountSQL += BuildStatusFilter(StatusFilter);

            // Data query with pagination
            string zDataSQL = $@"SELECT {baseColumns} 
                       FROM [dbo].[TEC_Part3_Question] {BuildWhereClause(true)}";

            if (Level > 0)
                zDataSQL += " AND SkillLevel = @Level ";

            zDataSQL += BuildStatusFilter(StatusFilter);
            zDataSQL += " ORDER BY CreatedOn DESC ";
            zDataSQL += " OFFSET @PageSize * (@PageNumber - 1) ROWS FETCH NEXT @PageSize ROWS ONLY OPTION(RECOMPILE)";

            DataTable zTable = new DataTable();
            int totalCount = 0;
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

            try
            {
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();

                    // Get total count
                    using (SqlCommand zCountCommand = new SqlCommand(zCountSQL, zConnect))
                    {
                        zCountCommand.CommandType = CommandType.Text;
                        zCountCommand.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = new DateTime(FromDate.Year, FromDate.Month, FromDate.Day, 0, 0, 1);
                        zCountCommand.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = new DateTime(ToDate.Year, ToDate.Month, ToDate.Day, 23, 59, 59);
                        zCountCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        zCountCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        totalCount = (int)zCountCommand.ExecuteScalar();
                    }

                    // Get data
                    using (SqlCommand zCommand = new SqlCommand(zDataSQL, zConnect))
                    {
                        zCommand.CommandType = CommandType.Text;
                        zCommand.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = new DateTime(FromDate.Year, FromDate.Month, FromDate.Day, 0, 0, 1);
                        zCommand.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = new DateTime(ToDate.Year, ToDate.Month, ToDate.Day, 23, 59, 59);
                        zCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        zCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        zCommand.Parameters.Add("@PageSize", SqlDbType.Int).Value = PageSize;
                        zCommand.Parameters.Add("@PageNumber", SqlDbType.Int).Value = PageNumber;
                        using (SqlDataAdapter zAdapter = new SqlDataAdapter(zCommand))
                        {
                            zAdapter.Fill(zTable);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                zMessage = ex.ToString();
            }

            // ✅ BỎ CorrectRate và Anomaly khỏi anonymous object
            var zDataList = zTable.AsEnumerable().Select(row => new
            {
                QuestionKey = row["QuestionKey"].ToString(),
                QuestionText = row["QuestionText"].ToString() ?? "",
                QuestionVoice = row["QuestionVoice"].ToString() ?? "",
                SkillLevel = row["SkillLevel"] != DBNull.Value ? Convert.ToInt32(row["SkillLevel"]) : 0,
                AmountAccess = row["AmountAccess"] != DBNull.Value ? Convert.ToInt32(row["AmountAccess"]) : 0,
                Publish = row["Publish"] != DBNull.Value ? Convert.ToBoolean(row["Publish"]) : false,
                RecordStatus = row["RecordStatus"] != DBNull.Value ? Convert.ToInt32(row["RecordStatus"]) : 0
            }).ToList();

            return new JsonResult(new { data = zDataList, totalCount = totalCount });
        }
        /// <summary>
        /// Validate child questions (sub-questions) of a parent question (passage)
        /// </summary>
        public static List<string> ValidateChildQuestions(string parentQuestionKey)
        {
            List<string> errors = new List<string>();
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

            string countSQL = @"SELECT COUNT(*) 
                       FROM [dbo].[TEC_Part3_Question] 
                       WHERE Parent = @ParentKey 
                       AND RecordStatus != 99";

            string listSQL = @"SELECT QuestionKey, QuestionText, Explanation, SkillLevel, 
                              Category, GrammarTopic, VocabularyTopic, ErrorType, Ranking
                       FROM [dbo].[TEC_Part3_Question] 
                       WHERE Parent = @ParentKey 
                       AND RecordStatus != 99
                       ORDER BY Ranking";

            try
            {
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();

                    int childCount = 0;
                    using (SqlCommand countCmd = new SqlCommand(countSQL, zConnect))
                    {
                        countCmd.Parameters.Add("@ParentKey", SqlDbType.UniqueIdentifier).Value = new Guid(parentQuestionKey);
                        childCount = (int)countCmd.ExecuteScalar();
                    }

                    if (childCount < 3)
                    {
                        errors.Add($"Passage must have at least 3 sub-questions (currently has {childCount}).");
                        return errors;
                    }

                    List<SubQuestionValidation> subQuestions = new List<SubQuestionValidation>();

                    using (SqlCommand listCmd = new SqlCommand(listSQL, zConnect))
                    {
                        listCmd.Parameters.Add("@ParentKey", SqlDbType.UniqueIdentifier).Value = new Guid(parentQuestionKey);

                        using (SqlDataReader reader = listCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                subQuestions.Add(new SubQuestionValidation
                                {
                                    QuestionKey = reader["QuestionKey"].ToString(),
                                    QuestionText = reader["QuestionText"]?.ToString(),
                                    Explanation = reader["Explanation"]?.ToString(),
                                    SkillLevel = reader["SkillLevel"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SkillLevel"]),
                                    Category = reader["Category"]?.ToString(),
                                    GrammarTopic = reader["GrammarTopic"]?.ToString(),
                                    VocabularyTopic = reader["VocabularyTopic"]?.ToString(),
                                    ErrorType = reader["ErrorType"]?.ToString(),
                                    Ranking = reader["Ranking"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Ranking"])
                                });
                            }
                        }
                    }

                    for (int i = 0; i < subQuestions.Count; i++)
                    {
                        var child = subQuestions[i];
                        int childIndex = i + 1;
                        List<string> childErrors = new List<string>();

                        // Check required fields
                        if (string.IsNullOrWhiteSpace(child.QuestionText))
                            childErrors.Add("Text");

                        if (string.IsNullOrWhiteSpace(child.Explanation))
                            childErrors.Add("Explanation");

                        if (child.SkillLevel <= 0)
                            childErrors.Add("Level");

                        if (string.IsNullOrWhiteSpace(child.Category))
                            childErrors.Add("Category");

                        if (string.IsNullOrWhiteSpace(child.GrammarTopic))
                            childErrors.Add("Grammar Topic");

                        if (string.IsNullOrWhiteSpace(child.VocabularyTopic))
                            childErrors.Add("Vocabulary Topic");

                        if (string.IsNullOrWhiteSpace(child.ErrorType))
                            childErrors.Add("Error Type");

                        if (child.Ranking <= 0)
                            childErrors.Add("Ranking");

                        if (childErrors.Count > 0)
                        {
                            errors.Add($"Sub-question #{childIndex} is missing: {string.Join(", ", childErrors)}");
                        }

                        // ✅ KIỂM TRA SỐ LƯỢNG ĐÁP ÁN
                        int answerCount = GetAnswerCount(child.QuestionKey);
                        if (answerCount != 4)
                        {
                            errors.Add($"Sub-question #{childIndex} must have exactly 4 answers (currently has {answerCount}).");
                        }

                        // ✅ THÊM MỚI: KIỂM TRA SỐ LƯỢNG ĐÁP ÁN ĐÚNG
                        int correctAnswerCount = GetCorrectAnswerCount(child.QuestionKey);
                        if (correctAnswerCount != 1)
                        {
                            if (correctAnswerCount == 0)
                            {
                                errors.Add($"Sub-question #{childIndex} must have exactly 1 correct answer (currently has none).");
                            }
                            else
                            {
                                errors.Add($"Sub-question #{childIndex} must have exactly 1 correct answer (currently has {correctAnswerCount}).");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error validating sub-questions: {ex.Message}");
            }

            return errors;
        }
        /// <summary>
        /// Count the number of answers for a question (not deleted)
        /// </summary>
        public static int GetAnswerCount(string QuestionKey)
        {
            string zSQL = @"SELECT COUNT(*) 
                   FROM [dbo].[TEC_Part3_Answer] 
                   WHERE QuestionKey = @QuestionKey 
                   AND RecordStatus != 99";

            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            try
            {
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();
                    using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                    {
                        zCommand.CommandType = CommandType.Text;
                        zCommand.Parameters.Add("@QuestionKey", SqlDbType.UniqueIdentifier).Value = new Guid(QuestionKey);
                        return (int)zCommand.ExecuteScalar();
                    }
                }
            }
            catch
            {
                return 0;
            }
        }
        // Thêm phương thức này NGAY SAU phương thức GetAnswerCount

        /// <summary>
        /// Count the number of correct answers for a question
        /// </summary>
        public static int GetCorrectAnswerCount(string QuestionKey)
        {
            string zSQL = @"SELECT COUNT(*) 
                   FROM [dbo].[TEC_Part3_Answer] 
                   WHERE QuestionKey = @QuestionKey 
                   AND RecordStatus != 99 
                   AND AnswerCorrect = 1";

            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            try
            {
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();
                    using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                    {
                        zCommand.CommandType = CommandType.Text;
                        zCommand.Parameters.Add("@QuestionKey", SqlDbType.UniqueIdentifier).Value = new Guid(QuestionKey);
                        return (int)zCommand.ExecuteScalar();
                    }
                }
            }
            catch
            {
                return 0;
            }
        }
        // Helper class to hold validation information
        private class SubQuestionValidation
        {
            public string QuestionKey { get; set; }
            public string QuestionText { get; set; }
            public string Explanation { get; set; }
            public int SkillLevel { get; set; }
            public string Category { get; set; }
            public string GrammarTopic { get; set; }
            public string VocabularyTopic { get; set; }
            public string ErrorType { get; set; }
            public int Ranking { get; set; }
        }
    }
}