using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.RegularExpressions;

namespace TNS_TOEICTest.Models.ChatWithAI.Services
{
    public class AdminDataService
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;
        public static async Task<string> LoadAdminOriginalDataAsync(string adminKey)
        {
            var contextBuilder = new StringBuilder();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                contextBuilder.AppendLine("--- Admin Profile & Department ---");
                var adminQuery = @"
            SELECT 
                u.UserName, u.LastLoginDate,
                e.FirstName, e.LastName, e.CompanyEmail,
                d.DepartmentName,
                e.PositionName
            FROM SYS_Users u
            LEFT JOIN HRM_Employee e ON u.EmployeeKey = e.EmployeeKey
            LEFT JOIN HRM_Department d ON e.DepartmentKey = d.DepartmentKey
            WHERE u.UserKey = @AdminKey;";

                using (var command = new SqlCommand(adminQuery, connection))
                {
                    command.Parameters.AddWithValue("@AdminKey", adminKey);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            contextBuilder.AppendLine($"Name: {reader["FirstName"]} {reader["LastName"]}");
                            contextBuilder.AppendLine($"Username: {reader["UserName"]}");
                            contextBuilder.AppendLine($"Position: {reader["PositionName"]}, Department: {reader["DepartmentName"]}");
                            contextBuilder.AppendLine($"Company Email: {reader["CompanyEmail"]}");
                            contextBuilder.AppendLine($"Last Login: {reader["LastLoginDate"]}");
                        }
                        else
                        {
                            contextBuilder.AppendLine("Admin information not found.");
                        }
                    }
                }
                contextBuilder.AppendLine();

                // === PHẦN 2: LẤY QUYỀN HẠN (ROLES) CỦA ADMIN ===
                contextBuilder.AppendLine("--- Admin Permissions ---");
                var rolesQuery = @"
            SELECT 
                r.RoleName, ur.RoleRead, ur.RoleEdit, ur.RoleAdd, ur.RoleDel, ur.RoleApproval
            FROM SYS_Users_Roles ur
            JOIN SYS_Roles r ON ur.RoleKey = r.RoleKey
            WHERE ur.UserKey = @AdminKey;";

                using (var command = new SqlCommand(rolesQuery, connection))
                {
                    command.Parameters.AddWithValue("@AdminKey", adminKey);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var permissions = new List<string>();
                            if ((bool)reader["RoleRead"]) permissions.Add("Read");
                            if ((bool)reader["RoleEdit"]) permissions.Add("Edit");
                            if ((bool)reader["RoleAdd"]) permissions.Add("Add");
                            if ((bool)reader["RoleDel"]) permissions.Add("Delete");
                            if ((bool)reader["RoleApproval"]) permissions.Add("Approval");
                            contextBuilder.AppendLine($"- Role: {reader["RoleName"]}, Permissions: [{string.Join(", ", permissions)}]");
                        }
                    }
                }
                contextBuilder.AppendLine();

                var toeicConfigQuery = "SELECT TOP 1 * FROM TOEICConfiguration;";
                using (var command = new SqlCommand(toeicConfigQuery, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            contextBuilder.AppendLine("Number of questions per part:");
                            contextBuilder.AppendLine($"  - Part 1: {reader["NumberOfPart1"]}, Part 2: {reader["NumberOfPart2"]}, Part 3: {reader["NumberOfPart3"]}, Part 4: {reader["NumberOfPart4"]}");
                            contextBuilder.AppendLine($"  - Part 5: {reader["NumberOfPart5"]}, Part 6: {reader["NumberOfPart6"]}, Part 7: {reader["NumberOfPart7"]}");
                            contextBuilder.AppendLine($"Total Duration: {reader["Duration"]} minutes");
                        }
                    }
                }
                contextBuilder.AppendLine();

                // --- Truy vấn bảng SkillLevelDistribution ---
                contextBuilder.AppendLine("Skill Level Distribution (%):");
                var skillDistQuery = "SELECT Part, SkillLevel1, SkillLevel2, SkillLevel3, SkillLevel4, SkillLevel5 FROM SkillLevelDistribution ORDER BY Part;";
                using (var command = new SqlCommand(skillDistQuery, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            contextBuilder.AppendLine(
                                $"  - Part {reader["Part"]}: [" +
                                $"Level 1: {reader["SkillLevel1"]}%, " +
                                $"Level 2: {reader["SkillLevel2"]}%, " +
                                $"Level 3: {reader["SkillLevel3"]}%, " +
                                $"Level 4: {reader["SkillLevel4"]}%, " +
                                $"Level 5: {reader["SkillLevel5"]}%]"
                            );
                        }
                    }
                }
                contextBuilder.AppendLine();
            }
            return contextBuilder.ToString();
        }
        public static async Task<Dictionary<string, object>> GetMemberSummaryAsync(string memberIdentifier)
        {
            var memberInfo = new Dictionary<string, object>();
            string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // === BƯỚC 1: TÌM KIẾM THÀNH VIÊN ===
                string initialQuery;
                var initialCommand = new SqlCommand();
                initialCommand.Connection = connection;

                if (Regex.IsMatch(memberIdentifier, emailPattern, RegexOptions.IgnoreCase))
                {
                    initialQuery = "SELECT * FROM EDU_Member WHERE MemberID = @Identifier;";
                    initialCommand.Parameters.AddWithValue("@Identifier", memberIdentifier);
                }
                else
                {
                    initialQuery = "SELECT TOP 1 * FROM EDU_Member WHERE MemberName COLLATE Vietnamese_CI_AI LIKE @IdentifierPattern COLLATE Vietnamese_CI_AI;";
                    initialCommand.Parameters.AddWithValue("@IdentifierPattern", $"%{memberIdentifier}%");
                }
                initialCommand.CommandText = initialQuery;

                string? memberKey = null;

                using (initialCommand)
                {
                    using (var reader = await initialCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Lấy thông tin cơ bản
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var columnName = reader.GetName(i);
                                var value = reader.GetValue(i);
                                memberInfo[columnName] = value == DBNull.Value ? null : value;
                            }
                            memberKey = memberInfo.ContainsKey("MemberKey") ? memberInfo["MemberKey"].ToString() : null;
                        }
                    }
                }

                // NẾU TÌM THẤY THÀNH VIÊN, TIẾN HÀNH PHÂN TÍCH SÂU
                if (!string.IsNullOrEmpty(memberKey))
                {
                    // === PHẦN 2: PHÂN TÍCH TỔNG QUAN HIỆU SUẤT ===
                    var allResultsQuery = "SELECT TestScore FROM ResultOfUserForTest WHERE MemberKey = @MemberKey AND TestScore IS NOT NULL ORDER BY StartTime ASC;";
                    var allScores = new List<int>();
                    using (var command = new SqlCommand(allResultsQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MemberKey", memberKey);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                allScores.Add(Convert.ToInt32(reader["TestScore"]));
                            }
                        }
                    }

                    var performanceSummary = new Dictionary<string, object>();
                    if (allScores.Count > 0)
                    {
                        performanceSummary["HighestScore"] = allScores.Max();
                        performanceSummary["LowestScore"] = allScores.Min();
                        performanceSummary["AverageScore"] = $"{allScores.Average():F0} (trên {allScores.Count} bài)";

                        if (allScores.Count >= 3)
                        {
                            var firstThreeAvg = allScores.Take(3).Average();
                            var lastThreeAvg = allScores.Skip(allScores.Count - 3).Average();
                            string trend = lastThreeAvg > firstThreeAvg + 10 ? "Clearly Upward" : (lastThreeAvg < firstThreeAvg - 10 ? "Clearly Downward" : "Stable");
                            performanceSummary["LongTermTrend"] = trend;

                            double avg = allScores.Average();
                            double sumOfSquares = allScores.Sum(score => Math.Pow(score - avg, 2));
                            double stdDev = Math.Sqrt(sumOfSquares / allScores.Count);
                            string stability = stdDev < 50 ? "Very Stable" : (stdDev < 100 ? "Relatively Stable" : "Unstable");
                            performanceSummary["PerformanceStability"] = $"{stability} (Std. Dev: {stdDev:F1})";
                            performanceSummary["RecentPerformanceStatus"] = lastThreeAvg > avg ? "Improving" : "Below Average";
                        }
                    }
                    else
                    {
                        performanceSummary["Status"] = "No test results found.";
                    }
                    memberInfo["PerformanceSummary"] = performanceSummary;

                    // === PHẦN 3: PHÂN TÍCH HÀNH VI LÀM BÀI ===
                    var behaviorSummary = new Dictionary<string, object>();
                    var behaviorQuery = @"
                SELECT AVG(CAST(ua.TimeSpent AS FLOAT)) AS AvgTime, AVG(CAST(ua.NumberOfAnswerChanges AS FLOAT)) AS AvgChanges
                FROM UserAnswers ua
                WHERE ua.ResultKey IN (SELECT TOP 10 ResultKey FROM ResultOfUserForTest WHERE MemberKey = @MemberKey ORDER BY StartTime DESC);";
                    using (var command = new SqlCommand(behaviorQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MemberKey", memberKey);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync() && reader["AvgTime"] != DBNull.Value)
                            {
                                behaviorSummary["AvgTimePerQuestion_Last10Tests"] = $"{Convert.ToDouble(reader["AvgTime"]):F1} seconds";
                                behaviorSummary["AvgAnswerChanges_Last10Tests"] = $"{Convert.ToDouble(reader["AvgChanges"]):F2}";
                            }
                        }
                    }

                    var completionTimeQuery = @"
                SELECT TOP 5 R.[Time] FROM ResultOfUserForTest R
                JOIN Test T ON R.TestKey = T.TestKey
                WHERE R.MemberKey = @MemberKey AND T.TotalQuestion >= 100 AND R.[Time] IS NOT NULL ORDER BY R.StartTime DESC;";
                    var completionTimesInMinutes = new List<double>();
                    using (var command = new SqlCommand(completionTimeQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MemberKey", memberKey);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                if (double.TryParse(reader["Time"].ToString(), out double time))
                                {
                                    completionTimesInMinutes.Add(time);
                                }
                            }
                        }
                    }

                    if (completionTimesInMinutes.Any())
                    {
                        behaviorSummary["AvgFullTestCompletionTime"] = $"{completionTimesInMinutes.Average():F0} minutes";
                    }
                    memberInfo["BehaviorAnalysis"] = behaviorSummary;

                    // === ✅ PHẦN MỚI: IRT ANALYSIS ===
                    var irtAnalysis = new Dictionary<string, object>();
                    if (memberInfo.ContainsKey("IrtAbility") && memberInfo["IrtAbility"] != null)
                    {
                        var irtAbility = Convert.ToSingle(memberInfo["IrtAbility"]);
                        irtAnalysis["IrtAbility"] = Math.Round(irtAbility, 2);
                        irtAnalysis["IrtUpdatedOn"] = memberInfo.ContainsKey("IrtUpdatedOn") && memberInfo["IrtUpdatedOn"] != null
                            ? memberInfo["IrtUpdatedOn"]
                            : "Not updated";

                        // So sánh với độ khó câu sai
                        var irtQuery = @"
SELECT AVG(Q.IrtDifficulty) AS AvgErrorDifficulty, 
       COUNT(*) AS ErrorCount
FROM (
    SELECT UA.QuestionKey FROM UserAnswers UA
    JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey
    WHERE R.MemberKey = @MemberKey AND UA.IsCorrect = 0
    ORDER BY UA.AnswerTime DESC
    OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY
) AS RecentErrors
JOIN (
    SELECT QuestionKey, IrtDifficulty FROM TEC_Part1_Question WHERE IrtDifficulty IS NOT NULL
    UNION ALL SELECT QuestionKey, IrtDifficulty FROM TEC_Part2_Question WHERE IrtDifficulty IS NOT NULL
    UNION ALL SELECT QuestionKey, IrtDifficulty FROM TEC_Part3_Question WHERE IrtDifficulty IS NOT NULL
    UNION ALL SELECT QuestionKey, IrtDifficulty FROM TEC_Part4_Question WHERE IrtDifficulty IS NOT NULL
    UNION ALL SELECT QuestionKey, IrtDifficulty FROM TEC_Part5_Question WHERE IrtDifficulty IS NOT NULL
    UNION ALL SELECT QuestionKey, IrtDifficulty FROM TEC_Part6_Question WHERE IrtDifficulty IS NOT NULL
    UNION ALL SELECT QuestionKey, IrtDifficulty FROM TEC_Part7_Question WHERE IrtDifficulty IS NOT NULL
) AS Q ON RecentErrors.QuestionKey = Q.QuestionKey";

                        using (var cmd = new SqlCommand(irtQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync() && reader["ErrorCount"] != DBNull.Value)
                                {
                                    var errorCount = Convert.ToInt32(reader["ErrorCount"]);
                                    if (errorCount > 0)
                                    {
                                        var avgDifficulty = Convert.ToSingle(reader["AvgErrorDifficulty"]);
                                        irtAnalysis["AvgErrorDifficulty"] = Math.Round(avgDifficulty, 2);
                                        irtAnalysis["DifficultyGap"] = Math.Round(avgDifficulty - irtAbility, 2);

                                        // Interpretation
                                        var gap = avgDifficulty - irtAbility;
                                        if (gap > 0.5)
                                            irtAnalysis["Interpretation"] = "⚠️ Student is struggling with questions above their ability level.";
                                        else if (gap < -0.5)
                                            irtAnalysis["Interpretation"] = "⚠️ Student is making careless errors on easy questions.";
                                        else
                                            irtAnalysis["Interpretation"] = "✅ Error difficulty matches ability level - normal learning curve.";
                                    }
                                    else
                                    {
                                        irtAnalysis["AvgErrorDifficulty"] = "N/A (no recent errors)";
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        irtAnalysis["Status"] = "IRT analysis not available (need more test data)";
                    }
                    memberInfo["IrtAnalysis"] = irtAnalysis;

                    // === PHẦN 4: PHÂN TÍCH LỖI SAI CHI TIẾT ===
                    var errorAnalysisBuilder = new StringBuilder();
                    string? latestResultKey = null;
                    int totalQuestions = 0;
                    var latestTestQuery = "SELECT TOP 1 R.ResultKey, T.TotalQuestion FROM ResultOfUserForTest R JOIN Test T ON R.TestKey = T.TestKey WHERE R.MemberKey = @MemberKey ORDER BY R.StartTime DESC;";
                    using (var latestTestCmd = new SqlCommand(latestTestQuery, connection))
                    {
                        latestTestCmd.Parameters.AddWithValue("@MemberKey", memberKey);
                        using (var reader = await latestTestCmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                latestResultKey = reader["ResultKey"].ToString();
                                totalQuestions = reader["TotalQuestion"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TotalQuestion"]);
                            }
                        }
                    }

                    var errorQuery = @"
            WITH AllQuestionsAndAnswers AS (
            SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '1' AS Part FROM TEC_Part1_Question Q JOIN TEC_Part1_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
            SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '2' AS Part FROM TEC_Part2_Question Q JOIN TEC_Part2_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
            SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '3' AS Part FROM TEC_Part3_Question Q JOIN TEC_Part3_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
            SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '4' AS Part FROM TEC_Part4_Question Q JOIN TEC_Part4_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
            SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '5' AS Part FROM TEC_Part5_Question Q JOIN TEC_Part5_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
            SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '6' AS Part FROM TEC_Part6_Question Q JOIN TEC_Part6_Answer A ON Q.QuestionKey = A.QuestionKey UNION ALL
            SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, A.AnswerKey, A.AnswerText, A.AnswerCorrect, '7' AS Part FROM TEC_Part7_Question Q JOIN TEC_Part7_Answer A ON Q.QuestionKey = A.QuestionKey
            )
            SELECT 
                UE.ErrorDate, ET.ErrorDescription, 
                GT.TopicName AS GrammarTopicName, 
                VT.TopicName AS VocabularyTopicName,
                CAT.CategoryName AS CategoryTopicName,
                QuestionInfo.QuestionText, 
                UserSelectedAnswer.AnswerText AS UserAnswer, 
                CorrectAnswer.AnswerText AS CorrectAnswer,
                QuestionInfo.Explanation,
                UA.TimeSpent, UA.NumberOfAnswerChanges,
                QuestionInfo.Part
            FROM UsersError UE
            JOIN UserAnswers UA ON UE.ResultKey = UA.ResultKey AND UE.AnswerKey = UA.SelectAnswerKey
            JOIN (SELECT DISTINCT QuestionKey, QuestionText, Explanation, Part FROM AllQuestionsAndAnswers) AS QuestionInfo ON UA.QuestionKey = QuestionInfo.QuestionKey
            JOIN AllQuestionsAndAnswers AS UserSelectedAnswer ON UA.SelectAnswerKey = UserSelectedAnswer.AnswerKey
            JOIN AllQuestionsAndAnswers AS CorrectAnswer ON UA.QuestionKey = CorrectAnswer.QuestionKey AND CorrectAnswer.AnswerCorrect = 1
            LEFT JOIN ErrorTypes ET ON UE.ErrorType = ET.ErrorTypeID
            LEFT JOIN GrammarTopics GT ON UE.GrammarTopic = GT.GrammarTopicID
            LEFT JOIN VocabularyTopics VT ON UE.VocabularyTopic = VT.VocabularyTopicID
            LEFT JOIN TEC_Category CAT ON UE.CategoryTopic = CAT.CategoryKey
            {WHERE_CLAUSE}
            {ORDER_AND_LIMIT};";

                    var finalErrorQuery = "";
                    var errorCommand = new SqlCommand();
                    errorCommand.Connection = connection;
                    errorCommand.Parameters.AddWithValue("@MemberKeyParam", memberKey);

                    if (!string.IsNullOrEmpty(latestResultKey) && totalQuestions >= 100)
                    {
                        errorAnalysisBuilder.AppendLine($"--- Detailed Error Analysis (From Latest Full Test) ---");
                        finalErrorQuery = errorQuery
                            .Replace("{WHERE_CLAUSE}", "WHERE UE.ResultKey = @ResultKey AND UA.IsCorrect = 0")
                            .Replace("{ORDER_AND_LIMIT}", "ORDER BY UE.ErrorDate DESC");
                        errorCommand.Parameters.AddWithValue("@ResultKey", latestResultKey);
                    }
                    else
                    {
                        errorAnalysisBuilder.AppendLine($"--- Detailed Error Analysis (Last 150 Errors) ---");
                        finalErrorQuery = errorQuery
                            .Replace("{WHERE_CLAUSE}", "WHERE UE.UserKey = @MemberKeyParam AND UA.IsCorrect = 0")
                            .Replace("{ORDER_AND_LIMIT}", "ORDER BY UE.ErrorDate DESC OFFSET 0 ROWS FETCH NEXT 150 ROWS ONLY");
                    }

                    errorCommand.CommandText = finalErrorQuery;
                    using (errorCommand)
                    {
                        using (var reader = await errorCommand.ExecuteReaderAsync())
                        {
                            int errorCount = 1;
                            while (await reader.ReadAsync())
                            {
                                errorAnalysisBuilder.AppendLine($"[Error #{errorCount++} - Part {reader["Part"]}]");
                                errorAnalysisBuilder.AppendLine($"  - Question: {reader["QuestionText"]}");
                                errorAnalysisBuilder.AppendLine($"  - Your Answer: '{reader["UserAnswer"]}'");
                                errorAnalysisBuilder.AppendLine($"  - Correct Answer: '{reader["CorrectAnswer"]}'");
                                errorAnalysisBuilder.AppendLine($"  - Error Type: {reader["ErrorDescription"]}");
                                errorAnalysisBuilder.AppendLine($"  - Topics: Category '{reader["CategoryTopicName"]}', Grammar '{reader["GrammarTopicName"]}', Vocabulary '{reader["VocabularyTopicName"]}'");
                                errorAnalysisBuilder.AppendLine($"  - Behavior: Time spent was {reader["TimeSpent"]}s, changed answer {reader["NumberOfAnswerChanges"]} times.");
                                errorAnalysisBuilder.AppendLine($"  - Explanation: {reader["Explanation"]}");
                            }
                            if (errorCount == 1)
                            {
                                errorAnalysisBuilder.AppendLine("No specific errors found to analyze.");
                            }
                        }
                    }
                    memberInfo["DetailedErrorAnalysis"] = errorAnalysisBuilder.ToString();
                }
            }
            return memberInfo;
        }
        public static async Task<Dictionary<string, object>> GetQuestionCountsAsync()
        {
            var counts = new Dictionary<string, object>();

            // Giả định rằng câu hỏi cha có cột 'Parent' là NULL hoặc 0.
            // Bạn có thể cần điều chỉnh điều kiện này cho đúng với cấu trúc DB của mình.
            string query = @"
        SELECT 'Part1' AS Part, COUNT(*) AS QuestionCount FROM TEC_Part1_Question UNION ALL
        SELECT 'Part2' AS Part, COUNT(*) AS QuestionCount FROM TEC_Part2_Question UNION ALL
        SELECT 'Part5' AS Part, COUNT(*) AS QuestionCount FROM TEC_Part5_Question UNION ALL

        -- Đếm câu hỏi cha (Passages) cho Part 3
        SELECT 'Part3_Passages' AS Part, COUNT(*) FROM TEC_Part3_Question WHERE Parent IS NULL UNION ALL
        -- Đếm câu hỏi con cho Part 3
        SELECT 'Part3_Questions' AS Part, COUNT(*) FROM TEC_Part3_Question WHERE Parent IS NOT NULL UNION ALL

        -- Đếm câu hỏi cha (Passages) cho Part 4
        SELECT 'Part4_Passages' AS Part, COUNT(*) FROM TEC_Part4_Question WHERE Parent IS NULL UNION ALL
        -- Đếm câu hỏi con cho Part 4
        SELECT 'Part4_Questions' AS Part, COUNT(*) FROM TEC_Part4_Question WHERE Parent IS NOT NULL UNION ALL

        -- Đếm câu hỏi cha (Passages) cho Part 6
        SELECT 'Part6_Passages' AS Part, COUNT(*) FROM TEC_Part6_Question WHERE Parent IS NULL UNION ALL
        -- Đếm câu hỏi con cho Part 6
        SELECT 'Part6_Questions' AS Part, COUNT(*) FROM TEC_Part6_Question WHERE Parent IS NOT NULL UNION ALL

        -- Đếm câu hỏi cha (Passages) cho Part 7
        SELECT 'Part7_Passages' AS Part, COUNT(*) FROM TEC_Part7_Question WHERE Parent IS NULL UNION ALL
        -- Đếm câu hỏi con cho Part 7
        SELECT 'Part7_Questions' AS Part, COUNT(*) FROM TEC_Part7_Question WHERE Parent IS NOT NULL;
    ";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(query, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            counts[reader.GetString(0)] = reader.GetInt32(1);
                        }
                    }
                }
            }

            // Tính toán tổng số câu hỏi thực tế (không tính các passages)
            int totalQuestions = 0;
            totalQuestions += counts.ContainsKey("Part1") ? (int)counts["Part1"] : 0;
            totalQuestions += counts.ContainsKey("Part2") ? (int)counts["Part2"] : 0;
            totalQuestions += counts.ContainsKey("Part5") ? (int)counts["Part5"] : 0;
            totalQuestions += counts.ContainsKey("Part3_Questions") ? (int)counts["Part3_Questions"] : 0;
            totalQuestions += counts.ContainsKey("Part4_Questions") ? (int)counts["Part4_Questions"] : 0;
            totalQuestions += counts.ContainsKey("Part6_Questions") ? (int)counts["Part6_Questions"] : 0;
            totalQuestions += counts.ContainsKey("Part7_Questions") ? (int)counts["Part7_Questions"] : 0;

            counts["Total_Actual_Questions"] = totalQuestions;

            return counts;
        }
        public static async Task<List<Dictionary<string, object>>> FindMembersByCriteriaAsync(
 string score_condition = null,
 string last_login_before = null,
 int? min_tests_completed = null,
 string sort_by = "LastLoginDate",
 int limit = 10)
        {
            var members = new List<Dictionary<string, object>>();
            var queryBuilder = new StringBuilder();
            var parameters = new Dictionary<string, object>();

            // Sử dụng CTE để tổng hợp dữ liệu trước khi lọc và sắp xếp
            queryBuilder.Append(@"
;WITH MemberStats AS (
    SELECT
        M.MemberKey,
        M.MemberName,
        M.MemberID,
        M.LastLoginDate,
        COUNT(R.ResultKey) AS TestCount,
        MAX(R.TestScore) AS HighestScore,
        AVG(CAST(R.TestScore AS FLOAT)) AS AverageScore -- Đảm bảo tính toán AVG trên số thực
    FROM EDU_Member M
    LEFT JOIN ResultOfUserForTest R ON M.MemberKey = R.MemberKey
    GROUP BY M.MemberKey, M.MemberName, M.MemberID, M.LastLoginDate
)
SELECT * FROM MemberStats
WHERE 1=1 ");

            // Xây dựng mệnh đề WHERE động
            if (!string.IsNullOrEmpty(score_condition))
            {
                // Phân tích điều kiện điểm số (ví dụ: "> 800", "<= 500")
                var match = Regex.Match(score_condition, @"([><=]+)\s*(\d+)");
                if (match.Success)
                {
                    queryBuilder.Append($"AND AverageScore {match.Groups[1].Value} @Score ");
                    parameters["@Score"] = int.Parse(match.Groups[2].Value);
                }
            }

            if (!string.IsNullOrEmpty(last_login_before) && DateTime.TryParse(last_login_before, out var loginDate))
            {
                queryBuilder.Append("AND LastLoginDate < @LastLoginDate ");
                parameters["@LastLoginDate"] = loginDate;
            }

            if (min_tests_completed.HasValue)
            {
                queryBuilder.Append("AND TestCount >= @MinTestsCompleted ");
                parameters["@MinTestsCompleted"] = min_tests_completed.Value;
            }

            // Xây dựng mệnh đề ORDER BY
            queryBuilder.Append("ORDER BY ");
            switch (sort_by?.ToLower())
            {
                case "highest_score":
                    queryBuilder.Append("HighestScore DESC");
                    break;
                case "average_score":
                    queryBuilder.Append("AverageScore DESC");
                    break;
                case "test_count":
                    queryBuilder.Append("TestCount DESC");
                    break;
                default:
                    queryBuilder.Append("LastLoginDate DESC");
                    break;
            }

            // Thêm phân trang
            queryBuilder.Append(" OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY;");
            parameters["@Limit"] = limit;

            // Thực thi truy vấn
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(queryBuilder.ToString(), connection))
                {
                    foreach (var p in parameters)
                    {
                        command.Parameters.AddWithValue(p.Key, p.Value);
                    }

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var member = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                member[reader.GetName(i)] = reader.GetValue(i) == DBNull.Value ? null : reader.GetValue(i);
                            }
                            members.Add(member);
                        }
                    }
                }
            }
            return members;
        }
        public static async Task<List<Dictionary<string, object>>> FindQuestionsByCriteriaAsync(
       int? part = null,
       string correct_rate_condition = null,
       string topic_name = null,
       bool? has_anomaly = null,
       int? min_feedback_count = null,
       string sortBy = null,
       int limit = 10,
       string irt_difficulty_condition = null,
       string quality_filter = null
   )
        {
            var questions = new List<Dictionary<string, object>>();
            var queryBuilder = new StringBuilder();
            var parameters = new Dictionary<string, object>();

            // ✅ CTE với IRT columns
            queryBuilder.Append(@"
;WITH AllQuestions AS (
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly, 
           IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, LastAnalyzed,
           '1' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part1_Question 
    UNION ALL
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly,
           IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, LastAnalyzed,
           '2' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part2_Question 
    UNION ALL
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly,
           IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, LastAnalyzed,
           '3' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part3_Question 
    UNION ALL
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly,
           IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, LastAnalyzed,
           '4' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part4_Question 
    UNION ALL
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly,
           IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, LastAnalyzed,
           '5' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part5_Question 
    UNION ALL
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly,
           IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, LastAnalyzed,
           '6' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part6_Question 
    UNION ALL
    SELECT QuestionKey, QuestionText, CorrectRate, FeedbackCount, SkillLevel, Anomaly,
           IrtDifficulty, IrtDiscrimination, IrtGuessing, Quality, ConfidenceLevel, LastAnalyzed,
           '7' AS Part, GrammarTopic, VocabularyTopic FROM TEC_Part7_Question
)
SELECT Q.*, GT.TopicName as GrammarTopicName, VT.TopicName as VocabularyTopicName
FROM AllQuestions Q
LEFT JOIN GrammarTopics GT ON Q.GrammarTopic = GT.GrammarTopicID
LEFT JOIN VocabularyTopics VT ON Q.VocabularyTopic = VT.VocabularyTopicID
WHERE 1=1 ");

            // ✅ FILTER 1: Part
            if (part.HasValue)
            {
                queryBuilder.Append("AND Q.Part = @Part ");
                parameters["@Part"] = part.Value.ToString();
            }

            // ✅ FILTER 2: Anomaly
            if (has_anomaly.HasValue)
            {
                queryBuilder.Append("AND Q.Anomaly = @HasAnomaly ");
                parameters["@HasAnomaly"] = has_anomaly.Value;
            }

            // ✅ FILTER 3: Feedback Count
            if (min_feedback_count.HasValue)
            {
                queryBuilder.Append("AND Q.FeedbackCount >= @MinFeedbackCount ");
                parameters["@MinFeedbackCount"] = min_feedback_count.Value;
            }

            // ✅ FILTER 4: Topic Name (Grammar or Vocab)
            if (!string.IsNullOrEmpty(topic_name))
            {
                queryBuilder.Append("AND (GT.TopicName LIKE @TopicName OR VT.TopicName LIKE @TopicName) ");
                parameters["@TopicName"] = $"%{topic_name}%";
            }

            // ✅ FILTER 5: Correct Rate
            if (!string.IsNullOrEmpty(correct_rate_condition))
            {
                var match = Regex.Match(correct_rate_condition, @"([><=]+)\s*(.+)");
                if (match.Success && float.TryParse(match.Groups[2].Value.Replace("%", "").Trim(), out float targetValue))
                {
                    queryBuilder.Append($"AND Q.CorrectRate IS NOT NULL AND Q.CorrectRate {match.Groups[1].Value} @CorrectRate ");
                    parameters["@CorrectRate"] = targetValue;
                }
            }

            // ✅ FILTER 6: IRT Difficulty
            if (!string.IsNullOrEmpty(irt_difficulty_condition))
            {
                var match = Regex.Match(irt_difficulty_condition, @"([><=]+)\s*(.+)");
                if (match.Success && float.TryParse(match.Groups[2].Value, out float diffValue))
                {
                    queryBuilder.Append($"AND Q.IrtDifficulty IS NOT NULL AND Q.IrtDifficulty {match.Groups[1].Value} @IrtDifficulty ");
                    parameters["@IrtDifficulty"] = diffValue;
                }
            }

            // ✅ FILTER 7: Quality
            if (!string.IsNullOrEmpty(quality_filter))
            {
                queryBuilder.Append("AND Q.Quality = @Quality ");
                parameters["@Quality"] = quality_filter;
            }

            // ✅ ORDER BY
            queryBuilder.Append("ORDER BY ");
            if (!string.IsNullOrEmpty(sortBy))
            {
                switch (sortBy.ToUpper())
                {
                    case "CORRECTRATE_ASC":
                        queryBuilder.Append("Q.CorrectRate ASC, Q.QuestionKey ASC ");
                        break;
                    case "CORRECTRATE_DESC":
                        queryBuilder.Append("Q.CorrectRate DESC, Q.QuestionKey ASC ");
                        break;
                    case "FEEDBACKCOUNT_DESC":
                        queryBuilder.Append("Q.FeedbackCount DESC, Q.QuestionKey ASC ");
                        break;
                    case "IRT_DIFFICULTY_ASC":
                        queryBuilder.Append("Q.IrtDifficulty ASC, Q.QuestionKey ASC ");
                        break;
                    case "IRT_DIFFICULTY_DESC":
                        queryBuilder.Append("Q.IrtDifficulty DESC, Q.QuestionKey ASC ");
                        break;
                    default:
                        queryBuilder.Append("Q.Part ASC, Q.QuestionKey ASC ");
                        break;
                }
            }
            else
            {
                queryBuilder.Append("Q.Part ASC, Q.QuestionKey ASC ");
            }

            queryBuilder.Append("OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY;");
            parameters["@Limit"] = limit;

            // Execute
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(queryBuilder.ToString(), connection))
                {
                    foreach (var p in parameters)
                    {
                        command.Parameters.AddWithValue(p.Key, p.Value);
                    }
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var question = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                question[reader.GetName(i)] = reader.GetValue(i) == DBNull.Value ? null : reader.GetValue(i);
                            }
                            questions.Add(question);
                        }
                    }
                }
            }
            return questions;
        }
        public static async Task<List<Dictionary<string, object>>> GetUnresolvedFeedbacksAsync(int limit = 10)
        {
            var feedbacks = new List<Dictionary<string, object>>();
            var query = @"
        SELECT TOP (@Limit) 
            F.FeedbackText, 
            F.QuestionKey, 
            M.MemberName, 
            F.CreatedOn
        FROM QuestionFeedbacks F
        LEFT JOIN EDU_Member M ON F.MemberKey = M.MemberKey
        WHERE F.Status = 0 OR F.Status IS NULL
        ORDER BY F.CreatedOn DESC;";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Limit", limit);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var feedback = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                feedback[reader.GetName(i)] = reader.GetValue(i) == DBNull.Value ? null : reader.GetValue(i);
                            }
                            feedbacks.Add(feedback);
                        }
                    }
                }
            }
            return feedbacks;
        }
        public static async Task<Dictionary<string, object>> GetSystemActivitySummaryAsync(DateTime startDate, DateTime endDate)
        {
            var summary = new Dictionary<string, object>();
            var query = @"
        SELECT
            (SELECT COUNT(*) FROM EDU_Member WHERE CreatedOn BETWEEN @StartDate AND @EndDate) AS NewMembersCount,
            (SELECT COUNT(*) FROM ResultOfUserForTest WHERE EndTime BETWEEN @StartDate AND @EndDate) AS CompletedTestsCount,
            (SELECT AVG(CAST(TestScore AS FLOAT)) FROM ResultOfUserForTest WHERE EndTime BETWEEN @StartDate AND @EndDate) AS AverageScoreInPeriod;
    ";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                summary[reader.GetName(i)] = reader.GetValue(i) == DBNull.Value ? null : reader.GetValue(i);
                            }
                        }
                    }
                }
            }
            return summary;
        }

    }
}
