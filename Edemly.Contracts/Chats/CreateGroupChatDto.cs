namespace Edemly.Contracts.Chats
{
    public class CreateGroupChatDto
    {
        public string GroupName { get; set; } = string.Empty;
        public List<int> ParticipantIds { get; set; } = new();
    }
}
