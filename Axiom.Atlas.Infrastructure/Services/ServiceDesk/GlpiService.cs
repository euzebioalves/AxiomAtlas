using Axiom.Atlas.Application.DTOs.Integrations;
using Axiom.Atlas.Domain.Entities.Integrations;
using Axiom.Atlas.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
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

                using var client = CreateClient(baseUrl, appToken);
                using var sessionRequest = new HttpRequestMessage(HttpMethod.Get, "initSession?get_full_session=true");
                sessionRequest.Headers.TryAddWithoutValidation("Content-Type", "application/json");
                sessionRequest.Headers.TryAddWithoutValidation("Authorization", $"user_token {userToken}");
                using var response = await client.SendAsync(sessionRequest);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return new GlpiConnectionTestResult { Message = DescribeConnectionError($"{(int)response.StatusCode}: {content}") };

                using var document = JsonDocument.Parse(content);
                var sessionToken = document.RootElement.TryGetProperty("session_token", out var token) ? token.GetString() : null;
                if (string.IsNullOrWhiteSpace(sessionToken))
                    return new GlpiConnectionTestResult { Message = "GLPI não retornou um token de sessão." };

                var version = document.RootElement.TryGetProperty("glpi_version", out var versionElement) ? versionElement.GetString() : null;
                using var killRequest = new HttpRequestMessage(HttpMethod.Get, "killSession");
                killRequest.Headers.TryAddWithoutValidation("Content-Type", "application/json");
                killRequest.Headers.TryAddWithoutValidation("Session-Token", sessionToken);
                await client.SendAsync(killRequest);
                return new GlpiConnectionTestResult { Success = true, GlpiVersion = version, Message = "Conexão com GLPI validada com sucesso.", Warnings = string.IsNullOrWhiteSpace(request.ClassificationFieldKey) || string.IsNullOrWhiteSpace(request.DevOpsUrlFieldKey) ? new List<string> { "Configure as chaves técnicas dos campos adicionais após a validação da conexão." } : new List<string>() };
            }
            catch (Exception exception)
            {
                return new GlpiConnectionTestResult { Message = exception.Message };
            }
        }

        private HttpClient CreateClient(string baseUrl, string appToken)
        {
            var client = _httpClientFactory.CreateClient();
            var normalizedBaseUrl = baseUrl.Trim().TrimEnd('/');
            var apiBaseUrl = normalizedBaseUrl.EndsWith("/apirest.php", StringComparison.OrdinalIgnoreCase)
                ? normalizedBaseUrl
                : $"{normalizedBaseUrl}/apirest.php";
            client.BaseAddress = new Uri($"{apiBaseUrl}/");
            client.DefaultRequestHeaders.Add("App-Token", appToken);
            return client;
        }

        private string? ResolveToken(string? requestedToken, string? protectedToken)
        {
            if (!string.IsNullOrWhiteSpace(requestedToken) && requestedToken != "********") return requestedToken;
            return string.IsNullOrWhiteSpace(protectedToken) ? null : _protector.Unprotect(protectedToken);
        }

        private static string DescribeConnectionError(string? response)
        {
            if (response?.Contains("ERROR_SESSION_TOKEN_MISSING", StringComparison.OrdinalIgnoreCase) == true ||
                response?.Contains("ERROR_GLPI_LOGIN_USER_TOKEN", StringComparison.OrdinalIgnoreCase) == true ||
                response?.Contains("ERROR_WRONG_USER_TOKEN", StringComparison.OrdinalIgnoreCase) == true)
            {
                return $"O GLPI não criou uma sessão para a credencial informada.\n\nDiagnóstico técnico:\n- Rota: /apirest.php/initSession\n- Autorização enviada: user_token USER_TOKEN\n- Código GLPI: ERROR_SESSION_TOKEN_MISSING\n- Retorno: {SanitizeGlpiResponse(response)}\n\nVerifique se o mesmo APP_TOKEN e USER_TOKEN usados na integração funcional foram salvos nesta tela.";
            }

            if (response?.Contains("ERROR_WRONG_APP_TOKEN_PARAMETER", StringComparison.OrdinalIgnoreCase) == true)
                return $"O GLPI não reconheceu o APP_TOKEN informado.\n\nDiagnóstico técnico:\n- Rota: /apirest.php/initSession\n- Autorização enviada: user_token USER_TOKEN\n- Código GLPI: ERROR_WRONG_APP_TOKEN_PARAMETER\n- Retorno: {SanitizeGlpiResponse(response)}";

            return $"Não foi possível iniciar uma sessão no GLPI.\n\nDiagnóstico técnico:\n- Rota: /apirest.php/initSession\n- Autorização enviada: user_token USER_TOKEN\n- Retorno: {SanitizeGlpiResponse(response)}";
        }

        private static string SanitizeGlpiResponse(string? response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return "nenhum detalhe retornado.";

            const int maximumLength = 500;
            return response.Length <= maximumLength ? response : $"{response[..maximumLength]}...";
        }
    }
}
