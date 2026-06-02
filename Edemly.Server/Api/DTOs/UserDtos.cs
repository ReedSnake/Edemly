using System.ComponentModel.DataAnnotations;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Api.DTOs
{
    public class UserCreateDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        public required string Username { get; set; }
    }

    public class UserUpdateDto
    {
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        public string? Username { get; set; }

        [StringLength(100)]
        public string? FirstName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        [StringLength(25)]
        public string? PhoneNumber { get; set; }

        [StringLength(255)]
        public string? Location { get; set; }

        [StringLength(255)]
        public string? Description { get; set; }

        public string? PfpUrl { get; set; }
    }

    public class UserGetSelfDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Location { get; set; }
        public string? Description { get; set; }
        public string? PfpUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        // Return subscription as string (e.g. "Free", "Premium") so clients expecting string can parse
        public string SubscriptionStatus { get; set; } = string.Empty;
        public DateTime? SubscriptionExpiration { get; set; }
    }

    // ОНОВЛЕНО: Додано Email та PhoneNumber
    public class UserGetDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? PfpUrl { get; set; }
        public string? Description { get; set; }
    }
}