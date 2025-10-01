using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TNS_EDU_TEST.Areas.Test.Models;
using TNS_EDU_TEST.Areas.Test.Pages;

namespace TNS_EDU_STUDY.Areas.Study.Models
{
    public class PartConfig
    {
        public int TotalQuestion { get; set; }
        public int Duration { get; set; }
    }

    public static class IndexAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        /// <summary>
        /// Checks for the latest unfinished study session for a specific user and part.
        /// </summary>
        public static async Task<(Guid? TestKey, Guid? ResultKey)> CheckForUnfinishedStudySession(Guid memberKey, int part)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string testNamePattern = $"TOEIC STUDY Part {part} - {memberKey}";

                string sql = @"
                    SELECT TOP 1 t.TestKey, r.ResultKey
                    FROM [dbo].[Test] t
                    JOIN [dbo].[ResultOfUserForTest] r ON t.TestKey = r.TestKey
                    WHERE r.MemberKey = @MemberKey
                      AND t.TestName = @TestName
                      AND r.TestScore IS NULL
                      AND r.EndTime IS NULL
                    ORDER BY t.CreatedOn DESC";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    cmd.Parameters.AddWithValue("@TestName", testNamePattern);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return (reader.GetGuid(0), reader.GetGuid(1));
                        }
                    }
                }
            }
            return (null, null);
        }

        /// <summary>
        /// Inserts a new study result record with a NULL EndTime.
        /// </summary>
        public static async Task InsertStudyResult(Guid resultKey, Guid testKey, Guid memberKey, string memberName, DateTime startTime)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = @"
                    INSERT INTO [ResultOfUserForTest] (ResultKey, TestKey, MemberKey, MemberName, StartTime, EndTime, Status, Time)
                    VALUES (@ResultKey, @TestKey, @MemberKey, @MemberName, @StartTime, NULL, 0, 0)";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    cmd.Parameters.AddWithValue("@TestKey", testKey);
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    cmd.Parameters.AddWithValue("@MemberName", memberName);
                    cmd.Parameters.AddWithValue("@StartTime", startTime);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task<(PartConfig, ReadyModel.SkillLevelDistribution)> GetPartConfiguration(int part)
        {
            PartConfig config = null;
            ReadyModel.SkillLevelDistribution distribution = null;
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string configSql = $@"
                    SELECT NumberOfPart{part}, Duration 
                    FROM [TOEICConfiguration]
                    WHERE 
                        (CASE WHEN NumberOfPart1 IS NOT NULL THEN 1 ELSE 0 END +
                         CASE WHEN NumberOfPart2 IS NOT NULL THEN 1 ELSE 0 END +
                         CASE WHEN NumberOfPart3 IS NOT NULL THEN 1 ELSE 0 END +
                         CASE WHEN NumberOfPart4 IS NOT NULL THEN 1 ELSE 0 END +
                         CASE WHEN NumberOfPart5 IS NOT NULL THEN 1 ELSE 0 END +
                         CASE WHEN NumberOfPart6 IS NOT NULL THEN 1 ELSE 0 END +
                         CASE WHEN NumberOfPart7 IS NOT NULL THEN 1 ELSE 0 END) = 1
                        AND NumberOfPart{part} IS NOT NULL";
                using (var configCmd = new SqlCommand(configSql, conn))
                {
                    using (var reader = await configCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            config = new PartConfig { TotalQuestion = reader.GetInt32(0), Duration = reader.GetInt32(1) };
                        }
                    }
                }
                if (config == null) return (null, null);

                string distSql = "SELECT DistributionKey, Part, SkillLevel1, SkillLevel2, SkillLevel3, SkillLevel4, SkillLevel5 FROM [SkillLevelDistribution] WHERE Part = @Part";
                using (var distCmd = new SqlCommand(distSql, conn))
                {
                    distCmd.Parameters.AddWithValue("@Part", part);
                    using (var reader = await distCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            distribution = new ReadyModel.SkillLevelDistribution
                            {
                                DistributionKey = reader.GetGuid(0),
                                Part = reader.GetInt32(1),
                                SkillLevel1 = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                SkillLevel2 = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                SkillLevel3 = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                SkillLevel4 = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                SkillLevel5 = reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
                            };
                        }
                    }
                }
            }
            return (config, distribution);
        }

        public static Task InsertTest(Guid testKey, string testName, string description, int totalQuestion, int duration, Guid createdBy, string createdName)
        {
            return ReadyAccessData.InsertTest(testKey, testName, description, totalQuestion, duration, createdBy, createdName);
        }

        /// <summary>
        /// Hàm chính điều phối việc tạo nội dung bài thi cho một Part cụ thể.
        /// </summary>
        public static async Task GenerateTestContentForPart(Guid testKey, Guid resultKey, int part, PartConfig config, ReadyModel.SkillLevelDistribution distribution)
        {
            var allQuestions = new List<ReadyAccessData.QuestionInfo>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string tableName = $"TEC_Part{part}_Question";

                // Phân loại Part để gọi đúng hàm xử lý
                if (part == 3 || part == 4 || part == 6 || part == 7)
                {
                    // Sử dụng logic ưu tiên sự toàn vẹn của đoạn văn
                    await GenerateQuestionsWithPassagesForPart(conn, part, tableName, distribution, config.TotalQuestion, allQuestions);
                }
                else
                {
                    // Sử dụng logic phân bổ chính xác theo SkillLevel
                    await GenerateSimpleQuestionsForPart(conn, part, tableName, distribution, config.TotalQuestion, allQuestions);
                }

                if (allQuestions.Any())
                {
                    // Sắp xếp lại danh sách câu hỏi theo thứ tự trước khi lưu
                    var orderedQuestions = allQuestions.OrderBy(q => q.Order).ToList();
                    await ReadyAccessData.BulkInsertAndUpdateTestDataFromString(testKey, resultKey, orderedQuestions);
                }
            }
        }

        /// <summary>
        /// Tạo câu hỏi cho các Part đơn giản (1, 2, 5), tuân thủ chặt chẽ phân bổ SkillLevel.
        /// </summary>
        private static async Task GenerateSimpleQuestionsForPart(SqlConnection conn, int part, string tableName, ReadyModel.SkillLevelDistribution dist, int totalQuestions, List<ReadyAccessData.QuestionInfo> collected)
        {
            int currentOrder = 1;
            // Mảng chứa số lượng câu hỏi cần lấy cho mỗi Skill Level
            int[] skillCounts = { dist.SkillLevel1 ?? 0, dist.SkillLevel2 ?? 0, dist.SkillLevel3 ?? 0, dist.SkillLevel4 ?? 0, dist.SkillLevel5 ?? 0 };

            for (int level = 1; level <= 5; level++)
            {
                int questionsForLevel = skillCounts[level - 1];
                if (questionsForLevel == 0) continue; // Bỏ qua nếu không có câu hỏi nào cho level này (ví dụ: Part 1 SkillLevel 5)

                string sql = $@"
            SELECT TOP ({questionsForLevel}) QuestionKey
            FROM {tableName}
            WHERE Publish = 1 AND RecordStatus != 99 AND SkillLevel = @SkillLevel
            ORDER BY NEWID()"; // Lấy ngẫu nhiên

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@SkillLevel", level);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            collected.Add(new ReadyAccessData.QuestionInfo { QuestionKey = reader.GetGuid(0), Part = part, Order = currentOrder++ });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tạo câu hỏi cho các Part có đoạn văn (3, 4, 6, 7), ưu tiên sự toàn vẹn format.
        /// </summary>
        private static async Task GenerateQuestionsWithPassagesForPart(SqlConnection conn, int part, string tableName, ReadyModel.SkillLevelDistribution dist, int totalQuestions, List<ReadyAccessData.QuestionInfo> collected)
        {
            // 1. Xác định format chuẩn cho từng Part
            int questionsPerPassage;
            bool isPart7 = (part == 7);
            switch (part)
            {
                case 3:
                case 4:
                    questionsPerPassage = 3;
                    break;
                case 6:
                    questionsPerPassage = 4;
                    break;
                case 7:
                    questionsPerPassage = 4; // Lấy các đoạn có ít nhất 4 câu làm chuẩn
                    break;
                default:
                    return;
            }

            // Nếu totalQuestions không phải là bội số của format, sẽ có cảnh báo
            if (totalQuestions % questionsPerPassage != 0)
            {
                Console.WriteLine($"Warning: Total questions for Part {part} ({totalQuestions}) is not a multiple of its format ({questionsPerPassage}).");
            }
            int numberOfPassages = totalQuestions / questionsPerPassage;

            // 2. Tìm tất cả các đoạn văn (parent) "đủ chuẩn"
            // "Đủ chuẩn" là có ít nhất số câu hỏi con theo yêu cầu format.
            string validParentsSql = $@"
        SELECT Parent
        FROM {tableName}
        WHERE Parent IS NOT NULL AND Publish = 1 AND RecordStatus != 99
        GROUP BY Parent
        HAVING COUNT(QuestionKey) >= @QuestionsPerPassage";

            var validParentKeys = new List<Guid>();
            using (var cmd = new SqlCommand(validParentsSql, conn))
            {
                cmd.Parameters.AddWithValue("@QuestionsPerPassage", questionsPerPassage);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        validParentKeys.Add(reader.GetGuid(0));
                    }
                }
            }

            // 3. Lấy ngẫu nhiên số lượng đoạn văn cần thiết từ danh sách "đủ chuẩn"
            var random = new Random();
            var selectedParentKeys = validParentKeys.OrderBy(k => random.Next()).Take(numberOfPassages).ToList();

            if (selectedParentKeys.Count < numberOfPassages)
            {
                Console.WriteLine($"Warning: Not enough valid passages for Part {part}. Found {selectedParentKeys.Count}, needed {numberOfPassages}.");
            }

            // 4. Lấy các câu hỏi cha và con tương ứng
            int currentOrder = 1;
            foreach (var parentKey in selectedParentKeys)
            {
                // Thêm câu hỏi cha vào danh sách
                collected.Add(new ReadyAccessData.QuestionInfo { QuestionKey = parentKey, Part = part, Order = currentOrder });

                // Part 7 có thể có 5 câu, nên ta sẽ lấy TOP 5
                int limitChildren = isPart7 ? 5 : questionsPerPassage;

                // Lấy các câu hỏi con tương ứng, đảm bảo đúng số lượng theo format
                string childrenSql = $@"
            SELECT TOP ({limitChildren}) QuestionKey
            FROM {tableName}
            WHERE Parent = @ParentKey AND Publish = 1 AND RecordStatus != 99
            ORDER BY Ranking"; // Sắp xếp theo thứ tự đã định sẵn

                using (var cmd = new SqlCommand(childrenSql, conn))
                {
                    cmd.Parameters.AddWithValue("@ParentKey", parentKey);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            collected.Add(new ReadyAccessData.QuestionInfo { QuestionKey = reader.GetGuid(0), Part = part, Order = currentOrder++ });
                        }
                    }
                }
            }
        }


    }
}