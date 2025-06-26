using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TNS_TOEICAdmin.Models
{
    public class ChatAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

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
                             JOIN EDU_Member m ON cp2.UserKey = m.MemberKey 
                             WHERE cp2.ConversationKey = c.ConversationKey AND cp2.UserKey != @MemberKey AND cp2.UserType = 'Member'),
                            (SELECT TOP 1 cp2.UserType 
                             FROM ConversationParticipants cp2 
                             JOIN SYS_Users u ON cp2.UserKey = u.UserKey 
                             WHERE cp2.ConversationKey = c.ConversationKey AND cp2.UserKey != @MemberKey AND cp2.UserType = 'Admin'),
                            NULL
                        )
                END AS PartnerUserType,
                c.LastMessageKey,
                c.LastMessageTime,
                m.Content,
                m.SenderKey,
                c.Name
            FROM ConversationParticipants cp
            JOIN Conversations c ON cp.ConversationKey = c.ConversationKey
            LEFT JOIN Messages m ON c.LastMessageKey = m.MessageKey
            WHERE cp.UserKey = @MemberKey
            AND cp.IsBanned = 0
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
                                ? (reader["SenderKey"].ToString() == currentMemberKey ? "Bạn: " : reader["DisplayName"] + ": ") + reader["Content"].ToString()
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
                        { "Name", reader["Name"] ?? (object)DBNull.Value }
                    };
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

                // Tìm kiếm trong Conversations (Group)
                var groupQuery = @"
            SELECT c.ConversationKey, c.Name AS Name, c.GroupAvatar AS Avatar, 'Group' AS UserType
            FROM Conversations c
            JOIN ConversationParticipants cp ON c.ConversationKey = cp.ConversationKey
            WHERE c.ConversationType = 'Group'
            AND cp.UserKey = @MemberKey
            AND c.Name LIKE '%' + @Query + '%'";

                // Tìm kiếm trong EDU_Member (loại bỏ bản thân)
                var memberQuery = @"
            SELECT m.MemberKey AS UserKey, m.MemberName AS Name, m.Avatar AS Avatar, 'Member' AS UserType
            FROM EDU_Member m
            WHERE m.MemberName LIKE '%' + @Query + '%'
            AND m.MemberKey != @MemberKey";

                // Tìm kiếm trong SYS_Users và HRM_Employee (loại bỏ bản thân)
                var userQuery = @"
            SELECT u.UserKey, u.UserName AS Name, e.PhotoPath AS Avatar, 'Admin' AS UserType
            FROM SYS_Users u
            JOIN HRM_Employee e ON u.EmployeeKey = e.EmployeeKey
            WHERE u.UserName LIKE '%' + @Query + '%'
            AND u.UserKey != @MemberKey";

                var queries = new[]
                {
            (query: groupQuery, hasConversationKey: true, hasUserKey: false),
            (query: memberQuery, hasConversationKey: false, hasUserKey: true),
            (query: userQuery, hasConversationKey: false, hasUserKey: true)
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
                    SELECT m.MessageKey, m.ConversationKey, m.SenderKey, m.SenderType, m.ReceiverKey, 
                        m.ReceiverType, m.MessageType, m.Content, m.ParentMessageKey, m.CreatedOn, 
                        m.Status, m.IsPinned,
                        ma.AttachmentKey, ma.Type AS AttachmentType, ma.Url, ma.FileSize, ma.FileName, ma.MimeType
                    FROM Messages m
                    LEFT JOIN MessageAttachments ma ON m.MessageKey = ma.MessageKey
                    WHERE m.ConversationKey = @ConversationKey
                    ORDER BY m.CreatedOn DESC
                    OFFSET @Skip ROWS FETCH NEXT 100 ROWS ONLY";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ConversationKey", conversationKey);
                    command.Parameters.AddWithValue("@Skip", skip);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
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
                                { "AttachmentKey", reader["AttachmentKey"] ?? (object)DBNull.Value },
                                { "AttachmentType", reader["AttachmentType"] ?? (object)DBNull.Value },
                                { "Url", reader["Url"] ?? (object)DBNull.Value },
                                { "FileSize", reader["FileSize"] ?? (object)DBNull.Value },
                                { "FileName", reader["FileName"] ?? (object)DBNull.Value },
                                { "MimeType", reader["MimeType"] ?? (object)DBNull.Value }
                            };
                            messages.Add(message);
                        }
                    }
                }
            }
            return messages;
        }
    }
}