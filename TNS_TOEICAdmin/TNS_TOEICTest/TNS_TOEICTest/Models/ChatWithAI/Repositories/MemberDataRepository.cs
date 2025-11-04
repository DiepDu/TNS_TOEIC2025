using Microsoft.Data.SqlClient;
using System.Data;
using static TNS_TOEICTest.Models.ChatWithAI.DTOs.DTOs;

namespace TNS_TOEICTest.Models.ChatWithAI.Repositories
{
    /// <summary>
    /// Repository chứa các helper methods để query dữ liệu Member
    /// </summary>
    public class MemberDataRepository
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        // ===================== MEMBER PROFILE =====================
        public static async Task<MemberProfileDto?> GetMemberProfileAsync(SqlConnection conn, string memberKey)
        {
            var q = @"
        SELECT 
            MemberName, Gender, YearOld, 
            ToeicScoreExam, 
            LastLoginDate, CreatedOn,
            PracticeScore_Part1, PracticeScore_Part2, PracticeScore_Part3, PracticeScore_Part4,
            PracticeScore_Part5, PracticeScore_Part6, PracticeScore_Part7,
            ScoreTarget, IrtAbility, IrtUpdatedOn
        FROM EDU_Member 
        WHERE MemberKey = @MemberKey";

            using var cmd = new SqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
            using var r = await cmd.ExecuteReaderAsync();

            if (await r.ReadAsync())
            {
                string gender = "Not Specified";
                if (r["Gender"] != DBNull.Value)
                {
                    var g = Convert.ToInt32(r["Gender"]);
                    gender = g == 1 ? "Male" : g == 0 ? "Female" : "Not Specified";
                }

                int? birthYear = r["YearOld"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["YearOld"]);
                int? age = birthYear.HasValue ? (DateTime.Now.Year - birthYear.Value) : null;

                return new MemberProfileDto
                {
                    MemberName = r["MemberName"]?.ToString(),
                    Gender = gender,
                    BirthYear = birthYear,
                    Age = age,
                    PracticeScore_Part1 = r["PracticeScore_Part1"] == DBNull.Value ? null : (int?)Convert.ToInt32(r["PracticeScore_Part1"]),
                    PracticeScore_Part2 = r["PracticeScore_Part2"] == DBNull.Value ? null : (int?)Convert.ToInt32(r["PracticeScore_Part2"]),
                    PracticeScore_Part3 = r["PracticeScore_Part3"] == DBNull.Value ? null : (int?)Convert.ToInt32(r["PracticeScore_Part3"]),
                    PracticeScore_Part4 = r["PracticeScore_Part4"] == DBNull.Value ? null : (int?)Convert.ToInt32(r["PracticeScore_Part4"]),
                    PracticeScore_Part5 = r["PracticeScore_Part5"] == DBNull.Value ? null : (int?)Convert.ToInt32(r["PracticeScore_Part5"]),
                    PracticeScore_Part6 = r["PracticeScore_Part6"] == DBNull.Value ? null : (int?)Convert.ToInt32(r["PracticeScore_Part6"]),
                    PracticeScore_Part7 = r["PracticeScore_Part7"] == DBNull.Value ? null : (int?)Convert.ToInt32(r["PracticeScore_Part7"]),
                    ScoreTarget = r["ScoreTarget"] == DBNull.Value ? null : (int?)Convert.ToInt32(r["ScoreTarget"]),
                    IrtAbility = r["IrtAbility"] == DBNull.Value ? null : (float?)Convert.ToSingle(r["IrtAbility"]),
                    IrtUpdatedOn = r["IrtUpdatedOn"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["IrtUpdatedOn"]),
                    ToeicScoreExam = r["ToeicScoreExam"] == DBNull.Value ? null : (int?)Convert.ToInt32(r["ToeicScoreExam"]),
                    LastLoginDate = r["LastLoginDate"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["LastLoginDate"]),
                    CreatedOn = r["CreatedOn"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["CreatedOn"])
                };
            }
            return null;
        }
        /// <summary>
        /// Lấy profile tóm tắt nhẹ cho prompt (không bao gồm phân tích chi tiết)
        /// </summary>
        public static async Task<MemberProfileSummaryDto?> GetMemberProfileSummaryAsync(SqlConnection conn, string memberKey)
        {
            var query = @"
SELECT 
    M.MemberName, M.Gender, M.YearOld, 
    M.ScoreTarget, M.IrtAbility, M.ToeicScoreExam, M.LastLoginDate,
    (SELECT COUNT(*) FROM ResultOfUserForTest WHERE MemberKey = @MemberKey) AS TotalTestsTaken,
    (SELECT TOP 1 TestScore FROM ResultOfUserForTest WHERE MemberKey = @MemberKey ORDER BY EndTime DESC) AS LatestScore,
    (SELECT TOP 1 EndTime FROM ResultOfUserForTest WHERE MemberKey = @MemberKey ORDER BY EndTime DESC) AS LatestTestDate
FROM EDU_Member M
WHERE M.MemberKey = @MemberKey";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
            using var r = await cmd.ExecuteReaderAsync();

            if (await r.ReadAsync())
            {
                string gender = "Not Specified";
                if (r["Gender"] != DBNull.Value)
                {
                    var g = Convert.ToInt32(r["Gender"]);
                    gender = g == 1 ? "Male" : g == 0 ? "Female" : "Not Specified";
                }

                int? birthYear = r["YearOld"] == DBNull.Value ? null : Convert.ToInt32(r["YearOld"]);
                int? age = birthYear.HasValue ? (DateTime.Now.Year - birthYear.Value) : null;

                return new MemberProfileSummaryDto
                {
                    MemberName = r["MemberName"]?.ToString(),
                    Gender = gender,
                    Age = age,
                    ScoreTarget = r["ScoreTarget"] == DBNull.Value ? null : Convert.ToInt32(r["ScoreTarget"]),
                    IrtAbility = r["IrtAbility"] == DBNull.Value ? null : Convert.ToSingle(r["IrtAbility"]),
                    ToeicScoreExam = r["ToeicScoreExam"] == DBNull.Value ? null : Convert.ToInt32(r["ToeicScoreExam"]),
                    LastLoginDate = r["LastLoginDate"] == DBNull.Value ? null : Convert.ToDateTime(r["LastLoginDate"]),
                    TotalTestsTaken = r["TotalTestsTaken"] == DBNull.Value ? null : Convert.ToInt32(r["TotalTestsTaken"]),
                    LatestScore = r["LatestScore"] == DBNull.Value ? null : Convert.ToInt32(r["LatestScore"]),
                    LatestTestDate = r["LatestTestDate"] == DBNull.Value ? null : Convert.ToDateTime(r["LatestTestDate"])
                };
            }
            return null;
        }

