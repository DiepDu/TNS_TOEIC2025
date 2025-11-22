using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNS_EDU_STUDY.Areas.Study.Models
{
    /// <summary>
    /// Data access layer for Adaptive Practice test generation
    /// Implements IRT-based adaptive question selection with 20-20-40-20 distribution
    /// </summary>
    public class StartAdaptivePracticeAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        // ============================================================
        // 🎯 MAIN: CREATE ADAPTIVE TEST
        // ============================================================

        /// <summary>
        /// Create adaptive test with 20% incorrect, 20% easy, 40% balanced, 20% challenging
        /// </summary>
        public static async Task<(Guid testKey, Guid resultKey)> CreateAdaptiveTestAsync(
            Guid memberKey,
            string memberName,
            int part)
        {
            try
            {
                Console.WriteLine($"[CreateAdaptiveTest] START - Member: {memberKey}, Part: {part}");

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // 1. Get Part Configuration
                var (config, distribution) = await GetPartConfigurationAsync(conn, part);
                if (config == null)
                {
                    throw new Exception($"Configuration not found for Part {part}");
                }

                int totalQuestions = config.TotalQuestions;
                Console.WriteLine($"[CreateAdaptiveTest] Total questions needed: {totalQuestions}");

                // 2. Get Member's IRT Ability
                float irtAbility = await GetMemberIrtAbilityAsync(conn, memberKey) ?? 0.0f;
                Console.WriteLine($"[CreateAdaptiveTest] Member IRT Ability: {irtAbility:F2}");

                // 3. Analyze Error Patterns
                var (topCategories, topGrammar, topVocab, topErrors) = await AnalyzeErrorPatternsAsync(conn, memberKey, part);
                Console.WriteLine($"[CreateAdaptiveTest] Error analysis - Cat:{topCategories.Count}, Gram:{topGrammar.Count}, Vocab:{topVocab.Count}");

                // 4. Calculate Distribution (20-20-40-20)
                int incorrectCount = (int)Math.Round(totalQuestions * 0.20);
                int easyCount = (int)Math.Round(totalQuestions * 0.20);
                int balancedCount = (int)Math.Round(totalQuestions * 0.40);
                int challengingCount = totalQuestions - incorrectCount - easyCount - balancedCount; // Ensure exact total

                Console.WriteLine($"[CreateAdaptiveTest] Distribution - Incorrect:{incorrectCount}, Easy:{easyCount}, Balanced:{balancedCount}, Challenging:{challengingCount}");

                // 5. Select Questions for Each Group
                var selectedQuestions = new List<Guid>();

                // Group 1: Previously Incorrect (20%)
                var incorrectQuestions = await SelectIncorrectQuestionsAsync(conn, memberKey, part, incorrectCount);
                selectedQuestions.AddRange(incorrectQuestions);
                Console.WriteLine($"[CreateAdaptiveTest] Group 1 (Incorrect): {incorrectQuestions.Count}/{incorrectCount}");

                // Group 2: Easy Questions (20%)
                var easyQuestions = await SelectDifficultyBasedQuestionsAsync(
                    conn, memberKey, part, easyCount, irtAbility, "EASY",
                    topCategories, topGrammar, topVocab, selectedQuestions);
                selectedQuestions.AddRange(easyQuestions);
                Console.WriteLine($"[CreateAdaptiveTest] Group 2 (Easy): {easyQuestions.Count}/{easyCount}");

                // Group 3: Balanced Questions (40%)
                var balancedQuestions = await SelectDifficultyBasedQuestionsAsync(
                    conn, memberKey, part, balancedCount, irtAbility, "BALANCED",
                    topCategories, topGrammar, topVocab, selectedQuestions);
                selectedQuestions.AddRange(balancedQuestions);
                Console.WriteLine($"[CreateAdaptiveTest] Group 3 (Balanced): {balancedQuestions.Count}/{balancedCount}");

                // Group 4: Challenging Questions (20%)
                var challengingQuestions = await SelectDifficultyBasedQuestionsAsync(
                    conn, memberKey, part, challengingCount, irtAbility, "CHALLENGING",
                    topCategories, topGrammar, topVocab, selectedQuestions);
                selectedQuestions.AddRange(challengingQuestions);
                Console.WriteLine($"[CreateAdaptiveTest] Group 4 (Challenging): {challengingQuestions.Count}/{challengingCount}");

                // 6. Verify Total Count
                if (selectedQuestions.Count < totalQuestions)
                {
                    Console.WriteLine($"[CreateAdaptiveTest] WARNING: Only {selectedQuestions.Count}/{totalQuestions} questions selected. Filling remaining...");
                    var fillQuestions = await FillRemainingQuestionsAsync(conn, memberKey, part, totalQuestions - selectedQuestions.Count, selectedQuestions);
                    selectedQuestions.AddRange(fillQuestions);
                }

                Console.WriteLine($"[CreateAdaptiveTest] Final count: {selectedQuestions.Count} questions");

                // 7. Create Test Record
                var testKey = Guid.NewGuid();
                var testName = $"Adaptive Practice Part {part} [{memberKey}]";
                var description = $"IRT-based adaptive test for {memberName} - Part {part}";
                await InsertTestAsync(conn, testKey, testName, description, totalQuestions, config.Duration, memberKey, memberName);

                // 8. Create Result Record
                var resultKey = Guid.NewGuid();
                await InsertStudyResultAsync(conn, resultKey, testKey, memberKey, memberName);

                // 9. Insert Test Content
                await InsertTestContentAsync(conn, testKey, resultKey, part, selectedQuestions);

                Console.WriteLine($"[CreateAdaptiveTest] SUCCESS - TestKey: {testKey}, ResultKey: {resultKey}");
                return (testKey, resultKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CreateAdaptiveTest ERROR]: {ex.Message}");
                Console.WriteLine($"[Stack]: {ex.StackTrace}");
                throw;
            }
        }

        // ============================================================
        // 🔍 HELPER: GET PART CONFIGURATION
        // ============================================================

        private static async Task<(PartConfig config, object distribution)> GetPartConfigurationAsync(SqlConnection conn, int part)
        {
            string sql = @"
                SELECT TotalQuestion, Duration 
                FROM TEC_Config 
                WHERE Part = @Part";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Part", part);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (new PartConfig
                {
                    TotalQuestions = reader.GetInt32(0),
                    Duration = reader.GetInt32(1)
                }, null);
            }

            return (null, null);
        }

        // ============================================================
        // 🔍 HELPER: GET MEMBER IRT ABILITY
        // ============================================================

        private static async Task<float?> GetMemberIrtAbilityAsync(SqlConnection conn, Guid memberKey)
        {
            string sql = "SELECT IrtAbility FROM EDU_Member WHERE MemberKey = @MemberKey";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);

            var result = await cmd.ExecuteScalarAsync();
            return result != DBNull.Value ? Convert.ToSingle(result) : null;
        }

        // ============================================================
        // 🔍 HELPER: ANALYZE ERROR PATTERNS
        // ============================================================

        private static async Task<(List<Guid> categories, List<Guid> grammar, List<Guid> vocab, List<Guid> errors)>
            AnalyzeErrorPatternsAsync(SqlConnection conn, Guid memberKey, int part)
        {
            string sql = @"
                WITH RecentErrors AS (
                    SELECT TOP 150 
                        UE.CategoryTopic,
                        UE.GrammarTopic,
                        UE.VocabularyTopic,
                        UE.ErrorType
                    FROM UsersError UE
                    WHERE UE.UserKey = @MemberKey
                      AND UE.Part = @Part
                    ORDER BY UE.ErrorDate DESC
                )
                SELECT 
                    CategoryTopic, GrammarTopic, VocabularyTopic, ErrorType, COUNT(*) AS ErrorCount
                FROM RecentErrors
                GROUP BY CategoryTopic, GrammarTopic, VocabularyTopic, ErrorType
                ORDER BY ErrorCount DESC";

            var categories = new List<Guid>();
            var grammar = new List<Guid>();
            var vocab = new List<Guid>();
            var errors = new List<Guid>();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
            cmd.Parameters.AddWithValue("@Part", part);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader["CategoryTopic"] != DBNull.Value && categories.Count < 5)
                    categories.Add(reader.GetGuid(0));
                if (reader["GrammarTopic"] != DBNull.Value && grammar.Count < 5)
                    grammar.Add(reader.GetGuid(1));
                if (reader["VocabularyTopic"] != DBNull.Value && vocab.Count < 5)
                    vocab.Add(reader.GetGuid(2));
                if (reader["ErrorType"] != DBNull.Value && errors.Count < 5)
                    errors.Add(reader.GetGuid(3));
            }

            return (categories, grammar, vocab, errors);
        }

        // ============================================================
        // 📊 GROUP 1: SELECT INCORRECT QUESTIONS (20%)
        // ============================================================

        private static async Task<List<Guid>> SelectIncorrectQuestionsAsync(
       SqlConnection conn, Guid memberKey, int part, int count)
        {
            // Logic: 
            // 1. Subquery (RecentErrors): Lấy 50 câu sai gần nhất (Dùng GROUP BY để loại bỏ trùng lặp an toàn)
            // 2. Outer Query: Lấy ngẫu nhiên số lượng cần thiết từ danh sách trên
            string sql = $@"
        SELECT TOP (@Count) QuestionKey
        FROM (
            SELECT TOP 50 Q.QuestionKey
            FROM TEC_Part{part}_Question Q
            INNER JOIN UserAnswers UA ON Q.QuestionKey = UA.QuestionKey
            INNER JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey
            WHERE R.MemberKey = @MemberKey
              AND UA.IsCorrect = 0
            GROUP BY Q.QuestionKey -- Dùng GROUP BY thay cho DISTINCT để tránh lỗi ORDER BY
            ORDER BY MAX(UA.AnswerTime) DESC -- Lấy lần sai gần nhất của câu đó
        ) AS RecentErrors
        ORDER BY NEWID()"; // Trộn ngẫu nhiên trong nhóm sai này

            var questions = new List<Guid>();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Count", count);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                questions.Add(reader.GetGuid(0));
            }

            return questions;
        }

        // ============================================================
        // 📊 GROUPS 2-4: SELECT DIFFICULTY-BASED QUESTIONS
        // ============================================================

        private static async Task<List<Guid>> SelectDifficultyBasedQuestionsAsync(
            SqlConnection conn,
            Guid memberKey,
            int part,
            int count,
            float irtAbility,
            string difficultyType, // "EASY", "BALANCED", "CHALLENGING"
            List<Guid> topCategories,
            List<Guid> topGrammar,
            List<Guid> topVocab,
            List<Guid> excludeQuestions)
        {
            // Define IRT difficulty ranges
            float minDifficulty, maxDifficulty;
            switch (difficultyType)
            {
                case "EASY":
                    minDifficulty = irtAbility - 1.5f;
                    maxDifficulty = irtAbility - 0.3f;
                    break;
                case "BALANCED":
                    minDifficulty = irtAbility - 0.3f;
                    maxDifficulty = irtAbility + 0.3f;
                    break;
                case "CHALLENGING":
                    minDifficulty = irtAbility + 0.3f;
                    maxDifficulty = irtAbility + 1.5f;
                    break;
                default:
                    minDifficulty = irtAbility - 0.5f;
                    maxDifficulty = irtAbility + 0.5f;
                    break;
            }

            var excludeClause = excludeQuestions.Any()
                ? $"AND Q.QuestionKey NOT IN ({string.Join(",", excludeQuestions.Select((_, i) => $"@exclude{i}"))})"
                : "";

            // Priority 1: Match error patterns + difficulty
            string sqlPriority = $@"
                SELECT TOP (@Count) Q.QuestionKey
                FROM TEC_Part{part}_Question Q
                WHERE Q.IrtDifficulty BETWEEN @MinDiff AND @MaxDiff
                  {excludeClause}
                  AND (
                      Q.Category IN ({BuildInClause(topCategories, "cat")}) OR
                      Q.GrammarTopic IN ({BuildInClause(topGrammar, "gram")}) OR
                      Q.VocabularyTopic IN ({BuildInClause(topVocab, "vocab")})
                  )
                  AND Q.QuestionKey NOT IN (
                      SELECT UA.QuestionKey 
                      FROM UserAnswers UA
                      INNER JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey
                      WHERE R.MemberKey = @MemberKey 
                        AND UA.AnswerTime >= DATEADD(DAY, -7, GETDATE())
                  )
                ORDER BY (
                    (CASE WHEN Q.Category IN ({BuildInClause(topCategories, "cat")}) THEN 100 ELSE 0 END) +
                    (CASE WHEN Q.GrammarTopic IN ({BuildInClause(topGrammar, "gram")}) THEN 80 ELSE 0 END) +
                    (CASE WHEN Q.VocabularyTopic IN ({BuildInClause(topVocab, "vocab")}) THEN 60 ELSE 0 END) +
                    (5.0 - ABS(Q.IrtDifficulty - @IrtAbility)) * 10
                ) DESC, NEWID()";

            var selectedQuestions = new List<Guid>();

            using (var cmd = new SqlCommand(sqlPriority, conn))
            {
                cmd.Parameters.AddWithValue("@Count", count);
                cmd.Parameters.AddWithValue("@MinDiff", minDifficulty);
                cmd.Parameters.AddWithValue("@MaxDiff", maxDifficulty);
                cmd.Parameters.AddWithValue("@IrtAbility", irtAbility);
                cmd.Parameters.AddWithValue("@MemberKey", memberKey);

                AddInClauseParameters(cmd, topCategories, "cat");
                AddInClauseParameters(cmd, topGrammar, "gram");
                AddInClauseParameters(cmd, topVocab, "vocab");

                for (int i = 0; i < excludeQuestions.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@exclude{i}", excludeQuestions[i]);
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    selectedQuestions.Add(reader.GetGuid(0));
                }
            }

            // If insufficient, fill with random questions of same difficulty
            if (selectedQuestions.Count < count)
            {
                int remaining = count - selectedQuestions.Count;
                var allExclude = excludeQuestions.Concat(selectedQuestions).ToList();
                var excludeClause2 = allExclude.Any()
                    ? $"AND Q.QuestionKey NOT IN ({string.Join(",", allExclude.Select((_, i) => $"@exc2_{i}"))})"
                    : "";

                string sqlFill = $@"
                    SELECT TOP (@Remaining) Q.QuestionKey
                    FROM TEC_Part{part}_Question Q
                    WHERE Q.IrtDifficulty BETWEEN @MinDiff AND @MaxDiff
                      {excludeClause2}
                      AND Q.QuestionKey NOT IN (
                          SELECT UA.QuestionKey 
                          FROM UserAnswers UA
                          INNER JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey
                          WHERE R.MemberKey = @MemberKey 
                            AND UA.AnswerTime >= DATEADD(DAY, -7, GETDATE())
                      )
                    ORDER BY NEWID()";

                using var cmd2 = new SqlCommand(sqlFill, conn);
                cmd2.Parameters.AddWithValue("@Remaining", remaining);
                cmd2.Parameters.AddWithValue("@MinDiff", minDifficulty);
                cmd2.Parameters.AddWithValue("@MaxDiff", maxDifficulty);
                cmd2.Parameters.AddWithValue("@MemberKey", memberKey);

                for (int i = 0; i < allExclude.Count; i++)
                {
                    cmd2.Parameters.AddWithValue($"@exc2_{i}", allExclude[i]);
                }

                using var reader = await cmd2.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    selectedQuestions.Add(reader.GetGuid(0));
                }

                Console.WriteLine($"[SelectDifficultyBased] {difficultyType}: Filled {selectedQuestions.Count - (count - remaining)} additional questions");
            }

            return selectedQuestions;
        }

        // ============================================================
        // 📊 FALLBACK: FILL REMAINING QUESTIONS
        // ============================================================

        private static async Task<List<Guid>> FillRemainingQuestionsAsync(
            SqlConnection conn, Guid memberKey, int part, int count, List<Guid> excludeQuestions)
        {
            var excludeClause = excludeQuestions.Any()
                ? $"AND Q.QuestionKey NOT IN ({string.Join(",", excludeQuestions.Select((_, i) => $"@exc{i}"))})"
                : "";

            string sql = $@"
                SELECT TOP (@Count) Q.QuestionKey
                FROM TEC_Part{part}_Question Q
                WHERE 1=1
                  {excludeClause}
                  AND Q.QuestionKey NOT IN (
                      SELECT UA.QuestionKey 
                      FROM UserAnswers UA
                      INNER JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey
                      WHERE R.MemberKey = @MemberKey 
                        AND UA.AnswerTime >= DATEADD(DAY, -7, GETDATE())
                  )
                ORDER BY NEWID()";

            var questions = new List<Guid>();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Count", count);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);

            for (int i = 0; i < excludeQuestions.Count; i++)
            {
                cmd.Parameters.AddWithValue($"@exc{i}", excludeQuestions[i]);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                questions.Add(reader.GetGuid(0));
            }

            return questions;
        }

        // ============================================================
        // 💾 INSERT TEST RECORD
        // ============================================================

        private static async Task InsertTestAsync(
            SqlConnection conn, Guid testKey, string testName, string description,
            int totalQuestions, int duration, Guid memberKey, string memberName)
        {
            string sql = @"
                INSERT INTO [Test] (TestKey, TestName, TestDescription, TotalQuestions, Duration, MemberKey, MemberName, CreateDate)
                VALUES (@TestKey, @TestName, @Description, @TotalQuestions, @Duration, @MemberKey, @MemberName, GETDATE())";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@TestKey", testKey);
            cmd.Parameters.AddWithValue("@TestName", testName);
            cmd.Parameters.AddWithValue("@Description", description);
            cmd.Parameters.AddWithValue("@TotalQuestions", totalQuestions);
            cmd.Parameters.AddWithValue("@Duration", duration);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
            cmd.Parameters.AddWithValue("@MemberName", memberName);

            await cmd.ExecuteNonQueryAsync();
        }

        // ============================================================
        // 💾 INSERT RESULT RECORD
        // ============================================================

        private static async Task InsertStudyResultAsync(
            SqlConnection conn, Guid resultKey, Guid testKey, Guid memberKey, string memberName)
        {
            string sql = @"
                INSERT INTO ResultOfUserForTest (ResultKey, TestKey, MemberKey, MemberName, BeginTime, RecordStatus)
                VALUES (@ResultKey, @TestKey, @MemberKey, @MemberName, GETDATE(), 1)";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ResultKey", resultKey);
            cmd.Parameters.AddWithValue("@TestKey", testKey);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
            cmd.Parameters.AddWithValue("@MemberName", memberName);

            await cmd.ExecuteNonQueryAsync();
        }

        // ============================================================
        // 💾 INSERT TEST CONTENT
        // ============================================================

        private static async Task InsertTestContentAsync(
            SqlConnection conn, Guid testKey, Guid resultKey, int part, List<Guid> questionKeys)
        {
            // For Part 3, 4, 6, 7: Need to group by Parent
            if (part == 3 || part == 4 || part == 6 || part == 7)
            {
                await InsertTestContentWithParentsAsync(conn, testKey, resultKey, part, questionKeys);
            }
            else
            {
                await InsertTestContentStandaloneAsync(conn, testKey, resultKey, part, questionKeys);
            }
        }

        private static async Task InsertTestContentWithParentsAsync(
            SqlConnection conn, Guid testKey, Guid resultKey, int part, List<Guid> questionKeys)
        {
            // Get parent-child mapping
            var parentGroups = new Dictionary<Guid, List<Guid>>();

            string sqlGetParents = $@"
                SELECT QuestionKey, Parent 
                FROM TEC_Part{part}_Question 
                WHERE QuestionKey IN ({string.Join(",", questionKeys.Select((_, i) => $"@qk{i}"))})";

            using (var cmd = new SqlCommand(sqlGetParents, conn))
            {
                for (int i = 0; i < questionKeys.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@qk{i}", questionKeys[i]);
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var qKey = reader.GetGuid(0);
                    var parent = reader.IsDBNull(1) ? Guid.Empty : reader.GetGuid(1);

                    if (parent != Guid.Empty)
                    {
                        if (!parentGroups.ContainsKey(parent))
                            parentGroups[parent] = new List<Guid>();
                        parentGroups[parent].Add(qKey);
                    }
                }
            }

            // Insert into TestContent
            int orderNumber = 1;
            foreach (var parentGroup in parentGroups)
            {
                string sqlInsert = @"
                    INSERT INTO TestContent (TestKey, ResultKey, QuestionKey, Part, OrderNumber)
                    VALUES (@TestKey, @ResultKey, @QuestionKey, @Part, @OrderNumber)";

                foreach (var childKey in parentGroup.Value)
                {
                    using var cmd = new SqlCommand(sqlInsert, conn);
                    cmd.Parameters.AddWithValue("@TestKey", testKey);
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    cmd.Parameters.AddWithValue("@QuestionKey", childKey);
                    cmd.Parameters.AddWithValue("@Part", part);
                    cmd.Parameters.AddWithValue("@OrderNumber", orderNumber++);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private static async Task InsertTestContentStandaloneAsync(
            SqlConnection conn, Guid testKey, Guid resultKey, int part, List<Guid> questionKeys)
        {
            string sqlInsert = @"
                INSERT INTO TestContent (TestKey, ResultKey, QuestionKey, Part, OrderNumber)
                VALUES (@TestKey, @ResultKey, @QuestionKey, @Part, @OrderNumber)";

            for (int i = 0; i < questionKeys.Count; i++)
            {
                using var cmd = new SqlCommand(sqlInsert, conn);
                cmd.Parameters.AddWithValue("@TestKey", testKey);
                cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                cmd.Parameters.AddWithValue("@QuestionKey", questionKeys[i]);
                cmd.Parameters.AddWithValue("@Part", part);
                cmd.Parameters.AddWithValue("@OrderNumber", i + 1);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        // ============================================================
        // 🔧 UTILITY: BUILD IN CLAUSE
        // ============================================================

        private static string BuildInClause(List<Guid> values, string prefix)
        {
            if (values == null || values.Count == 0)
                return "NULL";

            return string.Join(", ", values.Select((_, i) => $"@{prefix}{i}"));
        }

        private static void AddInClauseParameters(SqlCommand cmd, List<Guid> values, string prefix)
        {
            if (values == null || values.Count == 0)
                return;

            for (int i = 0; i < values.Count; i++)
            {
                cmd.Parameters.AddWithValue($"@{prefix}{i}", values[i]);
            }
        }

        // ============================================================
        // 📦 DTO CLASSES
        // ============================================================

        private class PartConfig
        {
            public int TotalQuestions { get; set; }
            public int Duration { get; set; }
        }
    }
}