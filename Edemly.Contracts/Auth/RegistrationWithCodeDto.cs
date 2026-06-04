using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Auth
{
    public class RegistrationWithCodeDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
        public string? Username { get; set; }

        [Required(ErrorMessage = "Code is required")]
        [StringLength(10, ErrorMessage = "Code cannot exceed 10 characters")]
        public string Code { get; set; } = string.Empty;
    }
}