using Edemly.Client.Api.Users;
using Edemly.Contracts.Users;
namespace Edemly.Client.Application.Users.Profile
{
    internal static class CurrentUserProfileState
    {
        public static string Email { get; set; } = string.Empty;
        public static string PhoneNumber { get; set; } = string.Empty;
        public static string UserName { get; set; } = string.Empty;
        public static string PfpUrl { get; set; } = string.Empty;
        public static string Description { get; set; } = string.Empty;
        public static string FirstName { get; set; } = string.Empty;
        public static string LastName { get; set; } = string.Empty;
        public static int CurrentChatIdNotification { get; set; } = -1;

        public static void Apply(UserInfoDto userInfo)
        {
            if (userInfo == null)
            {
                return;
            }

            UserName = userInfo.Username ?? string.Empty;
            Email = userInfo.Email ?? string.Empty;
            PhoneNumber = userInfo.PhoneNumber ?? string.Empty;
            PfpUrl = userInfo.PfpUrl ?? string.Empty;
            Description = userInfo.Description ?? string.Empty;
            FirstName = userInfo.FirstName ?? string.Empty;
            LastName = userInfo.LastName ?? string.Empty;
        }

        public static void Apply(UserProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            UserName = snapshot.Username;
            Email = snapshot.Email;
            PhoneNumber = snapshot.PhoneNumber;
            PfpUrl = snapshot.PfpUrl;
            Description = snapshot.Description;
            FirstName = snapshot.FirstName;
            LastName = snapshot.LastName;
        }

        public static async Task<bool> LoadFromServerAsync(IUserApiClient _apiClient)
        {
            try
            {
                var userInfo = await _apiClient.GetUserInfoAsync();

                if (userInfo != null && userInfo.Id > 0)
                {
                    Apply(userInfo);
                    return true;
                }

                return false;
            }
            catch (Exception) { return false; }
        }

        public static void Clear()
        {
            Email = string.Empty;
            PhoneNumber = string.Empty;
            UserName = string.Empty;
            PfpUrl = string.Empty;
            Description = string.Empty;
            FirstName = string.Empty;
            LastName = string.Empty;
            CurrentChatIdNotification = -1;
        }
    }
}
