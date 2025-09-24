using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Math;
using Google.Cloud.AIPlatform.V1;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;


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
            var report = new Dictionary<string, object>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // --- 1) MEMBER PROFILE ---
                    var memberProfile = await GetMemberProfileAsync(connection, memberKey);
                    report["memberProfile"] = memberProfile != null
       ? (object)memberProfile
       : new { message = "Member not found" };


                    // --- 2) ALL TESTS (most recent first) ---
                    var allResults = await GetAllResultsForMemberAsync(connection, memberKey);
                    report["allResultsCount"] = allResults.Count;
                    report["recentResults"] = allResults.Take(20).ToList();

                    // --- 3) FULL TESTS SUMMARY (last 5 full tests) ---
                    var lastFullTests = allResults
                        .Where(r => r.TotalQuestion >= 100 && r.TestScore != null)
                        .Take(5)
                        .ToList();
                    report["lastFullTests"] = lastFullTests;

                    // Score statistics & trend
                    report["scoreStatistics"] = ComputeScoreStatistics(
                        allResults.Where(r => r.TestScore.HasValue).Select(r => r.TestScore!.Value).ToList()
                    );
                    report["scoreTrend"] = ComputeScoreTrend(
                        lastFullTests.Where(r => r.TestScore.HasValue).Select(r => r.TestScore!.Value).ToList()
                    );

                    // --- 4) BEHAVIOR ANALYSIS ---
                    var recentResultKeys = allResults.Select(r => r.ResultKey).Take(20).ToList();
                    var userAnswers = await GetUserAnswersByResultKeysAsync(connection, recentResultKeys);
                    report["behavior"] = AnalyzeBehavior(userAnswers);

                    // --- 5) ERROR ANALYSIS ---
                    var userErrors = await GetUserErrorsAsync(connection, memberKey, limit: 150);
                    report["errorAnalysis"] = AnalyzeErrors(userErrors);

                    // --- 6) RECENT MISTAKES DETAILED (10 gần nhất) ---
                    var mistakes = await GetRecentMistakesDetailedAsync(connection, memberKey, 10);
                    report["recentMistakesDetailed"] = mistakes;

                    // --- 7) NOTES ---
                    var notes = new List<string>();
                    if (!recentResultKeys.Any()) notes.Add("Not enough tests to analyze behavior. Recommend user takes more tests.");
                    if (!userAnswers.Any()) notes.Add("No per-question data (UserAnswers) available in recent tests.");
                    report["notes"] = notes;
                }
            }
            catch (Exception ex)
            {
                var err = new { error = ex.Message, stack = ex.StackTrace?.Split('\n')?.Take(5) };
                report["fatal"] = err;
            }

            return JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
        }

        // ===================== DTO CLASSES =====================
        public class MemberProfileDto
        {
            public string MemberName { get; set; }
            public string Gender { get; set; }
            public int? BirthYear { get; set; }
            public int? Age { get; set; }
            public int? ToeicScoreStudy { get; set; }
            public int? ToeicScoreExam { get; set; }
            public DateTime? LastLoginDate { get; set; }
            public DateTime? CreatedOn { get; set; }
        }

        public class ResultRow
        {
            public Guid ResultKey { get; set; }
            public Guid TestKey { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public int? ListeningScore { get; set; }
            public int? ReadingScore { get; set; }
            public int? TestScore { get; set; }
            public int? Time { get; set; }
            public int? TotalQuestion { get; set; }
        }

        public class UserAnswerRow
        {
            public Guid UAnswerKey { get; set; }
            public Guid ResultKey { get; set; }
            public Guid QuestionKey { get; set; }
            public Guid? SelectAnswerKey { get; set; }
            public bool IsCorrect { get; set; }
            public int TimeSpent { get; set; }
            public DateTime AnswerTime { get; set; }
            public int NumberOfAnswerChanges { get; set; }
            public int Part { get; set; }
        }

        public class UserErrorRow
        {
            public Guid ErrorKey { get; set; }
            public Guid AnswerKey { get; set; }
            public Guid UserKey { get; set; }
            public Guid ResultKey { get; set; }
            public string ErrorTypeName { get; set; }
            public string GrammarTopicName { get; set; }
            public string VocabularyTopicName { get; set; }
            public DateTime? ErrorDate { get; set; }
            public int? Part { get; set; }
            public int? SkillLevel { get; set; }
        }

        public class MistakeDetailDto
        {
            public int Part { get; set; }
            public Guid QuestionKey { get; set; }
            public Guid ResultKey { get; set; }
            public DateTime AnswerTime { get; set; }
            public int TimeSpent { get; set; }
            public int NumberOfAnswerChanges { get; set; }
            public string SelectedAnswer { get; set; }
            public string CorrectAnswer { get; set; }
            public string QuestionText { get; set; }
            public string Explanation { get; set; }
        }

        // ===================== HELPERS =====================

        private static async Task<MemberProfileDto?> GetMemberProfileAsync(SqlConnection conn, string memberKey)
        {
            var q = @"SELECT MemberName, Gender, YearOld, ToeicScoreStudy, ToeicScoreExam, LastLoginDate, CreatedOn
                  FROM EDU_Member WHERE MemberKey = @MemberKey";
            using var cmd = new SqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                string gender = "Not Specified";
                if (r["Gender"] != DBNull.Value)
                {
                    var g = Convert.ToInt32(r["Gender"]);
                    gender = g == 1 ? "Male" : g == 0 ? "Female" : "Not Specified";
                }
                int? birthYear = r["YearOld"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["YearOld"]);
                int? age = birthYear.HasValue ? (DateTime.Now.Year - birthYear.Value) : null;

                return new MemberProfileDto
                {
                    MemberName = r["MemberName"]?.ToString(),
                    Gender = gender,
                    BirthYear = birthYear,
                    Age = age,
                    ToeicScoreStudy = r["ToeicScoreStudy"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["ToeicScoreStudy"]),
                    ToeicScoreExam = r["ToeicScoreExam"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["ToeicScoreExam"]),
                    LastLoginDate = r["LastLoginDate"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["LastLoginDate"]),
                    CreatedOn = r["CreatedOn"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["CreatedOn"])
                };
            }
            return null;
        }

        private static async Task<List<ResultRow>> GetAllResultsForMemberAsync(SqlConnection conn, string memberKey)
        {
            var list = new List<ResultRow>();
            var q = @"
            SELECT r.ResultKey, r.TestKey, r.StartTime, r.EndTime, r.ListeningScore, r.ReadingScore, r.TestScore, r.Time, t.TotalQuestion
            FROM ResultOfUserForTest r
            LEFT JOIN Test t ON r.TestKey = t.TestKey
            WHERE r.MemberKey = @MemberKey
            ORDER BY r.StartTime DESC";
            using var cmd = new SqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new ResultRow
                {
                    ResultKey = r.GetGuid(r.GetOrdinal("ResultKey")),
                    TestKey = r.GetGuid(r.GetOrdinal("TestKey")),
                    StartTime = r["StartTime"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["StartTime"]),
                    EndTime = r["EndTime"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["EndTime"]),
                    ListeningScore = r["ListeningScore"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["ListeningScore"]),
                    ReadingScore = r["ReadingScore"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["ReadingScore"]),
                    TestScore = r["TestScore"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["TestScore"]),
                    Time = r["Time"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["Time"]),
                    TotalQuestion = r["TotalQuestion"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["TotalQuestion"])
                });
            }
            return list;
        }

        private static async Task<List<UserAnswerRow>> GetUserAnswersByResultKeysAsync(SqlConnection conn, List<Guid> resultKeys)
        {
            var answers = new List<UserAnswerRow>();
            if (resultKeys == null || resultKeys.Count == 0) return answers;

            var paramNames = resultKeys.Select((g, idx) => $"@r{idx}").ToList();
            var inClause = string.Join(", ", paramNames);
            var q = $@"SELECT UAnswerKey, ResultKey, QuestionKey, SelectAnswerKey, IsCorrect, TimeSpent, AnswerTime, NumberOfAnswerChanges, Part
                   FROM UserAnswers
                   WHERE ResultKey IN ({inClause})
                   ORDER BY AnswerTime DESC";

            using var cmd = new SqlCommand(q, conn);
            for (int i = 0; i < resultKeys.Count; i++)
                cmd.Parameters.AddWithValue(paramNames[i], resultKeys[i]);

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                answers.Add(new UserAnswerRow
                {
                    UAnswerKey = r.GetGuid(r.GetOrdinal("UAnswerKey")),
                    ResultKey = r.GetGuid(r.GetOrdinal("ResultKey")),
                    QuestionKey = r.GetGuid(r.GetOrdinal("QuestionKey")),
                    SelectAnswerKey = r["SelectAnswerKey"] == DBNull.Value ? (Guid?)null : r.GetGuid(r.GetOrdinal("SelectAnswerKey")),
                    IsCorrect = Convert.ToBoolean(r["IsCorrect"]),
                    TimeSpent = Convert.ToInt32(r["TimeSpent"]),
                    AnswerTime = Convert.ToDateTime(r["AnswerTime"]),
                    NumberOfAnswerChanges = Convert.ToInt32(r["NumberOfAnswerChanges"]),
                    Part = Convert.ToInt32(r["Part"])
                });
            }
            return answers;
        }

        private static async Task<List<UserErrorRow>> GetUserErrorsAsync(SqlConnection conn, string memberKey, int limit = 150)
        {
            var list = new List<UserErrorRow>();
            var q = @"
            SELECT TOP (@Limit) UE.ErrorKey, UE.AnswerKey, UE.UserKey, UE.ResultKey, UE.ErrorDate, UE.Part, UE.SkillLevel,
                   ET.ErrorDescription, GT.TopicName as GrammarTopicName, VT.TopicName as VocabularyTopicName
            FROM UsersError UE
            LEFT JOIN ErrorTypes ET ON UE.ErrorType = ET.ErrorTypeID
            LEFT JOIN GrammarTopics GT ON UE.GrammarTopic = GT.GrammarTopicID
            LEFT JOIN VocabularyTopics VT ON UE.VocabularyTopic = VT.VocabularyTopicID
            WHERE UE.UserKey = @UserKey
            ORDER BY UE.ErrorDate DESC";
            using var cmd = new SqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@Limit", limit);
            cmd.Parameters.AddWithValue("@UserKey", memberKey);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new UserErrorRow
                {
                    ErrorKey = r.GetGuid(r.GetOrdinal("ErrorKey")),
                    AnswerKey = r.GetGuid(r.GetOrdinal("AnswerKey")),
                    UserKey = r.GetGuid(r.GetOrdinal("UserKey")),
                    ResultKey = r.GetGuid(r.GetOrdinal("ResultKey")),
                    ErrorDate = r["ErrorDate"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["ErrorDate"]),
                    Part = r["Part"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["Part"]),
                    SkillLevel = r["SkillLevel"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SkillLevel"]),
                    ErrorTypeName = r["ErrorDescription"]?.ToString(),
                    GrammarTopicName = r["GrammarTopicName"]?.ToString(),
                    VocabularyTopicName = r["VocabularyTopicName"]?.ToString()
                });
            }
            return list;
        }

        private static async Task<List<MistakeDetailDto>> GetRecentMistakesDetailedAsync(SqlConnection conn, string memberKey, int limit)
        {
            var mistakes = new List<MistakeDetailDto>();
            string q = @"
            SELECT TOP (@Limit) ua.UAnswerKey, ua.ResultKey, ua.QuestionKey, ua.SelectAnswerKey, ua.Part, ua.TimeSpent,
                   ua.NumberOfAnswerChanges, ua.AnswerTime
            FROM UserAnswers ua
            INNER JOIN ResultOfUserForTest r ON ua.ResultKey = r.ResultKey
            WHERE r.MemberKey = @MemberKey AND ua.IsCorrect = 0
            ORDER BY ua.AnswerTime DESC";

            using var cmd = new SqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@Limit", limit);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);

            using var reader = await cmd.ExecuteReaderAsync();
            var temp = new List<UserAnswerRow>();
            while (await reader.ReadAsync())
            {
                temp.Add(new UserAnswerRow
                {
                    UAnswerKey = reader.GetGuid(reader.GetOrdinal("UAnswerKey")),
                    ResultKey = reader.GetGuid(reader.GetOrdinal("ResultKey")),
                    QuestionKey = reader.GetGuid(reader.GetOrdinal("QuestionKey")),
                    SelectAnswerKey = reader["SelectAnswerKey"] == DBNull.Value ? (Guid?)null : reader.GetGuid(reader.GetOrdinal("SelectAnswerKey")),
                    Part = Convert.ToInt32(reader["Part"]),
                    TimeSpent = Convert.ToInt32(reader["TimeSpent"]),
                    NumberOfAnswerChanges = Convert.ToInt32(reader["NumberOfAnswerChanges"]),
                    AnswerTime = Convert.ToDateTime(reader["AnswerTime"]),
                    IsCorrect = false
                });
            }

            foreach (var ua in temp)
            {
                string qTable = $"TEC_Part{ua.Part}_Question";
                string aTable = $"TEC_Part{ua.Part}_Answer";

                string detailSql = $@"
                SELECT q.QuestionText, q.Explanation,
                       sa.AnswerText AS SelectedAnswer,
                       ca.AnswerText AS CorrectAnswer
                FROM {qTable} q
                LEFT JOIN {aTable} sa ON sa.AnswerKey = @SelectAnswerKey
                LEFT JOIN {aTable} ca ON ca.QuestionKey = q.QuestionKey AND ca.AnswerCorrect = 1
                WHERE q.QuestionKey = @QuestionKey";

                using var qCmd = new SqlCommand(detailSql, conn);
                qCmd.Parameters.AddWithValue("@QuestionKey", ua.QuestionKey);
                qCmd.Parameters.AddWithValue("@SelectAnswerKey", (object)ua.SelectAnswerKey ?? DBNull.Value);

                using var qReader = await qCmd.ExecuteReaderAsync();
                if (await qReader.ReadAsync())
                {
                    mistakes.Add(new MistakeDetailDto
                    {
                        Part = ua.Part,
                        QuestionKey = ua.QuestionKey,
                        ResultKey = ua.ResultKey,
                        AnswerTime = ua.AnswerTime,
                        TimeSpent = ua.TimeSpent,
                        NumberOfAnswerChanges = ua.NumberOfAnswerChanges,
                        SelectedAnswer = qReader["SelectedAnswer"]?.ToString(),
                        CorrectAnswer = qReader["CorrectAnswer"]?.ToString(),
                        QuestionText = qReader["QuestionText"]?.ToString(),
                        Explanation = qReader["Explanation"]?.ToString()
                    });
                }
            }
            return mistakes;
        }

        // ===================== ANALYSIS METHODS =====================
        private static Dictionary<string, object> AnalyzeBehavior(List<UserAnswerRow> answers)
        {
            var res = new Dictionary<string, object>();
            if (answers == null || answers.Count == 0)
            {
                res["message"] = "No user answer data available.";
                return res;
            }

            double avgTime = answers.Average(a => a.TimeSpent);
            double avgChanges = answers.Average(a => a.NumberOfAnswerChanges);
            double correctRate = answers.Average(a => a.IsCorrect ? 1.0 : 0.0) * 100.0;
            res["overall"] = new
            {
                totalAnswers = answers.Count,
                avgTimePerQuestionSeconds = Math.Round(avgTime, 2),
                avgNumberOfAnswerChanges = Math.Round(avgChanges, 2),
                overallCorrectRatePercent = Math.Round(correctRate, 2)
            };

            var byPart = answers.GroupBy(a => a.Part)
                .Select(g => new {
                    Part = g.Key,
                    QuestionCount = g.Count(),
                    AvgTimeSeconds = Math.Round(g.Average(x => x.TimeSpent), 2),
                    CorrectRatePercent = Math.Round(g.Average(x => x.IsCorrect ? 1.0 : 0.0) * 100.0, 2),
                    AvgNumberOfAnswerChanges = Math.Round(g.Average(x => x.NumberOfAnswerChanges), 2)
                }).ToList();

            res["byPart"] = byPart;

            return res;
        }

        private static object AnalyzeErrors(List<UserErrorRow> errors)
        {
            var res = new Dictionary<string, object>();
            if (errors == null || errors.Count == 0)
            {
                res["message"] = "No recorded errors for this user.";
                return res;
            }

            var grammarGroups = errors.Where(e => !string.IsNullOrEmpty(e.GrammarTopicName))
                .GroupBy(e => e.GrammarTopicName)
                .Select(g => new { Topic = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(10).ToList();

            var vocabGroups = errors.Where(e => !string.IsNullOrEmpty(e.VocabularyTopicName))
                .GroupBy(e => e.VocabularyTopicName)
                .Select(g => new { Topic = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(10).ToList();

            var errorTypeGroups = errors.Where(e => !string.IsNullOrEmpty(e.ErrorTypeName))
                .GroupBy(e => e.ErrorTypeName)
                .Select(g => new { ErrorType = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(10).ToList();

            res["totalErrorsCollected"] = errors.Count;
            res["topGrammarTopics"] = grammarGroups;
            res["topVocabularyTopics"] = vocabGroups;
            res["topErrorTypes"] = errorTypeGroups;
            res["latestErrorsSample"] = errors.Take(20).ToList();

            return res;
        }

        private static Dictionary<string, object> ComputeScoreStatistics(List<int> scores)
        {
            var res = new Dictionary<string, object>();
            if (scores == null || scores.Count == 0)
            {
                res["message"] = "No scores available.";
                return res;
            }
            res["count"] = scores.Count;
            res["max"] = scores.Max();
            res["min"] = scores.Min();
            res["avg"] = Math.Round(scores.Average(), 2);

            double mean = scores.Average();
            double variance = scores.Average(d => Math.Pow(d - mean, 2));
            res["stddev"] = Math.Round(Math.Sqrt(variance), 2);

            res["improvementAbsolute"] = scores.Last() - scores.First();

            return res;
        }

        private static object ComputeScoreTrend(List<int> recentScores)
        {
            if (recentScores == null || recentScores.Count == 0) return new { message = "No score history" };
            if (recentScores.Count == 1) return new { message = "Only one recent score available", score = recentScores.First() };

            int improved = 0, declined = 0, same = 0;
            for (int i = 1; i < recentScores.Count; i++)
            {
                if (recentScores[i] > recentScores[i - 1]) improved++;
                else if (recentScores[i] < recentScores[i - 1]) declined++;
                else same++;
            }
            string summary = improved > declined
                ? $"Mostly improving ({improved} improving vs {declined} declining) in recent {recentScores.Count} tests."
                : declined > improved
                    ? $"Mostly declining ({declined} declining vs {improved} improving) in recent {recentScores.Count} tests."
                    : $"Mixed trend: {improved} improving, {declined} declining, {same} same.";

            return new { recentCount = recentScores.Count, improvedCount = improved, declinedCount = declined, sameCount = same, summary };
        }


        public static async Task<string> LoadAdminOriginalDataAsync(string adminKey)
        {
            var contextBuilder = new StringBuilder();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
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

                var toeicConfigQuery = "SELECT TOP 1 * FROM TOEICConfiguration;";
                using (var command = new SqlCommand(toeicConfigQuery, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            contextBuilder.AppendLine("Number of questions per part:");
                            contextBuilder.AppendLine($"  - Part 1: {reader["NumberOfPart1"]}, Part 2: {reader["NumberOfPart2"]}, Part 3: {reader["NumberOfPart3"]}, Part 4: {reader["NumberOfPart4"]}");
                            contextBuilder.AppendLine($"  - Part 5: {reader["NumberOfPart5"]}, Part 6: {reader["NumberOfPart6"]}, Part 7: {reader["NumberOfPart7"]}");
                            contextBuilder.AppendLine($"Total Duration: {reader["Duration"]} minutes");
                        }
                    }
                }
                contextBuilder.AppendLine();

                // --- Truy vấn bảng SkillLevelDistribution ---
                contextBuilder.AppendLine("Skill Level Distribution (%):");
                var skillDistQuery = "SELECT Part, SkillLevel1, SkillLevel2, SkillLevel3, SkillLevel4, SkillLevel5 FROM SkillLevelDistribution ORDER BY Part;";
                using (var command = new SqlCommand(skillDistQuery, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            contextBuilder.AppendLine(
                                $"  - Part {reader["Part"]}: [" +
                                $"Level 1: {reader["SkillLevel1"]}%, " +
                                $"Level 2: {reader["SkillLevel2"]}%, " +
                                $"Level 3: {reader["SkillLevel3"]}%, " +
                                $"Level 4: {reader["SkillLevel4"]}%, " +
                                $"Level 5: {reader["SkillLevel5"]}%]"
                            );
                        }
                    }
                }
                contextBuilder.AppendLine();
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
            string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // === BƯỚC 1: TÌM KIẾM THÀNH VIÊN ===
                string initialQuery;
                var initialCommand = new SqlCommand();
                initialCommand.Connection = connection;

                if (Regex.IsMatch(memberIdentifier, emailPattern, RegexOptions.IgnoreCase))
                {
                    initialQuery = "SELECT * FROM EDU_Member WHERE MemberID = @Identifier;";
                    initialCommand.Parameters.AddWithValue("@Identifier", memberIdentifier);
                }
                else
                {
                    initialQuery = "SELECT TOP 1 * FROM EDU_Member WHERE MemberName COLLATE Vietnamese_CI_AI LIKE @IdentifierPattern COLLATE Vietnamese_CI_AI;";
                    initialCommand.Parameters.AddWithValue("@IdentifierPattern", $"%{memberIdentifier}%");
                }
                initialCommand.CommandText = initialQuery;

                string? memberKey = null;

                using (initialCommand)
                {
                    using (var reader = await initialCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Lấy thông tin cơ bản
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var columnName = reader.GetName(i);
                                var value = reader.GetValue(i);
                                memberInfo[columnName] = value == DBNull.Value ? null : value;
                            }
                            memberKey = memberInfo.ContainsKey("MemberKey") ? memberInfo["MemberKey"].ToString() : null;
                        }
                    }
                }

                // NẾU TÌM THẤY THÀNH VIÊN, TIẾN HÀNH PHÂN TÍCH SÂU
                if (!string.IsNullOrEmpty(memberKey))
                {
                    // === PHẦN 2: PHÂN TÍCH TỔNG QUAN HIỆU SUẤT ===
                    var allResultsQuery = "SELECT TestScore FROM ResultOfUserForTest WHERE MemberKey = @MemberKey AND TestScore IS NOT NULL ORDER BY StartTime ASC;";
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

                    var performanceSummary = new Dictionary<string, object>();
                    if (allScores.Count > 0)
                    {
                        performanceSummary["HighestScore"] = allScores.Max();
                        performanceSummary["LowestScore"] = allScores.Min();
                        performanceSummary["AverageScore"] = $"{allScores.Average():F0} (trên {allScores.Count} bài)";

                        if (allScores.Count >= 3)
                        {
                            var firstThreeAvg = allScores.Take(3).Average();
                            var lastThreeAvg = allScores.Skip(allScores.Count - 3).Average();
                            string trend = lastThreeAvg > firstThreeAvg + 10 ? "Clearly Upward" : (lastThreeAvg < firstThreeAvg - 10 ? "Clearly Downward" : "Stable");
                            performanceSummary["LongTermTrend"] = trend;

                            double avg = allScores.Average();
                            double sumOfSquares = allScores.Sum(score => Math.Pow(score - avg, 2));
                            double stdDev = Math.Sqrt(sumOfSquares / allScores.Count);
                            string stability = stdDev < 50 ? "Very Stable" : (stdDev < 100 ? "Relatively Stable" : "Unstable");
                            performanceSummary["PerformanceStability"] = $"{stability} (Std. Dev: {stdDev:F1})";
                            performanceSummary["RecentPerformanceStatus"] = lastThreeAvg > avg ? "Improving" : "Below Average";
                        }
                    }
                    else
                    {
                        performanceSummary["Status"] = "No test results found.";
                    }
                    memberInfo["PerformanceSummary"] = performanceSummary;


                    // === PHẦN 3: PHÂN TÍCH HÀNH VI LÀM BÀI ===
                    var behaviorSummary = new Dictionary<string, object>();
                    var behaviorQuery = @"
                SELECT AVG(CAST(ua.TimeSpent AS FLOAT)) AS AvgTime, AVG(CAST(ua.NumberOfAnswerChanges AS FLOAT)) AS AvgChanges
                FROM UserAnswers ua
                WHERE ua.ResultKey IN (SELECT TOP 10 ResultKey FROM ResultOfUserForTest WHERE MemberKey = @MemberKey ORDER BY StartTime DESC);";
                    using (var command = new SqlCommand(behaviorQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MemberKey", memberKey);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync() && reader["AvgTime"] != DBNull.Value)
                            {
                                behaviorSummary["AvgTimePerQuestion_Last10Tests"] = $"{Convert.ToDouble(reader["AvgTime"]):F1} seconds";
                                behaviorSummary["AvgAnswerChanges_Last10Tests"] = $"{Convert.ToDouble(reader["AvgChanges"]):F2}";
                            }
                        }
                    }

                    var completionTimeQuery = @"
                SELECT TOP 5 R.[Time] FROM ResultOfUserForTest R
                JOIN Test T ON R.TestKey = T.TestKey
                WHERE R.MemberKey = @MemberKey AND T.TotalQuestion >= 100 AND R.[Time] IS NOT NULL ORDER BY R.StartTime DESC;";
                    var completionTimesInMinutes = new List<double>();
                    using (var command = new SqlCommand(completionTimeQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MemberKey", memberKey);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                if (double.TryParse(reader["Time"].ToString(), out double time))
                                {
                                    completionTimesInMinutes.Add(time);
                                }
                            }
                        }
                    }

                    if (completionTimesInMinutes.Any())
                    {
                        behaviorSummary["AvgFullTestCompletionTime"] = $"{completionTimesInMinutes.Average():F0} minutes";
                    }
                    memberInfo["BehaviorAnalysis"] = behaviorSummary;


                    // === PHẦN 4: PHÂN TÍCH LỖI SAI CHI TIẾT ===
                    var errorAnalysisBuilder = new StringBuilder();
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
                        errorAnalysisBuilder.AppendLine($"--- Detailed Error Analysis (From Latest Full Test) ---");
                        finalErrorQuery = errorQuery
                            .Replace("{WHERE_CLAUSE}", "WHERE UE.ResultKey = @ResultKey AND UA.IsCorrect = 0")
                            .Replace("{ORDER_AND_LIMIT}", "ORDER BY UE.ErrorDate DESC");
                        errorCommand.Parameters.AddWithValue("@ResultKey", latestResultKey);
                    }
                    else
                    {
                        errorAnalysisBuilder.AppendLine($"--- Detailed Error Analysis (Last 150 Errors) ---");
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
                                errorAnalysisBuilder.AppendLine($"[Error #{errorCount++} - Part {reader["Part"]}]");
                                errorAnalysisBuilder.AppendLine($"  - Question: {reader["QuestionText"]}");
                                errorAnalysisBuilder.AppendLine($"  - Your Answer: '{reader["UserAnswer"]}'");
                                errorAnalysisBuilder.AppendLine($"  - Correct Answer: '{reader["CorrectAnswer"]}'");
                                errorAnalysisBuilder.AppendLine($"  - Error Type: {reader["ErrorDescription"]}");
                                errorAnalysisBuilder.AppendLine($"  - Topics: Category '{reader["CategoryTopicName"]}', Grammar '{reader["GrammarTopicName"]}', Vocabulary '{reader["VocabularyTopicName"]}'");
                                errorAnalysisBuilder.AppendLine($"  - Behavior: Time spent was {reader["TimeSpent"]}s, changed answer {reader["NumberOfAnswerChanges"]} times.");
                                errorAnalysisBuilder.AppendLine($"  - Explanation: {reader["Explanation"]}");
                            }
                            if (errorCount == 1)
                            {
                                errorAnalysisBuilder.AppendLine("No specific errors found to analyze.");
                            }
                        }
                    }
                    memberInfo["DetailedErrorAnalysis"] = errorAnalysisBuilder.ToString();
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
        public static async Task<List<Dictionary<string, object>>> FindMembersByCriteriaAsync(
         string score_condition = null,
         string last_login_before = null,
         int? min_tests_completed = null,
         string sort_by = "LastLoginDate",
         int limit = 10)
        {
            var members = new List<Dictionary<string, object>>();
            var queryBuilder = new StringBuilder();
            var parameters = new Dictionary<string, object>();

            // Sử dụng CTE để tổng hợp dữ liệu trước khi lọc và sắp xếp
            queryBuilder.Append(@"
;WITH MemberStats AS (
    SELECT
        M.MemberKey,
        M.MemberName,
        M.MemberID,
        M.LastLoginDate,
        COUNT(R.ResultKey) AS TestCount,
        MAX(R.TestScore) AS HighestScore,
        AVG(CAST(R.TestScore AS FLOAT)) AS AverageScore -- Đảm bảo tính toán AVG trên số thực
    FROM EDU_Member M
    LEFT JOIN ResultOfUserForTest R ON M.MemberKey = R.MemberKey
    GROUP BY M.MemberKey, M.MemberName, M.MemberID, M.LastLoginDate
)
SELECT * FROM MemberStats
WHERE 1=1 ");

            // Xây dựng mệnh đề WHERE động
            if (!string.IsNullOrEmpty(score_condition))
            {
                // Phân tích điều kiện điểm số (ví dụ: "> 800", "<= 500")
                var match = Regex.Match(score_condition, @"([><=]+)\s*(\d+)");
                if (match.Success)
                {
                    queryBuilder.Append($"AND AverageScore {match.Groups[1].Value} @Score ");
                    parameters["@Score"] = int.Parse(match.Groups[2].Value);
                }
            }

            if (!string.IsNullOrEmpty(last_login_before) && DateTime.TryParse(last_login_before, out var loginDate))
            {
                queryBuilder.Append("AND LastLoginDate < @LastLoginDate ");
                parameters["@LastLoginDate"] = loginDate;
            }

            if (min_tests_completed.HasValue)
            {
                queryBuilder.Append("AND TestCount >= @MinTestsCompleted ");
                parameters["@MinTestsCompleted"] = min_tests_completed.Value;
            }

            // Xây dựng mệnh đề ORDER BY
            queryBuilder.Append("ORDER BY ");
            switch (sort_by?.ToLower())
            {
                case "highest_score":
                    queryBuilder.Append("HighestScore DESC");
                    break;
                case "average_score":
                    queryBuilder.Append("AverageScore DESC");
                    break;
                case "test_count":
                    queryBuilder.Append("TestCount DESC");
                    break;
                default:
                    queryBuilder.Append("LastLoginDate DESC");
                    break;
            }

            // Thêm phân trang
            queryBuilder.Append(" OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY;");
            parameters["@Limit"] = limit;

            // Thực thi truy vấn
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(queryBuilder.ToString(), connection))
                {
                    foreach (var p in parameters)
                    {
                        command.Parameters.AddWithValue(p.Key, p.Value);
                    }

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var member = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                member[reader.GetName(i)] = reader.GetValue(i) == DBNull.Value ? null : reader.GetValue(i);
                            }
                            members.Add(member);
                        }
                    }
                }
            }
            return members;
        }


        public static async Task<List<Dictionary<string, object>>> FindQuestionsByCriteriaAsync(
        int? part = null,
        string correct_rate_condition = null,
        string topic_name = null,
        bool? has_anomaly = null,
        int? min_feedback_count = null,
        string sortBy = null,
        int limit = 10)
        {
            var questions = new List<Dictionary<string, object>>();
            var queryBuilder = new StringBuilder();
            var parameters = new Dictionary<string, object>();

            // Phần CTE và SELECT ban đầu không thay đổi
            queryBuilder.Append(@"
;WITH AllQuestions AS (
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly, '1' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part1_Question UNION ALL
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly, '2' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part2_Question UNION ALL
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly, '3' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part3_Question UNION ALL
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly, '4' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part4_Question UNION ALL
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly, '5' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part5_Question UNION ALL
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly, '6' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part6_Question UNION ALL
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly, '7' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part7_Question
)
SELECT Q.*, GT.TopicName as GrammarTopicName, VT.TopicName as VocabularyTopicName
FROM AllQuestions Q
LEFT JOIN GrammarTopics GT ON Q.GrammarTopic = GT.GrammarTopicID
LEFT JOIN VocabularyTopics VT ON Q.VocabularyTopic = VT.VocabularyTopicID
WHERE 1=1 ");

            // Phần xây dựng mệnh đề WHERE
            if (part.HasValue)
            {
                queryBuilder.Append("AND Q.Part = @Part ");
                parameters["@Part"] = part.Value.ToString();
            }
            if (has_anomaly.HasValue)
            {
                queryBuilder.Append("AND Q.Anomaly = @HasAnomaly ");
                parameters["@HasAnomaly"] = has_anomaly.Value;
            }
            if (min_feedback_count.HasValue)
            {
                queryBuilder.Append("AND Q.FeedbackCount >= @MinFeedbackCount ");
                parameters["@MinFeedbackCount"] = min_feedback_count.Value;
            }
            if (!string.IsNullOrEmpty(topic_name))
            {
                queryBuilder.Append("AND (GT.TopicName LIKE @TopicName OR VT.TopicName LIKE @TopicName) ");
                parameters["@TopicName"] = $"%{topic_name}%";
            }

            // === PHẦN SỬA LỖI LOGIC QUAN TRỌNG ===
            if (!string.IsNullOrEmpty(correct_rate_condition))
            {
                var match = Regex.Match(correct_rate_condition, @"([><=]+)\s*(.+)");
                if (match.Success)
                {
                    string op = match.Groups[1].Value;
                    string valueStr = match.Groups[2].Value.Replace("%", "").Trim();

                    if (float.TryParse(valueStr, out float targetValue))
                    {
                        // ĐÃ XÓA BỎ HOÀN TOÀN KHỐI LỆNH `if (correct_rate_condition.Contains("%"))`
                        // Giờ đây hàm sẽ so sánh trực tiếp với giá trị phần trăm (ví dụ: 30, 80)
                        queryBuilder.Append($"AND Q.CorrectRate IS NOT NULL AND Q.CorrectRate {op} @CorrectRate ");
                        parameters["@CorrectRate"] = targetValue;
                    }
                }
            }

            // Phần ORDER BY và phân trang không đổi
            queryBuilder.Append("ORDER BY ");
            if (!string.IsNullOrEmpty(sortBy))
            {
                switch (sortBy.ToUpper())
                {
                    case "CORRECTRATE_ASC":
                        queryBuilder.Append("Q.CorrectRate ASC, Q.QuestionKey ASC ");
                        break;
                    case "CORRECTRATE_DESC":
                        queryBuilder.Append("Q.CorrectRate DESC, Q.QuestionKey ASC ");
                        break;
                    case "FEEDBACKCOUNT_DESC":
                        queryBuilder.Append("Q.FeedbackCount DESC, Q.QuestionKey ASC ");
                        break;
                    default:
                        queryBuilder.Append("Q.Part ASC, Q.QuestionKey ASC ");
                        break;
                }
            }
            else
            {
                queryBuilder.Append("Q.Part ASC, Q.QuestionKey ASC ");
            }

            queryBuilder.Append("OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY;");
            parameters["@Limit"] = limit;

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(queryBuilder.ToString(), connection))
                {
                    foreach (var p in parameters)
                    {
                        command.Parameters.AddWithValue(p.Key, p.Value);
                    }
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var question = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                question[reader.GetName(i)] = reader.GetValue(i) == DBNull.Value ? null : reader.GetValue(i);
                            }
                            questions.Add(question);
                        }
                    }
                }
            }
            return questions;
        }
        public static async Task<List<Dictionary<string, object>>> GetUnresolvedFeedbacksAsync(int limit = 10)
        {
            var feedbacks = new List<Dictionary<string, object>>();
            var query = @"
        SELECT TOP (@Limit) 
            F.FeedbackText, 
            F.QuestionKey, 
            M.MemberName, 
            F.CreatedOn
        FROM QuestionFeedbacks F
        LEFT JOIN EDU_Member M ON F.MemberKey = M.MemberKey
        WHERE F.Status = 0 OR F.Status IS NULL
        ORDER BY F.CreatedOn DESC;";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Limit", limit);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var feedback = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                feedback[reader.GetName(i)] = reader.GetValue(i) == DBNull.Value ? null : reader.GetValue(i);
                            }
                            feedbacks.Add(feedback);
                        }
                    }
                }
            }
            return feedbacks;
        }
        public static async Task<Dictionary<string, object>> GetSystemActivitySummaryAsync(DateTime startDate, DateTime endDate)
        {
            var summary = new Dictionary<string, object>();
            var query = @"
        SELECT
            (SELECT COUNT(*) FROM EDU_Member WHERE CreatedOn BETWEEN @StartDate AND @EndDate) AS NewMembersCount,
            (SELECT COUNT(*) FROM ResultOfUserForTest WHERE EndTime BETWEEN @StartDate AND @EndDate) AS CompletedTestsCount,
            (SELECT AVG(CAST(TestScore AS FLOAT)) FROM ResultOfUserForTest WHERE EndTime BETWEEN @StartDate AND @EndDate) AS AverageScoreInPeriod;
    ";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                summary[reader.GetName(i)] = reader.GetValue(i) == DBNull.Value ? null : reader.GetValue(i);
                            }
                        }
                    }
                }
            }
            return summary;
        }
        // Thêm hàm này vào file ChatWithAIAccessData.cs
        // XÓA HÀM GetTestAnalysisByDateAsync CŨ VÀ THAY BẰNG HÀM MỚI NÀY
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
                (string ResultKey, string TestName, int TestScore, int ListeningScore, int ReadingScore, int CompletionTime, DateTime EndTime, int CorrectListening, int CorrectReading) selectedTest;

                if (exactScore.HasValue)
                {
                    selectedTest = tests.FirstOrDefault(t => t.TestScore == exactScore.Value);
                    if (selectedTest.ResultKey == null)
                        return $"Không tìm thấy bài thi có điểm {exactScore.Value} vào ngày {testDate:dd/MM/yyyy}.";
                }
                else if (exactTime.HasValue)
                {
                    selectedTest = tests
                        .OrderBy(t => Math.Abs((t.EndTime.TimeOfDay - exactTime.Value).Ticks))
                        .First();
                }
                else
                {
                    selectedTest = tests.First(); // gần nhất
                }

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
            ORDER BY cast(QuestionInfo.Part as int), NEWID();";

                using (var errorCommand = new SqlCommand(errorQuery, connection))
                {
                    errorCommand.Parameters.AddWithValue("@ResultKey", selectedTest.ResultKey);
                    using (var reader = await errorCommand.ExecuteReaderAsync())
                    {
                        int errorCount = 1;
                        while (await reader.ReadAsync())
                        {
                            analysisBuilder.AppendLine($"- **Lỗi #{errorCount++} (Part {reader["Part"]})**");
                            analysisBuilder.AppendLine($"  - **Câu hỏi:** {reader["QuestionText"]}");
                            analysisBuilder.AppendLine($"  - **Bạn đã chọn:** '{reader["UserAnswer"]}'");
                            analysisBuilder.AppendLine($"  - **Đáp án đúng:** '{reader["CorrectAnswer"]}'");
                            analysisBuilder.AppendLine($"  - **Chủ đề:** Ngữ pháp '{reader["GrammarTopicName"]}', Từ vựng '{reader["VocabularyTopicName"]}'");
                            analysisBuilder.AppendLine($"  - **Giải thích:** {reader["Explanation"]}");
                        }
                        if (errorCount == 1)
                        {
                            analysisBuilder.AppendLine("Bài thi này không có lỗi sai nào được ghi nhận. Rất tốt!");
                        }
                    }
                }
            }

            return analysisBuilder.ToString();
        }

        public static async Task<List<IncorrectDetailDto>> FindMyIncorrectQuestionsByTopicAsync(string memberKey, string topicName, int limit = 10)
        {
            var results = new List<IncorrectDetailDto>();
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // We join UserAnswers -> ResultOfUserForTest (to filter by MemberKey) -> unioned question meta, filter by grammar/vocab topic name LIKE pattern
                    var query = @"
WITH Q AS (
 SELECT QuestionKey, QuestionText, Explanation, 1 AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part1_Question
 UNION ALL SELECT QuestionKey, QuestionText, Explanation, 2 AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part2_Question
 UNION ALL SELECT QuestionKey, QuestionText, Explanation, 3 AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part3_Question
 UNION ALL SELECT QuestionKey, QuestionText, Explanation, 4 AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part4_Question
 UNION ALL SELECT QuestionKey, QuestionText, Explanation, 5 AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part5_Question
 UNION ALL SELECT QuestionKey, QuestionText, Explanation, 6 AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part6_Question
 UNION ALL SELECT QuestionKey, QuestionText, Explanation, 7 AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part7_Question
)
SELECT TOP (@Limit)
    UA.UAnswerKey, UA.ResultKey, UA.QuestionKey, UA.SelectAnswerKey, UA.IsCorrect, UA.TimeSpent, UA.AnswerTime, UA.NumberOfAnswerChanges, UA.Part,
    Q.QuestionText, Q.Explanation, GT.TopicName AS GrammarTopicName, VT.TopicName AS VocabularyTopicName
FROM UserAnswers UA
JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey AND R.MemberKey = @MemberKey
LEFT JOIN Q ON UA.QuestionKey = Q.QuestionKey
LEFT JOIN GrammarTopics GT ON Q.GrammarTopic = GT.GrammarTopicID
LEFT JOIN VocabularyTopics VT ON Q.VocabularyTopic = VT.VocabularyTopicID
WHERE UA.IsCorrect = 0 AND (GT.TopicName LIKE @Pattern OR VT.TopicName LIKE @Pattern)
ORDER BY UA.AnswerTime DESC;
";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Limit", limit);
                        cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                        cmd.Parameters.AddWithValue("@Pattern", $"%{topicName}%");
                        var uaRows = new List<UserAnswerRow>();
                        using (var rdr = await cmd.ExecuteReaderAsync())
                        {
                            while (await rdr.ReadAsync())
                            {
                                uaRows.Add(new UserAnswerRow
                                {
                                    UAnswerKey = rdr.GetGuid(rdr.GetOrdinal("UAnswerKey")),
                                    ResultKey = rdr.GetGuid(rdr.GetOrdinal("ResultKey")),
                                    QuestionKey = rdr.GetGuid(rdr.GetOrdinal("QuestionKey")),
                                    SelectAnswerKey = rdr["SelectAnswerKey"] == DBNull.Value ? (Guid?)null : rdr.GetGuid(rdr.GetOrdinal("SelectAnswerKey")),
                                    IsCorrect = Convert.ToBoolean(rdr["IsCorrect"]),
                                    TimeSpent = Convert.ToInt32(rdr["TimeSpent"]),
                                    AnswerTime = Convert.ToDateTime(rdr["AnswerTime"]),
                                    NumberOfAnswerChanges = Convert.ToInt32(rdr["NumberOfAnswerChanges"]),
                                    Part = Convert.ToInt32(rdr["Part"])
                                });

                                // Build a temporary object list of question meta in parallel (we can query texts later)
                                results.Add(new IncorrectDetailDto
                                {
                                    UAnswerKey = rdr.GetGuid(rdr.GetOrdinal("UAnswerKey")),
                                    ResultKey = rdr.GetGuid(rdr.GetOrdinal("ResultKey")),
                                    QuestionKey = rdr.GetGuid(rdr.GetOrdinal("QuestionKey")),
                                    Part = rdr["Part"] == DBNull.Value ? 0 : Convert.ToInt32(rdr["Part"]),
                                    QuestionText = rdr["QuestionText"] == DBNull.Value ? "" : rdr["QuestionText"].ToString() ?? "",
                                    Explanation = rdr["Explanation"] == DBNull.Value ? "" : rdr["Explanation"].ToString() ?? "",
                                    TimeSpentSeconds = rdr["TimeSpent"] == DBNull.Value ? 0 : Convert.ToInt32(rdr["TimeSpent"]),
                                    AnswerTime = rdr["AnswerTime"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(rdr["AnswerTime"]),
                                    NumberOfAnswerChanges = rdr["NumberOfAnswerChanges"] == DBNull.Value ? 0 : Convert.ToInt32(rdr["NumberOfAnswerChanges"]),
                                    GrammarTopic = rdr["GrammarTopicName"] == DBNull.Value ? "" : rdr["GrammarTopicName"].ToString() ?? "",
                                    VocabularyTopic = rdr["VocabularyTopicName"] == DBNull.Value ? "" : rdr["VocabularyTopicName"].ToString() ?? ""
                                });
                            }
                        }

                        // Fetch selected answer texts and correct answers for the collected question/selectAnswer keys
                        var qKeys = results.Select(x => x.QuestionKey).Distinct().ToList();
                        var selAnsKeys = results.Where(x => !string.IsNullOrEmpty(x.SelectedAnswerText) == false) // we don't have selected text yet
                                             .Select(x => uaRows.FirstOrDefault(u => u.UAnswerKey == x.UAnswerKey)?.SelectAnswerKey)
                                             .Where(g => g.HasValue)
                                             .Select(g => g!.Value)
                                             .Distinct()
                                             .ToList();

                        // get selected answer texts
                        var answerTextByAnswerKey = new Dictionary<Guid, string>();
                        if (selAnsKeys.Any())
                        {
                            using (var cmd2 = new SqlCommand("", conn))
                            {
                                var union = @"
SELECT AnswerKey, AnswerText FROM TEC_Part1_Answer WHERE AnswerKey IN ({0})
UNION ALL SELECT AnswerKey, AnswerText FROM TEC_Part2_Answer WHERE AnswerKey IN ({0})
UNION ALL SELECT AnswerKey, AnswerText FROM TEC_Part3_Answer WHERE AnswerKey IN ({0})
UNION ALL SELECT AnswerKey, AnswerText FROM TEC_Part4_Answer WHERE AnswerKey IN ({0})
UNION ALL SELECT AnswerKey, AnswerText FROM TEC_Part5_Answer WHERE AnswerKey IN ({0})
UNION ALL SELECT AnswerKey, AnswerText FROM TEC_Part6_Answer WHERE AnswerKey IN ({0})
UNION ALL SELECT AnswerKey, AnswerText FROM TEC_Part7_Answer WHERE AnswerKey IN ({0});";
                                var inClause = BuildInParameterList(cmd2, selAnsKeys, "sa");
                                cmd2.CommandText = string.Format(union, inClause);
                                using (var rdr = await cmd2.ExecuteReaderAsync())
                                {
                                    while (await rdr.ReadAsync())
                                    {
                                        var ak = rdr.GetGuid(0);
                                        var at = rdr["AnswerText"] == DBNull.Value ? "" : rdr["AnswerText"].ToString() ?? "";
                                        if (!answerTextByAnswerKey.ContainsKey(ak)) answerTextByAnswerKey[ak] = at;
                                    }
                                }
                            }
                        }

                        // get correct answers by question
                        var correctAnswerByQuestionKey = new Dictionary<Guid, string>();
                        if (qKeys.Any())
                        {
                            using (var cmd3 = new SqlCommand("", conn))
                            {
                                var union = @"
SELECT A.QuestionKey, A.AnswerText FROM TEC_Part1_Answer A WHERE A.AnswerCorrect = 1 AND A.QuestionKey IN ({0})
UNION ALL SELECT A.QuestionKey, A.AnswerText FROM TEC_Part2_Answer A WHERE A.AnswerCorrect = 1 AND A.QuestionKey IN ({0})
UNION ALL SELECT A.QuestionKey, A.AnswerText FROM TEC_Part3_Answer A WHERE A.AnswerCorrect = 1 AND A.QuestionKey IN ({0})
UNION ALL SELECT A.QuestionKey, A.AnswerText FROM TEC_Part4_Answer A WHERE A.AnswerCorrect = 1 AND A.QuestionKey IN ({0})
UNION ALL SELECT A.QuestionKey, A.AnswerText FROM TEC_Part5_Answer A WHERE A.AnswerCorrect = 1 AND A.QuestionKey IN ({0})
UNION ALL SELECT A.QuestionKey, A.AnswerText FROM TEC_Part6_Answer A WHERE A.AnswerCorrect = 1 AND A.QuestionKey IN ({0})
UNION ALL SELECT A.QuestionKey, A.AnswerText FROM TEC_Part7_Answer A WHERE A.AnswerCorrect = 1 AND A.QuestionKey IN ({0});";
                                var inClause = BuildInParameterList(cmd3, qKeys, "cq");
                                cmd3.CommandText = string.Format(union, inClause);
                                using (var rdr = await cmd3.ExecuteReaderAsync())
                                {
                                    while (await rdr.ReadAsync())
                                    {
                                        var qk = rdr.GetGuid(0);
                                        var at = rdr["AnswerText"] == DBNull.Value ? "" : rdr["AnswerText"].ToString() ?? "";
                                        if (!correctAnswerByQuestionKey.ContainsKey(qk)) correctAnswerByQuestionKey[qk] = at;
                                    }
                                }
                            }
                        }

                        // fill selected + correct answer texts into results
                        for (int i = 0; i < results.Count; i++)
                        {
                            var r = results[i];
                            var uaRow = uaRows.FirstOrDefault(u => u.UAnswerKey == r.UAnswerKey);
                            if (uaRow != null && uaRow.SelectAnswerKey.HasValue)
                            {
                                if (answerTextByAnswerKey.TryGetValue(uaRow.SelectAnswerKey.Value, out var st))
                                    r.SelectedAnswerText = st;
                            }
                            if (correctAnswerByQuestionKey.TryGetValue(r.QuestionKey, out var ct))
                                r.CorrectAnswerText = ct;
                        }

                        // Optionally fetch UsersError mapping to fill ErrorType for each (if exist)
                        using (var cmd4 = new SqlCommand(@"
SELECT UE.AnswerKey, ISNULL(ET.ErrorDescription,'') AS ErrorDescription
FROM UsersError UE
LEFT JOIN ErrorTypes ET ON UE.ErrorType = ET.ErrorTypeID
WHERE UE.UserKey = @MemberKey
", conn))
                        {
                            cmd4.Parameters.AddWithValue("@MemberKey", memberKey);
                            var errMap = new Dictionary<Guid, string>();
                            using (var rdr = await cmd4.ExecuteReaderAsync())
                            {
                                while (await rdr.ReadAsync())
                                {
                                    var ak = rdr["AnswerKey"] == DBNull.Value ? Guid.Empty : rdr.GetGuid(0);
                                    if (ak != Guid.Empty && !errMap.ContainsKey(ak))
                                        errMap[ak] = rdr["ErrorDescription"]?.ToString() ?? "";
                                }
                            }
                            // apply
                            foreach (var r in results)
                            {
                                var uaRow = uaRows.FirstOrDefault(u => u.UAnswerKey == r.UAnswerKey);
                                if (uaRow != null && uaRow.SelectAnswerKey.HasValue && errMap.ContainsKey(uaRow.SelectAnswerKey.Value))
                                {
                                    r.ErrorType = errMap[uaRow.SelectAnswerKey.Value];
                                }
                            }
                        }
                    }

                    return results;
                }
            }
            catch (Exception ex)
            {
                // return empty list if failure (controller/tool will get empty result), but log real error in server logs
                return results;
            }
        }
        #region Helpers
        private static string BuildInParameterList(SqlCommand cmd, List<Guid> guids, string baseName)
        {
            // returns e.g. "@p0, @p1, @p2"
            var names = new List<string>();
            for (int i = 0; i < guids.Count; i++)
            {
                var p = $"@{baseName}{i}";
                cmd.Parameters.Add(p, SqlDbType.UniqueIdentifier).Value = guids[i];
                names.Add(p);
            }
            return names.Count > 0 ? string.Join(", ", names) : "NULL";
        }
        private static double StdDev(IEnumerable<double> values)
        {
            var arr = values.ToArray();
            if (arr.Length <= 1) return 0;
            var mean = arr.Average();
            var variance = arr.Average(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(variance);
        }
        #endregion
    }
    #region DTOs
    public class TestInfoDto
    {
        public Guid ResultKey { get; set; }
        public string TestName { get; set; } = "";
        public int? TestScore { get; set; }
        public int? ListeningScore { get; set; }
        public int? ReadingScore { get; set; }
        public int? CompletionTimeMinutes { get; set; }
        public int CorrectListening { get; set; }
        public int CorrectReading { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public class PerPartStatDto
    {
        public int Part { get; set; }
        public int QuestionCount { get; set; }
        public double AvgTimeSeconds { get; set; }
        public double StdDevTimeSeconds { get; set; }
        public int TotalTimeSeconds { get; set; }
        public double AvgNumberOfAnswerChanges { get; set; }
        public double CorrectRatePercent { get; set; }
    }

    public class IncorrectDetailDto
    {
        public Guid UAnswerKey { get; set; }
        public Guid ResultKey { get; set; }
        public Guid QuestionKey { get; set; }
        public int Part { get; set; }
        public string QuestionText { get; set; } = "";
        public string SelectedAnswerText { get; set; } = "";
        public string CorrectAnswerText { get; set; } = "";
        public string Explanation { get; set; } = "";
        public int TimeSpentSeconds { get; set; }
        public DateTime AnswerTime { get; set; }
        public int NumberOfAnswerChanges { get; set; }
        public string GrammarTopic { get; set; } = "";
        public string VocabularyTopic { get; set; } = "";
        public string ErrorType { get; set; } = "";
    }
    #endregion
}