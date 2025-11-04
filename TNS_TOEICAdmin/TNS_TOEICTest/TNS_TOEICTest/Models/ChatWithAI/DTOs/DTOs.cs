namespace TNS_TOEICTest.Models.ChatWithAI.DTOs
{
    public class DTOs
    {
        public class MemberProfileDto
        {
            public string MemberName { get; set; }
            public string Gender { get; set; }
            public int? BirthYear { get; set; }
            public int? Age { get; set; }

            // Practice Scores
            public int? PracticeScore_Part1 { get; set; }
            public int? PracticeScore_Part2 { get; set; }
            public int? PracticeScore_Part3 { get; set; }
            public int? PracticeScore_Part4 { get; set; }
            public int? PracticeScore_Part5 { get; set; }
            public int? PracticeScore_Part6 { get; set; }
            public int? PracticeScore_Part7 { get; set; }

            // IRT & Target
            public int? ScoreTarget { get; set; }
            public float? IrtAbility { get; set; }
            public DateTime? IrtUpdatedOn { get; set; }

            // Others
            public int? ToeicScoreExam { get; set; }
            public DateTime? LastLoginDate { get; set; }
            public DateTime? CreatedOn { get; set; }
        }
        public class MemberProfileSummaryDto
        {
            public string MemberName { get; set; }
            public string Gender { get; set; }
            public int? Age { get; set; }
            public int? ScoreTarget { get; set; }
            public float? IrtAbility { get; set; }
            public int? ToeicScoreExam { get; set; }
            public DateTime? LastLoginDate { get; set; }

            // ✅ CHỈ CÓ TỔNG QUAN, KHÔNG CÓ CHI TIẾT
            public int? TotalTestsTaken { get; set; }
            public int? LatestScore { get; set; }
            public DateTime? LatestTestDate { get; set; }
        }
        public class ResultRow
        {
            public Guid ResultKey { get; set; }
            public Guid TestKey { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public int? ListeningScore { get; set; }
            public int? ReadingScore { get; set; }
            public int? TestScore { get; set; }
            public int? Time { get; set; }
            public int? TotalQuestion { get; set; }
        }

        public class UserAnswerRow
        {
            public Guid UAnswerKey { get; set; }
            public Guid ResultKey { get; set; }
            public Guid QuestionKey { get; set; }
            public Guid? SelectAnswerKey { get; set; }
            public bool IsCorrect { get; set; }
            public int TimeSpent { get; set; }
            public DateTime AnswerTime { get; set; }
            public int NumberOfAnswerChanges { get; set; }
            public int Part { get; set; }
        }

        public class UserErrorRow
        {
            public Guid ErrorKey { get; set; }
            public Guid AnswerKey { get; set; }
            public Guid UserKey { get; set; }
            public Guid ResultKey { get; set; }
            public string ErrorTypeName { get; set; }
            public string GrammarTopicName { get; set; }
            public string VocabularyTopicName { get; set; }
            public DateTime? ErrorDate { get; set; }
            public int? Part { get; set; }
            public int? SkillLevel { get; set; }
        }

        public class MistakeDetailDto
        {
            public int Part { get; set; }
            public Guid QuestionKey { get; set; }
            public Guid ResultKey { get; set; }
            public DateTime AnswerTime { get; set; }
            public int TimeSpent { get; set; }
            public int NumberOfAnswerChanges { get; set; }
            public string SelectedAnswer { get; set; }
            public string CorrectAnswer { get; set; }
            public string QuestionText { get; set; }
            public string Explanation { get; set; }
        }

        public class IncorrectDetailDto
        {
            public Guid UAnswerKey { get; set; }
            public Guid ResultKey { get; set; }
            public Guid QuestionKey { get; set; }
            public int Part { get; set; }
            public List<AnswerOptionDto> AllAnswers { get; set; } = new List<AnswerOptionDto>();

            public string QuestionImageUrl { get; set; } = "";     // Part 1, 7
            public string QuestionAudioUrl { get; set; } = "";     // Part 2
            public string ParentAudioUrl { get; set; } = "";       // Part 3, 4 parent au
            public string QuestionText { get; set; } = "";
            public string ParentText { get; set; } = "";  // ✅ THÊM MỚI
            
            public string SelectedAnswerText { get; set; } = "";
            public string CorrectAnswerText { get; set; } = "";
            public string Explanation { get; set; } = "";
            public int TimeSpentSeconds { get; set; }
            public DateTime AnswerTime { get; set; }
            public int NumberOfAnswerChanges { get; set; }
            public string GrammarTopic { get; set; } = "";
            public string VocabularyTopic { get; set; } = "";
            public string CategoryName { get; set; } = "";
            public string ErrorType { get; set; } = "";
        }
        public class AnswerOptionDto
        {
            public Guid AnswerKey { get; set; }
            public string AnswerText { get; set; } = "";
            public bool IsCorrect { get; set; }
            public bool IsSelected { get; set; }  // User đã chọn đáp án này không
        }

    }
}
