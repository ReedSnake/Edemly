using Edemly.Server.Configuration;
using System.Collections.Concurrent;

namespace Edemly.Server.Services
{
    public class MockEmailService : IEmailService
    {
        private readonly BrevoSettings _brevoSettings;
        private readonly ILogger<MockEmailService> _logger;

        private static readonly ConcurrentDictionary<string, VerificationCode> _verificationCodes = new();

        public MockEmailService(BrevoSettings brevoSettings, ILogger<MockEmailService> logger)
        {
            _brevoSettings = brevoSettings;
            _logger = logger;
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        public Task<string> GenerateCodeAsync(string email)
        {
            var normalized = NormalizeEmail(email);
            var code = Random.Shared.Next(100000, 999999).ToString();
            var expiresAt = DateTime.UtcNow.AddMinutes(_brevoSettings.CodeExpirationMinutes);

            var verificationCode = new VerificationCode
            {
                Code = code,
                ExpirationTime = expiresAt
            };

            _verificationCodes.AddOrUpdate(normalized, verificationCode, (key, oldValue) => verificationCode);

            _logger.LogInformation("[MOCK EMAIL] Згенеровано код для {Email}", normalized);

            return Task.FromResult(code);
        }

        public Task<bool> VerifyCodeAsync(string email, string code)
        {
            var normalized = NormalizeEmail(email);

            if (!_verificationCodes.TryGetValue(normalized, out var verification))
            {
                return Task.FromResult(false);
            }

            if (DateTime.UtcNow > verification.ExpirationTime)
            {
                _verificationCodes.TryRemove(normalized, out _);
                return Task.FromResult(false);
            }

            if (verification.Code != code)
            {
                return Task.FromResult(false);
            }

            _verificationCodes.TryRemove(normalized, out _);
            return Task.FromResult(true);
        }

        public Task SendVerificationCodeAsync(string email, string code)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n==================================================");
            Console.WriteLine("   [MOCK EMAIL SERVICE] - ТЕСТОВИЙ РЕЖИМ");
            Console.WriteLine($"   Одержувач: {email}");
            Console.WriteLine($"   КОД ПІДТВЕРДЖЕННЯ: {code}");
            Console.WriteLine("==================================================\n");
            Console.ResetColor();

            return Task.CompletedTask;
        }
    }
}