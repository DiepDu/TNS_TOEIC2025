using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TNS_EDU_TEST.Areas.Test.Models
{
    public class TestAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public static async Task<(DateTime?, List<TestQuestion>)> GetTestData(Guid testKey, Guid resultKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Lấy EndTime từ ResultOfUserForTest
                DateTime? endTime = null;
                string timeSql = @"
                    SELECT EndTime 
                    FROM [ResultOfUserForTest] 
                    WHERE ResultKey = @ResultKey";
                using (var timeCmd = new SqlCommand(timeSql, conn))
                {
                    timeCmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    var result = await timeCmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        endTime = (DateTime)result;
                    }
                    else
                    {
                        Console.WriteLine($"No EndTime found for ResultKey={resultKey}");
                        return (null, null);
                    }
                }

                // Truy vấn chính
                string sql = @"
                    SELECT 
                        c.TestKey, c.ResultKey, c.QuestionKey, c.Part, c.[Order],
                        q.QuestionText, q.QuestionImage, q.QuestionVoice, q.Parent,
                        a.AnswerKey, a.AnswerText, a.AnswerImage, a.AnswerVoice, a.AnswerCorrect, a.Ranking,
                        u.SelectAnswerKey
                    FROM [ContentOfTest] c
                    LEFT JOIN (
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent
                        FROM [TEC_Part1_Question] WHERE RecordStatus != 99
                        UNION
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent
                        FROM [TEC_Part2_Question] WHERE RecordStatus != 99
                        UNION
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent
                        FROM [TEC_Part3_Question] WHERE RecordStatus != 99
                        UNION
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent
                        FROM [TEC_Part4_Question] WHERE RecordStatus != 99
                        UNION
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent
                        FROM [TEC_Part5_Question] WHERE RecordStatus != 99
                        UNION
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent
                        FROM [TEC_Part6_Question] WHERE RecordStatus != 99
                        UNION
                        SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent
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
                    LEFT JOIN [UserAnswers] u ON c.ResultKey = u.ResultKey AND c.QuestionKey = u.QuestionKey
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
                                    Order = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                                    QuestionText = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    QuestionImage = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    QuestionVoice = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    Parent = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                                    Answers = new List<TestAnswer>(),
                                    UserAnswerKey = reader.IsDBNull(15) ? null : reader.GetGuid(15).ToString()
                                };
                                questionsDict[questionKey] = question;
                                allQuestions.Add(question);
                            }

                            if (!reader.IsDBNull(9))
                            {
                                var answer = new TestAnswer
                                {
                                    AnswerKey = reader.GetGuid(9),
                                    QuestionKey = questionKey,
                                    AnswerText = (part <= 2 || reader.IsDBNull(10)) ? null : reader.GetString(10),
                                    AnswerImage = (part <= 2 || reader.IsDBNull(11)) ? null : reader.GetString(11),
                                    AnswerVoice = (part <= 2 || reader.IsDBNull(12)) ? null : reader.GetString(12),
                                    AnswerCorrect = reader.GetBoolean(13),
                                    Ranking = reader.IsDBNull(14) ? 0 : reader.GetInt32(14)
                                };
                                questionsDict[questionKey].Answers.Add(answer);
                            }
                        }
                    }
                }

                // Sắp xếp đáp án cho Part 1 và 2
                foreach (var question in allQuestions)
                {
                    if (question.Part <= 2)
                    {
                        question.Answers.Sort((a, b) => a.Ranking.CompareTo(b.Ranking));
                    }
                }

                // Nhóm câu hỏi cha-con
                var resultQuestions = new List<TestQuestion>();
                foreach (var question in allQuestions)
                {
                    if (question.Parent == null)
                    {
                        resultQuestions.Add(question);
                    }
                    else
                    {
                        var parentQuestion = questionsDict[question.Parent.Value];
                        parentQuestion.Children.Add(question);
                    }
                }

                // Log chi tiết
                Console.WriteLine($"Total questions from SQL: {allQuestions.Count}");
                foreach (var q in allQuestions)
                {
                    Console.WriteLine($"QuestionKey={q.QuestionKey}, Part={q.Part}, Parent={q.Parent ?? Guid.Empty}, ChildrenCount={q.Children.Count}");
                }
                Console.WriteLine($"Total parent questions returned: {resultQuestions.Count}");
                foreach (var q in resultQuestions)
                {
                    Console.WriteLine($"Parent QuestionKey={q.QuestionKey}, Part={q.Part}, ChildrenCount={q.Children.Count}");
                }

                return (endTime, resultQuestions);
            }
        }
        public static async Task SaveFlaggedQuestion(Guid resultKey, Guid questionKey, bool isFlagged)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Kiểm tra xem bản ghi đã tồn tại chưa
                string checkSql = @"
            SELECT COUNT(*) 
            FROM [FlaggedQuestions] 
            WHERE ResultKey = @ResultKey AND QuestionKey = @QuestionKey";
                using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    checkCmd.Parameters.AddWithValue("@QuestionKey", questionKey);
                    int count = (int)await checkCmd.ExecuteScalarAsync();

                    if (count == 0 && isFlagged) // Nếu chưa tồn tại và đang gắn cờ, thì insert
                    {
                        string insertSql = @"
                    INSERT INTO [FlaggedQuestions] (FlagKey, ResultKey, QuestionKey, IsFlagged, CreateOn)
                    VALUES (NEWID(), @ResultKey, @QuestionKey, @IsFlagged, @CreateOn)";
                        using (var insertCmd = new SqlCommand(insertSql, conn))
                        {
                            insertCmd.Parameters.AddWithValue("@ResultKey", resultKey);
                            insertCmd.Parameters.AddWithValue("@QuestionKey", questionKey);
                            insertCmd.Parameters.AddWithValue("@IsFlagged", isFlagged);
                            insertCmd.Parameters.AddWithValue("@CreateOn", DateTime.Now);
                  
                            await insertCmd.ExecuteNonQueryAsync();
                        }
                    }
                    else if (count > 0) // Nếu đã tồn tại, thì update
                    {
                        string updateSql = @"
                    UPDATE [FlaggedQuestions]
                    SET IsFlagged = @IsFlagged, UpdateOn = @UpdateOn
                    WHERE ResultKey = @ResultKey AND QuestionKey = @QuestionKey";
                        using (var updateCmd = new SqlCommand(updateSql, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@ResultKey", resultKey);
                            updateCmd.Parameters.AddWithValue("@QuestionKey", questionKey);
                            updateCmd.Parameters.AddWithValue("@IsFlagged", isFlagged);
                            updateCmd.Parameters.AddWithValue("@UpdateOn", DateTime.Now);
                            await updateCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }

        public static async Task<List<Guid>> GetFlaggedQuestions(Guid resultKey)
        {
            var flaggedQuestions = new List<Guid>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string sql = @"
            SELECT QuestionKey 
            FROM [FlaggedQuestions] 
            WHERE ResultKey = @ResultKey AND IsFlagged = 1";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            flaggedQuestions.Add(reader.GetGuid(0));
                        }
                    }
                }
            }
            return flaggedQuestions;
        }
    }

    public class TestQuestion
    {
        public Guid QuestionKey { get; set; }
        public int Part { get; set; }
        public int? Order { get; set; }
        public string QuestionText { get; set; }
        public string QuestionImage { get; set; }
        public string QuestionVoice { get; set; }
        public Guid? Parent { get; set; }
        public List<TestAnswer> Answers { get; set; }
        public string UserAnswerKey { get; set; }
        public List<TestQuestion> Children { get; set; } = new List<TestQuestion>();
    }

    public class TestAnswer
    {
        public Guid AnswerKey { get; set; }
        public Guid QuestionKey { get; set; }
        public string AnswerText { get; set; }
        public string AnswerImage { get; set; }
        public string AnswerVoice { get; set; }
        public bool AnswerCorrect { get; set; }
        public int Ranking { get; set; }
    }
}