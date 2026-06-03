using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Users
{
    public class UpdateUserDto
    {
        [StringLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
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
}
