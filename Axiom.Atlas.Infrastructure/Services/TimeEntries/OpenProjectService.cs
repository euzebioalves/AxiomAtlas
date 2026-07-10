using Axiom.Atlas.Application.DTOs.Integrations;
using Axiom.Atlas.Application.DTOs.TimeEntries;
using Axiom.Atlas.Domain.Entities.Integrations;
using Axiom.Atlas.Domain.Entities.TimeEntries;
using Axiom.Atlas.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Axiom.Atlas.Infrastructure.Services.TimeEntries
{
    public class OpenProjectService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppDbContext _context;
        private readonly IDataProtector _protector;

        public OpenProjectService(
            IHttpClientFactory httpClientFactory,
            AppDbContext context,
            IDataProtectionProvider provider)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _protector = provider.CreateProtector("AxiomAtlas.Integrations");
        }

        private async Task<HttpClient> CreateConfiguredClientAsync(string? environment = null)
        {
            var query = _context.Set<IntegrationSettings>()
                .Where(x => x.Provider == "OpenProject");

            var config = string.IsNullOrWhiteSpace(environment)
                ? await query.FirstOrDefaultAsync(x => x.IsActive)
                : await query.FirstOrDefaultAsync(x => x.Environment == environment);

            if (config == null)
            {
                var target = string.IsNullOrWhiteSpace(environment) ? "ativo" : environment;
                throw new InvalidOperationException($"O OpenProject não está configurado para o ambiente {target}.");
            }

            if (string.IsNullOrWhiteSpace(config.PrimaryToken))
            {
                throw new InvalidOperationException("O token do OpenProject não foi configurado para o ambiente selecionado.");
            }

            if (string.IsNullOrWhiteSpace(config.BaseUrl))
            {
                throw new InvalidOperationException("A URL base do OpenProject não foi configurada para o ambiente selecionado.");
            }

            var plainTextToken = UnprotectToken(config.PrimaryToken);
            return CreateClient(config.BaseUrl, plainTextToken);
        }

        private HttpClient CreateClient(string baseUrl, string plainTextToken)
        {
            var client = _httpClientFactory.CreateClient();
            var normalizedBaseUrl = baseUrl.EndsWith("/") ? baseUrl : $"{baseUrl}/";
            client.BaseAddress = new Uri(normalizedBaseUrl);

            var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"apikey:{plainTextToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));

            return client;
        }

        public async Task<string?> GetActiveOpenProjectBaseUrlAsync()
        {
            var baseUrl = await _context.Set<IntegrationSettings>()
                .Where(x => x.Provider == "OpenProject" && x.IsActive)
                .Select(x => x.BaseUrl)
                .FirstOrDefaultAsync();

            return NormalizeBaseUrl(baseUrl);
        }

        public static string? BuildWorkPackageWebUrl(string? baseUrl, WorkPackageCache? workPackage)
        {
            var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
            if (string.IsNullOrWhiteSpace(normalizedBaseUrl) || workPackage == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(workPackage.ProjectIdentifier))
            {
                return $"{normalizedBaseUrl}/projects/{Uri.EscapeDataString(workPackage.ProjectIdentifier)}/work_packages/{workPackage.Id}/activity";
            }

            return $"{normalizedBaseUrl}/work_packages/{workPackage.Id}/activity";
        }

        public static string? BuildTimeEntryApiUrl(string? baseUrl, int? openProjectTimeEntryId)
        {
            var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
            return string.IsNullOrWhiteSpace(normalizedBaseUrl) || !openProjectTimeEntryId.HasValue
                ? null
                : $"{normalizedBaseUrl}/api/v3/time_entries/{openProjectTimeEntryId.Value}";
        }

        public static string? BuildTimeEntryWebUrl(string? baseUrl, int? openProjectTimeEntryId, WorkPackageCache? workPackage)
        {
            var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
            if (string.IsNullOrWhiteSpace(normalizedBaseUrl) || !openProjectTimeEntryId.HasValue || workPackage == null)
            {
                return null;
            }

            var projectSegment = !string.IsNullOrWhiteSpace(workPackage.ProjectIdentifier)
                ? Uri.EscapeDataString(workPackage.ProjectIdentifier)
                : workPackage.ProjectId > 0
                    ? workPackage.ProjectId.ToString()
                    : null;

            if (string.IsNullOrWhiteSpace(projectSegment))
            {
                return BuildWorkPackageWebUrl(normalizedBaseUrl, workPackage);
            }

            var filters = JsonSerializer.Serialize(new[]
            {
                new Dictionary<string, object>
                {
                    ["workPackageId"] = new
                    {
                        @operator = "=",
                        values = new[] { workPackage.Id.ToString() }
                    }
                }
            });

            return $"{normalizedBaseUrl}/projects/{projectSegment}/cost_reports?filters={Uri.EscapeDataString(filters)}";
        }

        public async Task<OpenProjectConnectionTestResult> TestConnectionAsync(TestOpenProjectConnectionRequest request)
        {
            var baseUrl = request.BaseUrl;

            try
            {
                var token = request.PrimaryToken;

                if (string.IsNullOrWhiteSpace(baseUrl) ||
                    string.IsNullOrWhiteSpace(token) ||
                    token == "********")
                {
                    var storedConfig = await _context.Set<IntegrationSettings>()
                        .FirstOrDefaultAsync(x =>
                            x.Provider == "OpenProject" &&
                            x.Environment == request.Environment);

                    baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? storedConfig?.BaseUrl : baseUrl;

                    if (string.IsNullOrWhiteSpace(token) || token == "********")
                    {
                        token = string.IsNullOrWhiteSpace(storedConfig?.PrimaryToken)
                            ? null
                            : UnprotectToken(storedConfig.PrimaryToken);
                    }
                }

                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    return new OpenProjectConnectionTestResult
                    {
                        Success = false,
                        Environment = request.Environment,
                        Message = "Informe a URL base do OpenProject."
                    };
                }

                if (string.IsNullOrWhiteSpace(token))
                {
                    return new OpenProjectConnectionTestResult
                    {
                        Success = false,
                        Environment = request.Environment,
                        BaseUrl = baseUrl,
                        Message = "Informe o token da API do OpenProject."
                    };
                }

                var client = CreateClient(baseUrl, token);
                var userResponse = await client.GetAsync("api/v3/users/me");

                if (!userResponse.IsSuccessStatusCode)
                {
                    return new OpenProjectConnectionTestResult
                    {
                        Success = false,
                        Environment = request.Environment,
                        BaseUrl = baseUrl,
                        Message = $"OpenProject retornou {(int)userResponse.StatusCode}: {await ReadOpenProjectErrorMessageAsync(userResponse)}"
                    };
                }

                var userName = await ReadUserNameAsync(userResponse);
                var activities = await GetTimeEntryActivitiesAsync(client);
                var warnings = new List<string>();

                if (activities.Count == 0)
                {
                    warnings.Add("Conexão validada, mas nenhuma atividade de apontamento foi retornada pelo OpenProject.");
                }

                return new OpenProjectConnectionTestResult
                {
                    Success = true,
                    Environment = request.Environment,
                    BaseUrl = baseUrl,
                    UserName = userName,
                    ActivitiesCount = activities.Count,
                    Warnings = warnings,
                    Message = "Conexão com OpenProject validada com sucesso."
                };
            }
            catch (Exception ex)
            {
                return new OpenProjectConnectionTestResult
                {
                    Success = false,
                    Environment = request.Environment,
                    BaseUrl = baseUrl,
                    Message = ex.Message
                };
            }
        }

        private string UnprotectToken(string protectedToken)
        {
            try
            {
                return _protector.Unprotect(protectedToken);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    "O token salvo do OpenProject não pôde ser descriptografado nesta instalação. Reinsira o token da API e salve novamente as configurações do OpenProject.",
                    ex);
            }
        }

        public async Task<List<OpenProjectTimeEntryActivityDto>> GetTimeEntryActivitiesAsync()
        {
            var client = await CreateConfiguredClientAsync();
            return await GetTimeEntryActivitiesAsync(client);
        }

        public async Task<List<OpenProjectTimeEntryActivityDto>> GetTimeEntryActivitiesForWorkPackageAsync(int workPackageId, DateTime? spentOn = null)
        {
            var workPackage = await GetWorkPackageAsync(workPackageId);
            var client = await CreateConfiguredClientAsync();

            var links = new Dictionary<string, object>
            {
                ["entity"] = new
                {
                    href = $"/api/v3/work_packages/{workPackageId}"
                },
                ["workPackage"] = new
                {
                    href = $"/api/v3/work_packages/{workPackageId}"
                },
                ["user"] = new
                {
                    href = "/api/v3/users/me"
                }
            };

            if (workPackage?.ProjectId > 0)
            {
                links["project"] = new
                {
                    href = $"/api/v3/projects/{workPackage.ProjectId}"
                };
            }

            var payload = new Dictionary<string, object>
            {
                ["_type"] = "TimeEntry",
                ["spentOn"] = (spentOn ?? DateTime.Today).ToString("yyyy-MM-dd"),
                ["hours"] = "PT1H",
                ["_links"] = links
            };

            var response = await client.PostAsJsonAsync("api/v3/time_entries/form", payload);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"OpenProject retornou {(int)response.StatusCode} ao carregar atividades da Work Package: {await ReadOpenProjectErrorMessageAsync(response)}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return new List<OpenProjectTimeEntryActivityDto>();
            }

            using var document = JsonDocument.Parse(responseBody);
            var activities = ReadActivitiesFromTimeEntryForm(document.RootElement);
            return activities.Count > 0
                ? activities
                : await GetTimeEntryActivitiesAsync(client);
        }

        private static async Task<List<OpenProjectTimeEntryActivityDto>> GetTimeEntryActivitiesAsync(HttpClient client)
        {
            var response = await client.GetAsync("api/v3/time_entries/activities");

            if (!response.IsSuccessStatusCode)
            {
                return new List<OpenProjectTimeEntryActivityDto>();
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return new List<OpenProjectTimeEntryActivityDto>();
            }

            using var document = JsonDocument.Parse(responseBody);
            var activities = new List<OpenProjectTimeEntryActivityDto>();

            if (document.RootElement.TryGetProperty("_embedded", out var embedded) &&
                embedded.TryGetProperty("elements", out var elements) &&
                elements.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in elements.EnumerateArray())
                {
                    var activity = ReadActivity(element);
                    if (activity != null)
                    {
                        activities.Add(activity);
                    }
                }
            }
            else if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    var activity = ReadActivity(element);
                    if (activity != null)
                    {
                        activities.Add(activity);
                    }
                }
            }

            return activities
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name)
                .ToList();
        }

        private static List<OpenProjectTimeEntryActivityDto> ReadActivitiesFromTimeEntryForm(JsonElement root)
        {
            if (!root.TryGetProperty("_embedded", out var embedded) ||
                !embedded.TryGetProperty("schema", out var schema) ||
                !TryGetActivitySchema(schema, out var activitySchema))
            {
                return ReadActivitiesFromAllowedValueArrays(root);
            }

            var activities = new List<OpenProjectTimeEntryActivityDto>();

            if (activitySchema.TryGetProperty("_embedded", out var activityEmbedded) &&
                activityEmbedded.TryGetProperty("allowedValues", out var embeddedAllowedValues) &&
                embeddedAllowedValues.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in embeddedAllowedValues.EnumerateArray())
                {
                    var activity = ReadActivity(element);
                    if (activity != null)
                    {
                        activities.Add(activity);
                    }
                }
            }

            if (activities.Count == 0 &&
                activitySchema.TryGetProperty("_links", out var activityLinks) &&
                activityLinks.TryGetProperty("allowedValues", out var linkedAllowedValues) &&
                linkedAllowedValues.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in linkedAllowedValues.EnumerateArray())
                {
                    var activity = ReadActivityFromLink(element);
                    if (activity != null)
                    {
                        activities.Add(activity);
                    }
                }
            }

            if (activities.Count == 0)
            {
                activities = ReadActivitiesFromAllowedValueArrays(root);
            }

            return activities
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name)
                .ToList();
        }

        private static bool TryGetActivitySchema(JsonElement schema, out JsonElement activitySchema)
        {
            foreach (var propertyName in new[] { "activity", "activityId", "timeEntryActivity" })
            {
                if (schema.TryGetProperty(propertyName, out activitySchema))
                {
                    return true;
                }
            }

            activitySchema = default;
            return false;
        }

        private static List<OpenProjectTimeEntryActivityDto> ReadActivitiesFromAllowedValueArrays(JsonElement root)
        {
            var activities = new List<OpenProjectTimeEntryActivityDto>();
            CollectActivitiesFromAllowedValueArrays(root, activities);

            return activities
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name)
                .ToList();
        }

        private static void CollectActivitiesFromAllowedValueArrays(JsonElement element, List<OpenProjectTimeEntryActivityDto> activities)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("allowedValues") && property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var allowedValue in property.Value.EnumerateArray())
                        {
                            var activity = ReadActivityAllowedValue(allowedValue);
                            if (activity != null)
                            {
                                activities.Add(activity);
                            }
                        }
                    }
                    else
                    {
                        CollectActivitiesFromAllowedValueArrays(property.Value, activities);
                    }
                }

                return;
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    CollectActivitiesFromAllowedValueArrays(item, activities);
                }
            }
        }

        private static OpenProjectTimeEntryActivityDto? ReadActivityAllowedValue(JsonElement element)
        {
            if (element.TryGetProperty("_type", out var typeElement) &&
                string.Equals(typeElement.GetString(), "TimeEntriesActivity", StringComparison.OrdinalIgnoreCase))
            {
                return ReadActivity(element);
            }

            if (element.TryGetProperty("_links", out var links) &&
                links.TryGetProperty("self", out var selfLink) &&
                selfLink.TryGetProperty("href", out var selfHrefElement) &&
                IsTimeEntryActivityHref(selfHrefElement.GetString()))
            {
                return ReadActivity(element);
            }

            if (element.TryGetProperty("href", out var hrefElement) &&
                IsTimeEntryActivityHref(hrefElement.GetString()))
            {
                return ReadActivityFromLink(element);
            }

            return null;
        }

        private static bool IsTimeEntryActivityHref(string? href)
        {
            return href?.Contains("/time_entries/activity", StringComparison.OrdinalIgnoreCase) == true ||
                   href?.Contains("/time_entries/activities", StringComparison.OrdinalIgnoreCase) == true;
        }

        public async Task<WorkPackageCache?> GetWorkPackageAsync(int wpId)
        {
            var cachedWp = await _context.Set<WorkPackageCache>().FindAsync(wpId);

            if (cachedWp != null &&
                cachedWp.ProjectId > 0 &&
                !string.IsNullOrWhiteSpace(cachedWp.ProjectIdentifier) &&
                cachedWp.LastUpdated > DateTime.UtcNow.AddHours(-24))
            {
                return cachedWp;
            }

            var client = await CreateConfiguredClientAsync();
            var response = await client.GetAsync($"api/v3/work_packages/{wpId}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var wpData = await response.Content.ReadFromJsonAsync<OpenProjectWpResponse>();
            if (wpData == null)
            {
                return null;
            }

            if (cachedWp == null)
            {
                cachedWp = new WorkPackageCache { Id = wpId };
                _context.Set<WorkPackageCache>().Add(cachedWp);
            }

            cachedWp.Subject = wpData.Subject;
            cachedWp.ProjectId = TryReadIdFromHref(wpData._links?.Project?.Href, out var projectId)
                ? projectId
                : cachedWp.ProjectId;
            cachedWp.ProjectName = wpData._links?.Project?.Title ?? "Projeto Desconhecido";

            if (cachedWp.ProjectId > 0)
            {
                var project = await GetProjectAsync(client, cachedWp.ProjectId);
                if (project != null)
                {
                    cachedWp.ProjectIdentifier = project.Identifier ?? cachedWp.ProjectIdentifier;
                    cachedWp.ProjectName = project.Name ?? cachedWp.ProjectName;
                }
            }

            cachedWp.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return cachedWp;
        }

        private static async Task<OpenProjectProjectResponse?> GetProjectAsync(HttpClient client, int projectId)
        {
            var response = await client.GetAsync($"api/v3/projects/{projectId}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<OpenProjectProjectResponse>();
        }

        public async Task<List<OpenProjectWorkPackageSearchResult>> SearchWorkPackagesAsync(string query, int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<OpenProjectWorkPackageSearchResult>();
            }

            var client = await CreateConfiguredClientAsync();
            var response = await client.GetAsync(BuildWorkPackageSearchUrl("subjectOrId", "**", query, pageSize));
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"OpenProject retornou {(int)response.StatusCode} ao buscar Work Packages: {await ReadOpenProjectErrorMessageAsync(response)}");
            }

            var results = await ReadWorkPackageSearchResultsAsync(response);
            if (results.Count == 0)
            {
                var subjectResponse = await client.GetAsync(BuildWorkPackageSearchUrl("subject", "~", query, pageSize));
                if (subjectResponse.IsSuccessStatusCode)
                {
                    results = await ReadWorkPackageSearchResultsAsync(subjectResponse);
                }
            }

            await UpsertWorkPackageCacheAsync(results);
            return results;
        }

        public async Task<List<OpenProjectWorkPackageMonitoringItemDto>> GetWorkPackagesForStatusMonitoringAsync(
            CancellationToken cancellationToken = default)
        {
            const int pageSize = 100;
            var client = await CreateConfiguredClientAsync();
            var workPackages = new List<OpenProjectWorkPackageMonitoringItemDto>();

            for (var offset = 1; ; offset += pageSize)
            {
                using var response = await client.GetAsync(
                    $"api/v3/work_packages?pageSize={pageSize}&offset={offset}",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"OpenProject retornou {(int)response.StatusCode} ao monitorar Work Packages: {await ReadOpenProjectErrorMessageAsync(response)}");
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    break;
                }

                using var document = JsonDocument.Parse(responseBody);
                if (!document.RootElement.TryGetProperty("_embedded", out var embedded) ||
                    !embedded.TryGetProperty("elements", out var elements) ||
                    elements.ValueKind != JsonValueKind.Array)
                {
                    break;
                }

                var pageItems = elements
                    .EnumerateArray()
                    .Select(ReadWorkPackageMonitoringItem)
                    .Where(x => x != null)
                    .Cast<OpenProjectWorkPackageMonitoringItemDto>()
                    .ToList();

                workPackages.AddRange(pageItems);

                if (pageItems.Count < pageSize)
                {
                    break;
                }
            }

            await UpsertWorkPackageCacheAsync(workPackages.Select(x => new OpenProjectWorkPackageSearchResult
            {
                Id = x.Id,
                Subject = x.Subject,
                ProjectId = x.ProjectId,
                ProjectName = x.ProjectName ?? "Projeto Desconhecido"
            }).ToList());

            return workPackages;
        }

        public async Task<Dictionary<int, string>> GetOpenProjectUserEmailsAsync(
            IEnumerable<int> userIds,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateConfiguredClientAsync();
            var emails = new Dictionary<int, string>();

            foreach (var userId in userIds.Distinct().Where(id => id > 0))
            {
                using var response = await client.GetAsync($"api/v3/users/{userId}", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(responseBody);
                if (document.RootElement.TryGetProperty("email", out var emailElement) &&
                    !string.IsNullOrWhiteSpace(emailElement.GetString()))
                {
                    emails[userId] = emailElement.GetString()!;
                }
            }

            return emails;
        }

        private static string BuildWorkPackageSearchUrl(string filterName, string filterOperator, string query, int pageSize)
        {
            var filters = JsonSerializer.Serialize(new[]
            {
                new Dictionary<string, object>
                {
                    [filterName] = new
                    {
                        @operator = filterOperator,
                        values = new[] { query.Trim() }
                    }
                }
            });

            var sortBy = JsonSerializer.Serialize(new[] { new[] { "id", "desc" } });
            return $"api/v3/work_packages?filters={Uri.EscapeDataString(filters)}&pageSize={pageSize}&sortBy={Uri.EscapeDataString(sortBy)}";
        }

        private static async Task<List<OpenProjectWorkPackageSearchResult>> ReadWorkPackageSearchResultsAsync(HttpResponseMessage response)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return new List<OpenProjectWorkPackageSearchResult>();
            }

            using var document = JsonDocument.Parse(responseBody);
            var results = new List<OpenProjectWorkPackageSearchResult>();
            if (!document.RootElement.TryGetProperty("_embedded", out var embedded) ||
                !embedded.TryGetProperty("elements", out var elements) ||
                elements.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (var element in elements.EnumerateArray())
            {
                var result = ReadWorkPackageSearchResult(element);
                if (result != null)
                {
                    results.Add(result);
                }
            }

            return results;
        }

        public async Task<SyncTimeEntryResult> SyncTimeEntryAsync(TimeEntry entry)
        {
            try
            {
                var workPackage = await GetWorkPackageAsync(entry.WorkPackageId);
                var client = await CreateConfiguredClientAsync();
                var payload = BuildTimeEntryPayload(entry, workPackage);

                HttpResponseMessage response;

                if (entry.OpenProjectTimeEntryId.HasValue)
                {
                    var request = new HttpRequestMessage(HttpMethod.Patch, $"api/v3/time_entries/{entry.OpenProjectTimeEntryId.Value}")
                    {
                        Content = JsonContent.Create(payload)
                    };

                    response = await client.SendAsync(request);

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        response = await client.PostAsJsonAsync("api/v3/time_entries", payload);
                    }
                }
                else
                {
                    response = await client.PostAsJsonAsync("api/v3/time_entries", payload);
                }

                if (!response.IsSuccessStatusCode)
                {
                    return new SyncTimeEntryResult
                    {
                        Success = false,
                        ErrorMessage = $"OpenProject retornou {(int)response.StatusCode}: {await ReadOpenProjectErrorMessageAsync(response)}"
                    };
                }

                return new SyncTimeEntryResult
                {
                    Success = true,
                    OpenProjectTimeEntryId = await ReadRemoteIdAsync(response)
                };
            }
            catch (Exception ex)
            {
                return new SyncTimeEntryResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static Dictionary<string, object> BuildTimeEntryPayload(TimeEntry entry, WorkPackageCache? workPackage)
        {
            var links = new Dictionary<string, object>
            {
                ["entity"] = new
                {
                    href = $"/api/v3/work_packages/{entry.WorkPackageId}"
                },
                ["workPackage"] = new
                {
                    href = $"/api/v3/work_packages/{entry.WorkPackageId}"
                },
                ["activity"] = new
                {
                    href = $"/api/v3/time_entries/activities/{entry.ActivityId}"
                }
            };

            if (workPackage?.ProjectId > 0)
            {
                links["project"] = new
                {
                    href = $"/api/v3/projects/{workPackage.ProjectId}"
                };
            }

            return new Dictionary<string, object>
            {
                ["_type"] = "TimeEntry",
                ["comment"] = new
                {
                    format = "plain",
                    raw = entry.Comment ?? string.Empty
                },
                ["spentOn"] = entry.SpentOn.ToString("yyyy-MM-dd"),
                ["hours"] = FormatDuration(entry.Hours),
                ["_links"] = links
            };
        }

        private static string FormatDuration(decimal hours)
        {
            var totalMinutes = (int)Math.Round(hours * 60m, MidpointRounding.AwayFromZero);
            if (totalMinutes <= 0)
            {
                return "PT0M";
            }

            var totalHours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            var builder = new StringBuilder("PT");

            if (totalHours > 0)
            {
                builder.Append(totalHours).Append('H');
            }

            if (minutes > 0)
            {
                builder.Append(minutes).Append('M');
            }

            return builder.ToString();
        }

        private static OpenProjectTimeEntryActivityDto? ReadActivity(JsonElement element)
        {
            var id = 0;
            if (element.TryGetProperty("id", out var idElement))
            {
                idElement.TryGetInt32(out id);
            }

            var name = element.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : null;

            if (element.TryGetProperty("_links", out var links) &&
                links.TryGetProperty("self", out var selfLink))
            {
                if (string.IsNullOrWhiteSpace(name) &&
                    selfLink.TryGetProperty("title", out var titleElement))
                {
                    name = titleElement.GetString();
                }

                if (id <= 0 &&
                    selfLink.TryGetProperty("href", out var hrefElement) &&
                    TryReadIdFromHref(hrefElement.GetString(), out var idFromHref))
                {
                    id = idFromHref;
                }
            }

            if (id <= 0 || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var isDefault = element.TryGetProperty("default", out var defaultElement) &&
                            defaultElement.ValueKind == JsonValueKind.True;

            return new OpenProjectTimeEntryActivityDto
            {
                Id = id,
                Name = name,
                IsDefault = isDefault
            };
        }

        private static OpenProjectTimeEntryActivityDto? ReadActivityFromLink(JsonElement element)
        {
            var href = element.TryGetProperty("href", out var hrefElement)
                ? hrefElement.GetString()
                : null;

            var name = element.TryGetProperty("title", out var titleElement)
                ? titleElement.GetString()
                : null;

            if (!TryReadIdFromHref(href, out var id) || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return new OpenProjectTimeEntryActivityDto
            {
                Id = id,
                Name = name
            };
        }

        private static OpenProjectWorkPackageSearchResult? ReadWorkPackageSearchResult(JsonElement element)
        {
            if (!element.TryGetProperty("id", out var idElement) ||
                !idElement.TryGetInt32(out var id))
            {
                return null;
            }

            var subject = element.TryGetProperty("subject", out var subjectElement)
                ? subjectElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(subject))
            {
                return null;
            }

            var projectId = 0;
            var projectName = "Projeto Desconhecido";
            if (element.TryGetProperty("_links", out var links) &&
                links.TryGetProperty("project", out var projectLink))
            {
                if (projectLink.TryGetProperty("href", out var projectHrefElement))
                {
                    TryReadIdFromHref(projectHrefElement.GetString(), out projectId);
                }

                if (projectLink.TryGetProperty("title", out var projectTitleElement))
                {
                    projectName = projectTitleElement.GetString() ?? projectName;
                }
            }

            return new OpenProjectWorkPackageSearchResult
            {
                Id = id,
                Subject = subject,
                ProjectId = projectId,
                ProjectName = projectName
            };
        }

        private static OpenProjectWorkPackageMonitoringItemDto? ReadWorkPackageMonitoringItem(JsonElement element)
        {
            if (!element.TryGetProperty("id", out var idElement) || !idElement.TryGetInt32(out var id))
            {
                return null;
            }

            var subject = element.TryGetProperty("subject", out var subjectElement)
                ? subjectElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(subject))
            {
                return null;
            }

            var statusName = string.Empty;
            var projectId = 0;
            string? projectName = null;
            var responsibleUserIds = new List<int>();

            if (element.TryGetProperty("_links", out var links))
            {
                if (links.TryGetProperty("status", out var statusLink) &&
                    statusLink.TryGetProperty("title", out var statusTitle))
                {
                    statusName = statusTitle.GetString() ?? string.Empty;
                }

                if (links.TryGetProperty("project", out var projectLink))
                {
                    if (projectLink.TryGetProperty("href", out var projectHref))
                    {
                        TryReadIdFromHref(projectHref.GetString(), out projectId);
                    }

                    if (projectLink.TryGetProperty("title", out var projectTitle))
                    {
                        projectName = projectTitle.GetString();
                    }
                }

                foreach (var responsibleLinkName in new[] { "assignee", "responsible" })
                {
                    if (links.TryGetProperty(responsibleLinkName, out var responsibleLink) &&
                        responsibleLink.TryGetProperty("href", out var responsibleHref) &&
                        TryReadIdFromHref(responsibleHref.GetString(), out var responsibleUserId))
                    {
                        responsibleUserIds.Add(responsibleUserId);
                    }
                }
            }

            return new OpenProjectWorkPackageMonitoringItemDto
            {
                Id = id,
                Subject = subject,
                StatusName = statusName,
                ResponsibleUserIds = responsibleUserIds.Distinct().ToList(),
                ProjectId = projectId,
                ProjectName = projectName
            };
        }

        private async Task UpsertWorkPackageCacheAsync(List<OpenProjectWorkPackageSearchResult> workPackages)
        {
            if (workPackages.Count == 0)
            {
                return;
            }

            var ids = workPackages.Select(x => x.Id).ToArray();
            var existingItems = await _context.Set<WorkPackageCache>()
                .Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            foreach (var workPackage in workPackages)
            {
                if (!existingItems.TryGetValue(workPackage.Id, out var cacheItem))
                {
                    cacheItem = new WorkPackageCache { Id = workPackage.Id };
                    _context.Set<WorkPackageCache>().Add(cacheItem);
                }

                cacheItem.Subject = workPackage.Subject;
                cacheItem.ProjectId = workPackage.ProjectId;
                cacheItem.ProjectName = workPackage.ProjectName;
                cacheItem.LastUpdated = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        private static string? NormalizeBaseUrl(string? baseUrl)
        {
            return string.IsNullOrWhiteSpace(baseUrl)
                ? null
                : baseUrl.Trim().TrimEnd('/');
        }

        private static bool TryReadIdFromHref(string? href, out int id)
        {
            id = 0;

            if (string.IsNullOrWhiteSpace(href))
            {
                return false;
            }

            var lastSlashIndex = href.LastIndexOf('/');
            return lastSlashIndex >= 0 &&
                   lastSlashIndex < href.Length - 1 &&
                   int.TryParse(href[(lastSlashIndex + 1)..], out id);
        }

        private static async Task<string?> ReadUserNameAsync(HttpResponseMessage response)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return null;
            }

            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("name", out var nameElement))
            {
                return nameElement.GetString();
            }

            if (document.RootElement.TryGetProperty("_links", out var links) &&
                links.TryGetProperty("self", out var selfLink) &&
                selfLink.TryGetProperty("title", out var titleElement))
            {
                return titleElement.GetString();
            }

            return null;
        }

        private static async Task<int?> ReadRemoteIdAsync(HttpResponseMessage response)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return null;
            }

            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("id", out var idElement) &&
                idElement.TryGetInt32(out var parsedId))
            {
                return parsedId;
            }

            return null;
        }

        private static async Task<string> ReadOpenProjectErrorMessageAsync(HttpResponseMessage response)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return response.ReasonPhrase ?? "Resposta sem detalhes.";
            }

            try
            {
                using var document = JsonDocument.Parse(responseBody);
                if (document.RootElement.TryGetProperty("message", out var messageElement))
                {
                    var message = messageElement.GetString() ?? responseBody;
                    var details = ReadOpenProjectErrorDetails(document.RootElement);

                    return string.IsNullOrWhiteSpace(details)
                        ? message
                        : $"{message} Detalhes: {details}";
                }
            }
            catch (JsonException)
            {
                // O OpenProject normalmente responde HAL/JSON, mas proxies podem devolver HTML/texto.
            }

            const int maxLength = 500;
            return responseBody.Length <= maxLength
                ? responseBody
                : $"{responseBody[..maxLength]}...";
        }

        private static string? ReadOpenProjectErrorDetails(JsonElement root)
        {
            if (!root.TryGetProperty("_embedded", out var embedded))
            {
                return null;
            }

            var details = new List<string>();

            if (embedded.TryGetProperty("details", out var detailElement))
            {
                AddOpenProjectErrorDetail(details, detailElement, null);
            }

            if (embedded.TryGetProperty("validationErrors", out var validationErrors) &&
                validationErrors.ValueKind == JsonValueKind.Object)
            {
                foreach (var validationError in validationErrors.EnumerateObject())
                {
                    AddOpenProjectErrorDetail(details, validationError.Value, validationError.Name);
                }
            }

            return details.Count == 0
                ? null
                : string.Join("; ", details.Distinct());
        }

        private static void AddOpenProjectErrorDetail(List<string> details, JsonElement element, string? fallbackAttribute)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    AddOpenProjectErrorDetail(details, item, fallbackAttribute);
                }

                return;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var attribute = element.TryGetProperty("attribute", out var attributeElement)
                ? attributeElement.GetString()
                : fallbackAttribute;

            var message = element.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(attribute) && !string.IsNullOrWhiteSpace(message))
            {
                details.Add($"{attribute}: {message}");
                return;
            }

            if (!string.IsNullOrWhiteSpace(attribute))
            {
                details.Add(attribute);
                return;
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                details.Add(message);
            }
        }
    }
}
