using Google.Cloud.AIPlatform.V1;
using Microsoft.Data.SqlClient;

using System.Text;

namespace TNS_TOEICTest.Models
{
    public class ChatWithAIAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
        public static async Task<List<Dictionary<string, object>>> GetConversationsWithAIAsync(string userId)
        {
            var conversations = new List<Dictionary<string, object>>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Câu lệnh SQL này sẽ lấy các cuộc hội thoại và tin nhắn cuối cùng của mỗi cuộc hội thoại đó.
                var query = @"
                    WITH LastMessages AS (
                        SELECT 
                            ConversationAIID, 
                            Content,
                            Timestamp,
                            ROW_NUMBER() OVER(PARTITION BY ConversationAIID ORDER BY Timestamp DESC) as rn
                        FROM MessageWithAI
                    )
                    SELECT 
                        c.ConversationAIID,
                        c.UserID,
                        c.Title,
                        c.StartedAt,
                        lm.Content AS LastMessage
                    FROM ConversationsWithAI c
                    LEFT JOIN LastMessages lm ON c.ConversationAIID = lm.ConversationAIID AND lm.rn = 1
                    WHERE c.UserID = @UserID
                    ORDER BY COALESCE(lm.Timestamp, c.StartedAt) DESC;";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserID", userId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var conversation = new Dictionary<string, object>
                            {
                                { "ConversationAIID", reader["ConversationAIID"] },
                                { "UserID", reader["UserID"] },
                                { "Title", reader["Title"] == DBNull.Value ? "New Chat" : reader["Title"] },
                                { "StartedAt", reader["StartedAt"] },
                                { "LastMessage", reader["LastMessage"] == DBNull.Value ? "No messages yet." : reader["LastMessage"] }
                            };
                            conversations.Add(conversation);
                        }
                    }
                }
            }
            return conversations;
        }

        public static async Task<Dictionary<string, object>> GetInitialChatDataAsync(string userId)
        {
            var initialData = new Dictionary<string, object>();
            var messages = new List<Dictionary<string, object>>();
            object conversationInfo = null;

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Bước 1: Tìm cuộc trò chuyện gần đây nhất của người dùng
                var convoQuery = @"
                    SELECT TOP 1 ConversationAIID, Title, StartedAt 
                    FROM ConversationsWithAI 
                    WHERE UserID = @UserID 
                    ORDER BY StartedAt DESC;";

                Guid? latestConversationId = null;

                using (var convoCommand = new SqlCommand(convoQuery, connection))
                {
                    convoCommand.Parameters.AddWithValue("@UserID", userId);
                    using (var reader = await convoCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            latestConversationId = (Guid)reader["ConversationAIID"];
                            conversationInfo = new
                            {
                                ConversationAIID = latestConversationId,
                                Title = reader["Title"] == DBNull.Value ? "New Chat" : reader["Title"],
                                StartedAt = reader["StartedAt"]
                            };
                        }
                    }
                }

                // Bước 2: Nếu tìm thấy, lấy các tin nhắn gần nhất của cuộc trò chuyện đó
                if (latestConversationId.HasValue)
                {
                    var messagesQuery = @"
                        SELECT TOP 50 MessageAIID, SenderRole, Content, Timestamp
                        FROM MessageWithAI
                        WHERE ConversationAIID = @ConversationAIID
                        ORDER BY Timestamp ASC;"; // ASC để hiển thị đúng thứ tự trong chat

                    using (var messagesCommand = new SqlCommand(messagesQuery, connection))
                    {
                        messagesCommand.Parameters.AddWithValue("@ConversationAIID", latestConversationId.Value);
                        using (var reader = await messagesCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var message = new Dictionary<string, object>
                                {
                                    { "MessageAIID", reader["MessageAIID"] },
                                    { "SenderRole", reader["SenderRole"] },
                                    { "Content", reader["Content"] },
                                    { "Timestamp", reader["Timestamp"] }
                                };
                                messages.Add(message);
                            }
                        }
                    }
                }
            }

            initialData["conversation"] = conversationInfo;
            initialData["messages"] = messages;

            return initialData;
        }
        public static async Task<Guid> CreateConversationAsync(string userId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var conversationId = Guid.NewGuid();
                var createQuery = "INSERT INTO ConversationsWithAI (ConversationAIID, UserID, Title) OUTPUT INSERTED.ConversationAIID VALUES (@ConversationAIID, @UserID, @Title)";
                using (var createCommand = new SqlCommand(createQuery, connection))
                {
                    createCommand.Parameters.AddWithValue("@ConversationAIID", conversationId);
                    createCommand.Parameters.AddWithValue("@UserID", userId);
                    createCommand.Parameters.AddWithValue("@Title", "New Chat");
                    // Thực thi và trả về ID vừa được tạo
                    return (Guid)await createCommand.ExecuteScalarAsync();
                }
            }
        }

        public static async Task SaveMessageAsync(Guid conversationId, string role, string content)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // SỬA LỖI 1: Thêm cột 'MessageAIID' vào câu lệnh INSERT
                var query = "INSERT INTO MessageWithAI (MessageAIID, ConversationAIID, SenderRole, Content, Timestamp) VALUES (@MessageAIID, @ConversationAIID, @SenderRole, @Content, @Timestamp)";

                using (var command = new SqlCommand(query, connection))
                {
                    // SỬA LỖI 2: Tạo một ID mới cho tin nhắn
                    var messageId = Guid.NewGuid();

                    // SỬA LỖI 3: Thêm các tham số vào command
                    command.Parameters.AddWithValue("@MessageAIID", messageId);
                    command.Parameters.AddWithValue("@ConversationAIID", conversationId);
                    command.Parameters.AddWithValue("@SenderRole", role);
                    command.Parameters.AddWithValue("@Content", content);
                    command.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        public static async Task<IEnumerable<Content>> GetMessageHistoryForApiAsync(Guid conversationId)
        {
            var history = new List<Content>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                // Lấy 10 tin nhắn gần nhất để làm ngữ cảnh
                var commandText = "SELECT TOP 10 SenderRole, Content FROM MessageWithAI WHERE ConversationAIID = @ConversationAIID ORDER BY Timestamp ASC";

                using (var command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@ConversationAIID", conversationId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var content = new Content();
                            var role = reader["SenderRole"].ToString()?.ToLower() ?? "user";
                            content.Role = role == "ai" ? "model" : "user";
                            var textContent = reader["Content"].ToString() ?? "";
                            content.Parts.Add(new Part { Text = textContent });

                            history.Add(content);
                        }
                    }
                }
            }
            return history;
        }
        public static async Task<List<Dictionary<string, object>>> GetMoreMessagesAsync(Guid conversationId, int skipCount)
        {
            var messages = new List<Dictionary<string, object>>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                
                var query = @"
            SELECT MessageAIID, SenderRole, Content, Timestamp
            FROM MessageWithAI
            WHERE ConversationAIID = @ConversationAIID
            ORDER BY Timestamp DESC
            OFFSET @SkipCount ROWS 
            FETCH NEXT 100 ROWS ONLY;";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ConversationAIID", conversationId);
                    command.Parameters.AddWithValue("@SkipCount", skipCount);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var message = new Dictionary<string, object>
                    {
                        { "MessageAIID", reader["MessageAIID"] },
                        { "SenderRole", reader["SenderRole"].ToString()! },
                        { "Content", reader["Content"].ToString()! },
                        { "Timestamp", reader["Timestamp"] }
                    };
                            messages.Add(message);
                        }
                    }
                }
            }
            messages.Reverse();
            return messages;
        }
        public static async Task<string> LoadMemberOriginalDataAsync(string memberKey)
        {
            var contextBuilder = new StringBuilder();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // === PHẦN 1: LẤY HỒ SƠ CÁ NHÂN TỪ EDU_Member ===
                var memberQuery = "SELECT MemberName, Gender, YearOld, ToeicScoreStudy, ToeicScoreExam FROM EDU_Member WHERE MemberKey = @MemberKey;";
                using (var command = new SqlCommand(memberQuery, connection))
                {
                    command.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            contextBuilder.AppendLine("--- Student Profile ---");
                            contextBuilder.AppendLine($"Name: {reader["MemberName"]}");
                            contextBuilder.AppendLine($"Gender: {reader["Gender"]}, Age: {reader["YearOld"]}");
                            contextBuilder.AppendLine($"Latest Practice Score (Part-by-part): {reader["ToeicScoreStudy"]}");
                            contextBuilder.AppendLine($"Latest Full Test Score: {reader["ToeicScoreExam"]}");
                            contextBuilder.AppendLine();
                        }
                    }
                }

                // === PHẦN 2: LẤY 5 KẾT QUẢ THI GẦN NHẤT VÀ TÍNH TOÁN TÓM TẮT ===
                var resultsQuery = "SELECT TOP 5 TestScore FROM ResultOfUserForTest WHERE MemberKey = @MemberKey AND TestScore IS NOT NULL ORDER BY StartTime DESC;";
                var recentScores = new List<int>();
                using (var command = new SqlCommand(resultsQuery, connection))
                {
                    command.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            recentScores.Add(Convert.ToInt32(reader["TestScore"]));
                        }
                    }
                }

                contextBuilder.AppendLine("--- Performance Summary ---");
                if (recentScores.Any())
                {
                    contextBuilder.AppendLine($"Highest Score: {recentScores.Max()}");
                    contextBuilder.AppendLine($"Average Score (last {recentScores.Count} tests): {recentScores.Average():F0}");
                    if (recentScores.Count > 1)
                    {
                        // So sánh điểm đầu tiên (mới nhất) và điểm cuối cùng (cũ nhất) trong 5 bài
                        var trend = recentScores.First() > recentScores.Last() ? "Upward" : (recentScores.First() < recentScores.Last() ? "Downward" : "Stable");
                        contextBuilder.AppendLine($"Score Trend: {trend}");
                    }
                }
                else
                {
                    contextBuilder.AppendLine("No test results found.");
                }
                contextBuilder.AppendLine();

                // === PHẦN 3: PHÂN TÍCH HÀNH VI LÀM BÀI ===
                var behaviorQuery = @"
                    SELECT 
                        AVG(CAST(ua.TimeSpent AS FLOAT)) AS AvgTime, 
                        AVG(CAST(ua.NumberOfAnswerChanges AS FLOAT)) AS AvgChanges
                    FROM UserAnswers ua
                    WHERE ua.ResultKey IN (SELECT TOP 5 ResultKey FROM ResultOfUserForTest WHERE MemberKey = @MemberKey ORDER BY StartTime DESC);";
                using (var command = new SqlCommand(behaviorQuery, connection))
                {
                    command.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync() && reader["AvgTime"] != DBNull.Value)
                        {
                            contextBuilder.AppendLine("--- Test-taking Behavior ---");
                            contextBuilder.AppendLine($"Average time per question: {Convert.ToDouble(reader["AvgTime"]):F1} seconds");
                            contextBuilder.AppendLine($"Average answer changes per question: {Convert.ToDouble(reader["AvgChanges"]):F2}");
                            contextBuilder.AppendLine();
                        }
                    }
                }

                // === PHẦN 4: PHÂN TÍCH SÂU VỀ LỖI SAI (LOGIC CÓ ĐIỀU KIỆN - ĐÃ CẬP NHẬT) ===
                string? latestResultKey = null;
                int totalQuestions = 0;
                var latestTestQuery = "SELECT TOP 1 R.ResultKey, T.TotalQuestion FROM ResultOfUserForTest R JOIN Test T ON R.TestKey = T.TestKey WHERE R.MemberKey = @MemberKey ORDER BY R.StartTime DESC;";
                using (var latestTestCmd = new SqlCommand(latestTestQuery, connection))
                {
                    latestTestCmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await latestTestCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            latestResultKey = reader["ResultKey"].ToString();
                            totalQuestions = reader["TotalQuestion"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TotalQuestion"]);
                        }
                    }
                }

                // SỬA LỖI SQL TẠI ĐÂY
                var errorQuery = @"
            -- Bước 1: Tạo CTE hợp nhất tất cả câu hỏi và câu trả lời, THÊM CỘT 'Part' THỦ CÔNG
            WITH AllQuestionsAndAnswers AS (
                SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '1' AS Part FROM TEC_Part1_Question Q JOIN TEC_Part1_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
                SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '2' AS Part FROM TEC_Part2_Question Q JOIN TEC_Part2_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
                SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '3' AS Part FROM TEC_Part3_Question Q JOIN TEC_Part3_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
                SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '4' AS Part FROM TEC_Part4_Question Q JOIN TEC_Part4_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
                SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '5' AS Part FROM TEC_Part5_Question Q JOIN TEC_Part5_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
                SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '6' AS Part FROM TEC_Part6_Question Q JOIN TEC_Part6_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
                SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '7' AS Part FROM TEC_Part7_Question Q JOIN TEC_Part7_Answer A ON Q.QuestionKey = A.QuestionKey
            )
            -- Bước 2: Truy vấn chính
            SELECT 
                UE.ErrorDate, ET.ErrorDescription, GT.TopicName AS GrammarTopicName, VT.TopicName AS VocabularyTopicName,
                QuestionInfo.QuestionText, 
                UserSelectedAnswer.AnswerText AS UserAnswer, 
                CorrectAnswer.AnswerText AS CorrectAnswer,
                QuestionInfo.Explanation,
                UA.TimeSpent, UA.NumberOfAnswerChanges,
                QuestionInfo.Part -- Cột Part giờ đã tồn tại
            FROM UsersError UE
            JOIN UserAnswers UA ON UE.ResultKey = UA.ResultKey AND UE.AnswerKey = UA.SelectAnswerKey
            JOIN (SELECT DISTINCT QuestionKey, QuestionText, Explanation, Part FROM AllQuestionsAndAnswers) AS QuestionInfo ON UA.QuestionKey = QuestionInfo.QuestionKey
            JOIN AllQuestionsAndAnswers AS UserSelectedAnswer ON UA.SelectAnswerKey = UserSelectedAnswer.AnswerKey
            JOIN AllQuestionsAndAnswers AS CorrectAnswer ON UA.QuestionKey = CorrectAnswer.QuestionKey AND CorrectAnswer.AnswerCorrect = 1
            LEFT JOIN ErrorTypes ET ON UE.ErrorType = ET.ErrorTypeID
            LEFT JOIN GrammarTopics GT ON UE.GrammarTopic = GT.GrammarTopicID
            LEFT JOIN VocabularyTopics VT ON UE.VocabularyTopic = VT.VocabularyTopicID
            {WHERE_CLAUSE}
            {ORDER_AND_LIMIT};";

                var finalErrorQuery = "";
                var errorCommand = new SqlCommand();
                errorCommand.Connection = connection;
                errorCommand.Parameters.AddWithValue("@MemberKey", memberKey);

                if (!string.IsNullOrEmpty(latestResultKey) && totalQuestions >= 100)
                {
                    contextBuilder.AppendLine($"--- Detailed Error Analysis (From Latest Full Test) ---");
                    finalErrorQuery = errorQuery
                        .Replace("{WHERE_CLAUSE}", "WHERE UE.ResultKey = @ResultKey AND UA.IsCorrect = 0")
                        .Replace("{ORDER_AND_LIMIT}", "ORDER BY UE.ErrorDate DESC");
                    errorCommand.Parameters.AddWithValue("@ResultKey", latestResultKey);
                }
                else
                {
                    contextBuilder.AppendLine($"--- Detailed Error Analysis (Last 30 Errors) ---");
                    finalErrorQuery = errorQuery
                        .Replace("{WHERE_CLAUSE}", "WHERE UE.UserKey = @MemberKey AND UA.IsCorrect = 0")
                        .Replace("{ORDER_AND_LIMIT}", "ORDER BY UE.ErrorDate DESC OFFSET 0 ROWS FETCH NEXT 30 ROWS ONLY");
                }

                errorCommand.CommandText = finalErrorQuery;
                using (errorCommand)
                {
                    using (var reader = await errorCommand.ExecuteReaderAsync())
                    {
                        int errorCount = 1;
                        while (await reader.ReadAsync())
                        {
                            contextBuilder.AppendLine($"[Error #{errorCount++} - Part {reader["Part"]}]");
                            contextBuilder.AppendLine($"  - Question: {reader["QuestionText"]}");
                            contextBuilder.AppendLine($"  - Your Answer: '{reader["UserAnswer"]}'");
                            contextBuilder.AppendLine($"  - Correct Answer: '{reader["CorrectAnswer"]}'");
                            contextBuilder.AppendLine($"  - Error Type: {reader["ErrorDescription"]}");
                            contextBuilder.AppendLine($"  - Topics: {reader["GrammarTopicName"]}, {reader["VocabularyTopicName"]}");
                            contextBuilder.AppendLine($"  - Behavior: Time spent was {reader["TimeSpent"]}s, changed answer {reader["NumberOfAnswerChanges"]} times.");
                            contextBuilder.AppendLine($"  - Explanation: {reader["Explanation"]}");
                        }
                        if (errorCount == 1)
                        {
                            contextBuilder.AppendLine("No specific errors found to analyze.");
                        }
                    }
                }
            }

            return contextBuilder.ToString();
        }



    }
}