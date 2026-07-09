using Microsoft.AspNetCore.Http;

namespace Axiom.Atlas.API.Models.Users
{
    public class UpdateProfileRequest
    {
        public string? Username { get; set; }
        public string? FullName { get; set; }
        public IFormFile? ProfilePictureFile { get; set; }
    }
}
