#nullable enable

using System.Text.RegularExpressions;

using Edemly.Client.Application.Localization;

namespace Edemly.Client.Application.Users.Profile
{
    public static class ProfileInputValidator
    {
        private static readonly Regex UsernameRegex = new(
            @"^[\p{L}\p{N}_-]+$",
            RegexOptions.Compiled);

        private static readonly Regex PhoneRegex = new(
            @"^[0-9+\-\s()]+$",
            RegexOptions.Compiled);

        public static bool TryValidate(UpdateUserDto request, out string errorMessage)
        {
            if (!TryValidateUsername(request.Username, out errorMessage))
            {
                return false;
            }

            if (!IsValidPhone(request.PhoneNumber))
            {
                errorMessage = DefaultLanguage.PleaseEnterValidPhone;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public static bool TryValidateUsername(string? username, out string errorMessage)
        {
            var trimmedUsername = Normalize(username);

            if (string.IsNullOrWhiteSpace(trimmedUsername))
            {
                errorMessage = DefaultLanguage.PleaseEnterUsername;
                return false;
            }

            if (trimmedUsername.Length < 3 || trimmedUsername.Length > 50)
            {
                errorMessage = DefaultLanguage.UsernameLength;
                return false;
            }

            if (!UsernameRegex.IsMatch(trimmedUsername))
            {
                errorMessage = DefaultLanguage.UsernameInvalid;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public static bool IsValidPhone(string? phoneNumber)
        {
            var trimmedPhone = Normalize(phoneNumber);
            return string.IsNullOrWhiteSpace(trimmedPhone) || PhoneRegex.IsMatch(trimmedPhone);
        }

        public static string BuildDisplayName(string? firstName, string? lastName, string? username)
        {
            var fullName = $"{Normalize(firstName)} {Normalize(lastName)}".Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            return Normalize(username);
        }

        public static string BuildInitials(string? firstName, string? lastName, string? username)
        {
            var normalizedFirstName = Normalize(firstName);
            var normalizedLastName = Normalize(lastName);

            if (!string.IsNullOrWhiteSpace(normalizedFirstName) && !string.IsNullOrWhiteSpace(normalizedLastName))
            {
                return $"{normalizedFirstName[0]}{normalizedLastName[0]}".ToUpperInvariant();
            }

            if (!string.IsNullOrWhiteSpace(normalizedFirstName))
            {
                return normalizedFirstName[..Math.Min(2, normalizedFirstName.Length)].ToUpperInvariant();
            }

            var normalizedUsername = Normalize(username);
            if (!string.IsNullOrWhiteSpace(normalizedUsername))
            {
                return normalizedUsername[..Math.Min(2, normalizedUsername.Length)].ToUpperInvariant();
            }

            return string.Empty;
        }

        private static string Normalize(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}
