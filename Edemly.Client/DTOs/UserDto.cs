#nullable disable
using System;

namespace Edemly.Client.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; }
        public string PfpUrl { get; set; }
        public string Description { get; set; }
    }
    public class ProfileUpdateDto
    {
        public int UserId { get; set; }
        public string PfpUrl { get; set; }
    }
    public class UserUpdateDto
    {
        public string? PhoneNumber { get; set; }
        public string? Description { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PfpUrl { get; set; }
    }
    public class UserStatusDto
    {
        public int UserId { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
    }
}