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

        public AuthService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _httpClient.BaseAddress = new Uri(_configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7255");
        }

        public async Task<(LoginResultViewModel? Data, string? ErrorMessage)> LoginAsync(LoginViewModel model)
        {
            var response = await _httpClient.PostAsJsonAsync("api/Auth/login", model);

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
