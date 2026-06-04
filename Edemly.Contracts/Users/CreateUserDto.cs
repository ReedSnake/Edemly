using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Users
{
    public class CreateUserDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
        public string? Username { get; set; }
    }
}