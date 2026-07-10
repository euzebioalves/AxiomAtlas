using System.ComponentModel.DataAnnotations;

namespace Axiom.Atlas.Web.Model.Login
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "O usuário é obrigatório")]
        public string Username { get; set; } = string.Empty;
        [Required(ErrorMessage = "A senha é obrigatória")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }
}
