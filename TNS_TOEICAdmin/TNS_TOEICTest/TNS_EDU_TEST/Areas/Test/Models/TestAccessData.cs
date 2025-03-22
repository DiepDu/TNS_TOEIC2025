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
                        return (null, null); // Không tìm thấy ResultKey
                    }
                }

                // Truy vấn chính: Join ContentOfTest, TEC_PartX_Question, TEC_PartX_Answer, UserAnswers
                string sql = @"
                    SELECT 
                        c.TestKey, c.ResultKey, c.QuestionKey, c.Part, c.[Order],
                        q.QuestionText, q.QuestionImage, q.QuestionVoice, q.Parent,
                        a.AnswerKey, a.AnswerText, a.AnswerImage, a.AnswerVoice, a.AnswerCorrect,
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
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect
                        FROM [TEC_Part1_Answer] WHERE RecordStatus != 99
                        UNION
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect
                        FROM [TEC_Part2_Answer] WHERE RecordStatus != 99
                        UNION
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect
                        FROM [TEC_Part3_Answer] WHERE RecordStatus != 99
                        UNION
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect
                        FROM [TEC_Part4_Answer] WHERE RecordStatus != 99
                        UNION
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect
                        FROM [TEC_Part5_Answer] WHERE RecordStatus != 99
                        UNION
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect
                        FROM [TEC_Part6_Answer] WHERE RecordStatus != 99
                        UNION
                        SELECT AnswerKey, QuestionKey, AnswerText, AnswerImage, AnswerVoice, AnswerCorrect
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
                            Guid questionKey = reader.GetGuid(2); // c.QuestionKey
                            if (!questionsDict.ContainsKey(questionKey))
                            {
                                var question = new TestQuestion
                                {
                                    QuestionKey = questionKey,
                                    Part = reader.GetInt32(3), // c.Part
                                    Order = reader.IsDBNull(4) ? null : reader.GetInt32(4), // c.Order
                                    QuestionText = reader.IsDBNull(5) ? null : reader.GetString(5), // q.QuestionText
                                    QuestionImage = reader.IsDBNull(6) ? null : reader.GetString(6), // q.QuestionImage
                                    QuestionVoice = reader.IsDBNull(7) ? null : reader.GetString(7), // q.QuestionVoice
                                    Parent = reader.IsDBNull(8) ? null : reader.GetGuid(8), // q.Parent
                                    Answers = new List<TestAnswer>(),
                                    UserAnswerKey = reader.IsDBNull(14) ? null : reader.GetGuid(14).ToString() // u.SelectAnswerKey
                                };
                                questionsDict[questionKey] = question;
                                allQuestions.Add(question);
                            }

                            // Thêm đáp án nếu có
                            if (!reader.IsDBNull(9)) // a.AnswerKey
                            {
                                var answer = new TestAnswer
                                {
                                    AnswerKey = reader.GetGuid(9), // a.AnswerKey
                                    QuestionKey = questionKey,
                                    AnswerText = reader.IsDBNull(10) ? null : reader.GetString(10), // a.AnswerText
                                    AnswerImage = reader.IsDBNull(11) ? null : reader.GetString(11), // a.AnswerImage
                                    AnswerVoice = reader.IsDBNull(12) ? null : reader.GetString(12), // a.AnswerVoice
                                    AnswerCorrect = reader.GetBoolean(13) // a.AnswerCorrect
                                };
                                questionsDict[questionKey].Answers.Add(answer);
                            }
                        }
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
                        var parentQuestion = questionsDict.ContainsKey(question.Parent.Value)
                            ? questionsDict[question.Parent.Value]
                            : null;
                        if (parentQuestion != null)
                        {
                            parentQuestion.Children.Add(question);
                        }
                    }
                }

                return (endTime, resultQuestions);
            }
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
        public string UserAnswerKey { get; set; } // Lưu AnswerKey từ UserAnswers
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
    }
}