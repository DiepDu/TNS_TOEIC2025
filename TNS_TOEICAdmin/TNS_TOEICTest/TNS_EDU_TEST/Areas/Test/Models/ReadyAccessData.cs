using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TNS_EDU_TEST.Areas.Test.Pages;

namespace TNS_EDU_TEST.Areas.Test.Models
{
    public class ReadyAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
        private static readonly Random _random = new Random();

        public static async Task<(Guid? TestKey, Guid? ResultKey, bool IsTestScoreNull)> CheckLatestTest(Guid memberKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string sql = @"
                    SELECT TOP 1 r.TestKey, r.ResultKey, r.TestScore
                    FROM ResultOfUserForTest r
                    INNER JOIN [Test] t ON r.TestKey = t.TestKey
                    WHERE r.MemberKey = @MemberKey
                    ORDER BY t.CreatedOn DESC";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var testKey = reader.GetGuid(0);
                            var resultKey = reader.GetGuid(1);
                            var testScore = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                            return (testKey, resultKey, testScore == null);
                        }
                    }
                }
            }
            return (null, null, false);
        }

        public static async Task InsertTest(Guid testKey, string testName, string description, int totalQuestion,
            int duration, Guid createdBy, string createdName)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string sql = @"
                    INSERT INTO [Test] (TestKey, TestName, Description, TotalQuestion, Duration, CreatedOn, CreatedBy, CreatedName, IsRandom, Status)
                    VALUES (@TestKey, @TestName, @Description, @TotalQuestion, @Duration, @CreatedOn, @CreatedBy, @CreatedName, 0, 1)";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@TestKey", testKey);
                    cmd.Parameters.AddWithValue("@TestName", testName);
                    cmd.Parameters.AddWithValue("@Description", description);
                    cmd.Parameters.AddWithValue("@TotalQuestion", totalQuestion);
                    cmd.Parameters.AddWithValue("@Duration", duration);
                    cmd.Parameters.AddWithValue("@CreatedOn", DateTime.Now);
                    cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                    cmd.Parameters.AddWithValue("@CreatedName", createdName);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task InsertResultOfUserForTest(Guid resultKey, Guid testKey, Guid memberKey,
            string memberName, DateTime startTime, DateTime endTime)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string sql = @"
                    INSERT INTO [ResultOfUserForTest] (ResultKey, TestKey, MemberKey, MemberName, StartTime, EndTime, Status)
                    VALUES (@ResultKey, @TestKey, @MemberKey, @MemberName, @StartTime, @EndTime, 0)";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    cmd.Parameters.AddWithValue("@TestKey", testKey);
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    cmd.Parameters.AddWithValue("@MemberName", memberName);
                    cmd.Parameters.AddWithValue("@StartTime", startTime);
                    cmd.Parameters.AddWithValue("@EndTime", endTime);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public static async Task<(ReadyModel.ToeicConfig, List<ReadyModel.SkillLevelDistribution>)> GetToeicConfiguration()
        {
            ReadyModel.ToeicConfig config = null;
            var distributions = new List<ReadyModel.SkillLevelDistribution>();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string sql = @"
                    SELECT ConfigKey, NumberOfPart1, NumberOfPart2, NumberOfPart3, NumberOfPart4, NumberOfPart5, NumberOfPart6, NumberOfPart7, Duration
                    FROM [TOEICConfiguration]
                    WHERE ConfigKey = '95611C83-2C2E-4192-9A78-991FB5A34CA4';

                    SELECT DistributionKey, Part, SkillLevel1, SkillLevel2, SkillLevel3, SkillLevel4, SkillLevel5
                    FROM [SkillLevelDistribution]";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            config = new ReadyModel.ToeicConfig
                            {
                                ConfigKey = reader.GetGuid(0),
                                NumberOfPart1 = reader.GetInt32(1),
                                NumberOfPart2 = reader.GetInt32(2),
                                NumberOfPart3 = reader.GetInt32(3),
                                NumberOfPart4 = reader.GetInt32(4),
                                NumberOfPart5 = reader.GetInt32(5),
                                NumberOfPart6 = reader.GetInt32(6),
                                NumberOfPart7 = reader.GetInt32(7),
                                Duration = reader.GetInt32(8)
                            };
                        }

                        await reader.NextResultAsync();
                        while (await reader.ReadAsync())
                        {
                            distributions.Add(new ReadyModel.SkillLevelDistribution
                            {
                                DistributionKey = reader.GetGuid(0),
                                Part = reader.GetInt32(1),
                                SkillLevel1 = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                                SkillLevel2 = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                                SkillLevel3 = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                                SkillLevel4 = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                                SkillLevel5 = reader.IsDBNull(6) ? null : reader.GetInt32(6)
                            });
                        }
                    }
                }
            }

            return (config, distributions);
        }

        public static async Task GenerateTestContent(Guid testKey, Guid resultKey, ReadyModel.ToeicConfig config, List<ReadyModel.SkillLevelDistribution> distributions)
        {
            if (config == null || distributions == null || distributions.Count != 7)
                throw new ArgumentException("Invalid TOEIC configuration or distributions.");

            using (var conn = new SqlConnection(_connectionString))
            {
                try
                {
                    await conn.OpenAsync();
                    int currentOrder = 1;
                    int totalQuestionsAdded = 0;

                    for (int part = 1; part <= 7; part++)
                    {
                        var dist = distributions.First(d => d.Part == part);
                        int totalQuestions = part switch
                        {
                            1 => config.NumberOfPart1,
                            2 => config.NumberOfPart2,
                            3 => config.NumberOfPart3,
                            4 => config.NumberOfPart4,
                            5 => config.NumberOfPart5,
                            6 => config.NumberOfPart6,
                            7 => config.NumberOfPart7,
                            _ => 0
                        };

                        string tableName = $"TEC_Part{part}_Question";
                        int questionsBefore = totalQuestionsAdded;
                        if (part == 3 || part == 4 || part == 6 || part == 7)
                        {
                            currentOrder = await GenerateQuestionsWithPassages(conn, testKey, resultKey, part, tableName, dist, totalQuestions, currentOrder);
                        }
                        else
                        {
                            currentOrder = await GenerateSimpleQuestions(conn, testKey, resultKey, part, tableName, dist, totalQuestions, currentOrder);
                        }
                        totalQuestionsAdded += (currentOrder - questionsBefore - 1); // Trừ 1 vì currentOrder đã tăng cho câu tiếp theo
                    }

                    if (totalQuestionsAdded < 200)
                        Console.WriteLine($"Warning: Only {totalQuestionsAdded} questions generated, expected 200.");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error generating test content: {ex.Message}", ex);
                }
            }
        }

        private static async Task<int> GenerateSimpleQuestions(SqlConnection conn, Guid testKey, Guid resultKey, int part, string tableName,
            ReadyModel.SkillLevelDistribution dist, int totalQuestions, int currentOrder)
        {
            int[] skillLevels = { dist.SkillLevel1 ?? 0, dist.SkillLevel2 ?? 0, dist.SkillLevel3 ?? 0, dist.SkillLevel4 ?? 0, dist.SkillLevel5 ?? 0 };
            int questionsAdded = 0;

            try
            {
                for (int level = 1; level <= 5; level++)
                {
                    int questionsForLevel = skillLevels[level - 1];
                    if (questionsForLevel == 0) continue;

                    string sql = $@"
                        SELECT TOP ({questionsForLevel}) QuestionKey
                        FROM {tableName}
                        WHERE Publish = 1 AND RecordStatus != 99 AND SkillLevel = @SkillLevel
                        ORDER BY NEWID()";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@SkillLevel", level);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var questionKey = reader.GetGuid(0);
                                await InsertContentOfTest(conn, testKey, resultKey, questionKey, part, currentOrder++);
                                await UpdateAmountAccess(conn, tableName, questionKey);
                                questionsAdded++;
                            }
                        }
                    }
                }

                if (questionsAdded < totalQuestions)
                    Console.WriteLine($"Warning: Part {part} only has {questionsAdded} questions, expected {totalQuestions}.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating simple questions for Part {part}: {ex.Message}", ex);
            }

            return currentOrder;
        }

        private static async Task<int> GenerateQuestionsWithPassages(SqlConnection conn, Guid testKey, Guid resultKey, int part, string tableName,
            ReadyModel.SkillLevelDistribution dist, int totalQuestions, int currentOrder)
        {
            int[] skillLevels = { dist.SkillLevel1 ?? 0, dist.SkillLevel2 ?? 0, dist.SkillLevel3 ?? 0, dist.SkillLevel4 ?? 0, dist.SkillLevel5 ?? 0 };
            int questionsRemaining = totalQuestions;
            int questionsAdded = 0;
            int questionsPerPassage = part switch { 3 => 3, 4 => 3, 6 => 4, 7 => 4, _ => 3 };

            try
            {
                for (int level = 1; level <= 5 && questionsRemaining > 0; level++)
                {
                    int questionsForLevel = Math.Min(skillLevels[level - 1], questionsRemaining);
                    if (questionsForLevel == 0) continue;

                    string parentSql = $@"
                SELECT QuestionKey
                FROM {tableName}
                WHERE Publish = 1 AND RecordStatus != 99 AND SkillLevel = @SkillLevel AND Parent IS NULL
                ORDER BY NEWID()";

                    var parentKeys = new List<Guid>();
                    using (var cmd = new SqlCommand(parentSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@SkillLevel", level);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync()) parentKeys.Add(reader.GetGuid(0));
                        }
                    }

                    int levelQuestionsAdded = 0;
                    foreach (var parentKey in parentKeys)
                    {
                        if (levelQuestionsAdded >= questionsForLevel) break;

                        string subSql = $@"
                    SELECT TOP ({questionsPerPassage}) QuestionKey
                    FROM {tableName}
                    WHERE Publish = 1 AND RecordStatus != 99 AND Parent = @Parent
                    ORDER BY NEWID()";

                        var subQuestionKeys = new List<Guid>();
                        using (var cmd = new SqlCommand(subSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@Parent", parentKey);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync()) subQuestionKeys.Add(reader.GetGuid(0));
                            }
                        }

                        if (subQuestionKeys.Count > 0)
                        {
                            await InsertContentOfTest(conn, testKey, resultKey, parentKey, part, null);
                            await UpdateAmountAccess(conn, tableName, parentKey);

                            int subQuestionsToAdd = Math.Min(subQuestionKeys.Count, questionsForLevel - levelQuestionsAdded);
                            for (int i = 0; i < subQuestionsToAdd; i++)
                            {
                                var subKey = subQuestionKeys[i];
                                await InsertContentOfTest(conn, testKey, resultKey, subKey, part, currentOrder++);
                                await UpdateAmountAccess(conn, tableName, subKey);
                                levelQuestionsAdded++;
                                questionsAdded++;
                                questionsRemaining--;
                            }
                        }
                    }
                }

                if (questionsAdded < totalQuestions)
                {
                    int additionalQuestionsNeeded = totalQuestions - questionsAdded;
                    string fallbackSql = $@"
                SELECT TOP ({additionalQuestionsNeeded}) q.QuestionKey, q.Parent
                FROM {tableName} q
                WHERE q.Publish = 1 AND q.RecordStatus != 99 AND q.Parent IS NOT NULL
                AND q.QuestionKey NOT IN (
                    SELECT QuestionKey FROM [ContentOfTest] WHERE TestKey = @TestKey
                )
                ORDER BY NEWID()";

                    using (var cmd = new SqlCommand(fallbackSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@TestKey", testKey);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync() && questionsRemaining > 0)
                            {
                                var subKey = reader.GetGuid(0);
                                var parentKey = reader.GetGuid(1);
                                string checkParentSql = $"SELECT COUNT(*) FROM [ContentOfTest] WHERE TestKey = @TestKey AND QuestionKey = @ParentKey";
                                using (var checkCmd = new SqlCommand(checkParentSql, conn))
                                {
                                    checkCmd.Parameters.AddWithValue("@TestKey", testKey);
                                    checkCmd.Parameters.AddWithValue("@ParentKey", parentKey);
                                    int parentExists = (int)await checkCmd.ExecuteScalarAsync();
                                    if (parentExists == 0)
                                    {
                                        await InsertContentOfTest(conn, testKey, resultKey, parentKey, part, null);
                                        await UpdateAmountAccess(conn, tableName, parentKey);
                                    }
                                }
                                await InsertContentOfTest(conn, testKey, resultKey, subKey, part, currentOrder++);
                                await UpdateAmountAccess(conn, tableName, subKey);
                                questionsAdded++;
                                questionsRemaining--;
                            }
                        }
                    }
                }

                if (questionsAdded != totalQuestions)
                    Console.WriteLine($"Warning: Part {part} only has {questionsAdded} questions, expected {totalQuestions}.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating questions with passages for Part {part}: {ex.Message}", ex);
            }

            return currentOrder;
        } 
        private static async Task InsertContentOfTest(SqlConnection conn, Guid testKey, Guid resultKey, Guid questionKey, int part, int? order)
        {
            try
            {
                string sql = @"
            INSERT INTO [ContentOfTest] (ContentKey, TestKey, ResultKey, QuestionKey, Part, [Order])
            VALUES (@ContentKey, @TestKey, @ResultKey, @QuestionKey, @Part, @Order)";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ContentKey", Guid.NewGuid());
                    cmd.Parameters.AddWithValue("@TestKey", testKey);
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    cmd.Parameters.AddWithValue("@QuestionKey", questionKey);
                    cmd.Parameters.AddWithValue("@Part", part);
                    cmd.Parameters.AddWithValue("@Order", (object)order ?? DBNull.Value);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error inserting into ContentOfTest: {ex.Message}", ex);
            }
        }

        private static async Task UpdateAmountAccess(SqlConnection conn, string tableName, Guid questionKey)
        {
            try
            {
                string sql = $@"
                    UPDATE {tableName}
                    SET AmountAccess = AmountAccess + 1
                    WHERE QuestionKey = @QuestionKey";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@QuestionKey", questionKey);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating AmountAccess in {tableName}: {ex.Message}", ex);
            }
        }
    }
}