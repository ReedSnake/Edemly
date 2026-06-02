namespace uchat_server.Services
{
    /// <summary>
    /// Інтерфейс для відправки email та роботи з verification кодами
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Генерує та зберігає verification код
        /// </summary>
        Task<string> GenerateCodeAsync(string email);

        /// <summary>
        /// Перевіряє verification код
        /// </summary>
        Task<bool> VerifyCodeAsync(string email, string code);

        /// <summary>
        /// Відправляє verification код на email
        /// </summary>
        Task SendVerificationCodeAsync(string email, string code);
    }
}