using Google.Cloud.AIPlatform.V1;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
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
                            // === SỬA ĐỔI TẠI ĐÂY ===
                            var startedAt = (DateTime)reader["StartedAt"];
                            var title = reader["Title"] == DBNull.Value || string.IsNullOrEmpty(reader["Title"].ToString())
                                ? startedAt.ToString("yyyy-MM-dd HH:mm") // Nếu Title trống, dùng ngày tạo
                                : reader["Title"].ToString();

                            var conversation = new Dictionary<string, object>
                    {
                        { "ConversationAIID", reader["ConversationAIID"] },
                        { "UserID", reader["UserID"] },
                        { "Title", title }, // Sử dụng biến title đã được xử lý
                        { "StartedAt", startedAt },
                        { "LastMessage", reader["LastMessage"] == DBNull.Value ? "No messages yet." : reader["LastMessage"] }
                    };
                            conversations.Add(conversation);
                        }
                    }
                }
            }
            return conversations;
        }

        // File: Models/ChatWithAIAccessData.cs

        public static async Task<Dictionary<string, object>> GetInitialChatDataAsync(string userId)
        {
            var initialData = new Dictionary<string, object>();
            var messages = new List<Dictionary<string, object>>();
            object conversationInfo = null;

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Bước 1: Tìm cuộc trò chuyện gần đây nhất (không đổi)
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

                // Bước 2: Lấy 50 tin nhắn gần nhất của cuộc trò chuyện đó
                if (latestConversationId.HasValue)
                {
                    var messagesQuery = @"
                SELECT TOP 50 MessageAIID, SenderRole, Content, Timestamp
                FROM MessageWithAI
                WHERE ConversationAIID = @ConversationAIID
                ORDER BY Timestamp DESC;"; // <-- SỬA TỪ ASC THÀNH DESC ĐỂ LẤY TIN NHẮN MỚI NHẤT

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
        // ĐÂY LÀ CODE MỚI ĐÃ SỬA LỖI
        public static async Task<Guid> CreateNewConversationAsync(string memberKey)
        {
            var newConversationId = Guid.NewGuid();
            // Tiêu đề mặc định có thể để trống hoặc đặt theo ý bạn
            var title = "New Conversation";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                // Sửa "MemberKey" thành "UserID" để khớp với các hàm khác
                var query = "INSERT INTO ConversationsWithAI (ConversationAIID, UserID, Title, StartedAt) VALUES (@ConversationAIID, @UserID, @Title, @StartedAt);";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ConversationAIID", newConversationId);
                    command.Parameters.AddWithValue("@UserID", memberKey); // Sửa tham số thành @UserID
                    command.Parameters.AddWithValue("@Title", title);
                    command.Parameters.AddWithValue("@StartedAt", DateTime.Now);
                    await command.ExecuteNonQueryAsync();
                }
            }
            return newConversationId;
        }

        /// <summary>
        /// Xóa một cuộc hội thoại và tất cả tin nhắn liên quan.
        /// </summary>
        public static async Task DeleteConversationAsync(Guid conversationId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                // Do có ON DELETE CASCADE, chỉ cần xóa trong bảng ConversationsWithAI
                var query = "DELETE FROM ConversationsWithAI WHERE ConversationAIID = @ConversationAIID; DELETE FROM MessageWithAI WHERE ConversationAIID = @ConversationAIID";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ConversationAIID", conversationId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Đổi tên (cập nhật Tiêu đề) của một cuộc hội thoại.
        /// </summary>
        public static async Task RenameConversationAsync(Guid conversationId, string newTitle)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "UPDATE ConversationsWithAI SET Title = @NewTitle WHERE ConversationAIID = @ConversationAIID;";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@NewTitle", newTitle);
                    command.Parameters.AddWithValue("@ConversationAIID", conversationId);
                    await command.ExecuteNonQueryAsync();
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

                // === PHẦN 1: LẤY HỒ SƠ CÁ NHÂN (Giữ nguyên) ===
                // ... (Code không đổi)
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


                // === PHẦN 2: TÓM TẮT HIỆU SUẤT TOÀN DIỆN (Giữ nguyên) ===
                // ... (Code không đổi)
                var allResultsQuery = "SELECT TestScore FROM ResultOfUserForTest WHERE MemberKey = @MemberKey AND TestScore IS NOT NULL ORDER BY StartTime ASC;"; // Lấy toàn bộ, sắp xếp từ cũ đến mới
                var allScores = new List<int>();
                using (var command = new SqlCommand(allResultsQuery, connection))
                {
                    command.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            allScores.Add(Convert.ToInt32(reader["TestScore"]));
                        }
                    }
                }

                contextBuilder.AppendLine("--- Comprehensive Performance Summary ---");
                if (allScores.Count > 0)
                {
                    contextBuilder.AppendLine($"Highest Score: {allScores.Max()}");
                    contextBuilder.AppendLine($"Lowest Score: {allScores.Min()}");
                    contextBuilder.AppendLine($"Average Score (all {allScores.Count} tests): {allScores.Average():F0}");

                    if (allScores.Count >= 3)
                    {
                        var firstThreeAvg = allScores.Take(3).Average();
                        var lastThreeAvg = allScores.Skip(allScores.Count - 3).Average();
                        string trend = lastThreeAvg > firstThreeAvg + 10 ? "Clearly Upward" : (lastThreeAvg < firstThreeAvg - 10 ? "Clearly Downward" : "Stable");
                        contextBuilder.AppendLine($"Long-term Trend: {trend}");

                        double avg = allScores.Average();
                        double sumOfSquares = allScores.Sum(score => Math.Pow(score - avg, 2));
                        double stdDev = Math.Sqrt(sumOfSquares / allScores.Count);
                        string stability = stdDev < 50 ? "Very Stable" : (stdDev < 100 ? "Relatively Stable" : "Unstable");
                        contextBuilder.AppendLine($"Performance Stability: {stability} (Std. Deviation: {stdDev:F1})");

                        string recentStatus = lastThreeAvg > avg ? "Improving" : "Below Average";
                        contextBuilder.AppendLine($"Recent Performance Status: {recentStatus}");
                    }
                }
                else
                {
                    contextBuilder.AppendLine("No test results found.");
                }
                contextBuilder.AppendLine();


                // === PHẦN 3: PHÂN TÍCH HÀNH VI LÀM BÀI (ĐÃ SỬA LẠI) ===
                contextBuilder.AppendLine("--- Test-taking Behavior Analysis ---");
                var behaviorQuery = @"
            SELECT 
                AVG(CAST(ua.TimeSpent AS FLOAT)) AS AvgTime, 
                AVG(CAST(ua.NumberOfAnswerChanges AS FLOAT)) AS AvgChanges
            FROM UserAnswers ua
            WHERE ua.ResultKey IN (SELECT TOP 10 ResultKey FROM ResultOfUserForTest WHERE MemberKey = @MemberKey ORDER BY StartTime DESC);";
                using (var command = new SqlCommand(behaviorQuery, connection))
                {
                    command.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync() && reader["AvgTime"] != DBNull.Value)
                        {
                            contextBuilder.AppendLine($"- Average time per question (last 10 tests): {Convert.ToDouble(reader["AvgTime"]):F1} seconds");
                            contextBuilder.AppendLine($"- Average answer changes (last 10 tests): {Convert.ToDouble(reader["AvgChanges"]):F2}");
                        }
                    }
                }

                // ĐÃ SỬA: Lấy thời gian từ cột [Time]
                var completionTimeQuery = @"
            SELECT TOP 5 R.[Time]
            FROM ResultOfUserForTest R
            JOIN Test T ON R.TestKey = T.TestKey
            WHERE R.MemberKey = @MemberKey AND T.TotalQuestion >= 100 AND R.[Time] IS NOT NULL
            ORDER BY R.StartTime DESC;";
                var completionTimesInMinutes = new List<double>();
                using (var command = new SqlCommand(completionTimeQuery, connection))
                {
                    command.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Cột Time là nvarchar, cần Parse sang số. Giả định là số phút.
                            if (double.TryParse(reader["Time"].ToString(), out double time))
                            {
                                completionTimesInMinutes.Add(time);
                            }
                        }
                    }
                }

                if (completionTimesInMinutes.Any())
                {
                    contextBuilder.AppendLine("- Completion Times for recent Full Tests:");
                    for (int i = 0; i < completionTimesInMinutes.Count; i++)
                    {
                        contextBuilder.AppendLine($"  - Test {i + 1}: {completionTimesInMinutes[i]:F0} minutes");
                    }
                    contextBuilder.AppendLine($"- Average Full Test Completion Time: {completionTimesInMinutes.Average():F0} minutes");
                }
                contextBuilder.AppendLine();


                // === PHẦN 4: PHÂN TÍCH LỖI SAI CHI TIẾT (Giữ nguyên như lần nâng cấp trước) ===
                // ... (Code không đổi)
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
            UE.ErrorDate, ET.ErrorDescription, 
            GT.TopicName AS GrammarTopicName, 
            VT.TopicName AS VocabularyTopicName,
            CAT.CategoryName AS CategoryTopicName,
            QuestionInfo.QuestionText, 
            UserSelectedAnswer.AnswerText AS UserAnswer, 
            CorrectAnswer.AnswerText AS CorrectAnswer,
            QuestionInfo.Explanation,
            UA.TimeSpent, UA.NumberOfAnswerChanges,
            QuestionInfo.Part
        FROM UsersError UE
        JOIN UserAnswers UA ON UE.ResultKey = UA.ResultKey AND UE.AnswerKey = UA.SelectAnswerKey
        JOIN (SELECT DISTINCT QuestionKey, QuestionText, Explanation, Part FROM AllQuestionsAndAnswers) AS QuestionInfo ON UA.QuestionKey = QuestionInfo.QuestionKey
        JOIN AllQuestionsAndAnswers AS UserSelectedAnswer ON UA.SelectAnswerKey = UserSelectedAnswer.AnswerKey
        JOIN AllQuestionsAndAnswers AS CorrectAnswer ON UA.QuestionKey = CorrectAnswer.QuestionKey AND CorrectAnswer.AnswerCorrect = 1
        LEFT JOIN ErrorTypes ET ON UE.ErrorType = ET.ErrorTypeID
        LEFT JOIN GrammarTopics GT ON UE.GrammarTopic = GT.GrammarTopicID
        LEFT JOIN VocabularyTopics VT ON UE.VocabularyTopic = VT.VocabularyTopicID
        LEFT JOIN TEC_Category CAT ON UE.CategoryTopic = CAT.CategoryKey
        {WHERE_CLAUSE}
        {ORDER_AND_LIMIT};";

                var finalErrorQuery = "";
                var errorCommand = new SqlCommand();
                errorCommand.Connection = connection;
                errorCommand.Parameters.AddWithValue("@MemberKeyParam", memberKey);

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
                    contextBuilder.AppendLine($"--- Detailed Error Analysis (Last 150 Errors) ---");
                    finalErrorQuery = errorQuery
                        .Replace("{WHERE_CLAUSE}", "WHERE UE.UserKey = @MemberKeyParam AND UA.IsCorrect = 0")
                        .Replace("{ORDER_AND_LIMIT}", "ORDER BY UE.ErrorDate DESC OFFSET 0 ROWS FETCH NEXT 150 ROWS ONLY");
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
                            contextBuilder.AppendLine($"  - Topics: Category '{reader["CategoryTopicName"]}', Grammar '{reader["GrammarTopicName"]}', Vocabulary '{reader["VocabularyTopicName"]}'");
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
        // File: Models/ChatWithAIAccessData.cs

        public static async Task<string> LoadAdminOriginalDataAsync(string adminKey)
        {
            var contextBuilder = new StringBuilder();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // === PHẦN 1: LẤY THÔNG TIN ADMIN VÀ PHÒNG BAN ===
                contextBuilder.AppendLine("--- Admin Profile & Department ---");
                var adminQuery = @"
            SELECT 
                u.UserName, u.LastLoginDate,
                e.FirstName, e.LastName, e.CompanyEmail,
                d.DepartmentName,
                e.PositionName
            FROM SYS_Users u
            LEFT JOIN HRM_Employee e ON u.EmployeeKey = e.EmployeeKey
            LEFT JOIN HRM_Department d ON e.DepartmentKey = d.DepartmentKey
            WHERE u.UserKey = @AdminKey;";

                using (var command = new SqlCommand(adminQuery, connection))
                {
                    command.Parameters.AddWithValue("@AdminKey", adminKey);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            contextBuilder.AppendLine($"Name: {reader["FirstName"]} {reader["LastName"]}");
                            contextBuilder.AppendLine($"Username: {reader["UserName"]}");
                            contextBuilder.AppendLine($"Position: {reader["PositionName"]}, Department: {reader["DepartmentName"]}");
                            contextBuilder.AppendLine($"Company Email: {reader["CompanyEmail"]}");
                            contextBuilder.AppendLine($"Last Login: {reader["LastLoginDate"]}");
                        }
                        else
                        {
                            contextBuilder.AppendLine("Admin information not found.");
                        }
                    }
                }
                contextBuilder.AppendLine();

                // === PHẦN 2: LẤY QUYỀN HẠN (ROLES) CỦA ADMIN ===
                contextBuilder.AppendLine("--- Admin Permissions ---");
                var rolesQuery = @"
            SELECT 
                r.RoleName, ur.RoleRead, ur.RoleEdit, ur.RoleAdd, ur.RoleDel, ur.RoleApproval
            FROM SYS_Users_Roles ur
            JOIN SYS_Roles r ON ur.RoleKey = r.RoleKey
            WHERE ur.UserKey = @AdminKey;";

                using (var command = new SqlCommand(rolesQuery, connection))
                {
                    command.Parameters.AddWithValue("@AdminKey", adminKey);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var permissions = new List<string>();
                            if ((bool)reader["RoleRead"]) permissions.Add("Read");
                            if ((bool)reader["RoleEdit"]) permissions.Add("Edit");
                            if ((bool)reader["RoleAdd"]) permissions.Add("Add");
                            if ((bool)reader["RoleDel"]) permissions.Add("Delete");
                            if ((bool)reader["RoleApproval"]) permissions.Add("Approval");
                            contextBuilder.AppendLine($"- Role: {reader["RoleName"]}, Permissions: [{string.Join(", ", permissions)}]");
                        }
                    }
                }
                contextBuilder.AppendLine();

                // === PHẦN 3 & 4: LẤY CẤU HÌNH HỆ THỐNG (Không đổi) ===
                // ... (phần này đã đúng và không cần sửa)
            }
            return contextBuilder.ToString();
        }
        /// <summary>
        /// Tải 10 phản hồi (feedback) gần đây nhất của học viên.
        /// </summary>
        /// <param name="memberKey">Mã của học viên.</param>
        /// <returns>Một chuỗi chứa thông tin về các feedback gần đây.</returns>
        public static async Task<string> LoadRecentFeedbacksAsync(string memberKey)
        {
            var feedbackBuilder = new StringBuilder();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                // Câu truy vấn này sẽ lấy 10 feedback gần nhất và JOIN với câu hỏi tương ứng
                var query = @"
            WITH AllQuestions AS (
                SELECT QuestionKey, QuestionText, '1' AS Part FROM TEC_Part1_Question UNION ALL
                SELECT QuestionKey, QuestionText, '2' AS Part FROM TEC_Part2_Question UNION ALL
                SELECT QuestionKey, QuestionText, '3' AS Part FROM TEC_Part3_Question UNION ALL
                SELECT QuestionKey, QuestionText, '4' AS Part FROM TEC_Part4_Question UNION ALL
                SELECT QuestionKey, QuestionText, '5' AS Part FROM TEC_Part5_Question UNION ALL
                SELECT QuestionKey, QuestionText, '6' AS Part FROM TEC_Part6_Question UNION ALL
                SELECT QuestionKey, QuestionText, '7' AS Part FROM TEC_Part7_Question
            )
            SELECT TOP 10
                FB.FeedbackText,
                FB.CreatedOn,
                FB.Part,
                Q.QuestionText
            FROM QuestionFeedbacks FB
            LEFT JOIN AllQuestions Q ON FB.QuestionKey = Q.QuestionKey
            WHERE FB.MemberKey = @MemberKey
            ORDER BY FB.CreatedOn DESC;";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        int feedbackCount = 1;
                        while (await reader.ReadAsync())
                        {
                            feedbackBuilder.AppendLine($"[Feedback #{feedbackCount++} - Part {reader["Part"]} - Date: {((DateTime)reader["CreatedOn"]):yyyy-MM-dd}]");
                            feedbackBuilder.AppendLine($"  - Regarding Question: '{reader["QuestionText"]}'");
                            feedbackBuilder.AppendLine($"  - Student's Feedback: \"{reader["FeedbackText"]}\"");
                        }

                        // Nếu không có feedback nào thì trả về một chuỗi rỗng
                        if (feedbackCount == 1)
                        {
                            return string.Empty;
                        }
                    }
                }
            }
            return feedbackBuilder.ToString();
        }
        public static async Task<Dictionary<string, object>> GetMemberSummaryAsync(string memberIdentifier)
        {
            var memberInfo = new Dictionary<string, object>();
            string query;
            string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand())
                {
                    command.Connection = connection;

                    if (Regex.IsMatch(memberIdentifier, emailPattern, RegexOptions.IgnoreCase))
                    {
                        query = "SELECT * FROM EDU_Member WHERE MemberID = @Identifier;";
                        command.Parameters.AddWithValue("@Identifier", memberIdentifier);
                    }
                    else
                    {
                        // THAY ĐỔI NẰM Ở ĐÂY
                        query = "SELECT TOP 1 * FROM EDU_Member WHERE MemberName COLLATE Vietnamese_CI_AI LIKE @IdentifierPattern COLLATE Vietnamese_CI_AI;";
                        command.Parameters.AddWithValue("@IdentifierPattern", $"%{memberIdentifier}%");
                    }

                    command.CommandText = query;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var columnName = reader.GetName(i);
                                var value = reader.GetValue(i);
                                memberInfo[columnName] = value == DBNull.Value ? null : value;
                            }
                        }
                    }
                }
            }
            return memberInfo;
        }
        public static async Task<Dictionary<string, object>> GetQuestionCountsAsync()
        {
            var counts = new Dictionary<string, object>();

            // Giả định rằng câu hỏi cha có cột 'Parent' là NULL hoặc 0.
            // Bạn có thể cần điều chỉnh điều kiện này cho đúng với cấu trúc DB của mình.
            string query = @"
        SELECT 'Part1' AS Part, COUNT(*) AS QuestionCount FROM TEC_Part1_Question UNION ALL
        SELECT 'Part2' AS Part, COUNT(*) AS QuestionCount FROM TEC_Part2_Question UNION ALL
        SELECT 'Part5' AS Part, COUNT(*) AS QuestionCount FROM TEC_Part5_Question UNION ALL

        -- Đếm câu hỏi cha (Passages) cho Part 3
        SELECT 'Part3_Passages' AS Part, COUNT(*) FROM TEC_Part3_Question WHERE Parent IS NULL UNION ALL
        -- Đếm câu hỏi con cho Part 3
        SELECT 'Part3_Questions' AS Part, COUNT(*) FROM TEC_Part3_Question WHERE Parent IS NOT NULL UNION ALL

        -- Đếm câu hỏi cha (Passages) cho Part 4
        SELECT 'Part4_Passages' AS Part, COUNT(*) FROM TEC_Part4_Question WHERE Parent IS NULL UNION ALL
        -- Đếm câu hỏi con cho Part 4
        SELECT 'Part4_Questions' AS Part, COUNT(*) FROM TEC_Part4_Question WHERE Parent IS NOT NULL UNION ALL

        -- Đếm câu hỏi cha (Passages) cho Part 6
        SELECT 'Part6_Passages' AS Part, COUNT(*) FROM TEC_Part6_Question WHERE Parent IS NULL UNION ALL
        -- Đếm câu hỏi con cho Part 6
        SELECT 'Part6_Questions' AS Part, COUNT(*) FROM TEC_Part6_Question WHERE Parent IS NOT NULL UNION ALL

        -- Đếm câu hỏi cha (Passages) cho Part 7
        SELECT 'Part7_Passages' AS Part, COUNT(*) FROM TEC_Part7_Question WHERE Parent IS NULL UNION ALL
        -- Đếm câu hỏi con cho Part 7
        SELECT 'Part7_Questions' AS Part, COUNT(*) FROM TEC_Part7_Question WHERE Parent IS NOT NULL;
    ";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(query, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            counts[reader.GetString(0)] = reader.GetInt32(1);
                        }
                    }
                }
            }

            // Tính toán tổng số câu hỏi thực tế (không tính các passages)
            int totalQuestions = 0;
            totalQuestions += counts.ContainsKey("Part1") ? (int)counts["Part1"] : 0;
            totalQuestions += counts.ContainsKey("Part2") ? (int)counts["Part2"] : 0;
            totalQuestions += counts.ContainsKey("Part5") ? (int)counts["Part5"] : 0;
            totalQuestions += counts.ContainsKey("Part3_Questions") ? (int)counts["Part3_Questions"] : 0;
            totalQuestions += counts.ContainsKey("Part4_Questions") ? (int)counts["Part4_Questions"] : 0;
            totalQuestions += counts.ContainsKey("Part6_Questions") ? (int)counts["Part6_Questions"] : 0;
            totalQuestions += counts.ContainsKey("Part7_Questions") ? (int)counts["Part7_Questions"] : 0;

            counts["Total_Actual_Questions"] = totalQuestions;

            return counts;
        }

    }
}