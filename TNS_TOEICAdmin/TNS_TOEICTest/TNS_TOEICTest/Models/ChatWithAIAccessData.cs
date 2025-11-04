using Google.Cloud.AIPlatform.V1;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System.Data;
using TNS_TOEICTest.Models.ChatWithAI.DTOs;
using TNS_TOEICTest.Models.ChatWithAI.Repositories;
using TNS_TOEICTest.Models.ChatWithAI.Services;
using static TNS_TOEICTest.Models.ChatWithAI.DTOs.DTOs;

namespace TNS_TOEICTest.Models
{
    /// <summary>
    /// ✅ FACADE PATTERN: Điểm truy cập duy nhất cho Controller
    /// Tất cả logic đã được di chuyển vào Services/Repositories
    /// </summary>
    public static class ChatWithAIAccessData
    {
        private static readonly string _connectionString = TNS.DBConnection.Connecting.SQL_MainDatabase;

        // ========================================
        // CONVERSATION MANAGEMENT (ConversationDataService)
        // ========================================

        public static Task<List<Dictionary<string, object>>> GetConversationsWithAIAsync(string userId)
            => ConversationDataService.GetConversationsWithAIAsync(userId);

        public static Task<Dictionary<string, object>> GetInitialChatDataAsync(string userId)
            => ConversationDataService.GetInitialChatDataAsync(userId);

        public static Task<Guid> CreateNewConversationAsync(string memberKey)
            => ConversationDataService.CreateNewConversationAsync(memberKey);

        public static Task DeleteConversationAsync(Guid conversationId)
            => ConversationDataService.DeleteConversationAsync(conversationId);

        public static Task RenameConversationAsync(Guid conversationId, string newTitle)
            => ConversationDataService.RenameConversationAsync(conversationId, newTitle);

        public static Task SaveMessageAsync(Guid conversationId, string role, string content)
            => ConversationDataService.SaveMessageAsync(conversationId, role, content);

        public static Task<IEnumerable<Content>> GetMessageHistoryForApiAsync(Guid conversationId, int limit = 10)
     => ConversationDataService.GetMessageHistoryForApiAsync(conversationId, limit);

        public static Task<List<Dictionary<string, object>>> GetMoreMessagesAsync(Guid conversationId, int skipCount)
            => ConversationDataService.GetMoreMessagesAsync(conversationId, skipCount);

        // ========================================
        // MEMBER ANALYSIS TOOLS (Lightweight - For Chatbot)
        // ========================================

