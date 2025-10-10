using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TNS_EDU_TEST.Areas.Test.Models;

namespace TNS_EDU_STUDY.Areas.Study.Models
{
    public class StudySessionData
    {
        public int Duration { get; set; } 
        public int TimeSpent { get; set; } 
        public List<TestQuestion> Questions { get; set; }
        public int? TestScore { get; set; }
        public int Status { get; set; }
    }

    public static class StudyAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public static async Task<StudySessionData> GetStudySessionData(Guid testKey, Guid resultKey, int part)
        {
            var sessionData = new StudySessionData();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // 1. SỬA CÂU SQL ĐỂ LẤY THÊM "r.Status"
                string timeSql = @"
                    SELECT t.Duration, r.Time, r.TestScore, r.Status 
                    FROM [dbo].[Test] t JOIN [dbo].[ResultOfUserForTest] r ON t.TestKey = r.TestKey
                    WHERE r.ResultKey = @ResultKey AND t.TestKey = @TestKey";

                using (var timeCmd = new SqlCommand(timeSql, conn))
                {
                    timeCmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    timeCmd.Parameters.AddWithValue("@TestKey", testKey);
                    using (var reader = await timeCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            sessionData.Duration = reader.GetInt32(0);
                            sessionData.TimeSpent = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                            sessionData.TestScore = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                            sessionData.Status = reader.GetInt32(3); // GÁN GIÁ TRỊ CHO Status
                        }
                        else { return null; }
                    }
                }

                // Nếu status là 99, không cần lấy câu hỏi, trả về luôn để PageModel xử lý
                if (sessionData.Status == 99)
                {
                    return sessionData;
                }

                string questionTable = $"[dbo].[TEC_Part{part}_Question]";
                string answerTable = $"[dbo].[TEC_Part{part}_Answer]";
                // 2. THÊM ĐIỀU KIỆN "a.RecordStatus != 99" VÀO LEFT JOIN
                string sql = $@"
                    SELECT 
                        c.QuestionKey, c.Part, c.[Order],
                        q.QuestionText, q.QuestionImage, q.QuestionVoice, q.Parent, q.Ranking as QuestionRanking,
                        a.AnswerKey, a.AnswerText, a.AnswerCorrect, a.Ranking as AnswerRanking,
                        u.SelectAnswerKey
                    FROM [ContentOfTest] c
                    LEFT JOIN {questionTable} q ON c.QuestionKey = q.QuestionKey
                    LEFT JOIN {answerTable} a ON q.QuestionKey = a.QuestionKey AND a.RecordStatus != 99
                    LEFT JOIN [UserAnswers] u ON c.ResultKey = u.ResultKey AND c.QuestionKey = u.QuestionKey
                    WHERE c.ResultKey = @ResultKey AND c.TestKey = @TestKey AND c.Part = @Part
                    ORDER BY c.[Order], q.Ranking, a.Ranking";

                var questionsDict = new Dictionary<Guid, TestQuestion>();
                var allQuestions = new List<TestQuestion>();

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    cmd.Parameters.AddWithValue("@TestKey", testKey);
                    cmd.Parameters.AddWithValue("@Part", part);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Guid questionKey = reader.GetGuid(0);
                            if (!questionsDict.ContainsKey(questionKey))
                            {
                                var question = new TestQuestion
                                {
                                    QuestionKey = questionKey,
                                    Part = reader.GetInt32(1),
                                    Order = (float)reader.GetDouble(2),
                                    QuestionText = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    QuestionImage = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    QuestionVoice = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    Parent = reader.IsDBNull(6) ? (Guid?)null : reader.GetGuid(6),
                                    Ranking = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                                    Answers = new List<TestAnswer>(),
                                    UserAnswerKey = reader.IsDBNull(12) ? null : reader.GetGuid(12).ToString()
                                };
                                questionsDict[questionKey] = question;
                                allQuestions.Add(question);
                            }
                            if (!reader.IsDBNull(8))
                            {
                                questionsDict[questionKey].Answers.Add(new TestAnswer
                                {
                                    AnswerKey = reader.GetGuid(8),
                                    QuestionKey = questionKey,
                                    AnswerText = reader.IsDBNull(9) ? null : reader.GetString(9),
                                    AnswerCorrect = reader.GetBoolean(10),
                                    Ranking = reader.IsDBNull(11) ? 0 : reader.GetInt32(11)
                                });
                            }
                        }
                    }
                }

                foreach (var q in allQuestions) q.Answers = q.Answers.OrderBy(a => a.Ranking).ToList();
                var resultQuestions = new List<TestQuestion>();
                foreach (var question in allQuestions.OrderBy(q => q.Order))
                {
                    if (question.Parent == null)
                    {
                        question.Children = allQuestions.Where(q => q.Parent == question.QuestionKey).OrderBy(q => q.Ranking).ToList();
                        resultQuestions.Add(question);
                    }
                }
                sessionData.Questions = resultQuestions;
                return sessionData;
            }
        }


        public static async Task UpdateTimeSpent(Guid resultKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"UPDATE [ResultOfUserForTest] SET Time = ISNULL(Time, 0) + 1 WHERE ResultKey = @ResultKey";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task SubmitStudySession(Guid resultKey, Guid memberKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                int correctAnswers = 0, totalQuestions = 0;
                string sql = @"
                    SELECT COUNT(CASE WHEN ua.IsCorrect = 1 THEN 1 END), t.TotalQuestion
                    FROM [UserAnswers] ua JOIN [Test] t ON ua.TestKey = t.TestKey
                    WHERE ua.ResultKey = @ResultKey GROUP BY t.TotalQuestion";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if(await reader.ReadAsync()){
                            correctAnswers = reader.GetInt32(0);
                            totalQuestions = reader.GetInt32(1);
                        }
                    }
                }
                int studyScore = (totalQuestions > 0) ? (int)Math.Round((double)correctAnswers / totalQuestions * 100) : 0;
                string updateResultSql = @"UPDATE [ResultOfUserForTest] SET EndTime = @EndTime, TestScore = @TestScore, Status = 1 WHERE ResultKey = @ResultKey";
                using (var cmd = new SqlCommand(updateResultSql, conn))
                {
                    cmd.Parameters.AddWithValue("@EndTime", DateTime.Now);
                    cmd.Parameters.AddWithValue("@TestScore", studyScore);
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    await cmd.ExecuteNonQueryAsync();
                }
                string updateMemberSql = @"UPDATE [EDU_Member] SET ToeicScoreStudy = @StudyScore WHERE MemberKey = @MemberKey AND ISNULL(ToeicScoreStudy, 0) < @StudyScore";
                using (var cmd = new SqlCommand(updateMemberSql, conn))
                {
                    cmd.Parameters.AddWithValue("@StudyScore", studyScore);
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}