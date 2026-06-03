using Edemly.Contracts.Users;

namespace Edemly.Server.Api.Services
{
    public interface IUserService
    {
        Task<(bool Success, string? Error)> CreateUser(CreateUserDto model);
        Task<(bool Success, string? Error, UserInfoDto? User)> GetFullInfo(int id);
        Task<(bool Success, string? Error, UserDto? User)> GetById(int id);
        Task<(bool Success, string? Error, List<UserDto> Users)> SearchUsers(string searchQuery);
        Task<(bool Success, string? Error)> UpdateUser(int id, UpdateUserDto model);
        Task<(bool Success, string? Error)> DeleteUser(int id);
        Task<(bool Success, string? Error, List<UserDto> Users)> GetUsersBatch(List<int> userIds);
    }
}
