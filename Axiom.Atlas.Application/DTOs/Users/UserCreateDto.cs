namespace Axiom.Atlas.Application.DTOs.Users
{
    public class UserCreateDto
    {
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public byte[]? ProfilePicture { get; set; }
        public Guid RoleId { get; set; }
    }
}
