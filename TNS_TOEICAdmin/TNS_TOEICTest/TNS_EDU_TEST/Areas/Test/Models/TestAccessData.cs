using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
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

                string sql = @"
            SELECT 
                c.TestKey, c.ResultKey, c.QuestionKey, c.Part, c.[Order],
                q.QuestionText, q.QuestionImage, q.QuestionVoice, q.Parent,
                q.Ranking,
                a.AnswerKey, a.AnswerText, a.AnswerImage, a.AnswerVoice, a.AnswerCorrect,
                a.Ranking AS AnswerRanking,
                u.SelectAnswerKey
            FROM [ContentOfTest] c
            LEFT JOIN (
                SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, Ranking
                FROM [TEC_Part1_Question] WHERE RecordStatus != 99
                UNION
                SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, Ranking
                FROM [TEC_Part2_Question] WHERE RecordStatus != 99
                UNION
                SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, NULL AS Ranking
                FROM [TEC_Part3_Question] WHERE RecordStatus != 99
                UNION
                SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, NULL AS Ranking
                FROM [TEC_Part4_Question] WHERE RecordStatus != 99
                UNION
                SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, NULL AS Ranking
                FROM [TEC_Part5_Question] WHERE RecordStatus != 99
                UNION
                SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, Ranking
                FROM [TEC_Part6_Question] WHERE RecordStatus != 99
                UNION
                SELECT QuestionKey, QuestionText, QuestionImage, QuestionVoice, Parent, Ranking
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
                                    Order = reader.IsDBNull(4) ? null : (float?)reader.GetDouble(4), // Đọc Order dạng float
                                    QuestionText = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    QuestionImage = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    QuestionVoice = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    Parent = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                                    Ranking = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                    Answers = new List<TestAnswer>(),
                                    UserAnswerKey = reader.IsDBNull(16) ? null : reader.GetGuid(16).ToString()
                                };
                                questionsDict[questionKey] = question;
                                allQuestions.Add(question);
                            }

                            if (!reader.IsDBNull(10))
                            {
                                var answer = new TestAnswer
                                {
                                    AnswerKey = reader.GetGuid(10),
                                    QuestionKey = questionKey,
                                    AnswerText = (part <= 2 || reader.IsDBNull(11)) ? null : reader.GetString(11),
                                    AnswerImage = (part <= 2 || reader.IsDBNull(12)) ? null : reader.GetString(12),
                                    AnswerVoice = (part <= 2 || reader.IsDBNull(13)) ? null : reader.GetString(13),
                                    AnswerCorrect = reader.GetBoolean(14),
                                    Ranking = reader.IsDBNull(15) ? 0 : reader.GetInt32(15)
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
                        if (question.Part == 6 || question.Part == 7)
                        {
                            question.Children = allQuestions
                                .Where(q => q.Parent == question.QuestionKey)
                                .OrderBy(q => q.Order) // Sắp xếp theo Order
                                .ToList();
                        }
                        else
                        {
                            question.Children = allQuestions
                                .Where(q => q.Parent == question.QuestionKey)
                                .OrderBy(q => q.Order) // Sắp xếp theo Order
                                .ToList();
                        }
                    }
                }

                // Sắp xếp resultQuestions theo Part và Order
                resultQuestions.Sort((a, b) =>
                {
                    int partCompare = a.Part.CompareTo(b.Part);
                    if (partCompare != 0) return partCompare;
                    return a.Order.Value.CompareTo(b.Order.Value);
                });

                Console.WriteLine($"Total questions from SQL: {allQuestions.Count}");
                foreach (var q in allQuestions)
                {
                    Console.WriteLine($"QuestionKey={q.QuestionKey}, Part={q.Part}, Parent={q.Parent ?? Guid.Empty}, ChildrenCount={q.Children.Count}, Order={q.Order}");
                }
                Console.WriteLine($"Total parent questions returned: {resultQuestions.Count}");
                foreach (var q in resultQuestions)
                {
                    Console.WriteLine($"Parent QuestionKey={q.QuestionKey}, Part={q.Part}, ChildrenCount={q.Children.Count}, Order={q.Order}");
                }

                return (endTime, resultQuestions);
            }
        }


        public static async Task SaveFlaggedQuestion(Guid resultKey, Guid questionKey, bool isFlagged)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string checkSql = @"
                    SELECT COUNT(*) 
                    FROM [FlaggedQuestions] 
                    WHERE ResultKey = @ResultKey AND QuestionKey = @QuestionKey";
                using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    checkCmd.Parameters.AddWithValue("@QuestionKey", questionKey);
                    int count = (int)await checkCmd.ExecuteScalarAsync();

                    if (count == 0 && isFlagged)
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
                    else if (count > 0)
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

        // Cập nhật: Hỗ trợ bỏ chọn đáp án
        public static async Task SaveAnswer(Guid resultKey, Guid questionKey, Guid? selectAnswerKey, int timeSpent, DateTime answerTime, int part)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                int numberOfAnswerChanges = 0;
                string checkSql = @"
                    SELECT NumberOfAnswerChanges 
                    FROM [UserAnswers] 
                    WHERE ResultKey = @ResultKey AND QuestionKey = @QuestionKey";
                using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    checkCmd.Parameters.AddWithValue("@QuestionKey", questionKey);
                    var result = await checkCmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        numberOfAnswerChanges = (int)result + 1;
                    }
                }

                bool? isCorrect = null;
                if (selectAnswerKey.HasValue)
                {
                    string tableName = $"TEC_Part{part}_Answer";
                    string isCorrectSql = $@"
                        SELECT AnswerCorrect 
                        FROM [{tableName}] 
                        WHERE AnswerKey = @AnswerKey";
                    using (var isCorrectCmd = new SqlCommand(isCorrectSql, conn))
                    {
                        isCorrectCmd.Parameters.AddWithValue("@AnswerKey", selectAnswerKey.Value);
                        var result = await isCorrectCmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            isCorrect = (bool)result;
                        }
                    }
                }
                else
                {
                    isCorrect = false; // Khi bỏ chọn, IsCorrect = false
                }

                if (numberOfAnswerChanges == 0)
                {
                    // Insert bản ghi mới
                    string insertSql = @"
                        INSERT INTO [UserAnswers] (UAnswerKey, ResultKey, QuestionKey, SelectAnswerKey, IsCorrect, TimeSpent, AnswerTime, NumberOfAnswerChanges, Part, RecordStatus)
                        VALUES (NEWID(), @ResultKey, @QuestionKey, @SelectAnswerKey, @IsCorrect, @TimeSpent, @AnswerTime, @NumberOfAnswerChanges, @Part, @RecordStatus)";
                    using (var insertCmd = new SqlCommand(insertSql, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@ResultKey", resultKey);
                        insertCmd.Parameters.AddWithValue("@QuestionKey", questionKey);
                        insertCmd.Parameters.AddWithValue("@SelectAnswerKey", (object)selectAnswerKey ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@IsCorrect", (object)isCorrect ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@TimeSpent", timeSpent);
                        insertCmd.Parameters.AddWithValue("@AnswerTime", answerTime);
                        insertCmd.Parameters.AddWithValue("@NumberOfAnswerChanges", numberOfAnswerChanges);
                        insertCmd.Parameters.AddWithValue("@Part", part);
                        insertCmd.Parameters.AddWithValue("@RecordStatus", selectAnswerKey.HasValue ? 0 : 99); // 99 nếu bỏ chọn
                        await insertCmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    // Update bản ghi hiện có
                    string updateSql = @"
                        UPDATE [UserAnswers]
                        SET SelectAnswerKey = @SelectAnswerKey,
                            IsCorrect = @IsCorrect,
                            TimeSpent = @TimeSpent,
                            AnswerTime = @AnswerTime,
                            NumberOfAnswerChanges = @NumberOfAnswerChanges,
                            Part = @Part,
                            RecordStatus = @RecordStatus
                        WHERE ResultKey = @ResultKey AND QuestionKey = @QuestionKey";
                    using (var updateCmd = new SqlCommand(updateSql, conn))
                    {
                        updateCmd.Parameters.AddWithValue("@ResultKey", resultKey);
                        updateCmd.Parameters.AddWithValue("@QuestionKey", questionKey);
                        updateCmd.Parameters.AddWithValue("@SelectAnswerKey", (object)selectAnswerKey ?? DBNull.Value);
                        updateCmd.Parameters.AddWithValue("@IsCorrect", (object)isCorrect ?? DBNull.Value);
                        updateCmd.Parameters.AddWithValue("@TimeSpent", timeSpent);
                        updateCmd.Parameters.AddWithValue("@AnswerTime", answerTime);
                        updateCmd.Parameters.AddWithValue("@NumberOfAnswerChanges", numberOfAnswerChanges);
                        updateCmd.Parameters.AddWithValue("@Part", part);
                        updateCmd.Parameters.AddWithValue("@RecordStatus", selectAnswerKey.HasValue ? 0 : 99); // 99 nếu bỏ chọn
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }
        public static async Task SubmitTest(Guid testKey, Guid resultKey, int remainingMinutes, string userKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // 1. Tính điểm
                int listeningScore = 0;
                int readingScore = 0;

                // Đếm số câu đúng cho Listening (Part 1-4)
                string listeningScoreSql = @"
            SELECT COUNT(*)
            FROM [UserAnswers]
            WHERE ResultKey = @ResultKey AND Part IN (1, 2, 3, 4) 
            AND IsCorrect = 1 AND RecordStatus != 99";
                using (var cmd = new SqlCommand(listeningScoreSql, conn))
                {
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);

                    int correctListeningAnswers = (int)await cmd.ExecuteScalarAsync();
                    listeningScore = correctListeningAnswers * 5;
                }

                // Đếm số câu đúng cho Reading (Part 5-7)
                string readingScoreSql = @"
            SELECT COUNT(*)
            FROM [UserAnswers]
            WHERE ResultKey = @ResultKey AND Part IN (5, 6, 7) 
            AND IsCorrect = 1 AND RecordStatus != 99";
                using (var cmd = new SqlCommand(readingScoreSql, conn))
                {
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);

                    int correctReadingAnswers = (int)await cmd.ExecuteScalarAsync();
                    readingScore = correctReadingAnswers * 5;
                }

                int totalScore = listeningScore + readingScore;

                // 2. Tính thời gian làm bài
                int timeSpent = 120 - remainingMinutes;

                // 3. Cập nhật ResultOfUserForTest (không cập nhật EndTime, thêm Status)
                string updateResultSql = @"
            UPDATE [ResultOfUserForTest]
            SET ListeningScore = @ListeningScore,
                ReadingScore = @ReadingScore,
                TestScore = @TestScore,
                Time = @Time,
                Status = 1
            WHERE ResultKey = @ResultKey AND TestKey = @TestKey";
                using (var cmd = new SqlCommand(updateResultSql, conn))
                {
                    cmd.Parameters.AddWithValue("@ListeningScore", listeningScore);
                    cmd.Parameters.AddWithValue("@ReadingScore", readingScore);
                    cmd.Parameters.AddWithValue("@TestScore", totalScore);
                    cmd.Parameters.AddWithValue("@Time", timeSpent);
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    cmd.Parameters.AddWithValue("@TestKey", testKey);
                    await cmd.ExecuteNonQueryAsync();
                }

                // 4. Xử lý câu trả lời sai và lưu vào UsersError
                string wrongAnswersSql = @"
            SELECT ResultKey, QuestionKey, SelectAnswerKey, Part
            FROM [UserAnswers]
            WHERE ResultKey = @ResultKey AND IsCorrect = 0 AND RecordStatus != 99";
                using (var cmd = new SqlCommand(wrongAnswersSql, conn))
                {
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Guid answerResultKey = reader.GetGuid(0);
                            Guid questionKey = reader.GetGuid(1);

                            // Kiểm tra SelectAnswerKey có NULL không
                            Guid? answerKey = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);
                            if (!answerKey.HasValue)
                            {
                                continue; // Bỏ qua nếu SelectAnswerKey là NULL
                            }

                            int part = reader.GetInt32(3);

                            // Lấy thông tin từ TEC_PartX_Answer
                            string answerTable = $"TEC_Part{part}_Answer";
                            string questionTable = $"TEC_Part{part}_Question";
                            string answerSql = $@"
                        SELECT a.ErrorType, a.GrammarTopic, a.VocabularyTopic, a.Category, a.QuestionKey
                        FROM [{answerTable}] a
                        WHERE a.AnswerKey = @AnswerKey";
                            Guid? errorType = null, grammarTopic = null, vocabularyTopic = null, categoryTopic = null;
                            Guid answerQuestionKey;

                            using (var answerCmd = new SqlCommand(answerSql, conn))
                            {
                                answerCmd.Parameters.AddWithValue("@AnswerKey", answerKey.Value);
                                using (var answerReader = await answerCmd.ExecuteReaderAsync())
                                {
                                    if (await answerReader.ReadAsync())
                                    {
                                        errorType = answerReader.IsDBNull(0) ? (Guid?)null : answerReader.GetGuid(0);
                                        grammarTopic = answerReader.IsDBNull(1) ? (Guid?)null : answerReader.GetGuid(1);
                                        vocabularyTopic = answerReader.IsDBNull(2) ? (Guid?)null : answerReader.GetGuid(2);
                                        categoryTopic = answerReader.IsDBNull(3) ? (Guid?)null : answerReader.GetGuid(3);
                                        answerQuestionKey = answerReader.GetGuid(4);
                                    }
                                    else
                                    {
                                        continue; // Bỏ qua nếu không tìm thấy Answer
                                    }
                                }
                            }

                            // Nếu có cột NULL, lấy từ TEC_PartX_Question
                            if (errorType == null || grammarTopic == null || vocabularyTopic == null || categoryTopic == null)
                            {
                                string questionSql = $@"
                            SELECT ErrorType, GrammarTopic, VocabularyTopic, Category
                            FROM [{questionTable}]
                            WHERE QuestionKey = @QuestionKey";
                                using (var questionCmd = new SqlCommand(questionSql, conn))
                                {
                                    questionCmd.Parameters.AddWithValue("@QuestionKey", answerQuestionKey);
                                    using (var questionReader = await questionCmd.ExecuteReaderAsync())
                                    {
                                        if (await questionReader.ReadAsync())
                                        {
                                            errorType = errorType ?? (questionReader.IsDBNull(0) ? (Guid?)null : questionReader.GetGuid(0));
                                            grammarTopic = grammarTopic ?? (questionReader.IsDBNull(1) ? (Guid?)null : questionReader.GetGuid(1));
                                            vocabularyTopic = vocabularyTopic ?? (questionReader.IsDBNull(2) ? (Guid?)null : questionReader.GetGuid(2));
                                            categoryTopic = categoryTopic ?? (questionReader.IsDBNull(3) ? (Guid?)null : questionReader.GetGuid(3));
                                        }
                                    }
                                }
                            }

                            // Lưu vào UsersError
                            string insertErrorSql = @"
                        INSERT INTO [UsersError] (ErrorKey, AnswerKey, UserKey, ResultKey, ErrorType, GrammarTopic, VocabularyTopic, CategoryTopic, ErrorDate, Part)
                        VALUES (@ErrorKey, @AnswerKey, @UserKey, @ResultKey, @ErrorType, @GrammarTopic, @VocabularyTopic, @CategoryTopic, @ErrorDate, @Part)";
                            using (var insertCmd = new SqlCommand(insertErrorSql, conn))
                            {
                                insertCmd.Parameters.AddWithValue("@ErrorKey", Guid.NewGuid());
                                insertCmd.Parameters.AddWithValue("@AnswerKey", answerKey.Value);
                                insertCmd.Parameters.AddWithValue("@UserKey", userKey);
                                insertCmd.Parameters.AddWithValue("@ResultKey", answerResultKey);
                                insertCmd.Parameters.AddWithValue("@ErrorType", (object)errorType ?? DBNull.Value);
                                insertCmd.Parameters.AddWithValue("@GrammarTopic", (object)grammarTopic ?? DBNull.Value);
                                insertCmd.Parameters.AddWithValue("@VocabularyTopic", (object)vocabularyTopic ?? DBNull.Value);
                                insertCmd.Parameters.AddWithValue("@CategoryTopic", (object)categoryTopic ?? DBNull.Value);
                                insertCmd.Parameters.AddWithValue("@ErrorDate", DateTime.Now);
                                insertCmd.Parameters.AddWithValue("@Part", part);
                                await insertCmd.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }
            }
        }
    }

        public class TestQuestion
    {
        public Guid QuestionKey { get; set; }
        public int Part { get; set; }
        public float? Order { get; set; } 
        public string QuestionText { get; set; }
        public string QuestionImage { get; set; }
        public string QuestionVoice { get; set; }
        public Guid? Parent { get; set; }
        public int Ranking { get; set; }
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