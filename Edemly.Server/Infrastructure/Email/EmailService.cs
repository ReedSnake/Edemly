using Edemly.Server.Configuration;
using sib_api_v3_sdk.Api;
using sib_api_v3_sdk.Model;
using System.Collections.Concurrent;
using BrevoConfig = sib_api_v3_sdk.Client.Configuration;

namespace Edemly.Server.Services
{
    internal class VerificationCode
    {
        public string Code { get; set; } = string.Empty;
        public DateTime ExpirationTime { get; set; }
    }

    public class EmailService : IEmailService
    {
        private readonly BrevoSettings _brevoSettings;
        private readonly TransactionalEmailsApi _brevoApi;
        private readonly ILogger<EmailService> _logger;

        private static readonly ConcurrentDictionary<string, VerificationCode> _verificationCodes = new();

        public EmailService(
            BrevoSettings brevoSettings,
            ILogger<EmailService> logger)
        {
            _brevoSettings = brevoSettings;
            _logger = logger;

            BrevoConfig.Default.ApiKey["api-key"] = _brevoSettings.ApiKey;
            _brevoApi = new TransactionalEmailsApi();
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        public System.Threading.Tasks.Task<string> GenerateCodeAsync(string email)
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

            _logger.LogInformation("Generated verification code for {Email} (normalized: {NormalizedEmail})", email, normalized);

            return System.Threading.Tasks.Task.FromResult(code);
        }

        public System.Threading.Tasks.Task<bool> VerifyCodeAsync(string email, string code)
        {
            var normalized = NormalizeEmail(email);

            if (!_verificationCodes.TryGetValue(normalized, out var verification))
            {
                _logger.LogWarning("Verification code not found for {Email} (normalized: {NormalizedEmail})", email, normalized);
                return System.Threading.Tasks.Task.FromResult(false);
            }

            if (DateTime.UtcNow > verification.ExpirationTime)
            {
                _logger.LogWarning("Verification code expired for {Email} (normalized: {NormalizedEmail})", email, normalized);
                _verificationCodes.TryRemove(normalized, out _);
                return System.Threading.Tasks.Task.FromResult(false);
            }

            if (verification.Code != code)
            {
                _logger.LogWarning("Invalid verification code for {Email} (normalized: {NormalizedEmail}). Provided: {ProvidedCode}, Expected: {ExpectedCode}", email, normalized, code, verification.Code);
                return System.Threading.Tasks.Task.FromResult(false);
            }

            _verificationCodes.TryRemove(normalized, out _);
            _logger.LogInformation("Verification code verified for {Email} (normalized: {NormalizedEmail})", email, normalized);

            return System.Threading.Tasks.Task.FromResult(true);
        }

        public async System.Threading.Tasks.Task SendVerificationCodeAsync(string email, string code)
        {
            try
            {
                var sender = new SendSmtpEmailSender(
                    _brevoSettings.SenderName,
                    _brevoSettings.SenderEmail
                );

                var to = new List<SendSmtpEmailTo>
                {
                    new SendSmtpEmailTo(email)
                };

                var htmlContent = GenerateEmailHtml(code);

                var sendSmtpEmail = new SendSmtpEmail(
                    sender: sender,
                    to: to,
                    htmlContent: htmlContent,
                    subject: "Verify your Email - " + _brevoSettings.ServiceName
                );

                var result = await _brevoApi.SendTransacEmailAsync(sendSmtpEmail);

                _logger.LogInformation(
                    "Email sent successfully to {Email}. Message ID: {MessageId}",
                    email,
                    result.MessageId
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send email to {Email}. Error: {Message}",
                    email,
                    ex.Message
                );
                throw new InvalidOperationException("Failed to send verification email", ex);
            }
        }

        private string GenerateEmailHtml(string code)
        {
            var logoSrc = _brevoSettings.LogoUrl;

            return $@"
                <html>
                <head>
                    <meta charset=""utf-8"">
                </head>
                <body style='font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px;'>

                    <div style='max-width: 580px; margin: 0 auto; background-color: #ffffff; border: 1px solid #e0e0e0; border-radius: 8px; padding: 40px;'>

                        <div style='display: flex; align-items: center; gap: 15px;'>

                            <table cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border-collapse: collapse;"">
                              <tr>
                                <td style=""vertical-align: middle;"">

                                  <img src=""{logoSrc}"" alt=""Logo"" style=""width: 64px; height: 64px; display: block;"">

                                </td>

                                <td style=""width: 15px;""></td>

                                <td style=""vertical-align: middle;"">

                                  <h1 style=""color: #338f89; margin: 0; font-size: 28px;"">{_brevoSettings.ServiceName}</h1>

                                </td>
                              </tr>
                            </table>

                        </div>

                        <hr style='border: 0; border-top: 1px solid #eeeeee; margin: 20px 0;'>

                        <h2 style='color: #333333; margin-top: 0;'>Verify your email address</h2>

                        <p style='font-size: 16px; color: #555555; line-height: 1.5;'>
                            You need to verify your email address to continue using your {_brevoSettings.ServiceName} account. Enter the following code to verify your email address:
                        </p>

                        <div style='font-size: 32px; font-weight: bold; color: #111111; letter-spacing: 4px; background-color: #f9f9f9; padding: 20px; border-radius: 5px; text-align: center; margin: 30px 0;'>
                            {code}
                        </div>

                        <p style='font-size: 16px; color: #555555; line-height: 1.5; text-align: center;'>
                            This code is valid for {_brevoSettings.CodeExpirationMinutes} minutes.
                        </p>

                        <p style='font-size: 16px; color: #555555; line-height: 1.5; text-align: center;'>
                            If you did not request a code, simply ignore this email.
                        </p>

                        <hr style='border: 0; border-top: 1px solid #eeeeee; margin: 20px 0;'>

                        <p style='font-size: 12px; color: #999999; text-align: center; margin: 0;'>
                            � 2025 {_brevoSettings.ServiceName}. All rights reserved.
                        </p>

                    </div>
                </body>
                </html>";
        }
    }
}