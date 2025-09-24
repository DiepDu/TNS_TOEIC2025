using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            var allQuestions = new List<QuestionInfo>();
            using (var conn = new SqlConnection(_connectionString))
            {
                try
                {
                    await conn.OpenAsync();
                    int currentOrder = 1;

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
                        if (part == 3 || part == 4 || part == 6 || part == 7)
                        {
                            currentOrder = await GenerateQuestionsWithPassages(conn, testKey, resultKey, part, tableName, dist, totalQuestions, currentOrder, allQuestions);
                        }
                        else
                        {
                            currentOrder = await GenerateSimpleQuestions(conn, testKey, resultKey, part, tableName, dist, totalQuestions, currentOrder, allQuestions);
                        }
                    }

                    if (allQuestions.Any())
                    {
                        await BulkInsertAndUpdateTestDataFromString(testKey, resultKey, allQuestions);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error generating test content: {ex.Message}", ex);
                }
            }
        }


        // Đổi định nghĩa hàm:
        private static async Task<int> GenerateQuestionsWithPassages(
      SqlConnection conn, Guid testKey, Guid resultKey, int part, string tableName,
      ReadyModel.SkillLevelDistribution dist, int totalQuestions, int currentOrder,
      List<QuestionInfo> collected)
        {
            int[] skillLevels = { dist.SkillLevel1 ?? 0, dist.SkillLevel2 ?? 0, dist.SkillLevel3 ?? 0, dist.SkillLevel4 ?? 0, dist.SkillLevel5 ?? 0 };
            int questionsRemaining = totalQuestions;
            int questionsAdded = 0;
            int questionsPerPassage = part switch { 3 => 3, 4 => 3, 6 => 4, 7 => 4, _ => 3 };

            // Bước 1: Lấy tất cả các câu hỏi con từ tất cả các SkillLevel
            var allSubQuestions = new List<(Guid SubQuestionKey, Guid ParentKey, int SkillLevel)>();
            for (int level = 1; level <= 5; level++)
            {
                if (skillLevels[level - 1] == 0) continue;

                string subSql = $@"
            SELECT QuestionKey, Parent
            FROM {tableName}
            WHERE Publish = 1 AND RecordStatus != 99 AND SkillLevel = @SkillLevel AND Parent IS NOT NULL";

                using (var cmd = new SqlCommand(subSql, conn))
                {
                    cmd.Parameters.AddWithValue("@SkillLevel", level);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            allSubQuestions.Add((reader.GetGuid(0), reader.GetGuid(1), level));
                        }
                    }
                }
            }

            // Bước 2: Nhóm các câu hỏi con theo ParentKey
            var groupedSubQuestions = allSubQuestions
                .GroupBy(q => q.ParentKey)
                .Select(g => (ParentKey: g.Key, SubQuestionKeys: g.ToList()))
                .Where(g => g.SubQuestionKeys.Count >= questionsPerPassage)
                .ToList();

            // Bước 3: Phân bổ câu hỏi theo SkillLevel
            var passageGroups = new List<(Guid ParentKey, List<(Guid SubQuestionKey, Guid ParentKey)> SubQuestionKeys)>();
            int[] remainingQuestionsPerLevel = new int[5];
            for (int level = 0; level < 5; level++)
                remainingQuestionsPerLevel[level] = skillLevels[level];

            foreach (var group in groupedSubQuestions)
            {
                if (questionsRemaining <= 0) break;

                int selectedLevel = -1;
                for (int level = 1; level <= 5; level++)
                {
                    if (remainingQuestionsPerLevel[level - 1] > 0 && group.SubQuestionKeys.Any(q => q.SkillLevel == level))
                    {
                        selectedLevel = level;
                        break;
                    }
                }
                if (selectedLevel == -1) continue;

                int subQuestionsToAdd = part == 3 || part == 4 ? questionsPerPassage : group.SubQuestionKeys.Count;
                if (subQuestionsToAdd > questionsRemaining) subQuestionsToAdd = questionsRemaining;
                if (subQuestionsToAdd > remainingQuestionsPerLevel[selectedLevel - 1]) subQuestionsToAdd = remainingQuestionsPerLevel[selectedLevel - 1];

                passageGroups.Add((group.ParentKey, group.SubQuestionKeys.Take(subQuestionsToAdd).Select(q => (q.SubQuestionKey, q.ParentKey)).ToList()));
                questionsAdded += subQuestionsToAdd;
                questionsRemaining -= subQuestionsToAdd;
                remainingQuestionsPerLevel[selectedLevel - 1] -= subQuestionsToAdd;
            }

            // Bước 4: Gán Order liên tiếp
            var allQuestionsWithOrder = new List<(Guid QuestionKey, bool IsParent, int Order)>();
            int tempOrder = currentOrder;

            foreach (var group in passageGroups)
            {
                Guid parentKey = group.ParentKey;
                List<(Guid SubQuestionKey, Guid ParentKey)> subQuestionKeys = group.SubQuestionKeys;

                var subQuestionOrders = new List<(Guid SubQuestionKey, int Order)>();
                foreach (var subQuestion in subQuestionKeys)
                {
                    Guid subKey = subQuestion.SubQuestionKey;
                    subQuestionOrders.Add((subKey, tempOrder++));
                }

                if (subQuestionOrders.Any())
                {
                    int parentOrder = subQuestionOrders.First().Order;
                    allQuestionsWithOrder.Add((parentKey, true, parentOrder));
                }

                foreach (var subQuestion in subQuestionOrders)
                {
                    allQuestionsWithOrder.Add((subQuestion.SubQuestionKey, false, subQuestion.Order));
                }
            }

            // Bước 5: Gom vào collected (đảm bảo không trùng)
            foreach (var question in allQuestionsWithOrder)
            {
                if (!collected.Any(x => x.QuestionKey == question.QuestionKey))
                    collected.Add(new QuestionInfo { QuestionKey = question.QuestionKey, Part = part, Order = question.Order });
            }

            if (allQuestionsWithOrder.Any())
            {
                currentOrder = allQuestionsWithOrder.Max(x => x.Order) + 1;
            }

            // Fallback logic (giữ nguyên logic gốc, nhưng lọc theo collected để mô phỏng ContentOfTest hiện có)
            if (questionsAdded < totalQuestions)
            {
                int additionalQuestionsNeeded = totalQuestions - questionsAdded;
                string fallbackSql = $@"
            SELECT q.QuestionKey, q.Parent
            FROM {tableName} q
            WHERE q.Publish = 1 AND RecordStatus != 99 AND q.Parent IS NOT NULL
            AND q.QuestionKey NOT IN (
                SELECT QuestionKey FROM [ContentOfTest] WHERE TestKey = @TestKey
            )";

                var fallbackGroups = new Dictionary<Guid, List<(Guid SubQuestionKey, Guid ParentKey)>>();
                using (var cmd = new SqlCommand(fallbackSql, conn))
                {
                    cmd.Parameters.AddWithValue("@TestKey", testKey);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var subKey = reader.GetGuid(0);
                            var parentKey = reader.GetGuid(1);
                            if (!fallbackGroups.ContainsKey(parentKey))
                                fallbackGroups[parentKey] = new List<(Guid SubQuestionKey, Guid ParentKey)>();
                            fallbackGroups[parentKey].Add((subKey, parentKey));
                        }
                    }
                }

                // --- IMPORTANT: loại bỏ các sub-question đã có trong collected (tương đương đã có trong ContentOfTest hiện tại) ---
                var keys = fallbackGroups.Keys.ToList();
                foreach (var k in keys)
                {
                    var filtered = fallbackGroups[k].Where(s => !collected.Any(c => c.QuestionKey == s.SubQuestionKey)).ToList();
                    if (filtered.Count >= questionsPerPassage)
                        fallbackGroups[k] = filtered;
                    else
                        fallbackGroups.Remove(k); // không đủ sub-question sau khi lọc -> loại nhóm này
                }

                var sortedFallbackGroups = fallbackGroups
                    .Where(g => g.Value.Count >= questionsPerPassage)
                    .Select(g => (ParentKey: g.Key, SubQuestionKeys: g.Value))
                    .ToList();

                foreach (var group in sortedFallbackGroups)
                {
                    Guid parentKey = group.ParentKey;
                    List<(Guid SubQuestionKey, Guid ParentKey)> subQuestionsToAdd =
                        part == 3 || part == 4
                        ? group.SubQuestionKeys.Take(questionsPerPassage).ToList()
                        : group.SubQuestionKeys;

                    if (questionsRemaining <= 0) break;

                    var fallbackSubQuestionOrders = new List<(Guid SubQuestionKey, int Order)>();
                    foreach (var subQuestion in subQuestionsToAdd)
                    {
                        Guid subKey = subQuestion.SubQuestionKey;
                        if (questionsRemaining <= 0) break;

                        // nếu subKey đã có trong collected thì skip (an toàn)
                        if (collected.Any(c => c.QuestionKey == subKey)) continue;

                        fallbackSubQuestionOrders.Add((subKey, currentOrder++));
                        questionsAdded++;
                        questionsRemaining--;
                    }

                    // Giữ nguyên logic gốc: chỉ add parent nếu chưa có trong collected
                    bool parentAlreadyAdded = collected.Any(q => q.QuestionKey == parentKey);
                    if (!parentAlreadyAdded && fallbackSubQuestionOrders.Any())
                    {
                        int parentOrder = fallbackSubQuestionOrders.First().Order;
                        collected.Add(new QuestionInfo { QuestionKey = parentKey, Part = part, Order = parentOrder });
                    }

                    // Add sub-questions
                    foreach (var subQuestion in fallbackSubQuestionOrders)
                    {
                        // bảo đảm không duplicate
                        if (!collected.Any(c => c.QuestionKey == subQuestion.SubQuestionKey))
                        {
                            collected.Add(new QuestionInfo { QuestionKey = subQuestion.SubQuestionKey, Part = part, Order = subQuestion.Order });
                        }
                    }
                }
            }

            if (questionsAdded != totalQuestions)
                Console.WriteLine($"Warning: Part {part} only has {questionsAdded} questions, expected {totalQuestions}.");

            return currentOrder;
        }



        private static async Task<int> GenerateSimpleQuestions(SqlConnection conn, Guid testKey, Guid resultKey, int part, string tableName,
       ReadyModel.SkillLevelDistribution dist, int totalQuestions, int currentOrder, List<QuestionInfo> collected)
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
                                collected.Add(new QuestionInfo { QuestionKey = questionKey, Part = part, Order = currentOrder++ });
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
        public static async Task BulkInsertAndUpdateTestDataFromString(Guid testKey, Guid resultKey, List<QuestionInfo> questions)
        {
            if (questions == null || !questions.Any()) return;

            var sqlQuery = new StringBuilder();
            var parameters = new List<SqlParameter>();
            int paramIndex = 0;

            // INSERT nhiều dòng
            sqlQuery.AppendLine("INSERT INTO [ContentOfTest] (ContentKey, TestKey, ResultKey, QuestionKey, Part, [Order]) VALUES");
            var valueClauses = new List<string>();
            foreach (var q in questions)
            {
                string pContentKey = $"@p{paramIndex++}";
                string pTestKey = $"@p{paramIndex++}";
                string pResultKey = $"@p{paramIndex++}";
                string pQuestionKey = $"@p{paramIndex++}";
                string pPart = $"@p{paramIndex++}";
                string pOrder = $"@p{paramIndex++}";

                valueClauses.Add($"({pContentKey}, {pTestKey}, {pResultKey}, {pQuestionKey}, {pPart}, {pOrder})");

                parameters.Add(new SqlParameter(pContentKey, Guid.NewGuid()));
                parameters.Add(new SqlParameter(pTestKey, testKey));
                parameters.Add(new SqlParameter(pResultKey, resultKey));
                parameters.Add(new SqlParameter(pQuestionKey, q.QuestionKey));
                parameters.Add(new SqlParameter(pPart, q.Part));
                parameters.Add(new SqlParameter(pOrder, q.Order));
            }
            sqlQuery.AppendLine(string.Join(",\n", valueClauses));
            sqlQuery.AppendLine(";");

            // UPDATE cho từng Part
            var questionsByPart = questions.GroupBy(q => q.Part);
            foreach (var group in questionsByPart)
            {
                string tableName = $"TEC_Part{group.Key}_Question";
                var questionKeysInPart = group.Select(q => q.QuestionKey).Distinct().ToList();
                var updateParamNames = new List<string>();
                foreach (var key in questionKeysInPart)
                {
                    string pKey = $"@p{paramIndex++}";
                    updateParamNames.Add(pKey);
                    parameters.Add(new SqlParameter(pKey, key));
                }
                sqlQuery.AppendLine($"UPDATE {tableName} SET AmountAccess = AmountAccess + 1 WHERE QuestionKey IN ({string.Join(", ", updateParamNames)});");
            }

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    using (var cmd = new SqlCommand(sqlQuery.ToString(), conn, transaction))
                    {
                        cmd.Parameters.AddRange(parameters.ToArray());
                        try
                        {
                            await cmd.ExecuteNonQueryAsync();
                            await transaction.CommitAsync();
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                }
            }
        }


        private static async Task InsertContentOfTest(SqlConnection conn, Guid testKey, Guid resultKey, Guid questionKey, int part, int order)
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
                    cmd.Parameters.AddWithValue("@Order", order);

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
        public class QuestionInfo
        {
            public Guid QuestionKey { get; set; }
            public int Part { get; set; }
            public int Order { get; set; }
        }

    }
}