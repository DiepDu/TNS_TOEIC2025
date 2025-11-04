namespace TNS_TOEICTest.Services
{
    /// <summary>
    /// Service tự động tính toán số tin nhắn tối ưu dựa vào loại câu hỏi
    /// </summary>
    public static class SmartContextService
    {
        /// <summary>
        /// Phân tích câu hỏi và trả về số tin nhắn tối ưu (5-15)
        /// </summary>
        public static int GetOptimalLimit(string userMessage)
        {
            if (string.IsNullOrEmpty(userMessage))
                return 10;

            var lower = userMessage.ToLower();

            // ========================================
            // 🔴 HIGH RISK: Tool response rất lớn
            // → CHỈ GỬI 5 TIN NHẮN
            // ========================================

            // Part 7 recommendations
            if (lower.Contains("part 7") || lower.Contains("part7"))
            {
                Console.WriteLine("[SmartContext] 🔴 HIGH RISK: Part 7 → Limit = 5");
                return 5;
            }

            // Comprehensive analysis
            if ((lower.Contains("toàn diện") || lower.Contains("comprehensive")) &&
                (lower.Contains("phân tích") || lower.Contains("analysis")))
            {
                Console.WriteLine("[SmartContext] 🔴 HIGH RISK: Comprehensive → Limit = 5");
                return 5;
            }

            // Multiple requirements (3+ in one question)
            var count = 0;
            if (lower.Contains("điểm") || lower.Contains("score")) count++;
            if (lower.Contains("lỗi") || lower.Contains("error") || lower.Contains("mistake")) count++;
            if (lower.Contains("đề xuất") || lower.Contains("gợi ý") || lower.Contains("recommend")) count++;
            if (lower.Contains("so sánh") || lower.Contains("compare")) count++;

            if (count >= 3)
            {
                Console.WriteLine($"[SmartContext] 🔴 HIGH RISK: {count} requirements → Limit = 5");
                return 5;
            }

            // ========================================
            // 🟡 MEDIUM RISK: Tool response vừa
            // → GỬI 10 TIN NHẮN
            // ========================================

            // Recommendations (Part 1-6)
            if (lower.Contains("đề xuất") || lower.Contains("gợi ý") ||
                lower.Contains("recommend") || lower.Contains("suggest"))
            {
                Console.WriteLine("[SmartContext] 🟡 MEDIUM RISK: Recommendations → Limit = 10");
                return 10;
            }

            // Error analysis
            if (lower.Contains("phân tích lỗi") || lower.Contains("error analysis") ||
                lower.Contains("lỗi sai") || lower.Contains("mistakes"))
            {
                Console.WriteLine("[SmartContext] 🟡 MEDIUM RISK: Error analysis → Limit = 10");
                return 10;
            }

            // Performance analysis
            if (lower.Contains("phân tích") || lower.Contains("analysis") ||
                lower.Contains("performance") || lower.Contains("thành tích"))
            {
                Console.WriteLine("[SmartContext] 🟡 MEDIUM RISK: Analysis → Limit = 10");
                return 10;
            }

            // ========================================
            // ✅ LOW RISK: Simple Q&A
            // → GỬI 15 TIN NHẮN
            // ========================================

            Console.WriteLine("[SmartContext] ✅ LOW RISK: Simple Q&A → Limit = 15");
            return 15;
        }

        /// <summary>
        /// Log chi tiết token budget estimate
        /// </summary>
        public static void LogTokenEstimate(int limit, string userMessage)
        {
            int basePrompt = 1000;
            int studentProfile = 300;
            int historyTokens = limit * 150;
            int questionTokens = 100;
            int toolResponseEstimate = EstimateToolResponse(userMessage);

            int total = basePrompt + studentProfile + historyTokens + questionTokens + toolResponseEstimate;

            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         TOKEN BUDGET ESTIMATE                          ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║  Base Prompt:           {basePrompt,6} tokens                  ║");
            Console.WriteLine($"║  Student Profile:       {studentProfile,6} tokens                  ║");
            Console.WriteLine($"║  History ({limit,2} msgs):      {historyTokens,6} tokens                  ║");
            Console.WriteLine($"║  Current Question:      {questionTokens,6} tokens                  ║");
            Console.WriteLine($"║  Tool Response (est):   {toolResponseEstimate,6} tokens                  ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║  TOTAL ESTIMATE:        {total,6} tokens                  ║");
            Console.WriteLine($"║  Safety Threshold:      20,000 tokens                  ║");
            Console.WriteLine($"║  Status: {(total < 20000 ? "✅ SAFE    " : "⚠️ RISKY   ")}                               ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
        }

        private static int EstimateToolResponse(string userMessage)
        {
            var lower = userMessage.ToLower();

            if (lower.Contains("part 7") || lower.Contains("toàn diện"))
                return 15000;

            if (lower.Contains("đề xuất") || lower.Contains("phân tích"))
                return 5000;

            return 1500;
        }
    }
}