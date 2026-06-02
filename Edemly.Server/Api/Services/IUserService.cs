using Edemly.Server.Api.DTOs;

namespace Edemly.Server.Api.Services
{
    public interface IUserService
    {
        Task<(bool Success, string? Error)> CreateUser(UserCreateDto model);
        Task<(bool Success, string? Error, UserGetSelfDto? User)> GetFullInfo(int id);
        Task<(bool Success, string? Error, UserGetDto? User)> GetById(int id);
        Task<(bool Success, string? Error, List<UserGetDto>? Users)> SearchUsers(string searchQuery);
        Task<(bool Success, string? Error)> UpdateUser(int id, UserUpdateDto model);
        Task<(bool Success, string? Error)> DeleteUser(int id);
        
        // ДОДАНО: Batch отримання користувачів
        Task<(bool Success, string? Error, List<UserGetDto>? Users)> GetUsersBatch(List<int> userIds);
    }
}