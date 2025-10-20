using Microsoft.Data.SqlClient;
using TNS_TOEICAdmin.Models;
using Microsoft.Data.SqlClient;

namespace TNS_TOEICAdmin.Models
{
    public class QuestionAttributesAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        /// <summary>
        /// Lấy danh sách attributes theo loại
        /// </summary>
        public static async Task<(List<Dictionary<string, object>> data, int totalItems)> GetAttributesAsync(string type, int page, int pageSize, string search)
        {
            var data = new List<Dictionary<string, object>>();
            int totalItems = 0;

            string tableName = GetTableName(type);
            string keyColumn = GetKeyColumn(type);
            string nameColumn = GetNameColumn(type);

            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Invalid attribute type.");
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Count total items
                string countSql = $@"
                    SELECT COUNT(*) 
                    FROM [dbo].[{tableName}]
                    WHERE (@Search IS NULL OR [{nameColumn}] LIKE @Search)";

                using (var cmd = new SqlCommand(countSql, conn))
                {
                    cmd.Parameters.AddWithValue("@Search", string.IsNullOrEmpty(search) ? DBNull.Value : $"%{search}%");
                    totalItems = (int)await cmd.ExecuteScalarAsync();
                }

                // Get paginated data
                string sql = $@"
                    SELECT [{keyColumn}], [{nameColumn}]
                    FROM [dbo].[{tableName}]
                    WHERE (@Search IS NULL OR [{nameColumn}] LIKE @Search)
                    ORDER BY [{nameColumn}]
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Search", string.IsNullOrEmpty(search) ? DBNull.Value : $"%{search}%");
                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            data.Add(new Dictionary<string, object>
                            {
                                { "Key", reader.GetGuid(0).ToString() }, // Tất cả đều là GUID
                                { "Name", reader.GetString(1) }
                            });
                        }
                    }
                }
            }

            return (data, totalItems);
        }

        /// <summary>
        /// Tạo mới attribute
        /// </summary>
        public static async Task CreateAttributeAsync(string type, string name)
        {
            string tableName = GetTableName(type);
            string keyColumn = GetKeyColumn(type);
            string nameColumn = GetNameColumn(type);

            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Invalid attribute type.");
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Tất cả các bảng đều dùng UNIQUEIDENTIFIER (GUID)
                string sql = $@"
                    INSERT INTO [dbo].[{tableName}] ([{keyColumn}], [{nameColumn}])
                    VALUES (NEWID(), @Name)";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Cập nhật attribute
        /// </summary>
        public static async Task UpdateAttributeAsync(string type, string key, string name)
        {
            string tableName = GetTableName(type);
            string keyColumn = GetKeyColumn(type);
            string nameColumn = GetNameColumn(type);

            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Invalid attribute type.");
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string sql = $@"
                    UPDATE [dbo].[{tableName}]
                    SET [{nameColumn}] = @Name
                    WHERE [{keyColumn}] = @Key";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Key", Guid.Parse(key)); // Tất cả đều parse thành Guid
                    cmd.Parameters.AddWithValue("@Name", name);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Xóa attribute (hard delete)
        /// </summary>
        public static async Task DeleteAttributeAsync(string type, string key)
        {
            string tableName = GetTableName(type);
            string keyColumn = GetKeyColumn(type);

            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Invalid attribute type.");
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string sql = $@"
                    DELETE FROM [dbo].[{tableName}]
                    WHERE [{keyColumn}] = @Key";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Key", Guid.Parse(key)); // Tất cả đều parse thành Guid
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        #region [Helper Methods]
        private static string GetTableName(string type)
        {
            return type switch
            {
                "ErrorType" => "ErrorTypes",
                "VocabularyTopic" => "VocabularyTopics",
                "GrammarTopic" => "GrammarTopics",
                "Category" => "TEC_Category",
                _ => null
            };
        }

        private static string GetKeyColumn(string type)
        {
            return type switch
            {
                "ErrorType" => "ErrorTypeID",
                "VocabularyTopic" => "VocabularyTopicID",
                "GrammarTopic" => "GrammarTopicID",
                "Category" => "CategoryKey",
                _ => null
            };
        }

        private static string GetNameColumn(string type)
        {
            return type switch
            {
                "ErrorType" => "ErrorDescription",
                "VocabularyTopic" => "TopicName",
                "GrammarTopic" => "TopicName",
                "Category" => "CategoryName",
                _ => null
            };
        }
        #endregion
    }
}