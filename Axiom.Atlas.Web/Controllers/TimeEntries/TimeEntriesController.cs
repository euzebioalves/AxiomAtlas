using Axiom.Atlas.Application.DTOs.TimeEntries;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace Axiom.Atlas.Web.Controllers.TimeEntries
{
    public class TimeEntriesController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public TimeEntriesController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult Index(int? workPackageId)
        {
            ViewData["PreselectedWorkPackageId"] = workPackageId is > 0 ? workPackageId : null;
            return View();
        }

        private async Task<HttpClient> CreateAuthorizedApiClientAsync()
        {
            var client = _httpClientFactory.CreateClient("Api");
            var accessToken = await HttpContext.GetTokenAsync("access_token")
                              ?? User.FindFirst("JWToken")?.Value;

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            return client;
        }

        [HttpGet]
        public async Task<IActionResult> GetWorkPackageInfo(int wpId)
        {
            var client = await CreateAuthorizedApiClientAsync();
            var response = await client.GetAsync($"api/TimeEntries/work-package/{wpId}");

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                return Content(jsonString, "application/json");
            }

            return StatusCode((int)response.StatusCode, "Erro ao consultar a API.");
        }

        [HttpPost]
        public async Task<IActionResult> LogTime([FromBody] CreateTimeEntryRequest request)
        {
            var client = await CreateAuthorizedApiClientAsync();

            var response = await client.PostAsJsonAsync("api/TimeEntries", request);

            if (response.IsSuccessStatusCode)
            {
                return Ok(new { success = true });
            }

            var erroRealDaApi = await response.Content.ReadAsStringAsync();
            return BadRequest(new { success = false, message = $"A API recusou: {response.StatusCode} - {erroRealDaApi}" });
        }

        [HttpPut]
        [Route("TimeEntries/Update/{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateTimeEntryRequest request)
        {
            var client = await CreateAuthorizedApiClientAsync();

            var response = await client.PutAsJsonAsync($"api/TimeEntries/{id}", request);

            if (response.IsSuccessStatusCode)
            {
                return Ok(new { success = true });
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return BadRequest(new { success = false, message = errorMessage });
        }

        [HttpGet]
        public async Task<IActionResult> GetEntries()
        {
            var client = await CreateAuthorizedApiClientAsync();

            var response = await client.GetAsync("api/TimeEntries");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return Content(json, "application/json");
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, errorContent);
        }

        [HttpGet]
        public async Task<IActionResult> GetSummary()
        {
            var client = await CreateAuthorizedApiClientAsync();
            var response = await client.GetAsync("api/TimeEntries/summary");
            var content = await response.Content.ReadAsStringAsync();

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = "application/json"
            };
        }

        [HttpDelete]
        [Route("TimeEntries/Delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var client = await CreateAuthorizedApiClientAsync();

            var response = await client.DeleteAsync($"api/TimeEntries/{id}");

            if (response.IsSuccessStatusCode) return Ok();

            return BadRequest();
        }

        [HttpPost]
        public async Task<IActionResult> SyncEntries([FromBody] Guid[] ids)
        {
            var client = await CreateAuthorizedApiClientAsync();

            var response = await client.PostAsJsonAsync("api/TimeEntries/sync", ids);
            var responseBody = await response.Content.ReadAsStringAsync();

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = responseBody,
                ContentType = "application/json"
            };
        }

        [HttpGet]
        public async Task<IActionResult> GetActivities(int? workPackageId)
        {
            var client = await CreateAuthorizedApiClientAsync();
            var requestUrl = workPackageId.HasValue
                ? $"api/TimeEntries/activities?workPackageId={workPackageId.Value}"
                : "api/TimeEntries/activities";

            var response = await client.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                return Json(new List<object>());
            }

            var json = await response.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }

        [HttpGet]
        public async Task<IActionResult> SearchWorkPackages(string query)
        {
            // Se o usuário não digitou nada, devolvemos uma lista vazia
            if (string.IsNullOrWhiteSpace(query))
                return Json(new List<object>());

            var client = await CreateAuthorizedApiClientAsync();

            // Chama a API passando a query na URL
            var requestUrl = $"api/TimeEntries/SearchWorkPackages?query={Uri.EscapeDataString(query)}";
            var response = await client.GetAsync(requestUrl);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                // Retorna o JSON exatamente como a API mandou para o Select2 ler
                return Content(json, "application/json");
            }

            // Se der erro, retorna lista vazia para não quebrar a tela do usuário
            return Json(new List<object>());
        }
    }
}
