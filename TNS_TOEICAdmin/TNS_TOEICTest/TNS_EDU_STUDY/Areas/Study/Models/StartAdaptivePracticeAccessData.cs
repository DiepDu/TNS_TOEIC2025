
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace TNS_EDU_STUDY.Areas.Study.Models
{
    /// <summary>
    /// FIXED VERSION: 20 questions per part, 30 minutes (Part 7: 50 min)
    /// Distribution: 20% incorrect, 20% easy, 40% balanced, 20% challenging
    /// Order: Parent shares order with first child
    /// Filter: Publish = 1, RecordStatus != 99
    /// </summary>
    public class StartAdaptivePracticeAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
        private const int FIXED_QUESTION_COUNT = 20;

        // ============================================================
        // 🎯 MAIN: CHECK UNFINISHED TEST
        // ============================================================
        public static async Task<(Guid? TestKey, Guid? ResultKey)> CheckForUnfinishedAdaptiveTestAsync(Guid memberKey, int part)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string testName = $"Adaptive Practice Part {part} [{memberKey}]";
            string sql = @"
                SELECT TOP 1 t.TestKey, r.ResultKey
                FROM [Test] t
                JOIN [ResultOfUserForTest] r ON t.TestKey = r.TestKey
                WHERE r.MemberKey = @MemberKey
                  AND t.TestName = @TestName
                  AND r.EndTime IS NULL
                  AND r.Status != 99
                ORDER BY r.StartTime DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
            cmd.Parameters.AddWithValue("@TestName", testName);

            using var readerCheck = await cmd.ExecuteReaderAsync();
            if (await readerCheck.ReadAsync())
            {
                return (readerCheck.GetGuid(0), readerCheck.GetGuid(1));
            }

            return (null, null);
        }

        // ============================================================
        // 🎯 MAIN: CREATE ADAPTIVE TEST
        // ============================================================
        public static async Task<(Guid testKey, Guid resultKey)> CreateAdaptiveTestAsync(
            Guid memberKey, string memberName, int part)
        {
            try
            {
                Console.WriteLine($"[CreateAdaptiveTest] START - Member: {memberKey}, Part: {part}");

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // 1. Fixed configuration
                int totalQuestions = FIXED_QUESTION_COUNT;
                int duration = part == 7 ? 50 : 30;
                Console.WriteLine($"[CreateAdaptiveTest] Fixed: {totalQuestions} questions, {duration} minutes");

                // 2. Get member IRT ability
                float irtAbility = await GetMemberIrtAbilityAsync(conn, memberKey) ?? 0.0f;
                Console.WriteLine($"[CreateAdaptiveTest] IRT Ability: {irtAbility:F2}");

                // 3. Analyze error patterns
                var (topCategories, topGrammar, topVocab, topErrors) =
                    await AnalyzeErrorPatternsAsync(conn, memberKey, part);

                // 4. Distribution
                int incorrectCount = 4;  // 20% of 20
                int easyCount = 4;       // 20% of 20
                int balancedCount = 8;   // 40% of 20
                int challengingCount = 4; // 20% of 20

                // 5. Select parents/questions
                var usedParents = new HashSet<Guid>();
                var allSelected = new List<Guid>();

                var g1 = await SelectIncorrectParentsAsync(conn, memberKey, part, incorrectCount, usedParents);
                allSelected.AddRange(g1);

                var g2 = await SelectDifficultyBasedParentsAsync(conn, memberKey, part, easyCount, irtAbility, "EASY",
                    topCategories, topGrammar, topVocab, usedParents);
                allSelected.AddRange(g2);

                var g3 = await SelectDifficultyBasedParentsAsync(conn, memberKey, part, balancedCount, irtAbility, "BALANCED",
                    topCategories, topGrammar, topVocab, usedParents);
                allSelected.AddRange(g3);

                var g4 = await SelectDifficultyBasedParentsAsync(conn, memberKey, part, challengingCount, irtAbility, "CHALLENGING",
                    topCategories, topGrammar, topVocab, usedParents);
                allSelected.AddRange(g4);

                // 6. Verify children count and fill if needed
                int currentTotal = await CountTotalQuestionsFromParentsAsync(conn, part, allSelected);
                if (currentTotal < FIXED_QUESTION_COUNT)
                {
                    var fillResult = await FillRemainingQuestionsAsync(conn, memberKey, part, FIXED_QUESTION_COUNT - currentTotal, allSelected, usedParents);
                    allSelected.AddRange(fillResult);
                }

                // 7. Create test + result
                var testKey = Guid.NewGuid();
                var testName = $"Adaptive Practice Part {part} [{memberKey}]";
                var description = $"IRT-based adaptive test - {FIXED_QUESTION_COUNT} questions, {duration} minutes";
                await InsertTestAsync(conn, testKey, testName, description, FIXED_QUESTION_COUNT, duration, memberKey, memberName);

                var resultKey = Guid.NewGuid();
                await InsertStudyResultAsync(conn, resultKey, testKey, memberKey, memberName);

                // 8. Build explicit content list (parents + children to insert) and insert sequentially
                var contentList = await BuildContentListAsync(conn, part, allSelected, FIXED_QUESTION_COUNT);
                await InsertContentListSequentiallyAsync(conn, testKey, resultKey, part, contentList);

                Console.WriteLine($"[CreateAdaptiveTest] SUCCESS - TestKey: {testKey}");
                return (testKey, resultKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CreateAdaptiveTest ERROR]: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        // ============================================================
        // 🔍 HELPERS: IRT / ANALYSIS (unchanged names, unique readers inside)
        // ============================================================
        private static async Task<float?> GetMemberIrtAbilityAsync(SqlConnection conn, Guid memberKey)
        {
            string sql = "SELECT IrtAbility FROM EDU_Member WHERE MemberKey = @MemberKey";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
            var result = await cmd.ExecuteScalarAsync();
            return result != DBNull.Value ? Convert.ToSingle(result) : null;
        }

        private static async Task<(List<Guid> categories, List<Guid> grammar, List<Guid> vocab, List<Guid> errors)>
            AnalyzeErrorPatternsAsync(SqlConnection conn, Guid memberKey, int part)
        {
            string sql = @"
                WITH RecentErrors AS (
                    SELECT TOP 150 
                        UE.CategoryTopic, UE.GrammarTopic, UE.VocabularyTopic, UE.ErrorType
                    FROM UsersError UE
                    WHERE UE.UserKey = @MemberKey AND UE.Part = @Part
                    ORDER BY UE.ErrorDate DESC
                )
                SELECT CategoryTopic, GrammarTopic, VocabularyTopic, ErrorType, COUNT(*) AS ErrorCount
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

            using var readerAnalysis = await cmd.ExecuteReaderAsync();
            while (await readerAnalysis.ReadAsync())
            {
                if (readerAnalysis["CategoryTopic"] != DBNull.Value && categories.Count < 5)
                    categories.Add(readerAnalysis.GetGuid(0));
                if (readerAnalysis["GrammarTopic"] != DBNull.Value && grammar.Count < 5)
                    grammar.Add(readerAnalysis.GetGuid(1));
                if (readerAnalysis["VocabularyTopic"] != DBNull.Value && vocab.Count < 5)
                    vocab.Add(readerAnalysis.GetGuid(2));
                if (readerAnalysis["ErrorType"] != DBNull.Value && errors.Count < 5)
                    errors.Add(readerAnalysis.GetGuid(3));
            }

            return (categories, grammar, vocab, errors);
        }

        // ============================================================
        // 📊 SELECTION FUNCTIONS (kept behavior but reader names unique)
        // ============================================================
        private static async Task<List<Guid>> SelectIncorrectParentsAsync(
            SqlConnection conn, Guid memberKey, int part, int targetCount, HashSet<Guid> usedParents)
        {
            if (part == 1 || part == 2 || part == 5)
                return await SelectIncorrectStandaloneAsync(conn, memberKey, part, targetCount);

            int questionsPerPassage = part switch { 3 or 4 => 3, 6 => 4, 7 => 4, _ => 3 };
            int targetPassages = (int)Math.Ceiling((double)targetCount / questionsPerPassage);

            string sql = $@"
                SELECT TOP {targetPassages * 2} Q.Parent
                FROM TEC_Part{part}_Question Q
                INNER JOIN UserAnswers UA ON Q.QuestionKey = UA.QuestionKey
                INNER JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey
                WHERE R.MemberKey = @MemberKey
                  AND UA.IsCorrect = 0
                  AND Q.Parent IS NOT NULL
                  AND Q.Publish = 1
                  AND Q.RecordStatus != 99
                GROUP BY Q.Parent
                HAVING COUNT(DISTINCT Q.QuestionKey) >= 1
                ORDER BY COUNT(DISTINCT Q.QuestionKey) DESC, NEWID()";

            var selectedParents = new List<Guid>();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync() && selectedParents.Count < targetPassages)
            {
                var parentKey = rdr.GetGuid(0);
                if (!usedParents.Contains(parentKey))
                {
                    selectedParents.Add(parentKey);
                    usedParents.Add(parentKey);
                }
            }
            return selectedParents;
        }

        private static async Task<List<Guid>> SelectIncorrectStandaloneAsync(
            SqlConnection conn, Guid memberKey, int part, int count)
        {
            string sql = $@"
                SELECT TOP (@Count) QuestionKey
                FROM (
                    SELECT TOP 50 Q.QuestionKey
                    FROM TEC_Part{part}_Question Q
                    INNER JOIN UserAnswers UA ON Q.QuestionKey = UA.QuestionKey
                    INNER JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey
                    WHERE R.MemberKey = @MemberKey 
                      AND UA.IsCorrect = 0
                      AND Q.Publish = 1
                      AND Q.RecordStatus != 99
                    GROUP BY Q.QuestionKey
                    ORDER BY MAX(UA.AnswerTime) DESC
                ) AS RecentErrors
                ORDER BY NEWID()";

            var questions = new List<Guid>();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Count", count);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                questions.Add(rdr.GetGuid(0));
            }
            return questions;
        }

        private static async Task<List<Guid>> SelectDifficultyBasedParentsAsync(
            SqlConnection conn, Guid memberKey, int part, int targetCount, float irtAbility, string difficultyType,
            List<Guid> topCategories, List<Guid> topGrammar, List<Guid> topVocab, HashSet<Guid> usedParents)
        {
            if (part == 1 || part == 2 || part == 5)
                return await SelectDifficultyStandaloneAsync(conn, memberKey, part, targetCount, irtAbility, difficultyType,
                    topCategories, topGrammar, topVocab);

            float minDiff, maxDiff;
            switch (difficultyType)
            {
                case "EASY":
                    minDiff = irtAbility - 1.5f; maxDiff = irtAbility - 0.3f; break;
                case "BALANCED":
                    minDiff = irtAbility - 0.3f; maxDiff = irtAbility + 0.3f; break;
                case "CHALLENGING":
                    minDiff = irtAbility + 0.3f; maxDiff = irtAbility + 1.5f; break;
                default:
                    minDiff = irtAbility - 0.5f; maxDiff = irtAbility + 0.5f; break;
            }

            int questionsPerPassage = part switch { 3 or 4 => 3, 6 => 4, 7 => 4, _ => 3 };
            int targetPassages = (int)Math.Ceiling((double)targetCount / questionsPerPassage);

            var usedParentsList = usedParents.ToList();
            var excludeClause = usedParentsList.Any()
                ? $"AND Q.Parent NOT IN ({string.Join(",", usedParentsList.Select((_, i) => $"@up{i}"))})"
                : "";

            string sql = $@"
                SELECT TOP {targetPassages * 2} Q.Parent
                FROM TEC_Part{part}_Question Q
                WHERE Q.Parent IS NOT NULL
                  AND Q.IrtDifficulty BETWEEN @MinDiff AND @MaxDiff
                  AND Q.Publish = 1
                  AND Q.RecordStatus != 99
                  {excludeClause}
                  AND (
                      Q.Category IN ({BuildInClause(topCategories, "cat")}) OR
                      Q.GrammarTopic IN ({BuildInClause(topGrammar, "gram")}) OR
                      Q.VocabularyTopic IN ({BuildInClause(topVocab, "vocab")})
                  )
                GROUP BY Q.Parent
                HAVING COUNT(*) >= 1
                ORDER BY NEWID()";

            var selectedParents = new List<Guid>();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MinDiff", minDiff);
            cmd.Parameters.AddWithValue("@MaxDiff", maxDiff);

            AddInClauseParameters(cmd, topCategories, "cat");
            AddInClauseParameters(cmd, topGrammar, "gram");
            AddInClauseParameters(cmd, topVocab, "vocab");

            for (int i = 0; i < usedParentsList.Count; i++)
                cmd.Parameters.AddWithValue($"@up{i}", usedParentsList[i]);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync() && selectedParents.Count < targetPassages)
            {
                var parentKey = rdr.GetGuid(0);
                if (!usedParents.Contains(parentKey))
                {
                    selectedParents.Add(parentKey);
                    usedParents.Add(parentKey);
                }
            }
            return selectedParents;
        }

        private static async Task<List<Guid>> SelectDifficultyStandaloneAsync(
            SqlConnection conn, Guid memberKey, int part, int count, float irtAbility, string difficultyType,
            List<Guid> topCategories, List<Guid> topGrammar, List<Guid> topVocab)
        {
            float minDiff, maxDiff;
            switch (difficultyType)
            {
                case "EASY":
                    minDiff = irtAbility - 1.5f; maxDiff = irtAbility - 0.3f; break;
                case "BALANCED":
                    minDiff = irtAbility - 0.3f; maxDiff = irtAbility + 0.3f; break;
                case "CHALLENGING":
                    minDiff = irtAbility + 0.3f; maxDiff = irtAbility + 1.5f; break;
                default:
                    minDiff = irtAbility - 0.5f; maxDiff = irtAbility + 0.5f; break;
            }

            string sql = $@"
                SELECT TOP (@Count) Q.QuestionKey
                FROM TEC_Part{part}_Question Q
                WHERE Q.IrtDifficulty BETWEEN @MinDiff AND @MaxDiff
                  AND Q.Publish = 1
                  AND Q.RecordStatus != 99
                  AND (
                      Q.Category IN ({BuildInClause(topCategories, "cat")}) OR
                      Q.GrammarTopic IN ({BuildInClause(topGrammar, "gram")}) OR
                      Q.VocabularyTopic IN ({BuildInClause(topVocab, "vocab")})
                  )
                ORDER BY NEWID()";

            var questions = new List<Guid>();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Count", count);
            cmd.Parameters.AddWithValue("@MinDiff", minDiff);
            cmd.Parameters.AddWithValue("@MaxDiff", maxDiff);

            AddInClauseParameters(cmd, topCategories, "cat");
            AddInClauseParameters(cmd, topGrammar, "gram");
            AddInClauseParameters(cmd, topVocab, "vocab");

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                questions.Add(rdr.GetGuid(0));
            }
            return questions;
        }

        // ============================================================
        // 📊 FALLBACK: FILL REMAINING
        // ============================================================
        private static async Task<List<Guid>> FillRemainingQuestionsAsync(
            SqlConnection conn, Guid memberKey, int part, int remainingCount, List<Guid> existingParents, HashSet<Guid> usedParents)
        {
            if (part == 1 || part == 2 || part == 5)
            {
                string sql = $@"
                    SELECT TOP (@Count) Q.QuestionKey
                    FROM TEC_Part{part}_Question Q
                    WHERE Q.Publish = 1 
                      AND Q.RecordStatus != 99
                    ORDER BY NEWID()";

                var questions = new List<Guid>();
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Count", remainingCount);

                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    questions.Add(rdr.GetGuid(0));

                return questions;
            }

            // For parent-child: try to use more children from existing parents first
            var filledParents = new List<Guid>();

            // Look through existing parents and add them (we will choose children later in BuildContentList)
            foreach (var p in existingParents.Distinct())
            {
                if (!filledParents.Contains(p))
                {
                    filledParents.Add(p);
                    if (filledParents.Count * (part == 7 ? 4 : (part == 6 ? 4 : 3)) >= remainingCount)
                        break;
                }
            }

            // If not enough, pick additional parents randomly
            if (filledParents.Count * 1 < Math.Ceiling((double)remainingCount / (part == 7 ? 4 : (part == 6 ? 4 : 3))))
            {
                var usedParentsList = usedParents.ToList();
                var excludeClause = usedParentsList.Any()
                    ? $"AND Q.Parent NOT IN ({string.Join(",", usedParentsList.Select((_, i) => $"@upx{i}"))})"
                    : "";

                string sql2 = $@"
                    SELECT TOP {Math.Ceiling((double)remainingCount / (part == 7 ? 4 : (part == 6 ? 4 : 3)))} Q.Parent
                    FROM TEC_Part{part}_Question Q
                    WHERE Q.Parent IS NOT NULL 
                      AND Q.Publish = 1
                      AND Q.RecordStatus != 99
                      {excludeClause}
                    GROUP BY Q.Parent
                    ORDER BY NEWID()";

                using var cmd2 = new SqlCommand(sql2, conn);
                for (int i = 0; i < usedParentsList.Count; i++)
                    cmd2.Parameters.AddWithValue($"@upx{i}", usedParentsList[i]);

                using var rdr2 = await cmd2.ExecuteReaderAsync();
                while (await rdr2.ReadAsync())
                {
                    var pk = rdr2.GetGuid(0);
                    if (!usedParents.Contains(pk))
                    {
                        filledParents.Add(pk);
                        usedParents.Add(pk);
                    }
                }
            }

            // Return parent GUIDs (we will expand them into children later)
            return filledParents;
        }

        // ============================================================
        // 🔍 COUNT HELPER
        // ============================================================
        private static async Task<int> CountTotalQuestionsFromParentsAsync(
            SqlConnection conn, int part, List<Guid> parentKeys)
        {
            if (!parentKeys.Any()) return 0;

            if (part == 1 || part == 2 || part == 5)
                return parentKeys.Count;

            var parentParams = string.Join(",", parentKeys.Select((_, i) => $"@p{i}"));
            string sql = $@"
                SELECT COUNT(*)
                FROM TEC_Part{part}_Question Q
                WHERE Q.Parent IN ({parentParams})
                  AND Q.Publish = 1
                  AND Q.RecordStatus != 99";

            using var cmd = new SqlCommand(sql, conn);
            for (int i = 0; i < parentKeys.Count; i++)
                cmd.Parameters.AddWithValue($"@p{i}", parentKeys[i]);

            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        // ============================================================
        // 💾 INSERT TEST / RESULT (unchanged)
        // ============================================================
        private static async Task InsertTestAsync(
            SqlConnection conn, Guid testKey, string testName, string description,
            int totalQuestions, int duration, Guid memberKey, string memberName)
        {
            string sql = @"
                INSERT INTO [Test] (TestKey, TestName, Description, TotalQuestion, Duration, CreatedOn, CreatedBy, CreatedName, Status)
                VALUES (@TestKey, @TestName, @Description, @TotalQuestion, @Duration, GETDATE(), @CreatedBy, @CreatedName, 1)";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@TestKey", testKey);
            cmd.Parameters.AddWithValue("@TestName", testName);
            cmd.Parameters.AddWithValue("@Description", description);
            cmd.Parameters.AddWithValue("@TotalQuestion", totalQuestions);
            cmd.Parameters.AddWithValue("@Duration", duration);
            cmd.Parameters.AddWithValue("@CreatedBy", memberKey);
            cmd.Parameters.AddWithValue("@CreatedName", memberName);

            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task InsertStudyResultAsync(
            SqlConnection conn, Guid resultKey, Guid testKey, Guid memberKey, string memberName)
        {
            string sql = @"
                INSERT INTO ResultOfUserForTest (ResultKey, TestKey, MemberKey, MemberName, StartTime, Status)
                VALUES (@ResultKey, @TestKey, @MemberKey, @MemberName, GETDATE(), 1)";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ResultKey", resultKey);
            cmd.Parameters.AddWithValue("@TestKey", testKey);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
            cmd.Parameters.AddWithValue("@MemberName", memberName);

            await cmd.ExecuteNonQueryAsync();
        }

        // ============================================================
        // 🔧 BUILD CONTENT LIST: parents + selected children (guarantee child count)
        // Returns ordered list where parent entries are followed by their selected children.
        // ============================================================
        private static async Task<List<(Guid QuestionKey, bool IsParent)>> BuildContentListAsync(
            SqlConnection conn, int part, List<Guid> selectedGuids, int targetChildCount)
        {
            var content = new List<(Guid, bool)>();
            if (part == 1 || part == 2 || part == 5)
            {
                // Standalone: selectedGuids are question keys; ensure distinct and limit to target
                var distinct = selectedGuids.Distinct().ToList();
                if (distinct.Count > targetChildCount) distinct = distinct.Take(targetChildCount).ToList();
                foreach (var q in distinct)
                    content.Add((q, false));
                return content;
            }

            // Map every GUID to a parent GUID (if GUID is a child -> get its Parent; if it's a parent -> keep)
            var parentsOrdered = new List<Guid>();
            foreach (var g in selectedGuids)
            {
                Guid parentKey;
                string sqlParent = $@"SELECT Parent FROM TEC_Part{part}_Question WHERE QuestionKey = @Qk";
                using (var cmd = new SqlCommand(sqlParent, conn))
                {
                    cmd.Parameters.AddWithValue("@Qk", g);
                    var obj = await cmd.ExecuteScalarAsync();
                    if (obj == null || obj == DBNull.Value)
                        parentKey = g; // this is a parent or standalone
                    else
                        parentKey = (Guid)obj;
                }
                if (!parentsOrdered.Contains(parentKey)) parentsOrdered.Add(parentKey);
                if (parentsOrdered.Count >= targetChildCount) break;
            }

            int currentChildren = 0;
            var usedChildren = new HashSet<Guid>();

            // For each parent, take children up to remaining
            foreach (var parent in parentsOrdered)
            {
                if (currentChildren >= targetChildCount) break;

                // Get children ordered by Ranking
                var children = new List<Guid>();
                string sqlChildren = $@"
                    SELECT QuestionKey FROM TEC_Part{part}_Question
                    WHERE Parent = @ParentKey AND Publish = 1 AND RecordStatus != 99
                    ORDER BY Ranking";
                using (var cmd = new SqlCommand(sqlChildren, conn))
                {
                    cmd.Parameters.AddWithValue("@ParentKey", parent);
                    using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                        children.Add(rdr.GetGuid(0));
                }

                if (!children.Any()) continue;

                int toTake = Math.Min(children.Count, targetChildCount - currentChildren);
                if (toTake <= 0) continue;

                // Add parent then its first 'toTake' children
                content.Add((parent, true));
                for (int i = 0; i < toTake; i++)
                {
                    var ck = children[i];
                    if (!usedChildren.Contains(ck))
                    {
                        content.Add((ck, false));
                        usedChildren.Add(ck);
                        currentChildren++;
                    }
                }
            }

            // If still short, find additional parents and take children
            if (currentChildren < targetChildCount)
            {
                string sqlMoreParents = $@"
                    SELECT DISTINCT Parent
                    FROM TEC_Part{part}_Question
                    WHERE Parent IS NOT NULL AND Publish = 1 AND RecordStatus != 99
                    ORDER BY NEWID()";
                var extraParents = new List<Guid>();
                using (var cmd = new SqlCommand(sqlMoreParents, conn))
                {
                    using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                        extraParents.Add(rdr.GetGuid(0));
                }

                foreach (var parent in extraParents)
                {
                    if (currentChildren >= targetChildCount) break;
                    if (parentsOrdered.Contains(parent)) continue;

                    var children = new List<Guid>();
                    string sqlChildren = $@"
                        SELECT QuestionKey FROM TEC_Part{part}_Question
                        WHERE Parent = @ParentKey AND Publish = 1 AND RecordStatus != 99
                        ORDER BY Ranking";
                    using (var cmd = new SqlCommand(sqlChildren, conn))
                    {
                        cmd.Parameters.AddWithValue("@ParentKey", parent);
                        using var rdr = await cmd.ExecuteReaderAsync();
                        while (await rdr.ReadAsync())
                            children.Add(rdr.GetGuid(0));
                    }

                    if (!children.Any()) continue;

                    int toTake = Math.Min(children.Count, targetChildCount - currentChildren);
                    content.Add((parent, true));
                    for (int i = 0; i < toTake; i++)
                    {
                        var ck = children[i];
                        if (!usedChildren.Contains(ck))
                        {
                            content.Add((ck, false));
                            usedChildren.Add(ck);
                            currentChildren++;
                        }
                    }
                }
            }

            // Safety: ensure unique question keys in content
            var seen = new HashSet<Guid>();
            var deduped = new List<(Guid, bool)>();
            foreach (var entry in content)
            {
                if (!seen.Contains(entry.Item1))
                {
                    deduped.Add(entry);
                    seen.Add(entry.Item1);
                }
            }

            return deduped;
        }

        // ============================================================
        // 🔧 INSERT contentList sequentially, parent shares order with first child
        // contentList = [parent, child1, child2, parent2, child1, child2...]
        // ============================================================
        private static async Task InsertContentListSequentiallyAsync(
            SqlConnection conn, Guid testKey, Guid resultKey, int part, List<(Guid QuestionKey, bool IsParent)> contentList)
        {
            if (!contentList.Any()) return;

            // For standalone parts, treat all entries as single questions
            if (part == 1 || part == 2 || part == 5)
            {
                int order = 1;
                string sqlInsert = @"
                    INSERT INTO ContentOfTest (ContentKey, TestKey, ResultKey, QuestionKey, Part, [Order])
                    VALUES (NEWID(), @TestKey, @ResultKey, @QuestionKey, @Part, @Order)";
                foreach (var entry in contentList)
                {
                    using var cmd = new SqlCommand(sqlInsert, conn);
                    cmd.Parameters.AddWithValue("@TestKey", testKey);
                    cmd.Parameters.AddWithValue("@ResultKey", resultKey);
                    cmd.Parameters.AddWithValue("@QuestionKey", entry.QuestionKey);
                    cmd.Parameters.AddWithValue("@Part", part);
                    cmd.Parameters.AddWithValue("@Order", order++);
                    await cmd.ExecuteNonQueryAsync();
                }
                return;
            }

            // Parent-child: insert in groups. We expect contentList grouped: parent followed by its children.
            int idx = 0;
            int currentOrder = 1;
            string sqlInsertRow = @"
                INSERT INTO ContentOfTest (ContentKey, TestKey, ResultKey, QuestionKey, Part, [Order])
                VALUES (NEWID(), @TestKey, @ResultKey, @QuestionKey, @Part, @Order)";

            while (idx < contentList.Count)
            {
                // Expect a parent entry
                if (!contentList[idx].IsParent)
                {
                    // If data is not grouped as expected, treat this as orphan child: insert it with next order.
                    using (var cmdOrphan = new SqlCommand(sqlInsertRow, conn))
                    {
                        cmdOrphan.Parameters.AddWithValue("@TestKey", testKey);
                        cmdOrphan.Parameters.AddWithValue("@ResultKey", resultKey);
                        cmdOrphan.Parameters.AddWithValue("@QuestionKey", contentList[idx].QuestionKey);
                        cmdOrphan.Parameters.AddWithValue("@Part", part);
                        cmdOrphan.Parameters.AddWithValue("@Order", currentOrder);
                        await cmdOrphan.ExecuteNonQueryAsync();
                    }
                    idx++;
                    currentOrder++;
                    continue;
                }

                // It's a parent entry
                var parentGuid = contentList[idx].QuestionKey;

                // Insert parent with order = currentOrder
                using (var cmdParent = new SqlCommand(sqlInsertRow, conn))
                {
                    cmdParent.Parameters.AddWithValue("@TestKey", testKey);
                    cmdParent.Parameters.AddWithValue("@ResultKey", resultKey);
                    cmdParent.Parameters.AddWithValue("@QuestionKey", parentGuid);
                    cmdParent.Parameters.AddWithValue("@Part", part);
                    cmdParent.Parameters.AddWithValue("@Order", currentOrder);
                    await cmdParent.ExecuteNonQueryAsync();
                }

                idx++;

                // Insert following child entries for this parent.
                bool firstChildInserted = false;
                while (idx < contentList.Count && !contentList[idx].IsParent)
                {
                    var childGuid = contentList[idx].QuestionKey;

                    int childOrder;
                    if (!firstChildInserted)
                    {
                        // first child shares order with parent
                        childOrder = currentOrder;
                        firstChildInserted = true;
                    }
                    else
                    {
                        childOrder = ++currentOrder; // increment then use for next child
                    }

                    using (var cmdChild = new SqlCommand(sqlInsertRow, conn))
                    {
                        cmdChild.Parameters.AddWithValue("@TestKey", testKey);
                        cmdChild.Parameters.AddWithValue("@ResultKey", resultKey);
                        cmdChild.Parameters.AddWithValue("@QuestionKey", childGuid);
                        cmdChild.Parameters.AddWithValue("@Part", part);
                        cmdChild.Parameters.AddWithValue("@Order", childOrder);
                        await cmdChild.ExecuteNonQueryAsync();
                    }

                    idx++;
                }

                // After finishing children for this parent, move order to next position
                currentOrder++;
            }
        }

        // ============================================================
        // 🔧 UTILITIES
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
                cmd.Parameters.AddWithValue($"@{prefix}{i}", values[i]);
        }

        // ============================================================
        // 📦 DTO
        // ============================================================
        private class PartConfig
        {
            public int TotalQuestions { get; set; }
            public int Duration { get; set; }
        }
    }
}