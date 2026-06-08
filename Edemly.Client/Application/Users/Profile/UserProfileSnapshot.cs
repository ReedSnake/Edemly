#nullable enable

using Edemly.Contracts.Users;

namespace Edemly.Client.Application.Users.Profile
{
    public sealed record UserProfileSnapshot(
        string Username,
        string FirstName,
        string LastName,
        string PhoneNumber,
        string Description,
        string PfpUrl,
        string Email)
    {
        public static readonly UserProfileSnapshot Empty = new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);

        public static UserProfileSnapshot From(UserInfoDto userInfo)
        {
            return new UserProfileSnapshot(
                Normalize(userInfo?.Username),
                Normalize(userInfo?.FirstName),
                Normalize(userInfo?.LastName),
                Normalize(userInfo?.PhoneNumber),
                Normalize(userInfo?.Description),
                Normalize(userInfo?.PfpUrl),
                Normalize(userInfo?.Email));
        }

        public static UserProfileSnapshot From(UpdateUserDto request, string? email)
        {
            return new UserProfileSnapshot(
                Normalize(request?.Username),
                Normalize(request?.FirstName),
                Normalize(request?.LastName),
                Normalize(request?.PhoneNumber),
                Normalize(request?.Description),
                Normalize(request?.PfpUrl),
                Normalize(email));
        }

        public bool Matches(UpdateUserDto request)
        {
            return Username == Normalize(request?.Username)
                && FirstName == Normalize(request?.FirstName)
                && LastName == Normalize(request?.LastName)
                && PhoneNumber == Normalize(request?.PhoneNumber)
                && Description == Normalize(request?.Description)
                && PfpUrl == Normalize(request?.PfpUrl);
        }

        private static string Normalize(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}
