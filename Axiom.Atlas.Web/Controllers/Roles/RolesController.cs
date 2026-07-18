using Axiom.Atlas.Web.Model.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Axiom.Atlas.Web.Controllers.Roles
{
    [Authorize(Policy = "AdministrationOnly")]
    public class RolesController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public RolesController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Chama a rota de Roles da sua API
            var response = await client.GetAsync("api/Roles");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var roles = JsonSerializer.Deserialize<List<RoleViewModel>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return View(roles);
            }

            TempData["ErrorMessage"] = "Não foi possível carregar a lista de papéis.";
            return View(new List<RoleViewModel>());
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RoleViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
                return Json(new { success = false, message = "O nome do papel é obrigatório." });

            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Dispara o POST para a sua API com os dados
            var response = await client.PostAsJsonAsync("api/Roles", new { name = model.Name });

            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }

            // Se a API retornar erro (ex: perfil já existe), tentamos ler a mensagem
            var errorJson = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = "Erro ao criar perfil. Verifique se ele já não existe." });
        }

        // GET: Abre a tela de edição montando a matriz
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;
            if (!string.IsNullOrEmpty(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync($"api/Roles/{id}/permissions");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var model = JsonSerializer.Deserialize<EditRoleViewModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return View(model);
            }

            TempData["ErrorMessage"] = "Não foi possível carregar as permissões do papel.";
            return RedirectToAction("Index");
        }

        // POST: Salva os checkboxes que o usuário marcou
        [HttpPost]
        public async Task<IActionResult> Edit(EditRoleViewModel model)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;
            if (!string.IsNullOrEmpty(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // A nossa API espera um objeto com RoleId e a lista de Permissions
            var updateData = new
            {
                RoleId = model.RoleId,
                Permissions = model.Permissions
            };

            var response = await client.PutAsJsonAsync($"api/Roles/{model.RoleId}/permissions", updateData);

            if (response.IsSuccessStatusCode)
            {
                // Usando TempData para mostrar uma mensagem bonita quando voltar para a listagem
                TempData["SuccessMessage"] = "Matriz de permissões atualizada com sucesso!";
                return RedirectToAction("Index");
            }

            TempData["ErrorMessage"] = "Erro ao salvar as permissões.";
            return View(model);
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(Guid id)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Repassa a ordem de exclusão para a API
            var response = await client.DeleteAsync($"api/Roles/{id}");

            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Perfil excluído com sucesso!" });
            }

            // Se falhar (erro 400 ou 500), tenta ler a mensagem de erro que a API mandou
            var errorMessage = "Não foi possível excluir o perfil. Ele pode estar em uso.";
            if (response.Content != null)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                // Aqui você pode refinar a leitura do JSON de erro se quiser, 
                // mas a mensagem genérica já protege a tela.
            }

            return Json(new { success = false, message = errorMessage });
        }
    }
}
