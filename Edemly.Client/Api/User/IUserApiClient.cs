using Edemly.Client.Application.Users.Profile;
using Edemly.Contracts.Users;

namespace Edemly.Client.Api.Users;

public interface IUserApiClient
{
    Task<List<UserDto>> SearchUsersAsync(string query);

    Task<UserDto?> GetUserByIdAsync(int userId);

    Task<UserInfoDto> GetUserInfoAsync();

    Task<(bool Success, string? Error)> UpdateUserInfoAsync(UpdateUserDto request);
}