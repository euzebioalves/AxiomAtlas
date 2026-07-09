using Audit.Core;
using Axiom.Atlas.Domain.Entities.Users;
using Axiom.Atlas.Application.DTOs.Auth;
using Axiom.Atlas.Application.DTOs.Users;
using Axiom.Atlas.Domain.Interfaces.Auth;
using Axiom.Atlas.Domain.Interfaces.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Claims;
using System.Text;

namespace Axiom.Atlas.API.Controllers.Auth
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly UserManager<User> _userManager;
        private readonly IEmailService _emailService;

        public AuthController(IAuthService authService, UserManager<User> userManager, IEmailService emailService)
        {
            _authService = authService;
            _userManager = userManager;
            _emailService = emailService;
        }

        //[HttpPost("login")]
        //public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        //{
        //    var authResult = await _authService.AuthenticateAsync(model.Username, model.Password);

        //    if (authResult == null)
        //        return Unauthorized(new { message = "Credenciais inválidas" });

        //    if (!authResult.IsActive)
        //    {
        //        return Unauthorized(new { message = "Esta conta está desativada. Entre em contato com o administrador do Axiom Atlas." });
        //    }

        //    return Ok(authResult);
        //}

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest model)
        {
            // Criamos o escopo de auditoria para monitorar esta tentativa de acesso
            using (var scope = AuditScope.Create("LoginEvent", () => new { Username = model.Username }))
            {
                var authResult = await _authService.AuthenticateAsync(model.Username, model.Password);

                // Caso 1: Usuário ou senha não encontrados/errados
                if (authResult == null)
                {
                    scope.SetCustomField("Status", "Falha");
                    scope.SetCustomField("Motivo", "Credenciais inválidas");
                    // Força a gravação imediata do log para garantir que esta tentativa seja registrada, mesmo que o processo de login seja interrompido aqui
                    try
                    {
                        await scope.SaveAsync();
                    }
                    catch (Exception ex)
                    {
                        // Se cair aqui, o erro vai aparecer no console da API!
                        Console.WriteLine($"[ERRO AUDIT]: {ex.Message}");
                    }
                    return Unauthorized(new { message = "Credenciais inválidas" });
                }

                // Caso 2: Credenciais ok, mas a trava de segurança barrou (Status Inativo)
                if (!authResult.IsActive)
                {
                    scope.SetCustomField("Status", "Bloqueado");
                    scope.SetCustomField("Motivo", "Conta Inativa");
                    // Força a gravação imediata do log para garantir que esta tentativa seja registrada, mesmo que o processo de login seja interrompido aqui
                    try
                    {
                        await scope.SaveAsync();
                    }
                    catch (Exception ex)
                    {
                        // Se cair aqui, o erro vai aparecer no console da API!
                        Console.WriteLine($"[ERRO AUDIT]: {ex.Message}");
                    }
                    return Unauthorized(new { message = "Esta conta está desativada. Entre em contato com o administrador do Axiom Atlas." });
                }

                // Caso 3: Sucesso total
                scope.SetCustomField("Status", "Sucesso");
                // Força a gravação imediata do log para garantir que esta tentativa seja registrada antes de retornar a resposta
                try
                {
                    await scope.SaveAsync();
                }
                catch (Exception ex)
                {
                    // Se cair aqui, o erro vai aparecer no console da API!
                    Console.WriteLine($"[ERRO AUDIT]: {ex.Message}");
                }
                return Ok(authResult);
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest model)
        {
            // 1. Busca o usuário pelo e-mail
            var user = await _userManager.FindByEmailAsync(model.Email);

            // 2. Proteção contra Enumeração de Usuários (Sempre retorna Ok)
            if (user == null || !user.IsActive)
            {
                return Ok(new { message = "Se o e-mail existir em nossa base, um link de recuperação será enviado em instantes." });
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return Ok(new { message = "Se o e-mail existir em nossa base, um link de recuperação será enviado em instantes." });
            }

            // 3. Gera o Token e codifica para navegação web segura
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            // 4. Monta a URL que o usuário vai clicar no e-mail (Ajuste a URL do seu Front)
            var frontendUrl = "https://localhost:7204"; // Mude para a porta do seu projeto Web
            var resetLink = $"{frontendUrl}/Auth/ResetPassword?email={model.Email}&token={encodedToken}";

            // 5. Template Metronic Adaptado
            // ATENÇÃO: Substitua as URLs das tags <img> pelas URLs completas de onde suas imagens estarão hospedadas
            var htmlBody = $@"
                <div style='background-color:#D5D9E2; font-family:Arial,Helvetica,sans-serif; line-height: 1.5; font-weight: normal; font-size: 15px; color: #2F3044; margin:0; padding: 40px 20px; width:100%;'>    
                    <div style='background-color:#ffffff; padding: 45px 0 34px 0; border-radius: 24px; margin:0 auto; max-width: 600px;'>        
                        <table align='center' border='0' cellpadding='0' cellspacing='0' width='100%' height='auto' style='border-collapse:collapse'>            
                            <tbody>                                      
                                <tr>                    
                                    <td align='center' valign='center' style='text-align:center; padding-bottom: 10px'>                        
                                        <div style='text-align:center; margin:0 60px 34px 60px'>                                                            
                            
                                            <div style='margin-bottom: 10px'>                                
                                                <img alt='Logo Axiom Atlas' src='{frontendUrl}/metronic8/images/logo-1.svg' style='height: 35px'/>                            
                                            </div>                            
                                            <div style='margin-bottom: 15px'>                                
                                                <img alt='Icon' src='{frontendUrl}/metronic8/images/icon-positive-vote-2.svg'/>                             
                                            </div>                                                         
                            
                                            <div style='font-size: 14px; font-weight: 500; margin-bottom: 27px; font-family:Arial,Helvetica,sans-serif;'>                                
                                                <p style='margin-bottom:9px; color:#181C32; font-size: 22px; font-weight:700'>Olá, {user.FullName}!</p>                                
                                                <p style='margin-bottom:2px; color:#7E8299'>Recebemos uma solicitação para redefinir a senha da sua conta.</p>                                
                                                <p style='margin-bottom:2px; color:#7E8299'>Clique no botão abaixo para escolher uma nova senha.</p>                              
                                            </div>                                                              
                            
                                            <a href='{resetLink}' target='_blank' style='background-color:#50cd89; border-radius:6px;display:inline-block; padding:11px 19px; color: #FFFFFF; font-size: 14px; font-weight:500; font-family:Arial,Helvetica,sans-serif; text-decoration: none;'>                                
                                                Redefinir Minha Senha                            
                                            </a>                                                         
                                        </div>                    
                                    </td>                
                                </tr>                  
                                <tr>                    
                                    <td align='center' valign='center' style='font-size: 13px; padding:0 15px; text-align:center; font-weight: 500; color: #A1A5B7;font-family:Arial,Helvetica,sans-serif'>                                                    
                                        <p>&copy; Copyright Axiom Atlas.</p>                                             
                                    </td>                
                                </tr>                  
                            </tbody>           
                        </table>     
                    </div>
                </div>";

            // 6. Envia o E-mail
            try
            {
                await _emailService.SendEmailAsync(user.Email, "Recuperação de Senha - Axiom Atlas", htmlBody);
            }
            catch (Exception ex)
            {
                // Se a configuração do Gmail falhar, cai aqui
                return StatusCode(500, new { message = "Erro ao enviar o e-mail de recuperação. Verifique as configurações do servidor SMTP.", error = ex.Message });
            }

            return Ok(new { message = "Se o e-mail existir em nossa base, um link de recuperação será enviado em instantes." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest model)
        {
            // 1. Busca o usuário
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Novamente, mensagem genérica para não confirmar se o e-mail existe na base
                return BadRequest(new { message = "Não foi possível redefinir a senha. Verifique o link utilizado." });
            }

            // 2. Decodifica o token que veio da URL (revertendo o Base64UrlEncode)
            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
            }
            catch (FormatException)
            {
                return BadRequest(new { message = "O token de recuperação é inválido ou está malformado." });
            }

            // 3. Efetua a redefinição de senha via Identity
            var resetResult = await _userManager.ResetPasswordAsync(user, decodedToken, model.NewPassword);

            if (resetResult.Succeeded)
            {
                return Ok(new { message = "Sua senha foi redefinida com sucesso! Você já pode fazer login." });
            }

            // 4. Se falhar (ex: token expirado, senha fraca), retorna os erros do Identity
            var errors = resetResult.Errors.Select(e => e.Description);
            return BadRequest(new { message = "Erro ao redefinir a senha.", details = errors });
        }

        [Authorize] // Exige que o usuário esteja logado
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest model)
        {
            // 1. Pega o ID do usuário diretamente do Token JWT autenticado
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Usuário não autenticado ou token inválido." });
            }

            // 2. Busca o usuário no banco
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "Usuário não encontrado." });
            }

            // 3. Efetua a troca de senha (o Identity já valida a senha atual automaticamente)
            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (result.Succeeded)
            {
                // Detalhe: o Audit.NET vai pegar esse Update automaticamente e gerar outro log lá no banco!
                return Ok(new { message = "Sua senha foi alterada com sucesso." });
            }

            // 4. Se a senha atual estiver errada ou a nova for fraca, retorna os erros
            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { message = "Não foi possível alterar a senha.", details = errors });
        }
    }
}
