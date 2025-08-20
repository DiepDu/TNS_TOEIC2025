using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TNS_TOEICTest.Models.Chat;

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

        public static async Task<Dictionary<string, object>> GetGroupDetailsAsync(string conversationKey)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Lấy thông tin nhóm từ bảng [Conversations]
            var groupQuery = "SELECT [GroupAvatar], [Name] FROM [TNS_Toeic].[dbo].[Conversations] WHERE [ConversationKey] = @ConversationKey";
            using var groupCmd = new SqlCommand(groupQuery, connection);
            groupCmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
            using var groupReader = await groupCmd.ExecuteReaderAsync();

            if (!groupReader.Read())
            {
                return null;
            }

            var groupAvatar = groupReader["GroupAvatar"]?.ToString() ?? "/images/avatar/default-avatar.jpg";
            var groupName = groupReader["Name"]?.ToString() ?? "Unnamed Group";
            groupReader.Close();

            // Lấy danh sách thành viên từ [ConversationParticipants]
            var participantsQuery = @"
                SELECT [UserKey], [UserType], [Role]
                FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                WHERE [ConversationKey] = @ConversationKey AND [IsBanned] = 0 AND [IsApproved] = 1";
            using var participantsCmd = new SqlCommand(participantsQuery, connection);
            participantsCmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
            using var participantsReader = await participantsCmd.ExecuteReaderAsync();

            var members = new List<object>();
            while (await participantsReader.ReadAsync())
            {
                var userKey = participantsReader["UserKey"].ToString();
                var userType = participantsReader["UserType"].ToString();
                var role = participantsReader["Role"].ToString();

                object userInfo = null;
                if (userType == "Admin")
                {
                    var adminQuery = @"
                        SELECT u.[UserName], e.[PhotoPath] AS Avatar
                        FROM [TNS_Toeic].[dbo].[SYS_Users] u
                        LEFT JOIN [TNS_Toeic].[dbo].[HRM_Employee] e ON u.[EmployeeKey] = e.[EmployeeKey]
                        WHERE u.[UserKey] = @UserKey";
                    using var adminCmd = new SqlCommand(adminQuery, connection);
                    adminCmd.Parameters.AddWithValue("@UserKey", userKey);
                    using var adminReader = await adminCmd.ExecuteReaderAsync();
                    if (await adminReader.ReadAsync())
                    {
                        var userName = adminReader["UserName"].ToString();
                        var avatar = adminReader["Avatar"]?.ToString();
                        if (!string.IsNullOrEmpty(avatar))
                        {
                            avatar = $"https://localhost:7078/{avatar}";
                        }
                        else
                        {
                            avatar = "/images/avatar/default-avatar.jpg";
                        }
                        userInfo = new { UserKey = userKey, UserName = userName, Avatar = avatar, Role = role };
                    }
                    adminReader.Close();
                }
                else if (userType == "Member")
                {
                    var memberQuery = @"
                        SELECT [MemberName], [Avatar]
                        FROM [TNS_Toeic].[dbo].[EDU_Member]
                        WHERE [MemberKey] = @UserKey";
                    using var memberCmd = new SqlCommand(memberQuery, connection);
                    memberCmd.Parameters.AddWithValue("@UserKey", userKey);
                    using var memberReader = await memberCmd.ExecuteReaderAsync();
                    if (await memberReader.ReadAsync())
                    {
                        var memberName = memberReader["MemberName"].ToString();
                        var avatar = memberReader["Avatar"]?.ToString() ?? "/images/avatar/default-avatar.jpg";
                        userInfo = new { UserKey = userKey, UserName = memberName, Avatar = avatar, Role = role };
                    }
                    memberReader.Close();
                }

                if (userInfo != null)
                {
                    members.Add(userInfo);
                }
            }
            participantsReader.Close();

            return new Dictionary<string, object>
            {
                { "GroupAvatar", groupAvatar },
                { "GroupName", groupName },
                { "Members", members }
            };
        }
        public static async Task<Dictionary<string, object>> UpdateGroupAvatarAsync(string conversationKey, string memberKey, IFormFile file, HttpContext httpContext)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Kiểm tra role trong bảng ConversationParticipants
                        var roleQuery = @"
                    SELECT [Role]
                    FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                    WHERE [ConversationKey] = @ConversationKey AND [UserKey] = @MemberKey AND [IsApproved] = 1";
                        using (var command = new SqlCommand(roleQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            command.Parameters.AddWithValue("@MemberKey", memberKey);
                            var role = (await command.ExecuteScalarAsync())?.ToString();

                            if (role != "Admin")
                            {
                                return new Dictionary<string, object>
                        {
                            { "success", false },
                            { "message", "ACCESS DENIED" }
                        };
                            }
                        }

                        // Lấy URL ảnh cũ từ bảng Conversations
                        var oldAvatarQuery = @"
                    SELECT [GroupAvatar]
                    FROM [TNS_Toeic].[dbo].[Conversations]
                    WHERE [ConversationKey] = @ConversationKey";
                        string oldAvatarUrl = null;
                        using (var command = new SqlCommand(oldAvatarQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            oldAvatarUrl = (await command.ExecuteScalarAsync())?.ToString();
                        }

                        // Xóa ảnh cũ dựa trên URL
                        if (!string.IsNullOrEmpty(oldAvatarUrl) && oldAvatarUrl != "/images/avatar/default-avatar.jpg")
                        {
                            var fileName = Path.GetFileName(oldAvatarUrl); // Giữ nguyên biến fileName cho ảnh cũ
                            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "avatar", "group", fileName);
                            if (System.IO.File.Exists(filePath))
                            {
                                System.IO.File.Delete(filePath);
                                Console.WriteLine($"[UpdateGroupAvatar] Deleted old avatar: {filePath}");
                            }
                        }

                        // Lưu ảnh mới
                        var newFileName = $"{conversationKey}{Path.GetExtension(file.FileName)}"; // Thay đổi tên biến thành newFileName
                        var newFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "avatar", "group", newFileName);
                        Directory.CreateDirectory(Path.GetDirectoryName(newFilePath) ?? string.Empty);
                        using (var stream = new FileStream(newFilePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Tạo URL tuyệt đối cho ảnh mới
                        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
                        var newAvatarUrl = $"{baseUrl}/images/avatar/group/{newFileName}";

                        // Cập nhật URL mới vào bảng Conversations
                        var updateQuery = @"
                    UPDATE [TNS_Toeic].[dbo].[Conversations]
                    SET [GroupAvatar] = @GroupAvatar
                    WHERE [ConversationKey] = @ConversationKey";
                        using (var command = new SqlCommand(updateQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            command.Parameters.AddWithValue("@GroupAvatar", newAvatarUrl);
                            await command.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        return new Dictionary<string, object>
                {
                    { "success", true },
                    { "newAvatarUrl", newAvatarUrl }
                };
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", $"Error updating avatar: {ex.Message}" }
                };
                    }
                }
            }
        }

        public static async Task<Dictionary<string, object>> UpdateGroupNameAsync(string conversationKey, string memberKey, string memberName, string newGroupName, HttpContext httpContext)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Kiểm tra role
                        var roleQuery = @"
                    SELECT [Role]
                    FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                    WHERE [ConversationKey] = @ConversationKey AND [UserKey] = @MemberKey AND [IsApproved] = 1";
                        using (var command = new SqlCommand(roleQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            command.Parameters.AddWithValue("@MemberKey", memberKey);
                            var role = (await command.ExecuteScalarAsync())?.ToString();

                            if (role != "Admin")
                            {
                                return new Dictionary<string, object>
                        {
                            { "success", false },
                            { "message", "ACCESS DENIED" }
                        };
                            }
                        }

                        // Cập nhật tên nhóm
                        var updateNameQuery = @"
                    UPDATE [TNS_Toeic].[dbo].[Conversations]
                    SET [Name] = @NewGroupName
                    WHERE [ConversationKey] = @ConversationKey";
                        using (var command = new SqlCommand(updateNameQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            command.Parameters.AddWithValue("@NewGroupName", newGroupName);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Tạo tin nhắn hệ thống
                        var messageKey = Guid.NewGuid().ToString();
                        var createdOn = DateTime.Now;
                        var systemContent = $"{memberName} has updated the group name";

                        var systemMessageQuery = @"
                    INSERT INTO [TNS_Toeic].[dbo].[Messages] (
                        [MessageKey], [ConversationKey], [SenderKey], [SenderType], [ReceiverKey], [ReceiverType], 
                        [MessageType], [Content], [ParentMessageKey], [CreatedOn], [Status], [IsPinned], [IsSystemMessage]
                    )
                    VALUES (
                        @MessageKey, @ConversationKey, @SenderKey, @SenderType, @ReceiverKey, @ReceiverType, 
                        @MessageType, @Content, @ParentMessageKey, @CreatedOn, @Status, @IsPinned, @IsSystemMessage
                    )";
                        using (var command = new SqlCommand(systemMessageQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@MessageKey", messageKey);
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            command.Parameters.AddWithValue("@SenderKey", DBNull.Value);
                            command.Parameters.AddWithValue("@SenderType", DBNull.Value);
                            command.Parameters.AddWithValue("@ReceiverKey", DBNull.Value);
                            command.Parameters.AddWithValue("@ReceiverType", DBNull.Value);
                            command.Parameters.AddWithValue("@MessageType", "Text");
                            command.Parameters.AddWithValue("@Content", systemContent);
                            command.Parameters.AddWithValue("@ParentMessageKey", DBNull.Value);
                            command.Parameters.AddWithValue("@CreatedOn", createdOn);
                            command.Parameters.AddWithValue("@Status", 1);
                            command.Parameters.AddWithValue("@IsPinned", 0);
                            command.Parameters.AddWithValue("@IsSystemMessage", 1);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Cập nhật LastMessageKey & LastMessageTime
                        var updateLastMessageQuery = @"
                    UPDATE [TNS_Toeic].[dbo].[Conversations]
                    SET [LastMessageKey] = @MessageKey, [LastMessageTime] = @CreatedOn
                    WHERE [ConversationKey] = @ConversationKey";
                        using (var command = new SqlCommand(updateLastMessageQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            command.Parameters.AddWithValue("@MessageKey", messageKey);
                            command.Parameters.AddWithValue("@CreatedOn", createdOn);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Cập nhật UnreadCount
                        var updateUnreadCountQuery = @"
                    UPDATE [TNS_Toeic].[dbo].[ConversationParticipants]
                    SET [UnreadCount] = [UnreadCount] + 1
                    WHERE [ConversationKey] = @ConversationKey";
                        using (var command = new SqlCommand(updateUnreadCountQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            await command.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", "Group name updated successfully" },
                    { "newGroupName", newGroupName },
                    { "messageKey", messageKey },
                    { "systemContent", systemContent },
                    { "createdOn", createdOn }
                };
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", $"Error updating group name: {ex.Message}" }
                };
                    }
                }
            }
        }
        public static async Task<Dictionary<string, object>> RemoveMemberAsync(
            string conversationKey, string memberKey, string memberName,
            string targetUserKey, string targetUserName, HttpContext httpContext)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Kiểm tra targetUserKey tồn tại trong nhóm
                        var checkUserQuery = @"
                    SELECT COUNT(*) 
                    FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                    WHERE [ConversationKey] = @ConversationKey AND [UserKey] = @TargetUserKey AND [IsApproved] = 1";
                        using (var cmd = new SqlCommand(checkUserQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            cmd.Parameters.AddWithValue("@TargetUserKey", targetUserKey);
                            var userCount = (int)await cmd.ExecuteScalarAsync();
                            if (userCount == 0)
                            {
                                return new Dictionary<string, object> {
                            { "success", false }, { "message", "Target user not found in group" }
                        };
                            }
                        }

                        // Kiểm tra quyền Admin
                        var roleQuery = @"
                    SELECT [Role]
                    FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                    WHERE [ConversationKey] = @ConversationKey AND [UserKey] = @MemberKey AND [IsApproved] = 1";
                        using (var cmd = new SqlCommand(roleQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                            var role = (await cmd.ExecuteScalarAsync())?.ToString();
                            if (role != "Admin")
                            {
                                return new Dictionary<string, object> {
                            { "success", false }, { "message", "ACCESS DENIED" }
                        };
                            }
                        }

                        // Xóa thành viên
                        var deleteQuery = @"
                    DELETE FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                    WHERE [ConversationKey] = @ConversationKey AND [UserKey] = @TargetUserKey AND [IsApproved] = 1";
                        using (var cmd = new SqlCommand(deleteQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            cmd.Parameters.AddWithValue("@TargetUserKey", targetUserKey);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Kiểm tra số lượng thành viên còn lại
                        var memberCountQuery = @"
                    SELECT COUNT(*) 
                    FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                    WHERE [ConversationKey] = @ConversationKey AND [IsApproved] = 1";
                        using (var cmd = new SqlCommand(memberCountQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            var memberCount = (int)await cmd.ExecuteScalarAsync();
                            if (memberCount <= 1)
                            {
                                var deactivateGroupQuery = @"
                            UPDATE [TNS_Toeic].[dbo].[Conversations]
                            SET [IsActive] = 0
                            WHERE [ConversationKey] = @ConversationKey";
                                using (var cmdDeactivate = new SqlCommand(deactivateGroupQuery, connection, transaction))
                                {
                                    cmdDeactivate.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                    await cmdDeactivate.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        // Tạo system message
                        var messageKey = Guid.NewGuid().ToString();
                        var createdOn = DateTime.Now;
                        var systemContent = $"{memberName} removed {targetUserName} from the group";

                        var insertMsgQuery = @"
                    INSERT INTO [TNS_Toeic].[dbo].[Messages] (
                        [MessageKey],[ConversationKey],[MessageType],[Content],
                        [CreatedOn],[Status],[IsPinned],[IsSystemMessage]
                    ) VALUES (
                        @MessageKey,@ConversationKey,'Text',@Content,
                        @CreatedOn,1,0,1
                    )";
                        using (var cmd = new SqlCommand(insertMsgQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@MessageKey", messageKey);
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            cmd.Parameters.AddWithValue("@Content", systemContent);
                            cmd.Parameters.AddWithValue("@CreatedOn", createdOn);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Update Conversations
                        var updateConv = @"
                    UPDATE [TNS_Toeic].[dbo].[Conversations]
                    SET [LastMessageKey] = @MessageKey, [LastMessageTime] = @CreatedOn
                    WHERE [ConversationKey] = @ConversationKey";
                        using (var cmd = new SqlCommand(updateConv, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            cmd.Parameters.AddWithValue("@MessageKey", messageKey);
                            cmd.Parameters.AddWithValue("@CreatedOn", createdOn);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Update UnreadCount
                        var updateUnread = @"
                    UPDATE [TNS_Toeic].[dbo].[ConversationParticipants]
                    SET [UnreadCount] = [UnreadCount] + 1
                    WHERE [ConversationKey] = @ConversationKey";
                        using (var cmd = new SqlCommand(updateUnread, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        return new Dictionary<string, object> {
                    { "success", true },
                    { "message", "Member removed successfully" },
                    { "messageKey", messageKey },
                    { "systemContent", systemContent },
                    { "createdOn", createdOn }
                };
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return new Dictionary<string, object> {
                    { "success", false },
                    { "message", $"Error: {ex.Message}" }
                };
                    }
                }
            }
        }

        public static async Task<List<Dictionary<string, object>>> GetAddableMembersAsync(string conversationKey, List<string> excludeKeys)
        {
            var results = new List<Dictionary<string, object>>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Lấy danh sách tất cả Member (loại trừ những người đã ở trong nhóm)
                var memberQuery = @"
            SELECT m.MemberKey AS UserKey, m.MemberName AS Name, 
                   ISNULL(m.Avatar, '/images/avatar/default-avatar.jpg') AS Avatar, 
                   'Member' AS UserType
            FROM EDU_Member m
            WHERE m.MemberKey NOT IN (@ExcludeKeys)";

                // Lấy danh sách tất cả Admin/User (loại trừ những người đã ở trong nhóm)
                var userQuery = @"
            SELECT u.UserKey, u.UserName AS Name, 
                   ISNULL(e.PhotoPath, '/images/avatar/default-avatar.jpg') AS Avatar, 
                   'Admin' AS UserType
            FROM SYS_Users u
            JOIN HRM_Employee e ON u.EmployeeKey = e.EmployeeKey
            WHERE u.UserKey NOT IN (@ExcludeKeys)";

                // Tạo table parameter cho ExcludeKeys
                var excludeTable = new DataTable();
                excludeTable.Columns.Add("Key", typeof(string));
                foreach (var key in excludeKeys.Distinct())
                    excludeTable.Rows.Add(key);

                var queries = new[]
                {
            (query: memberQuery, type: "Member"),
            (query: userQuery, type: "Admin")
        };

                foreach (var (q, _) in queries)
                {
                    using (var command = new SqlCommand(q.Replace("@ExcludeKeys",
                        string.Join(",", excludeKeys.Select(k => $"'{k}'"))), connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var avatar = reader["Avatar"]?.ToString();
                                if (reader["UserType"].ToString() == "Admin" && !string.IsNullOrEmpty(avatar) && !avatar.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                    avatar = $"https://localhost:7078/{avatar}";

                                results.Add(new Dictionary<string, object>
                        {
                            { "UserKey", reader["UserKey"] },
                            { "Name", reader["Name"] },
                            { "Avatar", avatar ?? "/images/avatar/default-avatar.jpg" },
                            { "UserType", reader["UserType"] }
                        });
                            }
                        }
                    }
                }
            }
            return results;
        }
        public static async Task<Dictionary<string, object>> AddMembersAsync(
    string conversationKey,
    string adminKey,
    string adminName,
    List<NewMemberInfo> newMembers,
    HttpContext httpContext)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Kiểm tra quyền Admin
                        var roleQuery = @"
                    SELECT [Role]
                    FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                    WHERE [ConversationKey] = @ConversationKey AND [UserKey] = @MemberKey AND [IsApproved] = 1";
                        using (var cmd = new SqlCommand(roleQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            cmd.Parameters.AddWithValue("@MemberKey", adminKey);
                            var role = (await cmd.ExecuteScalarAsync())?.ToString();
                            if (role != "Admin")
                            {
                                return new Dictionary<string, object> {
                            { "success", false },
                            { "message", "ACCESS DENIED" }
                        };
                            }
                        }

                        var messageKeys = new List<string>();
                        var messagesList = new List<object>();
                        DateTime lastCreatedOn = DateTime.Now;
                        string lastMessageKey = null;

                        foreach (var nm in newMembers)
                        {
                            // Insert vào ConversationParticipants
                            var participantKey = Guid.NewGuid().ToString();
                            var insertParticipantQuery = @"
                        INSERT INTO [TNS_Toeic].[dbo].[ConversationParticipants]
                        ([ParticipantKey],[ConversationKey],[UserKey],[UserType],[Role],[JoinedOn],[UnreadCount],
                         [LastReadMessageKey],[IsBanned],[IsApproved])
                        VALUES
                        (@ParticipantKey,@ConversationKey,@UserKey,@UserType,'Member',@JoinedOn,0,NULL,0,1)";
                            using (var cmd = new SqlCommand(insertParticipantQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ParticipantKey", participantKey);
                                cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                cmd.Parameters.AddWithValue("@UserKey", nm.UserKey);
                                cmd.Parameters.AddWithValue("@UserType", nm.UserType ?? "Member");
                                cmd.Parameters.AddWithValue("@JoinedOn", DateTime.Now);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // Insert System Message
                            var messageKey = Guid.NewGuid().ToString();
                            var createdOn = DateTime.Now;
                            var content = $"{nm.UserName} added to group by {adminName}";

                            var insertMsgQuery = @"
                        INSERT INTO [TNS_Toeic].[dbo].[Messages]
                        ([MessageKey],[ConversationKey],[SenderKey],[SenderType],[ReceiverKey],[ReceiverType],
                         [MessageType],[Content],[ParentMessageKey],[CreatedOn],[Status],[IsPinned],[IsSystemMessage])
                        VALUES
                        (@MessageKey,@ConversationKey,NULL,NULL,NULL,NULL,'Text',@Content,NULL,@CreatedOn,1,0,1)";
                            using (var cmd = new SqlCommand(insertMsgQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@MessageKey", messageKey);
                                cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                cmd.Parameters.AddWithValue("@Content", content);
                                cmd.Parameters.AddWithValue("@CreatedOn", createdOn);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            messageKeys.Add(messageKey);
                            lastMessageKey = messageKey;
                            lastCreatedOn = createdOn;

                            // Thêm vào danh sách để trả về cho SignalR
                            messagesList.Add(new
                            {
                                MessageKey = messageKey,
                                ConversationKey = conversationKey,
                                SenderKey = (string)null,
                                SenderName = (string)null,
                                SenderAvatar = (string)null,
                                MessageType = "Text",
                                Content = content,
                                ParentMessageKey = (string)null,
                                CreatedOn = createdOn,
                                Status = 1,
                                IsPinned = false,
                                IsSystemMessage = true,
                                Url = (string)null
                            });
                        }

                        // Update UnreadCount
                        var updateUnreadCountQuery = @"
                    UPDATE [TNS_Toeic].[dbo].[ConversationParticipants]
                    SET UnreadCount = UnreadCount + @AddCount
                    WHERE ConversationKey = @ConversationKey";
                        using (var cmd = new SqlCommand(updateUnreadCountQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            cmd.Parameters.AddWithValue("@AddCount", messageKeys.Count);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Update LastMessageKey & LastMessageTime
                        if (!string.IsNullOrEmpty(lastMessageKey))
                        {
                            var updateLastMessageQuery = @"
                        UPDATE [TNS_Toeic].[dbo].[Conversations]
                        SET LastMessageKey = @LastMessageKey, LastMessageTime = @LastMessageTime
                        WHERE ConversationKey = @ConversationKey";
                            using (var cmd = new SqlCommand(updateLastMessageQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                cmd.Parameters.AddWithValue("@LastMessageKey", lastMessageKey);
                                cmd.Parameters.AddWithValue("@LastMessageTime", lastCreatedOn);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        return new Dictionary<string, object> {
                    { "success", true },
                    { "messages", messagesList }
                };
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return new Dictionary<string, object> {
                    { "success", false },
                    { "message", $"Error: {ex.Message}" }
                };
                    }
                }
            }
        }

        public static async Task<Dictionary<string, object>> LeaveGroupAsync(
            string conversationKey,
            string memberKey, // MemberKey của người rời nhóm
            string memberName, // MemberName của người rời nhóm
            HttpContext httpContext)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Bước 1: Kiểm tra thành viên có trong nhóm không
                        var checkUserQuery = @"
                SELECT [Role], [UserKey], [UserType]
                FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                WHERE [ConversationKey] = @ConversationKey AND [UserKey] = @MemberKey AND [IsApproved] = 1";
                        string memberRole = null;
                        string memberUserType = null;
                        using (var cmd = new SqlCommand(checkUserQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    memberRole = reader["Role"].ToString();
                                    memberUserType = reader["UserType"].ToString();
                                }
                                else
                                {
                                    return new Dictionary<string, object>
                            {
                                { "success", false },
                                { "message", "User not found in group" }
                            };
                                }
                            }
                        }

                        // Bước 2: Nếu thành viên là Admin, chuyển quyền Admin cho thành viên khác
                        string newAdminKey = null;
                        string newAdminName = null;
                        string newAdminType = null;
                        if (memberRole == "Admin")
                        {
                            var newAdminQuery = @"
                    SELECT TOP 1 [UserKey], [UserType]
                    FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                    WHERE [ConversationKey] = @ConversationKey AND [UserKey] != @MemberKey AND [IsApproved] = 1";
                            using (var cmd = new SqlCommand(newAdminQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    if (await reader.ReadAsync())
                                    {
                                        newAdminKey = reader["UserKey"].ToString();
                                        newAdminType = reader["UserType"].ToString();
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(newAdminKey))
                            {
                                // Lấy tên của Admin mới
                                if (newAdminType == "Admin")
                                {
                                    var adminQuery = @"
                            SELECT [UserName]
                            FROM [TNS_Toeic].[dbo].[SYS_Users]
                            WHERE [UserKey] = @UserKey";
                                    using (var cmd = new SqlCommand(adminQuery, connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@UserKey", newAdminKey);
                                        newAdminName = (await cmd.ExecuteScalarAsync())?.ToString();
                                    }
                                }
                                else // UserType == "Member"
                                {
                                    var memberQueryName = @"
                            SELECT [MemberName]
                            FROM [TNS_Toeic].[dbo].[EDU_Member]
                            WHERE [MemberKey] = @MemberKey";
                                    using (var cmd = new SqlCommand(memberQueryName, connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@MemberKey", newAdminKey);
                                        newAdminName = (await cmd.ExecuteScalarAsync())?.ToString();
                                    }
                                }

                                // Cập nhật vai trò Admin mới
                                var updateAdminQuery = @"
                        UPDATE [TNS_Toeic].[dbo].[ConversationParticipants]
                        SET [Role] = 'Admin'
                        WHERE [ConversationKey] = @ConversationKey AND [UserKey] = @NewAdminKey";
                                using (var cmd = new SqlCommand(updateAdminQuery, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                    cmd.Parameters.AddWithValue("@NewAdminKey", newAdminKey);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        // Bước 3: Xóa thành viên khỏi nhóm
                        var deleteQuery = @"
                DELETE FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                WHERE [ConversationKey] = @ConversationKey AND [UserKey] = @MemberKey AND [IsApproved] = 1";
                        using (var cmd = new SqlCommand(deleteQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Bước 4: Kiểm tra số lượng thành viên còn lại
                        var memberCountQuery = @"
                SELECT COUNT(*)
                FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                WHERE [ConversationKey] = @ConversationKey AND [IsApproved] = 1";
                        int memberCount;
                        using (var cmd = new SqlCommand(memberCountQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            memberCount = (int)await cmd.ExecuteScalarAsync();
                        }

                        // Bước 5: Nếu không còn thành viên, xóa toàn bộ dữ liệu liên quan
                        if (memberCount == 0)
                        {
                            // Lấy GroupAvatar từ bảng Conversations trước khi xóa
                            string groupAvatarUrl = null;
                            var groupAvatarQuery = @"
                    SELECT [GroupAvatar]
                    FROM [TNS_Toeic].[dbo].[Conversations]
                    WHERE [ConversationKey] = @ConversationKey";
                            using (var cmd = new SqlCommand(groupAvatarQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                groupAvatarUrl = (await cmd.ExecuteScalarAsync())?.ToString();
                            }

                            // Lấy danh sách file từ MessageAttachments để xóa
                            var attachmentsQuery = @"
                    SELECT [Url]
                    FROM [TNS_Toeic].[dbo].[MessageAttachments]
                    WHERE [MessageKey] IN (
                        SELECT [MessageKey]
                        FROM [TNS_Toeic].[dbo].[Messages]
                        WHERE [ConversationKey] = @ConversationKey
                    )";
                            var fileUrls = new List<string>();
                            using (var cmd = new SqlCommand(attachmentsQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    while (await reader.ReadAsync())
                                    {
                                        fileUrls.Add(reader["Url"].ToString());
                                    }
                                }
                            }

                            // Xóa dữ liệu từ MessageAttachments
                            var deleteAttachmentsQuery = @"
                    DELETE FROM [TNS_Toeic].[dbo].[MessageAttachments]
                    WHERE [MessageKey] IN (
                        SELECT [MessageKey]
                        FROM [TNS_Toeic].[dbo].[Messages]
                        WHERE [ConversationKey] = @ConversationKey
                    )";
                            using (var cmd = new SqlCommand(deleteAttachmentsQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // Xóa dữ liệu từ Messages
                            var deleteMessagesQuery = @"
                    DELETE FROM [TNS_Toeic].[dbo].[Messages]
                    WHERE [ConversationKey] = @ConversationKey";
                            using (var cmd = new SqlCommand(deleteMessagesQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // Xóa dữ liệu từ ConversationParticipants
                            var deleteParticipantsQuery = @"
                    DELETE FROM [TNS_Toeic].[dbo].[ConversationParticipants]
                    WHERE [ConversationKey] = @ConversationKey";
                            using (var cmd = new SqlCommand(deleteParticipantsQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // Xóa dữ liệu từ Conversations
                            var deleteConversationQuery = @"
                    DELETE FROM [TNS_Toeic].[dbo].[Conversations]
                    WHERE [ConversationKey] = @ConversationKey";
                            using (var cmd = new SqlCommand(deleteConversationQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // Xóa file vật lý trong wwwroot/images/messages
                            foreach (var url in fileUrls)
                            {
                                try
                                {
                                    // Chuyển URL thành đường dẫn vật lý
                                    var fileName = Path.GetFileName(url);
                                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "messages", fileName);
                                    if (File.Exists(filePath))
                                    {
                                        File.Delete(filePath);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Log lỗi xóa file nhưng không làm gián đoạn transaction
                                    Console.WriteLine($"Error deleting file {url}: {ex.Message}");
                                }
                            }

                            // Xóa file avatar của nhóm trong wwwroot/images/avatar/group
                            if (!string.IsNullOrEmpty(groupAvatarUrl))
                            {
                                try
                                {
                                    // Chuyển URL thành đường dẫn vật lý
                                    var fileName = Path.GetFileName(groupAvatarUrl);
                                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "avatar", "group", fileName);
                                    if (File.Exists(filePath))
                                    {
                                        File.Delete(filePath);
                                        Console.WriteLine($"Successfully deleted group avatar: {filePath}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Group avatar file not found: {filePath}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Log lỗi xóa file nhưng không làm gián đoạn transaction
                                    Console.WriteLine($"Error deleting group avatar {groupAvatarUrl}: {ex.Message}");
                                }
                            }

                            // Commit transaction
                            transaction.Commit();

                            // Trả về kết quả khi nhóm bị xóa
                            return new Dictionary<string, object>
                    {
                        { "success", true },
                        { "message", "Group deleted as no members remain" },
                        { "messages", new List<object>() }
                    };
                        }

                        // Bước 6: Tạo tin nhắn hệ thống
                        var messageKeys = new List<string>();
                        var messagesList = new List<object>();
                        DateTime lastCreatedOn = DateTime.Now;
                        string lastMessageKey = null;

                        // Tin nhắn 1: [memberName] has left the group
                        var messageKey1 = Guid.NewGuid().ToString();
                        var systemContent1 = $"{memberName} has left the group.";
                        var insertMsgQuery1 = @"
                INSERT INTO [TNS_Toeic].[dbo].[Messages]
                ([MessageKey],[ConversationKey],[MessageType],[Content],[CreatedOn],[Status],[IsPinned],[IsSystemMessage])
                VALUES
                (@MessageKey,@ConversationKey,'Text',@Content,@CreatedOn,1,0,1)";
                        using (var cmd = new SqlCommand(insertMsgQuery1, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@MessageKey", messageKey1);
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            cmd.Parameters.AddWithValue("@Content", systemContent1);
                            cmd.Parameters.AddWithValue("@CreatedOn", lastCreatedOn);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        messageKeys.Add(messageKey1);
                        lastMessageKey = messageKey1;
                        messagesList.Add(new
                        {
                            MessageKey = messageKey1,
                            ConversationKey = conversationKey,
                            SenderKey = (string)null,
                            SenderName = (string)null,
                            SenderAvatar = (string)null,
                            MessageType = "Text",
                            Content = systemContent1,
                            ParentMessageKey = (string)null,
                            CreatedOn = lastCreatedOn,
                            Status = 1,
                            IsPinned = false,
                            IsSystemMessage = true,
                            Url = (string)null
                        });

                        // Tin nhắn 2 (nếu thành viên rời là Admin): [newAdminName] has become the group admin
                        if (memberRole == "Admin" && !string.IsNullOrEmpty(newAdminName))
                        {
                            var messageKey2 = Guid.NewGuid().ToString();
                            var systemContent2 = $"{newAdminName} has become the group admin.";
                            var insertMsgQuery2 = @"
                    INSERT INTO [TNS_Toeic].[dbo].[Messages]
                    ([MessageKey],[ConversationKey],[MessageType],[Content],[CreatedOn],[Status],[IsPinned],[IsSystemMessage])
                    VALUES
                    (@MessageKey,@ConversationKey,'Text',@Content,@CreatedOn,1,0,1)";
                            using (var cmd = new SqlCommand(insertMsgQuery2, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@MessageKey", messageKey2);
                                cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                cmd.Parameters.AddWithValue("@Content", systemContent2);
                                cmd.Parameters.AddWithValue("@CreatedOn", lastCreatedOn);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            messageKeys.Add(messageKey2);
                            lastMessageKey = messageKey2;
                            messagesList.Add(new
                            {
                                MessageKey = messageKey2,
                                ConversationKey = conversationKey,
                                SenderKey = (string)null,
                                SenderName = (string)null,
                                SenderAvatar = (string)null,
                                MessageType = "Text",
                                Content = systemContent2,
                                ParentMessageKey = (string)null,
                                CreatedOn = lastCreatedOn,
                                Status = 1,
                                IsPinned = false,
                                IsSystemMessage = true,
                                Url = (string)null
                            });
                        }

                        // Bước 7: Cập nhật UnreadCount
                        var updateUnreadQuery = @"
                UPDATE [TNS_Toeic].[dbo].[ConversationParticipants]
                SET [UnreadCount] = [UnreadCount] + @AddCount
                WHERE [ConversationKey] = @ConversationKey";
                        using (var cmd = new SqlCommand(updateUnreadQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            cmd.Parameters.AddWithValue("@AddCount", messageKeys.Count);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Bước 8: Cập nhật LastMessageKey & LastMessageTime
                        if (!string.IsNullOrEmpty(lastMessageKey))
                        {
                            var updateConvQuery = @"
                    UPDATE [TNS_Toeic].[dbo].[Conversations]
                    SET [LastMessageKey] = @MessageKey, [LastMessageTime] = @CreatedOn
                    WHERE [ConversationKey] = @ConversationKey";
                            using (var cmd = new SqlCommand(updateConvQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                cmd.Parameters.AddWithValue("@MessageKey", lastMessageKey);
                                cmd.Parameters.AddWithValue("@CreatedOn", lastCreatedOn);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        // Bước 9: Commit transaction
                        transaction.Commit();

                        // Bước 10: Trả về kết quả cho SignalR
                        return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", "Member left group successfully" },
                    { "messages", messagesList }
                };
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", $"Error: {ex.Message}" }
                };
                    }
                }
            }
        }
        /// <summary>
        /// Đảo ngược trạng thái chặn (block/unblock) của một người dùng trong cuộc hội thoại.
        /// </summary>
        /// <param name="conversationKey">Khóa của cuộc hội thoại.</param>
        /// <param name="targetUserKey">Khóa của người dùng bị chặn/bỏ chặn.</param>
        /// <param name="currentUserKey">Khóa của người dùng thực hiện hành động.</param>
        /// <returns>Một tuple chứa trạng thái thành công và trạng thái bị chặn mới.</returns>
        public static async Task<(bool success, bool isBanned)> ToggleBlockUserAsync(string conversationKey, string targetUserKey, string currentUserKey)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Câu lệnh SQL để kiểm tra và cập nhật
                var query = @"
            -- Kiểm tra xem người dùng hiện tại có thuộc cuộc hội thoại này không để đảm bảo an toàn
            IF NOT EXISTS (SELECT 1 FROM ConversationParticipants WHERE ConversationKey = @ConversationKey AND UserKey = @CurrentUserKey)
            BEGIN
                -- Nếu không, trả về lỗi (select một giá trị không hợp lệ để nhận biết)
                SELECT -1 AS NewStatus;
            END
            ELSE
            BEGIN
                -- Cập nhật trạng thái IsBanned (nếu là 1 thì thành 0, và ngược lại)
                UPDATE ConversationParticipants
                SET IsBanned = CASE WHEN IsBanned = 1 THEN 0 ELSE 1 END
                WHERE ConversationKey = @ConversationKey AND UserKey = @TargetUserKey;

                -- Trả về trạng thái IsBanned mới sau khi cập nhật
                SELECT IsBanned FROM ConversationParticipants WHERE ConversationKey = @ConversationKey AND UserKey = @TargetUserKey;
            END";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                    command.Parameters.AddWithValue("@TargetUserKey", targetUserKey);
                    command.Parameters.AddWithValue("@CurrentUserKey", currentUserKey);

                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        var newStatus = Convert.ToInt32(result);
                        if (newStatus == -1)
                        {
                            // Người dùng không có quyền
                            return (success: false, isBanned: false);
                        }
                        return (success: true, isBanned: Convert.ToBoolean(newStatus));
                    }
                }
            }
            // Trường hợp có lỗi xảy ra
            return (success: false, isBanned: false);
        }
        public static async Task<bool> CheckUserInConversationAsync(string userKey, string conversationKey)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = @"
            SELECT COUNT(*)
            FROM [ConversationParticipants]
            WHERE [UserKey] = @UserKey AND [ConversationKey] = @ConversationKey AND [IsApproved] = 1";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserKey", userKey);
                    command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                    var count = (int)await command.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }

        public static async Task<bool> GetParticipantBanStatusAsync(string conversationKey, string targetUserKey)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = @"
            SELECT [IsBanned]
            FROM [ConversationParticipants]
            WHERE [ConversationKey] = @ConversationKey AND [UserKey] = @TargetUserKey";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                    command.Parameters.AddWithValue("@TargetUserKey", targetUserKey);
                    var result = await command.ExecuteScalarAsync();
                    return result != null && result != DBNull.Value ? Convert.ToBoolean(result) : false;
                }
            }
        }
        // Đặt hàm này vào bên trong class ChatAccessData
        public static async Task<bool> MarkConversationAsReadAsync(string conversationKey, string memberKey)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Bước 1: Cập nhật tất cả tin nhắn của đối phương thành "đã đọc" (Status = 1)
                        var updateMessagesQuery = @"
                    UPDATE Messages
                    SET Status = 1
                    WHERE ConversationKey = @ConversationKey 
                      AND SenderKey != @MemberKey 
                      AND Status = 0;"; // Chỉ cập nhật tin nhắn có trạng thái "đã gửi" (0)

                        using (var command = new SqlCommand(updateMessagesQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            command.Parameters.AddWithValue("@MemberKey", memberKey);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Bước 2: Reset UnreadCount về 0 cho người dùng hiện tại trong cuộc hội thoại này
                        var updateParticipantQuery = @"
                    UPDATE ConversationParticipants
                    SET UnreadCount = 0
                    WHERE ConversationKey = @ConversationKey 
                      AND UserKey = @MemberKey;";

                        using (var command = new SqlCommand(updateParticipantQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            command.Parameters.AddWithValue("@MemberKey", memberKey);
                            await command.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        // Ghi lại lỗi để debug
                        Console.WriteLine($"[MarkConversationAsReadAsync] Error: {ex.Message}");
                        return false;
                    }
                }
            }
        }
        // TÌM VÀ THAY THẾ TOÀN BỘ HÀM NÀY TRONG FILE ChatAccessData.cs

        public static async Task<(bool success, string errorMessage)> MarkSpecificMessagesAsReadAsync(List<string> messageKeys, string conversationKey, string readerUserKey)
        {
            if (messageKeys == null || !messageKeys.Any() || string.IsNullOrEmpty(conversationKey) || string.IsNullOrEmpty(readerUserKey))
            {
                return (true, null); // Không có gì để xử lý
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // --- BƯỚC 1: Cập nhật trạng thái các tin nhắn cụ thể ---
                        var parameters = new List<string>();
                        var updateMessagesCommand = new SqlCommand();
                        for (int i = 0; i < messageKeys.Count; i++)
                        {
                            var paramName = $"@p{i}";
                            parameters.Add(paramName);
                            updateMessagesCommand.Parameters.AddWithValue(paramName, messageKeys[i]);
                        }

                        var updateMessagesQuery = $@"
                    UPDATE Messages
                    SET Status = 1
                    WHERE MessageKey IN ({string.Join(", ", parameters)}) AND Status = 0;";

                        updateMessagesCommand.CommandText = updateMessagesQuery;
                        updateMessagesCommand.Connection = connection;
                        updateMessagesCommand.Transaction = transaction;
                        int messagesUpdatedCount = await updateMessagesCommand.ExecuteNonQueryAsync();

                        // --- BƯỚC 2 (QUAN TRỌNG): Cập nhật UnreadCount ---
                        // Chỉ cập nhật UnreadCount nếu có tin nhắn thực sự được chuyển trạng thái
                        if (messagesUpdatedCount > 0)
                        {
                            var updateParticipantQuery = @"
                        UPDATE ConversationParticipants
                        SET UnreadCount = UnreadCount - @MessagesUpdatedCount
                        WHERE ConversationKey = @ConversationKey 
                          AND UserKey = @ReaderUserKey
                          AND UnreadCount > 0;"; // Đảm bảo không bị số âm

                            using (var updateParticipantCommand = new SqlCommand(updateParticipantQuery, connection, transaction))
                            {
                                updateParticipantCommand.Parameters.AddWithValue("@MessagesUpdatedCount", messagesUpdatedCount);
                                updateParticipantCommand.Parameters.AddWithValue("@ConversationKey", conversationKey);
                                updateParticipantCommand.Parameters.AddWithValue("@ReaderUserKey", readerUserKey);
                                await updateParticipantCommand.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        return (true, null);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"[MarkSpecificMessagesAsReadAsync] Error: {ex.Message}");
                        return (false, ex.Message);
                    }
                }
            }
        }
        public static async Task<Dictionary<string, object>> SendMessageAsync(
            string conversationKey,
            string senderKey,
            string senderType,
            string senderName,
            string senderAvatar,
            string receiverKey,
            string receiverType,
            string content,
            string parentMessageKey,
            string parentMessageContent,
            IFormFile file,
            HttpContext httpContext)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var messageKey = Guid.NewGuid().ToString();
                        var createdOn = DateTime.Now;
                        var messageType = "Text";
                        string fileUrl = null;

                        // --- Bước 1: Xử lý file đính kèm nếu có ---
                        if (file != null && file.Length > 0)
                        {
                            // Xác định MessageType từ MimeType của file
                            if (file.ContentType.StartsWith("image/")) messageType = "Image";
                            else if (file.ContentType.StartsWith("audio/")) messageType = "Audio";
                            else if (file.ContentType.StartsWith("video/")) messageType = "Video";
                            else messageType = "File"; // Loại file khác

                            var fileName = $"{messageKey}{Path.GetExtension(file.FileName)}";
                            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "messages", fileName);

                            // Tạo thư mục nếu chưa tồn tại
                            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }
                            fileUrl = $"/images/messages/{fileName}";

                            // Insert vào MessageAttachments
                            var attachmentQuery = @"
                        INSERT INTO MessageAttachments 
                        (AttachmentKey, MessageKey, Type, Url, FileSize, CreatedOn, FileName, MimeType)
                        VALUES (@AttachmentKey, @MessageKey, @Type, @Url, @FileSize, @CreatedOn, @FileName, @MimeType)";
                            using (var cmd = new SqlCommand(attachmentQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@AttachmentKey", Guid.NewGuid().ToString());
                                cmd.Parameters.AddWithValue("@MessageKey", messageKey);
                                cmd.Parameters.AddWithValue("@Type", messageType);
                                cmd.Parameters.AddWithValue("@Url", fileUrl);
                                cmd.Parameters.AddWithValue("@FileSize", file.Length);
                                cmd.Parameters.AddWithValue("@CreatedOn", createdOn);
                                cmd.Parameters.AddWithValue("@FileName", file.FileName);
                                cmd.Parameters.AddWithValue("@MimeType", file.ContentType);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        // --- Bước 2: Insert vào Messages ---
                        var messageQuery = @"
                    INSERT INTO Messages 
                    (MessageKey, ConversationKey, SenderKey, SenderType, ReceiverKey, ReceiverType, MessageType, Content, ParentMessageKey, CreatedOn, Status, IsPinned, IsSystemMessage)
                    VALUES (@MessageKey, @ConversationKey, @SenderKey, @SenderType, @ReceiverKey, @ReceiverType, @MessageType, @Content, @ParentMessageKey, @CreatedOn, @Status, 0, 0)";
                        using (var cmd = new SqlCommand(messageQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@MessageKey", messageKey);
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            cmd.Parameters.AddWithValue("@SenderKey", senderKey);
                            cmd.Parameters.AddWithValue("@SenderType", senderType);
                            cmd.Parameters.AddWithValue("@ReceiverKey", (object)receiverKey ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ReceiverType", (object)receiverType ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@MessageType", messageType);
                            cmd.Parameters.AddWithValue("@Content", (object)content ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ParentMessageKey", (object)parentMessageKey ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@CreatedOn", createdOn);
                            cmd.Parameters.AddWithValue("@Status", 0); // 0 = Đã gửi, 1 = Đã đọc
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // --- Bước 3: Cập nhật Conversations ---
                        var updateConvQuery = @"
                    UPDATE Conversations 
                    SET LastMessageKey = @LastMessageKey, LastMessageTime = @LastMessageTime 
                    WHERE ConversationKey = @ConversationKey";
                        using (var cmd = new SqlCommand(updateConvQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@LastMessageKey", messageKey);
                            cmd.Parameters.AddWithValue("@LastMessageTime", createdOn);
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // --- Bước 4: Cập nhật UnreadCount ---
                        var updateUnreadQuery = @"
                    UPDATE ConversationParticipants 
                    SET UnreadCount = UnreadCount + 1 
                    WHERE ConversationKey = @ConversationKey AND UserKey != @SenderKey";
                        using (var cmd = new SqlCommand(updateUnreadQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ConversationKey", conversationKey);
                            cmd.Parameters.AddWithValue("@SenderKey", senderKey);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();

                        // --- Bước 5: Trả về đối tượng message hoàn chỉnh để gửi qua SignalR ---
                        var messageObject = new Dictionary<string, object>
                {
                    { "MessageKey", messageKey },
                    { "ConversationKey", conversationKey },
                    { "SenderKey", senderKey },
                    { "SenderName", senderName },
                    { "SenderAvatar", senderAvatar },
                    { "MessageType", messageType },
                    { "Content", content },
                    { "ParentMessageKey", parentMessageKey },
                    { "ParentContent", parentMessageContent }, // Trả về content tin nhắn cha
                    { "CreatedOn", createdOn },
                    { "Status", 0 }, // Trạng thái ban đầu là "Đã gửi"
                    { "IsPinned", false },
                    { "IsSystemMessage", false },
                    { "Url", fileUrl }
                };
                        return messageObject;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"[SendMessageAsync] Error: {ex.Message}");
                        return null;
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