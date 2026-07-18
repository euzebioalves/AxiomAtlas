using Axiom.Atlas.Application.DTOs.Integrations;
using Axiom.Atlas.Domain.Entities.Integrations;
using Axiom.Atlas.Domain.Entities.ServiceDesk;
using Axiom.Atlas.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Axiom.Atlas.Infrastructure.Services.TimeEntries;
using Axiom.Atlas.Infrastructure.Services.ServiceDesk;
using System.Security.Claims;
using System.Text.Json;

namespace Axiom.Atlas.API.Controllers.Integrations
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "AdministrationOnly")]
    public class IntegrationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IDataProtector _protector;
        private readonly OpenProjectService _openProjectService;
        private readonly GlpiService _glpiService;
        private readonly GlpiImprovementTicketSynchronizationQueue _synchronizationQueue;

        // Injetamos o contexto do banco e o provedor de proteção de dados
        public IntegrationsController(
            AppDbContext context,
            IDataProtectionProvider provider,
            OpenProjectService openProjectService,
            GlpiService glpiService,
            GlpiImprovementTicketSynchronizationQueue synchronizationQueue)
        {
            _context = context;
            // O nome "AxiomAtlas.Integrations" é o propósito. Serve como um "sal" extra.
            _protector = provider.CreateProtector("AxiomAtlas.Integrations");
            _openProjectService = openProjectService;
            _glpiService = glpiService;
            _synchronizationQueue = synchronizationQueue;
        }

        [HttpPost("openproject")]
        public async Task<IActionResult> SaveOpenProject([FromBody] SaveOpenProjectSettingsRequest request)
        {
            // Busca os registros existentes (se houver)
            var existingSettings = await _context.Set<IntegrationSettings>()
                .Where(x => x.Provider == "OpenProject")
                .ToListAsync();

            // 1. Processa Homologação
            await UpsertEnvironmentSetting(existingSettings, "OpenProject", "Homologation",
                request.Homologation, request.ActiveEnvironment == "Homologation");

            // 2. Processa Produção
            await UpsertEnvironmentSetting(existingSettings, "OpenProject", "Production",
                request.Production, request.ActiveEnvironment == "Production");

            await _context.SaveChangesAsync();

            return Ok(new { message = "Configurações salvas com sucesso." });
        }

        // Método auxiliar para criar ou atualizar o ambiente
        private async Task UpsertEnvironmentSetting(
            List<IntegrationSettings> existingSettings,
            string provider,
            string environment,
            EnvironmentSettingDto dto,
            bool isActive)
        {
            var setting = existingSettings.FirstOrDefault(x => x.Environment == environment);

            if (setting == null)
            {
                setting = new IntegrationSettings
                {
                    Provider = provider,
                    Environment = environment
                };
                _context.Set<IntegrationSettings>().Add(setting);
            }

            setting.IsActive = isActive;
            setting.BaseUrl = dto.BaseUrl;

            // Só atualiza (e criptografa) o token se o usuário enviou um novo.
            // Isso evita apagar o token se o form for enviado vazio por engano.
            if (!string.IsNullOrWhiteSpace(dto.PrimaryToken) && dto.PrimaryToken != "********")
            {
                // A MÁGICA DA SEGURANÇA: Criptografa antes de ir para o banco
                setting.PrimaryToken = _protector.Protect(dto.PrimaryToken);
            }
        }

        [HttpGet("openproject")]
        public async Task<IActionResult> GetOpenProjectSettings()
        {
            var settings = await _context.Set<IntegrationSettings>()
                .Where(x => x.Provider == "OpenProject")
                .ToListAsync();

            if (!settings.Any())
            {
                // Retorna uma casca vazia se for a primeira vez que o admin acessa
                return Ok(new SaveOpenProjectSettingsRequest
                {
                    ActiveEnvironment = "Homologation",
                    Production = new EnvironmentSettingDto(),
                    Homologation = new EnvironmentSettingDto()
                });
            }

            var prodSetting = settings.FirstOrDefault(x => x.Environment == "Production");
            var homolSetting = settings.FirstOrDefault(x => x.Environment == "Homologation");

            // Descobre quem é o ativo (se nenhum for, por segurança cai para homologação)
            var activeSetting = settings.FirstOrDefault(x => x.IsActive) ?? homolSetting;

            var response = new SaveOpenProjectSettingsRequest
            {
                ActiveEnvironment = activeSetting?.Environment ?? "Homologation",

                Production = new EnvironmentSettingDto
                {
                    BaseUrl = prodSetting?.BaseUrl,
                    // Se existir token no banco, devolve a máscara. Se não, devolve nulo.
                    PrimaryToken = string.IsNullOrEmpty(prodSetting?.PrimaryToken) ? null : "********"
                },
                Homologation = new EnvironmentSettingDto
                {
                    BaseUrl = homolSetting?.BaseUrl,
                    PrimaryToken = string.IsNullOrEmpty(homolSetting?.PrimaryToken) ? null : "********"
                }
            };

            return Ok(response);
        }

        [HttpPost("openproject/test")]
        public async Task<IActionResult> TestOpenProject([FromBody] TestOpenProjectConnectionRequest request)
        {
            var result = await _openProjectService.TestConnectionAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("glpi")]
        public async Task<IActionResult> GetGlpiSettings()
        {
            var settings = await _context.Integrations.AsNoTracking()
                .Where(x => x.Provider == "GLPI")
                .ToListAsync();
            var production = settings.FirstOrDefault(x => x.Environment == "Production");
            var homologation = settings.FirstOrDefault(x => x.Environment == "Homologation");
            var active = settings.FirstOrDefault(x => x.IsActive) ?? production ?? homologation;

            return Ok(new SaveGlpiSettingsRequest
            {
                ActiveEnvironment = active?.Environment ?? "Homologation",
                Production = ToGlpiEnvironmentDto(production),
                Homologation = ToGlpiEnvironmentDto(homologation)
            });
        }

        [HttpPost("glpi")]
        public async Task<IActionResult> SaveGlpi([FromBody] SaveGlpiSettingsRequest request)
        {
            var existing = await _context.Integrations.Where(x => x.Provider == "GLPI").ToListAsync();
            var activeEnvironment = request.ActiveEnvironment == "Production" ? "Production" : "Homologation";
            await UpsertGlpiEnvironmentSetting(existing, "Production", request.Production, activeEnvironment == "Production");
            await UpsertGlpiEnvironmentSetting(existing, "Homologation", request.Homologation, activeEnvironment == "Homologation");
            await _context.SaveChangesAsync();
            return Ok(new { message = "Configurações do GLPI salvas com sucesso." });
        }

        [HttpPost("glpi/test")]
        public async Task<IActionResult> TestGlpi([FromBody] TestGlpiConnectionRequest request)
        {
            var result = await _glpiService.TestConnectionAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("synchronizations")]
        public async Task<IActionResult> GetSynchronizations([FromQuery] string? status = null, [FromQuery] int take = 50)
        {
            var safeTake = Math.Clamp(take, 10, 200);
            var baseQuery = _context.IntegrationSynchronizationJobs.AsNoTracking();
            var counts = await baseQuery
                .GroupBy(x => x.Status)
                .Select(x => new { Status = x.Key, Count = x.Count() })
                .ToListAsync();

            var jobsQuery = baseQuery;
            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<IntegrationSynchronizationJobStatus>(status, true, out var parsedStatus))
            {
                jobsQuery = jobsQuery.Where(x => x.Status == parsedStatus);
            }

            var persistedJobs = await jobsQuery
                .OrderByDescending(x => x.CreatedAt)
                .Take(safeTake)
                .ToListAsync();

            var jobs = persistedJobs.Select(x => new IntegrationSynchronizationJobDetailsDto
                {
                    Id = x.Id,
                    Type = x.Type.ToString(),
                    Status = x.Status.ToString(),
                    CorrelationKey = x.CorrelationKey,
                    GlpiTicketId = x.GlpiTicketId,
                    OpenProjectWorkPackageId = x.OpenProjectWorkPackageId,
                    AttemptCount = x.AttemptCount,
                    MaxAttempts = x.MaxAttempts,
                    AvailableAt = x.AvailableAt,
                    CreatedAt = x.CreatedAt,
                    StartedAt = x.StartedAt,
                    CompletedAt = x.CompletedAt,
                    LastError = x.LastError
                })
                .ToList();

            return Ok(new IntegrationSynchronizationOverviewDto
            {
                PendingCount = counts.FirstOrDefault(x => x.Status == IntegrationSynchronizationJobStatus.Pending)?.Count ?? 0,
                ProcessingCount = counts.FirstOrDefault(x => x.Status == IntegrationSynchronizationJobStatus.Processing)?.Count ?? 0,
                FailedCount = counts.FirstOrDefault(x => x.Status == IntegrationSynchronizationJobStatus.Failed)?.Count ?? 0,
                SucceededCount = counts.FirstOrDefault(x => x.Status == IntegrationSynchronizationJobStatus.Succeeded)?.Count ?? 0,
                LastCompletedAt = await baseQuery
                    .Where(x => x.Status == IntegrationSynchronizationJobStatus.Succeeded)
                    .MaxAsync(x => x.CompletedAt),
                Jobs = jobs
            });
        }

        [HttpPost("synchronizations/{id:guid}/retry")]
        public async Task<IActionResult> RetrySynchronization(Guid id)
        {
            var requestedBy = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name;
            var job = await _synchronizationQueue.RetryAsync(id, requestedBy);
            if (job == null)
            {
                return NotFound(new { message = "Operação de integração não encontrada." });
            }

            return Accepted(new
            {
                id = job.Id,
                status = job.Status.ToString(),
                message = "Nova tentativa adicionada à fila de integração."
            });
        }

        private Task UpsertGlpiEnvironmentSetting(
            List<IntegrationSettings> existing,
            string environment,
            GlpiEnvironmentSettingDto dto,
            bool isActive)
        {
            var setting = existing.FirstOrDefault(x => x.Environment == environment);
            if (setting == null)
            {
                setting = new IntegrationSettings { Provider = "GLPI", Environment = environment };
                _context.Integrations.Add(setting);
            }

            setting.IsActive = isActive;
            setting.BaseUrl = dto.BaseUrl?.Trim().TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(dto.UserToken) && dto.UserToken != "********") setting.PrimaryToken = _protector.Protect(dto.UserToken);
            if (!string.IsNullOrWhiteSpace(dto.AppToken) && dto.AppToken != "********") setting.SecondaryToken = _protector.Protect(dto.AppToken);
            setting.AdditionalSettings = JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["classificationFieldKey"] = dto.ClassificationFieldKey?.Trim(),
                ["devOpsUrlFieldKey"] = dto.DevOpsUrlFieldKey?.Trim()
            });
            return Task.CompletedTask;
        }

        private static GlpiEnvironmentSettingDto ToGlpiEnvironmentDto(IntegrationSettings? setting)
        {
            var additional = string.IsNullOrWhiteSpace(setting?.AdditionalSettings)
                ? new Dictionary<string, string?>()
                : JsonSerializer.Deserialize<Dictionary<string, string?>>(setting.AdditionalSettings!) ?? new();
            return new GlpiEnvironmentSettingDto
            {
                BaseUrl = setting?.BaseUrl,
                AppToken = string.IsNullOrWhiteSpace(setting?.SecondaryToken) ? null : "********",
                UserToken = string.IsNullOrWhiteSpace(setting?.PrimaryToken) ? null : "********",
                ClassificationFieldKey = additional.GetValueOrDefault("classificationFieldKey"),
                DevOpsUrlFieldKey = additional.GetValueOrDefault("devOpsUrlFieldKey")
            };
        }
    }
}
