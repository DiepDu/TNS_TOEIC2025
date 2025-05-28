using Microsoft.Data.SqlClient;

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
}