        /// <summary>
        /// ✅ DÙNG CHO PROMPT: Chỉ lấy thông tin cơ bản nhẹ (2KB thay vì 15KB)
        /// </summary>
        public static async Task<string> LoadMemberBasicProfileAsync(string memberKey)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                var profile = await MemberDataRepository.GetMemberProfileSummaryAsync(conn, memberKey);
                return JsonConvert.SerializeObject(profile, Formatting.Indented);
            }
        }

        /// <summary>
        /// ⚠️ DEPRECATED: Hàm này CHỈ dùng cho Admin xem toàn bộ phân tích
        /// Member chatbot KHÔNG NÊN dùng hàm này vì quá nặng (15KB)
        /// </summary>
        [Obsolete("Use LoadMemberBasicProfileAsync for chatbot prompts. This is for Admin view only.")]
        public static Task<string> LoadMemberOriginalDataAsync(string memberKey)
            => MemberAnalysisService.LoadMemberFullAnalysisForAdminAsync(memberKey);

        /// <summary>
        /// ✅ TOOL 1: Lấy phân tích chi tiết về năng lực hiện tại (IRT, Part scores, Progress)
        /// </summary>
        public static Task<object> GetMyPerformanceAnalysisAsync(string memberKey)
            => MemberAnalysisService.GetMyPerformanceAnalysisAsync(memberKey);

        /// <summary>
        /// ✅ TOOL 2: Lấy phân tích lỗi sai chi tiết (Grammar, Vocab, Error types)
        /// </summary>
        public static async Task<object> GetMyErrorAnalysisAsync(string memberKey, int limit = 150)
        {
            return await MemberAnalysisService.GetMyErrorAnalysisAsync(memberKey, 150); // ✅ LUÔN 150
        }

        // ✅ THÊM MỚI
        public static async Task<object> GetMyIncorrectQuestionsByPartAsync(string memberKey, int part, int limit = 10)
        {
            return await MemberAnalysisService.GetMyIncorrectQuestionsByPartAsync(memberKey, part, limit);
        }

        /// <summary>
        /// ✅ TOOL 3: Lấy chi tiết các câu sai gần đây với giải thích
        /// </summary>
        public static Task<object> GetMyRecentMistakesAsync(string memberKey, int limit = 10)
            => MemberAnalysisService.GetMyRecentMistakesAsync(memberKey, limit);

        /// <summary>
        /// ✅ TOOL 4: Lấy phân tích hành vi làm bài (Time, Answer changes)
        /// </summary>
        public static Task<object> GetMyBehaviorAnalysisAsync(string memberKey)
            => MemberAnalysisService.GetMyBehaviorAnalysisAsync(memberKey);

        /// <summary>
        /// ✅ TOOL 5: Lấy feedbacks gần đây của Member
        /// </summary>
        public static Task<string> LoadRecentFeedbacksAsync(string memberKey)
            => MemberAnalysisService.LoadRecentFeedbacksAsync(memberKey);

        // ========================================
        // ADAPTIVE LEARNING (AdaptiveLearningService)
        // ========================================

        /// <summary>
        /// ✅ TOOL 6: Đề xuất câu hỏi thích ứng dựa trên IRT và lỗi thường gặp
        /// </summary>
        public static Task<List<Dictionary<string, object>>> GetRecommendedQuestionsAsync(
            string memberKey,
            int part,
            int limit = 10)
            => AdaptiveLearningService.GetRecommendedQuestionsAsync(memberKey, part, limit);

        // ========================================
        // TEST ANALYSIS (TestAnalysisService)
        // ========================================

        /// <summary>
        /// ✅ TOOL 7: Phân tích bài thi cụ thể theo ngày
        /// </summary>
        public static Task<string> GetTestAnalysisByDateAsync(
            string memberKey,
            DateTime testDate,
            int? exactScore = null,
            TimeSpan? exactTime = null)
            => TestAnalysisService.GetTestAnalysisByDateAsync(memberKey, testDate, exactScore, exactTime);

 
        /// <summary>
        /// ✅ TOOL 9: Tìm kiếm TẤT CẢ topics (Grammar, Vocabulary, Category, ErrorType)
        /// </summary>
      

        /// <summary>
        /// ✅ TOOL 10: Tìm câu sai theo ALL topic types
        /// </summary>
        /// <summary>
        /// ✅ TOOL 8: Tìm câu sai theo topic names (AI tự dịch tiếng Việt → Anh)
        /// </summary>
        public static Task<List<IncorrectDetailDto>> FindMyIncorrectQuestionsByTopicNamesAsync(
            string memberKey,
            List<string> grammarTopics = null,
            List<string> vocabularyTopics = null,
            List<string> categories = null,
            List<string> errorTypes = null,
            int limit = 10)
            => TestAnalysisService.FindMyIncorrectQuestionsByTopicNamesAsync(
                memberKey, grammarTopics, vocabularyTopics, categories, errorTypes, limit);

        // ========================================
        // ADMIN FUNCTIONS (AdminDataService)
        // ========================================

        /// <summary>
        /// Admin: Load thông tin Admin profile
        /// </summary>
        public static Task<string> LoadAdminOriginalDataAsync(string adminKey)
            => AdminDataService.LoadAdminOriginalDataAsync(adminKey);

        /// <summary>
        /// Admin: Xem chi tiết Member (bao gồm phân tích đầy đủ)
        /// </summary>
        public static Task<Dictionary<string, object>> GetMemberSummaryAsync(string memberIdentifier)
            => AdminDataService.GetMemberSummaryAsync(memberIdentifier);

        /// <summary>
        /// Admin: Đếm số lượng câu hỏi theo Part
        /// </summary>
        public static Task<Dictionary<string, object>> GetQuestionCountsAsync()
            => AdminDataService.GetQuestionCountsAsync();

        /// <summary>
        /// Admin: Tìm Members theo điều kiện (score, last login, etc.)
        /// </summary>
        public static Task<List<Dictionary<string, object>>> FindMembersByCriteriaAsync(
            string score_condition = null,
            string last_login_before = null,
            int? min_tests_completed = null,
            string sort_by = "LastLoginDate",
            int limit = 10)
            => AdminDataService.FindMembersByCriteriaAsync(
                score_condition, last_login_before, min_tests_completed, sort_by, limit);

        /// <summary>
        /// Admin: Tìm Questions theo điều kiện (part, correct rate, IRT, quality, etc.)
        /// </summary>
        public static Task<List<Dictionary<string, object>>> FindQuestionsByCriteriaAsync(
            int? part = null,
            string correct_rate_condition = null,
            string topic_name = null,
            bool? has_anomaly = null,
            int? min_feedback_count = null,
            string sortBy = null,
            int limit = 10,
            string irt_difficulty_condition = null,
            string quality_filter = null)
            => AdminDataService.FindQuestionsByCriteriaAsync(
                part, correct_rate_condition, topic_name, has_anomaly,
                min_feedback_count, sortBy, limit, irt_difficulty_condition, quality_filter);

        /// <summary>
        /// Admin: Lấy feedbacks chưa giải quyết
        /// </summary>
        public static Task<List<Dictionary<string, object>>> GetUnresolvedFeedbacksAsync(int limit = 10)
            => AdminDataService.GetUnresolvedFeedbacksAsync(limit);

        /// <summary>
        /// Admin: Thống kê hoạt động hệ thống theo khoảng thời gian
        /// </summary>
        public static Task<Dictionary<string, object>> GetSystemActivitySummaryAsync(DateTime startDate, DateTime endDate)
            => AdminDataService.GetSystemActivitySummaryAsync(startDate, endDate);

        // ========================================
        // HELPER UTILITIES (Shared across services)
        // ========================================

        /// <summary>
        /// Helper: Xây dựng IN clause với parameters động
        /// </summary>
        internal static string BuildInParameterList(SqlCommand cmd, List<Guid> guids, string baseName)
        {
            var names = new List<string>();
            for (int i = 0; i < guids.Count; i++)
            {
                var p = $"@{baseName}{i}";
                cmd.Parameters.Add(p, SqlDbType.UniqueIdentifier).Value = guids[i];
                names.Add(p);
            }
            return names.Count > 0 ? string.Join(", ", names) : "NULL";
        }

        /// <summary>
        /// Helper: Tính standard deviation
        /// </summary>
        internal static double StdDev(IEnumerable<double> values)
        {
            var arr = values.ToArray();
            if (arr.Length <= 1) return 0;
            var mean = arr.Average();
            var variance = arr.Average(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(variance);
        }
    }
}