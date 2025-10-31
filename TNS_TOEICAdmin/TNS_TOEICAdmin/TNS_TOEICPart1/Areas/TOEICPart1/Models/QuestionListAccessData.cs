using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace TNS_TOEICPart1.Areas.TOEICPart1.Models
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
        private class QuestionStat
        {
            public string QuestionKey { get; set; }
            public int Part { get; set; }
            public double? CorrectRate { get; set; }
            public int? Anomaly { get; set; }
        }
        public static int GetTotalCount(string Search, int Level, string StatusFilter)
        {
            string zSQL = @"SELECT COUNT(*) 
                   FROM [dbo].[TEC_Part1_Question] 
                   WHERE QuestionText LIKE @Search 
                   AND (QuestionKey IS NOT NULL) ";

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Using")
                    zSQL += " AND Publish = 1 AND RecordStatus != 99 ";
                else if (StatusFilter == "Unpublished")
                    zSQL += " AND Publish = 0 ";
                else if (StatusFilter == "Deleted")
                    zSQL += " AND RecordStatus = 99 ";
            }

            if (Level > 0)
                zSQL += " AND SkillLevel = @Level ";

            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            try
            {
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();
                    using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                    {
                        zCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        zCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        return (int)zCommand.ExecuteScalar();
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        // THÊM OVERLOAD CHO GetTotalCount với Date
        public static int GetTotalCount(string Search, int Level, DateTime FromDate, DateTime ToDate, string StatusFilter)
        {
            string zSQL = @"SELECT COUNT(*) 
                   FROM [dbo].[TEC_Part1_Question] 
                   WHERE (CreatedOn >= @FromDate AND CreatedOn <= @ToDate) 
                   AND QuestionText LIKE @Search 
                   AND (QuestionKey IS NOT NULL) ";

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Using")
                    zSQL += " AND Publish = 1 AND RecordStatus != 99 ";
                else if (StatusFilter == "Unpublished")
                    zSQL += " AND Publish = 0 ";
                else if (StatusFilter == "Deleted")
                    zSQL += " AND RecordStatus = 99 ";
            }

            if (Level > 0)
                zSQL += " AND SkillLevel = @Level ";

            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            try
            {
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();
                    using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                    {
                        zCommand.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = new DateTime(FromDate.Year, FromDate.Month, FromDate.Day, 0, 0, 1);
                        zCommand.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = new DateTime(ToDate.Year, ToDate.Month, ToDate.Day, 23, 59, 59);
                        zCommand.Parameters.Add("@Search", SqlDbType.NVarChar).Value = "%" + Search + "%";
                        zCommand.Parameters.Add("@Level", SqlDbType.Int).Value = Level;
                        return (int)zCommand.ExecuteScalar();
                    }
                }
            }
            catch
            {
                return 0;
            }
        }
        public static JsonResult GetList(string Search, int Level, int PageSize, int PageNumber, string StatusFilter)
        {
            string zMessage = "";
            // ✅ CHỈ THAY ĐỔI DÒNG SQL NÀY - THÊM 6 CỘT IRT
            string zSQL = @"SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, SkillLevel, AmountAccess, 
                           CorrectRate, Anomaly, Publish, RecordStatus,
                           IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, LastAnalyzed
                       FROM [dbo].[TEC_Part1_Question] 
                       WHERE QuestionText LIKE @Search 
                       AND (QuestionKey IS NOT NULL) ";

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Using")
                {
                    zSQL += " AND Publish = 1 AND RecordStatus != 99 ";
                }
                else if (StatusFilter == "Unpublished")
                {
                    zSQL += " AND Publish = 0 ";
                }
                else if (StatusFilter == "Deleted")
                {
                    zSQL += " AND RecordStatus = 99 ";
                }
            }

            if (Level > 0)
                zSQL += " AND SkillLevel = @Level ";

            zSQL += " ORDER BY CreatedOn DESC ";
            zSQL += " OFFSET @PageSize * (@PageNumber - 1) ROWS FETCH NEXT @PageSize ROWS ONLY OPTION(RECOMPILE)";

            DataTable zTable = new DataTable();
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            try
            {
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();
                    using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
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

            // ✅ THAY ĐỔI 2: THÊM 6 PROPERTIES IRT VÀO RESPONSE
            var zDataList = zTable.AsEnumerable().Select(row => new
            {
                QuestionKey = row["QuestionKey"].ToString(),
                QuestionText = row["QuestionText"].ToString() ?? "",
                QuestionImage = row["QuestionImage"].ToString() ?? "",
                QuestionVoice = row["QuestionVoice"].ToString() ?? "",
                SkillLevel = row["SkillLevel"] != DBNull.Value ? Convert.ToInt32(row["SkillLevel"]) : 0,
                AmountAccess = row["AmountAccess"] != DBNull.Value ? Convert.ToInt32(row["AmountAccess"]) : 0,
                Publish = row["Publish"] != DBNull.Value ? Convert.ToBoolean(row["Publish"]) : false,
                RecordStatus = row["RecordStatus"] != DBNull.Value ? Convert.ToInt32(row["RecordStatus"]) : 0,
                CorrectRate = row["CorrectRate"] != DBNull.Value ? Convert.ToDouble(row["CorrectRate"]) : (double?)null,
                Anomaly = row["Anomaly"] != DBNull.Value ? Convert.ToInt32(row["Anomaly"]) : (int?)null,
                // ✅ THÊM 6 DÒNG NÀY
                IrtDifficulty = row["IrtDifficulty"] != DBNull.Value ? Convert.ToDouble(row["IrtDifficulty"]) : (double?)null,
                IrtDiscrimination = row["IrtDiscrimination"] != DBNull.Value ? Convert.ToDouble(row["IrtDiscrimination"]) : (double?)null,
                IrtGuessing = row["IrtGuessing"] != DBNull.Value ? Convert.ToDouble(row["IrtGuessing"]) : (double?)null,
                Quality = row["Quality"].ToString() ?? "",
                ConfidenceLevel = row["ConfidenceLevel"].ToString() ?? "",
                LastAnalyzed = row["LastAnalyzed"] != DBNull.Value ? Convert.ToDateTime(row["LastAnalyzed"]).ToString("yyyy-MM-dd HH:mm") : ""
            }).ToList();

            return new JsonResult(zDataList);
        }

        // ✅ THAY ĐỔI 3: TƯƠNG TỰ CHO HÀM OVERLOAD
        public static JsonResult GetList(string Search, int Level, DateTime FromDate, DateTime ToDate, int PageSize, int PageNumber, string StatusFilter)
        {
            string zMessage = "";
            // ✅ CHỈ THAY ĐỔI DÒNG SQL NÀY - THÊM 6 CỘT IRT
            string zSQL = @"SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, SkillLevel, AmountAccess, 
                           Publish, RecordStatus, Anomaly, CorrectRate,
                           IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, LastAnalyzed
                       FROM [dbo].[TEC_Part1_Question] 
                       WHERE (CreatedOn >= @FromDate AND CreatedOn <= @ToDate) 
                       AND QuestionText LIKE @Search 
                       AND (QuestionKey IS NOT NULL) ";

            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Using")
                {
                    zSQL += " AND Publish = 1 AND RecordStatus != 99 ";
                }
                else if (StatusFilter == "Unpublished")
                {
                    zSQL += " AND Publish = 0 ";
                }
                else if (StatusFilter == "Deleted")
                {
                    zSQL += " AND RecordStatus = 99 ";
                }
            }

            if (Level > 0)
                zSQL += " AND SkillLevel = @Level ";

            zSQL += " ORDER BY CreatedOn DESC ";
            zSQL += " OFFSET @PageSize * (@PageNumber - 1) ROWS FETCH NEXT @PageSize ROWS ONLY OPTION(RECOMPILE)";

            DataTable zTable = new DataTable();
            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            try
            {
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();
                    using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
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

            // ✅ THAY ĐỔI 4: THÊM 6 PROPERTIES IRT VÀO RESPONSE
            var zDataList = zTable.AsEnumerable().Select(row => new
            {
                QuestionKey = row["QuestionKey"].ToString(),
                QuestionText = row["QuestionText"].ToString() ?? "",
                QuestionImage = row["QuestionImage"].ToString() ?? "",
                QuestionVoice = row["QuestionVoice"].ToString() ?? "",
                SkillLevel = row["SkillLevel"] != DBNull.Value ? Convert.ToInt32(row["SkillLevel"]) : 0,
                AmountAccess = row["AmountAccess"] != DBNull.Value ? Convert.ToInt32(row["AmountAccess"]) : 0,
                Publish = row["Publish"] != DBNull.Value ? Convert.ToBoolean(row["Publish"]) : false,
                RecordStatus = row["RecordStatus"] != DBNull.Value ? Convert.ToInt32(row["RecordStatus"]) : 0,
                CorrectRate = row["CorrectRate"] != DBNull.Value ? Convert.ToDouble(row["CorrectRate"]) : (double?)null,
                Anomaly = row["Anomaly"] != DBNull.Value ? Convert.ToInt32(row["Anomaly"]) : (int?)null,
                // ✅ THÊM 6 DÒNG NÀY
                IrtDifficulty = row["IrtDifficulty"] != DBNull.Value ? Convert.ToDouble(row["IrtDifficulty"]) : (double?)null,
                IrtDiscrimination = row["IrtDiscrimination"] != DBNull.Value ? Convert.ToDouble(row["IrtDiscrimination"]) : (double?)null,
                IrtGuessing = row["IrtGuessing"] != DBNull.Value ? Convert.ToDouble(row["IrtGuessing"]) : (double?)null,
                Quality = row["Quality"].ToString() ?? "",
                ConfidenceLevel = row["ConfidenceLevel"].ToString() ?? "",
                LastAnalyzed = row["LastAnalyzed"] != DBNull.Value ? Convert.ToDateTime(row["LastAnalyzed"]).ToString("yyyy-MM-dd HH:mm") : ""
            }).ToList();

            return new JsonResult(zDataList);
        }
        public static int GetAnswerCount(string QuestionKey)
        {
            // Đếm số lượng đáp án chưa bị xóa (RecordStatus != 99)
            string zSQL = @"SELECT COUNT(*) 
                           FROM [dbo].[TEC_Part1_Answer] 
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
                        zCommand.Parameters.Add("@QuestionKey", SqlDbType.NVarChar).Value = QuestionKey;
                        return (int)zCommand.ExecuteScalar();
                    }
                }
            }
            catch
            {
                // Trả về 0 nếu có lỗi
                return 0;
            }
        }

        private static List<QuestionStat> GetQuestionStatistics()
        {
            const int MIN_ATTEMPTS = 30;
            const int SCORE_THRESHOLD = 500;
            const double DIFFICULTY_THRESHOLD = 20;
            const double EASY_THRESHOLD = 70;
            const double HARD_THRESHOLD = 30;

            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            DataTable zTable = new DataTable();

            using (SqlConnection zConnect = new SqlConnection(zConnectionString))
            {
                zConnect.Open();
                // SỬA LỖI SQL TẠI ĐÂY
                string zSQL = @"
                    SELECT 
                        ua.QuestionKey,
                        ua.Part,
                        COUNT(ua.UAnswerKey) AS TotalAttempts,
                        SUM(CASE WHEN ua.IsCorrect = 1 THEN 1 ELSE 0 END) AS CorrectAttempts,
                        SUM(CASE WHEN ua.IsCorrect = 1 AND ISNULL(m.ToeicScoreExam, 0) >= @ScoreThreshold THEN 1 ELSE 0 END) AS CorrectHighScore,
                        SUM(CASE WHEN ua.IsCorrect = 0 AND ISNULL(m.ToeicScoreExam, 0) >= @ScoreThreshold THEN 1 ELSE 0 END) AS IncorrectHighScore,
                        SUM(CASE WHEN ua.IsCorrect = 1 AND ISNULL(m.ToeicScoreExam, 0) < @ScoreThreshold THEN 1 ELSE 0 END) AS CorrectLowScore,
                        SUM(CASE WHEN ua.IsCorrect = 0 AND ISNULL(m.ToeicScoreExam, 0) < @ScoreThreshold THEN 1 ELSE 0 END) AS IncorrectLowScore
                    FROM [dbo].[UserAnswers] ua
                    LEFT JOIN [dbo].[ResultOfUserForTest] r ON ua.ResultKey = r.ResultKey
                    LEFT JOIN [dbo].[EDU_Member] m ON r.MemberKey = m.MemberKey
                    WHERE ua.Part BETWEEN 1 AND 7
                    GROUP BY ua.QuestionKey, ua.Part";

                using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                {
                    zCommand.Parameters.AddWithValue("@ScoreThreshold", SCORE_THRESHOLD);
                    using (SqlDataAdapter zAdapter = new SqlDataAdapter(zCommand))
                    {
                        zAdapter.Fill(zTable);
                    }
                }
            }

            var questionStats = zTable.AsEnumerable().Select(row =>
            {
                int totalAttempts = row.Field<int?>("TotalAttempts") ?? 0;
                int correctAttempts = row.Field<int?>("CorrectAttempts") ?? 0;
                int correctHighScore = row.Field<int?>("CorrectHighScore") ?? 0;
                int incorrectHighScore = row.Field<int?>("IncorrectHighScore") ?? 0;
                int correctLowScore = row.Field<int?>("CorrectLowScore") ?? 0;
                int incorrectLowScore = row.Field<int?>("IncorrectLowScore") ?? 0;

                double? correctRate = null;
                int? anomaly = null;

                if (totalAttempts >= MIN_ATTEMPTS)
                {
                    correctRate = totalAttempts > 0 ? Math.Round((double)correctAttempts / totalAttempts * 100, 2) : 0;

                    double highScoreCorrectRate = (correctHighScore + incorrectHighScore > 0)
                        ? (double)correctHighScore / (correctHighScore + incorrectHighScore) * 100 : 0;
                    double lowScoreCorrectRate = (correctLowScore + incorrectLowScore > 0)
                        ? (double)correctLowScore / (correctLowScore + incorrectLowScore) * 100 : 0;

                    if (correctRate < HARD_THRESHOLD)
                    {
                        anomaly = (lowScoreCorrectRate - highScoreCorrectRate > DIFFICULTY_THRESHOLD) ? 1 : 0;
                    }
                    else if (correctRate > EASY_THRESHOLD)
                    {
                        anomaly = (highScoreCorrectRate < lowScoreCorrectRate) ? 1 : 0;
                    }
                    else
                    {
                        anomaly = (highScoreCorrectRate < lowScoreCorrectRate) ? 1 : 0;
                    }
                }

                return new QuestionStat
                {
                    QuestionKey = row["QuestionKey"].ToString(),
                    Part = Convert.ToInt32(row["Part"]),
                    CorrectRate = correctRate,
                    Anomaly = anomaly
                };
            }).ToList();

            return questionStats;
        }

        public static void UpdateDifficulty()
        {
            try
            {
                List<QuestionStat> statsList = GetQuestionStatistics();
                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();
                    foreach (var stat in statsList)
                    {
                        if (stat.CorrectRate == null) continue;

                        string tableName = $"[dbo].[TEC_Part{stat.Part}_Question]";
                        string updateSQL = $"UPDATE {tableName} SET CorrectRate = @CorrectRate WHERE QuestionKey = @QuestionKey";

                        if (stat.Part == 3 || stat.Part == 4 || stat.Part == 6 || stat.Part == 7)
                        {
                            updateSQL += " AND Parent IS NOT NULL";
                        }

                        using (SqlCommand updateCommand = new SqlCommand(updateSQL, zConnect))
                        {
                            updateCommand.Parameters.AddWithValue("@QuestionKey", stat.QuestionKey);
                            updateCommand.Parameters.AddWithValue("@CorrectRate", stat.CorrectRate);
                            updateCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi khi cập nhật độ khó: " + ex.Message);
            }
        }

        public static void UpdateAnomaly()
        {
            try
            {
                List<QuestionStat> statsList = GetQuestionStatistics();
                string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();
                    foreach (var stat in statsList)
                    {
                        if (stat.Anomaly == null) continue;

                        string tableName = $"[dbo].[TEC_Part{stat.Part}_Question]";
                        string updateSQL = $"UPDATE {tableName} SET Anomaly = @Anomaly WHERE QuestionKey = @QuestionKey";

                        if (stat.Part == 3 || stat.Part == 4 || stat.Part == 6 || stat.Part == 7)
                        {
                            updateSQL += " AND Parent IS NOT NULL";
                        }

                        using (SqlCommand updateCommand = new SqlCommand(updateSQL, zConnect))
                        {
                            updateCommand.Parameters.AddWithValue("@QuestionKey", stat.QuestionKey);
                            updateCommand.Parameters.AddWithValue("@Anomaly", stat.Anomaly);
                            updateCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi khi cập nhật bất thường: " + ex.Message);
            }
        }
    }
}