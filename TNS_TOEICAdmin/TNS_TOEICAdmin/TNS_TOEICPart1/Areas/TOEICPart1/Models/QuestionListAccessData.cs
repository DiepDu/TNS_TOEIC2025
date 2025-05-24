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
        // Phương thức không có ngày
        public static JsonResult GetList(string Search, int Level, int PageSize, int PageNumber, string StatusFilter)
        {
            string zMessage = "";
            string zSQL = @"SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, SkillLevel, AmountAccess, CorrectRate, Anomaly, Publish, RecordStatus 
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
                Anomaly = row["Anomaly"] != DBNull.Value ? Convert.ToInt32(row["Anomaly"]) : (int?)null
            }).ToList();

            return new JsonResult(zDataList);
        }

        // Phương thức có ngày
        public static JsonResult GetList(string Search, int Level, DateTime FromDate, DateTime ToDate, int PageSize, int PageNumber, string StatusFilter)
        {
            string zMessage = "";
            string zSQL = @"SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, SkillLevel, AmountAccess, Publish, RecordStatus, Anomaly, CorrectRate
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
                Anomaly = row["Anomaly"] != DBNull.Value ? Convert.ToInt32(row["Anomaly"]) : (int?)null
            }).ToList();

            return new JsonResult(zDataList);
        }
        public static void UpdateStatistics()
        {
            const int MIN_ATTEMPTS = 30;
            const int SCORE_THRESHOLD = 500;
            const double DIFFICULTY_THRESHOLD = 20;
            const double EASY_THRESHOLD = 70;
            const double HARD_THRESHOLD = 30;

            string zConnectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
            try
            {
                using (SqlConnection zConnect = new SqlConnection(zConnectionString))
                {
                    zConnect.Open();

                    // Truy vấn dữ liệu cho tất cả 7 Part
                    string zSQL = @"
                        SELECT 
                            ua.QuestionKey,
                            ua.Part,
                            COUNT(ua.UAnswerKey) AS TotalAttempts,
                            SUM(CASE WHEN ua.IsCorrect = 1 THEN 1 ELSE 0 END) AS CorrectAttempts,
                            SUM(CASE WHEN ua.IsCorrect = 1 AND m.ToeicScoreExam >= @ScoreThreshold THEN 1 ELSE 0 END) AS CorrectHighScore,
                            SUM(CASE WHEN ua.IsCorrect = 0 AND m.ToeicScoreExam >= @ScoreThreshold THEN 1 ELSE 0 END) AS IncorrectHighScore,
                            SUM(CASE WHEN ua.IsCorrect = 1 AND m.ToeicScoreExam < @ScoreThreshold THEN 1 ELSE 0 END) AS CorrectLowScore,
                            SUM(CASE WHEN ua.IsCorrect = 0 AND m.ToeicScoreExam < @ScoreThreshold THEN 1 ELSE 0 END) AS IncorrectLowScore
                        FROM [dbo].[UserAnswers] ua
                        LEFT JOIN [dbo].[ResultOfUserForTest] r ON ua.ResultKey = r.ResultKey
                        LEFT JOIN [dbo].[EDU_Member] m ON r.MemberKey = m.MemberKey
                        WHERE ua.Part BETWEEN 1 AND 7 -- Lấy tất cả 7 Part
                        AND m.ToeicScoreExam IS NOT NULL
                        GROUP BY ua.QuestionKey, ua.Part";

                    DataTable zTable = new DataTable();
                    using (SqlCommand zCommand = new SqlCommand(zSQL, zConnect))
                    {
                        zCommand.Parameters.AddWithValue("@ScoreThreshold", SCORE_THRESHOLD);
                        using (SqlDataAdapter zAdapter = new SqlDataAdapter(zCommand))
                        {
                            zAdapter.Fill(zTable);
                        }
                    }

                    var questionStats = zTable.AsEnumerable().Select(row =>
                    {
                        int totalAttempts = row["TotalAttempts"] != DBNull.Value ? Convert.ToInt32(row["TotalAttempts"]) : 0;
                        int correctAttempts = row["CorrectAttempts"] != DBNull.Value ? Convert.ToInt32(row["CorrectAttempts"]) : 0;
                        int correctHighScore = row["CorrectHighScore"] != DBNull.Value ? Convert.ToInt32(row["CorrectHighScore"]) : 0;
                        int incorrectHighScore = row["IncorrectHighScore"] != DBNull.Value ? Convert.ToInt32(row["IncorrectHighScore"]) : 0;
                        int correctLowScore = row["CorrectLowScore"] != DBNull.Value ? Convert.ToInt32(row["CorrectLowScore"]) : 0;
                        int incorrectLowScore = row["IncorrectLowScore"] != DBNull.Value ? Convert.ToInt32(row["IncorrectLowScore"]) : 0;
                        int part = Convert.ToInt32(row["Part"]);

                        double? correctRate = null;
                        int? anomaly = null;

                        if (totalAttempts >= MIN_ATTEMPTS)
                        {
                            // Tính CorrectRate
                            correctRate = totalAttempts > 0 ? Math.Round((double)correctAttempts / totalAttempts * 100, 2) : 0;

                            // Tính tỷ lệ làm đúng theo nhóm
                            double highScoreCorrectRate = correctHighScore + incorrectHighScore > 0
                                ? (double)correctHighScore / (correctHighScore + incorrectHighScore) * 100 : 0;
                            double lowScoreCorrectRate = correctLowScore + incorrectLowScore > 0
                                ? (double)correctLowScore / (correctLowScore + incorrectLowScore) * 100 : 0;

                            // Xác định bất thường
                            if (correctRate < HARD_THRESHOLD) // Câu khó
                            {
                                anomaly = (lowScoreCorrectRate - highScoreCorrectRate > DIFFICULTY_THRESHOLD) ? 1 : 0;
                            }
                            else if (correctRate > EASY_THRESHOLD) // Câu dễ
                            {
                                anomaly = ((100 - lowScoreCorrectRate) - (100 - highScoreCorrectRate) > DIFFICULTY_THRESHOLD) ? 1 : 0;
                            }
                            else
                            {
                                anomaly = 0; // Câu trung bình
                            }
                        }

                        return new
                        {
                            QuestionKey = row["QuestionKey"].ToString(),
                            Part = part,
                            CorrectRate = correctRate,
                            Anomaly = anomaly
                        };
                    }).ToList();

                    // Cập nhật dữ liệu cho từng Part
                    foreach (var stat in questionStats)
                    {
                        string tableName = $"[dbo].[TEC_Part{stat.Part}_Question]";
                        string updateSQL = $@"
                            UPDATE {tableName}
                            SET CorrectRate = @CorrectRate, 
                                Anomaly = @Anomaly
                            WHERE QuestionKey = @QuestionKey";
                        using (SqlCommand updateCommand = new SqlCommand(updateSQL, zConnect))
                        {
                            updateCommand.Parameters.AddWithValue("@QuestionKey", stat.QuestionKey);
                            updateCommand.Parameters.AddWithValue("@CorrectRate", (object)stat.CorrectRate ?? DBNull.Value);
                            updateCommand.Parameters.AddWithValue("@Anomaly", (object)stat.Anomaly ?? DBNull.Value);
                            updateCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error updating statistics: " + ex.Message);
            }
        }
    }
}