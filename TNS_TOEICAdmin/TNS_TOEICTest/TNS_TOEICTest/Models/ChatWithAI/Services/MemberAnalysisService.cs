using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System.Text;
using TNS_TOEICTest.Models.ChatWithAI.Repositories;
using static TNS_TOEICTest.Models.ChatWithAI.DTOs.DTOs;

namespace TNS_TOEICTest.Models.ChatWithAI.Services
{
    public class MemberAnalysisService
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
        /// <summary>
        /// ⚠️ DEPRECATED: Hàm này chỉ dùng cho Admin xem chi tiết Member
        /// Member chatbot KHÔNG NÊN dùng hàm này vì quá nặng
        /// Thay vào đó dùng các tool riêng: GetMyPerformanceAnalysisAsync, GetMyErrorAnalysisAsync, etc.
        /// </summary>
        [Obsolete("Use GetMyPerformanceAnalysisAsync and other specific tools instead for chatbot")]
        public static async Task<string> LoadMemberFullAnalysisForAdminAsync(string memberKey)
        {
            // ✅ ĐỔI TÊN để tránh nhầm lẫn
            var report = new Dictionary<string, object>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // --- 1) MEMBER PROFILE ---
                    var memberProfile = await MemberDataRepository.GetMemberProfileAsync(connection, memberKey);

                    if (memberProfile != null)
                    {
                        report["memberProfile"] = memberProfile;
                    }
                    else
                    {
                        report["memberProfile"] = new { message = "Member not found" };
                    }

                    // --- 2) ALL TESTS ---
                    var allResults = await MemberDataRepository.GetAllResultsForMemberAsync(connection, memberKey);
                    report["allResultsCount"] = allResults.Count;
                    report["recentResults"] = allResults.Take(20).ToList();

                    // --- 3) FULL TESTS SUMMARY ---
                    var lastFullTests = allResults
                        .Where(r => r.TotalQuestion >= 100 && r.TestScore != null)
                        .Take(5)
                        .ToList();
                    report["lastFullTests"] = lastFullTests;
                    report["scoreStatistics"] = MemberDataRepository.ComputeScoreStatistics(
                        allResults.Where(r => r.TestScore.HasValue).Select(r => r.TestScore!.Value).ToList()
                    );
                    report["scoreTrend"] = MemberDataRepository.ComputeScoreTrend(
                        lastFullTests.Where(r => r.TestScore.HasValue).Select(r => r.TestScore!.Value).ToList()
                    );

                    // --- 4) BEHAVIOR ANALYSIS ---
                    var recentResultKeys = allResults.Select(r => r.ResultKey).Take(20).ToList();
                    var userAnswers = await MemberDataRepository.GetUserAnswersByResultKeysAsync(connection, recentResultKeys);
                    report["behavior"] = MemberDataRepository.AnalyzeBehavior(userAnswers);

                    // --- 5) ERROR ANALYSIS ---
                    var userErrors = await MemberDataRepository.GetUserErrorsAsync(connection, memberKey, limit: 150);
                    report["errorAnalysis"] = MemberDataRepository.AnalyzeErrors(userErrors);

                    // --- 6) RECENT MISTAKES DETAILED ---
                    var mistakes = await MemberDataRepository.GetRecentMistakesDetailedAsync(connection, memberKey, 10);
                    report["recentMistakesDetailed"] = mistakes;

                    // ✅ === 7) IRT-BASED ANALYSIS ===
                    if (memberProfile != null)
                    {
                        report["irtAnalysis"] = await AnalyzeIrtMatchingAsync(connection, memberKey, memberProfile);
                        report["partStrengthWeakness"] = AnalyzePartPerformance(memberProfile);
                        report["progressToTarget"] = AnalyzeProgressToTarget(memberProfile, allResults);
                    }
                    else
                    {
                        report["irtAnalysis"] = new { message = "Cannot analyze IRT - member profile not found" };
                        report["partStrengthWeakness"] = new { message = "Cannot analyze part performance - member profile not found" };
                        report["progressToTarget"] = new { message = "Cannot analyze progress - member profile not found" };
                    }

                    // --- 8) NOTES ---
                    var notes = new List<string>();
                    if (!recentResultKeys.Any()) notes.Add("Not enough tests to analyze behavior.");
                    if (!userAnswers.Any()) notes.Add("No per-question data available.");
                    if (memberProfile == null) notes.Add("⚠️ Member profile not found - some analysis features are disabled.");
                    report["notes"] = notes;
                }
            }
            catch (Exception ex)
            {
                report["fatal"] = new { error = ex.Message, stack = ex.StackTrace?.Split('\n')?.Take(5) };
            }

            return JsonConvert.SerializeObject(report, Formatting.Indented);
        }
        public static async Task<string> LoadRecentFeedbacksAsync(string memberKey)
        {
            var feedbackBuilder = new StringBuilder();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = @"
            WITH AllQuestions AS (
                SELECT QuestionKey, QuestionText, '1' AS Part FROM TEC_Part1_Question UNION ALL
                SELECT QuestionKey, QuestionText, '2' AS Part FROM TEC_Part2_Question UNION ALL
                SELECT QuestionKey, QuestionText, '3' AS Part FROM TEC_Part3_Question UNION ALL
                SELECT QuestionKey, QuestionText, '4' AS Part FROM TEC_Part4_Question UNION ALL
                SELECT QuestionKey, QuestionText, '5' AS Part FROM TEC_Part5_Question UNION ALL
                SELECT QuestionKey, QuestionText, '6' AS Part FROM TEC_Part6_Question UNION ALL
                SELECT QuestionKey, QuestionText, '7' AS Part FROM TEC_Part7_Question
            )
            SELECT TOP 10
                FB.FeedbackText,
                FB.CreatedOn,
                FB.Part,
                Q.QuestionText
            FROM QuestionFeedbacks FB
            LEFT JOIN AllQuestions Q ON FB.QuestionKey = Q.QuestionKey
            WHERE FB.MemberKey = @MemberKey
            ORDER BY FB.CreatedOn DESC;";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@MemberKey", memberKey);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        int feedbackCount = 1;
                        while (await reader.ReadAsync())
                        {
                            feedbackBuilder.AppendLine($"[Feedback #{feedbackCount++} - Part {reader["Part"]} - Date: {((DateTime)reader["CreatedOn"]):yyyy-MM-dd}]");
                            feedbackBuilder.AppendLine($"  - Regarding Question: '{reader["QuestionText"]}'");
                            feedbackBuilder.AppendLine($"  - Student's Feedback: \"{reader["FeedbackText"]}\"");
                        }

                        if (feedbackCount == 1)
                        {
                            return string.Empty;
                        }
                    }
                }
            }
            return feedbackBuilder.ToString();
        }
        private static async Task<object> AnalyzeIrtMatchingAsync(
        SqlConnection conn,
        string memberKey,
        MemberProfileDto profile)
        {
            if (!profile.IrtAbility.HasValue)
            {
                return new { message = "IRT ability not calculated yet. Need more test data." };
            }

            var irtAbility = profile.IrtAbility.Value;

            // ✅ CẬP NHẬT QUERY: Thêm IrtGuessing và ConfidenceLevel
            var query = @"
WITH RecentErrors AS (
    SELECT TOP 50 UA.QuestionKey, UA.Part, UA.AnswerTime
    FROM UserAnswers UA
    JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey
    WHERE R.MemberKey = @MemberKey AND UA.IsCorrect = 0
    ORDER BY UA.AnswerTime DESC
),
AllQuestionsWithIRT AS (
    SELECT QuestionKey, IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, 1 AS Part FROM TEC_Part1_Question WHERE IrtDifficulty IS NOT NULL
    UNION ALL SELECT QuestionKey, IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, 2 AS Part FROM TEC_Part2_Question WHERE IrtDifficulty IS NOT NULL
    UNION ALL SELECT QuestionKey, IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, 3 AS Part FROM TEC_Part3_Question WHERE IrtDifficulty IS NOT NULL
    UNION ALL SELECT QuestionKey, IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, 4 AS Part FROM TEC_Part4_Question WHERE IrtDifficulty IS NOT NULL
    UNION ALL SELECT QuestionKey, IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, 5 AS Part FROM TEC_Part5_Question WHERE IrtDifficulty IS NOT NULL
    UNION ALL SELECT QuestionKey, IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, 6 AS Part FROM TEC_Part6_Question WHERE IrtDifficulty IS NOT NULL
    UNION ALL SELECT QuestionKey, IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, 7 AS Part FROM TEC_Part7_Question WHERE IrtDifficulty IS NOT NULL
)
SELECT 
    RE.Part,
    Q.IrtDifficulty,
    Q.IrtDiscrimination,
    Q.IrtGuessing,
    Q.Quality,
    Q.ConfidenceLevel,
    CASE 
        WHEN Q.IrtDifficulty < @IrtAbility - 0.5 THEN 'TooEasy'
        WHEN Q.IrtDifficulty > @IrtAbility + 0.5 THEN 'TooHard'
        ELSE 'Appropriate'
    END AS DifficultyLevel
FROM RecentErrors RE
JOIN AllQuestionsWithIRT Q ON RE.QuestionKey = Q.QuestionKey";

            var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
            cmd.Parameters.AddWithValue("@IrtAbility", irtAbility);

            var errorsByDifficulty = new Dictionary<string, int> {
        { "TooEasy", 0 }, { "Appropriate", 0 }, { "TooHard", 0 }
    };
            var avgDifficulty = new List<float>();
            var avgDiscrimination = new List<float>(); // ✅ THÊM
            var avgGuessing = new List<float>(); // ✅ THÊM
            var qualityDistribution = new Dictionary<string, int>(); // ✅ THÊM
            var confidenceDistribution = new Dictionary<string, int>(); // ✅ THÊM

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var diffLevel = reader["DifficultyLevel"].ToString();
                    if (errorsByDifficulty.ContainsKey(diffLevel))
                        errorsByDifficulty[diffLevel]++;

                    avgDifficulty.Add(Convert.ToSingle(reader["IrtDifficulty"]));

                    // ✅ THÊM: Thu thập IrtDiscrimination
                    if (reader["IrtDiscrimination"] != DBNull.Value)
                        avgDiscrimination.Add(Convert.ToSingle(reader["IrtDiscrimination"]));

                    // ✅ THÊM: Thu thập IrtGuessing
                    if (reader["IrtGuessing"] != DBNull.Value)
                        avgGuessing.Add(Convert.ToSingle(reader["IrtGuessing"]));

                    // ✅ THÊM: Phân loại Quality
                    var quality = reader["Quality"]?.ToString() ?? "Unknown";
                    if (!qualityDistribution.ContainsKey(quality)) qualityDistribution[quality] = 0;
                    qualityDistribution[quality]++;

                    // ✅ THÊM: Phân loại ConfidenceLevel
                    var confidence = reader["ConfidenceLevel"]?.ToString() ?? "Unknown";
                    if (!confidenceDistribution.ContainsKey(confidence)) confidenceDistribution[confidence] = 0;
                    confidenceDistribution[confidence]++;
                }
            }

            var result = new Dictionary<string, object>
    {
        { "memberIrtAbility", Math.Round(irtAbility, 2) },
        { "lastUpdated", profile.IrtUpdatedOn?.ToString("yyyy-MM-dd HH:mm") ?? "N/A" },
        { "recentErrorsByDifficulty", errorsByDifficulty },
        { "avgDifficultyOfErrors", avgDifficulty.Any() ? Math.Round(avgDifficulty.Average(), 2) : 0 },
        
        // ✅ THÊM: Phân tích Discrimination
        { "avgDiscriminationOfErrors", avgDiscrimination.Any() ? Math.Round(avgDiscrimination.Average(), 2) : 0 },
        { "discriminationInterpretation", InterpretDiscrimination(avgDiscrimination.Any() ? avgDiscrimination.Average() : 0) },
        
        // ✅ THÊM: Phân tích Guessing
        { "avgGuessingParameterOfErrors", avgGuessing.Any() ? Math.Round(avgGuessing.Average(), 2) : 0 },
        { "guessingInterpretation", InterpretGuessing(avgGuessing.Any() ? avgGuessing.Average() : 0) },
        
        // ✅ THÊM: Phân phối Quality
        { "errorsByQuality", qualityDistribution },
        
        // ✅ THÊM: Phân phối ConfidenceLevel
        { "errorsByConfidenceLevel", confidenceDistribution }
    };

            // ✅ NÂNG CẤP: AI Suggestions
            var suggestions = GenerateAdvancedSuggestions(
                errorsByDifficulty,
                avgDiscrimination.Any() ? avgDiscrimination.Average() : 0,
                avgGuessing.Any() ? avgGuessing.Average() : 0,
                qualityDistribution
            );

            result["aiSuggestions"] = suggestions;
            return result;
        }

        /// <summary>
        /// ✅ TOOL 1: Lấy phân tích chi tiết về năng lực hiện tại
        /// </summary>
        public static async Task<object> GetMyPerformanceAnalysisAsync(string memberKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                var profile = await MemberDataRepository.GetMemberProfileAsync(conn, memberKey);

                if (profile == null)
                    return new { message = "Profile not found" };

                var allResults = await MemberDataRepository.GetAllResultsForMemberAsync(conn, memberKey);

                return new
                {
                    irtAnalysis = await AnalyzeIrtMatchingAsync(conn, memberKey, profile),
                    partStrengthWeakness = AnalyzePartPerformance(profile),
                    progressToTarget = AnalyzeProgressToTarget(profile, allResults),
                    scoreStatistics = MemberDataRepository.ComputeScoreStatistics(
                        allResults.Where(r => r.TestScore.HasValue).Select(r => r.TestScore!.Value).ToList()
                    )
                };
            }
        }

        /// <summary>
        /// ✅ TOOL 2: Lấy phân tích lỗi chi tiết (LUÔN lấy 150 lỗi gần nhất)
        /// </summary>
        public static async Task<object> GetMyErrorAnalysisAsync(string memberKey, int limit = 150)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // ✅ LUÔN lấy 150 lỗi gần nhất (bỏ tham số limit)
                var userErrors = await MemberDataRepository.GetUserErrorsAsync(conn, memberKey, 150);

                return MemberDataRepository.AnalyzeErrors(userErrors);
            }
        }

        /// <summary>
        /// ✅ TOOL MỚI: Lấy câu sai theo Part
        /// </summary>
        public static async Task<object> GetMyIncorrectQuestionsByPartAsync(string memberKey, int part, int limit = 10)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var mistakes = await MemberDataRepository.GetIncorrectQuestionsByPartAsync(conn, memberKey, part, limit);

                // ✅ HANDLE EMPTY
                if (!mistakes.Any())
                {
                    return new
                    {
                        success = false,
                        message = $"You haven't made any mistakes in Part {part} yet.",
                        suggestions = new[]
                        {
                    $"Try taking a Part {part} practice test",
                    "Your performance in this part might already be excellent!"
                }
                    };
                }

                return mistakes;
            }
        }

        public static async Task<object> GetMyRecentMistakesAsync(string memberKey, int limit = 10)
        {
            const string DOMAIN = "https://localhost:7078"; // ✅ Thêm domain

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                var mistakes = await MemberDataRepository.GetRecentMistakesDetailedAsync(conn, memberKey, limit);

                // ✅ THÊM: Build media URLs cho mỗi mistake
                foreach (var mistake in mistakes)
                {
                    mistake.QuestionImageUrl = BuildMediaUrl(DOMAIN, mistake.QuestionImageUrl);
                    mistake.QuestionAudioUrl = BuildMediaUrl(DOMAIN, mistake.QuestionAudioUrl);
                    mistake.ParentAudioUrl = BuildMediaUrl(DOMAIN, mistake.ParentAudioUrl);
                }

                return mistakes;
            }
        }

        // ✅ THÊM helper method
        private static string BuildMediaUrl(string domain, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return "";

            if (!relativePath.StartsWith("/"))
                relativePath = "/" + relativePath;

            return domain + relativePath;
        }

        /// <summary>
        /// ✅ TOOL 4: Lấy phân tích hành vi làm bài
        /// </summary>
        public static async Task<object> GetMyBehaviorAnalysisAsync(string memberKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                var allResults = await MemberDataRepository.GetAllResultsForMemberAsync(conn, memberKey);
                var recentResultKeys = allResults.Select(r => r.ResultKey).Take(10).ToList();
                var userAnswers = await MemberDataRepository.GetUserAnswersByResultKeysAsync(conn, recentResultKeys);
                return MemberDataRepository.AnalyzeBehavior(userAnswers);
            }
        }
        private static string InterpretDiscrimination(double avgDiscrimination)
        {
            if (avgDiscrimination >= 1.5)
                return "✅ Errors on highly discriminating questions (good indicators of ability).";
            else if (avgDiscrimination >= 0.8)
                return "⚠️ Errors on moderately discriminating questions.";
            else
                return "❌ Errors on low-discrimination questions (may need review for quality).";
        }

        private static string InterpretGuessing(double avgGuessing)
        {
            if (avgGuessing >= 0.3)
                return "🎲 High guessing parameter detected - questions may be vulnerable to random guessing.";
            else if (avgGuessing >= 0.15)
                return "⚠️ Moderate guessing factor - some strategic guessing might help.";
            else
                return "✅ Low guessing parameter - errors are likely due to knowledge gaps, not luck.";
        }

        private static List<string> GenerateAdvancedSuggestions(
            Dictionary<string, int> errorsByDifficulty,
            double avgDiscrimination,
            double avgGuessing,
            Dictionary<string, int> qualityDistribution)
        {
            var suggestions = new List<string>();

            // 1. Difficulty-based suggestions
            if (errorsByDifficulty["TooEasy"] > errorsByDifficulty["TooHard"])
            {
                suggestions.Add("⚠️ You're making mistakes on questions below your ability level. Focus on reducing careless errors.");
            }
            else if (errorsByDifficulty["TooHard"] > errorsByDifficulty["Appropriate"] * 2)
            {
                suggestions.Add("📚 You're tackling questions above your current level. Consider more practice with intermediate difficulty.");
            }
            else
            {
                suggestions.Add("✅ Your errors are mostly on appropriate-level questions. Keep practicing!");
            }

            // 2. Discrimination-based suggestions
            if (avgDiscrimination < 0.8)
            {
                suggestions.Add("🔍 You're struggling with questions that don't clearly distinguish skill levels. Focus on fundamental concepts.");
            }
            else if (avgDiscrimination >= 1.5)
            {
                suggestions.Add("🎯 You're challenged by highly discriminating questions - these are key to improving your score!");
            }

            // 3. Guessing-based suggestions
            if (avgGuessing >= 0.25)
            {
                suggestions.Add("🎲 Many error questions have high guessing parameters. Learn elimination strategies!");
            }

            // 4. Quality-based suggestions
            if (qualityDistribution.ContainsKey("Poor") && qualityDistribution["Poor"] > 5)
            {
                suggestions.Add("⚠️ Some of your errors are on low-quality questions. Don't be too hard on yourself!");
            }
            else if (qualityDistribution.ContainsKey("Excellent") && qualityDistribution["Excellent"] >= qualityDistribution.Values.Sum() / 2)
            {
                suggestions.Add("💪 You're being challenged by high-quality questions - excellent for real test preparation!");
            }

            return suggestions;
        }
        private static object AnalyzePartPerformance(MemberProfileDto profile)
        {
            var partScores = new Dictionary<int, int?>
    {
        { 1, profile.PracticeScore_Part1 },
        { 2, profile.PracticeScore_Part2 },
        { 3, profile.PracticeScore_Part3 },
        { 4, profile.PracticeScore_Part4 },
        { 5, profile.PracticeScore_Part5 },
        { 6, profile.PracticeScore_Part6 },
        { 7, profile.PracticeScore_Part7 }
    };

            var validScores = partScores.Where(p => p.Value.HasValue).ToList();
            if (!validScores.Any())
            {
                return new { message = "No part-specific practice scores available yet." };
            }

            var avgScore = validScores.Average(p => p.Value!.Value);
            var weakestPart = validScores.OrderBy(p => p.Value).First();
            var strongestPart = validScores.OrderByDescending(p => p.Value).First();

            var listeningAvg = partScores.Where(p => p.Key <= 4 && p.Value.HasValue)
                                          .Average(p => p.Value ?? 0);
            var readingAvg = partScores.Where(p => p.Key >= 5 && p.Value.HasValue)
                                        .Average(p => p.Value ?? 0);

            return new
            {
                partScores = partScores.Where(p => p.Value.HasValue)
                                       .Select(p => new { part = p.Key, score = p.Value }),
                overallAvg = Math.Round(avgScore, 1),
                listeningAvg = Math.Round(listeningAvg, 1),
                readingAvg = Math.Round(readingAvg, 1),
                weakestPart = new { part = weakestPart.Key, score = weakestPart.Value },
                strongestPart = new { part = strongestPart.Key, score = strongestPart.Value },
                recommendations = GeneratePartRecommendations(weakestPart.Key, weakestPart.Value!.Value)
            };
        }
        private static object AnalyzeProgressToTarget(MemberProfileDto profile, List<ResultRow> allResults)
        {
            if (!profile.ScoreTarget.HasValue)
            {
                return new { message = "No target score set yet." };
            }

            var target = profile.ScoreTarget.Value;
            var recentScores = allResults
                .Where(r => r.TestScore.HasValue && r.TotalQuestion >= 100)
                .Take(5)
                .Select(r => r.TestScore!.Value)
                .ToList();

            if (!recentScores.Any())
            {
                return new
                {
                    targetScore = target,
                    message = "Take more full tests to track progress."
                };
            }

            var currentAvg = recentScores.Average();
            var gap = target - currentAvg;
            var progress = Math.Max(0, Math.Min(100, (currentAvg / target) * 100));

            string motivation;
            if (gap <= 0)
                motivation = $"🎉 Congratulations! You've reached your target of {target}!";
            else if (gap <= 50)
                motivation = $"💪 You're {gap:F0} points away from your target. Final push!";
            else if (gap <= 100)
                motivation = $"📈 Gap: {gap:F0} points. Stay focused on weak areas.";
            else
                motivation = $"🎯 Target: {target}. Current: {currentAvg:F0}. Consistent practice is key!";

            return new
            {
                targetScore = target,
                currentAverage = Math.Round(currentAvg, 1),
                gap = Math.Round(gap, 1),
                progressPercent = Math.Round(progress, 1),
                recentScores = recentScores,
                motivation
            };
        }
        private static string GeneratePartRecommendations(int part, int score)
        {
            var partNames = new Dictionary<int, string>
    {
        { 1, "Part 1 (Photos)" }, { 2, "Part 2 (Question-Response)" },
        { 3, "Part 3 (Conversations)" }, { 4, "Part 4 (Short Talks)" },
        { 5, "Part 5 (Incomplete Sentences)" }, { 6, "Part 6 (Text Completion)" },
        { 7, "Part 7 (Reading Comprehension)" }
    };

            if (score < 50)
                return $"🚨 {partNames[part]} needs urgent attention. Consider focused practice sessions.";
            else if (score < 70)
                return $"⚠️ {partNames[part]} requires more practice. Aim for 15-20 questions daily.";
            else
                return $"✅ {partNames[part]} is performing well. Maintain this level.";
        }
    }
}
