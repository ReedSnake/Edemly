namespace Edemly.Server.Services
{
    public interface IEmailService
    {
        Task<string> GenerateCodeAsync(string email);

        Task<bool> VerifyCodeAsync(string email, string code);

        Task SendVerificationCodeAsync(string email, string code);
    }
}