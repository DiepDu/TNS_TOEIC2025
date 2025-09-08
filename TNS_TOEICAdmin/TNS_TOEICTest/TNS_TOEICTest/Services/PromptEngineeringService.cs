using Google.Cloud.AIPlatform.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Namespace có thể là TNS_TOEICTest.Services hoặc tương tự
namespace TNS_TOEICTest.Services
{
    public class PromptEngineeringService
    {
        /// <summary>
        /// Xây dựng một prompt hoàn chỉnh cho Member dựa trên tất cả ngữ cảnh.
        /// </summary>
        /// <param name="backgroundData">Dữ liệu nền đã được xử lý từ LoadMemberOriginalDataAsync.</param>
        /// <param name="chatHistory">Lịch sử tin nhắn gần nhất.</param>
        /// <param name="currentUserMessage">Tin nhắn mới nhất của người dùng.</param>
        /// <param name="screenData">Dữ liệu màn hình (nếu có, có thể là null).</param>
        /// <returns>Một chuỗi prompt hoàn chỉnh, thân thiện với AI.</returns>
        public string BuildPromptForMember(
            string backgroundData,
            IEnumerable<Content> chatHistory,
            string currentUserMessage,
            string? screenData = null)
        {
            var promptBuilder = new StringBuilder();

            // === 1. CHỈ THỊ VAI TRÒ (SYSTEM PROMPT) ===
            promptBuilder.AppendLine("--- SYSTEM INSTRUCTION ---");
            promptBuilder.AppendLine("You are a professional, friendly, and patient TOEIC tutor AI. Your main goal is to help the student improve their score. Analyze all the provided data to give personalized advice. You must always respond in Vietnamese.");
            promptBuilder.AppendLine($"Current date is: {DateTime.Now:yyyy-MM-dd HH:mm}.");
            promptBuilder.AppendLine();

            // === 2. DỮ LIỆU NỀN CỦA HỌC VIÊN ===
            promptBuilder.AppendLine("--- STUDENT'S BACKGROUND REPORT ---");
            promptBuilder.AppendLine(backgroundData);
            promptBuilder.AppendLine();

            // === 3. LỊCH SỬ HỘI THOẠI GẦN ĐÂY ===
            promptBuilder.AppendLine("--- RECENT CONVERSATION HISTORY ---");
            foreach (var message in chatHistory)
            {
                // Đảm bảo message.Parts không rỗng
                var textPart = message.Parts?.FirstOrDefault()?.Text ?? "";
                promptBuilder.AppendLine($"{message.Role}: {textPart}");
            }
            promptBuilder.AppendLine();

            // === 4. DỮ LIỆU MÀN HÌNH (NẾU CÓ) ===
            if (!string.IsNullOrEmpty(screenData))
            {
                promptBuilder.AppendLine("--- CURRENT SCREEN CONTEXT ---");
                promptBuilder.AppendLine("The user is looking at a screen with the following information. Use this context to answer the question accurately.");
                promptBuilder.AppendLine(screenData);
                promptBuilder.AppendLine();
            }

            // === 5. CÂU HỎI MỚI NHẤT CỦA NGƯỜI DÙNG ===
            promptBuilder.AppendLine("--- USER'S NEW QUESTION ---");
            promptBuilder.AppendLine(currentUserMessage);

            return promptBuilder.ToString();
        }

        // Trong tương lai, bạn có thể thêm hàm cho Admin ở đây
        // public string BuildPromptForAdmin(...) { ... }
    }
}