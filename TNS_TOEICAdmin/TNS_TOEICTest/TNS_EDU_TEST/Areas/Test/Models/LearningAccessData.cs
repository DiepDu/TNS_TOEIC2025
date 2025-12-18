using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TNS_EDU_TEST.Services;

namespace TNS_EDU_TEST.Areas.Test.Models
{
    public class LearningAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
        private readonly GeminiApiKeyManager _apiKeyManager;
        private readonly string _pythonServiceUrl = "http://localhost:5002/analyze_result";

        public LearningAccessData(
      IConfiguration configuration,
      GeminiApiKeyManager apiKeyManager) // ✅ THÊM PARAMETER
        {
            _apiKeyManager = apiKeyManager; // ✅ ASSIGN
                                            // ❌ BỎ DÒNG NÀY: _geminiApiKey = configuration["GeminiApiKey"];
        }

        // ============================================================
        // 🎯 MAIN ENTRY POINT
        // ============================================================

        /// <summary>
        /// Trigger analysis for a completed test (Full Test or Study Part)
        /// Should be called as BACKGROUND TASK from Result pages
        /// </summary>
        public async Task TriggerAnalysisAsync(string memberKey, string testKey)
        {
            try
            {
                Console.WriteLine($"[LearningAnalysis] ========== START ==========");
                Console.WriteLine($"[LearningAnalysis] MemberKey: {memberKey}");
                Console.WriteLine($"[LearningAnalysis] TestKey: {testKey}");
                Console.WriteLine($"[LearningAnalysis] Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                // 1. Get test information
                var testInfo = await GetTestInfoAsync(testKey);
                if (string.IsNullOrEmpty(testInfo.TestName))  // ✅ ĐÚNG - Kiểm tra TestName thay vì toàn bộ tuple
                {
                    Console.WriteLine($"[LearningAnalysis] ❌ Test not found");
                    return;
                }

                Console.WriteLine($"[LearningAnalysis] TestName: {testInfo.TestName}");
                Console.WriteLine($"[LearningAnalysis] Type: {testInfo.Type}");

                // 2. Get best available Theta (EDU_Member → MemberLearningProfile → Calculate)
                var (theta, source) = await GetBestAvailableThetaAsync(memberKey);
                Console.WriteLine($"[LearningAnalysis] Theta: {theta?.ToString("F2") ?? "NULL"} (Source: {source})");

                // 3. Route to appropriate handler
                if (testInfo.Type == TestType.FullTest)
                {
                    Console.WriteLine($"[LearningAnalysis] 🔥 Processing FULL TEST - 7 Parts");
                    await ProcessFullTestAsync(memberKey, theta);
                }
                else if (testInfo.Type == TestType.StudyPart)
                {
                    Console.WriteLine($"[LearningAnalysis] 🔥 Processing STUDY PART {testInfo.TargetPart}");
                    await ProcessStudyPartAsync(memberKey, testInfo.TargetPart, theta);
                }

                Console.WriteLine($"[LearningAnalysis] ========== COMPLETED ==========");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LearningAnalysis FATAL ERROR]: {ex.Message}");
                Console.WriteLine($"[LearningAnalysis STACK]: {ex.StackTrace}");
            }
        }

        // ============================================================
        // 🧠 CORE LOGIC: THETA MANAGEMENT
        // ============================================================

        /// <summary>
        /// Get best available Theta with priority: EDU_Member > MemberLearningProfile > Calculate
        /// </summary>
        private async Task<(float? theta, string source)> GetBestAvailableThetaAsync(string memberKey)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Priority 1: Check EDU_Member.IrtAbility (MOST RELIABLE)
            string memberSql = "SELECT IrtAbility FROM [dbo].[EDU_Member] WHERE MemberKey = @MemberKey";
            using (var cmd = new SqlCommand(memberSql, conn))
            {
                cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                var result = await cmd.ExecuteScalarAsync();

                if (result != DBNull.Value && result != null)
                {
                    return (Convert.ToSingle(result), "EDU_Member.IrtAbility");
                }
            }

            // Priority 2: Check MemberLearningProfile.AbilityTemporary (TEMPORARY)
            string profileSql = @"
                SELECT TOP 1 AbilityTemporary 
                FROM [dbo].[MemberLearningProfile] 
                WHERE MemberKey = @MemberKey AND AbilityTemporary IS NOT NULL
                ORDER BY LastAnalyzed DESC";

            using (var cmd = new SqlCommand(profileSql, conn))
            {
                cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                var result = await cmd.ExecuteScalarAsync();

                if (result != DBNull.Value && result != null)
                {
                    return (Convert.ToSingle(result), "MemberLearningProfile.AbilityTemporary");
                }
            }

