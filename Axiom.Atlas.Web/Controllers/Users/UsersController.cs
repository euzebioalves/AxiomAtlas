using Axiom.Atlas.Web.Model.Roles;
using Axiom.Atlas.Web.Model.Users;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Axiom.Atlas.Web.Controllers.Users
{
    [Route("Users")]
    public class UsersController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;

        public UsersController(IWebHostEnvironment env, IHttpClientFactory httpClientFactory)
        {
            _env = env;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("/Users/GetAvatar")]
        public async Task<IActionResult> GetAvatar([FromQuery] string username)
        {
            var defaultImagePath = Path.Combine(_env.WebRootPath, "resources", "images", "1.png");

            if (string.IsNullOrEmpty(username))
            {
                System.Diagnostics.Debug.WriteLine("---> PROXY: Username chegou vazio.");
                return PhysicalFile(defaultImagePath, "image/png");
            }

            try
            {
                using var client = _httpClientFactory.CreateClient("Api");

                var token = User.FindFirst("JWToken")?.Value;

                if (string.IsNullOrEmpty(token))
                {
                    System.Diagnostics.Debug.WriteLine("---> PROXY: JWToken NÃO encontrado nas Claims do usuário logado.");
                }
                else
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                System.Diagnostics.Debug.WriteLine($"---> PROXY: Chamando API para o usuário: {username}");

                var safeUsername = Uri.EscapeDataString(username.Trim());

                var response = await client.GetAsync($"api/Users/profile-picture/{safeUsername}");

                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine("---> PROXY: Sucesso! A API retornou a imagem.");
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    return File(imageBytes, "image/jpeg");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"---> PROXY: A API recusou a requisição. StatusCode: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"---> PROXY: Exceção (Crash) ao tentar falar com a API: {ex.Message}");
            }

            return PhysicalFile(defaultImagePath, "image/png");
        }

        [HttpPost("EditProfile")]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(model.FullName ?? ""), "FullName");
            content.Add(new StringContent(model.Username ?? ""), "Username");

            if (model.ProfilePictureFile != null && model.ProfilePictureFile.Length > 0)
            {
                var streamContent = new StreamContent(model.ProfilePictureFile.OpenReadStream());
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(model.ProfilePictureFile.ContentType);
                content.Add(streamContent, "ProfilePictureFile", model.ProfilePictureFile.FileName);
            }

            var response = await client.PutAsync("api/Users/update-profile", content);

            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Erro ao atualizar o perfil na API.");
            return View(model);
        }

        [HttpGet("EditProfile")]
        public IActionResult EditProfile()
        {
            var model = new EditProfileViewModel
            {
                Username = User.Identity?.Name ?? User.FindFirst("sub")?.Value,
                FullName = User.FindFirst("FullName")?.Value ?? ""
            };

            return View(model);
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

            // 1. Busca os Utilizadores (Já deve ter esta parte)
            var usersResponse = await client.GetAsync("api/Users");
            var users = new List<UserViewModel>();

            if (usersResponse.IsSuccessStatusCode)
            {
                var usersJson = await usersResponse.Content.ReadAsStringAsync();
                users = JsonSerializer.Deserialize<List<UserViewModel>>(usersJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            // 2. Busca os Perfis (NOVO) para preencher o <select> do Modal
            var rolesResponse = await client.GetAsync("api/Roles");
            if (rolesResponse.IsSuccessStatusCode)
            {
                var rolesJson = await rolesResponse.Content.ReadAsStringAsync();
                var roles = JsonSerializer.Deserialize<List<RoleViewModel>>(rolesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                ViewBag.Roles = roles;
            }
            else
            {
                ViewBag.Roles = new List<RoleViewModel>(); // Envia lista vazia por segurança
            }

            return View(users);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] UserCreateViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
            {
                return Json(new { success = false, message = "Por favor, preencha todos os campos obrigatórios." });
            }

            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Dispara o POST para a API de Utilizadores
            var response = await client.PostAsJsonAsync("api/Users", model);

            if (response.IsSuccessStatusCode)
            {
                // Deixa a mensagem de sucesso preparada para o SweetAlert2 ler após o reload
                TempData["SuccessMessage"] = "Utilizador criado com sucesso!";
                return Json(new { success = true });
            }

            var errorJson = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = ExtractApiErrorMessage(errorJson, "Erro ao processar a requisição. Tente novamente.") });
        }

        // 1. BUSCAR USUÁRIO PARA PREENCHER O MODAL
        [HttpGet("GetUserForEdit/{id}")]
        public async Task<IActionResult> GetUserForEdit(Guid id)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Bate na API que acabamos de criar
            var response = await client.GetAsync($"api/Users/{id}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                // Devolve o JSON cru para o JavaScript ler facilmente
                return Content(json, "application/json");
            }

            return Json(new { success = false, message = "Não foi possível carregar os dados do usuário." });
        }

        // 2. ENVIAR OS DADOS ATUALIZADOS
        [HttpPost("Edit/{id}")]
        public async Task<IActionResult> Edit(Guid id, [FromBody] UserUpdateViewModel model)
        {
            if (id != model.Id)
            {
                return Json(new { success = false, message = "ID de usuário inválido." });
            }

            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Repassa os dados para o endpoint PUT da API
            var response = await client.PutAsJsonAsync($"api/Users/{id}", model);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Usuário atualizado com sucesso!";
                return Json(new { success = true });
            }

            var errorJson = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = ExtractApiErrorMessage(errorJson, "Erro ao atualizar utilizador. Verifique os dados e tente novamente.") });
        }

        [HttpDelete("Delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Dispara a exclusão para a API
            var response = await client.DeleteAsync($"api/Users/{id}");

            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Usuário excluído com sucesso!" });
            }

            return Json(new { success = false, message = "Não foi possível excluir este usuário." });
        }

        [HttpPut("ToggleStatus/{id}")]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Bate no novo endpoint da API
            var response = await client.PutAsync($"api/Users/{id}/ToggleStatus", null);

            if (response.IsSuccessStatusCode)
            {
                var responseData = await response.Content.ReadAsStringAsync();
                // Lemos a resposta da API para saber se ficou ativo ou inativo e passamos pro SweetAlert
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var jsonResult = JsonSerializer.Deserialize<JsonElement>(responseData, options);
                var message = jsonResult.GetProperty("message").GetString();

                return Json(new { success = true, message = message });
            }

            return Json(new { success = false, message = "Não foi possível alterar o status deste usuário." });
        }

        private static string ExtractApiErrorMessage(string errorJson, string fallbackMessage)
        {
            if (string.IsNullOrWhiteSpace(errorJson))
            {
                return fallbackMessage;
            }

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var errorData = JsonSerializer.Deserialize<JsonElement>(errorJson, options);

                if (TryGetStringProperty(errorData, "message", out var message))
                {
                    return message;
                }

                if (TryGetStringProperty(errorData, "title", out var title))
                {
                    return title;
                }

                if (TryGetStringProperty(errorData, "detail", out var detail))
                {
                    return detail;
                }

                if (errorData.TryGetProperty("errors", out var errors))
                {
                    if (errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
                    {
                        return errors[0].GetString() ?? fallbackMessage;
                    }

                    if (errors.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in errors.EnumerateObject())
                        {
                            if (property.Value.ValueKind == JsonValueKind.Array && property.Value.GetArrayLength() > 0)
                            {
                                var firstError = property.Value[0].GetString();
                                if (!string.IsNullOrWhiteSpace(firstError))
                                {
                                    return firstError;
                                }
                            }
                            else if (property.Value.ValueKind == JsonValueKind.String)
                            {
                                var fieldError = property.Value.GetString();
                                if (!string.IsNullOrWhiteSpace(fieldError))
                                {
                                    return fieldError;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                return fallbackMessage;
            }

            return fallbackMessage;
        }

        private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;

            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
