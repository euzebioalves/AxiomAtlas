namespace Axiom.Atlas.Web.Model.Users
{
    public class RegisterUserViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public Guid RoleId { get; set; }
        public IFormFile? ProfileImage { get; set; }
    }
}
