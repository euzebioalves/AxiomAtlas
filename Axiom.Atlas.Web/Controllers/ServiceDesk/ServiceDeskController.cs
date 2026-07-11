using System.Net.Http.Headers;
using System.Net.Http.Json;
using Axiom.Atlas.Application.DTOs.ServiceDesk;
using Microsoft.AspNetCore.Mvc;

namespace Axiom.Atlas.Web.Controllers.ServiceDesk
{
    public class ServiceDeskController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public ServiceDeskController(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;
        public IActionResult Index() => View();

        [HttpPost]
        public async Task<IActionResult> Import([FromBody] ImportGlpiTicketRequest request)
        {
            var client = CreateClient();
            var response = await client.PostAsJsonAsync("api/glpi/tickets/import", request);
            return new ContentResult { Content = await response.Content.ReadAsStringAsync(), ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }

        [HttpGet]
        public async Task<IActionResult> Workspace(Guid id)
        {
            var response = await CreateClient().GetAsync($"api/glpi/tickets/{id}");
            if (!response.IsSuccessStatusCode) return RedirectToAction(nameof(Index));
            return View(await response.Content.ReadFromJsonAsync<GlpiTicketWorkspaceDto>());
        }

        [HttpPost]
        public async Task<IActionResult> SaveDraft(Guid id, [FromBody] SaveRequirementDraftRequest request)
        {
            var response = await CreateClient().PutAsJsonAsync($"api/glpi/tickets/{id}/draft", request);
            return new ContentResult { Content = await response.Content.ReadAsStringAsync(), ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient("Api");
            var token = User.FindFirst("JWToken")?.Value;
            if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }
    }
}
