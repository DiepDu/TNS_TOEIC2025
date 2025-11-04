using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace TNS_TOEICTest.Models.ChatWithAI.Services
{
    /// <summary>
    /// Service đề xuất câu hỏi thích ứng dựa trên IRT và lỗi thường gặp của Member
    /// </summary>
    public static class AdaptiveLearningService
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        public static async Task<List<Dictionary<string, object>>> GetRecommendedQuestionsAsync(
            string memberKey,
            int part,
            int limit = 10)
        {
            Console.WriteLine($"[GetRecommendedQuestionsAsync] START - MemberKey={memberKey}, Part={part}, Limit={limit}");

            const string DOMAIN = "https://localhost:7078";
            var recommendations = new List<Dictionary<string, object>>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    Console.WriteLine($"[GetRecommendedQuestionsAsync] Database connected successfully");

                    // ✅ VALIDATE INPUT
                    if (part < 1 || part > 7)
                    {
                        Console.WriteLine($"[ERROR] Invalid part number: {part}");
                        return recommendations;
                    }

                    // ✅ BƯỚC 1: Lấy IrtAbility của Member
                    Console.WriteLine($"[GetRecommendedQuestionsAsync] Step 1: Getting IRT Ability...");
                    float? irtAbility = await GetIrtAbilityAsync(conn, memberKey);
                    Console.WriteLine($"[GetRecommendedQuestionsAsync] IRT Ability: {irtAbility?.ToString() ?? "NULL"}");

                    // ✅ BƯỚC 2: Phân tích lỗi thường gặp của Member
                    Console.WriteLine($"[GetRecommendedQuestionsAsync] Step 2: Analyzing error patterns...");
                    var (topCategories, topGrammarTopics, topVocabTopics) = await AnalyzeErrorPatternsAsync(conn, memberKey, part);
                    Console.WriteLine($"[GetRecommendedQuestionsAsync] Error patterns - Categories:{topCategories.Count}, Grammar:{topGrammarTopics.Count}, Vocab:{topVocabTopics.Count}");

                    // ✅ BƯỚC 3: Lấy câu hỏi phù hợp theo loại Part
                    Console.WriteLine($"[GetRecommendedQuestionsAsync] Step 3: Selecting questions...");
                    if (part == 3 || part == 4 || part == 6 || part == 7)
                    {
                        Console.WriteLine($"[GetRecommendedQuestionsAsync] Using parent-based selection for Part {part}");
                        recommendations = await GetRecommendationsWithParents(
                            conn, memberKey, part, irtAbility ?? 1.0f,
                            topCategories, topGrammarTopics, topVocabTopics, DOMAIN
                        );
                    }
                    else
                    {
                        Console.WriteLine($"[GetRecommendedQuestionsAsync] Using standalone selection for Part {part}");
                        recommendations = await GetStandaloneRecommendations(
                            conn, memberKey, part, limit, irtAbility ?? 1.0f,
                            topCategories, topGrammarTopics, topVocabTopics, DOMAIN
                        );
                    }

                    Console.WriteLine($"[GetRecommendedQuestionsAsync] COMPLETED - Returned {recommendations.Count} questions");
                }
            }
            catch (SqlException sqlEx)
            {
                Console.WriteLine($"[GetRecommendedQuestionsAsync SQL ERROR]: {sqlEx.Message}");
                Console.WriteLine($"[SQL Error Number]: {sqlEx.Number}, Line: {sqlEx.LineNumber}");
                Console.WriteLine($"[SQL Stack Trace]: {sqlEx.StackTrace}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetRecommendedQuestionsAsync ERROR]: {ex.Message}");
                Console.WriteLine($"[Stack Trace]: {ex.StackTrace}");
                throw;
            }

            return recommendations;
        }

        // ========================================
        // ✅ HELPER: Lấy IRT Ability
        // ========================================
        private static async Task<float?> GetIrtAbilityAsync(SqlConnection conn, string memberKey)
        {
            var query = "SELECT IrtAbility FROM EDU_Member WHERE MemberKey = @MemberKey";

            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.Add("@MemberKey", SqlDbType.NVarChar, 50).Value = memberKey;

                var result = await cmd.ExecuteScalarAsync();
                if (result != DBNull.Value && result != null)
                {
                    return Convert.ToSingle(result);
                }
            }

            return null;
        }

        // ========================================
        // ✅ HELPER: Phân tích lỗi thường gặp
        // ========================================
        private static async Task<(List<Guid> categories, List<Guid> grammar, List<Guid> vocab)> AnalyzeErrorPatternsAsync(
            SqlConnection conn,
            string memberKey,
            int part)
        {
            var query = @"
WITH RecentErrors AS (
    SELECT TOP 150 
        UE.CategoryTopic,
        UE.GrammarTopic,
        UE.VocabularyTopic
    FROM UsersError UE
    WHERE UE.UserKey = @MemberKey
      AND UE.Part = @Part
    ORDER BY UE.ErrorDate DESC
)
SELECT 
    CategoryTopic,
    GrammarTopic,
    VocabularyTopic,
    COUNT(*) AS ErrorCount
FROM RecentErrors
GROUP BY CategoryTopic, GrammarTopic, VocabularyTopic
ORDER BY ErrorCount DESC";

            var topCategories = new List<Guid>();
            var topGrammarTopics = new List<Guid>();
            var topVocabTopics = new List<Guid>();

            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.Add("@MemberKey", SqlDbType.NVarChar, 50).Value = memberKey;
                cmd.Parameters.Add("@Part", SqlDbType.Int).Value = part;

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        if (reader["CategoryTopic"] != DBNull.Value && topCategories.Count < 5)
                        {
                            topCategories.Add(reader.GetGuid(reader.GetOrdinal("CategoryTopic")));
                        }

                        if (reader["GrammarTopic"] != DBNull.Value && topGrammarTopics.Count < 5)
                        {
                            topGrammarTopics.Add(reader.GetGuid(reader.GetOrdinal("GrammarTopic")));
                        }

                        if (reader["VocabularyTopic"] != DBNull.Value && topVocabTopics.Count < 5)
                        {
                            topVocabTopics.Add(reader.GetGuid(reader.GetOrdinal("VocabularyTopic")));
                        }
                    }
                }
            }

            return (topCategories, topGrammarTopics, topVocabTopics);
        }

        // ========================================
        // ✅ LOGIC CHO PART 3, 4, 6, 7 (CÓ PARENT)
        // ========================================
        private static async Task<List<Dictionary<string, object>>> GetRecommendationsWithParents(
      SqlConnection conn,
      string memberKey,
      int part,
      float irtAbility,
      List<Guid> topCategories,
      List<Guid> topGrammarTopics,
      List<Guid> topVocabTopics,
      string domain)
        {
            var recommendations = new List<Dictionary<string, object>>();

            // ✅ BƯỚC 1: Chọn TOP 1 PARENT phù hợp nhất (GIẢM TỪ 2 XUỐNG 1)
            var parentSelectionQuery = $@"
WITH ParentScores AS (
    SELECT DISTINCT
        Q.Parent AS ParentKey,
        AVG(
            (CASE WHEN Q.Category IN ({BuildInClauseGuid(topCategories, "cat")}) THEN 100 ELSE 0 END) +
            (CASE WHEN Q.GrammarTopic IN ({BuildInClauseGuid(topGrammarTopics, "gram")}) THEN 80 ELSE 0 END) +
            (CASE WHEN Q.VocabularyTopic IN ({BuildInClauseGuid(topVocabTopics, "vocab")}) THEN 60 ELSE 0 END) +
            (CASE WHEN Q.IrtDifficulty IS NOT NULL THEN (5.0 - ABS(Q.IrtDifficulty - @IrtAbility)) * 10 ELSE 0 END)
        ) AS AvgScore,
        COUNT(*) AS QuestionCount
    FROM TEC_Part{part}_Question Q
    WHERE Q.Parent IS NOT NULL
      AND Q.Parent NOT IN (
          SELECT DISTINCT Q2.Parent
          FROM TEC_Part{part}_Question Q2
          JOIN UserAnswers UA ON Q2.QuestionKey = UA.QuestionKey
          JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey
          WHERE R.MemberKey = @MemberKey 
            AND UA.AnswerTime >= DATEADD(DAY, -7, GETDATE())
            AND Q2.Parent IS NOT NULL
      )
    GROUP BY Q.Parent
)
SELECT TOP 1
    ParentKey,
    QuestionCount,
    AvgScore
FROM ParentScores
ORDER BY AvgScore DESC, QuestionCount DESC, NEWID()";

            var selectedParentKeys = new List<Guid>();
            using (var cmd = new SqlCommand(parentSelectionQuery, conn))
            {
                cmd.Parameters.Add("@MemberKey", SqlDbType.NVarChar, 50).Value = memberKey;
                cmd.Parameters.Add("@IrtAbility", SqlDbType.Float).Value = irtAbility;
                AddInClauseParametersGuid(cmd, topCategories, "cat");
                AddInClauseParametersGuid(cmd, topGrammarTopics, "gram");
                AddInClauseParametersGuid(cmd, topVocabTopics, "vocab");

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var parentKey = reader.GetGuid(reader.GetOrdinal("ParentKey"));
                        selectedParentKeys.Add(parentKey);

                        Console.WriteLine($"[GetRecommendationsWithParents Part {part}] Selected Parent: {parentKey}, " +
                            $"ChildCount: {reader["QuestionCount"]}, Score: {Math.Round(Convert.ToDouble(reader["AvgScore"]), 2)}");
                    }
                }
            }

            if (!selectedParentKeys.Any())
            {
                Console.WriteLine($"[GetRecommendationsWithParents Part {part}] No suitable parents found!");
                return recommendations;
            }

            var inClause = string.Join(",", selectedParentKeys.Select((_, i) => $"@parent{i}"));
            var childQuestionsQuery = $@"
