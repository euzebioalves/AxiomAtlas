using Axiom.Atlas.Application.DTOs.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Axiom.Atlas.API.Controllers.Roles
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "AdministrationOnly")]
    public class RolesController : ControllerBase
    {
        private const string PermissionClaimType = "Permission";

        // Catálogo único das permissões configuráveis pela matriz de perfis.
        private static readonly string[] PermissionCatalog =
        {
            "Permissions.Users.View", "Permissions.Users.Create", "Permissions.Users.Edit", "Permissions.Users.Delete",
            "Permissions.Roles.View", "Permissions.Roles.Create", "Permissions.Roles.Edit", "Permissions.Roles.Delete",
            "Permissions.TimeEntries.View", "Permissions.TimeEntries.Create", "Permissions.TimeEntries.Edit", "Permissions.TimeEntries.Delete",
            "Permissions.TimeClock.View", "Permissions.TimeClock.Create", "Permissions.TimeClock.Edit", "Permissions.TimeClock.Delete",
            "Permissions.TimeClockAudit.View",
            "Permissions.TimeClockJourney.View", "Permissions.TimeClockJourney.Edit",
            "Permissions.TimeClockGlobalSettings.View", "Permissions.TimeClockGlobalSettings.Edit",
            "Permissions.ServiceDesk.View", "Permissions.ServiceDesk.Create", "Permissions.ServiceDesk.Edit",
            "Permissions.ServiceDeskKanban.View",
            "Permissions.ServiceDeskDashboard.View",
            "Permissions.IntegrationsOpenProject.View", "Permissions.IntegrationsOpenProject.Edit",
            "Permissions.IntegrationsGlpi.View", "Permissions.IntegrationsGlpi.Edit",
            "Permissions.IntegrationOperations.View", "Permissions.IntegrationOperations.Create",
            "Permissions.AuditLog.View",
            "Permissions.DesktopNotifications.View", "Permissions.DesktopNotifications.Edit",
            "Permissions.Releases.View"
        };

        private static readonly HashSet<string> PermissionCatalogSet =
            new(PermissionCatalog, StringComparer.Ordinal);

        private readonly RoleManager<IdentityRole<Guid>> _roleManager;

        // Injetando o Gerenciador de Papéis do Identity
        public RolesController(RoleManager<IdentityRole<Guid>> roleManager)
        {
            _roleManager = roleManager;
        }

        // 1. LISTAR TODOS
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var roles = await _roleManager.Roles
                .Select(r => new RoleResponseDto
                {
                    Id = r.Id,
                    Name = r.Name ?? string.Empty
                })
                .ToListAsync();

            return Ok(roles);
        }

        // 2. OBTER POR ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound("Papel não encontrado.");

            return Ok(new RoleResponseDto
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty
            });
        }

        // 3. CRIAR NOVO PAPEL
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RoleCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "O nome do papel é obrigatório." });

            // Verifica se o papel já existe para não duplicar
            var roleExists = await _roleManager.RoleExistsAsync(dto.Name);
            if (roleExists)
                return BadRequest(new { message = "Este papel já existe." });

            var role = new IdentityRole<Guid>
            {
                Name = dto.Name,
                NormalizedName = dto.Name.ToUpper()
            };
            var result = await _roleManager.CreateAsync(role);

            if (result.Succeeded)
                return CreatedAtAction(nameof(GetById), new { id = role.Id }, new { role.Id, role.Name });

            return BadRequest(result.Errors);
        }

        // 4. ATUALIZAR PAPEL
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] RoleUpdateDto dto)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound("Papel não encontrado.");

            role.Name = dto.Name;

            var result = await _roleManager.UpdateAsync(role);

            if (result.Succeeded)
                return Ok(new { message = "Papel atualizado com sucesso." });

            return BadRequest(result.Errors);
        }

        // 5. EXCLUIR PAPEL
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound();

            // Proteção extra: Evitar excluir papéis cruciais do sistema (opcional)
            if (role.Name != null && (role.Name.ToLower() == "admin" || role.Name.ToLower() == "administrador"))
                return BadRequest(new { message = "Não é permitido excluir o papel de Administrador principal." });

            var result = await _roleManager.DeleteAsync(role);

            if (result.Succeeded)
                return Ok(new { message = "Papel excluído com sucesso." });

            return BadRequest(result.Errors);
        }


        // 6. OBTER PERMISSÕES DO PAPEL (Para montar a Matriz)
        [HttpGet("{id}/permissions")]
        public async Task<IActionResult> GetPermissions(Guid id)
        {
            var role = await _roleManager.FindByIdAsync(id.ToString());
            if (role == null) return NotFound("Papel não encontrado.");

            // Busca as permissões que o papel já tem no banco
            var existingClaims = await _roleManager.GetClaimsAsync(role);
            var existingClaimValues = existingClaims
                .Where(c => c.Type == PermissionClaimType)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);

            // Cruza as permissões do sistema com as que o papel já tem
            var rolePermissions = PermissionCatalog.Select(p => new RolePermissionDto
            {
                Value = p,
                Selected = existingClaimValues.Contains(p)
            }).ToList();

            return Ok(new
            {
                RoleId = role.Id,
                RoleName = role.Name,
                Permissions = rolePermissions
            });
        }

        // 7. ATUALIZAR PERMISSÕES DO PAPEL (Salvar a Matriz)
        [HttpPut("{id}/permissions")]
        public async Task<IActionResult> UpdatePermissions(Guid id, [FromBody] UpdateRolePermissionsDto dto)
        {
            var role = await _roleManager.FindByIdAsync(id.ToString());
            if (role == null) return NotFound("Papel não encontrado.");

            // Remove somente as permissões administradas por esta matriz.
            // Claims adicionais permanecem intactas para evitar perda de permissões futuras.
            var claims = await _roleManager.GetClaimsAsync(role);
            foreach (var claim in claims.Where(c =>
                         c.Type == PermissionClaimType && PermissionCatalogSet.Contains(c.Value)))
            {
                var removeResult = await _roleManager.RemoveClaimAsync(role, claim);
                if (!removeResult.Succeeded)
                {
                    return BadRequest(removeResult.Errors);
                }
            }

            var selectedPermissions = (dto.Permissions ?? new List<RolePermissionDto>())
                .Where(p => p.Selected && PermissionCatalogSet.Contains(p.Value))
                .Select(p => p.Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // Adiciona as permissões selecionadas no banco.
            foreach (var permission in selectedPermissions)
            {
                var addResult = await _roleManager.AddClaimAsync(role, new Claim(PermissionClaimType, permission));
                if (!addResult.Succeeded)
                {
                    return BadRequest(addResult.Errors);
                }
            }

            return Ok(new { message = "Permissões atualizadas com sucesso." });
        }
    }
}
