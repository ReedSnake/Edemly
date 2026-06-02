using System;
using System.Threading.Tasks;
using Edemly.Client.Models;
using Edemly.Client.Services.Api;

namespace Edemly.Client.Pages
{
    internal class MyInfo
    {
        public static string Name { get; set; } = string.Empty;
        public static string Email { get; set; } = string.Empty;
        public static string PhoneNumber { get; set; } = string.Empty;
        public static string UserName { get; set; } = string.Empty;
        public static string PfpUrl { get; set; } = string.Empty;
        public static string Description { get; set; } = string.Empty;
        public static string FirstName { get; set; } = string.Empty;
        public static string LastName { get; set; } = string.Empty;
        public static int currentChatIdNotification { get; set; } = -1;

        /// <summary>
        /// Завантажує інформацію користувача з сервера
        /// </summary>
        public static async Task<bool> LoadFromServerAsync(IApiService apiService)
        {
            try
            {
                var userInfo = await apiService.GetUserInfo();

                if (userInfo != null && userInfo.Id > 0)
                {
                    UserName = userInfo.Username ?? string.Empty;
                    Email = userInfo.Email ?? string.Empty;
                    PhoneNumber = userInfo.PhoneNumber ?? string.Empty;
                    PfpUrl = userInfo.PfpUrl ?? string.Empty;
                    Description = userInfo.Description ?? string.Empty;
                    FirstName = userInfo.FirstName ?? string.Empty;
                    LastName = userInfo.LastName ?? string.Empty;

                    Name = $"{FirstName} {LastName}".Trim();
                    if (string.IsNullOrEmpty(Name))
                    {
                        Name = UserName;
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Очищає всі дані
        /// </summary>
        public static void Clear()
        {
            Name = string.Empty;
            Email = string.Empty;
            PhoneNumber = string.Empty;
            UserName = string.Empty;
            PfpUrl = string.Empty;
            Description = string.Empty;
            FirstName = string.Empty;
            LastName = string.Empty;
        }
    }
}