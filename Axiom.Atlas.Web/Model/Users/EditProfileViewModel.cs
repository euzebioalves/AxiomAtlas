namespace Axiom.Atlas.Web.Model.Users
{
    public class EditProfileViewModel
    {
        public string? Username { get; set; }
        public string? FullName { get; set; }
        public IFormFile? ProfilePictureFile { get; set; }
    }
}
