#nullable disable
using System.Collections.Generic;

namespace Edemly.Client.Models
{
    public class Contact
    {
        private const string DEFAULT_AVATAR_PATH = "pack://application:,,,/Assets/Avatars/default-avatar.png";

        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string PhotoPath { get; set; }
        
        public string Note { get; set; }

        public Contact(int userId, string name, string email, string phone = "", string photoPath = "")
        {
            UserId = userId;
            Name = name;
            Email = email;
            Phone = phone;
            PhotoPath = string.IsNullOrEmpty(photoPath) ? DEFAULT_AVATAR_PATH : photoPath;
            Note = string.Empty;
        }
    }
}