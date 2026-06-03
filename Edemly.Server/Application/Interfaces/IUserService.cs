using Edemly.Contracts.Users;

namespace Edemly.Server.Api.Services
{
    public interface IUserService
    {
        Task<ServiceMessageResult> CreateUser(CreateUserDto model);
        Task<ServiceDataResult<UserInfoDto>> GetFullInfo(int id);
        Task<ServiceDataResult<UserDto>> GetById(int id);
        Task<ServiceDataResult<List<UserDto>>> SearchUsers(string searchQuery);
        Task<ServiceMessageResult> UpdateUser(int id, UpdateUserDto model);
        Task<ServiceMessageResult> DeleteUser(int currentUserId, int id);
        Task<ServiceDataResult<List<UserDto>>> GetUsersBatch(List<int> userIds);
    }
}
