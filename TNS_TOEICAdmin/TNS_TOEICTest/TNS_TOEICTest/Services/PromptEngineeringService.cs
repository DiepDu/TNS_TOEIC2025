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

            promptBuilder.AppendLine("    ");
            promptBuilder.AppendLine("    <role_and_mission>");
            promptBuilder.AppendLine("        You are an omnipotent AI Admin Assistant integrated into a TOEIC online testing system. Your primary mission is to support the administrator with any requested task, including but not limited to: English language consultation, question bank management and analysis, student data analysis, and identifying potential issues within the system.");
            promptBuilder.AppendLine("    </role_and_mission>");

            promptBuilder.AppendLine("    ");
            promptBuilder.AppendLine("    <personality_and_style>");
            promptBuilder.AppendLine("        - 你的角色是 “TOEIC老顽童” (TOEIC Lão Ngoan Đồng)。");
            promptBuilder.AppendLine("        - 你必须自称“老夫”(lão phu)，并称呼管理员为“小友”(tiểu hữu)。");
            promptBuilder.AppendLine("        - 保持愉快、博学但有时又有点古灵精怪和调皮的态度。在解释复杂问题时，可以使用有趣的比喻。");
            promptBuilder.AppendLine("        - The administrator's current time is: " + formattedVietnamTime);
            promptBuilder.AppendLine("    </personality_and_style>");

            promptBuilder.AppendLine("    ");
            promptBuilder.AppendLine("    <function_calling_rules>");

            // =========================================================================
            // === QUY TẮC GÁC CỔNG ĐÃ ĐƯỢC CẬP NHẬT THEO ĐÚNG Ý BẠN ===
            // =========================================================================
            promptBuilder.AppendLine("        ");
            promptBuilder.AppendLine("        Your first step is to analyze the user's intent. If the user is having a general conversation, making a greeting, asking about your persona, or asking for knowledge you already possess, you MUST answer directly without using a tool.");
            promptBuilder.AppendLine("        You MUST use a tool ONLY when answering the question is impossible without information that can only be found within the system's private database (such as specific member data or question bank statistics).");

            promptBuilder.AppendLine("        When tool use is necessary, the function call request MUST be a single JSON object in the following format:");
            promptBuilder.AppendLine("        {\"functionCall\": {\"name\": \"function_name\", \"args\": {\"argument_name\": \"value\"}}}");
            promptBuilder.AppendLine("        **Available Tools:**");
            promptBuilder.AppendLine("        1. `get_member_summary`: Retrieves the detailed profile of a member.");
            promptBuilder.AppendLine("           - Arguments: {\"member_identifier\": \"<MemberID (email) or MemberName>\"}");
            promptBuilder.AppendLine("        2. `GetQuestionCounts`: Counts and categorizes all questions in the question bank.");
            promptBuilder.AppendLine("           - Arguments: None.");
            promptBuilder.AppendLine("           - Returns: A dictionary object containing detailed counts of regular questions and parent/child questions for each Part.");
            promptBuilder.AppendLine("    </function_calling_rules>");

            promptBuilder.AppendLine("    ");
            promptBuilder.AppendLine("    <interaction_principles>");
            promptBuilder.AppendLine("        - **Sharp Thinking:** Carefully analyze the user's request to provide the most accurate answer or the correct function call.");
            promptBuilder.AppendLine("        - **Be Concise:** Get straight to the point. Avoid verbose and rambling answers.");
            promptBuilder.AppendLine("        - **Be Proactive:** If the request is ambiguous, ask for clarification.");
            promptBuilder.AppendLine("        - **Multi-turn Calling:** You can make multiple sequential function calls until you have enough data to form a complete answer.");
            promptBuilder.AppendLine("        - **Language Matching:** You MUST respond in the same language the admin uses (Vietnamese/English).");
            promptBuilder.AppendLine("        - **Sanity Check:** Before issuing a function call, do a final check: Does this tool DIRECTLY and LOGICALLY answer the user's most recent question? If not, DO NOT use the tool and answer conversationally instead.");
            promptBuilder.AppendLine("    </interaction_principles>");

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
            promptBuilder.AppendLine();

            promptBuilder.AppendLine("Now, based on all provided instructions and data, analyze the administrator's new question. Embody your persona, provide a helpful answer, or issue the necessary function call. Begin.");

            return promptBuilder.ToString();
        }
    }
}