SELECT TOP 5  -- ✅ THÊM LIMIT: Chỉ lấy tối đa 5 câu đầu tiên
    Q.QuestionKey,
    Q.QuestionText,
    Q.QuestionImage,
    Q.QuestionVoice,
    Q.Parent AS ParentKey,
    Q.IrtDifficulty,
    Q.Quality
FROM TEC_Part{part}_Question Q
WHERE Q.Parent IN ({inClause})
ORDER BY Q.Parent, Q.QuestionKey";

            using (var cmd = new SqlCommand(childQuestionsQuery, conn))
            {
                for (int i = 0; i < selectedParentKeys.Count; i++)
                {
                    cmd.Parameters.Add($"@parent{i}", SqlDbType.UniqueIdentifier).Value = selectedParentKeys[i];
                }

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var qKey = reader.GetGuid(reader.GetOrdinal("QuestionKey"));
                        var parentKey = reader.GetGuid(reader.GetOrdinal("ParentKey"));

                        recommendations.Add(new Dictionary<string, object>
                {
                    { "QuestionKey", qKey },
                    { "QuestionText", reader["QuestionText"]?.ToString() ?? "" },
                    { "QuestionImageUrl", BuildMediaUrl(domain, reader["QuestionImage"]?.ToString()) },
                    { "QuestionAudioUrl", BuildMediaUrl(domain, reader["QuestionVoice"]?.ToString()) },
                    { "ParentKey", parentKey },
                    { "ParentText", "" },
                    { "ParentAudioUrl", "" },
                    { "IrtDifficulty", reader["IrtDifficulty"] == DBNull.Value ? null : Math.Round(Convert.ToDouble(reader["IrtDifficulty"]), 2) },
                    { "Quality", reader["Quality"]?.ToString() ?? "Unknown" },
                    { "AllAnswers", new List<Dictionary<string, object>>() },
                    { "RecommendationReason", $"From a passage/audio matching your error patterns (Part {part})" }
                });
                    }
                }
            }

            Console.WriteLine($"[GetRecommendationsWithParents Part {part}] Total questions: {recommendations.Count}");

            // ✅ BƯỚC 3: Enrich với Parent data (FULL TEXT - NO TRUNCATE) và Answers
            await EnrichWithParentData(conn, recommendations, part, domain);
            await EnrichWith4Answers(conn, recommendations, part);

            return recommendations;
        }

        // ========================================
        // ✅ LOGIC CHO PART 1, 2, 5 (STANDALONE)
        // ========================================
        private static async Task<List<Dictionary<string, object>>> GetStandaloneRecommendations(
            SqlConnection conn,
            string memberKey,
            int part,
            int limit,
            float irtAbility,
            List<Guid> topCategories,
            List<Guid> topGrammarTopics,
            List<Guid> topVocabTopics,
            string domain)
        {
            var recommendations = new List<Dictionary<string, object>>();

            var queryBuilder = new StringBuilder();
            queryBuilder.Append($@"
WITH QuestionsWithScore AS (
    SELECT 
        Q.QuestionKey,
        Q.QuestionText,
        Q.QuestionImage,
        Q.QuestionVoice,
        Q.IrtDifficulty,
        Q.Quality,
        (
            (CASE WHEN Q.Category IN ({BuildInClauseGuid(topCategories, "cat")}) THEN 100 ELSE 0 END) +
            (CASE WHEN Q.GrammarTopic IN ({BuildInClauseGuid(topGrammarTopics, "gram")}) THEN 80 ELSE 0 END) +
            (CASE WHEN Q.VocabularyTopic IN ({BuildInClauseGuid(topVocabTopics, "vocab")}) THEN 60 ELSE 0 END) +
            (CASE WHEN Q.IrtDifficulty IS NOT NULL THEN (5.0 - ABS(Q.IrtDifficulty - @IrtAbility)) * 10 ELSE 0 END) +
            (CASE WHEN Q.IrtDiscrimination IS NOT NULL THEN Q.IrtDiscrimination * 5 ELSE 0 END) +
            (CASE Q.Quality WHEN 'Excellent' THEN 20 WHEN 'Good' THEN 10 WHEN 'Fair' THEN 5 ELSE 0 END)
        ) AS TotalScore
    FROM TEC_Part{part}_Question Q
    WHERE Q.QuestionKey NOT IN (
        SELECT UA.QuestionKey 
        FROM UserAnswers UA
        JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey
        WHERE R.MemberKey = @MemberKey 
          AND UA.AnswerTime >= DATEADD(DAY, -7, GETDATE())
    )
)
SELECT TOP (@Limit)
    QuestionKey, QuestionText, QuestionImage, QuestionVoice, IrtDifficulty, Quality
FROM QuestionsWithScore
ORDER BY TotalScore DESC, NEWID()");

            using (var cmd = new SqlCommand(queryBuilder.ToString(), conn))
            {
                cmd.Parameters.Add("@Limit", SqlDbType.Int).Value = limit;
                cmd.Parameters.Add("@MemberKey", SqlDbType.NVarChar, 50).Value = memberKey;
                cmd.Parameters.Add("@IrtAbility", SqlDbType.Float).Value = irtAbility;
                AddInClauseParametersGuid(cmd, topCategories, "cat");
                AddInClauseParametersGuid(cmd, topGrammarTopics, "gram");
                AddInClauseParametersGuid(cmd, topVocabTopics, "vocab");

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var qKey = reader.GetGuid(reader.GetOrdinal("QuestionKey"));

                        recommendations.Add(new Dictionary<string, object>
                        {
                            { "QuestionKey", qKey },
                            { "QuestionText", reader["QuestionText"]?.ToString() ?? "" },
                            { "QuestionImageUrl", BuildMediaUrl(domain, reader["QuestionImage"]?.ToString()) },
                            { "QuestionAudioUrl", BuildMediaUrl(domain, reader["QuestionVoice"]?.ToString()) },
                            { "ParentKey", null },
                            { "ParentText", "" },
                            { "ParentAudioUrl", "" },
                            { "IrtDifficulty", reader["IrtDifficulty"] == DBNull.Value ? null : Math.Round(Convert.ToDouble(reader["IrtDifficulty"]), 2) },
                            { "Quality", reader["Quality"]?.ToString() ?? "Unknown" },
                            { "AllAnswers", new List<Dictionary<string, object>>() },
                            { "RecommendationReason", $"Matches your common error patterns (Part {part})" }
                        });
                    }
                }
            }

            Console.WriteLine($"[GetStandaloneRecommendations Part {part}] Found {recommendations.Count} questions");

            await EnrichWith4Answers(conn, recommendations, part);
            return recommendations;
        }

        // ========================================
          // ✅ HELPER: Enrich Parent Data (NO TRUNCATE)
          // ========================================
