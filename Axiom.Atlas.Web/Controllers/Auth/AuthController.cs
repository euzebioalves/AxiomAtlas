using Axiom.Atlas.Web.Model.Login;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using IAuthService = Axiom.Atlas.Web.Services.Auth.IAuthService;

namespace Axiom.Atlas.Web.Controllers.Auth
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthController(IAuthService authService, IHttpClientFactory httpClientFactory)
        {
            _authService = authService;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (User.Identity != null && User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");
            return View(new LoginViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // MUDANÇA AQUI: Desempacotamos a Tupla!
            var (loginResult, errorMessage) = await _authService.LoginAsync(model);

            if (loginResult != null)
            {
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, loginResult.FullName ),
            new Claim("Username", loginResult.Username),
            new Claim("Email", loginResult.Email),
            new Claim("Phone", loginResult.PhoneNumber ?? ""),
            new Claim("JobTitle", loginResult.JobTitle ?? ""),
            new Claim("ProfilePictureUrl", loginResult.ProfilePictureUrl ?? "~/metronic8/images/unknown-user.png"),
            new Claim("JWToken", loginResult.Token)
        };

                var claimsIdentity = new ClaimsIdentity(claims, "CookieAuth");

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                };

                authProperties.StoreTokens(new[]
                {
            new AuthenticationToken { Name = "access_token", Value = loginResult.Token }
        });

                await HttpContext.SignInAsync("CookieAuth", new ClaimsPrincipal(claimsIdentity), authProperties);

                return RedirectToAction("Index", "Home");
            }

            // MUDANÇA AQUI: Joga a mensagem de erro real no ModelState
            ModelState.AddModelError(string.Empty, errorMessage ?? "Usuário ou senha inválidos.");
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("CookieAuth");
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            // 1. Verifica se o usuário preencheu o e-mail validamente no formulário
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 2. Prepara o HttpClient para chamar a sua API
            // Dica: Em um cenário ideal futuro, você injeta o IHttpClientFactory no construtor.
            // Aqui estamos instanciando para resolver rápido e direto.
            var client = _httpClientFactory.CreateClient("Api");

            try
            {
                // 3. Faz o POST para o endpoint que criamos na API
                var response = await client.PostAsJsonAsync("api/Auth/forgot-password", model);

                if (response.IsSuccessStatusCode)
                {
                    // 4. Pega a mensagem de sucesso que a API devolveu ("Se o e-mail existir...")
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string apiMessage = result.GetProperty("message").GetString() ?? "Solicitação processada com sucesso.";

                    // 5. Guarda a mensagem no TempData para exibir na tela de Login
                    TempData["SuccessMessage"] = apiMessage;

                    // Redireciona de volta para o Login
                    return RedirectToAction("Login");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Não foi possível processar sua solicitação no momento. Tente novamente.");
                    return View(model);
                }
            }
            catch (Exception)
            {
                // Se a API estiver fora do ar, cai aqui
                ModelState.AddModelError(string.Empty, "Erro de comunicação com o servidor. A API está rodando?");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            // Se o usuário tentar acessar a tela sem o link do e-mail, mandamos pro Login
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Link de redefinição inválido ou incompleto.";
                return RedirectToAction("Login");
            }

            // Criamos o model já preenchido com os dados ocultos para a View
            var model = new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var client = _httpClientFactory.CreateClient("Api");

            try
            {
                // Envia os dados (E-mail, Token e Nova Senha) para a API validar
                var response = await client.PostAsJsonAsync("api/Auth/reset-password", model);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Senha redefinida com sucesso! Você já pode fazer login com sua nova senha.";
                    return RedirectToAction("Login");
                }
                else
                {
                    // Se o token expirou ou a senha for fraca, a API devolve um erro
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string errorMessage = result.GetProperty("message").GetString() ?? "Não foi possível redefinir a senha.";
                    ModelState.AddModelError(string.Empty, errorMessage);

                    return View(model);
                }
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Erro de comunicação com o servidor. A API está rodando?");
                return View(model);
            }
        }
    }
}
