using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using TNS_EDU_TEST.Areas.Test.Models; 

namespace TNS_EDU_TEST.Areas.Test.Models
{
    public class ResultTestAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public static async Task<(TestInfo, List<TestQuestion>)> GetResultData(Guid testKey, Guid resultKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Lấy thông tin bài thi
                TestInfo testInfo = null;
                string testInfoSql = @"
            SELECT t.Duration, t.TestName, t.CreatedName, r.Time, r.ListeningScore, r.ReadingScore, r.TestScore
            FROM [Test] t
            JOIN [ResultOfUserForTest] r ON t.TestKey = r.TestKey
            WHERE t.TestKey = @TestKey AND r.ResultKey = @ResultKey";
                using (var cmd = new SqlCommand(testInfoSql, conn))
                {
                    cmd.Parameters.AddWithValue("@TestKey", testKey);
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            testInfo = new TestInfo
                            {
                                MaximumTime = reader.GetInt32(0),
                                TestName = reader.GetString(1),
                                Member = reader.GetString(2),
                                TimeSpent = reader.GetInt32(3),
                                ListeningScore = reader.GetInt32(4), 
                                ReadingScore = reader.GetInt32(5),  
                                TestScore = reader.GetInt32(6)
                            };
                        }
                        else
                        {
                            return (null, null);
                        }
                    }
                }

                // Lấy câu hỏi và đáp án
                string sql = @"
                    SELECT 
                        c.TestKey, c.ResultKey, c.QuestionKey, c.Part, c.[Order],
                        q.QuestionText, q.QuestionImage, q.QuestionVoice, q.Parent, q.Ranking, q.Explanation,
                        a.AnswerKey, a.AnswerText, a.AnswerImage, a.AnswerVoice, a.AnswerCorrect, a.Ranking AS AnswerRanking,
                        u.SelectAnswerKey, u.IsCorrect
                    FROM [ContentOfTest] c
                    LEFT JOIN (
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, Ranking, Explanation
                        FROM [TEC_Part1_Question] WHERE RecordStatus != 99
                        UNION
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, Ranking, Explanation
                        FROM [TEC_Part2_Question] WHERE RecordStatus != 99
                        UNION
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, NULL AS Ranking, Explanation
                        FROM [TEC_Part3_Question] WHERE RecordStatus != 99
                        UNION
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, NULL AS Ranking, Explanation
                        FROM [TEC_Part4_Question] WHERE RecordStatus != 99
                        UNION
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, NULL AS Ranking, Explanation
                        FROM [TEC_Part5_Question] WHERE RecordStatus != 99
                        UNION
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, Ranking, Explanation
                        FROM [TEC_Part6_Question] WHERE RecordStatus != 99
                        UNION
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, Ranking, Explanation
                        FROM [TEC_Part7_Question] WHERE RecordStatus != 99
                    ) q ON c.QuestionKey = q.QuestionKey
                    LEFT JOIN (
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect, Ranking
                        FROM [TEC_Part1_Answer] WHERE RecordStatus != 99
                        UNION
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect, Ranking
                        FROM [TEC_Part2_Answer] WHERE RecordStatus != 99
                        UNION
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect, NULL AS Ranking
                        FROM [TEC_Part3_Answer] WHERE RecordStatus != 99
                        UNION
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect, NULL AS Ranking
                        FROM [TEC_Part4_Answer] WHERE RecordStatus != 99
                        UNION
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect, NULL AS Ranking
                        FROM [TEC_Part5_Answer] WHERE RecordStatus != 99
                        UNION
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect, NULL AS Ranking
                        FROM [TEC_Part6_Answer] WHERE RecordStatus != 99
                        UNION
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect, NULL AS Ranking
                        FROM [TEC_Part7_Answer] WHERE RecordStatus != 99
                    ) a ON c.QuestionKey = a.QuestionKey
                    LEFT JOIN [UserAnswers] u ON c.ResultKey = u.ResultKey AND c.QuestionKey = u.QuestionKey AND u.RecordStatus != 99
                    WHERE c.ResultKey = @ResultKey AND c.TestKey = @TestKey
                    ORDER BY c.Part, c.[Order]";

                var questionsDict = new Dictionary<Guid, TestQuestion>();
                var allQuestions = new List<TestQuestion>();

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    cmd.Parameters.AddWithValue("@TestKey", testKey);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Guid questionKey = reader.GetGuid(2);
                            int part = reader.GetInt32(3);

                            if (!questionsDict.ContainsKey(questionKey))
                            {
                                var question = new TestQuestion
                                {
                                    QuestionKey = questionKey,
                                    Part = part,
                                    Order = reader.IsDBNull(4) ? null : (float?)reader.GetDouble(4),
                                    QuestionText = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    QuestionImage = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    QuestionVoice = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    Parent = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                                    Ranking = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                    Explanation = reader.IsDBNull(10) ? null : reader.GetString(10),
                                    Answers = new List<TestAnswer>(),
                                    UserAnswerKey = reader.IsDBNull(17) ? null : reader.GetGuid(17).ToString(),
                                    IsCorrect = reader.IsDBNull(18) ? null : (bool?)reader.GetBoolean(18)
                                };
                                questionsDict[questionKey] = question;
                                allQuestions.Add(question);
                            }

                            if (!reader.IsDBNull(11))
                            {
                                var answer = new TestAnswer
                                {
                                    AnswerKey = reader.GetGuid(11),
                                    QuestionKey = questionKey,
                                    AnswerText = (part <= 2 || reader.IsDBNull(12)) ? null : reader.GetString(12),
                                    AnswerImage = (part <= 2 || reader.IsDBNull(13)) ? null : reader.GetString(13),
                                    AnswerVoice = (part <= 2 || reader.IsDBNull(14)) ? null : reader.GetString(14),
                                    AnswerCorrect = reader.GetBoolean(15),
                                    Ranking = reader.IsDBNull(16) ? 0 : reader.GetInt32(16)
                                };
                                questionsDict[questionKey].Answers.Add(answer);
                            }
                        }
                    }
                }

                foreach (var question in allQuestions)
                {
                    if (question.Part <= 2)
                    {
                        question.Answers.Sort((a, b) => a.Ranking.CompareTo(b.Ranking));
                    }
                }

                var resultQuestions = new List<TestQuestion>();
                foreach (var question in allQuestions)
                {
                    if (question.Parent == null)
                    {
                        resultQuestions.Add(question);
                        question.Children = allQuestions
                            .Where(q => q.Parent == question.QuestionKey)
                            .OrderBy(q => q.Order)
                            .ToList();
                    }
                }

                resultQuestions.Sort((a, b) =>
                {
                    int partCompare = a.Part.CompareTo(b.Part);
                    if (partCompare != 0) return partCompare;
                    return a.Order.Value.CompareTo(b.Order.Value);
                });

                return (testInfo, resultQuestions);
            }
        }
        public class TestInfo
        {
            public int MaximumTime { get; set; }
            public string TestName { get; set; }
            public string Member { get; set; }
            public int TimeSpent { get; set; }
            public int ListeningScore { get; set; } 
            public int ReadingScore { get; set; } 
            public int TestScore { get; set; }
        }
    }
}