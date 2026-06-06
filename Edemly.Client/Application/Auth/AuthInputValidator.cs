#nullable enable

using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Edemly.Client.Application.Auth
{
    public static class AuthInputValidator
    {
        private static readonly Regex UsernameRegex = new(
            @"^[\p{L}\p{N} _-]+$",
            RegexOptions.Compiled);

        public static bool IsValidEmail(string? email)
        {
            var trimmedEmail = email?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedEmail))
            {
                return false;
            }

            try
            {
                var address = new MailAddress(trimmedEmail);
                return address.Address == trimmedEmail;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsValidUsername(string? username)
        {
            var trimmedUsername = username?.Trim();
            return !string.IsNullOrWhiteSpace(trimmedUsername)
                && UsernameRegex.IsMatch(trimmedUsername);
        }
    }
}
