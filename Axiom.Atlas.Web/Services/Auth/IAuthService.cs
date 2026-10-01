using Axiom.Atlas.Web.Model.Login;
using System.Text.Json;

namespace Axiom.Atlas.Web.Services.Auth
{
    public interface IAuthService
    {
        Task<(LoginResultViewModel? Data, string? ErrorMessage)> LoginAsync(LoginViewModel model);
    }

    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthService(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _httpClient.BaseAddress = new Uri(_configuration["ApiSettings:BaseUrl"]
                ?? throw new InvalidOperationException("ApiSettings:BaseUrl não configurada."));
        }

        public async Task<(LoginResultViewModel? Data, string? ErrorMessage)> LoginAsync(LoginViewModel model)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/Auth/login")
            {
                Content = JsonContent.Create(model)
            };
            var sourceAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrWhiteSpace(sourceAddress))
            {
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", sourceAddress);
            }

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<LoginResultViewModel>();
                return (data, null);
            }

            var errorJson = await response.Content.ReadAsStringAsync();
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var errorData = JsonSerializer.Deserialize<JsonElement>(errorJson, options);
                if (errorData.TryGetProperty("message", out var messageProp))
                {
                    return (null, messageProp.GetString());
                }
            }
            catch
            {
                // A API pode retornar payload não JSON em falhas inesperadas.
            }

            return (null, "Usuário ou senha inválidos.");
        }
    }
}