private static async Task EnrichWithParentData(
    SqlConnection conn,
    List<Dictionary<string, object>> recommendations,
    int part,
    string domain)
        {
            var parentKeys = recommendations
                .Where(r => r["ParentKey"] != null && (Guid?)r["ParentKey"] != null)
                .Select(r => (Guid)r["ParentKey"]!)
                .Distinct()
                .ToList();

            if (!parentKeys.Any())
            {
                Console.WriteLine($"[EnrichWithParentData Part {part}] No parent keys to enrich");
                return;
            }

            var inClause = string.Join(",", parentKeys.Select((_, i) => $"@p{i}"));
            var query = $@"
        SELECT QuestionKey, QuestionText, QuestionVoice
        FROM TEC_Part{part}_Question
        WHERE QuestionKey IN ({inClause})";

            var parentData = new Dictionary<Guid, (string text, string audio)>();

            using (var cmd = new SqlCommand(query, conn))
            {
                for (int i = 0; i < parentKeys.Count; i++)
                {
                    cmd.Parameters.Add($"@p{i}", SqlDbType.UniqueIdentifier).Value = parentKeys[i];
                }

                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        var key = rdr.GetGuid(0);
                        var text = rdr["QuestionText"]?.ToString() ?? "";
                        var audio = BuildMediaUrl(domain, rdr["QuestionVoice"]?.ToString());

                        // ✅ NO TRUNCATE - Keep full text
                        Console.WriteLine($"[EnrichWithParentData] Parent {key}: Full text ({text.Length} chars) preserved");

                        parentData[key] = (text, audio);
                    }
                }
            }

            foreach (var rec in recommendations)
            {
                if (rec["ParentKey"] != null && parentData.ContainsKey((Guid)rec["ParentKey"]!))
                {
                    var (text, audio) = parentData[(Guid)rec["ParentKey"]!];
                    rec["ParentText"] = text;
                    rec["ParentAudioUrl"] = audio;
                }
            }

            Console.WriteLine($"[EnrichWithParentData Part {part}] Enriched {parentData.Count} parent(s) with full text");
        }

        // ========================================
        // ✅ HELPER: Enrich với 4 đáp án
        // ========================================
        private static async Task EnrichWith4Answers(
            SqlConnection conn,
            List<Dictionary<string, object>> recommendations,
            int part)
        {
            var tableName = $"TEC_Part{part}_Answer";
            int totalAnswers = 0;

            foreach (var rec in recommendations)
            {
                var qKey = (Guid)rec["QuestionKey"];
                var query = $@"
                    SELECT AnswerKey, AnswerText, AnswerCorrect
                    FROM {tableName}
                    WHERE QuestionKey = @QuestionKey
                    ORDER BY AnswerKey";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.Add("@QuestionKey", SqlDbType.UniqueIdentifier).Value = qKey;

                    var answers = new List<Dictionary<string, object>>();
                    using (var rdr = await cmd.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            answers.Add(new Dictionary<string, object>
                            {
                                { "AnswerKey", rdr.GetGuid(0) },
                                { "AnswerText", rdr["AnswerText"]?.ToString() ?? "" },
                                { "IsCorrect", Convert.ToBoolean(rdr["AnswerCorrect"]) }
                            });
                        }
                    }

                    rec["AllAnswers"] = answers;
                    totalAnswers += answers.Count;
                }
            }

            Console.WriteLine($"[EnrichWith4Answers Part {part}] Enriched {totalAnswers} answers for {recommendations.Count} questions");
        }

        // ========================================
        // ✅ HELPER: Build Media URL
        // ========================================
        private static string BuildMediaUrl(string domain, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return "";

            if (!relativePath.StartsWith("/"))
                relativePath = "/" + relativePath;

            return domain + relativePath;
        }

        // ========================================
        // ✅ HELPER: Build IN Clause for GUID
        // ========================================
        private static string BuildInClauseGuid(List<Guid> values, string prefix)
        {
            if (values == null || values.Count == 0)
                return "NULL";

            return string.Join(", ", values.Select((v, i) => $"@{prefix}{i}"));
        }

        // ========================================
        // ✅ HELPER: Add IN Clause Parameters
        // ========================================
        private static void AddInClauseParametersGuid(SqlCommand cmd, List<Guid> values, string prefix)
        {
            if (values == null || values.Count == 0)
                return;

            for (int i = 0; i < values.Count; i++)
            {
                cmd.Parameters.Add($"@{prefix}{i}", SqlDbType.UniqueIdentifier).Value = values[i];
            }
        }
    }
}