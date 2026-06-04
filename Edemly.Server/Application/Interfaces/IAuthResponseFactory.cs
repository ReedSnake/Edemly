using Edemly.Contracts.Auth;
using Edemly.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Api.Services
{
    public interface IAuthResponseFactory
    {
        Task<AuthResponseDto> CreateAuthResponseAsync(
            User user,
            LoginInfo loginInfo,
            DbContext dbContext,
            bool rotateSessionToken = true,
            Session? existingSession = null);
    }
}