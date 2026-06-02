namespace Edemly.Contracts.Users
{
    public class SearchUsersResponseDto
    {
        public List<UserDto> Users { get; set; } = new();
        public int Count { get; set; }
    }
}
