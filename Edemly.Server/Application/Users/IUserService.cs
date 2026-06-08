using Edemly.Contracts.Users;
using Edemly.Server.Application.Common;

namespace Edemly.Server.Application.Users
{
    public interface IUserService
    {
        Task<ServiceResult> CreateAsync(CreateUserDto request);

        Task<ServiceResult<UserInfoDto>> GetFullInfoAsync(int currentUserId);

        Task<ServiceResult<UserDto>> GetByIdAsync(int targetUserId);

        Task<ServiceResult<List<UserDto>>> SearchUsersAsync(string searchQuery);

        Task<ServiceResult> UpdateAsync(int currentUserId, UpdateUserDto request);

        Task<ServiceResult> DeleteAsync(int requesterId, int targetUserId);

        Task<ServiceResult<List<UserDto>>> GetUsersBatchAsync(List<int> targetUserIds);
    }
}