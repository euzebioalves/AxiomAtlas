using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net;

namespace Axiom.Atlas.Web.Controllers.TimeClock
{
    [Authorize]
    public class TimeClockController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public TimeClockController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Settings()
        {
            return View();
        }

        public async Task<IActionResult> GlobalSettings()
        {
            var client = await CreateAuthorizedApiClientAsync();
            var response = await client.GetAsync("api/TimeClock/settings/global");

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return RedirectToAction("AccessDenied", "Auth");
            }

            return View();
        }

        public IActionResult Audit()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetCalendar(int year, int month)
        {
            var client = await CreateAuthorizedApiClientAsync();
            var response = await client.GetAsync($"api/TimeClock/calendar?year={year}&month={month}");
            return await ProxyResponseAsync(response);
        }

        [HttpGet]
        public async Task<IActionResult> GetAudit(int year, int month)
        {
            var client = await CreateAuthorizedApiClientAsync();
            var response = await client.GetAsync($"api/TimeClock/audit?year={year}&month={month}");
            return await ProxyResponseAsync(response);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserSettings()
        {
            var client = await CreateAuthorizedApiClientAsync();
            var response = await client.GetAsync("api/TimeClock/settings/user");
            return await ProxyResponseAsync(response);
        }

        [HttpPost]
        public async Task<IActionResult> SaveUserSettings([FromBody] object request)
        {
            var client = await CreateAuthorizedApiClientAsync();
            var response = await client.PutAsJsonAsync("api/TimeClock/settings/user", request);
            return await ProxyResponseAsync(response);
        }

        [HttpGet]
        public async Task<IActionResult> GetGlobalSettings()
        {
            var client = await CreateAuthorizedApiClientAsync();
            var response = await client.GetAsync("api/TimeClock/settings/global");
            return await ProxyResponseAsync(response);
        }

        [HttpPost]
        public async Task<IActionResult> SaveGlobalSettings([FromBody] object request)
        {
            var client = await CreateAuthorizedApiClientAsync();
            var response = await client.PutAsJsonAsync("api/TimeClock/settings/global", request);
            return await ProxyResponseAsync(response);
        }

        [HttpPost]
        public async Task<IActionResult> SavePunches([FromBody] object request)
        {
            var client = await CreateAuthorizedApiClientAsync();
            var response = await client.PostAsJsonAsync("api/TimeClock/punches", request);
            return await ProxyResponseAsync(response);
        }

        [HttpDelete]
        [Route("TimeClock/DeletePunch/{id:guid}")]
        public async Task<IActionResult> DeletePunch(Guid id)
        {
            var client = await CreateAuthorizedApiClientAsync();
            var response = await client.DeleteAsync($"api/TimeClock/punches/{id}");
            return await ProxyResponseAsync(response);
        }

        [HttpPost]
        public async Task<IActionResult> SaveUnjustifiedAbsence([FromBody] object request)
        {
            var client = await CreateAuthorizedApiClientAsync();
            var response = await client.PostAsJsonAsync("api/TimeClock/unjustified-absence", request);
            return await ProxyResponseAsync(response);
        }

        [HttpDelete]
        [Route("TimeClock/DeleteUnjustifiedAbsence/{id:guid}")]
        public async Task<IActionResult> DeleteUnjustifiedAbsence(Guid id)
        {
            var client = await CreateAuthorizedApiClientAsync();
            var response = await client.DeleteAsync($"api/TimeClock/unjustified-absence/{id}");
            return await ProxyResponseAsync(response);
        }

        [HttpPost]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> SaveAbsence()
        {
            var client = await CreateAuthorizedApiClientAsync();
            var form = await Request.ReadFormAsync();

            using var multipart = new MultipartFormDataContent();

            foreach (var key in form.Keys)
            {
                multipart.Add(new StringContent(form[key].ToString()), key);
            }

            foreach (var file in form.Files)
            {
                var streamContent = new StreamContent(file.OpenReadStream());
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);

                multipart.Add(streamContent, file.Name, file.FileName);
            }

            var response = await client.PostAsync("api/TimeClock/absences", multipart);
            return await ProxyResponseAsync(response);
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

        private static async Task<IActionResult> ProxyResponseAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = string.IsNullOrWhiteSpace(content) ? "{}" : content,
                ContentType = "application/json"
            };
        }
    }
}
