using Microsoft.AspNetCore.Identity;

namespace Axiom.Atlas.Domain.Entities.Users
{
    public class User : IdentityUser<Guid>
    {
        public string FullName { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public override string? PhoneNumber { get; set; } = string.Empty;
        public byte[]? ProfilePicture { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
