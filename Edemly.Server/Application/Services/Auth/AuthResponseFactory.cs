using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Edemly.Contracts.Auth;
using Edemly.Server.Configuration;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;

namespace Edemly.Server.Api.Services
{
    public class AuthResponseFactory : IAuthResponseFactory
    {
        private readonly JwtSettings _jwtSettings;
        private readonly IJwtService _jwtService;
        private readonly IConfiguration _configuration;

        public AuthResponseFactory(
            JwtSettings jwtSettings,
            IJwtService jwtService,
            IConfiguration configuration)
        {
            _jwtSettings = jwtSettings;
            _jwtService = jwtService;
            _configuration = configuration;
        }

        public async Task<AuthResponseDto> CreateAuthResponseAsync(
            User user,
            LoginInfo loginInfo,
            DbContext dbContext,
            bool rotateSessionToken = true,
            Session? existingSession = null)
        {
            user.LastOnline = DateTime.UtcNow;
            try
            {
                dbContext.Update(user);
                await dbContext.SaveChangesAsync();
            }
            catch
            {
                // Ignore detached update issues and continue with session/token generation.
            }

            var sessions = dbContext.Set<Session>();
            var session = existingSession ?? await sessions.FirstOrDefaultAsync(item => item.UserId == user.Id);

            if (session == null)
            {
                session = new Session
                {
                    UserId = user.Id,
                    SessionToken = Guid.NewGuid().ToString(),
                    ExpirationTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiresInDays)
                };
                sessions.Add(session);
            }
            else if (rotateSessionToken)
            {
                session.SessionToken = Guid.NewGuid().ToString();
                session.ExpirationTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiresInDays);
            }

            await dbContext.SaveChangesAsync();

            var adminEmail = _configuration["AdminEmail"];
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                adminEmail = "admin@edemly.com";
            }

            var isAdmin = string.Equals(loginInfo.Email, adminEmail, StringComparison.OrdinalIgnoreCase);
            var username = user.Username ?? string.Empty;
            var token = _jwtService.GenerateToken(user.Id, username, loginInfo.Email, isAdmin);

            return new AuthResponseDto
            {
                Token = token,
                SessionToken = session.SessionToken,
                UserId = user.Id,
                Username = username,
                Email = loginInfo.Email
            };
        }
    }
}
