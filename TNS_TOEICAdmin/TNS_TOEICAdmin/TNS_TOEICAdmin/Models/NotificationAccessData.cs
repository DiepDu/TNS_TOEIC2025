using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TNS_TOEICAdmin.Models
{
    public class NotificationAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public static async Task InsertNotificationAsync(string type, string content, Guid relatedKey, Guid targetKey, string targetType, string project)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Kiểm tra trùng lặp dựa trên Type, Content, RelatedKey
                var checkQuery = @"
            SELECT COUNT(*) 
            FROM Notifications 
            WHERE Type = @Type AND Content = @Content AND RelatedKey = @RelatedKey";
                using (var checkCommand = new SqlCommand(checkQuery, connection))
                {
                    checkCommand.Parameters.AddWithValue("@Type", type);
                    checkCommand.Parameters.AddWithValue("@Content", content ?? (object)DBNull.Value);
                    checkCommand.Parameters.AddWithValue("@RelatedKey", relatedKey);
                    var count = (int)await checkCommand.ExecuteScalarAsync();
                    if (count > 0) return; // Bỏ qua nếu đã tồn tại
                }

                var query = @"
            INSERT INTO Notifications (
                NotificationKey, Type, Content, RelatedKey, CreatedOn, TargetKey, TargetType, IsRead, Project
            )
            VALUES (
                @NotificationKey, @Type, @Content, @RelatedKey, @CreatedOn, @TargetKey, @TargetType, @IsRead, @Project
            )";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@NotificationKey", Guid.NewGuid());
                    command.Parameters.AddWithValue("@Type", type);
                    command.Parameters.AddWithValue("@Content", content ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@RelatedKey", relatedKey);
                    command.Parameters.AddWithValue("@CreatedOn", DateTime.Now);
                    command.Parameters.AddWithValue("@TargetKey", targetKey);
                    command.Parameters.AddWithValue("@TargetType", targetType);
                    command.Parameters.AddWithValue("@IsRead", false);
                    command.Parameters.AddWithValue("@Project", project);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task<List<Notification>> GetNotificationsAsync(string userKey, string project, int skip = 0, int take = 30)
        {
            var notifications = new List<Notification>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
            SELECT NotificationKey, Type, Content, RelatedKey, CreatedOn, TargetKey, TargetType, IsRead, Project
            FROM Notifications
            WHERE (TargetKey = @UserKey OR TargetKey = '00000000-0000-0000-0000-000000000000')
            AND Project = @Project
            ORDER BY CreatedOn DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserKey", Guid.Parse(userKey));
                    command.Parameters.AddWithValue("@Project", project);
                    command.Parameters.AddWithValue("@Skip", skip);
                    command.Parameters.AddWithValue("@Take", take);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            notifications.Add(new Notification
                            {
                                NotificationKey = reader.GetGuid(0),
                                Type = reader.GetString(1),
                                Content = reader.GetString(2),
                                RelatedKey = reader.GetGuid(3),
                                CreatedOn = reader.GetDateTime(4),
                                TargetKey = reader.GetGuid(5),
                                TargetType = reader.GetString(6),
                                IsRead = reader.GetBoolean(7),
                                Project = reader.GetString(8)
                            });
                        }
                    }
                }
            }
            return notifications;
        }

        public static async Task<int> GetTotalCountAsync(string userKey, string project)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
            SELECT COUNT(*)
            FROM Notifications
            WHERE (TargetKey = @UserKey OR TargetKey = '00000000-0000-0000-0000-000000000000')
            AND Project = @Project";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserKey", Guid.Parse(userKey));
                    command.Parameters.AddWithValue("@Project", project);

                    return (int)await command.ExecuteScalarAsync();
                }
            }
        }

        public static async Task<int> GetUnreadCountAsync(string userKey, string project)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
            SELECT COUNT(*)
            FROM Notifications
            WHERE (TargetKey = @UserKey OR TargetKey = '00000000-0000-0000-0000-000000000000')
            AND Project = @Project AND IsRead = 0";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserKey", Guid.Parse(userKey));
                    command.Parameters.AddWithValue("@Project", project);

                    return (int)await command.ExecuteScalarAsync();
                }
            }
        }

        public static async Task MarkAsReadAsync(string userKey, string project, Guid[] notificationIds)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
            UPDATE Notifications
            SET IsRead = 1
            WHERE (TargetKey = @UserKey OR TargetKey = '00000000-0000-0000-0000-000000000000')
            AND Project = @Project 
            AND NotificationKey IN ({0})";

                var notificationIdsString = string.Join(",", notificationIds.Select(id => $"'{id}'"));
                query = string.Format(query, notificationIdsString);

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserKey", Guid.Parse(userKey));
                    command.Parameters.AddWithValue("@Project", project);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        // TÌM VÀ THAY THẾ PHƯƠNG THỨC NÀY TRONG NotificationAccessData.cs

        public static async Task<List<Feedback>> GetFeedbacksAsync(int skip = 0, int take = 50)
        {
            var feedbacks = new List<Feedback>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = @"
            SELECT f.FeedbackKey, f.QuestionKey, f.MemberKey, f.FeedbackText, f.CreatedOn, f.Part, f.Status,
                   m.MemberName, m.Avatar
            FROM QuestionFeedbacks f
            JOIN EDU_Member m ON f.MemberKey = m.MemberKey
           
            -- --- ĐẢM BẢO DÒNG NÀY LUÔN LÀ DESC ---
            ORDER BY f.CreatedOn DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Skip", skip);
                    command.Parameters.AddWithValue("@Take", take);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            feedbacks.Add(new Feedback
                            {
                                // ... code đọc dữ liệu không đổi ...
                                FeedbackKey = reader.GetGuid(0),
                                QuestionKey = reader.GetGuid(1),
                                MemberKey = reader.GetGuid(2),
                                Content = reader.GetString(3),
                                CreatedOn = reader.GetDateTime(4),
                                Part = reader.GetInt32(5),
                                Status = reader.GetInt32(6),
                                Name = reader.GetString(7),
                                AvatarUrl = reader.IsDBNull(8) ? "/images/avatar/default-avatar.jpg" : reader.GetString(8)
                            });
                        }
                    }
                }
            }
            return feedbacks;
        }

        public static async Task<int> GetFeedbackTotalCountAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT COUNT(*) FROM QuestionFeedbacks";
                using (var command = new SqlCommand(query, connection))
                {
                    return (int)await command.ExecuteScalarAsync();
                }
            }
        }

        public static async Task<bool> MarkFeedbackAsResolvedAsync(Guid feedbackId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "UPDATE QuestionFeedbacks SET Status = 1 WHERE FeedbackKey = @FeedbackId";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@FeedbackId", feedbackId);
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }
        public static async Task<(bool success, string message)> SendFeedbackReplyAsync(Guid feedbackKey, Guid memberKey, string replyContent, string adminKey)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // BƯỚC 1: Lấy hoặc tạo kênh Admin cho member.
                        string conversationKey = await GetOrCreateAdminChannelAsync(memberKey, connection, transaction);
                        if (string.IsNullOrEmpty(conversationKey))
                        {
                            throw new Exception("Could not get or create an admin channel for the member.");
                        }

                        // BƯỚC 2: Insert tin nhắn vào bảng Messages.
                        var messageKey = Guid.NewGuid();
                        var createdOn = DateTime.Now;
                        var finalContent = $"Feedback from administrator: {replyContent}";

                        var insertMsgQuery = @"
                            INSERT INTO Messages 
                            (MessageKey, ConversationKey, SenderKey, SenderType, ReceiverKey, ReceiverType, MessageType, Content, CreatedOn, Status, IsPinned, IsSystemMessage)
                            VALUES 
                            (@MessageKey, @ConversationKey, @SenderKey, 'Admin', @ReceiverKey, 'Member', 'Text', @Content, @CreatedOn, 0, 0, 0)";

                        using (var insertCmd = new SqlCommand(insertMsgQuery, connection, transaction))
                        {
                            insertCmd.Parameters.AddWithValue("@MessageKey", messageKey);
                            insertCmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            insertCmd.Parameters.AddWithValue("@SenderKey", adminKey);
                            insertCmd.Parameters.AddWithValue("@ReceiverKey", memberKey);
                            insertCmd.Parameters.AddWithValue("@Content", finalContent);
                            insertCmd.Parameters.AddWithValue("@CreatedOn", createdOn);
                            await insertCmd.ExecuteNonQueryAsync();
                        }

                        // BƯỚC 3: Cập nhật LastMessage và UnreadCount.
                        var updateConvQuery = @"
                            UPDATE Conversations 
                            SET LastMessageKey = @MessageKey, LastMessageTime = @CreatedOn 
                            WHERE ConversationKey = @ConversationKey";
                        using (var updateConvCmd = new SqlCommand(updateConvQuery, connection, transaction))
                        {
                            updateConvCmd.Parameters.AddWithValue("@MessageKey", messageKey);
                            updateConvCmd.Parameters.AddWithValue("@CreatedOn", createdOn);
                            updateConvCmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            await updateConvCmd.ExecuteNonQueryAsync();
                        }

                        var updateParticipantQuery = @"
                            UPDATE ConversationParticipants
                            SET UnreadCount = UnreadCount + 1
                            WHERE ConversationKey = @ConversationKey AND UserKey = @MemberKey";
                        using (var updateParticipantCmd = new SqlCommand(updateParticipantQuery, connection, transaction))
                        {
                            updateParticipantCmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            updateParticipantCmd.Parameters.AddWithValue("@MemberKey", memberKey);
                            await updateParticipantCmd.ExecuteNonQueryAsync();
                        }

                        // (Tùy chọn) Đánh dấu feedback gốc là đã được phản hồi (Status = 2)
                        var updateFeedbackQuery = "UPDATE QuestionFeedbacks SET Status = 2 WHERE FeedbackKey = @FeedbackKey";
                        using (var updateFeedbackCmd = new SqlCommand(updateFeedbackQuery, connection, transaction))
                        {
                            updateFeedbackCmd.Parameters.AddWithValue("@FeedbackKey", feedbackKey);
                            await updateFeedbackCmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        return (true, "Reply sent successfully.");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        // Ghi lại lỗi để debug
                        Console.WriteLine($"[SendFeedbackReplyAsync] Error: {ex.Message}");
                        return (false, "An internal error occurred.");
                    }
                }
            }
        }

        /// <summary>
        /// Hàm helper để tìm hoặc tạo một kênh giao tiếp Admin riêng cho từng Member.
        /// </summary>
        private static async Task<string> GetOrCreateAdminChannelAsync(Guid memberKey, SqlConnection connection, SqlTransaction transaction)
        {
            // BƯỚC 1: Tìm kênh Admin đã có.
            // LƯU Ý QUAN TRỌNG: Cần đảm bảo bạn đã thêm cột `IsAdminChannelForMemberKey` (UNIQUEIDENTIFIER, NULL) vào bảng Conversations.
            var findQuery = "SELECT ConversationKey FROM Conversations WHERE IsAdminChannelForMemberKey = @MemberKey";
            using (var findCmd = new SqlCommand(findQuery, connection, transaction))
            {
                findCmd.Parameters.AddWithValue("@MemberKey", memberKey);
                var result = await findCmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    return result.ToString(); // Trả về key nếu đã tồn tại
                }
            }

            // BƯỚC 2: Nếu không có, tạo kênh mới.
            var conversationKey = Guid.NewGuid();
            var now = DateTime.Now;

            // Insert vào Conversations
            var createConvQuery = @"
                INSERT INTO Conversations 
                (ConversationKey, ConversationType, CreatedOn, ConversationMode, Name, CreatorKey, IsActive, IsAdminChannelForMemberKey)
                VALUES 
                (@ConversationKey, 'Private', @CreatedOn, 'Private', 'Administrator', NULL, 1, @MemberKey)";
            using (var createConvCmd = new SqlCommand(createConvQuery, connection, transaction))
            {
                createConvCmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                createConvCmd.Parameters.AddWithValue("@CreatedOn", now);
                createConvCmd.Parameters.AddWithValue("@MemberKey", memberKey);
                await createConvCmd.ExecuteNonQueryAsync();
            }

            // Insert vào ConversationParticipants
            var createParticipantQuery = @"
                INSERT INTO ConversationParticipants
                (ParticipantKey, ConversationKey, UserKey, UserType, Role, JoinedOn, UnreadCount, IsBanned, IsApproved)
                VALUES 
                (@ParticipantKey, @ConversationKey, @UserKey, 'Member', 'Member', @JoinedOn, 0, 0, 1)";
            using (var createParticipantCmd = new SqlCommand(createParticipantQuery, connection, transaction))
            {
                createParticipantCmd.Parameters.AddWithValue("@ParticipantKey", Guid.NewGuid());
                createParticipantCmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                createParticipantCmd.Parameters.AddWithValue("@UserKey", memberKey);
                createParticipantCmd.Parameters.AddWithValue("@JoinedOn", now);
                await createParticipantCmd.ExecuteNonQueryAsync();
            }

            return conversationKey.ToString();
        }
        // --- KẾT THÚC CODE MỚI ---
    }

    public class Notification
    {
        public Guid NotificationKey { get; set; }
        public string Type { get; set; }
        public string Content { get; set; }
        public Guid RelatedKey { get; set; }
        public DateTime CreatedOn { get; set; }
        public Guid TargetKey { get; set; }
        public string TargetType { get; set; }
        public bool IsRead { get; set; }
        public string Project { get; set; }
    }

    public class Feedback
    {
        public Guid FeedbackKey { get; set; }
        public Guid QuestionKey { get; set; }
        public Guid MemberKey { get; set; }
        public string Content { get; set; }
        public DateTime CreatedOn { get; set; }
        public int Part { get; set; }
        public int Status { get; set; }
        public string Name { get; set; }
        public string AvatarUrl { get; set; }
    }

}