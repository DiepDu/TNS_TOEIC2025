using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TNS_TOEICAdmin.Models
{
    public class ChatAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
        private static readonly string _mediaRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "messages");
        public static async Task<Dictionary<string, object>> GetConversationsAsync(string userKey = null, string memberKey = null, string currentMemberKey = null)
        {
            var conversations = new List<Dictionary<string, object>>();
            int totalUnreadCount = 0;
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
            SELECT DISTINCT 
                c.ConversationKey,
                c.ConversationType,
                cp.UnreadCount,
                CASE 
                    WHEN c.ConversationType = 'Group' THEN c.Name
                    ELSE 
                        COALESCE(
                            (SELECT TOP 1 m.MemberName 
                             FROM ConversationParticipants cp2 
                             JOIN EDU_Member m ON cp2.UserKey = m.MemberKey 
                             WHERE cp2.ConversationKey = c.ConversationKey AND cp2.UserKey != @MemberKey AND cp2.UserType = 'Member'),
                            (SELECT TOP 1 u.UserName 
                             FROM ConversationParticipants cp2 
                             JOIN SYS_Users u ON cp2.UserKey = u.UserKey 
                             WHERE cp2.ConversationKey = c.ConversationKey AND cp2.UserKey != @MemberKey AND cp2.UserType = 'Admin'),
                            'Unknown'
                        )
                END AS DisplayName,
                CASE 
                    WHEN c.ConversationType = 'Group' THEN COALESCE(c.GroupAvatar, '/images/avatar/default-avatar.jpg')
                    ELSE 
                        COALESCE(
                            (SELECT TOP 1 m.Avatar 
                             FROM ConversationParticipants cp2 
                             JOIN EDU_Member m ON cp2.UserKey = m.MemberKey 
                             WHERE cp2.ConversationKey = c.ConversationKey AND cp2.UserKey != @MemberKey AND cp2.UserType = 'Member'),
                            (SELECT TOP 1 e.PhotoPath 
                             FROM ConversationParticipants cp2 
                             JOIN SYS_Users u ON cp2.UserKey = u.UserKey 
                             JOIN HRM_Employee e ON u.EmployeeKey = e.EmployeeKey 
                             WHERE cp2.ConversationKey = c.ConversationKey AND cp2.UserKey != @MemberKey AND cp2.UserType = 'Admin'),
                            '/images/avatar/default-avatar.jpg'
                        )
                END AS Avatar,
                CASE 
                    WHEN c.ConversationType = 'Group' THEN NULL
                    ELSE 
                        COALESCE(
                            (SELECT TOP 1 cp2.UserType 
                             FROM ConversationParticipants cp2 
                             WHERE cp2.ConversationKey = c.ConversationKey AND cp2.UserKey != @MemberKey),
                            NULL
                        )
                END AS PartnerUserType,
                (SELECT TOP 1 cp2.UserKey 
                 FROM ConversationParticipants cp2 
                 WHERE cp2.ConversationKey = c.ConversationKey AND cp2.UserKey != @MemberKey) AS PartnerUserKey,
                c.LastMessageKey,
                c.LastMessageTime,
                m.Content,
                m.SenderKey,
                c.Name,
                cp.IsBanned
            FROM ConversationParticipants cp
            JOIN Conversations c ON cp.ConversationKey = c.ConversationKey
            LEFT JOIN Messages m ON c.LastMessageKey = m.MessageKey
            WHERE cp.UserKey = @MemberKey
            AND cp.IsApproved = 1
            AND c.IsActive = 1";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@MemberKey", memberKey ?? currentMemberKey ?? (object)DBNull.Value);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var partnerUserType = reader["PartnerUserType"]?.ToString();
                            var avatarUrl = reader["Avatar"]?.ToString() ?? "/images/avatar/default-avatar.jpg";
                            if (partnerUserType == "Admin" && !string.IsNullOrEmpty(avatarUrl))
                            {
                                avatarUrl = $"https://localhost:7078/{avatarUrl}";
                            }
                            var lastMessage = reader["SenderKey"] != DBNull.Value && reader["Content"] != DBNull.Value
                                ? (reader["SenderKey"].ToString() == currentMemberKey ? "Bạn: " : reader["DisplayName"] + ": ") + (Convert.ToInt32(reader["IsBanned"]) == 1 ? "Đã bị chặn" : reader["Content"].ToString())
                                : "No messages";
                            var conversation = new Dictionary<string, object>
                    {
                        { "ConversationKey", reader["ConversationKey"] },
                        { "ConversationType", reader["ConversationType"] },
                        { "UnreadCount", reader["UnreadCount"] ?? 0 },
                        { "DisplayName", reader["DisplayName"] ?? "Unknown" },
                        { "Avatar", avatarUrl },
                        { "LastMessageKey", reader["LastMessageKey"] ?? (object)DBNull.Value },
                        { "LastMessageTime", reader["LastMessageTime"] ?? (object)DBNull.Value },
                        { "LastMessage", lastMessage },
                        { "Name", reader["Name"] ?? (object)DBNull.Value },
                        { "IsBanned", reader["IsBanned"] }
                    };

                            // Thêm UserKey và UserType cho 1-1
                            if (reader["ConversationType"].ToString() != "Group")
                            {
                                conversation.Add("PartnerUserKey", reader["PartnerUserKey"] ?? (object)DBNull.Value);
                                conversation.Add("PartnerUserType", reader["PartnerUserType"] ?? (object)DBNull.Value);
                            }

                            conversations.Add(conversation);
                            totalUnreadCount += Convert.ToInt32(reader["UnreadCount"] ?? 0);
                        }
                    }
                }
            }
            return new Dictionary<string, object>
    {
        { "conversations", conversations },
        { "totalUnreadCount", totalUnreadCount }
    };
        }

        public static async Task<List<Dictionary<string, object>>> SearchContactsAsync(string query, string memberKey)
        {
            var results = new List<Dictionary<string, object>>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var groupQuery = @"
            SELECT c.ConversationKey, c.Name AS Name, c.GroupAvatar AS Avatar, 'Group' AS UserType, c.ConversationType
            FROM Conversations c
            JOIN ConversationParticipants cp ON c.ConversationKey = cp.ConversationKey
            WHERE c.ConversationType = 'Group'
            AND cp.UserKey = @MemberKey
            AND c.Name LIKE '%' + @Query + '%'";

                var memberQuery = @"
            SELECT m.MemberKey AS UserKey, m.MemberName AS Name, m.Avatar AS Avatar, 'Member' AS UserType,
                   (SELECT TOP 1 c.ConversationKey 
                    FROM Conversations c
                    JOIN ConversationParticipants cp1 ON c.ConversationKey = cp1.ConversationKey
                    JOIN ConversationParticipants cp2 ON c.ConversationKey = cp2.ConversationKey
                    WHERE cp1.UserKey = @MemberKey AND cp2.UserKey = m.MemberKey AND c.ConversationType = 'Private') AS ConversationKey,
                   (SELECT TOP 1 c.ConversationType 
                    FROM Conversations c
                    JOIN ConversationParticipants cp1 ON c.ConversationKey = cp1.ConversationKey
                    JOIN ConversationParticipants cp2 ON c.ConversationKey = cp2.ConversationKey
                    WHERE cp1.UserKey = @MemberKey AND cp2.UserKey = m.MemberKey AND c.ConversationType = 'Private') AS ConversationType
            FROM EDU_Member m
            WHERE m.MemberName LIKE '%' + @Query + '%'
            AND m.MemberKey != @MemberKey";

                var userQuery = @"
            SELECT u.UserKey, u.UserName AS Name, e.PhotoPath AS Avatar, 'Admin' AS UserType,
                   (SELECT TOP 1 c.ConversationKey 
                    FROM Conversations c
                    JOIN ConversationParticipants cp1 ON c.ConversationKey = cp1.ConversationKey
                    JOIN ConversationParticipants cp2 ON c.ConversationKey = cp2.ConversationKey
                    WHERE cp1.UserKey = @MemberKey AND cp2.UserKey = u.UserKey AND c.ConversationType = 'Private') AS ConversationKey,
                   (SELECT TOP 1 c.ConversationType 
                    FROM Conversations c
                    JOIN ConversationParticipants cp1 ON c.ConversationKey = cp1.ConversationKey
                    JOIN ConversationParticipants cp2 ON c.ConversationKey = cp2.ConversationKey
                    WHERE cp1.UserKey = @MemberKey AND cp2.UserKey = u.UserKey AND c.ConversationType = 'Private') AS ConversationType
            FROM SYS_Users u
            JOIN HRM_Employee e ON u.EmployeeKey = e.EmployeeKey
            WHERE u.UserName LIKE '%' + @Query + '%'
            AND u.UserKey != @MemberKey";

                var queries = new[]
                {
            (query: groupQuery, hasConversationKey: true, hasUserKey: false),
            (query: memberQuery, hasConversationKey: true, hasUserKey: true),
            (query: userQuery, hasConversationKey: true, hasUserKey: true)
        };
                foreach (var (q, hasConversationKey, hasUserKey) in queries)
                {
                    using (var command = new SqlCommand(q, connection))
                    {
                        command.Parameters.AddWithValue("@MemberKey", memberKey);
                        command.Parameters.AddWithValue("@Query", query);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var result = new Dictionary<string, object>
                        {
                            { "Name", reader["Name"] },
                            { "Avatar", reader["Avatar"] ?? "/images/avatar/default-avatar.jpg" },
                            { "UserType", reader["UserType"] }
                        };
                                if (hasConversationKey && reader["ConversationKey"] != DBNull.Value)
                                    result["ConversationKey"] = reader["ConversationKey"];
                                else
                                    result["ConversationKey"] = DBNull.Value;

                                if (hasUserKey && reader["UserKey"] != DBNull.Value)
                                    result["UserKey"] = reader["UserKey"];
                                else
                                    result["UserKey"] = DBNull.Value;

                                if (reader["ConversationType"] != DBNull.Value)
                                    result["ConversationType"] = reader["ConversationType"];
                                else
                                    result["ConversationType"] = DBNull.Value;

                                if (result["UserType"].ToString() == "Admin" && !string.IsNullOrEmpty(result["Avatar"].ToString()))
                                    result["Avatar"] = $"https://localhost:7078/{result["Avatar"]}";
                                results.Add(result);
                            }
                        }
                    }
                }
            }
            return results;
        }
        public static async Task<List<Dictionary<string, object>>> GetMessagesAsync(string conversationKey, int skip = 0)
        {
            var messages = new List<Dictionary<string, object>>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
-- Lấy 100 tin nhắn gần nhất (bao gồm tin nhắn chính và tin nhắn cha nếu cần)
SELECT m.MessageKey, m.ConversationKey, m.SenderKey, m.SenderType, m.ReceiverKey, 
       m.ReceiverType, m.MessageType, m.Content, m.ParentMessageKey, m.CreatedOn, 
       m.Status, m.IsPinned, m.IsSystemMessage, -- Thêm IsSystemMessage
       ma.AttachmentKey, ma.Type AS AttachmentType, 
       CASE 
           WHEN m.SenderType = 'Admin' AND ma.Url IS NOT NULL THEN CONCAT('https://localhost:7078/', ma.Url)
           ELSE ma.Url 
       END AS Url, 
       ma.FileSize, ma.FileName, ma.MimeType,
       pm.Content AS ParentContent,
       pm.Status AS ParentStatus,
       CASE 
           WHEN m.SenderType = 'Member' THEN em.MemberName
           WHEN m.SenderType = 'Admin' THEN su.UserName
       END AS SenderName,
       CASE 
           WHEN m.SenderType = 'Member' THEN em.Avatar
           WHEN m.SenderType = 'Admin' THEN CONCAT('https://localhost:7078/', he.PhotoPath)
       END AS SenderAvatar
FROM Messages m
LEFT JOIN (
    SELECT *,
        ROW_NUMBER() OVER (PARTITION BY MessageKey ORDER BY CreatedOn DESC) AS rn
    FROM MessageAttachments
) ma ON m.MessageKey = ma.MessageKey AND ma.rn = 1
LEFT JOIN Messages pm ON m.ParentMessageKey = pm.MessageKey
LEFT JOIN [EDU_Member] em ON m.SenderType = 'Member' AND m.SenderKey = em.MemberKey
LEFT JOIN [SYS_Users] su ON m.SenderType = 'Admin' AND m.SenderKey = su.UserKey
LEFT JOIN [HRM_Employee] he ON m.SenderType = 'Admin' AND su.EmployeeKey = he.EmployeeKey
WHERE m.ConversationKey = @ConversationKey
AND m.Status IN (0, 1, 2)

UNION

-- Lấy các tin nhắn cha nằm ngoài phạm vi 100 tin nhắn gần nhất
SELECT m.MessageKey, m.ConversationKey, m.SenderKey, m.SenderType, m.ReceiverKey, 
       m.ReceiverType, m.MessageType, m.Content, m.ParentMessageKey, m.CreatedOn, 
       m.Status, m.IsPinned, m.IsSystemMessage, -- Thêm IsSystemMessage
       ma.AttachmentKey, ma.Type AS AttachmentType, 
       CASE 
           WHEN m.SenderType = 'Admin' AND ma.Url IS NOT NULL THEN CONCAT('https://localhost:7078/', ma.Url)
           ELSE ma.Url 
       END AS Url, 
       ma.FileSize, ma.FileName, ma.MimeType,
       pm.Content AS ParentContent,
       pm.Status AS ParentStatus,
       CASE 
           WHEN m.SenderType = 'Member' THEN em.MemberName
           WHEN m.SenderType = 'Admin' THEN su.UserName
       END AS SenderName,
       CASE 
           WHEN m.SenderType = 'Member' THEN em.Avatar
           WHEN m.SenderType = 'Admin' THEN CONCAT('https://localhost:7078/', he.PhotoPath)
       END AS SenderAvatar
FROM Messages m
LEFT JOIN (
    SELECT *,
        ROW_NUMBER() OVER (PARTITION BY MessageKey ORDER BY CreatedOn DESC) AS rn
    FROM MessageAttachments
) ma ON m.MessageKey = ma.MessageKey AND ma.rn = 1
LEFT JOIN Messages pm ON m.ParentMessageKey = pm.MessageKey
LEFT JOIN [EDU_Member] em ON m.SenderType = 'Member' AND m.SenderKey = em.MemberKey
LEFT JOIN [SYS_Users] su ON m.SenderType = 'Admin' AND m.SenderKey = su.UserKey
LEFT JOIN [HRM_Employee] he ON m.SenderType = 'Admin' AND su.EmployeeKey = he.EmployeeKey
WHERE m.ConversationKey = @ConversationKey
AND m.Status IN (0, 1, 2)
AND m.MessageKey IN (
    SELECT ParentMessageKey 
    FROM Messages 
    WHERE ConversationKey = @ConversationKey 
    AND ParentMessageKey IS NOT NULL
    AND CreatedOn <= (SELECT MAX(CreatedOn) FROM Messages WHERE ConversationKey = @ConversationKey AND Status IN (0, 1, 2))
)

-- Áp dụng ORDER BY và OFFSET...FETCH cho toàn bộ kết quả
ORDER BY CreatedOn DESC
OFFSET @Skip ROWS FETCH NEXT 100 ROWS ONLY";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                    command.Parameters.AddWithValue("@Skip", skip);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var messageDict = new Dictionary<string, Dictionary<string, object>>();
                        while (await reader.ReadAsync())
                        {
                            var messageKey = reader["MessageKey"].ToString();
                            if (!messageDict.ContainsKey(messageKey))
                            {
                                var message = new Dictionary<string, object>
                        {
                            { "MessageKey", reader["MessageKey"] },
                            { "ConversationKey", reader["ConversationKey"] },
                            { "SenderKey", reader["SenderKey"] },
                            { "SenderType", reader["SenderType"] },
                            { "ReceiverKey", reader["ReceiverKey"] ?? (object)DBNull.Value },
                            { "ReceiverType", reader["ReceiverType"] ?? (object)DBNull.Value },
                            { "MessageType", reader["MessageType"] },
                            { "Content", reader["Content"] ?? (object)DBNull.Value },
                            { "ParentMessageKey", reader["ParentMessageKey"] ?? (object)DBNull.Value },
                            { "CreatedOn", reader["CreatedOn"] },
                            { "Status", reader["Status"] },
                            { "IsPinned", reader["IsPinned"] },
                            { "IsSystemMessage", reader["IsSystemMessage"] ?? false }, // Thêm IsSystemMessage
                            { "AttachmentKey", reader["AttachmentKey"] ?? (object)DBNull.Value },
                            { "AttachmentType", reader["AttachmentType"] ?? (object)DBNull.Value },
                            { "Url", reader["Url"] ?? (object)DBNull.Value },
                            { "FileSize", reader["FileSize"] ?? (object)DBNull.Value },
                            { "FileName", reader["FileName"] ?? (object)DBNull.Value },
                            { "MimeType", reader["MimeType"] ?? (object)DBNull.Value },
                            { "ParentContent", reader["ParentContent"] ?? (object)DBNull.Value },
                            { "ParentStatus", reader["ParentStatus"] ?? (object)DBNull.Value },
                            { "SenderName", reader["SenderName"] ?? (object)DBNull.Value },
                            { "SenderAvatar", reader["SenderAvatar"] ?? (object)DBNull.Value }
                        };
                                var messageType = reader["MessageType"]?.ToString();
                                if (messageType != "Text")
                                {
                                    message["FileSize"] = reader["FileSize"] ?? (object)DBNull.Value;
                                    message["FileName"] = reader["FileName"] ?? (object)DBNull.Value;
                                }
                                else
                                {
                                    message["FileSize"] = DBNull.Value;
                                    message["FileName"] = DBNull.Value;
                                }
                                messageDict[messageKey] = message;
                            }
                        }
                        messages.AddRange(messageDict.Values.OrderByDescending(m => Convert.ToDateTime(m["CreatedOn"])));
                    }
                }
            }
            return messages;
        }
        public static async Task<List<string>> GetConversationMembersAsync(string conversationKey)
        {
            var memberKeys = new List<string>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT UserKey
                    FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                    WHERE ConversationKey = @ConversationKey
                    AND IsApproved = 1";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ConversationKey", conversationKey);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            if (reader["UserKey"] != DBNull.Value)
                            {
                                memberKeys.Add(reader["UserKey"].ToString());
                            }
                        }
                    }
                }
            }
            return memberKeys;
        }
        public static async Task<bool> UnpinMessageAsync(string messageKey, string memberKey)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
            UPDATE Messages
            SET IsPinned = 0
            WHERE MessageKey = @MessageKey";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@MessageKey", messageKey);
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }
        public static async Task<bool> PinMessageAsync(string messageKey, string memberKey)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
            UPDATE Messages
            SET IsPinned = 1
            WHERE MessageKey = @MessageKey";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@MessageKey", messageKey);
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }
        public static async Task<bool> RecallMessageAsync(string messageKey, string memberKey)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    UPDATE Messages
                    SET Status = 2
                    WHERE MessageKey = @MessageKey AND SenderKey = @MemberKey";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@MessageKey", messageKey);
                    command.Parameters.AddWithValue("@MemberKey", memberKey);
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected > 0)
                    {
                        // Lấy URL của media từ MessageAttachments
                        var mediaQuery = @"
                            SELECT Url 
                            FROM MessageAttachments 
                            WHERE MessageKey = @MessageKey";
                        string filePath = null;
                        using (var mediaCommand = new SqlCommand(mediaQuery, connection))
                        {
                            mediaCommand.Parameters.AddWithValue("@MessageKey", messageKey);
                            using (var reader = await mediaCommand.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync() && reader["Url"] != DBNull.Value)
                                {
                                    filePath = Path.Combine(_mediaRootPath, Path.GetFileName(reader["Url"].ToString()));
                                }
                            }
                        }

                        // Xóa file media nếu tồn tại
                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            try
                            {
                                File.Delete(filePath);
                                Console.WriteLine($"[RecallMessage] Đã xóa file media: {filePath}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[RecallMessage] Lỗi xóa file media: {ex.Message}");
                            }
                        }

                        // Xóa bản ghi trong MessageAttachments
                        var deleteQuery = @"
                            DELETE FROM MessageAttachments
                            WHERE MessageKey = @MessageKey";
                        using (var deleteCommand = new SqlCommand(deleteQuery, connection))
                        {
                            deleteCommand.Parameters.AddWithValue("@MessageKey", messageKey);
                            await deleteCommand.ExecuteNonQueryAsync();
                        }

                        return true;
                    }
                    return false;
                }
            }
        }
        public static async Task<List<string>> GetConversationKeysAsync(string participantKey)
        {
            var conversationKeys = new List<string>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = @"
            SELECT DISTINCT c.ConversationKey
            FROM ConversationParticipants cp
            JOIN Conversations c ON cp.ConversationKey = c.ConversationKey
            WHERE cp.UserKey = @ParticipantKey
            AND cp.IsApproved = 1
            AND c.IsActive = 1";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ParticipantKey", participantKey ?? (object)DBNull.Value);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            conversationKeys.Add(reader["ConversationKey"].ToString());
                        }
                    }
                }
            }
            return conversationKeys;
        }
        public static async Task<int> GetTotalUnreadCountAsync(string memberKey)
        {
            int totalUnreadCount = 0;
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
            SELECT SUM(cp.UnreadCount) as TotalUnread
            FROM ConversationParticipants cp
            JOIN Conversations c ON cp.ConversationKey = c.ConversationKey
            WHERE cp.UserKey = @MemberKey
            AND cp.IsApproved = 1
            AND c.IsActive = 1";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@MemberKey", memberKey ?? (object)DBNull.Value);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            totalUnreadCount = Convert.ToInt32(reader["TotalUnread"] ?? 0);
                        }
                    }
                }
            }
            return totalUnreadCount;
        }
        public static async Task<List<Dictionary<string, object>>> GetGroupMembersAsync(string memberKey)
        {
            var results = new List<Dictionary<string, object>>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var memberQuery = @"
            SELECT m.MemberKey AS UserKey, m.MemberName AS Name, m.Avatar AS Avatar, 'Member' AS UserType
            FROM EDU_Member m
            WHERE m.MemberKey != @MemberKey";

                var userQuery = @"
            SELECT u.UserKey, u.UserName AS Name, e.PhotoPath AS Avatar, 'Admin' AS UserType
            FROM SYS_Users u
            JOIN HRM_Employee e ON u.EmployeeKey = e.EmployeeKey
            WHERE u.UserKey != @MemberKey";

                var queries = new[]
                {
            (query: memberQuery, hasUserKey: true),
            (query: userQuery, hasUserKey: true)
        };
                foreach (var (q, hasUserKey) in queries)
                {
                    using (var command = new SqlCommand(q, connection))
                    {
                        command.Parameters.AddWithValue("@MemberKey", memberKey);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var result = new Dictionary<string, object>
                        {
                            { "Name", reader["Name"] },
                            { "Avatar", reader["Avatar"] ?? "/images/avatar/default-avatar.jpg" },
                            { "UserType", reader["UserType"] }
                        };
                                if (hasUserKey && reader["UserKey"] != DBNull.Value)
                                    result["UserKey"] = reader["UserKey"];
                                else
                                    result["UserKey"] = DBNull.Value;

                                if (result["UserType"].ToString() == "Admin" && !string.IsNullOrEmpty(result["Avatar"].ToString()))
                                    result["Avatar"] = $"https://localhost:7078/{result["Avatar"]}";
                                results.Add(result);
                            }
                        }
                    }
                }
            }
            return results;
        }
        public static async Task<Dictionary<string, object>> CreateGroupAsync(string groupName, IFormFile selectedAvatar, List<UserData> users, string currentMemberKey, string currentMemberName, HttpContext httpContext)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var conversationKey = Guid.NewGuid().ToString();
                        var createdOn = DateTime.Now;

                        // Insert vào Conversations
                        var conversationQuery = @"
       INSERT INTO [TNS_Toeic].[dbo].[Conversations] 
       ([ConversationKey], [ConversationType], [CreatedOn], [LastMessageKey], [LastMessageTime], [ConversationMode], [Name], [GroupAvatar], [CreatorKey], [IsActive])
       VALUES (@ConversationKey, 'Group', @CreatedOn, NULL, NULL, 'Public', @Name, NULL, @CreatorKey, 1)";
                        using (var command = new SqlCommand(conversationQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            command.Parameters.AddWithValue("@CreatedOn", createdOn);
                            command.Parameters.AddWithValue("@Name", groupName);
                            command.Parameters.AddWithValue("@CreatorKey", currentMemberKey);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Insert vào ConversationParticipants
                        var participantQuery = @"
       INSERT INTO [TNS_Toeic].[dbo].[ConversationParticipants] 
       ([ParticipantKey], [ConversationKey], [UserKey], [UserType], [Role], [JoinedOn], [UnreadCount], [LastReadMessageKey], [IsBanned], [IsApproved])
       VALUES (@ParticipantKey, @ConversationKey, @UserKey, @UserType, @Role, @JoinedOn, 0, NULL, 0, 1)";
                        foreach (var user in users)
                        {
                            var participantKey = Guid.NewGuid().ToString();
                            using (var command = new SqlCommand(participantQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@ParticipantKey", participantKey);
                                command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                command.Parameters.AddWithValue("@UserKey", user.userKey);
                                command.Parameters.AddWithValue("@UserType", user.userType);
                                command.Parameters.AddWithValue("@Role", user.userKey == currentMemberKey ? "Admin" : "Member");
                                command.Parameters.AddWithValue("@JoinedOn", createdOn);
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                        var creatorParticipantKey = Guid.NewGuid().ToString();
                        using (var command = new SqlCommand(participantQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ParticipantKey", creatorParticipantKey);
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            command.Parameters.AddWithValue("@UserKey", currentMemberKey);
                            command.Parameters.AddWithValue("@UserType", "Member");
                            command.Parameters.AddWithValue("@Role", "Admin");
                            command.Parameters.AddWithValue("@JoinedOn", createdOn);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Insert vào Messages
                        var messageQuery = @"
       INSERT INTO [TNS_Toeic].[dbo].[Messages] 
       ([MessageKey], [ConversationKey], [SenderKey], [SenderType], [ReceiverKey], [ReceiverType], [MessageType], [Content], [ParentMessageKey], [CreatedOn], [Status], [IsPinned], [IsSystemMessage])
       VALUES (@MessageKey, @ConversationKey, NULL, NULL, NULL, NULL, @MessageType, @Content, NULL, @CreatedOn, @Status, 0, 1)";
                        var messageKeys = new List<string>();
                        var lastMessageKey = Guid.NewGuid().ToString();
                        using (var command = new SqlCommand(messageQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            command.Parameters.AddWithValue("@CreatedOn", createdOn);
                            command.Parameters.AddWithValue("@MessageType", "Text");
                            command.Parameters.AddWithValue("@Content", $"Group {groupName} created by {currentMemberName}");
                            command.Parameters.AddWithValue("@MessageKey", lastMessageKey);
                            command.Parameters.AddWithValue("@Status", 1);
                            await command.ExecuteNonQueryAsync();
                            messageKeys.Add(lastMessageKey);
                        }
                        foreach (var user in users)
                        {
                            var messageKey = Guid.NewGuid().ToString();
                            using (var command = new SqlCommand(messageQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                command.Parameters.AddWithValue("@CreatedOn", createdOn);
                                command.Parameters.AddWithValue("@MessageType", "Text");
                                command.Parameters.AddWithValue("@Content", $"{user.userName} added to group by {currentMemberName}");
                                command.Parameters.AddWithValue("@MessageKey", messageKey);
                                command.Parameters.AddWithValue("@Status", 1);
                                await command.ExecuteNonQueryAsync();
                                messageKeys.Add(messageKey);
                            }
                        }

                        // Cập nhật UnreadCount
                        var updateUnreadCountQuery = @"
       UPDATE [TNS_Toeic].[dbo].[ConversationParticipants]
       SET UnreadCount = @UnreadCount
       WHERE ConversationKey = @ConversationKey";
                        using (var command = new SqlCommand(updateUnreadCountQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            command.Parameters.AddWithValue("@UnreadCount", messageKeys.Count);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Cập nhật LastMessageKey và LastMessageTime
                        var updateLastMessageQuery = @"
       UPDATE [TNS_Toeic].[dbo].[Conversations]
       SET LastMessageKey = @LastMessageKey, LastMessageTime = @LastMessageTime
       WHERE ConversationKey = @ConversationKey";
                        using (var command = new SqlCommand(updateLastMessageQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            command.Parameters.AddWithValue("@LastMessageKey", lastMessageKey);
                            command.Parameters.AddWithValue("@LastMessageTime", createdOn);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Lưu ảnh và tạo URL tuyệt đối
                        string groupAvatar = null;
                        if (selectedAvatar != null)
                        {
                            var fileName = $"{conversationKey}{Path.GetExtension(selectedAvatar.FileName)}";
                            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "avatar", "group", fileName);
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await selectedAvatar.CopyToAsync(stream);
                            }
                            // Tạo URL tuyệt đối sử dụng HttpContext
                            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
                            groupAvatar = $"{baseUrl}/images/avatar/group/{fileName}";
                            var updateAvatarQuery = @"
           UPDATE [TNS_Toeic].[dbo].[Conversations]
           SET GroupAvatar = @GroupAvatar
           WHERE ConversationKey = @ConversationKey";
                            using (var command = new SqlCommand(updateAvatarQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                command.Parameters.AddWithValue("@GroupAvatar", groupAvatar);
                                await command.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", "Group created successfully" },
                    { "conversationKey", conversationKey },
                    { "groupAvatar", groupAvatar }
                };
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", $"Error creating group: {ex.Message}" }
                };
                    }
                }
            }
        }
        public class UserData
        {
            public string userKey { get; set; }
            public string userType { get; set; }
            public string userName { get; set; }
            public string userAvatar { get; set; }
        }
    }
}