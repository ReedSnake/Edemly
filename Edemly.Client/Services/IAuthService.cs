using System.Threading.Tasks;
using Edemly.Client.DTOs;

namespace Edemly.Client.Services
{
    public interface IAuthService
    {
        /// <summary>
        /// Відправити код підтвердження на email
        /// </summary>
        Task<bool> SendVerificationCodeAsync(string email);

        /// <summary>
        /// Логін з email та кодом
        /// </summary>
        Task<AuthResponseDto?> LoginWithCodeAsync(string email, string code);

        /// <summary>
        /// Реєстрація з email, кодом та username
        /// </summary>
        Task<AuthResponseDto?> RegisterWithCodeAsync(string email, string code, string username);

        /// <summary>
        /// Логін через session token
        /// </summary>
        Task<AuthResponseDto?> SessionLoginAsync(string sessionToken);

        /// <summary>
        /// Вихід з системи
        /// </summary>
        Task<bool> LogoutAsync();

        /// <summary>
        /// Зберегти дані авторизації
        /// </summary>
        void SaveAuthData(AuthResponseDto authResponse);

        /// <summary>
        /// Завантажити збережені дані авторизації
        /// </summary>
        AuthResponseDto? LoadAuthData();

        /// <summary>
        /// Очистити дані авторизації
        /// </summary>
        void ClearAuthData();
    }
}