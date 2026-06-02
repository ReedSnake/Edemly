using System.ComponentModel.DataAnnotations;

namespace uchat_server.Api.DTOs
{
    public class LoginRequestDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public required string Email { get; set; }
    }
    ///<summary>
    /// DTO для входу з кодом підтвердження
    ///</summary>

    public class LoginWithCodeDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Code is required")]
        [StringLength(10, ErrorMessage = "Code cannot exceed 10 characters")]
        public required string Code { get; set; }
    }

    public class RegistrationRequestDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        public required string Username { get; set; }
    }

    public class RegistrationWithCodeDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        public required string Username { get; set; }

        [Required(ErrorMessage = "Code is required")]
        [StringLength(10, ErrorMessage = "Code cannot exceed 10 characters")]
        public required string Code { get; set; }
    }

    public class SessionLoginDto
    {
        [Required(ErrorMessage = "Session token is required")]
        public required string SessionToken { get; set; }
    }

    public class AuthResponseDto
    {
        public required string Token { get; set; }
        public required string SessionToken { get; set; }
        public int UserId { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
    }
}