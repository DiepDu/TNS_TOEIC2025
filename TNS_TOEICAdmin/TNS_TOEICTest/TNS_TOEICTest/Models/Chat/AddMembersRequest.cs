namespace TNS_TOEICTest.Models.Chat
{

    public class NewMemberInfo
    {
        public string UserKey { get; set; }
        public string UserType { get; set; }
        public string UserName { get; set; }
    }

    public class AddMembersRequest
    {
        public string ConversationKey { get; set; }
        public List<NewMemberInfo> NewMembers { get; set; }
    }

}
