using System.Security.Claims;

namespace Edemly.Server.Services
{
    public interface IJwtService
    {
        string GenerateToken(int userId, string username, string email, bool isAdmin = false);

        ClaimsPrincipal? ValidateToken(string token);
    }
}