        // ===================== RESULTS =====================
        public static async Task<List<ResultRow>> GetAllResultsForMemberAsync(SqlConnection conn, string memberKey)
        {
            var list = new List<ResultRow>();
            var q = @"
            SELECT r.ResultKey, r.TestKey, r.StartTime, r.EndTime, r.ListeningScore, r.ReadingScore, r.TestScore, r.Time, t.TotalQuestion
            FROM ResultOfUserForTest r
            LEFT JOIN Test t ON r.TestKey = t.TestKey
            WHERE r.MemberKey = @MemberKey
            ORDER BY r.StartTime DESC";
            using var cmd = new SqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@MemberKey", memberKey);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new ResultRow
                {
                    ResultKey = r.GetGuid(r.GetOrdinal("ResultKey")),
                    TestKey = r.GetGuid(r.GetOrdinal("TestKey")),
                    StartTime = r["StartTime"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["StartTime"]),
                    EndTime = r["EndTime"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["EndTime"]),
                    ListeningScore = r["ListeningScore"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["ListeningScore"]),
                    ReadingScore = r["ReadingScore"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["ReadingScore"]),
                    TestScore = r["TestScore"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["TestScore"]),
                    Time = r["Time"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["Time"]),
                    TotalQuestion = r["TotalQuestion"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["TotalQuestion"])
                });
            }
            return list;
        }

        // ===================== USER ANSWERS =====================
        public static async Task<List<UserAnswerRow>> GetUserAnswersByResultKeysAsync(SqlConnection conn, List<Guid> resultKeys)
        {
            var answers = new List<UserAnswerRow>();
            if (resultKeys == null || resultKeys.Count == 0) return answers;

            var paramNames = resultKeys.Select((g, idx) => $"@r{idx}").ToList();
            var inClause = string.Join(", ", paramNames);
            var q = $@"SELECT UAnswerKey, ResultKey, QuestionKey, SelectAnswerKey, IsCorrect, TimeSpent, AnswerTime, NumberOfAnswerChanges, Part
                   FROM UserAnswers
                   WHERE ResultKey IN ({inClause})
                   ORDER BY AnswerTime DESC";

            using var cmd = new SqlCommand(q, conn);
            for (int i = 0; i < resultKeys.Count; i++)
                cmd.Parameters.AddWithValue(paramNames[i], resultKeys[i]);

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                answers.Add(new UserAnswerRow
                {
                    UAnswerKey = r.GetGuid(r.GetOrdinal("UAnswerKey")),
                    ResultKey = r.GetGuid(r.GetOrdinal("ResultKey")),
                    QuestionKey = r.GetGuid(r.GetOrdinal("QuestionKey")),
                    SelectAnswerKey = r["SelectAnswerKey"] == DBNull.Value ? (Guid?)null : r.GetGuid(r.GetOrdinal("SelectAnswerKey")),
                    IsCorrect = Convert.ToBoolean(r["IsCorrect"]),
                    TimeSpent = Convert.ToInt32(r["TimeSpent"]),
                    AnswerTime = Convert.ToDateTime(r["AnswerTime"]),
                    NumberOfAnswerChanges = Convert.ToInt32(r["NumberOfAnswerChanges"]),
                    Part = Convert.ToInt32(r["Part"])
                });
            }
            return answers;
        }

        // ===================== ERRORS =====================
        public static async Task<List<UserErrorRow>> GetUserErrorsAsync(SqlConnection conn, string memberKey, int limit = 150)
        {
            var list = new List<UserErrorRow>();

            // ✅ THAY ĐỔI: LUÔN lấy 150 lỗi gần nhất (bỏ tham số limit)
            var q = @"
    SELECT TOP 150 
        UE.ErrorKey, UE.AnswerKey, UE.UserKey, UE.ResultKey, UE.ErrorDate, UE.Part, UE.SkillLevel,
        ET.ErrorDescription, GT.TopicName as GrammarTopicName, VT.TopicName as VocabularyTopicName
    FROM UsersError UE
    LEFT JOIN ErrorTypes ET ON UE.ErrorType = ET.ErrorTypeID
    LEFT JOIN GrammarTopics GT ON UE.GrammarTopic = GT.GrammarTopicID
    LEFT JOIN VocabularyTopics VT ON UE.VocabularyTopic = VT.VocabularyTopicID
    WHERE UE.UserKey = @UserKey
    ORDER BY UE.ErrorDate DESC";

            using var cmd = new SqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@UserKey", memberKey);

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new UserErrorRow
                {
                    ErrorKey = r.GetGuid(r.GetOrdinal("ErrorKey")),
                    AnswerKey = r.GetGuid(r.GetOrdinal("AnswerKey")),
                    UserKey = r.GetGuid(r.GetOrdinal("UserKey")),
                    ResultKey = r.GetGuid(r.GetOrdinal("ResultKey")),
                    ErrorDate = r["ErrorDate"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["ErrorDate"]),
                    Part = r["Part"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["Part"]),
                    SkillLevel = r["SkillLevel"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SkillLevel"]),
                    ErrorTypeName = r["ErrorDescription"]?.ToString(),
                    GrammarTopicName = r["GrammarTopicName"]?.ToString(),
                    VocabularyTopicName = r["VocabularyTopicName"]?.ToString()
                });
            }

            return list;
        }

        // ✅ THÊM MỚI: Analyze errors và lấy TOP lỗi lặp lại nhiều nhất
        public static object AnalyzeErrors(List<UserErrorRow> errors)
        {
            var res = new Dictionary<string, object>();
            if (errors == null || errors.Count == 0)
            {
                res["message"] = "No recorded errors for this user.";
                return res;
            }

            // ✅ THAY ĐỔI: TOP 10 lỗi lặp lại NHIỀU NHẤT (thay vì lấy tất cả)
            var grammarGroups = errors.Where(e => !string.IsNullOrEmpty(e.GrammarTopicName))
                .GroupBy(e => e.GrammarTopicName)
                .Select(g => new { Topic = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10) // ✅ TOP 10
                .ToList();

            var vocabGroups = errors.Where(e => !string.IsNullOrEmpty(e.VocabularyTopicName))
                .GroupBy(e => e.VocabularyTopicName)
                .Select(g => new { Topic = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10) // ✅ TOP 10
                .ToList();

            var errorTypeGroups = errors.Where(e => !string.IsNullOrEmpty(e.ErrorTypeName))
                .GroupBy(e => e.ErrorTypeName)
                .Select(g => new { ErrorType = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10) // ✅ TOP 10
                .ToList();

            res["totalErrorsAnalyzed"] = 150; // ✅ LUÔN là 150
            res["topGrammarTopics"] = grammarGroups;
            res["topVocabularyTopics"] = vocabGroups;
            res["topErrorTypes"] = errorTypeGroups;

            // ✅ THÊM: Phân tích theo Part
            var errorsByPart = errors.Where(e => e.Part.HasValue)
                .GroupBy(e => e.Part!.Value)
                .Select(g => new { Part = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();
            res["errorsByPart"] = errorsByPart;

            return res;
        }

        // ✅ THÊM MỚI: Lấy câu sai theo Part
        public static async Task<List<IncorrectDetailDto>> GetIncorrectQuestionsByPartAsync(
            SqlConnection conn,
            string memberKey,
            int part,
            int limit)
        {
            var results = new List<IncorrectDetailDto>();
            const string DOMAIN = "https://localhost:7078";

            // ✅ SỬ DỤNG LẠI UNION ALL LOGIC (tương tự GetRecentMistakesDetailedAsync)
            var query = $@"
WITH Q AS (
 SELECT QuestionKey, QuestionText, Explanation, 1 AS Part, 
        GrammarTopic, VocabularyTopic, Category,
        Parent AS ParentKey, CAST(NULL AS NVARCHAR(MAX)) AS ParentText,
        QuestionImage, QuestionVoice, CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part1_Question

 UNION ALL 
 SELECT QuestionKey, QuestionText, Explanation, 2 AS Part, 
        GrammarTopic, VocabularyTopic, Category,
        Parent AS ParentKey, CAST(NULL AS NVARCHAR(MAX)) AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, QuestionVoice,
        CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part2_Question

 UNION ALL 
 SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, 3 AS Part, 
        Q.GrammarTopic, Q.VocabularyTopic, Q.Category,
        Q.Parent AS ParentKey, P.QuestionText AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, 
        CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        P.QuestionVoice AS ParentAudioUrl
 FROM TEC_Part3_Question Q
 LEFT JOIN TEC_Part3_Question P ON Q.Parent = P.QuestionKey

 UNION ALL 
 SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, 4 AS Part, 
        Q.GrammarTopic, Q.VocabularyTopic, Q.Category,
        Q.Parent AS ParentKey, P.QuestionText AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, 
        CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        P.QuestionVoice AS ParentAudioUrl
 FROM TEC_Part4_Question Q
 LEFT JOIN TEC_Part4_Question P ON Q.Parent = P.QuestionKey

 UNION ALL 
 SELECT QuestionKey, QuestionText, Explanation, 5 AS Part, 
        GrammarTopic, VocabularyTopic, Category,
        Parent AS ParentKey, CAST(NULL AS NVARCHAR(MAX)) AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, 
        CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part5_Question

 UNION ALL 
 SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, 6 AS Part, 
        Q.GrammarTopic, Q.VocabularyTopic, Q.Category,
        Q.Parent AS ParentKey, P.QuestionText AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, 
        CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part6_Question Q
 LEFT JOIN TEC_Part6_Question P ON Q.Parent = P.QuestionKey

 UNION ALL 
 SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, 7 AS Part, 
        Q.GrammarTopic, Q.VocabularyTopic, Q.Category,
        Q.Parent AS ParentKey, P.QuestionText AS ParentText,
        Q.QuestionImage, CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part7_Question Q
 LEFT JOIN TEC_Part7_Question P ON Q.Parent = P.QuestionKey
)
SELECT TOP (@Limit)
    UA.UAnswerKey, UA.ResultKey, UA.QuestionKey, UA.SelectAnswerKey,
    UA.TimeSpent, UA.AnswerTime, UA.NumberOfAnswerChanges, UA.Part,
    Q.QuestionText, Q.ParentText, Q.Explanation,
    Q.QuestionImage, Q.QuestionVoice, Q.ParentAudioUrl,
    GT.TopicName AS GrammarTopicName,
    VT.TopicName AS VocabularyTopicName,
    CAT.CategoryName,
    ET.ErrorDescription
FROM UserAnswers UA
JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey AND R.MemberKey = @MemberKey
LEFT JOIN Q ON UA.QuestionKey = Q.QuestionKey
LEFT JOIN GrammarTopics GT ON Q.GrammarTopic = GT.GrammarTopicID
LEFT JOIN VocabularyTopics VT ON Q.VocabularyTopic = VT.VocabularyTopicID
LEFT JOIN TEC_Category CAT ON Q.Category = CAT.CategoryKey
LEFT JOIN UsersError UE ON UE.AnswerKey = UA.SelectAnswerKey
LEFT JOIN ErrorTypes ET ON UE.ErrorType = ET.ErrorTypeID
WHERE UA.IsCorrect = 0 AND UA.Part = @Part
ORDER BY UA.AnswerTime DESC";

            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.Add("@Limit", SqlDbType.Int).Value = limit;
                cmd.Parameters.Add("@MemberKey", SqlDbType.NVarChar, 50).Value = memberKey;
                cmd.Parameters.Add("@Part", SqlDbType.Int).Value = part;

                var uaRows = new List<UserAnswerRow>();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var dto = new IncorrectDetailDto
                        {
                            UAnswerKey = reader.GetGuid(reader.GetOrdinal("UAnswerKey")),
                            ResultKey = reader.GetGuid(reader.GetOrdinal("ResultKey")),
                            QuestionKey = reader.GetGuid(reader.GetOrdinal("QuestionKey")),
                            Part = Convert.ToInt32(reader["Part"]),
                            QuestionText = reader["QuestionText"]?.ToString() ?? "",
                            ParentText = reader["ParentText"]?.ToString() ?? "",
                            Explanation = reader["Explanation"]?.ToString() ?? "",
                            TimeSpentSeconds = Convert.ToInt32(reader["TimeSpent"]),
                            AnswerTime = Convert.ToDateTime(reader["AnswerTime"]),
                            NumberOfAnswerChanges = Convert.ToInt32(reader["NumberOfAnswerChanges"]),
                            GrammarTopic = reader["GrammarTopicName"]?.ToString() ?? "",
                            VocabularyTopic = reader["VocabularyTopicName"]?.ToString() ?? "",
                            CategoryName = reader["CategoryName"]?.ToString() ?? "",
                            ErrorType = reader["ErrorDescription"]?.ToString() ?? "",
                            QuestionImageUrl = BuildMediaUrl(DOMAIN, reader["QuestionImage"]?.ToString()),
                            QuestionAudioUrl = BuildMediaUrl(DOMAIN, reader["QuestionVoice"]?.ToString()),
                            ParentAudioUrl = BuildMediaUrl(DOMAIN, reader["ParentAudioUrl"]?.ToString())
                        };

                        results.Add(dto);

                        uaRows.Add(new UserAnswerRow
                        {
                            UAnswerKey = dto.UAnswerKey,
                            SelectAnswerKey = reader["SelectAnswerKey"] == DBNull.Value ? null : reader.GetGuid(reader.GetOrdinal("SelectAnswerKey")),
                            QuestionKey = dto.QuestionKey,
                            Part = dto.Part
                        });
                    }
                }

                // ✅ Enrich với 4 đáp án
                await EnrichWithAllAnswers(conn, results, uaRows);
            }

            return results;
        }

        // ✅ THÊM helper method (copy từ GetRecentMistakesDetailedAsync)
        private static string BuildMediaUrl(string domain, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return "";

            if (!relativePath.StartsWith("/"))
                relativePath = "/" + relativePath;

            return domain + relativePath;
        }

        public static async Task<List<IncorrectDetailDto>> GetRecentMistakesDetailedAsync(
        SqlConnection conn,
        string memberKey,
        int limit)
        {
            var results = new List<IncorrectDetailDto>();

            // ✅ FIX: Dùng SELF-JOIN thay vì JOIN TEC_Part3_Parent
            var query = $@"
WITH Q AS (
 SELECT QuestionKey, QuestionText, Explanation, 1 AS Part, 
        GrammarTopic, VocabularyTopic, Category,
        Parent AS ParentKey, CAST(NULL AS NVARCHAR(MAX)) AS ParentText,
        QuestionImage, QuestionVoice, CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part1_Question

 UNION ALL 
 SELECT QuestionKey, QuestionText, Explanation, 2 AS Part, 
        GrammarTopic, VocabularyTopic, Category,
        Parent AS ParentKey, CAST(NULL AS NVARCHAR(MAX)) AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, QuestionVoice,
        CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part2_Question

 UNION ALL 
 SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, 3 AS Part, 
        Q.GrammarTopic, Q.VocabularyTopic, Q.Category,
        Q.Parent AS ParentKey, P.QuestionText AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, 
        CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        P.QuestionVoice AS ParentAudioUrl
 FROM TEC_Part3_Question Q
 LEFT JOIN TEC_Part3_Question P ON Q.Parent = P.QuestionKey  -- ✅ SELF-JOIN

 UNION ALL 
 SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, 4 AS Part, 
        Q.GrammarTopic, Q.VocabularyTopic, Q.Category,
        Q.Parent AS ParentKey, P.QuestionText AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, 
        CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        P.QuestionVoice AS ParentAudioUrl
 FROM TEC_Part4_Question Q
 LEFT JOIN TEC_Part4_Question P ON Q.Parent = P.QuestionKey  -- ✅ SELF-JOIN

 UNION ALL 
 SELECT QuestionKey, QuestionText, Explanation, 5 AS Part, 
        GrammarTopic, VocabularyTopic, Category,
        Parent AS ParentKey, CAST(NULL AS NVARCHAR(MAX)) AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, 
        CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part5_Question

 UNION ALL 
 SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, 6 AS Part, 
        Q.GrammarTopic, Q.VocabularyTopic, Q.Category,
        Q.Parent AS ParentKey, P.QuestionText AS ParentText,
        CAST(NULL AS NVARCHAR(500)) AS QuestionImage, 
        CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part6_Question Q
 LEFT JOIN TEC_Part6_Question P ON Q.Parent = P.QuestionKey  -- ✅ SELF-JOIN

 UNION ALL 
 SELECT Q.QuestionKey, Q.QuestionText, Q.Explanation, 7 AS Part, 
        Q.GrammarTopic, Q.VocabularyTopic, Q.Category,
        Q.Parent AS ParentKey, P.QuestionText AS ParentText,
        Q.QuestionImage, CAST(NULL AS NVARCHAR(500)) AS QuestionVoice,
        CAST(NULL AS NVARCHAR(500)) AS ParentAudioUrl
 FROM TEC_Part7_Question Q
 LEFT JOIN TEC_Part7_Question P ON Q.Parent = P.QuestionKey  -- ✅ SELF-JOIN
)
SELECT TOP (@Limit)
    UA.UAnswerKey, UA.ResultKey, UA.QuestionKey, UA.SelectAnswerKey,
    UA.TimeSpent, UA.AnswerTime, UA.NumberOfAnswerChanges, UA.Part,
    Q.QuestionText, Q.ParentText, Q.Explanation,
    Q.QuestionImage, Q.QuestionVoice, Q.ParentAudioUrl,
    GT.TopicName AS GrammarTopicName,
    VT.TopicName AS VocabularyTopicName,
    CAT.CategoryName,
    ET.ErrorDescription
FROM UserAnswers UA
JOIN ResultOfUserForTest R ON UA.ResultKey = R.ResultKey AND R.MemberKey = @MemberKey
LEFT JOIN Q ON UA.QuestionKey = Q.QuestionKey
LEFT JOIN GrammarTopics GT ON Q.GrammarTopic = GT.GrammarTopicID
LEFT JOIN VocabularyTopics VT ON Q.VocabularyTopic = VT.VocabularyTopicID
LEFT JOIN TEC_Category CAT ON Q.Category = CAT.CategoryKey
LEFT JOIN UsersError UE ON UE.AnswerKey = UA.SelectAnswerKey
LEFT JOIN ErrorTypes ET ON UE.ErrorType = ET.ErrorTypeID
WHERE UA.IsCorrect = 0
ORDER BY UA.AnswerTime DESC;";

            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Limit", limit);
                cmd.Parameters.AddWithValue("@MemberKey", memberKey);

                var uaRows = new List<UserAnswerRow>();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var dto = new IncorrectDetailDto
                        {
                            UAnswerKey = reader.GetGuid(reader.GetOrdinal("UAnswerKey")),
                            ResultKey = reader.GetGuid(reader.GetOrdinal("ResultKey")),
                            QuestionKey = reader.GetGuid(reader.GetOrdinal("QuestionKey")),
                            Part = Convert.ToInt32(reader["Part"]),
                            QuestionText = reader["QuestionText"]?.ToString() ?? "",
                            ParentText = reader["ParentText"]?.ToString() ?? "",
                            Explanation = reader["Explanation"]?.ToString() ?? "",
                            TimeSpentSeconds = Convert.ToInt32(reader["TimeSpent"]),
                            AnswerTime = Convert.ToDateTime(reader["AnswerTime"]),
                            NumberOfAnswerChanges = Convert.ToInt32(reader["NumberOfAnswerChanges"]),
                            GrammarTopic = reader["GrammarTopicName"]?.ToString() ?? "",
                            VocabularyTopic = reader["VocabularyTopicName"]?.ToString() ?? "",
                            CategoryName = reader["CategoryName"]?.ToString() ?? "",
                            ErrorType = reader["ErrorDescription"]?.ToString() ?? "",

                            // ✅ THÊM: Lưu relative paths (chưa build full URL)
                            QuestionImageUrl = reader["QuestionImage"]?.ToString() ?? "",
                            QuestionAudioUrl = reader["QuestionVoice"]?.ToString() ?? "",
                            ParentAudioUrl = reader["ParentAudioUrl"]?.ToString() ?? ""
                        };

                        results.Add(dto);

                        uaRows.Add(new UserAnswerRow
                        {
                            UAnswerKey = dto.UAnswerKey,
                            SelectAnswerKey = reader["SelectAnswerKey"] == DBNull.Value ? null : reader.GetGuid(reader.GetOrdinal("SelectAnswerKey")),
                            QuestionKey = dto.QuestionKey,
                            Part = dto.Part
                        });
                    }
                }

                // ✅ Enrich với 4 đáp án
                await EnrichWithAllAnswers(conn, results, uaRows);
            }

            return results;
        }

        // ✅ THÊM: Helper method để lấy 4 đáp án
        private static async Task EnrichWithAllAnswers(
            SqlConnection conn,
            List<IncorrectDetailDto> results,
            List<UserAnswerRow> uaRows)
        {
            foreach (var result in results)
            {
                var part = result.Part;
                var tableName = $"TEC_Part{part}_Answer";

                var query = $@"
            SELECT AnswerKey, AnswerText, AnswerCorrect
            FROM {tableName}
            WHERE QuestionKey = @QuestionKey
            ORDER BY AnswerKey";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@QuestionKey", result.QuestionKey);

                    var answers = new List<AnswerOptionDto>();
                    var userAnswerKey = uaRows.FirstOrDefault(u => u.UAnswerKey == result.UAnswerKey)?.SelectAnswerKey;

                    using (var rdr = await cmd.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            var answerKey = rdr.GetGuid(0);
                            var answerText = rdr["AnswerText"]?.ToString() ?? "";
                            var isCorrect = Convert.ToBoolean(rdr["AnswerCorrect"]);
                            var isSelected = userAnswerKey.HasValue && userAnswerKey.Value == answerKey;

                            answers.Add(new AnswerOptionDto
                            {
                                AnswerKey = answerKey,
                                AnswerText = answerText,
                                IsCorrect = isCorrect,
                                IsSelected = isSelected
                            });

                            if (isSelected) result.SelectedAnswerText = answerText;
                            if (isCorrect) result.CorrectAnswerText = answerText;
                        }
                    }

                    result.AllAnswers = answers;
                }
            }
        }
        public static Dictionary<string, object> AnalyzeBehavior(List<UserAnswerRow> answers)
        {
            var res = new Dictionary<string, object>();
            if (answers == null || answers.Count == 0)
            {
                res["message"] = "No user answer data available.";
                return res;
            }

            double avgTime = answers.Average(a => a.TimeSpent);
            double avgChanges = answers.Average(a => a.NumberOfAnswerChanges);
            double correctRate = answers.Average(a => a.IsCorrect ? 1.0 : 0.0) * 100.0;
            res["overall"] = new
            {
                totalAnswers = answers.Count,
                avgTimePerQuestionSeconds = Math.Round(avgTime, 2),
                avgNumberOfAnswerChanges = Math.Round(avgChanges, 2),
                overallCorrectRatePercent = Math.Round(correctRate, 2)
            };

            var byPart = answers.GroupBy(a => a.Part)
                .Select(g => new
                {
                    Part = g.Key,
                    QuestionCount = g.Count(),
                    AvgTimeSeconds = Math.Round(g.Average(x => x.TimeSpent), 2),
                    CorrectRatePercent = Math.Round(g.Average(x => x.IsCorrect ? 1.0 : 0.0) * 100.0, 2),
                    AvgNumberOfAnswerChanges = Math.Round(g.Average(x => x.NumberOfAnswerChanges), 2)
                }).ToList();

            res["byPart"] = byPart;
            return res;
        }



        public static Dictionary<string, object> ComputeScoreStatistics(List<int> scores)
        {
            var res = new Dictionary<string, object>();
            if (scores == null || scores.Count == 0)
            {
                res["message"] = "No scores available.";
                return res;
            }
            res["count"] = scores.Count;
            res["max"] = scores.Max();
            res["min"] = scores.Min();
            res["avg"] = Math.Round(scores.Average(), 2);

            double mean = scores.Average();
            double variance = scores.Average(d => Math.Pow(d - mean, 2));
            res["stddev"] = Math.Round(Math.Sqrt(variance), 2);
            res["improvementAbsolute"] = scores.Last() - scores.First();

            return res;
        }

        public static object ComputeScoreTrend(List<int> recentScores)
        {
            if (recentScores == null || recentScores.Count == 0) return new { message = "No score history" };
            if (recentScores.Count == 1) return new { message = "Only one recent score available", score = recentScores.First() };

            int improved = 0, declined = 0, same = 0;
            for (int i = 1; i < recentScores.Count; i++)
            {
                if (recentScores[i] > recentScores[i - 1]) improved++;
                else if (recentScores[i] < recentScores[i - 1]) declined++;
                else same++;
            }
            string summary = improved > declined
                ? $"Mostly improving ({improved} improving vs {declined} declining) in recent {recentScores.Count} tests."
                : declined > improved
                    ? $"Mostly declining ({declined} declining vs {improved} improving) in recent {recentScores.Count} tests."
                    : $"Mixed trend: {improved} improving, {declined} declining, {same} same.";

            return new { recentCount = recentScores.Count, improvedCount = improved, declinedCount = declined, sameCount = same, summary };
        }
    }
}