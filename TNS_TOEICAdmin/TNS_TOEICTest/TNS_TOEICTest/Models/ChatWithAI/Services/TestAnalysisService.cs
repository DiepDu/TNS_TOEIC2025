using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;
using static TNS_TOEICTest.Models.ChatWithAI.DTOs.DTOs;

namespace TNS_TOEICTest.Models.ChatWithAI.Services
{
    /// <summary>
    /// Service phân tích bài thi cụ thể theo ngày/topic
    /// </summary>
    public static class TestAnalysisService
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public static async Task<string> GetTestAnalysisByDateAsync(
            string memberKey,
            DateTime testDate,
            int? exactScore = null,
            TimeSpan? exactTime = null)
        {
            var analysisBuilder = new StringBuilder();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // B1: Lấy danh sách các bài test trong ngày
                var tests = new List<(string ResultKey, string TestName, int TestScore, int ListeningScore, int ReadingScore, int CompletionTime, DateTime EndTime, int CorrectListening, int CorrectReading)>();

                var query = @"
            SELECT 
                R.ResultKey, 
                T.TestName,
                R.TestScore,
                R.ListeningScore,
                R.ReadingScore,
                R.[Time] AS CompletionTime,
                R.EndTime,
                (SELECT COUNT(*) FROM UserAnswers UA WHERE UA.ResultKey = R.ResultKey AND UA.IsCorrect = 1 AND UA.Part BETWEEN 1 AND 4) AS CorrectListening,
                (SELECT COUNT(*) FROM UserAnswers UA WHERE UA.ResultKey = R.ResultKey AND UA.IsCorrect = 1 AND UA.Part BETWEEN 5 AND 7) AS CorrectReading
            FROM ResultOfUserForTest R
            JOIN Test T ON R.TestKey = T.TestKey
            WHERE R.MemberKey = @MemberKey 
              AND CAST(R.EndTime AS DATE) = @TestDate
              AND R.TestScore IS NOT NULL
            ORDER BY R.EndTime DESC;";

                using (var cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    cmd.Parameters.AddWithValue("@TestDate", testDate.Date);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            tests.Add((
                                reader["ResultKey"].ToString(),
                                reader["TestName"].ToString(),
                                Convert.ToInt32(reader["TestScore"]),
                                Convert.ToInt32(reader["ListeningScore"]),
                                Convert.ToInt32(reader["ReadingScore"]),
                                Convert.ToInt32(reader["CompletionTime"]),
                                Convert.ToDateTime(reader["EndTime"]),
                                Convert.ToInt32(reader["CorrectListening"]),
                                Convert.ToInt32(reader["CorrectReading"])
                            ));
                        }
                    }
                }

                if (!tests.Any())
                    return $"Không tìm thấy bài thi nào đã hoàn thành vào ngày {testDate:dd/MM/yyyy}.";

                // B2: Chọn bài thi theo logic ưu tiên
                var selectedTest = SelectTestByPriority(tests, exactScore, exactTime, testDate);

                if (selectedTest.ResultKey == null)
                    return $"Không tìm thấy bài thi có điểm {exactScore.Value} vào ngày {testDate:dd/MM/yyyy}.";

                // B3: In thông tin tổng quan
                analysisBuilder.AppendLine($"--- Phân tích bài thi '{selectedTest.TestName}' (Ngày: {testDate:dd/MM/yyyy}, Kết thúc: {selectedTest.EndTime:HH:mm}) ---");
                analysisBuilder.AppendLine($"## I. Kết quả tổng quan");
                analysisBuilder.AppendLine($"- **Tổng điểm:** {selectedTest.TestScore}/990");
                analysisBuilder.AppendLine($"- **Điểm Nghe (Listening):** {selectedTest.ListeningScore}/495 (Đúng {selectedTest.CorrectListening}/100 câu)");
                analysisBuilder.AppendLine($"- **Điểm Đọc (Reading):** {selectedTest.ReadingScore}/495 (Đúng {selectedTest.CorrectReading}/100 câu)");
                analysisBuilder.AppendLine($"- **Thời gian hoàn thành:** {selectedTest.CompletionTime} phút");
                analysisBuilder.AppendLine();

                // B4: Phân tích lỗi sai
                analysisBuilder.AppendLine($"## II. Phân tích lỗi sai chi tiết");
                await AppendErrorAnalysis(connection, selectedTest.ResultKey, analysisBuilder);
            }

            return analysisBuilder.ToString();
        }

        public static async Task<List<IncorrectDetailDto>> FindMyIncorrectQuestionsByTopicNamesAsync(
            string memberKey,
            List<string> grammarTopics = null,
            List<string> vocabularyTopics = null,
            List<string> categories = null,
            List<string> errorTypes = null,
            int limit = 10)
        {
            var results = new List<IncorrectDetailDto>();
            const string DOMAIN = "https://localhost:7078";

            if ((grammarTopics == null || grammarTopics.Count == 0) &&
                (vocabularyTopics == null || vocabularyTopics.Count == 0) &&
                (categories == null || categories.Count == 0) &&
                (errorTypes == null || errorTypes.Count == 0))
            {
                Console.WriteLine("[FindByTopicNames] No topics provided");
                return results;
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // BUILD DYNAMIC WHERE CLAUSE
                    var whereConditions = new List<string>();
                    var parameters = new List<SqlParameter>();

                    if (grammarTopics != null && grammarTopics.Count > 0)
                    {
                        var grammarConditions = new List<string>();
                        for (int i = 0; i < grammarTopics.Count; i++)
                        {
                            grammarConditions.Add($"GT.TopicName LIKE @GrammarTopic{i}");
                            parameters.Add(new SqlParameter($"@GrammarTopic{i}", $"%{grammarTopics[i]}%"));
                        }
                        whereConditions.Add($"({string.Join(" OR ", grammarConditions)})");
                    }

                    if (vocabularyTopics != null && vocabularyTopics.Count > 0)
                    {
                        var vocabConditions = new List<string>();
                        for (int i = 0; i < vocabularyTopics.Count; i++)
                        {
                            vocabConditions.Add($"VT.TopicName LIKE @VocabTopic{i}");
                            parameters.Add(new SqlParameter($"@VocabTopic{i}", $"%{vocabularyTopics[i]}%"));
                        }
                        whereConditions.Add($"({string.Join(" OR ", vocabConditions)})");
                    }

                    if (categories != null && categories.Count > 0)
                    {
                        var categoryConditions = new List<string>();
                        for (int i = 0; i < categories.Count; i++)
                        {
                            categoryConditions.Add($"CAT.CategoryName LIKE @Category{i}");
                            parameters.Add(new SqlParameter($"@Category{i}", $"%{categories[i]}%"));
                        }
                        whereConditions.Add($"({string.Join(" OR ", categoryConditions)})");
                    }

                    if (errorTypes != null && errorTypes.Count > 0)
                    {
                        var errorConditions = new List<string>();
                        for (int i = 0; i < errorTypes.Count; i++)
                        {
                            errorConditions.Add($"ET.ErrorDescription LIKE @ErrorType{i}");
                            parameters.Add(new SqlParameter($"@ErrorType{i}", $"%{errorTypes[i]}%"));
                        }
                        whereConditions.Add($"({string.Join(" OR ", errorConditions)})");
                    }

                    var whereClause = whereConditions.Any() ? string.Join(" OR ", whereConditions) : "1=0";

                    // ✅ QUERY VỚI SELF-JOIN
                    var query = $@"
WITH Q AS (
 SELECT QuestionKey, QuestionText, Explanation, 1 AS Part, 
        GrammarTopic, VocabularyTopic, Category,
        Parent AS ParentKey, 
        CAST(NULL AS NVARCHAR(MAX)) AS ParentText,
        QuestionImage, QuestionVoice,
        CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part1_Question

 UNION ALL 
 SELECT QuestionKey, QuestionText, Explanation, 2 AS Part, 
        GrammarTopic, VocabularyTopic, Category,
        Parent AS ParentKey,
        CAST(NULL AS NVARCHAR(MAX)) AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, QuestionVoice,
        CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part2_Question

 UNION ALL 
 SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, 3 AS Part, 
        Q.GrammarTopic, Q.VocabularyTopic, Q.Category,
        Q.Parent AS ParentKey,
        P.QuestionText AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, 
        CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        P.QuestionVoice AS ParentAudioUrl
 FROM TEC_Part3_Question Q
 LEFT JOIN TEC_Part3_Question P ON Q.Parent = P.QuestionKey

 UNION ALL 
 SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, 4 AS Part, 
        Q.GrammarTopic, Q.VocabularyTopic, Q.Category,
        Q.Parent AS ParentKey,
        P.QuestionText AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, 
        CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        P.QuestionVoice AS ParentAudioUrl
 FROM TEC_Part4_Question Q
 LEFT JOIN TEC_Part4_Question P ON Q.Parent = P.QuestionKey

 UNION ALL 
 SELECT QuestionKey, QuestionText, Explanation, 5 AS Part, 
        GrammarTopic, VocabularyTopic, Category,
        Parent AS ParentKey,
        CAST(NULL AS NVARCHAR(MAX)) AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, 
        CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part5_Question

 UNION ALL 
 SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, 6 AS Part, 
        Q.GrammarTopic, Q.VocabularyTopic, Q.Category,
        Q.Parent AS ParentKey,
        P.QuestionText AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, 
        CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part6_Question Q
 LEFT JOIN TEC_Part6_Question P ON Q.Parent = P.QuestionKey

 UNION ALL 
 SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, 7 AS Part, 
        Q.GrammarTopic, Q.VocabularyTopic, Q.Category,
        Q.Parent AS ParentKey,
        P.QuestionText AS ParentText,
        Q.QuestionImage, 
        CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part7_Question Q
 LEFT JOIN TEC_Part7_Question P ON Q.Parent = P.QuestionKey
)
SELECT TOP (@Limit)
    UA.UAnswerKey, UA.ResultKey, UA.QuestionKey, UA.SelectAnswerKey, 
    UA.TimeSpent, UA.AnswerTime, UA.NumberOfAnswerChanges, UA.Part,
    Q.QuestionText, Q.Explanation, 
    Q.ParentText,
    Q.QuestionImage, Q.QuestionVoice, Q.ParentAudioUrl,
    GT.TopicName AS GrammarTopicName, 
    VT.TopicName AS VocabularyTopicName,
    CAT.CategoryName,
    ET.ErrorDescription
FROM UserAnswers UA
JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey AND R.MemberKey = @MemberKey
LEFT JOIN Q ON UA.QuestionKey = Q.QuestionKey
LEFT JOIN GrammarTopics GT ON Q.GrammarTopic = GT.GrammarTopicID
LEFT JOIN VocabularyTopics VT ON Q.VocabularyTopic = VT.VocabularyTopicID
LEFT JOIN TEC_Category CAT ON Q.Category = CAT.CategoryKey
LEFT JOIN UsersError UE ON UE.AnswerKey = UA.SelectAnswerKey
LEFT JOIN ErrorTypes ET ON UE.ErrorType = ET.ErrorTypeID
WHERE UA.IsCorrect = 0 
  AND ({whereClause})
ORDER BY UA.AnswerTime DESC;";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Limit", limit);
                        cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                        cmd.Parameters.AddRange(parameters.ToArray());

                        var uaRows = new List<UserAnswerRow>();
                        using (var rdr = await cmd.ExecuteReaderAsync())
                        {
                            while (await rdr.ReadAsync())
                            {
                                var dto = new IncorrectDetailDto
                                {
                                    UAnswerKey = rdr.GetGuid(rdr.GetOrdinal("UAnswerKey")),
                                    ResultKey = rdr.GetGuid(rdr.GetOrdinal("ResultKey")),
                                    QuestionKey = rdr.GetGuid(rdr.GetOrdinal("QuestionKey")),
                                    Part = Convert.ToInt32(rdr["Part"]),
                                    QuestionText = rdr["QuestionText"]?.ToString() ?? "",
                                    ParentText = rdr["ParentText"]?.ToString() ?? "",
                                    Explanation = rdr["Explanation"]?.ToString() ?? "",
                                    TimeSpentSeconds = Convert.ToInt32(rdr["TimeSpent"]),
                                    AnswerTime = Convert.ToDateTime(rdr["AnswerTime"]),
                                    NumberOfAnswerChanges = Convert.ToInt32(rdr["NumberOfAnswerChanges"]),
                                    GrammarTopic = rdr["GrammarTopicName"]?.ToString() ?? "",
                                    VocabularyTopic = rdr["VocabularyTopicName"]?.ToString() ?? "",
                                    CategoryName = rdr["CategoryName"]?.ToString() ?? "",
                                    ErrorType = rdr["ErrorDescription"]?.ToString() ?? "",
                                    QuestionImageUrl = BuildMediaUrl(DOMAIN, rdr["QuestionImage"]?.ToString()),
                                    QuestionAudioUrl = BuildMediaUrl(DOMAIN, rdr["QuestionVoice"]?.ToString()),
                                    ParentAudioUrl = BuildMediaUrl(DOMAIN, rdr["ParentAudioUrl"]?.ToString())
                                };

                                results.Add(dto);

                                uaRows.Add(new UserAnswerRow
                                {
                                    UAnswerKey = rdr.GetGuid(rdr.GetOrdinal("UAnswerKey")),
                                    SelectAnswerKey = rdr["SelectAnswerKey"] == DBNull.Value ? null : rdr.GetGuid(rdr.GetOrdinal("SelectAnswerKey")),
                                    QuestionKey = rdr.GetGuid(rdr.GetOrdinal("QuestionKey")),
                                    Part = Convert.ToInt32(rdr["Part"])
                                });
                            }
                        }

                        await EnrichWithAllAnswers(conn, results, uaRows);
                    }

                    return results;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FindByTopicNames Error]: {ex.Message}");
                return results;
            }
        }

        // ========================================
        // PRIVATE HELPERS
        // ========================================

        private static (string ResultKey, string TestName, int TestScore, int ListeningScore, int ReadingScore, int CompletionTime, DateTime EndTime, int CorrectListening, int CorrectReading)
            SelectTestByPriority(
                List<(string ResultKey, string TestName, int TestScore, int ListeningScore, int ReadingScore, int CompletionTime, DateTime EndTime, int CorrectListening, int CorrectReading)> tests,
                int? exactScore,
                TimeSpan? exactTime,
                DateTime testDate)
        {
            if (exactScore.HasValue)
            {
                var match = tests.FirstOrDefault(t => t.TestScore == exactScore.Value);
                if (match.ResultKey == null)
                    return default;
                return match;
            }
            else if (exactTime.HasValue)
            {
                return tests.OrderBy(t => Math.Abs((t.EndTime.TimeOfDay - exactTime.Value).Ticks)).First();
            }
            else
            {
                return tests.First();
            }
        }

        private static async Task AppendErrorAnalysis(SqlConnection connection, string resultKey, StringBuilder builder)
        {
            var errorQuery = @"
WITH AllQuestionsAndAnswers AS (
    SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '1' AS Part FROM TEC_Part1_Question Q JOIN TEC_Part1_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
    SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '2' AS Part FROM TEC_Part2_Question Q JOIN TEC_Part2_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
    SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '3' AS Part FROM TEC_Part3_Question Q JOIN TEC_Part3_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
    SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '4' AS Part FROM TEC_Part4_Question Q JOIN TEC_Part4_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
    SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '5' AS Part FROM TEC_Part5_Question Q JOIN TEC_Part5_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
    SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '6' AS Part FROM TEC_Part6_Question Q JOIN TEC_Part6_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
    SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '7' AS Part FROM TEC_Part7_Question Q JOIN TEC_Part7_Answer A ON Q.QuestionKey = A.QuestionKey
)
SELECT 
    ET.ErrorDescription, GT.TopicName AS GrammarTopicName, VT.TopicName AS VocabularyTopicName,
    QuestionInfo.QuestionText, UserSelectedAnswer.AnswerText AS UserAnswer, CorrectAnswer.AnswerText AS CorrectAnswer,
    QuestionInfo.Explanation, QuestionInfo.Part
FROM UsersError UE
JOIN UserAnswers UA ON UE.ResultKey = UA.ResultKey AND UE.AnswerKey = UA.SelectAnswerKey
JOIN (SELECT DISTINCT QuestionKey, QuestionText, Explanation, Part FROM AllQuestionsAndAnswers) AS QuestionInfo ON UA.QuestionKey = QuestionInfo.QuestionKey
JOIN AllQuestionsAndAnswers AS UserSelectedAnswer ON UA.SelectAnswerKey = UserSelectedAnswer.AnswerKey
JOIN AllQuestionsAndAnswers AS CorrectAnswer ON UA.QuestionKey = CorrectAnswer.QuestionKey AND CorrectAnswer.AnswerCorrect = 1
LEFT JOIN ErrorTypes ET ON UE.ErrorType = ET.ErrorTypeID
LEFT JOIN GrammarTopics GT ON UE.GrammarTopic = GT.GrammarTopicID
LEFT JOIN VocabularyTopics VT ON UE.VocabularyTopic = VT.VocabularyTopicID
WHERE UE.ResultKey = @ResultKey
ORDER BY CAST(QuestionInfo.Part AS INT), NEWID();";

            // ✅ BƯỚC 1: Kiểm tra xem có UserAnswers không
            var checkAnswersQuery = "SELECT COUNT(*) FROM UserAnswers WHERE ResultKey = @ResultKey AND IsCorrect = 0";
            int incorrectCount = 0;

            using (var checkCmd = new SqlCommand(checkAnswersQuery, connection))
            {
                checkCmd.Parameters.AddWithValue("@ResultKey", resultKey);
                incorrectCount = (int)(await checkCmd.ExecuteScalarAsync() ?? 0);
            }

            // ✅ BƯỚC 2: Nếu không có câu sai, kiểm tra có câu trả lời nào không
            if (incorrectCount == 0)
            {
                var totalAnswersQuery = "SELECT COUNT(*) FROM UserAnswers WHERE ResultKey = @ResultKey";
                int totalAnswers = 0;

                using (var totalCmd = new SqlCommand(totalAnswersQuery, connection))
                {
                    totalCmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    totalAnswers = (int)(await totalCmd.ExecuteScalarAsync() ?? 0);
                }

                if (totalAnswers == 0)
                {
                    // ❌ TRƯỜNG HỢP 1: Không trả lời câu nào
                    builder.AppendLine("⚠️ **Bài thi này chưa có câu trả lời nào.**");
                    builder.AppendLine();
                    builder.AppendLine("Bạn có thể đã:");
                    builder.AppendLine("- Vào bài thi nhưng chưa bắt đầu làm");
                    builder.AppendLine("- Nộp bài ngay mà chưa trả lời");
                    builder.AppendLine("- Gặp lỗi kỹ thuật trong quá trình làm bài");
                    builder.AppendLine();
                    builder.AppendLine("💡 **Gợi ý:** Hãy thử làm lại bài thi đầy đủ để Mr. TOEIC có thể phân tích chi tiết cho bạn nhé!");
                    return;
                }
                else
                {
                    // ✅ TRƯỜNG HỢP 2: Có trả lời nhưng KHÔNG SAI câu nào (Perfect score!)
                    builder.AppendLine("🎉 **Xuất sắc! Bài thi này bạn làm đúng tất cả các câu!**");
                    builder.AppendLine();
                    builder.AppendLine($"Bạn đã trả lời đúng **{totalAnswers}/{totalAnswers} câu**. Tiếp tục phát huy nhé! 💪");
                    return;
                }
            }

            // ✅ BƯỚC 3: Có câu sai, tiếp tục phân tích bình thường
            using (var errorCommand = new SqlCommand(errorQuery, connection))
            {
                errorCommand.Parameters.AddWithValue("@ResultKey", resultKey);
                using (var reader = await errorCommand.ExecuteReaderAsync())
                {
                    int errorCount = 1;
                    while (await reader.ReadAsync())
                    {
                        builder.AppendLine($"- **Lỗi #{errorCount++} (Part {reader["Part"]})**");
                        builder.AppendLine($"  - **Câu hỏi:** {reader["QuestionText"]}");
                        builder.AppendLine($"  - **Bạn đã chọn:** '{reader["UserAnswer"]}'");
                        builder.AppendLine($"  - **Đáp án đúng:** '{reader["CorrectAnswer"]}'");
                        builder.AppendLine($"  - **Chủ đề:** Ngữ pháp '{reader["GrammarTopicName"]}', Từ vựng '{reader["VocabularyTopicName"]}'");
                        builder.AppendLine($"  - **Giải thích:** {reader["Explanation"]}");
                        builder.AppendLine();
                    }
                }
            }
        }

        private static async Task EnrichWithAllAnswers(
            SqlConnection conn,
            List<IncorrectDetailDto> results,
            List<UserAnswerRow> uaRows)
        {
            var qKeys = results.Select(x => x.QuestionKey).Distinct().ToList();
            if (!qKeys.Any()) return;

            // ✅ QUERY LẤY TẤT CẢ 4 ĐÁP ÁN
            var answersByQuestion = new Dictionary<Guid, List<AnswerOptionDto>>();

            foreach (var result in results)
            {
                var part = result.Part;
                var tableName = $"TEC_Part{part}_Answer";

                var query = $@"
            SELECT AnswerKey, AnswerText, AnswerCorrect
            FROM {tableName}
            WHERE QuestionKey = @QuestionKey
            ORDER BY AnswerKey";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@QuestionKey", result.QuestionKey);

                    var answers = new List<AnswerOptionDto>();
                    var userAnswerKey = uaRows.FirstOrDefault(u => u.UAnswerKey == result.UAnswerKey)?.SelectAnswerKey;

                    using (var rdr = await cmd.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            var answerKey = rdr.GetGuid(0);
                            var answerText = rdr["AnswerText"]?.ToString() ?? "";
                            var isCorrect = Convert.ToBoolean(rdr["AnswerCorrect"]);
                            var isSelected = userAnswerKey.HasValue && userAnswerKey.Value == answerKey;

                            answers.Add(new AnswerOptionDto
                            {
                                AnswerKey = answerKey,
                                AnswerText = answerText,
                                IsCorrect = isCorrect,
                                IsSelected = isSelected
                            });

                            // ✅ Fill SelectedAnswerText và CorrectAnswerText
                            if (isSelected)
                                result.SelectedAnswerText = answerText;
                            if (isCorrect)
                                result.CorrectAnswerText = answerText;
                        }
                    }

                    result.AllAnswers = answers;
                }
            }
        }

        // ✅ HELPER: Build full URL
        private static string BuildMediaUrl(string domain, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return "";

            // Đảm bảo relativePath bắt đầu bằng /
            if (!relativePath.StartsWith("/"))
                relativePath = "/" + relativePath;

            return domain + relativePath;
        }
    }
}