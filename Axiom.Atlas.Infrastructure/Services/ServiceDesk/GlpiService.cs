using Axiom.Atlas.Application.DTOs.Integrations;
using Axiom.Atlas.Application.DTOs.ServiceDesk;
using Axiom.Atlas.Domain.Entities.Integrations;
using Axiom.Atlas.Domain.Entities.ServiceDesk;
using Axiom.Atlas.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Axiom.Atlas.Infrastructure.Services.TimeEntries;

namespace Axiom.Atlas.Infrastructure.Services.ServiceDesk
{
    public class GlpiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppDbContext _context;
        private readonly IDataProtector _protector;
        private readonly OpenProjectService _openProjectService;
        private static readonly ConcurrentDictionary<string, CachedGlpiSession> GlpiSessions = new();
        private static readonly ConcurrentDictionary<string, GlpiSearchFieldMetadata> SearchFieldMetadata = new();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> SessionLocks = new();
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> WorkspaceLocks = new();

        public GlpiService(IHttpClientFactory httpClientFactory, AppDbContext context, IDataProtectionProvider provider, OpenProjectService openProjectService)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _protector = provider.CreateProtector("AxiomAtlas.Integrations");
            _openProjectService = openProjectService;
        }

        public async Task<GlpiConnectionTestResult> TestConnectionAsync(TestGlpiConnectionRequest request)
        {
            try
            {
                var saved = await _context.Integrations.FirstOrDefaultAsync(x =>
                    x.Provider == "GLPI" &&
                    (!string.IsNullOrWhiteSpace(request.Environment)
                        ? x.Environment == request.Environment
                        : x.IsActive));
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
            var sessionToken = await GetCachedSessionAsync(client, setting.BaseUrl!, userToken, CancellationToken.None);
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

        public async Task<GlpiTicketWorkspaceDto> SaveDraftAsync(Guid id, string? markdown)
        {
            var workspace = await _context.GlpiTicketWorkspaces.FindAsync(id) ?? throw new KeyNotFoundException("Chamado importado não encontrado.");
            workspace.RequirementMarkdown = markdown;
            workspace.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ToDto(workspace);
        }

        public Task<List<OpenProjectProjectOptionDto>> GetOpenProjectProjectsAsync() => _openProjectService.GetProjectsAsync();

        public async Task<GlpiTicketWorkspaceDto> CreateUserStoryAsync(Guid workspaceId, CreateOpenProjectUserStoryRequest request)
        {
            var workspaceLock = WorkspaceLocks.GetOrAdd(workspaceId, _ => new SemaphoreSlim(1, 1));
            await workspaceLock.WaitAsync();
            try
            {
                var workspace = await _context.GlpiTicketWorkspaces.FindAsync(workspaceId)
                    ?? throw new KeyNotFoundException("Chamado importado não encontrado.");
                var markdown = request.RequirementMarkdown?.Trim();
                if (!string.IsNullOrWhiteSpace(markdown))
                {
                    workspace.RequirementMarkdown = markdown;
                }

                if (string.IsNullOrWhiteSpace(workspace.RequirementMarkdown))
                {
                    throw new InvalidOperationException("Documente a User Story antes de enviá-la para o OpenProject.");
                }

                if (!workspace.OpenProjectWorkPackageId.HasValue)
                {
                    var glpiBaseUrl = await _context.Integrations
                        .Where(x => x.Provider == "GLPI" && x.IsActive)
                        .Select(x => x.BaseUrl)
                        .FirstOrDefaultAsync();
                    var glpiTicketUrl = BuildTicketWebUrl(glpiBaseUrl, workspace.GlpiTicketId);
                    var created = await _openProjectService.CreateUserStoryAsync(
                        request.ProjectId,
                        workspace.Subject,
                        workspace.RequirementMarkdown,
                        workspace.ClientEntityName ?? string.Empty,
                        workspace.GlpiTicketId,
                        glpiTicketUrl ?? string.Empty);
                    workspace.OpenProjectWorkPackageId = created.Id;
                    workspace.OpenProjectWorkPackageUrl = created.Url;
                    workspace.UpdatedAt = DateTime.UtcNow;

                    // Persist before updating GLPI so a retry only repairs the link and never duplicates the User Story.
                    await _context.SaveChangesAsync();
                }
                else if (string.IsNullOrWhiteSpace(workspace.OpenProjectWorkPackageUrl))
                {
                    var workPackage = await _openProjectService.GetWorkPackageAsync(workspace.OpenProjectWorkPackageId.Value);
                    workspace.OpenProjectWorkPackageUrl = OpenProjectService.BuildWorkPackageWebUrl(
                        await _openProjectService.GetActiveOpenProjectBaseUrlAsync(),
                        workPackage);
                    workspace.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                if (string.IsNullOrWhiteSpace(workspace.OpenProjectWorkPackageUrl))
                {
                    throw new InvalidOperationException("A User Story foi criada, mas não foi possível determinar sua URL no OpenProject.");
                }

                if (!string.Equals(workspace.GlpiDevOpsUrl, workspace.OpenProjectWorkPackageUrl, StringComparison.OrdinalIgnoreCase))
                {
                    await UpdateGlpiDevOpsUrlAsync(workspace, workspace.OpenProjectWorkPackageUrl);
                    workspace.GlpiDevOpsUrl = workspace.OpenProjectWorkPackageUrl;
                    workspace.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                // The queue is a local projection. Refresh it immediately after a successful
                // GLPI update so the operator sees the new User Story when returning to the list.
                await RefreshImprovementTicketWorkPackageProjectionAsync(workspace);

                return ToDto(workspace);
            }
            finally
            {
                workspaceLock.Release();
            }
        }

        public async Task<GlpiImprovementTicketsResponse> GetImprovementTicketsAsync(int page, int pageSize, string? statusFilter)
        {
            page = Math.Max(1, page);
            pageSize = pageSize is 10 or 25 or 50 or 100 ? pageSize : 25;
            var normalizedStatusFilter = NormalizeStatusFilter(statusFilter);
            var query = _context.GlpiImprovementTickets.AsNoTracking()
                .Where(x => x.IsInImprovementQueue);
            query = normalizedStatusFilter switch
            {
                "new" => query.Where(x => x.StatusCode == 1),
                "processing_assigned" => query.Where(x => x.StatusCode == 2),
                "processing_planned" => query.Where(x => x.StatusCode == 3),
                "pending" => query.Where(x => x.StatusCode == 4),
                "solved" => query.Where(x => x.StatusCode == 5),
                "closed" => query.Where(x => x.StatusCode == 6),
                "not_solved" => query.Where(x => x.StatusCode != 5 && x.StatusCode != 6),
                _ => query
            };

            var totalCount = await query.CountAsync();
            var lastSynchronizedAt = await query.Select(x => (DateTime?)x.LastSynchronizedAt).MaxAsync();
            var tickets = await query.OrderBy(x => x.OpenedAt)
                .ThenBy(x => x.GlpiTicketId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new GlpiImprovementTicketsResponse
            {
                Items = tickets.Select(ToImprovementDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                StatusFilter = normalizedStatusFilter,
                LastSynchronizedAt = lastSynchronizedAt
            };
        }

        public async Task SynchronizeImprovementTicketsAsync(CancellationToken cancellationToken = default)
        {
            var setting = await _context.Integrations.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Provider == "GLPI" && x.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("Configure e ative uma integração GLPI antes de sincronizar as demandas.");

            var appToken = UnprotectRequired(setting.SecondaryToken, "APP_TOKEN");
            var userToken = UnprotectRequired(setting.PrimaryToken, "USER_TOKEN");
            using var client = CreateClient(setting.BaseUrl ?? throw new InvalidOperationException("BASE_URL do GLPI não configurada."), appToken);
            var sessionToken = await GetCachedSessionAsync(client, setting.BaseUrl!, userToken, cancellationToken);
            var fieldMetadata = await GetSearchFieldMetadataAsync(client, sessionToken, setting.BaseUrl!, setting.AdditionalSettings, cancellationToken);
            var endpoint = $"search/Ticket?criteria[0][field]={fieldMetadata.ClassificationFieldId}&criteria[0][searchtype]=contains&criteria[0][value]={Uri.EscapeDataString("Solicitação de Melhoria")}&range=0-999";
            if (fieldMetadata.DevOpsSearchFieldId.HasValue)
            {
                endpoint += $"&forcedisplay[0]={fieldMetadata.DevOpsSearchFieldId.Value}";
            }
            endpoint += $"&forcedisplay[{(fieldMetadata.DevOpsSearchFieldId.HasValue ? 1 : 0)}]=12";

            using var result = await GetJsonAsync(client, sessionToken, endpoint, cancellationToken);
            if (!result.RootElement.TryGetProperty("data", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var candidates = rows.EnumerateArray()
                .Select(row => new { TicketId = ReadSearchTicketId(row), DevOpsUrl = ReadSearchFieldValue(row, fieldMetadata.DevOpsSearchFieldId) })
                .Where(x => x.TicketId.HasValue)
                .Select(x => new SearchTicketCandidate(x.TicketId!.Value, x.DevOpsUrl))
                .ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            var workspaces = await _context.GlpiTicketWorkspaces
                .Where(x => x.OpenProjectWorkPackageId != null || x.GlpiDevOpsUrl != null || x.OpenProjectWorkPackageUrl != null)
                .ToDictionaryAsync(x => x.GlpiTicketId, cancellationToken);
            var snapshots = await FetchTicketSnapshotsAsync(client, sessionToken, candidates, fieldMetadata.DevOpsFieldKey, cancellationToken);
            var pluginRecords = await GetPluginFieldsTicketRecordsAsync(
                client,
                sessionToken,
                snapshots.Select(x => x.TicketId).ToHashSet(),
                cancellationToken);
            var currentDevOpsUrls = pluginRecords.ToDictionary(
                x => x.Key,
                x => ReadPluginFieldsDevOpsUrl(x.Value, fieldMetadata.DevOpsFieldKey));
            var entityPaths = await GetEntityPathsAsync(client, sessionToken, snapshots.Select(x => x.EntityId), cancellationToken);
            var workPackageIds = snapshots
                .Select(x => currentDevOpsUrls.TryGetValue(x.TicketId, out var devOpsUrl)
                    ? ExtractWorkPackageId(devOpsUrl)
                    : null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToArray();
            var workPackages = workPackageIds.Length == 0
                ? new Dictionary<int, Axiom.Atlas.Application.DTOs.TimeEntries.OpenProjectWorkPackageSummaryDto>()
                : await _openProjectService.GetWorkPackageSummariesAsync(workPackageIds, cancellationToken);

            var existing = await _context.GlpiImprovementTickets
                .ToDictionaryAsync(x => x.GlpiTicketId, cancellationToken);
            var now = DateTime.UtcNow;
            var synchronizedTicketIds = new HashSet<long>();
            foreach (var snapshot in snapshots)
            {
                synchronizedTicketIds.Add(snapshot.TicketId);
                if (!existing.TryGetValue(snapshot.TicketId, out var localTicket))
                {
                    localTicket = new GlpiImprovementTicket { GlpiTicketId = snapshot.TicketId };
                    _context.GlpiImprovementTickets.Add(localTicket);
                }

                workspaces.TryGetValue(snapshot.TicketId, out var workspace);

                // The Fields plugin is the source of truth. Do not keep a locally remembered
                // association visible when the ChamadoDevops value was changed or cleared in GLPI.
                currentDevOpsUrls.TryGetValue(snapshot.TicketId, out var devOpsUrl);
                var workPackageId = ExtractWorkPackageId(devOpsUrl);
                if (!workPackageId.HasValue)
                {
                    devOpsUrl = null;
                }

                if (workspace is not null)
                {
                    workspace.GlpiDevOpsUrl = devOpsUrl;
                }
                workPackages.TryGetValue(workPackageId ?? 0, out var workPackage);
                entityPaths.TryGetValue(snapshot.EntityId ?? 0, out var entityPath);

                localTicket.Subject = snapshot.Subject;
                localTicket.GlpiTicketUrl = BuildTicketWebUrl(setting.BaseUrl, snapshot.TicketId);
                localTicket.OpenedAt = snapshot.OpenedAt;
                localTicket.StatusCode = snapshot.StatusCode;
                localTicket.StatusName = GetGlpiStatusName(snapshot.StatusCode);
                localTicket.EntityPath = entityPath;
                localTicket.ClientEntityName = GetClientEntity(entityPath);
                localTicket.WorkPackageId = workPackageId;
                localTicket.WorkPackageUrl = devOpsUrl;
                localTicket.WorkPackageStatus = workPackage?.StatusName;
                localTicket.WorkPackageCreator = workPackage?.CreatorName;
                localTicket.WorkPackageCreatedAt = workPackage?.CreatedAt;
                localTicket.IsInImprovementQueue = true;
                localTicket.LastSynchronizedAt = now;
            }

            foreach (var localTicket in existing.Values.Where(x => !synchronizedTicketIds.Contains(x.GlpiTicketId)))
            {
                localTicket.IsInImprovementQueue = false;
                localTicket.LastSynchronizedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task RefreshImprovementTicketWorkPackageProjectionAsync(GlpiTicketWorkspace workspace)
        {
            var ticket = await _context.GlpiImprovementTickets.FindAsync(workspace.GlpiTicketId);
            if (ticket is null)
            {
                return;
            }

            var workPackageId = ExtractWorkPackageId(workspace.GlpiDevOpsUrl);
            var workPackage = workPackageId.HasValue
                ? await _openProjectService.GetWorkPackageSummaryAsync(workPackageId.Value)
                : null;

            ticket.WorkPackageId = workPackageId;
            ticket.WorkPackageUrl = workPackageId.HasValue ? workspace.GlpiDevOpsUrl : null;
            ticket.WorkPackageStatus = workPackage?.StatusName;
            ticket.WorkPackageCreator = workPackage?.CreatorName;
            ticket.WorkPackageCreatedAt = workPackage?.CreatedAt;
            ticket.LastSynchronizedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        private async Task<List<GlpiTicketSnapshot>> FetchTicketSnapshotsAsync(
            HttpClient client,
            string sessionToken,
            IReadOnlyCollection<SearchTicketCandidate> candidates,
            string? devOpsFieldKey,
            CancellationToken cancellationToken)
        {
            using var concurrencyGate = new SemaphoreSlim(8);
            var tasks = candidates.Select(async candidate =>
            {
                await concurrencyGate.WaitAsync(cancellationToken);
                try
                {
                    using var ticket = await GetJsonAsync(client, sessionToken, $"Ticket/{candidate.TicketId}", cancellationToken);
                    return new GlpiTicketSnapshot(
                        candidate.TicketId,
                        TryReadString(ticket.RootElement, "name") ?? $"Chamado #{candidate.TicketId}",
                        TryReadInt(ticket.RootElement, "entities_id"),
                        TryReadDateTime(ticket.RootElement, "date"),
                        TryReadInt(ticket.RootElement, "status"),
                        candidate.DevOpsUrl ?? FindConfiguredFieldValue(ticket.RootElement, devOpsFieldKey));
                }
                catch
                {
                    // A single inaccessible ticket must not invalidate the entire local queue.
                    return null;
                }
                finally
                {
                    concurrencyGate.Release();
                }
            });

            return (await Task.WhenAll(tasks)).Where(x => x is not null).Select(x => x!).ToList();
        }

        private static async Task<Dictionary<int, string?>> GetEntityPathsAsync(
            HttpClient client,
            string sessionToken,
            IEnumerable<int?> entityIds,
            CancellationToken cancellationToken)
        {
            var ids = entityIds.Where(x => x.HasValue && x.Value > 0).Select(x => x!.Value).Distinct().ToArray();
            using var concurrencyGate = new SemaphoreSlim(6);
            var tasks = ids.Select(async entityId =>
            {
                await concurrencyGate.WaitAsync(cancellationToken);
                try
                {
                    return new KeyValuePair<int, string?>(entityId, await GetEntityPathAsync(client, sessionToken, entityId, cancellationToken));
                }
                catch
                {
                    return new KeyValuePair<int, string?>(entityId, null);
                }
                finally
                {
                    concurrencyGate.Release();
                }
            });

            return (await Task.WhenAll(tasks)).ToDictionary(x => x.Key, x => x.Value);
        }

        private async Task<GlpiSearchFieldMetadata> GetSearchFieldMetadataAsync(
            HttpClient client,
            string sessionToken,
            string baseUrl,
            string? additionalSettings,
            CancellationToken cancellationToken)
        {
            var configured = ReadAdditionalSettings(additionalSettings);
            var configuredClassification = int.TryParse(configured.GetValueOrDefault("classificationFieldKey"), out var classificationId) ? classificationId : (int?)null;
            var configuredDevOps = int.TryParse(configured.GetValueOrDefault("devOpsUrlFieldKey"), out var devOpsId) ? devOpsId : (int?)null;
            var configuredClassificationFieldKey = configuredClassification.HasValue
                ? null
                : configured.GetValueOrDefault("classificationFieldKey")?.Trim();
            var configuredDevOpsFieldKey = configuredDevOps.HasValue
                ? null
                : configured.GetValueOrDefault("devOpsUrlFieldKey")?.Trim();

            var cacheKey = $"glpi:search-fields:{baseUrl.TrimEnd('/').ToLowerInvariant()}:{configured.GetValueOrDefault("classificationFieldKey")}:{configured.GetValueOrDefault("devOpsUrlFieldKey")}";
            if (SearchFieldMetadata.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return cached;
            }

            using var options = await GetJsonAsync(client, sessionToken, "listSearchOptions/Ticket", cancellationToken);
            var discoveredClassification = configuredClassification ?? 76673;
            var discoveredClassificationFieldKey = configuredClassificationFieldKey;
            int? discoveredDevOps = configuredDevOps;
            var discoveredDevOpsFieldKey = configuredDevOpsFieldKey;
            if (options.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var option in options.RootElement.EnumerateObject())
                {
                    if (!int.TryParse(option.Name, out var optionId) || option.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var name = TryReadString(option.Value, "name") ?? string.Empty;
                    if (name.Contains("classificação", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!configuredClassification.HasValue)
                        {
                            discoveredClassification = optionId;
                        }

                        discoveredClassificationFieldKey ??= GetSearchOptionTechnicalFieldKey(option.Value);
                    }

                    var isDevOpsField = name.Contains("chamadodevops", StringComparison.OrdinalIgnoreCase) ||
                                        (name.Contains("atividade", StringComparison.OrdinalIgnoreCase) && name.Contains("devops", StringComparison.OrdinalIgnoreCase));
                    if (isDevOpsField)
                    {
                        discoveredDevOps ??= optionId;
                        discoveredDevOpsFieldKey ??= GetSearchOptionTechnicalFieldKey(option.Value);
                    }
                }
            }

            var metadata = new GlpiSearchFieldMetadata(
                discoveredClassification,
                discoveredClassificationFieldKey,
                discoveredDevOps,
                discoveredDevOpsFieldKey,
                DateTimeOffset.UtcNow.AddHours(1));
            SearchFieldMetadata[cacheKey] = metadata;
            return metadata;
        }

        private async Task<string> GetCachedSessionAsync(HttpClient client, string baseUrl, string userToken, CancellationToken cancellationToken)
        {
            var userTokenFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userToken)));
            var cacheKey = $"glpi:session:{baseUrl.TrimEnd('/').ToLowerInvariant()}:{userTokenFingerprint}";
            if (GlpiSessions.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return cached.Token;
            }

            var sessionLock = SessionLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await sessionLock.WaitAsync(cancellationToken);
            try
            {
                if (GlpiSessions.TryGetValue(cacheKey, out cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
                {
                    return cached.Token;
                }

                var token = await CreateSessionAsync(client, userToken, cancellationToken);
                GlpiSessions[cacheKey] = new CachedGlpiSession(token, DateTimeOffset.UtcNow.AddMinutes(15));
                return token;
            }
            finally
            {
                sessionLock.Release();
            }
        }

        private static GlpiImprovementTicketDto ToImprovementDto(GlpiImprovementTicket ticket)
        {
            return new GlpiImprovementTicketDto
            {
                GlpiTicketId = ticket.GlpiTicketId,
                Subject = ticket.Subject,
                GlpiTicketUrl = ticket.GlpiTicketUrl,
                OpenedAt = ticket.OpenedAt,
                DaysOpen = ticket.OpenedAt.HasValue ? Math.Max(0, (int)(DateTime.UtcNow.Date - ticket.OpenedAt.Value.Date).TotalDays) : 0,
                ClientEntityName = ticket.ClientEntityName,
                GlpiStatusName = ticket.StatusName,
                WorkPackageId = ticket.WorkPackageId,
                WorkPackageUrl = ticket.WorkPackageUrl,
                WorkPackageStatus = ticket.WorkPackageStatus,
                WorkPackageCreator = ticket.WorkPackageCreator,
                WorkPackageDaysOpen = ticket.WorkPackageCreatedAt is { } createdAt
                    ? Math.Max(0, (int)(DateTime.UtcNow.Date - createdAt.Date).TotalDays)
                    : null
            };
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
            var session = await GetCachedSessionAsync(client, setting.BaseUrl!, UnprotectRequired(setting.PrimaryToken, "USER_TOKEN"), CancellationToken.None);
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

        private async Task UpdateGlpiDevOpsUrlAsync(GlpiTicketWorkspace workspace, string workPackageUrl)
        {
            var setting = await _context.Integrations.FirstOrDefaultAsync(x => x.Provider == "GLPI" && x.IsActive)
                ?? throw new InvalidOperationException("GLPI não configurado para atualizar o vínculo da User Story.");
            using var client = CreateClient(setting.BaseUrl ?? throw new InvalidOperationException("BASE_URL do GLPI não configurada."), UnprotectRequired(setting.SecondaryToken, "APP_TOKEN"));
            var session = await GetCachedSessionAsync(client, setting.BaseUrl!, UnprotectRequired(setting.PrimaryToken, "USER_TOKEN"), CancellationToken.None);
            var pluginRecord = await GetPluginFieldsTicketRecordAsync(client, session, workspace.GlpiTicketId);
            if (pluginRecord is null)
            {
                throw new InvalidOperationException("A User Story foi criada no OpenProject, mas o registro do plugin Fields não foi encontrado para este chamado. Confirme que o perfil da integração possui acesso aos campos adicionais e tente vincular novamente.");
            }

            const string classificationField = "plugin_fields_classificaodechamadofielddropdowns_id";
            const string devOpsField = "atividadedevopfield";
            var pluginRecordId = TryReadInt(pluginRecord.Value, "id");
            var classificationId = TryReadInt(pluginRecord.Value, classificationField);
            if (!pluginRecordId.HasValue || !classificationId.HasValue || classificationId.Value <= 0)
            {
                throw new InvalidOperationException("A User Story foi criada no OpenProject, mas o GLPI não retornou a Classificação obrigatória do registro de campos adicionais. Confirme as permissões do perfil da integração e tente vincular novamente.");
            }

            // ChamadoDevops e Classificação pertencem ao item do plugin Fields, não ao Ticket.
            // Reenviamos a classificação obrigatória para preservar a validação do próprio plugin.
            var input = new Dictionary<string, object?>
            {
                ["id"] = pluginRecordId.Value,
                [devOpsField] = workPackageUrl,
                [classificationField] = classificationId.Value
            };
            using var request = new HttpRequestMessage(HttpMethod.Put, $"PluginFieldsTicketchamado/{pluginRecordId.Value}")
            {
                Content = JsonContent.Create(new { input })
            };
            request.Headers.TryAddWithoutValidation("Session-Token", session);
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"A User Story foi criada no OpenProject, mas o GLPI retornou {(int)response.StatusCode} ao atualizar o campo ChamadoDevops: {SanitizeGlpiResponse(responseBody)}");
            }

            workspace.GlpiDevOpsFieldId = devOpsField;
        }

        private static async Task<JsonElement?> GetPluginFieldsTicketRecordAsync(HttpClient client, string sessionToken, long ticketId)
        {
            const int pageSize = 1000;
            var firstPage = await GetPluginFieldsTicketRecordsPageAsync(client, sessionToken, 0, pageSize - 1);
            if (firstPage.Records.Count == 0)
            {
                return null;
            }

            var record = FindPluginFieldsTicketRecord(firstPage.Records, ticketId);
            if (record.HasValue)
            {
                return record;
            }

            for (var start = pageSize; start < firstPage.TotalCount; start += pageSize)
            {
                var page = await GetPluginFieldsTicketRecordsPageAsync(
                    client,
                    sessionToken,
                    start,
                    Math.Min(start + pageSize - 1, firstPage.TotalCount - 1));
                record = FindPluginFieldsTicketRecord(page.Records, ticketId);
                if (record.HasValue)
                {
                    return record;
                }
            }

            return null;
        }

        private static async Task<Dictionary<long, JsonElement>> GetPluginFieldsTicketRecordsAsync(
            HttpClient client,
            string sessionToken,
            IReadOnlySet<long> ticketIds,
            CancellationToken cancellationToken)
        {
            var recordsByTicket = new Dictionary<long, JsonElement>();
            if (ticketIds.Count == 0)
            {
                return recordsByTicket;
            }

            const int pageSize = 1000;
            var firstPage = await GetPluginFieldsTicketRecordsPageAsync(client, sessionToken, 0, pageSize - 1);
            AddRequestedPluginRecords(firstPage.Records, ticketIds, recordsByTicket);

            for (var start = pageSize; start < firstPage.TotalCount && recordsByTicket.Count < ticketIds.Count; start += pageSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = await GetPluginFieldsTicketRecordsPageAsync(
                    client,
                    sessionToken,
                    start,
                    Math.Min(start + pageSize - 1, firstPage.TotalCount - 1));
                AddRequestedPluginRecords(page.Records, ticketIds, recordsByTicket);
            }

            return recordsByTicket;
        }

        private static void AddRequestedPluginRecords(
            IEnumerable<JsonElement> records,
            IReadOnlySet<long> ticketIds,
            IDictionary<long, JsonElement> recordsByTicket)
        {
            foreach (var record in records)
            {
                var ticketId = TryReadLong(record, "items_id");
                if (ticketId.HasValue && ticketIds.Contains(ticketId.Value))
                {
                    recordsByTicket[ticketId.Value] = record;
                }
            }
        }

        private static async Task<(List<JsonElement> Records, int TotalCount)> GetPluginFieldsTicketRecordsPageAsync(
            HttpClient client,
            string sessionToken,
            int start,
            int end)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"PluginFieldsTicketchamado?range={start}-{end}");
            request.Headers.TryAddWithoutValidation("Session-Token", sessionToken);
            using var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"GLPI retornou {(int)response.StatusCode} ao consultar os campos adicionais do chamado: {SanitizeGlpiResponse(responseBody)}");
            }

            using var document = JsonDocument.Parse(responseBody);
            var records = document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray().Select(record => record.Clone()).ToList()
                : new List<JsonElement>();
            var contentRange = response.Content.Headers.TryGetValues("Content-Range", out var values)
                ? values.FirstOrDefault()
                : null;
            var totalCount = int.TryParse(contentRange?.Split('/').Last(), out var parsedTotal)
                ? parsedTotal
                : records.Count;
            return (records, totalCount);
        }

        private static JsonElement? FindPluginFieldsTicketRecord(IEnumerable<JsonElement> records, long ticketId)
        {
            foreach (var record in records)
            {
                if (TryReadLong(record, "items_id") == ticketId)
                {
                    return record;
                }
            }

            return null;
        }

        private static async Task<JsonElement?> GetTicketSearchFieldValueAsync(
            HttpClient client,
            string sessionToken,
            long ticketId,
            int fieldId)
        {
            // Campos do plugin Fields nem sempre aparecem no recurso Ticket/{id}. A busca
            // expõe o valor formatado da search option e permite preserva-lo no PUT.
            var endpoint = $"search/Ticket?criteria[0][field]=2&criteria[0][searchtype]=equals&criteria[0][value]={ticketId}&forcedisplay[0]={fieldId}&range=0-0";
            using var result = await GetJsonAsync(client, sessionToken, endpoint);
            if (!result.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array ||
                data.GetArrayLength() == 0)
            {
                return null;
            }

            var row = data[0];
            if (!row.TryGetProperty(fieldId.ToString(), out var value) ||
                value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())))
            {
                return null;
            }

            return value.Clone();
        }

        private async Task<string> CreateSessionAsync(HttpClient client, string userToken, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "initSession?get_full_session=true");
            request.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            request.Headers.TryAddWithoutValidation("Authorization", $"user_token {userToken}");
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException(DescribeConnectionError($"{(int)response.StatusCode}: {body}"));
            using var document = JsonDocument.Parse(body);
            return document.RootElement.GetProperty("session_token").GetString() ?? throw new InvalidOperationException("GLPI não retornou session_token.");
        }

        private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string sessionToken, string endpoint, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.TryAddWithoutValidation("Session-Token", sessionToken);
            using var response = await client.SendAsync(request, cancellationToken);
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

        private static async Task<int> GetClassificationFieldIdAsync(HttpClient client, string sessionToken, string? additionalSettings)
        {
            var configuredField = ReadAdditionalSettings(additionalSettings).GetValueOrDefault("classificationFieldKey");
            if (int.TryParse(configuredField, out var configuredId))
            {
                return configuredId;
            }

            try
            {
                using var options = await GetJsonAsync(client, sessionToken, "listSearchOptions/Ticket");
                if (options.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var option in options.RootElement.EnumerateObject())
                    {
                        if (!int.TryParse(option.Name, out var optionId) || option.Value.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var name = TryReadString(option.Value, "name");
                        if (name?.Contains("classificação", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return optionId;
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // The configured GLPI profile can hide search metadata; the known fallback still supports this installation.
            }

            // Fallback confirmado para a instalação atual; a descoberta acima prevalece em outros ambientes.
            return 76673;
        }

        private static async Task<int?> GetDevOpsSearchFieldIdAsync(HttpClient client, string sessionToken, string? configuredField)
        {
            if (int.TryParse(configuredField, out var configuredId))
            {
                return configuredId;
            }

            try
            {
                using var options = await GetJsonAsync(client, sessionToken, "listSearchOptions/Ticket");
                if (options.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                foreach (var option in options.RootElement.EnumerateObject())
                {
                    if (!int.TryParse(option.Name, out var optionId) || option.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var name = TryReadString(option.Value, "name");
                    if (name?.Contains("chamadodevops", StringComparison.OrdinalIgnoreCase) == true ||
                        (name?.Contains("atividade", StringComparison.OrdinalIgnoreCase) == true &&
                         name?.Contains("devops", StringComparison.OrdinalIgnoreCase) == true))
                    {
                        return optionId;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // The plugin can be unavailable in an environment without invalidating the entire list.
            }

            return null;
        }

        private static long? ReadSearchTicketId(JsonElement row)
        {
            foreach (var preferredKey in new[] { "2", "id" })
            {
                if (row.TryGetProperty(preferredKey, out var preferred) && long.TryParse(preferred.ToString(), out var ticketId))
                {
                    return ticketId;
                }
            }

            foreach (var property in row.EnumerateObject())
            {
                if (long.TryParse(property.Value.ToString(), out var ticketId) && ticketId > 1_000_000)
                {
                    return ticketId;
                }
            }

            return null;
        }

        private static string? ReadSearchFieldValue(JsonElement row, int? fieldId)
        {
            if (!fieldId.HasValue || !row.TryGetProperty(fieldId.Value.ToString(), out var value))
            {
                return null;
            }

            var result = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }

        private static int? ReadSearchStatus(JsonElement row)
        {
            if (row.TryGetProperty("12", out var statusValue) && int.TryParse(statusValue.ToString(), out var numericStatus))
            {
                return numericStatus;
            }

            var statusText = row.TryGetProperty("12", out statusValue) ? statusValue.ToString() : null;
            return statusText?.Trim().ToLowerInvariant() switch
            {
                "novo" => 1,
                "em atendimento (atribuído)" => 2,
                "em atendimento (planejado)" => 3,
                "pendente" => 4,
                "solucionado" => 5,
                "fechado" => 6,
                _ => null
            };
        }

        private static bool IsSolvedOrClosed(int? status) => status is 5 or 6;

        private static Dictionary<string, string?> ReadAdditionalSettings(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? new Dictionary<string, string?>()
                : JsonSerializer.Deserialize<Dictionary<string, string?>>(value) ?? new Dictionary<string, string?>();
        }

        private static string? GetSearchOptionTechnicalFieldKey(JsonElement option)
        {
            foreach (var propertyName in new[] { "field", "uid" })
            {
                var value = TryReadString(option, propertyName)?.Trim();
                if (!string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _))
                {
                    return value;
                }
            }

            return null;
        }

        private static string? FindConfiguredFieldValue(JsonElement element, string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString();
                    }

                    var nestedValue = FindConfiguredFieldValue(property.Value, key);
                    if (!string.IsNullOrWhiteSpace(nestedValue))
                    {
                        return nestedValue;
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in element.EnumerateArray())
                {
                    var nestedValue = FindConfiguredFieldValue(child, key);
                    if (!string.IsNullOrWhiteSpace(nestedValue))
                    {
                        return nestedValue;
                    }
                }
            }

            return null;
        }

        private static string? ReadPluginFieldsDevOpsUrl(JsonElement pluginRecord, string? configuredFieldKey)
        {
            var value = FindConfiguredFieldValue(pluginRecord, configuredFieldKey)
                ?? FindConfiguredFieldValue(pluginRecord, "atividadedevopfield")
                ?? FindConfiguredFieldValue(pluginRecord, "chamadodevopsfield")
                ?? FindConfiguredFieldValue(pluginRecord, "chamadodevops");

            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static JsonElement? FindConfiguredFieldElement(JsonElement element, string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return property.Value.Clone();
                    }

                    var nestedValue = FindConfiguredFieldElement(property.Value, key);
                    if (nestedValue.HasValue)
                    {
                        return nestedValue;
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in element.EnumerateArray())
                {
                    var nestedValue = FindConfiguredFieldElement(child, key);
                    if (nestedValue.HasValue)
                    {
                        return nestedValue;
                    }
                }
            }

            return null;
        }

        private static int? ExtractWorkPackageId(string? url)
        {
            var match = Regex.Match(url ?? string.Empty, @"/work_packages/(\d+)", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, out var id) ? id : null;
        }

        private static string? BuildTicketWebUrl(string? baseUrl, long ticketId)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return null;
            }

            var url = baseUrl.TrimEnd('/');
            if (url.EndsWith("/apirest.php", StringComparison.OrdinalIgnoreCase))
            {
                url = url[..^"/apirest.php".Length];
            }

            return $"{url}/front/ticket.form.php?id={ticketId}";
        }

        private static string NormalizeStatusFilter(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "all" or "new" or "processing_assigned" or "processing_planned" or "pending" or "solved" or "closed" => value.Trim().ToLowerInvariant(),
                _ => "not_solved"
            };
        }

        private static string BuildStatusCriteria(string statusFilter)
        {
            const int statusField = 12;
            return statusFilter switch
            {
                "all" => string.Empty,
                "not_solved" => $"&criteria[1][link]=AND&criteria[1][field]={statusField}&criteria[1][searchtype]=notequals&criteria[1][value]=5&criteria[2][link]=AND&criteria[2][field]={statusField}&criteria[2][searchtype]=notequals&criteria[2][value]=6",
                "new" => BuildEqualsStatusCriterion(statusField, 1),
                "processing_assigned" => BuildEqualsStatusCriterion(statusField, 2),
                "processing_planned" => BuildEqualsStatusCriterion(statusField, 3),
                "pending" => BuildEqualsStatusCriterion(statusField, 4),
                "solved" => BuildEqualsStatusCriterion(statusField, 5),
                "closed" => BuildEqualsStatusCriterion(statusField, 6),
                _ => string.Empty
            };
        }

        private static string BuildEqualsStatusCriterion(int field, int value) =>
            $"&criteria[1][link]=AND&criteria[1][field]={field}&criteria[1][searchtype]=equals&criteria[1][value]={value}";

        private static string GetGlpiStatusName(int? status) => status switch
        {
            1 => "Novo",
            2 => "Em atendimento (atribuído)",
            3 => "Em atendimento (planejado)",
            4 => "Pendente",
            5 => "Solucionado",
            6 => "Fechado",
            _ => "Não informado"
        };

        private static async Task<string?> GetEntityPathAsync(HttpClient client, string sessionToken, int entityId, CancellationToken cancellationToken = default)
        {
            using var entity = await GetJsonAsync(client, sessionToken, $"Entity/{entityId}", cancellationToken);
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
        private static long? TryReadLong(JsonElement element, string name) => element.TryGetProperty(name, out var value) && long.TryParse(value.ToString(), out var result) ? result : null;
        private static string? TryReadString(JsonElement element, string name) => element.TryGetProperty(name, out var value) ? value.GetString() : null;
        private static DateTime? TryReadDateTime(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value) || !DateTime.TryParse(value.GetString(), out var result))
            {
                return null;
            }

            return result.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(result, DateTimeKind.Utc)
                : result.ToUniversalTime();
        }
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

        private sealed record SearchTicketCandidate(long TicketId, string? DevOpsUrl);
        private sealed record GlpiTicketSnapshot(long TicketId, string Subject, int? EntityId, DateTime? OpenedAt, int? StatusCode, string? DevOpsUrl);
        private sealed record CachedGlpiSession(string Token, DateTimeOffset ExpiresAt);
        private sealed record GlpiSearchFieldMetadata(
            int ClassificationFieldId,
            string? ClassificationFieldKey,
            int? DevOpsSearchFieldId,
            string? DevOpsFieldKey,
            DateTimeOffset ExpiresAt);
    }
}
