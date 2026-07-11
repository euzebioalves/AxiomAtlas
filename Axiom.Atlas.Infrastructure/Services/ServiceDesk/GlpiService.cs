using Axiom.Atlas.Application.DTOs.Integrations;
using Axiom.Atlas.Application.DTOs.ServiceDesk;
using Axiom.Atlas.Domain.Entities.Integrations;
using Axiom.Atlas.Domain.Entities.ServiceDesk;
using Axiom.Atlas.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

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

        public async Task<GlpiTicketWorkspaceDto> ImportTicketAsync(string query, string userId)
        {
            var setting = await _context.Integrations.FirstOrDefaultAsync(x => x.Provider == "GLPI" && x.IsActive)
                ?? throw new InvalidOperationException("Configure a integração GLPI antes de importar chamados.");
            var appToken = UnprotectRequired(setting.SecondaryToken, "APP_TOKEN");
            var userToken = UnprotectRequired(setting.PrimaryToken, "USER_TOKEN");
            using var client = CreateClient(setting.BaseUrl ?? throw new InvalidOperationException("BASE_URL do GLPI não configurada."), appToken);
            var sessionToken = await CreateSessionAsync(client, userToken);
            try
            {
                var ticketId = long.TryParse(query.Trim(), out var parsedId)
                    ? parsedId
                    : await FindTicketIdAsync(client, sessionToken, query);
                var ticket = await GetJsonAsync(client, sessionToken, $"Ticket/{ticketId}");
                var followUps = await GetJsonAsync(client, sessionToken, $"Ticket/{ticketId}/ITILFollowup");
                var attachments = await GetJsonAsync(client, sessionToken, $"Ticket/{ticketId}/Document_Item");
                var entityId = TryReadInt(ticket.RootElement, "entities_id");
                var entityPath = entityId.HasValue ? await GetEntityPathAsync(client, sessionToken, entityId.Value) : null;
                var subject = TryReadString(ticket.RootElement, "name") ?? $"Chamado #{ticketId}";
                var workspace = await _context.GlpiTicketWorkspaces.FirstOrDefaultAsync(x => x.GlpiTicketId == ticketId);
                if (workspace == null)
                {
                    workspace = new GlpiTicketWorkspace { GlpiTicketId = ticketId, CreatedByUserId = userId };
                    _context.GlpiTicketWorkspaces.Add(workspace);
                }
                workspace.Subject = subject;
                workspace.EntityPath = entityPath;
                workspace.ClientEntityName = GetClientEntity(entityPath);
                workspace.TicketPayloadJson = ticket.RootElement.GetRawText();
                workspace.FollowUpsJson = await EnrichFollowUpsAsync(client, sessionToken, followUps.RootElement);
                workspace.AttachmentsJson = await EnrichAttachmentsAsync(client, sessionToken, attachments.RootElement, setting.BaseUrl!);
                workspace.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return ToDto(workspace);
            }
            finally
            {
                using var kill = new HttpRequestMessage(HttpMethod.Get, "killSession");
                kill.Headers.TryAddWithoutValidation("Session-Token", sessionToken);
                await client.SendAsync(kill);
            }
        }

        public async Task<GlpiTicketWorkspaceDto> SaveDraftAsync(Guid id, string? markdown)
        {
            var workspace = await _context.GlpiTicketWorkspaces.FindAsync(id) ?? throw new KeyNotFoundException("Chamado importado não encontrado.");
            workspace.RequirementMarkdown = markdown;
            workspace.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ToDto(workspace);
        }

        public async Task<GlpiTicketWorkspaceDto?> GetWorkspaceAsync(Guid id)
        {
            var workspace = await _context.GlpiTicketWorkspaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return workspace == null ? null : ToDto(workspace);
        }

        public async Task<(byte[] Content, string ContentType)> DownloadAttachmentAsync(Guid workspaceId, int documentId)
        {
            var workspace = await _context.GlpiTicketWorkspaces.FindAsync(workspaceId) ?? throw new KeyNotFoundException("Chamado importado não encontrado.");
            using var attachments = JsonDocument.Parse(workspace.AttachmentsJson);
            var attachmentBelongsToWorkspace = attachments.RootElement.ValueKind == JsonValueKind.Array && attachments.RootElement
                .EnumerateArray()
                .Any(x => TryReadInt(x, "documents_id") == documentId);
            if (!attachmentBelongsToWorkspace) throw new KeyNotFoundException("Anexo não encontrado neste chamado.");

            var setting = await _context.Integrations.FirstOrDefaultAsync(x => x.Provider == "GLPI" && x.IsActive) ?? throw new InvalidOperationException("GLPI não configurado.");
            using var client = CreateClient(setting.BaseUrl!, UnprotectRequired(setting.SecondaryToken, "APP_TOKEN"));
            var session = await CreateSessionAsync(client, UnprotectRequired(setting.PrimaryToken, "USER_TOKEN"));
            try
            {
                // The web endpoint requires an interactive GLPI cookie. The REST endpoint returns the raw file for the API session.
                using var request = new HttpRequestMessage(HttpMethod.Get, $"Document/{documentId}?alt=media");
                request.Headers.TryAddWithoutValidation("Session-Token", session);
                request.Headers.TryAddWithoutValidation("Accept", "application/octet-stream");
                using var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Não foi possível obter o anexo no GLPI.");
                var content = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType?.Equals("text/html", StringComparison.OrdinalIgnoreCase) == true)
                    throw new InvalidOperationException("O GLPI retornou uma página HTML em vez do arquivo do anexo.");

                return (content, contentType ?? "application/octet-stream");
            }
            finally { using var kill = new HttpRequestMessage(HttpMethod.Get, "killSession"); kill.Headers.TryAddWithoutValidation("Session-Token", session); await client.SendAsync(kill); }
        }

        private async Task<string> CreateSessionAsync(HttpClient client, string userToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "initSession?get_full_session=true");
            request.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            request.Headers.TryAddWithoutValidation("Authorization", $"user_token {userToken}");
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException(DescribeConnectionError($"{(int)response.StatusCode}: {body}"));
            using var document = JsonDocument.Parse(body);
            return document.RootElement.GetProperty("session_token").GetString() ?? throw new InvalidOperationException("GLPI não retornou session_token.");
        }

        private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string sessionToken, string endpoint)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.TryAddWithoutValidation("Session-Token", sessionToken);
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"GLPI retornou {(int)response.StatusCode} ao consultar {endpoint}: {body}");
            return JsonDocument.Parse(body);
        }

        private static async Task<long> FindTicketIdAsync(HttpClient client, string sessionToken, string subject)
        {
            var endpoint = $"search/Ticket?criteria[0][field]=1&criteria[0][searchtype]=contains&criteria[0][value]={Uri.EscapeDataString(subject)}&range=0-0";
            using var results = await GetJsonAsync(client, sessionToken, endpoint);
            if (!results.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0) throw new KeyNotFoundException("Nenhum chamado foi encontrado para o assunto informado.");
            var first = data[0];
            foreach (var property in first.EnumerateObject()) if (long.TryParse(property.Value.ToString(), out var id)) return id;
            throw new KeyNotFoundException("O GLPI não retornou o ID do chamado encontrado.");
        }

        private static async Task<string?> GetEntityPathAsync(HttpClient client, string sessionToken, int entityId)
        {
            using var entity = await GetJsonAsync(client, sessionToken, $"Entity/{entityId}");
            return TryReadString(entity.RootElement, "completename") ?? TryReadString(entity.RootElement, "name");
        }

        private static async Task<string> EnrichFollowUpsAsync(HttpClient client, string sessionToken, JsonElement source)
        {
            var followUps = JsonNode.Parse(source.GetRawText())?.AsArray() ?? new JsonArray();
            var users = new Dictionary<int, string>();
            foreach (var followUp in followUps.OfType<JsonObject>())
            {
                var userId = followUp["users_id"]?.GetValue<int?>();
                if (!userId.HasValue || userId <= 0) continue;
                if (!users.TryGetValue(userId.Value, out var name))
                {
                    using var user = await GetJsonAsync(client, sessionToken, $"User/{userId}");
                    name = TryReadString(user.RootElement, "realname") ?? TryReadString(user.RootElement, "name") ?? $"Usuário GLPI #{userId}";
                    var firstName = TryReadString(user.RootElement, "firstname");
                    if (!string.IsNullOrWhiteSpace(firstName) && !name.Contains(firstName, StringComparison.OrdinalIgnoreCase)) name = $"{firstName} {name}".Trim();
                    users[userId.Value] = name;
                }
                followUp["authorName"] = name;
            }
            return followUps.ToJsonString();
        }

        private static async Task<string> EnrichAttachmentsAsync(HttpClient client, string sessionToken, JsonElement source, string baseUrl)
        {
            var attachments = JsonNode.Parse(source.GetRawText())?.AsArray() ?? new JsonArray();
            var webBaseUrl = baseUrl.TrimEnd('/');
            if (webBaseUrl.EndsWith("/apirest.php", StringComparison.OrdinalIgnoreCase)) webBaseUrl = webBaseUrl[..^"/apirest.php".Length];
            foreach (var attachment in attachments.OfType<JsonObject>())
            {
                var documentId = attachment["documents_id"]?.GetValue<int?>();
                if (!documentId.HasValue) continue;
                using var document = await GetJsonAsync(client, sessionToken, $"Document/{documentId}");
                attachment["documentName"] = TryReadString(document.RootElement, "name") ?? TryReadString(document.RootElement, "filename") ?? $"Documento #{documentId}";
                attachment["documentUrl"] = $"{webBaseUrl}/front/document.send.php?docid={documentId}";
            }
            return attachments.ToJsonString();
        }

        private static string? GetClientEntity(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var segments = path.Split('>', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 1 ? segments[1] : segments.FirstOrDefault();
        }

        private static int? TryReadInt(JsonElement element, string name) => element.TryGetProperty(name, out var value) && int.TryParse(value.ToString(), out var result) ? result : null;
        private static string? TryReadString(JsonElement element, string name) => element.TryGetProperty(name, out var value) ? value.GetString() : null;
        private string UnprotectRequired(string? token, string label) => string.IsNullOrWhiteSpace(token) ? throw new InvalidOperationException($"{label} do GLPI não configurado.") : _protector.Unprotect(token);
        private static GlpiTicketWorkspaceDto ToDto(GlpiTicketWorkspace x) => new() { Id = x.Id, GlpiTicketId = x.GlpiTicketId, Subject = x.Subject, EntityPath = x.EntityPath, ClientEntityName = x.ClientEntityName, Classification = x.Classification, TicketPayloadJson = x.TicketPayloadJson, FollowUpsJson = x.FollowUpsJson, AttachmentsJson = x.AttachmentsJson, RequirementMarkdown = x.RequirementMarkdown, OpenProjectWorkPackageId = x.OpenProjectWorkPackageId, OpenProjectWorkPackageUrl = x.OpenProjectWorkPackageUrl, GlpiDevOpsFieldId = x.GlpiDevOpsFieldId, GlpiDevOpsUrl = x.GlpiDevOpsUrl };

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
