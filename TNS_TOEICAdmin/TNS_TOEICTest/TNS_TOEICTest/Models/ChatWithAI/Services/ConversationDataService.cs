using Google.Cloud.AIPlatform.V1;
using Microsoft.Data.SqlClient;
using System.Data;

namespace TNS_TOEICTest.Models.ChatWithAI.Services
{
    public class ConversationDataService
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
                            DateTime? startedAt = reader["StartedAt"] == DBNull.Value
                                ? (DateTime?)null
                                : (DateTime)reader["StartedAt"];

                            string title;
                            if (reader["Title"] == DBNull.Value || string.IsNullOrEmpty(reader["Title"].ToString()))
                            {
                                title = startedAt.HasValue
                                    ? startedAt.Value.ToString("yyyy-MM-dd HH:mm")
                                    : "Cuộc hội thoại mới";
                            }
                            else
                            {
                                title = reader["Title"].ToString();
                            }

                            var conversation = new Dictionary<string, object>
                            {
                                { "ConversationAIID", reader["ConversationAIID"] },
                                { "UserID", reader["UserID"] },
                                { "Title", title },
                                { "StartedAt", startedAt },
                                { "LastMessage", reader["LastMessage"] == DBNull.Value ? "Chưa có tin nhắn." : reader["LastMessage"] }
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
            // ✅ VALIDATE
            if (string.IsNullOrEmpty(content))
            {
                Console.WriteLine($"[SaveMessage WARNING] Empty content for role={role}");
                content = "[Empty response]";
            }

            // ✅ TRUNCATE nếu quá dài (safety)
            const int MAX_LENGTH = 1_000_000; // 1MB text
            if (content.Length > MAX_LENGTH)
            {
                Console.WriteLine($"[SaveMessage WARNING] Content too long ({content.Length} chars), truncating...");
                content = content.Substring(0, MAX_LENGTH) + "\n\n[... Content truncated ...]";
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // ✅ FIX: Query string PHẢI ĐÚNG FORMAT
                var query = @"
            INSERT INTO MessageWithAI 
                (MessageAIID, ConversationAIID, SenderRole, Content, Timestamp) 
            VALUES 
                (@MessageAIID, @ConversationAIID, @SenderRole, @Content, @Timestamp)";

                using (var command = new SqlCommand(query, connection))
                {
                    // ✅ ADD ALL PARAMETERS EXPLICITLY
                    command.Parameters.Add("@MessageAIID", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
                    command.Parameters.Add("@ConversationAIID", SqlDbType.UniqueIdentifier).Value = conversationId;
                    command.Parameters.Add("@SenderRole", SqlDbType.NVarChar, 10).Value = role;
                    command.Parameters.Add("@Content", SqlDbType.NVarChar, -1).Value = content; // -1 = MAX
                    command.Parameters.Add("@Timestamp", SqlDbType.DateTime).Value = DateTime.UtcNow;

                    try
                    {
                        var rowsAffected = await command.ExecuteNonQueryAsync();
                        Console.WriteLine($"[SaveMessage OK] Role={role}, Length={content.Length} chars, Rows={rowsAffected}");
                    }
                    catch (SqlException sqlEx)
                    {
                        Console.WriteLine($"[SaveMessage SQL ERROR]: {sqlEx.Message}");
                        Console.WriteLine($"[Query]: {query}");

                        // ✅ DEBUG: Print all parameters
                        Console.WriteLine("[Parameters]:");
                        foreach (SqlParameter p in command.Parameters)
                        {
                            var valuePreview = p.Value?.ToString();
                            if (valuePreview != null && valuePreview.Length > 100)
                                valuePreview = valuePreview.Substring(0, 100) + "...";
                            Console.WriteLine($"  {p.ParameterName} ({p.SqlDbType}): {valuePreview}");
                        }
                        throw;
                    }
                }
            }
        }
        public static async Task<IEnumerable<Content>> GetMessageHistoryForApiAsync(
            Guid conversationId,
            int limit = 10) // ✅ THÊM PARAMETER
        {
            var history = new List<Content>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // ✅ DYNAMIC LIMIT
                var commandText = $"SELECT TOP {limit} SenderRole, Content FROM MessageWithAI WHERE ConversationAIID = @ConversationAIID ORDER BY Timestamp ASC";

                Console.WriteLine($"[GetMessageHistoryForApiAsync] Fetching last {limit} messages for conversation {conversationId}");

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

            Console.WriteLine($"[GetMessageHistoryForApiAsync] Fetched {history.Count} messages");
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
    }
}

