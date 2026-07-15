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
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> List(int page = 1, int pageSize = 25, string? status = null, bool refresh = false)
        {
            try
            {
                var response = await CreateClient().GetAsync(
                    $"api/glpi/tickets/improvements?page={page}&pageSize={pageSize}&status={Uri.EscapeDataString(status ?? "not_solved")}&refresh={refresh.ToString().ToLowerInvariant()}");
                if (response.IsSuccessStatusCode)
                {
                    return PartialView("_ImprovementTicketsTable", await response.Content.ReadFromJsonAsync<GlpiImprovementTicketsResponse>() ?? new GlpiImprovementTicketsResponse());
                }

                return StatusCode((int)response.StatusCode, new { message = "Não foi possível carregar as solicitações de melhoria do GLPI." });
            }
            catch (Exception)
            {
                return StatusCode(503, new { message = "Não foi possível comunicar com o serviço de integração do GLPI." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Import([FromBody] ImportGlpiTicketRequest request)
        {
            var client = CreateClient();
            var response = await client.PostAsJsonAsync("api/glpi/tickets/import", request);
            return new ContentResult { Content = await response.Content.ReadAsStringAsync(), ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }

        [HttpGet]
        public async Task<IActionResult> Workspace(Guid id, int returnPage = 1, int returnPageSize = 25, string? returnStatus = null)
        {
            var response = await CreateClient().GetAsync($"api/glpi/tickets/{id}");
            if (!response.IsSuccessStatusCode) return RedirectToAction(nameof(Index));

            var workspace = await response.Content.ReadFromJsonAsync<GlpiTicketWorkspaceDto>();
            if (workspace is null) return RedirectToAction(nameof(Index));

            var pageSize = new[] { 10, 25, 50, 100 }.Contains(returnPageSize) ? returnPageSize : 25;
            ViewData["ReturnUrl"] = Url.Action(nameof(Index), new
            {
                page = Math.Max(1, returnPage),
                pageSize,
                status = string.IsNullOrWhiteSpace(returnStatus) ? "not_solved" : returnStatus,
                highlight = workspace.GlpiTicketId
            });

            return View(workspace);
        }

        [HttpPost]
        public async Task<IActionResult> SaveDraft(Guid id, [FromBody] SaveRequirementDraftRequest request)
        {
            var response = await CreateClient().PutAsJsonAsync($"api/glpi/tickets/{id}/draft", request);
            return new ContentResult { Content = await response.Content.ReadAsStringAsync(), ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }

        [HttpGet]
        public async Task<IActionResult> OpenProjectProjects()
        {
            var response = await CreateClient().GetAsync("api/glpi/tickets/openproject-projects");
            return new ContentResult { Content = await response.Content.ReadAsStringAsync(), ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }

        [HttpPost]
        public async Task<IActionResult> CreateUserStory(Guid id, [FromBody] CreateOpenProjectUserStoryRequest request)
        {
            var response = await CreateClient().PostAsJsonAsync($"api/glpi/tickets/{id}/user-story", request);
            return new ContentResult { Content = await response.Content.ReadAsStringAsync(), ContentType = "application/json", StatusCode = (int)response.StatusCode };
        }

        [HttpGet]
        public async Task<IActionResult> Attachment(Guid id, int documentId)
        {
            var response = await CreateClient().GetAsync($"api/glpi/tickets/{id}/attachments/{documentId}");
            if (!response.IsSuccessStatusCode) return NotFound();
            return File(await response.Content.ReadAsByteArrayAsync(), response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream");
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
