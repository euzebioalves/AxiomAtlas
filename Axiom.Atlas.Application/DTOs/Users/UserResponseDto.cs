namespace Axiom.Atlas.Application.DTOs.Users
{
    public class UserResponseDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string ProfilePictureUrl { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // Ex: Admin, Operador, etc.
        public bool IsActive { get; set; }
    }
}
