using System.Security.Claims;

namespace Edemly.Server.Services
{
    /// <summary>
    /// Інтерфейс для роботи з JWT токенами
    /// </summary>
    public interface IJwtService
    {
        /// <summary>
        /// Генерує JWT токен для користувача
        /// </summary>
        string GenerateToken(int userId, string username, string email, bool isAdmin = false);

        /// <summary>
        /// Валідує токен та повертає claims
        /// </summary>
        ClaimsPrincipal? ValidateToken(string token);
    }
}