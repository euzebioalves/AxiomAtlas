using Axiom.Atlas.Web.Model.Integrations;
using Axiom.Atlas.Application.DTOs.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json; // Importante para o PostAsJsonAsync funcionar
using IHttpClientFactory = System.Net.Http.IHttpClientFactory;

namespace Axiom.Atlas.Web.Controllers.Integrations
{
    [Authorize(Policy = "AdministrationOnly")]
    public class IntegrationsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        // A CORREÇÃO: O construtor agora é público!
        public IntegrationsController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public async Task<IActionResult> OpenProject()
        {
            var model = new OpenProjectSettingsViewModel
            {
                ActiveEnvironment = "Homologation" // Padrão de fallback
            };

            try
            {
                var client = _httpClientFactory.CreateClient("Api");
                var token = User.FindFirst("JWToken")?.Value;

                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var response = await client.GetAsync("api/integrations/openproject");

                if (response.IsSuccessStatusCode)
                {
                    // O pacote JSON que chega tem a mesma estrutura do nosso ViewModel!
                    var apiData = await response.Content.ReadFromJsonAsync<OpenProjectSettingsViewModel>();

                    if (apiData != null)
                    {
                        model = apiData; // Preenchemos a tela com os dados vindos do banco
                    }
                }
            }
            catch (Exception)
            {
                // Se a API estiver offline, não quebra a tela, só abre em branco.
                TempData["ErrorMessage"] = "Aviso: Não foi possível carregar as configurações atuais.";
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SaveOpenProject(OpenProjectSettingsViewModel model)
        {
            if (!ModelState.IsValid)
                return View("OpenProject", model);

            try
            {
                var client = _httpClientFactory.CreateClient("Api");
                var token = User.FindFirst("JWToken")?.Value;

                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                // Dispara o POST para a API enviando o model inteiro
                var response = await client.PostAsJsonAsync("api/integrations/openproject", model);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Configurações do OpenProject salvas com sucesso!";
                    return RedirectToAction("OpenProject");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Falha ao salvar as configurações. Verifique os dados enviados.");
                    return View("OpenProject", model);
                }
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Erro de comunicação com o servidor. Tente novamente mais tarde.");
                return View("OpenProject", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> TestOpenProject([FromBody] OpenProjectConnectionTestViewModel model)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("Api");
                var token = User.FindFirst("JWToken")?.Value;

                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var response = await client.PostAsJsonAsync("api/integrations/openproject/test", model);
                var result = await response.Content.ReadAsStringAsync();

                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = result,
                    ContentType = "application/json"
                };
            }
            catch (Exception)
            {
                return StatusCode(503, new
                {
                    success = false,
                    message = "Erro de comunicação com a API."
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Glpi()
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;
            if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync("api/integrations/glpi");
            return View(response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<GlpiSettingsViewModel>() ?? new() : new GlpiSettingsViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> SaveGlpi(GlpiSettingsViewModel model)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;
            if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.PostAsJsonAsync("api/integrations/glpi", model);
            TempData[response.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = response.IsSuccessStatusCode ? "Configurações do GLPI salvas com sucesso!" : "Não foi possível salvar as configurações do GLPI.";
            return RedirectToAction(nameof(Glpi));
        }

        [HttpPost]
        public async Task<IActionResult> TestGlpi([FromBody] GlpiConnectionTestViewModel model)
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;
            if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.PostAsJsonAsync("api/integrations/glpi/test", model);
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = "application/json",
                Content = await response.Content.ReadAsStringAsync()
            };
        }

        [HttpGet]
        public IActionResult Operations() => View();

        [HttpGet]
        public async Task<IActionResult> SynchronizationJobs(string? status = null, int take = 50)
        {
            var client = CreateApiClient();
            var response = await client.GetAsync($"api/integrations/synchronizations?status={Uri.EscapeDataString(status ?? string.Empty)}&take={Math.Clamp(take, 10, 200)}");
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
            }

            var model = await response.Content.ReadFromJsonAsync<IntegrationSynchronizationOverviewDto>() ?? new();
            return PartialView("_SynchronizationJobs", model);
        }

        [HttpPost]
        public async Task<IActionResult> RetrySynchronizationJob(Guid id)
        {
            var client = CreateApiClient();
            var response = await client.PostAsync($"api/integrations/synchronizations/{id}/retry", null);
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = "application/json",
                Content = await response.Content.ReadAsStringAsync()
            };
        }

        private HttpClient CreateApiClient()
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;
            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return client;
        }
    }
}
