using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Auth
{
    public class SessionLoginDto
    {
        [Required(ErrorMessage = "Session token is required")]
        public string SessionToken { get; set; } = string.Empty;
    }
}