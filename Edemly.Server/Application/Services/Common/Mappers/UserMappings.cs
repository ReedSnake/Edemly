using System.Linq.Expressions;
using Edemly.Contracts.Users;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Api.Services
{
    public static class UserMappings
    {
        public static readonly Expression<Func<User, UserDto>> SearchProjection = user => new UserDto
        {
            Id = user.Id,
            Username = user.Username ?? string.Empty,
            Email = user.LoginInfo.Email,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            PfpUrl = user.PfpUrl ?? string.Empty,
            Description = user.Description ?? string.Empty
        };

        public static readonly Expression<Func<User, UserDto>> BatchProjection = user => new UserDto
        {
            Id = user.Id,
            Username = user.Username ?? string.Empty,
            PfpUrl = user.PfpUrl ?? string.Empty,
            Description = user.Description ?? string.Empty
        };

        public static UserInfoDto ToInfoDto(User user)
        {
            return new UserInfoDto
            {
                Id = user.Id,
                Username = user.Username ?? string.Empty,
                Email = user.LoginInfo.Email,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Location = user.Location ?? string.Empty,
                Description = user.Description ?? string.Empty,
                PfpUrl = user.PfpUrl ?? string.Empty,
                CreatedAt = user.CreatedAt,
                SubscriptionStatus = user.SubscriptionStatus.ToString(),
                SubscriptionExpiration = user.SubscriptionExpiration
            };
        }

        public static UserDto ToDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Username = user.Username ?? string.Empty,
                Email = user.LoginInfo?.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                PfpUrl = user.PfpUrl ?? string.Empty,
                Description = user.Description ?? string.Empty
            };
        }
    }
}
