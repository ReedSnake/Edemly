namespace Edemly.Server.Application.Users
{
    internal static class UsernameRules
    {
        public static string? Normalize(string? username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            return username.Trim();
        }

        public static string? Validate(string? username)
        {
            if (username == null)
            {
                return null;
            }

            if (username.Length < 3)
            {
                return "Username must be between 3 and 50 characters";
            }

            if (username.Length > 50)
            {
                return "Username cannot exceed 50 characters";
            }

            return null;
        }
    }
}