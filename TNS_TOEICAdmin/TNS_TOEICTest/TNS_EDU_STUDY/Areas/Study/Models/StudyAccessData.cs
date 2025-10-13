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

        // Dán vào file: TNS_EDU_STUDY/Areas/Study/Models/StudyAccessData.cs

        public static async Task SubmitStudySession(Guid resultKey, Guid memberKey, int partNumber)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    Guid testKey = Guid.Empty;
                    int totalQuestions = 0;
                    int correctAnswers = 0;

                    // BƯỚC 1: TỐI ƯU HÓA - Lấy TestKey VÀ tổng số câu hỏi (TotalQuestion) từ một lần truy vấn.
                    // Sử dụng JOIN để lấy thông tin từ cả hai bảng ResultOfUserForTest và Test.
                    string getInfoSql = @"
                SELECT r.TestKey, t.TotalQuestion
                FROM [ResultOfUserForTest] r
                INNER JOIN [Test] t ON r.TestKey = t.TestKey
                WHERE r.ResultKey = @ResultKey";

                    using (var cmd = new SqlCommand(getInfoSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                testKey = reader.GetGuid(0);
                                totalQuestions = reader.GetInt32(1);
                            }
                            else
                            {
                                // Nếu không tìm thấy, không thể tiếp tục.
                                throw new Exception($"Không tìm thấy bản ghi kết quả hoặc bài test tương ứng với ResultKey: {resultKey}");
                            }
                        } // Reader được tự động đóng ở đây
                    }

                    // BƯỚC 2: Chỉ đếm số câu trả lời đúng từ bảng UserAnswers.
                    string countCorrectSql = @"
                SELECT COUNT(ua.QuestionKey)
                FROM [UserAnswers] ua
                WHERE ua.ResultKey = @ResultKey AND ua.IsCorrect = 1 AND ua.RecordStatus != 99";

                    using (var cmd = new SqlCommand(countCorrectSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                        var result = await cmd.ExecuteScalarAsync();
                        correctAnswers = (result == null || result == DBNull.Value) ? 0 : (int)result;
                    }

                    // BƯỚC 3: Tính điểm phần trăm với logic đã được xác nhận là đúng.
                    int practiceScore = (totalQuestions > 0)
                        ? (int)Math.Round((double)correctAnswers / totalQuestions * 100)
                        : 0;

                    // 4. Cập nhật bảng ResultOfUserForTest (Giữ nguyên)
                    string updateResultSql = @"
                UPDATE [ResultOfUserForTest] 
                SET EndTime = @EndTime, 
                    TestScore = @PracticeScore,
                    Status = 1
                WHERE ResultKey = @ResultKey";
                    using (var cmd = new SqlCommand(updateResultSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@EndTime", DateTime.Now);
                        cmd.Parameters.AddWithValue("@PracticeScore", practiceScore);
                        cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 5. Cập nhật điểm luyện tập cao nhất cho Member (Giữ nguyên)
                    string partScoreColumn = $"PracticeScore_Part{partNumber}";
                    string updateMemberSql = $@"
                UPDATE [EDU_Member] 
                SET {partScoreColumn} = @PracticeScore 
                WHERE MemberKey = @MemberKey AND ISNULL({partScoreColumn}, 0) < @PracticeScore";
                    using (var cmd = new SqlCommand(updateMemberSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@PracticeScore", practiceScore);
                        cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 5. Lưu lại thông tin các câu trả lời sai (Logic giữ nguyên từ SubmitTest)
                    string wrongAnswersSql = $@"
    SELECT ResultKey, QuestionKey, SelectAnswerKey, Part
    FROM [UserAnswers]
    WHERE ResultKey = @ResultKey 
      AND (IsCorrect = 0 OR NumberOfAnswerChanges > 2) 
      AND RecordStatus != 99";
                    // Dùng một List để lưu tạm dữ liệu vì không thể dùng nhiều DataReader cùng lúc
                    var wrongAnswerList = new List<dynamic>();
                    using (var cmd = new SqlCommand(wrongAnswersSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Guid? answerKey = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);
                                if (answerKey.HasValue) // Chỉ xử lý những câu đã được chọn đáp án
                                {
                                    wrongAnswerList.Add(new
                                    {
                                        AnswerResultKey = reader.GetGuid(0),
                                        QuestionKey = reader.GetGuid(1),
                                        AnswerKey = answerKey.Value,
                                        Part = reader.GetInt32(3)
                                    });
                                }
                            }
                        }
                    }

                    // Vòng lặp xử lý từng câu sai
                    foreach (var wrongAnswer in wrongAnswerList)
                    {
                        string answerTable = $"TEC_Part{wrongAnswer.Part}_Answer";
                        string questionTable = $"TEC_Part{wrongAnswer.Part}_Question";

                        Guid? errorType = null, grammarTopic = null, vocabularyTopic = null, categoryTopic = null;
                        Guid answerQuestionKey = Guid.Empty;
                        int? skillLevel = null;

                        // Lấy thông tin từ bảng Answer trước
                        string answerSql = $@"
                    SELECT a.ErrorType, a.GrammarTopic, a.VocabularyTopic, a.Category, a.QuestionKey
                    FROM [{answerTable}] a
                    WHERE a.AnswerKey = @AnswerKey";

                        using (var answerCmd = new SqlCommand(answerSql, conn, transaction))
                        {
                            answerCmd.Parameters.AddWithValue("@AnswerKey", wrongAnswer.AnswerKey);
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
                                else continue;
                            }
                        }

                        // Lấy SkillLevel từ Question
                        string skillLevelSql = $"SELECT SkillLevel FROM [{questionTable}] WHERE QuestionKey = @QuestionKey";
                        using (var skillLevelCmd = new SqlCommand(skillLevelSql, conn, transaction))
                        {
                            skillLevelCmd.Parameters.AddWithValue("@QuestionKey", answerQuestionKey);
                            var result = await skillLevelCmd.ExecuteScalarAsync();
                            if (result != null && result != DBNull.Value)
                            {
                                skillLevel = Convert.ToInt32(result);
                            }
                        }

                        // Nếu có thuộc tính nào null, lấy bổ sung từ bảng Question
                        if (errorType == null || grammarTopic == null || vocabularyTopic == null || categoryTopic == null)
                        {
                            string questionSql = $@"
                        SELECT ErrorType, GrammarTopic, VocabularyTopic, Category
                        FROM [{questionTable}] WHERE QuestionKey = @QuestionKey";
                            using (var questionCmd = new SqlCommand(questionSql, conn, transaction))
                            {
                                questionCmd.Parameters.AddWithValue("@QuestionKey", answerQuestionKey);
                                using (var questionReader = await questionCmd.ExecuteReaderAsync())
                                {
                                    if (await questionReader.ReadAsync())
                                    {
                                        errorType ??= questionReader.IsDBNull(0) ? (Guid?)null : questionReader.GetGuid(0);
                                        grammarTopic ??= questionReader.IsDBNull(1) ? (Guid?)null : questionReader.GetGuid(1);
                                        vocabularyTopic ??= questionReader.IsDBNull(2) ? (Guid?)null : questionReader.GetGuid(2);
                                        categoryTopic ??= questionReader.IsDBNull(3) ? (Guid?)null : questionReader.GetGuid(3);
                                    }
                                }
                            }
                        }

                        // Ghi vào bảng UsersError
                        string insertErrorSql = @"
                    INSERT INTO [UsersError] (ErrorKey, AnswerKey, UserKey, ResultKey, ErrorType, GrammarTopic, VocabularyTopic, CategoryTopic, ErrorDate, Part, SkillLevel)
                    VALUES (@ErrorKey, @AnswerKey, @UserKey, @ResultKey, @ErrorType, @GrammarTopic, @VocabularyTopic, @CategoryTopic, @ErrorDate, @Part, @SkillLevel)";
                        using (var insertCmd = new SqlCommand(insertErrorSql, conn, transaction))
                        {
                            insertCmd.Parameters.AddWithValue("@ErrorKey", Guid.NewGuid());
                            insertCmd.Parameters.AddWithValue("@AnswerKey", wrongAnswer.AnswerKey);
                            insertCmd.Parameters.AddWithValue("@UserKey", memberKey.ToString());
                            insertCmd.Parameters.AddWithValue("@ResultKey", wrongAnswer.AnswerResultKey);
                            insertCmd.Parameters.AddWithValue("@ErrorType", (object)errorType ?? DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@GrammarTopic", (object)grammarTopic ?? DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@VocabularyTopic", (object)vocabularyTopic ?? DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@CategoryTopic", (object)categoryTopic ?? DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@ErrorDate", DateTime.Now);
                            insertCmd.Parameters.AddWithValue("@Part", wrongAnswer.Part);
                            insertCmd.Parameters.AddWithValue("@SkillLevel", (object)skillLevel ?? DBNull.Value);
                            await insertCmd.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync(); // Hoàn tất transaction nếu mọi thứ thành công
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync(); // Hoàn tác tất cả thay đổi nếu có lỗi
                    throw; // Ném lại lỗi để lớp gọi có thể xử lý
                }
            }
        }
    }
}