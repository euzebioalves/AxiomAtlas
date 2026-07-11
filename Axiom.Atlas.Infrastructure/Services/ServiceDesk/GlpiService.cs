using Axiom.Atlas.Application.DTOs.Integrations;
using Axiom.Atlas.Domain.Entities.Integrations;
using Axiom.Atlas.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Axiom.Atlas.Infrastructure.Services.ServiceDesk
{
    public class GlpiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppDbContext _context;
        private readonly IDataProtector _protector;

        public GlpiService(IHttpClientFactory httpClientFactory, AppDbContext context, IDataProtectionProvider provider)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _protector = provider.CreateProtector("AxiomAtlas.Integrations");
        }

        public async Task<GlpiConnectionTestResult> TestConnectionAsync(SaveGlpiSettingsRequest request)
        {
            try
            {
                var saved = await _context.Integrations.FirstOrDefaultAsync(x => x.Provider == "GLPI" && x.IsActive);
                var baseUrl = request.BaseUrl ?? saved?.BaseUrl;
                var appToken = ResolveToken(request.AppToken, saved?.SecondaryToken);
                var userToken = ResolveToken(request.UserToken, saved?.PrimaryToken);
                if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(appToken) || string.IsNullOrWhiteSpace(userToken))
                    return new GlpiConnectionTestResult { Message = "Informe BASE_URL, APP_TOKEN e USER_TOKEN." };

                using var client = CreateClient(baseUrl, appToken, userToken);
                using var response = await client.GetAsync("initSession");
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return new GlpiConnectionTestResult { Message = $"GLPI retornou {(int)response.StatusCode}: {content}" };

                using var document = JsonDocument.Parse(content);
                var sessionToken = document.RootElement.TryGetProperty("session_token", out var token) ? token.GetString() : null;
                if (string.IsNullOrWhiteSpace(sessionToken))
                    return new GlpiConnectionTestResult { Message = "GLPI não retornou um token de sessão." };

                client.DefaultRequestHeaders.Remove("Session-Token");
                client.DefaultRequestHeaders.Add("Session-Token", sessionToken);
                using var sessionResponse = await client.GetAsync("getFullSession");
                var sessionContent = await sessionResponse.Content.ReadAsStringAsync();
                await client.GetAsync("killSession");
                if (!sessionResponse.IsSuccessStatusCode)
                    return new GlpiConnectionTestResult { Message = $"Sessão iniciada, mas o GLPI recusou a leitura: {(int)sessionResponse.StatusCode}." };

                using var sessionDocument = JsonDocument.Parse(sessionContent);
                var version = sessionDocument.RootElement.TryGetProperty("glpi_version", out var versionElement)
                    ? versionElement.GetString()
                    : null;
                return new GlpiConnectionTestResult
                {
                    Success = true,
                    GlpiVersion = version,
                    Message = "Conexão com GLPI validada com sucesso.",
                    Warnings = string.IsNullOrWhiteSpace(request.ClassificationFieldKey) || string.IsNullOrWhiteSpace(request.DevOpsUrlFieldKey)
                        ? new List<string> { "Configure as chaves técnicas dos campos adicionais após a validação da conexão." }
                        : new List<string>()
                };
            }
            catch (Exception exception)
            {
                return new GlpiConnectionTestResult { Message = exception.Message };
            }
        }

        private HttpClient CreateClient(string baseUrl, string appToken, string userToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/apirest.php/");
            client.DefaultRequestHeaders.Add("App-Token", appToken);
            // A API REST legada do GLPI autentica o token externo do usuário com o esquema user_token.
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("user_token", userToken);
            return client;
        }

        private string? ResolveToken(string? requestedToken, string? protectedToken)
        {
            if (!string.IsNullOrWhiteSpace(requestedToken) && requestedToken != "********") return requestedToken;
            return string.IsNullOrWhiteSpace(protectedToken) ? null : _protector.Unprotect(protectedToken);
        }
    }
}
