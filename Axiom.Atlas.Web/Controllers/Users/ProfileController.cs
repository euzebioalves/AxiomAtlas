using Axiom.Atlas.Web.Model.Login;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace Axiom.Atlas.Web.Controllers.Users
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ProfileController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult Security()
        {
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Security", model);

            try
            {
                var client = _httpClientFactory.CreateClient("Api");
                var token = User.FindFirst("JWToken")?.Value;

                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                // Montando o "Pacote" (DTO) exatamente como a API espera receber
                var payload = new
                {
                    CurrentPassword = model.CurrentPassword,
                    NewPassword = model.NewPassword
                };

                // Enviando o POST para a rota da API (Ajuste a rota se a sua for diferente)
                var response = await client.PostAsJsonAsync("api/users/change-password", payload);

                if (response.IsSuccessStatusCode)
                {
                    // Deu tudo certo na API!
                    TempData["SuccessMessage"] = "Sua senha foi alterada com sucesso!";
                    return RedirectToAction("Security");
                }
                else
                {
                    // A API recusou (provavelmente a senha atual estava incorreta)
                    // Você pode logar o errorResponse se quiser ver o motivo exato
                    var errorResponse = await response.Content.ReadAsStringAsync();

                    ModelState.AddModelError(string.Empty, "Falha ao alterar a senha. Verifique se a sua senha atual está correta.");
                    return View("Security", model);
                }
            }
            catch (Exception)
            {
                // Cai aqui se a API estiver fora do ar, por exemplo
                ModelState.AddModelError(string.Empty, "Erro de comunicação com o servidor. Tente novamente mais tarde.");
                return View("Security", model);
            }
        }
    }
}
