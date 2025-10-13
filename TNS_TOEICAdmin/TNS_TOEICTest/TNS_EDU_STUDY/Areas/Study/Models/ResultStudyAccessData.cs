using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TNS_EDU_TEST.Areas.Test.Models;

namespace TNS_EDU_STUDY.Areas.Study.Models
{
    public class StudyResultInfo
    {
        public string TestName { get; set; }
        public string MemberName { get; set; }
        public int TimeSpent { get; set; }
        public int PracticeScore { get; set; }
        public int MaximumTime { get; set; } // Thêm thuộc tính này
    }

    public static class ResultStudyAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public static async Task<(StudyResultInfo, List<TestQuestion>)> GetStudyResultData(Guid testKey, Guid resultKey, int part)
        {
            StudyResultInfo resultInfo = null;
            var questions = new List<TestQuestion>();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // 1. Lấy thông tin chung, thêm t.Duration
                string infoSql = @"
                    SELECT t.TestName, r.MemberName, r.Time, r.TestScore, t.Duration
                    FROM [Test] t
                    JOIN [ResultOfUserForTest] r ON t.TestKey = r.TestKey
                    WHERE t.TestKey = @TestKey AND r.ResultKey = @ResultKey";
                using (var cmd = new SqlCommand(infoSql, conn))
                {
                    cmd.Parameters.AddWithValue("@TestKey", testKey);
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            resultInfo = new StudyResultInfo
                            {
                                TestName = reader.GetString(0),
                                MemberName = reader.GetString(1),
                                TimeSpent = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                PracticeScore = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                MaximumTime = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                            };
                        }
                    }
                }

                if (resultInfo == null) return (null, null);

                // 2. Lấy chi tiết câu hỏi (giữ nguyên logic)
                string questionTable = $"[dbo].[TEC_Part{part}_Question]";
                string answerTable = $"[dbo].[TEC_Part{part}_Answer]";
                string sql = $@"
                    SELECT 
                        c.QuestionKey, c.Part, c.[Order],
                        q.QuestionText, q.QuestionImage, q.QuestionVoice, q.Parent, q.Ranking as QuestionRanking,
                        a.AnswerKey, a.AnswerText, a.AnswerCorrect, a.Ranking as AnswerRanking,
                        u.SelectAnswerKey, u.IsCorrect, q.Explanation -- Thêm Explanation
                    FROM [ContentOfTest] c
                    LEFT JOIN {questionTable} q ON c.QuestionKey = q.QuestionKey
                    LEFT JOIN {answerTable} a ON q.QuestionKey = a.QuestionKey AND a.RecordStatus != 99
                    LEFT JOIN [UserAnswers] u ON c.ResultKey = u.ResultKey AND c.QuestionKey = u.QuestionKey
                    WHERE c.ResultKey = @ResultKey AND c.TestKey = @TestKey AND c.Part = @Part
                    ORDER BY c.[Order], q.Ranking, a.Ranking";

                var questionsDict = new Dictionary<Guid, TestQuestion>();

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
                                    UserAnswerKey = reader.IsDBNull(12) ? null : reader.GetGuid(12).ToString(),
                                    IsCorrect = reader.IsDBNull(13) ? (bool?)null : reader.GetBoolean(13),
                                    Explanation = reader.IsDBNull(14) ? null : reader.GetString(14)
                                };
                                questionsDict[questionKey] = question;
                                questions.Add(question);
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

                foreach (var q in questions) q.Answers = q.Answers.OrderBy(a => a.Ranking).ToList();
                var resultQuestions = new List<TestQuestion>();
                foreach (var question in questions.OrderBy(q => q.Order))
                {
                    if (question.Parent == null)
                    {
                        question.Children = questions.Where(q => q.Parent == question.QuestionKey).OrderBy(q => q.Ranking).ToList();
                        resultQuestions.Add(question);
                    }
                }

                return (resultInfo, resultQuestions);
            }
        }
    }
}