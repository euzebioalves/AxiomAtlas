namespace Axiom.Atlas.Application.DTOs.Users
{
    public class UserUpdateDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        // A foto será opcional na edição. 
        // Só enviaremos o Base64 se o usuário escolher uma foto nova.
        public byte[]? ProfilePicture { get; set; }
        public Guid RoleId { get; set; }
        public bool IsActive { get; set; }
    }
}
