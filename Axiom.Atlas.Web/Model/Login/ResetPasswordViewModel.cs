using System.ComponentModel.DataAnnotations;

namespace Axiom.Atlas.Web.Model.Login
{
    public class ResetPasswordViewModel
    {
        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "O token de recuperação é obrigatório.")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "A nova senha é obrigatória.")]
        [MinLength(6, ErrorMessage = "A senha deve ter no mínimo 6 caracteres.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "A confirmação da senha é obrigatória.")]
        [Compare("NewPassword", ErrorMessage = "As senhas não coincidem. Tente novamente.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
