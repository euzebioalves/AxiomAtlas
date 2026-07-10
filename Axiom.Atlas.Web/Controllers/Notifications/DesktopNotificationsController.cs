using Axiom.Atlas.Web.Model.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Axiom.Atlas.Web.Controllers.Notifications
{
    [Authorize]
    [Route("DesktopNotifications")]
    public class DesktopNotificationsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public DesktopNotificationsController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            return View(new DesktopNotificationSettingsViewModel());
        }

        [HttpGet("Settings")]
        public async Task<IActionResult> GetSettings()
        {
            using var client = CreateApiClient();
            return await ProxyResponseAsync(await client.GetAsync("api/DesktopNotifications/settings"));
        }

        [HttpPut("Settings")]
        public async Task<IActionResult> SaveSettings([FromBody] DesktopNotificationSettingsViewModel model)
        {
            using var client = CreateApiClient();
            return await ProxyResponseAsync(await client.PutAsJsonAsync("api/DesktopNotifications/settings", model));
        }

        [HttpGet("Pending")]
        public async Task<IActionResult> GetPending()
        {
            using var client = CreateApiClient();
            return await ProxyResponseAsync(await client.GetAsync("api/DesktopNotifications/pending"));
        }

        [HttpPost("{id:guid}/Delivered")]
        public async Task<IActionResult> MarkDelivered(Guid id)
        {
            using var client = CreateApiClient();
            var response = await client.PostAsync($"api/DesktopNotifications/{id}/delivered", null);
            return StatusCode((int)response.StatusCode);
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

        private static async Task<IActionResult> ProxyResponseAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = contentType,
                Content = content
            };
        }
    }
}
