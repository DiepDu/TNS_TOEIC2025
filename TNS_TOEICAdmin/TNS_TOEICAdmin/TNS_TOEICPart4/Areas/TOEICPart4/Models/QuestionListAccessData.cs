using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace TNS_TOEICPart4.Areas.TOEICPart4.Models
{
    public static class QuestionListDataAccess
    {
        // ===== EXISTING METHODS (giữ nguyên) =====
        public static JsonResult GetList(string Search, int Level, int PageSize, int PageNumber, string StatusFilter)
        {
            // ... code hiện tại giữ nguyên ...
            string zMessage = "";

            string zCountSQL = @"SELECT COUNT(*) 
                       FROM [dbo].[TEC_Part4_Question] 
                       WHERE QuestionText LIKE @Search AND Parent IS NULL
                       AND (QuestionKey IS NOT NULL) ";

            string zDataSQL = @"SELECT QuestionKey, QuestionText, QuestionVoice, SkillLevel, AmountAccess, Publish, RecordStatus 
                       FROM [dbo].[TEC_Part4_Question] 
                       WHERE QuestionText LIKE @Search AND Parent IS NULL
                       AND (QuestionKey IS NOT NULL) ";

            string statusFilterClause = "";
            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Using")
                {
                    statusFilterClause = " AND Publish = 1 AND RecordStatus != 99 ";
                }
                else if (StatusFilter == "Unpublished")
                {
                    statusFilterClause = " AND Publish = 0 ";
                }
                else if (StatusFilter == "Deleted")
                {
                    statusFilterClause = " AND RecordStatus = 99 ";
                }
            }

            zCountSQL += statusFilterClause;
            zDataSQL += statusFilterClause;

            if (Level > 0)
            {
                zCountSQL += " AND SkillLevel = @Level ";
                zDataSQL += " AND SkillLevel = @Level ";
            }

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

                    using (SqlCommand zCountCommand = new SqlCommand(zCountSQL, zConnect))
                    {
                        zCountCommand.CommandType = CommandType.Text;
                        zCountCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        zCountCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        totalCount = (int)zCountCommand.ExecuteScalar();
                    }

                    using (SqlCommand zDataCommand = new SqlCommand(zDataSQL, zConnect))
                    {
                        zDataCommand.CommandType = CommandType.Text;
                        zDataCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        zDataCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        zDataCommand.Parameters.Add("@PageSize", SqlDbType.Int).Value = PageSize;
                        zDataCommand.Parameters.Add("@PageNumber", SqlDbType.Int).Value = PageNumber;
                        using (SqlDataAdapter zAdapter = new SqlDataAdapter(zDataCommand))
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

            return new JsonResult(new
            {
                data = zDataList,
                totalCount = totalCount
            });
        }

        public static JsonResult GetList(string Search, int Level, DateTime FromDate, DateTime ToDate, int PageSize, int PageNumber, string StatusFilter)
        {
            // ... code hiện tại giữ nguyên ...
            string zMessage = "";

            string zCountSQL = @"SELECT COUNT(*) 
                       FROM [dbo].[TEC_Part4_Question] 
                       WHERE (CreatedOn >= @FromDate AND CreatedOn <= @ToDate) 
                       AND QuestionText LIKE @Search AND Parent IS NULL
                       AND (QuestionKey IS NOT NULL) ";

            string zDataSQL = @"SELECT QuestionKey, QuestionText, QuestionVoice, SkillLevel, AmountAccess, Publish, RecordStatus 
                       FROM [dbo].[TEC_Part4_Question] 
                       WHERE (CreatedOn >= @FromDate AND CreatedOn <= @ToDate) 
                       AND QuestionText LIKE @Search AND Parent IS NULL
                       AND (QuestionKey IS NOT NULL) ";

            string statusFilterClause = "";
            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Using")
                {
                    statusFilterClause = " AND Publish = 1 AND RecordStatus != 99 ";
                }
                else if (StatusFilter == "Unpublished")
                {
                    statusFilterClause = " AND Publish = 0 ";
                }
                else if (StatusFilter == "Deleted")
                {
                    statusFilterClause = " AND RecordStatus = 99 ";
                }
            }

            zCountSQL += statusFilterClause;
            zDataSQL += statusFilterClause;

            if (Level > 0)
            {
                zCountSQL += " AND SkillLevel = @Level ";
                zDataSQL += " AND SkillLevel = @Level ";
            }

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

                    using (SqlCommand zCountCommand = new SqlCommand(zCountSQL, zConnect))
                    {
                        zCountCommand.CommandType = CommandType.Text;
                        zCountCommand.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = new DateTime(FromDate.Year, FromDate.Month, FromDate.Day, 0, 0, 1);
                        zCountCommand.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = new DateTime(ToDate.Year, ToDate.Month, ToDate.Day, 23, 59, 59);
                        zCountCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        zCountCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        totalCount = (int)zCountCommand.ExecuteScalar();
                    }

                    using (SqlCommand zDataCommand = new SqlCommand(zDataSQL, zConnect))
                    {
                        zDataCommand.CommandType = CommandType.Text;
                        zDataCommand.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = new DateTime(FromDate.Year, FromDate.Month, FromDate.Day, 0, 0, 1);
                        zDataCommand.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = new DateTime(ToDate.Year, ToDate.Month, ToDate.Day, 23, 59, 59);
                        zDataCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        zDataCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        zDataCommand.Parameters.Add("@PageSize", SqlDbType.Int).Value = PageSize;
                        zDataCommand.Parameters.Add("@PageNumber", SqlDbType.Int).Value = PageNumber;
                        using (SqlDataAdapter zAdapter = new SqlDataAdapter(zDataCommand))
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

            return new JsonResult(new
            {
                data = zDataList,
                totalCount = totalCount
            });
        }

        // ===== ✅ THÊM CÁC PHƯƠNG THỨC VALIDATION MỚI =====

        /// <summary>
        /// Count the number of answers for a question (not deleted)
        /// </summary>
        public static int GetAnswerCount(string QuestionKey)
        {
            string zSQL = @"SELECT COUNT(*) 
                           FROM [dbo].[TEC_Part4_Answer] 
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

        /// <summary>
        /// Count the number of correct answers for a question
        /// </summary>
        public static int GetCorrectAnswerCount(string QuestionKey)
        {
            string zSQL = @"SELECT COUNT(*) 
                           FROM [dbo].[TEC_Part4_Answer] 
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

        /// <summary>
        /// Validate child questions (sub-questions) of a parent question (passage)
        /// </summary>
        public static List<string> ValidateChildQuestions(string parentQuestionKey)
        {
            List<string> errors = new List<string>();
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

            string countSQL = @"SELECT COUNT(*) 
                               FROM [dbo].[TEC_Part4_Question] 
                               WHERE Parent = @ParentKey 
                               AND RecordStatus != 99";

            string listSQL = @"SELECT QuestionKey, QuestionText, Explanation, SkillLevel, 
                                      Category, GrammarTopic, VocabularyTopic, ErrorType, Ranking
                               FROM [dbo].[TEC_Part4_Question] 
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

                        int answerCount = GetAnswerCount(child.QuestionKey);
                        if (answerCount != 4)
                        {
                            errors.Add($"Sub-question #{childIndex} must have exactly 4 answers (currently has {answerCount}).");
                        }

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
}