using Axiom.Atlas.Application.DTOs.Integrations;
using Axiom.Atlas.Domain.Entities.Integrations;
using Axiom.Atlas.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Axiom.Atlas.Infrastructure.Services.TimeEntries;
using Axiom.Atlas.Infrastructure.Services.ServiceDesk;
using System.Text.Json;

namespace Axiom.Atlas.API.Controllers.Integrations
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class IntegrationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IDataProtector _protector;
        private readonly OpenProjectService _openProjectService;
        private readonly GlpiService _glpiService;

        // Injetamos o contexto do banco e o provedor de proteção de dados
        public IntegrationsController(
            AppDbContext context,
            IDataProtectionProvider provider,
            OpenProjectService openProjectService,
            GlpiService glpiService)
        {
            _context = context;
            // O nome "AxiomAtlas.Integrations" é o propósito. Serve como um "sal" extra.
            _protector = provider.CreateProtector("AxiomAtlas.Integrations");
            _openProjectService = openProjectService;
            _glpiService = glpiService;
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
            var setting = await _context.Integrations.AsNoTracking().FirstOrDefaultAsync(x => x.Provider == "GLPI" && x.IsActive);
            var additional = string.IsNullOrWhiteSpace(setting?.AdditionalSettings)
                ? new Dictionary<string, string?>()
                : JsonSerializer.Deserialize<Dictionary<string, string?>>(setting.AdditionalSettings!) ?? new();
            return Ok(new SaveGlpiSettingsRequest
            {
                BaseUrl = setting?.BaseUrl,
                AppToken = string.IsNullOrWhiteSpace(setting?.SecondaryToken) ? null : "********",
                UserToken = string.IsNullOrWhiteSpace(setting?.PrimaryToken) ? null : "********",
                ClassificationFieldKey = additional.GetValueOrDefault("classificationFieldKey"),
                DevOpsUrlFieldKey = additional.GetValueOrDefault("devOpsUrlFieldKey")
            });
        }

        [HttpPost("glpi")]
        public async Task<IActionResult> SaveGlpi([FromBody] SaveGlpiSettingsRequest request)
        {
            var setting = await _context.Integrations.FirstOrDefaultAsync(x => x.Provider == "GLPI" && x.IsActive);
            if (setting == null)
            {
                setting = new IntegrationSettings { Provider = "GLPI", Environment = "Production", IsActive = true };
                _context.Integrations.Add(setting);
            }

            setting.BaseUrl = request.BaseUrl?.Trim().TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(request.UserToken) && request.UserToken != "********") setting.PrimaryToken = _protector.Protect(request.UserToken);
            if (!string.IsNullOrWhiteSpace(request.AppToken) && request.AppToken != "********") setting.SecondaryToken = _protector.Protect(request.AppToken);
            setting.AdditionalSettings = JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["classificationFieldKey"] = request.ClassificationFieldKey?.Trim(),
                ["devOpsUrlFieldKey"] = request.DevOpsUrlFieldKey?.Trim()
            });
            await _context.SaveChangesAsync();
            return Ok(new { message = "Configurações do GLPI salvas com sucesso." });
        }

        [HttpPost("glpi/test")]
        public async Task<IActionResult> TestGlpi([FromBody] SaveGlpiSettingsRequest request)
        {
            var result = await _glpiService.TestConnectionAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
