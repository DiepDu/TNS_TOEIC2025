using Google.Cloud.AIPlatform.V1;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TNS_TOEICTest.Services
{
    public class PromptEngineeringService
    {
        public string BuildPromptForMember(
            string backgroundData,
             string recentFeedbacks,
            IEnumerable<Content> chatHistory,
            string currentUserMessage)
        {
            var promptBuilder = new StringBuilder();
            TimeZoneInfo vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            DateTime vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);
            string formattedVietnamTime = vietnamTime.ToString("'Thứ' dddd, HH:mm:ss 'ngày' dd/MM/yyyy '(GMT+7)'", new CultureInfo("vi-VN"));


            // === 1. SỬ DỤNG THẺ CẤU TRÚC CHO CÁC CHỈ THỊ CỐT LÕI ===
            promptBuilder.AppendLine("<core_instructions>");
            promptBuilder.AppendLine("You are \"Mr. TOEIC\", an AI assistant and professional TOEIC tutor, deeply integrated into our test preparation website system. You are not a regular chatbot; you are an intelligent companion who is always understanding and dedicated to each student.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("**INTERACTION CONTEXT:**");
            promptBuilder.AppendLine("- Your Position: You are chatting with a student via a chat window directly on the practice website. The student might be reviewing their results or reading study materials.");
            promptBuilder.AppendLine("- Your Data Access: You have access to the student's academic profile, recent score data, recent errors, and their latest feedback/questions about specific test items.");
            promptBuilder.AppendLine($"- Current Time: {formattedVietnamTime}");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("**YOUR PRIMARY MISSIONS:**");
            promptBuilder.AppendLine("1. Personalized Tutoring: Your most important mission is to use the provided data—including student information, the screen the student is currently viewing (if available), and chat history (the last 10 exchanges)—as a foundation for reasoning, thinking, and providing the most logical answer to the student's current question. Answer what they ask based on the data you have; do not provide redundant or rambling information unless they ask for it.");
            promptBuilder.AppendLine("2. In-depth Q&A: Answer all student questions related to the TOEIC test, vocabulary, grammar, and test-taking strategies. Always try to connect your answers to their own academic data to be more persuasive.");
            promptBuilder.AppendLine("3. Attitude: Maintain a friendly, patient, and positive attitude. Be an inspiring teacher who helps students not get discouraged on their TOEIC journey. However, you must also be strict and stern enough to seriously remind the student when needed. Occasionally, you can be playful, friendly, and joke.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("**MANDATORY RULES:**");
            promptBuilder.AppendLine("- Language: Whatever language the student uses for their question, you must respond in that same language.");
            promptBuilder.AppendLine("- Concise and Focused: Get straight to the point. Avoid long, generic answers that provide no value.");
            promptBuilder.AppendLine("- Data-Driven: When making comments about a student's abilities, always base them on the provided \"Academic Report\". Do not speculate.");
            promptBuilder.AppendLine("- Image Analysis: If an image or file is attached to a user's message, YOU MUST analyze the image or file and use it as the main context to answer their question. If possible, relate the image content to TOEIC knowledge (e.g. describe the image, identify grammar points, etc.).");
            promptBuilder.AppendLine("</core_instructions>");
            promptBuilder.AppendLine();

            // === 2. SỬ DỤNG THẺ CẤU TRÚC CHO DỮ LIỆU HỌC VIÊN ===
            promptBuilder.AppendLine("<academic_report>");
            promptBuilder.AppendLine(backgroundData);
            promptBuilder.AppendLine("</academic_report>");
            promptBuilder.AppendLine();

            if (!string.IsNullOrEmpty(recentFeedbacks))
            {
                promptBuilder.AppendLine("<student_recent_feedbacks>");
                promptBuilder.AppendLine("Below are the student's most recent questions or complaints about specific test items. Use this to understand their pain points.");
                promptBuilder.AppendLine(recentFeedbacks);
                promptBuilder.AppendLine("</student_recent_feedbacks>");
                promptBuilder.AppendLine();
            }

            // === 3. SỬ DỤNG THẺ CẤU TRÚC CHO LỊCH SỬ CHAT ===
            promptBuilder.AppendLine("<conversation_history>");
            foreach (var message in chatHistory)
            {
                var textPart = message.Parts?.FirstOrDefault()?.Text ?? "";
                // Để rõ ràng hơn, chúng ta có thể dùng thẻ cho từng vai trò
                promptBuilder.AppendLine($"<{message.Role.ToLower()}>{textPart}</{message.Role.ToLower()}>");
            }
            promptBuilder.AppendLine("</conversation_history>");
            promptBuilder.AppendLine();

        

            // === 5. CÂU HỎI MỚI VÀ MỆNH LỆNH CUỐI CÙNG ===
            promptBuilder.AppendLine("<user_new_question>");
            promptBuilder.AppendLine(currentUserMessage);
            promptBuilder.AppendLine("</user_new_question>");
            promptBuilder.AppendLine();

            // Mệnh lệnh cuối cùng để tái tập trung AI vào đúng nhiệm vụ
            promptBuilder.AppendLine("Based on all the provided instructions and data, analyze the user's new question and provide a concise, personalized, and helpful response now.");

            return promptBuilder.ToString();
        }

        // File: Services/PromptEngineeringService.cs

        public string BuildPromptForAdmin(
              string adminBackgroundData,
              IEnumerable<Content> chatHistory,
              string currentUserMessage)
        {
            var promptBuilder = new StringBuilder();
            var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
            string formattedVietnamTime = vietnamTime.ToString("'Thứ' dddd, HH:mm:ss 'ngày' dd/MM/yyyy '(GMT+7)'", new CultureInfo("vi-VN"));

            promptBuilder.AppendLine("<core_instructions>");
            promptBuilder.AppendLine("You are a professional AI Admin Assistant. Your mission is to help administrators by querying data from the database.");
            promptBuilder.AppendLine($"- Current Time: {formattedVietnamTime}");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("**FUNCTION CALLING RULES:**");
            promptBuilder.AppendLine("When the admin asks for data, you MUST respond ONLY with a JSON object in the format: {\"functionCall\": {\"name\": \"function_name\", \"args\": {\"arg_name\": \"value\"}}}");
            promptBuilder.AppendLine("Do not add any other text outside of this JSON object.");
            promptBuilder.AppendLine("The available functions are:");
            promptBuilder.AppendLine("1. `get_member_summary`: Get a member's profile. Arguments: {\"member_identifier\": \"<member_id_or_name>\"}");
            promptBuilder.AppendLine("2. `count_questions_by_part`: Count questions in a TOEIC part. Arguments: {\"part_number\": <part_number>}");

            promptBuilder.AppendLine();
            promptBuilder.AppendLine("**MULTI-CALL RULE:**");
            promptBuilder.AppendLine("- If the admin asks for the *total number of questions across the whole bank*, you MUST call `count_questions_by_part` for each part (1 through 7).");
            promptBuilder.AppendLine("- After you receive the results for each part, you MUST sum them together and provide the final total.");
            promptBuilder.AppendLine("- Therefore, you may need to call the same function multiple times in sequence before giving the final answer.");
            promptBuilder.AppendLine("</core_instructions>");
            promptBuilder.AppendLine();

            promptBuilder.AppendLine("<admin_profile>");
            promptBuilder.AppendLine(adminBackgroundData);
            promptBuilder.AppendLine("</admin_profile>");
            promptBuilder.AppendLine();

            promptBuilder.AppendLine("<conversation_history>");
            foreach (var message in chatHistory)
            {
                var textPart = message.Parts?.FirstOrDefault()?.Text ?? "";
                promptBuilder.AppendLine($"<{message.Role.ToLower()}>{textPart}</{message.Role.ToLower()}>");
            }
            promptBuilder.AppendLine("</conversation_history>");
            promptBuilder.AppendLine();

            promptBuilder.AppendLine("<admin_new_question>");
            promptBuilder.AppendLine(currentUserMessage);
            promptBuilder.AppendLine("</admin_new_question>");

            return promptBuilder.ToString();
        }
    
}
}