            // Priority 3: Need to calculate
            return (null, "NEEDS_CALCULATION");
        }

        /// <summary>
        /// Check if Theta recalculation is needed based on current state
        /// </summary>
        private async Task<bool> ShouldRecalculateThetaAsync(string memberKey, int targetPart, float? currentTheta, string thetaSource)  // ✅ THÊM THAM SỐ
        {
            // If EDU_Member has IrtAbility, trust it (no recalculation)
            if (currentTheta.HasValue && thetaSource == "EDU_Member.IrtAbility")  // ✅ DÙNG CACHE
            {
                Console.WriteLine($"[ThetaCheck] Using trusted IrtAbility from EDU_Member");
                return false;
            }

            // Check if current part already has AbilityTemporary
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
        SELECT AbilityTemporary 
        FROM [dbo].[MemberLearningProfile] 
        WHERE MemberKey = @MemberKey AND Part = @Part";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
            cmd.Parameters.AddWithValue("@Part", targetPart);

            var result = await cmd.ExecuteScalarAsync();

            if (result != DBNull.Value && result != null)
            {
                Console.WriteLine($"[ThetaCheck] Part {targetPart} already has AbilityTemporary");
                return false; // Already analyzed
            }

            Console.WriteLine($"[ThetaCheck] Part {targetPart} needs new Theta calculation");
            return true; // Need calculation
        }
        // ============================================================
        // 📋 PROCESSING HANDLERS
        // ============================================================

        /// <summary>
        /// Process Full Test - Analyze all 7 parts
        /// </summary>
        private async Task ProcessFullTestAsync(string memberKey, float? currentTheta)
        {
            // Check if we need to recalculate Theta
            bool needsCalculation = !currentTheta.HasValue;

            // If needs calculation, collect data from ALL parts
            float? calculatedTheta = null;
            if (needsCalculation)
            {
                Console.WriteLine($"[FullTest] Calculating Theta from all 7 parts...");
                calculatedTheta = await CalculateThetaFromAllPartsAsync(memberKey);

                if (calculatedTheta.HasValue)
                {
                    Console.WriteLine($"[FullTest] Calculated Theta: {calculatedTheta.Value:F2}");
                    currentTheta = calculatedTheta;
                }
            }

            // Analyze each part
            for (int part = 1; part <= 7; part++)
            {
                await AnalyzeSinglePartAsync(memberKey, part, currentTheta,
                    isFullTest: true,
                    forceUseTheta: calculatedTheta);
                await Task.Delay(25000);
            }

            // If calculated new Theta, update ALL existing records
            if (calculatedTheta.HasValue)
            {
                await UpdateAllProfilesWithThetaAsync(memberKey, calculatedTheta.Value);
            }
        }

        /// <summary>
        /// Process Study Part - Analyze single part
        /// </summary>
        private async Task ProcessStudyPartAsync(string memberKey, int targetPart, float? currentTheta)
        {
            // ✅ LẤY SOURCE 1 LẦN DUY NHẤT
            var (_, source) = await GetBestAvailableThetaAsync(memberKey);

            // ✅ TRUYỀN SOURCE VÀO (tránh gọi DB lại)
            bool needsRecalculation = await ShouldRecalculateThetaAsync(memberKey, targetPart, currentTheta, source);

            float? calculatedTheta = null;
            if (needsRecalculation)
            {
                Console.WriteLine($"[StudyPart] Recalculating Theta from available parts...");
                calculatedTheta = await CalculateThetaFromAllPartsAsync(memberKey);

                if (calculatedTheta.HasValue)
                {
                    Console.WriteLine($"[StudyPart] Calculated Theta: {calculatedTheta.Value:F2}");
                    currentTheta = calculatedTheta;
                }
            }

            // Analyze target part
            await AnalyzeSinglePartAsync(memberKey, targetPart, currentTheta,
                isFullTest: false,
                forceUseTheta: calculatedTheta);

            // Update all profiles if calculated new Theta
            if (calculatedTheta.HasValue)
            {
                await UpdateAllProfilesWithThetaAsync(memberKey, calculatedTheta.Value);
            }
        }

        // ============================================================
        // 🧮 THETA CALCULATION
        // ============================================================

        /// <summary>
        /// Calculate Theta from all available parts (25+ responses per part)
        /// </summary>
        private async Task<float?> CalculateThetaFromAllPartsAsync(string memberKey)
        {
            var allData = new List<object>();
            var partThresholds = new Dictionary<int, int>
            {
                {1, 18}, {2, 25}, {3, 25}, {4, 25}, {5, 25}, {6, 32}, {7, 25}
            };

            foreach (var (part, threshold) in partThresholds)
            {
                var partData = await GetUserHistoryDataAsync(memberKey, part, 150);
                if (partData.Count >= threshold)
                {
                    allData.AddRange(partData);
                    Console.WriteLine($"[ThetaCalc] Part {part}: {partData.Count} responses");
                }
            }

            if (allData.Count < 50)
            {
                Console.WriteLine($"[ThetaCalc] Insufficient total data: {allData.Count}/50");
                return null;
            }

            Console.WriteLine($"[ThetaCalc] Total data collected: {allData.Count} responses");

            // Call Python to calculate Theta
            var pythonResult = await CallPythonAnalysisAsync(0.0f, allData, calculateAbility: true);

            return pythonResult?.NewTheta;
        }

        /// <summary>
        /// Update AbilityTemporary in ALL existing MemberLearningProfile records
        /// </summary>
        private async Task UpdateAllProfilesWithThetaAsync(string memberKey, float newTheta)
        {
            string sql = @"
                UPDATE [dbo].[MemberLearningProfile]
                SET AbilityTemporary = @NewTheta
                WHERE MemberKey = @MemberKey";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@NewTheta", newTheta);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            Console.WriteLine($"[ThetaUpdate] Updated {rowsAffected} profile records with new Theta: {newTheta:F2}");
        }

        // ============================================================
        // 🔬 SINGLE PART ANALYSIS
        // ============================================================

        /// <summary>
        /// Analyze a single part with comprehensive behavior and weakness detection
        /// </summary>
        private async Task AnalyzeSinglePartAsync(string memberKey, int part, float? theta, bool isFullTest, float? forceUseTheta)
        {
            try
            {
                Console.WriteLine($"\n[AnalyzePart{part}] ========== START ==========");

                // A. Check data threshold
                int minThreshold = part switch { 1 => 18, 6 => 32, _ => 25 };
                var historyData = await GetUserHistoryDataAsync(memberKey, part, 150);

                if (historyData.Count < minThreshold)
                {
                    Console.WriteLine($"[AnalyzePart{part}] ⚠️ SKIP - Insufficient data ({historyData.Count}/{minThreshold})");
                    return;
                }

                Console.WriteLine($"[AnalyzePart{part}] ✅ Collected {historyData.Count} responses");

                // B. Determine Theta to use
                float thetaToSend = forceUseTheta ?? theta ?? 0.0f;
                bool needCalculate = !theta.HasValue && !forceUseTheta.HasValue;

                Console.WriteLine($"[AnalyzePart{part}] Theta: {thetaToSend:F2} | Calculate: {needCalculate}");

                // C. Call Python for FULL ANALYSIS
                var pythonResult = await CallPythonAnalysisAsync(thetaToSend, historyData, needCalculate);
                if (pythonResult == null)
                {
                    Console.WriteLine($"[AnalyzePart{part}] ❌ Python service failed");
                    return;
                }

                // D. Log analysis results
                Console.WriteLine($"[AnalyzePart{part}] ✅ Python analysis complete");
                Console.WriteLine($"[AnalyzePart{part}] → Theta: {pythonResult.NewTheta:F2}");
                Console.WriteLine($"[AnalyzePart{part}] → Speed: {pythonResult.BehaviorScores.Speed:F1}");
                Console.WriteLine($"[AnalyzePart{part}] → Decisiveness: {pythonResult.BehaviorScores.Decisiveness:F1}");
                Console.WriteLine($"[AnalyzePart{part}] → Accuracy: {pythonResult.BehaviorScores.Accuracy:F1}");

                if (pythonResult.BehaviorScores.Stamina > 0)
                    Console.WriteLine($"[AnalyzePart{part}] → Stamina: {pythonResult.BehaviorScores.Stamina:F1}");

                // E. Build comprehensive AI prompt
                string prompt = BuildComprehensivePrompt(pythonResult, part, isFullTest);

                // F. Call Gemini AI
                Console.WriteLine($"[AnalyzePart{part}] 🤖 Calling Gemini API...");
                string aiAdvice = await CallGeminiApiAsync(prompt);
                Console.WriteLine($"[AnalyzePart{part}] ✅ Gemini response: {aiAdvice?.Length ?? 0} chars");

                // G. Save to database
                float abilityTemp = forceUseTheta ?? pythonResult.NewTheta;
                await SaveLearningProfileAsync(memberKey, part, pythonResult, aiAdvice, abilityTemp);

                Console.WriteLine($"[AnalyzePart{part}] ✅ Saved to database");
                Console.WriteLine($"[AnalyzePart{part}] ========== COMPLETED ==========");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AnalyzePart{part} ERROR]: {ex.Message}");
                Console.WriteLine($"[AnalyzePart{part} STACK]: {ex.StackTrace}");
            }
        }

        // ============================================================
        // 📝 PROMPT ENGINEERING (COMPREHENSIVE)
        // ============================================================

        /// <summary>
        /// Build comprehensive English prompt with full analysis data
        /// AI will respond in Vietnamese
        /// </summary>
        private string BuildComprehensivePrompt(PythonResponse pyData, int part, bool isFullTest)
        {
            var sb = new StringBuilder();

            string context = isFullTest
                ? $"The student just completed a **FULL 2-HOUR TOEIC TEST**. This is the detailed analysis for **PART {part}**."
                : $"The student just completed a **PART {part} PRACTICE SESSION**.";

            sb.AppendLine("# YOUR ROLE");
            sb.AppendLine("You are **Mr. TOEIC** - An expert AI TOEIC coach specializing in personalized, data-driven analysis.");
            sb.AppendLine();

            sb.AppendLine("# STUDENT DATA");
            sb.AppendLine(context);
            sb.AppendLine();

            // ============================================================
            // PERFORMANCE METRICS
            // ============================================================
            sb.AppendLine("## Behavioral Metrics:");
            sb.AppendLine($"- Accuracy: **{pyData.BehaviorScores.Accuracy:F1}%**");
            sb.AppendLine($"- Speed: {pyData.BehaviorScores.Speed:F1}/100 (Avg: {pyData.BehaviorScores.AvgTime:F1}s/question)");
            sb.AppendLine($"- Decisiveness: {pyData.BehaviorScores.Decisiveness:F1}/100");
            sb.AppendLine($"- IRT Theta: {pyData.NewTheta:F2} (Estimated TOEIC: ~{Math.Round(500 + pyData.NewTheta * 100)})");
            if (pyData.BehaviorScores.Stamina > 0)
                sb.AppendLine($"- Stamina: {pyData.BehaviorScores.Stamina:F1}/100");
            sb.AppendLine();

            // ============================================================
            // WEAKNESSES DATA
            // ============================================================
            sb.AppendLine("## Weakness Report:");
            sb.AppendLine(pyData.WeaknessAnalysis.Summary ?? "No summary available.");
            sb.AppendLine();

            if (pyData.WeaknessAnalysis.TopGrammar?.Count > 0)
            {
                sb.AppendLine("**Grammar Errors:**");
                foreach (var item in pyData.WeaknessAnalysis.TopGrammar.Take(5))
                    sb.AppendLine($"- {item}");
                sb.AppendLine();
            }

            if (pyData.WeaknessAnalysis.TopVocab?.Count > 0)
            {
                sb.AppendLine("**Vocabulary Gaps:**");
                foreach (var item in pyData.WeaknessAnalysis.TopVocab.Take(5))
                    sb.AppendLine($"- {item}");
                sb.AppendLine();
            }

            if (pyData.WeaknessAnalysis.TopErrorTypes?.Count > 0)
            {
                sb.AppendLine("**Error Patterns:**");
                foreach (var item in pyData.WeaknessAnalysis.TopErrorTypes.Take(5))
                    sb.AppendLine($"- {item}");
                sb.AppendLine();
            }

            // ============================================================
            // AI TASK - STRICT REQUIREMENTS
            // ============================================================
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("# YOUR TASK: CREATE PERSONALIZED ACTION PLAN");
            sb.AppendLine();
            sb.AppendLine("Based on the data above, write a **comprehensive coaching report** in **VIETNAMESE** following this EXACT structure:");
            sb.AppendLine();
            sb.AppendLine("```markdown");
            sb.AppendLine("## 🎯 Đánh Giá Tổng Quan");
            sb.AppendLine("[Reference specific numbers: Accuracy %, estimated TOEIC score, time spent]");
            sb.AppendLine("[Identify ROOT CAUSE: Is it lack of vocabulary? Grammar? Test-taking speed? Random guessing?]");
            sb.AppendLine();
            sb.AppendLine("## 📊 Phân Tích Chi Tiết");
            sb.AppendLine("### Điểm Mạnh");
            sb.AppendLine("[List 1-2 strengths based on data. If Accuracy < 20%, say: \"Chưa có điểm mạnh rõ ràng\"]");
            sb.AppendLine();
            sb.AppendLine("### Điểm Yếu Nghiêm Trọng");
            sb.AppendLine("[For EACH weakness from the report above, explain:]");
            sb.AppendLine("1. **[Weakness Name]** ([X] errors)");
            sb.AppendLine("   - **Tại sao sai:** [Root cause analysis]");
            sb.AppendLine("   - **Công thức/Kiến thức cần nhớ:** [Specific formulas/rules]");
            sb.AppendLine("   - **Impact:** [How it affects score]");
            sb.AppendLine();
            sb.AppendLine("## 💡 Kế Hoạch Hành Động (30 Ngày)");
            sb.AppendLine("### Tuần 1: [Focus on CRITICAL weakness]");
            sb.AppendLine("- **Học gì:** [Specific grammar topics/vocab lists from data above]");
            sb.AppendLine("- **Công thức cần thuộc:** [List specific formulas]");
            sb.AppendLine("- **Làm thế nào:**");
            sb.AppendLine("  1. [Concrete exercise 1 with website/app name]");
            sb.AppendLine("  2. [Concrete exercise 2 with specific quantity]");
            sb.AppendLine("  3. [Concrete exercise 3 with time frame]");
            sb.AppendLine("- **Tài liệu:** [Specific resources: websites, apps, YouTube channels]");
            sb.AppendLine();
            sb.AppendLine("### Tuần 2-4: [Progressive plan addressing remaining weaknesses...]");
            sb.AppendLine();
            sb.AppendLine("## 🚀 Chiến Thuật Làm Bài Phần " + part);
            sb.AppendLine("[Part-specific strategies based on time management and error patterns]");
            sb.AppendLine("[Include: pre-listening tips, note-taking methods, elimination strategies]");
            sb.AppendLine();
            sb.AppendLine("## 🎓 Lời Khuyên Cuối");
            sb.AppendLine("[Realistic motivation based on current level. NO generic praise.]");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("# MANDATORY RULES:");
            sb.AppendLine("1. **NO GENERIC ADVICE** - Every recommendation must reference SPECIFIC data points from above");
            sb.AppendLine("2. **ADDRESS ALL WEAKNESSES** - Do not skip any item from the Grammar/Vocab/Error lists");
            sb.AppendLine("3. **PRIORITIZE BY SEVERITY** - Start with items having highest error count (Critical 🔴 → High 🟠 → Medium 🟡)");
            sb.AppendLine("4. **BE SPECIFIC** - Instead of \"study grammar\", write \"Study Present/Past tense (15 exercises on EnglishGrammar.org Section 2.3)\"");
            sb.AppendLine("5. **ACTIONABLE** - Provide concrete exercises with website names, app names, specific quantities (e.g., \"50 flashcards\", \"20 minutes daily\")");
            sb.AppendLine("6. **INCLUDE FORMULAS** - For grammar weaknesses, provide actual formulas (e.g., Present Simple: S + V(s/es))");
            sb.AppendLine();
            sb.AppendLine("# CRITICAL LOGIC:");
            sb.AppendLine("- If **Accuracy < 20%** + **Speed > 80** → Diagnose as: \"Làm bừa/Khoanh đại\" (random guessing). MUST explain this is NOT a speed issue but a knowledge gap.");
            sb.AppendLine("- If **Accuracy < 35%** → Focus 100% on FUNDAMENTALS (vocab + basic grammar), NOT on test-taking strategies or speed.");
            sb.AppendLine("- If **Decisiveness > 70** + **Accuracy < 20** → Diagnose as \"Overconfident without knowledge\". MUST recommend: stop rushing, verify answers.");
            sb.AppendLine();
            sb.AppendLine("# EXAMPLE OF GOOD vs BAD RESPONSE:");
            sb.AppendLine("❌ BAD: \"Bạn cần học ngữ pháp về câu trần thuật và từ vựng văn phòng.\"");
            sb.AppendLine("✅ GOOD: \"**Statements (42 lỗi - 64% tổng số sai):** Bạn nhầm lẫn Present Simple vs Past Simple. Công thức: Present: S + V(s/es), Past: S + V-ed. Làm 15 bài tập tại EnglishGrammar.org → Section 2.3 'Present vs Past'. Thời gian: 30 phút/ngày, 7 ngày.\"");
            sb.AppendLine();
            sb.AppendLine("NOW CREATE THE REPORT. Start directly with diagnosis (NO greetings like 'Chào bạn'!).");

            return sb.ToString();
        }

        // ============================================================
        // 🛠️ DATA ACCESS (SQL)
        // ============================================================

        private async Task<(string TestName, TestType Type, int TargetPart)> GetTestInfoAsync(string testKey)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = "SELECT TestName FROM [dbo].[Test] WHERE TestKey = @TestKey";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@TestKey", testKey);

            var name = (string)await cmd.ExecuteScalarAsync();

            if (string.IsNullOrEmpty(name)) return (null, TestType.Unknown, 0);

            if (name.Contains("Full Test", StringComparison.OrdinalIgnoreCase))
                return (name, TestType.FullTest, 0);

            var match = Regex.Match(name, @"Part\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int part))
                return (name, TestType.StudyPart, part);

            return (null, TestType.Unknown, 0);
        }

        private async Task<List<object>> GetUserHistoryDataAsync(string memberKey, int part, int limit)
        {
            var data = new List<object>();

            // ✅ FIXED: Thêm INNER JOIN với ResultOfUserForTest để filter theo MemberKey
            string query = @"
        SELECT TOP (@Limit)
            UA.IsCorrect, 
            UA.TimeSpent, 
            UA.NumberOfAnswerChanges, 
            UA.AnswerTime,
            COALESCE(Q1.IrtDifficulty, Q2.IrtDifficulty, Q3.IrtDifficulty, Q4.IrtDifficulty, 
                     Q5.IrtDifficulty, Q6.IrtDifficulty, Q7.IrtDifficulty, 0) as Difficulty,
            GT.TopicName AS GrammarName,
            VT.TopicName AS VocabName,
            ET.ErrorDescription AS ErrorType,
            CAT.CategoryName AS CategoryName,
            UA.Part AS PartNumber
        FROM [dbo].[UserAnswers] UA
        INNER JOIN [dbo].[ResultOfUserForTest] R ON UA.ResultKey = R.ResultKey  -- ✅ THÊM DÒNG NÀY
        LEFT JOIN [dbo].[UsersError] UE ON UA.SelectAnswerKey = UE.AnswerKey
        LEFT JOIN [dbo].[GrammarTopics] GT ON UE.GrammarTopic = GT.GrammarTopicID
        LEFT JOIN [dbo].[VocabularyTopics] VT ON UE.VocabularyTopic = VT.VocabularyTopicID
        LEFT JOIN [dbo].[ErrorTypes] ET ON UE.ErrorType = ET.ErrorTypeID
        LEFT JOIN [dbo].[TEC_Category] CAT ON UE.CategoryTopic = CAT.CategoryKey
        LEFT JOIN [dbo].[TEC_Part1_Question] Q1 ON UA.QuestionKey = Q1.QuestionKey AND UA.Part = 1
        LEFT JOIN [dbo].[TEC_Part2_Question] Q2 ON UA.QuestionKey = Q2.QuestionKey AND UA.Part = 2
        LEFT JOIN [dbo].[TEC_Part3_Question] Q3 ON UA.QuestionKey = Q3.QuestionKey AND UA.Part = 3
        LEFT JOIN [dbo].[TEC_Part4_Question] Q4 ON UA.QuestionKey = Q4.QuestionKey AND UA.Part = 4
        LEFT JOIN [dbo].[TEC_Part5_Question] Q5 ON UA.QuestionKey = Q5.QuestionKey AND UA.Part = 5
        LEFT JOIN [dbo].[TEC_Part6_Question] Q6 ON UA.QuestionKey = Q6.QuestionKey AND UA.Part = 6
        LEFT JOIN [dbo].[TEC_Part7_Question] Q7 ON UA.QuestionKey = Q7.QuestionKey AND UA.Part = 7
        WHERE R.MemberKey = @MemberKey  -- ✅ SỬA: UserKey → MemberKey (qua bảng ResultOfUserForTest)
          AND UA.Part = @Part
          AND UA.RecordStatus != 99
        ORDER BY UA.AnswerTime DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    cmd.Parameters.AddWithValue("@Part", part);
                    cmd.Parameters.AddWithValue("@Limit", limit);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            data.Add(new
                            {
                                isCorrect = reader["IsCorrect"] != DBNull.Value && Convert.ToBoolean(reader["IsCorrect"]) ? 1 : 0,
                                timeSpent = reader["TimeSpent"] != DBNull.Value ? Convert.ToInt32(reader["TimeSpent"]) : 0,
                                numberOfAnswerChanges = reader["NumberOfAnswerChanges"] != DBNull.Value ? Convert.ToInt32(reader["NumberOfAnswerChanges"]) : 0,
                                difficulty = reader["Difficulty"] != DBNull.Value ? Convert.ToDouble(reader["Difficulty"]) : 0.0,
                                grammarName = reader["GrammarName"]?.ToString(),
                                vocabName = reader["VocabName"]?.ToString(),
                                errorType = reader["ErrorType"]?.ToString(),
                                categoryName = reader["CategoryName"]?.ToString(),
                                part = reader["PartNumber"] != DBNull.Value ? Convert.ToInt32(reader["PartNumber"]) : 0
                            });
                        }
                    }
                }
            }

            return data;
        }

        private async Task SaveLearningProfileAsync(string memberKey, int part, PythonResponse pyRes, string aiAdvice, float abilityTemp)
        {
            string deleteSql = "DELETE FROM [dbo].[MemberLearningProfile] WHERE MemberKey = @MemberKey AND Part = @Part";
            string insertSql = @"
                INSERT INTO [dbo].[MemberLearningProfile]
                (MemberKey, Part, SpeedScore, DecisivenessScore, AccuracyScore, AvgTimeSpent, 
                 WeakTopicsJSON, Advice, AbilityTemporary, LastAnalyzed)
                VALUES
                (@MemberKey, @Part, @Speed, @Decisiveness, @Accuracy, @AvgTime, 
                 @WeakTopics, @Advice, @AbilityTemp, GETDATE())";

            var weakTopics = new
            {
                grammar = pyRes.WeaknessAnalysis.TopGrammar ?? new List<string>(),
                vocab = pyRes.WeaknessAnalysis.TopVocab ?? new List<string>(),
                categories = pyRes.WeaknessAnalysis.TopCategories ?? new List<string>(),
                errors = pyRes.WeaknessAnalysis.TopErrorTypes ?? new List<string>(),
                summary = pyRes.WeaknessAnalysis.Summary,
                behavioralPattern = pyRes.BehavioralPatterns?.LearnerProfile,
                actionableRecommendations = pyRes.ActionableRecommendations
            };

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (var cmd = new SqlCommand(deleteSql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    cmd.Parameters.AddWithValue("@Part", part);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = new SqlCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                    cmd.Parameters.AddWithValue("@Part", part);
                    cmd.Parameters.AddWithValue("@Speed", pyRes.BehaviorScores.Speed);
                    cmd.Parameters.AddWithValue("@Decisiveness", pyRes.BehaviorScores.Decisiveness);
                    cmd.Parameters.AddWithValue("@Accuracy", pyRes.BehaviorScores.Accuracy);
                    cmd.Parameters.AddWithValue("@AvgTime", pyRes.BehaviorScores.AvgTime);
                    cmd.Parameters.AddWithValue("@WeakTopics", JsonConvert.SerializeObject(weakTopics));
                    cmd.Parameters.AddWithValue("@Advice", aiAdvice ?? "Chưa có lời khuyên");
                    cmd.Parameters.AddWithValue("@AbilityTemp", abilityTemp);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Call Python service with retry logic
        /// </summary>
        private async Task<PythonResponse> CallPythonAnalysisAsync(float currentTheta, List<object> history, bool calculateAbility, int maxRetries = 3)  // ✅ THÊM RETRY
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                using (var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(60) })
                {
                    var payload = new
                    {
                        current_theta = currentTheta,
                        responses = history,
                        calculate_ability = calculateAbility
                    };

                    try
                    {
                        var json = JsonConvert.SerializeObject(payload);
                        Console.WriteLine($"[Python] Attempt {attempt}/{maxRetries}: Sending {history.Count} responses, theta={currentTheta:F2}");

                        var response = await client.PostAsync(_pythonServiceUrl,
                            new StringContent(json, Encoding.UTF8, "application/json"));

                        if (response.IsSuccessStatusCode)
                        {
                            var resultJson = await response.Content.ReadAsStringAsync();
                            var result = JsonConvert.DeserializeObject<PythonResponse>(resultJson);
                            Console.WriteLine($"[Python] ✅ Success - Theta: {result.NewTheta:F2}");
                            return result;  // ✅ SUCCESS → RETURN NGAY
                        }
                        else
                        {
                            Console.WriteLine($"[Python] HTTP {response.StatusCode}");
                            var errorContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"[Python] Error: {errorContent}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Python ERROR] Attempt {attempt}/{maxRetries}: {ex.Message}");
                    }

                    // ✅ NẾU CHƯA HẾT RETRY → ĐỢI RỒI THỬ LẠI
                    if (attempt < maxRetries)
                    {
                        int delaySeconds = attempt * 2;  // 2s, 4s, 6s (exponential backoff)
                        Console.WriteLine($"[Python] Retrying in {delaySeconds}s...");
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    }
                }
            }

            // ❌ HẾT RETRY → RETURN NULL
            Console.WriteLine($"[Python] Failed after {maxRetries} attempts");
            return null;
        }
        /// <summary>
        /// Call Gemini API with retry logic
        /// </summary>
        /// <summary>
        /// Call Gemini API with retry logic + KEY ROTATION
        /// </summary>
        /// <summary>
        /// Call Gemini API with retry logic + KEY ROTATION
        /// </summary>
        private async Task<string> CallGeminiApiAsync(string prompt, int maxRetries = 3)
        {
            // ✅ LẤY KEY KHẢ DỤNG TỪ ROTATION SYSTEM
            var keyResult = await _apiKeyManager.GetAvailableApiKeyAsync();

            if (!keyResult.Success || keyResult.ApiKey == null)
            {
                Console.WriteLine($"[Gemini] ❌ No available API key: {keyResult.ErrorMessage}");
                return "⚠️ Hệ thống AI tạm thời quá tải. Vui lòng thử lại sau.";
            }

            var selectedKey = keyResult.ApiKey;
            var apiKey = selectedKey.ActualApiKey;

            Console.WriteLine($"[Gemini] Using {selectedKey.KeyName} (KeyID: {selectedKey.KeyID}) - Remaining: {selectedKey.RemainingQuota}/{selectedKey.DailyLimit}");

            // ✅ GỌI API VỚI RETRY + LOGGING
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Console.WriteLine($"[Gemini] Attempt {attempt}/{maxRetries}: Sending {prompt.Length} chars");

                    var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

                    using (var client = new HttpClient() { Timeout = TimeSpan.FromMinutes(3) })
                    {
                        var payload = new
                        {
                            contents = new[]
                            {
                        new
                        {
                            role = "user",
                            parts = new[] { new { text = prompt } }
                        }
                    },
                            generationConfig = new
                            {
                                temperature = 0.7,
                                maxOutputTokens = 16384,
                                topP = 0.95,
                                topK = 40
                            }
                        };

                        var jsonPayload = JsonConvert.SerializeObject(payload);
                        var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                        // ✅ LOG REQUEST PAYLOAD (CHỈ 500 KÝ TỰ ĐẦU)
                        Console.WriteLine($"[Gemini] 📤 Request Payload Preview: {jsonPayload.Substring(0, Math.Min(500, jsonPayload.Length))}...");

                        var httpResponse = await client.PostAsync(apiUrl, httpContent);

                        // ✅ LOG HTTP STATUS CODE
                        Console.WriteLine($"[Gemini] 📥 HTTP Status: {(int)httpResponse.StatusCode} {httpResponse.StatusCode}");

                        if (httpResponse.IsSuccessStatusCode)
                        {
                            var jsonResponse = await httpResponse.Content.ReadAsStringAsync();

                            // ✅ LOG FULL RESPONSE (QUAN TRỌNG!)
                            Console.WriteLine($"[Gemini] 📥 Full Response JSON:");
                            Console.WriteLine($"{jsonResponse}");
                            Console.WriteLine($"[Gemini] ==================== END RESPONSE ====================");

                            var responseObj = JObject.Parse(jsonResponse);

                            var advice = responseObj["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                            if (!string.IsNullOrEmpty(advice))
                            {
                                // ✅ GHI LOG THÀNH CÔNG
                                await _apiKeyManager.LogApiCallAttemptAsync(selectedKey.KeyID, isSuccess: true);
                                Console.WriteLine($"[Gemini] ✅ Success: {advice.Length} chars (KeyID: {selectedKey.KeyID})");
                                return advice;
                            }
                            else
                            {
                                // ✅ LOG TRƯỜNG HỢP RESPONSE HỢP LỆ NHƯNG KHÔNG CÓ TEXT
                                Console.WriteLine($"[Gemini] ⚠️ Valid response but no text found");
                                Console.WriteLine($"[Gemini] Response structure: {responseObj.ToString()}");
                            }
                        }
                        else
                        {
                            // ✅ GHI LOG THẤT BẠI
                            await _apiKeyManager.LogApiCallAttemptAsync(selectedKey.KeyID, isSuccess: false);

                            var errorContent = await httpResponse.Content.ReadAsStringAsync();

                            // ✅ LOG FULL ERROR RESPONSE
                            Console.WriteLine($"[Gemini] ❌ Error Response:");
                            Console.WriteLine($"[Gemini] Status Code: {httpResponse.StatusCode}");
                            Console.WriteLine($"[Gemini] Error Content: {errorContent}");
                            Console.WriteLine($"[Gemini] ==================== END ERROR ====================");

                            // ✅ PARSE ERROR JSON ĐỂ XEM CHI TIẾT
                            try
                            {
                                var errorObj = JObject.Parse(errorContent);
                                var errorMessage = errorObj["error"]?["message"]?.ToString();
                                var errorCode = errorObj["error"]?["code"]?.ToString();
                                var errorStatus = errorObj["error"]?["status"]?.ToString();

                                Console.WriteLine($"[Gemini] Parsed Error:");
                                Console.WriteLine($"  - Code: {errorCode}");
                                Console.WriteLine($"  - Status: {errorStatus}");
                                Console.WriteLine($"  - Message: {errorMessage}");
                            }
                            catch
                            {
                                Console.WriteLine($"[Gemini] Could not parse error JSON");
                            }

                            // ✅ NẾU LỖI 429 (QUOTA) → KHÔNG RETRY
                            if (httpResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                Console.WriteLine($"[Gemini] ⚠️ Key {selectedKey.KeyName} exhausted (429 Too Many Requests)");
                                // System sẽ tự động chọn key khác ở lần gọi tiếp theo
                                break; // Thoát retry loop
                            }
                        }
                    }
                }
                catch (TaskCanceledException ex)
                {
                    // ✅ TIMEOUT ERROR
                    await _apiKeyManager.LogApiCallAttemptAsync(selectedKey.KeyID, isSuccess: false);
                    Console.WriteLine($"[Gemini TIMEOUT] Attempt {attempt}/{maxRetries}:");
                    Console.WriteLine($"  - Message: {ex.Message}");
                    Console.WriteLine($"  - Timeout exceeded 3 minutes");
                }
                catch (HttpRequestException ex)
                {
                    // ✅ NETWORK ERROR
                    await _apiKeyManager.LogApiCallAttemptAsync(selectedKey.KeyID, isSuccess: false);
                    Console.WriteLine($"[Gemini NETWORK ERROR] Attempt {attempt}/{maxRetries}:");
                    Console.WriteLine($"  - Message: {ex.Message}");
                    Console.WriteLine($"  - InnerException: {ex.InnerException?.Message}");
                }
                catch (Exception ex)
                {
                    // ✅ OTHER ERRORS
                    await _apiKeyManager.LogApiCallAttemptAsync(selectedKey.KeyID, isSuccess: false);
                    Console.WriteLine($"[Gemini ERROR] Attempt {attempt}/{maxRetries}:");
                    Console.WriteLine($"  - Type: {ex.GetType().Name}");
                    Console.WriteLine($"  - Message: {ex.Message}");
                    Console.WriteLine($"  - StackTrace: {ex.StackTrace}");
                }

                // ✅ RETRY VỚI EXPONENTIAL BACKOFF
                if (attempt < maxRetries)
                {
                    int delaySeconds = attempt * 3; // 3s, 6s, 9s
                    Console.WriteLine($"[Gemini] Retrying in {delaySeconds}s...");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }

            // ❌ HẾT RETRY → FALLBACK
            Console.WriteLine($"[Gemini] ❌ Failed after {maxRetries} attempts");
            return "Phân tích hoàn tất. Hãy tiếp tục luyện tập để cải thiện kỹ năng.";
        }

        // ============================================================
        // 📊 DATA TRANSFER OBJECTS
        // ============================================================

        private enum TestType { Unknown, FullTest, StudyPart }

        public class PythonResponse
        {
            [JsonProperty("new_theta")]
            public float NewTheta { get; set; }

            [JsonProperty("behavior_scores")]
            public BehaviorScoreObj BehaviorScores { get; set; }

            [JsonProperty("weakness_analysis")]
            public WeaknessObj WeaknessAnalysis { get; set; }

            [JsonProperty("behavioral_patterns")]
            public BehavioralPatternsObj BehavioralPatterns { get; set; }

            [JsonProperty("skill_level_analysis")]
            public SkillLevelAnalysisObj SkillLevelAnalysis { get; set; }

            [JsonProperty("part_specific_insights")]
            public Dictionary<string, PartInsightObj> PartSpecificInsights { get; set; }

            [JsonProperty("actionable_recommendations")]
            public List<string> ActionableRecommendations { get; set; }
        }

        public class BehaviorScoreObj
        {
            [JsonProperty("speed")]
            public float Speed { get; set; }

            [JsonProperty("decisiveness")]
            public float Decisiveness { get; set; }

            [JsonProperty("accuracy")]
            public float Accuracy { get; set; }

            [JsonProperty("avg_time")]
            public float AvgTime { get; set; }

            [JsonProperty("stamina")]
            public float Stamina { get; set; }
        }

        public class WeaknessObj
        {
            [JsonProperty("top_grammar")]
            public List<string> TopGrammar { get; set; }

            [JsonProperty("top_vocab")]
            public List<string> TopVocab { get; set; }

            [JsonProperty("top_error_types")]
            public List<string> TopErrorTypes { get; set; }

            [JsonProperty("top_categories")]
            public List<string> TopCategories { get; set; }

            [JsonProperty("summary")]
            public string Summary { get; set; }
        }

        public class BehavioralPatternsObj
        {
            [JsonProperty("change_pattern")]
            public string ChangePattern { get; set; }

            [JsonProperty("first_answer_accuracy")]
            public float FirstAnswerAccuracy { get; set; }

            [JsonProperty("changed_answer_accuracy")]
            public float ChangedAnswerAccuracy { get; set; }

            [JsonProperty("answer_change_impact")]
            public string AnswerChangeImpact { get; set; }

            [JsonProperty("learner_profile")]
            public string LearnerProfile { get; set; }

            [JsonProperty("time_management")]
            public string TimeManagement { get; set; }
        }

        public class SkillLevelAnalysisObj
        {
            [JsonProperty("comfort_zone")]
            public string ComfortZone { get; set; }

            [JsonProperty("challenge_level")]
            public string ChallengeLevel { get; set; }
        }

        public class PartInsightObj
        {
            [JsonProperty("strength")]
            public string Strength { get; set; }

            [JsonProperty("accuracy")]
            public float Accuracy { get; set; }

            [JsonProperty("avg_time")]
            public float AvgTime { get; set; }

            [JsonProperty("weak_areas")]
            public List<string> WeakAreas { get; set; }

            [JsonProperty("advice")]
            public string Advice { get; set; }
        }
    }
}
