using Axiom.Atlas.Application.DTOs.ServiceDesk;
using Axiom.Atlas.Application.DTOs.TimeEntries;
using Axiom.Atlas.Web.Model.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Axiom.Atlas.Web.Controllers.Home
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public HomeController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [Authorize]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> DashboardData()
        {
            try
            {
                var client = CreateClient();
                var backlogTask = client.GetAsync("api/glpi/tickets/dashboard");
                var timeEntriesTask = client.GetAsync("api/TimeEntries/summary");
                await Task.WhenAll(backlogTask, timeEntriesTask);

                var backlogResponse = await backlogTask;
                if (!backlogResponse.IsSuccessStatusCode)
                {
                    return StatusCode((int)backlogResponse.StatusCode, new { message = "Não foi possível carregar os indicadores de chamados do GLPI." });
                }

                var timeEntriesResponse = await timeEntriesTask;
                if (!timeEntriesResponse.IsSuccessStatusCode)
                {
                    return StatusCode((int)timeEntriesResponse.StatusCode, new { message = "Não foi possível carregar os indicadores de entradas de tempo." });
                }

                var backlog = await backlogResponse.Content.ReadFromJsonAsync<UnifiedBacklogResponse>() ?? new UnifiedBacklogResponse();
                var timeEntries = await timeEntriesResponse.Content.ReadFromJsonAsync<TimeEntrySummaryDto>() ?? new TimeEntrySummaryDto();
                return Json(new { backlog, timeEntries, generatedAt = DateTimeOffset.UtcNow });
            }
            catch (Exception exception)
            {
                return StatusCode(503, new { message = "Não foi possível preparar o painel inicial.", detail = exception.Message });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private HttpClient CreateClient()
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
