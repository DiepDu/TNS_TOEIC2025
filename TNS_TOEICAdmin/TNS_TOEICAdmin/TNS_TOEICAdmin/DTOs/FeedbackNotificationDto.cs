namespace TNS_TOEICAdmin.Models
{
    public class FeedbackNotificationDto
    {
        public Guid FeedbackKey { get; set; }
        public Guid QuestionKey { get; set; }
        public int Part { get; set; }
        public string Member { get; set; }
        public string Summary { get; set; }
    }
}
