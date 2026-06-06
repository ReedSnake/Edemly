#nullable disable

namespace Edemly.Client.Models
{
    public class Contact
    {
        public const string DefaultAvatarPath = "pack://application:,,,/Assets/Avatars/default-avatar.png";

        public int UserId { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string PhotoPath { get; set; }
        public string Note { get; set; }

        public string DisplayName => ResolveDisplayName(Name, Username, FirstName, LastName);

        public Contact(
            int userId,
            string name,
            string email,
            string phone = "",
            string photoPath = "",
            string username = "",
            string firstName = "",
            string lastName = "")
        {
            UserId = userId;
            Username = Normalize(username);
            FirstName = Normalize(firstName);
            LastName = Normalize(lastName);
            Name = ResolveDisplayName(name, Username, FirstName, LastName);
            Email = Normalize(email);
            Phone = Normalize(phone);
            PhotoPath = string.IsNullOrEmpty(photoPath) ? DefaultAvatarPath : photoPath;
            Note = string.Empty;
        }

        public static Contact FromUserDto(UserDto user, string displayName = "")
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return new Contact(
                user.Id,
                displayName,
                user.Email ?? string.Empty,
                user.PhoneNumber ?? string.Empty,
                user.PfpUrl,
                user.Username,
                user.FirstName,
                user.LastName);
        }

        public static Contact CreateGroup(int chatId, string name, string photoPath = "")
        {
            return new Contact(chatId, name, string.Empty, string.Empty, photoPath);
        }

        public void ApplyUserProfile(UserDto user)
        {
            if (user == null)
            {
                return;
            }

            UpdateIdentity(user.Username, user.FirstName, user.LastName);

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                Email = Normalize(user.Email);
            }

            if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                Phone = Normalize(user.PhoneNumber);
            }

            if (!string.IsNullOrWhiteSpace(user.PfpUrl))
            {
                PhotoPath = Normalize(user.PfpUrl);
            }
            else if (string.IsNullOrWhiteSpace(PhotoPath))
            {
                PhotoPath = DefaultAvatarPath;
            }

            var refreshedName = ResolveDisplayName(string.Empty, Username, FirstName, LastName);
            if (!string.IsNullOrWhiteSpace(refreshedName))
            {
                Name = refreshedName;
            }
        }

        public void UpdateIdentity(string username = null, string firstName = null, string lastName = null)
        {
            if (username != null)
            {
                Username = Normalize(username);
            }

            if (firstName != null)
            {
                FirstName = Normalize(firstName);
            }

            if (lastName != null)
            {
                LastName = Normalize(lastName);
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                Name = ResolveDisplayName(Name, Username, FirstName, LastName);
            }
        }

        public static string ResolveDisplayName(string preferredName, string username = "", string firstName = "", string lastName = "")
        {
            var normalizedPreferredName = Normalize(preferredName);
            if (!string.IsNullOrWhiteSpace(normalizedPreferredName))
            {
                return normalizedPreferredName;
            }

            var fullName = $"{Normalize(firstName)} {Normalize(lastName)}".Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            return Normalize(username);
        }

        private static string Normalize(string value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}
