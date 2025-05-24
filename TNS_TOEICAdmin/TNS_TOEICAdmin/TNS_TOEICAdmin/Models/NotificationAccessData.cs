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
    }
}
