using Axiom.Atlas.Application.DTOs.TimeEntries;
using Axiom.Atlas.Domain.Entities.TimeEntries;
using Axiom.Atlas.Domain.Enums;
using Axiom.Atlas.Infrastructure.Services.TimeEntries;
using Axiom.Atlas.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Axiom.Atlas.API.Controllers.TimeEntries
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("AxiomAtlasPolicy")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class TimeEntriesController : ControllerBase
    {
        private readonly OpenProjectService _openProjectService;
        private readonly AppDbContext _context;

        public TimeEntriesController(OpenProjectService openProjectService, AppDbContext context)
        {
            _openProjectService = openProjectService;
            _context = context;
        }

        [HttpGet("work-package/{wpId}")]
        public async Task<IActionResult> GetWorkPackageInfo([FromRoute] int wpId)
        {
            var wp = await _openProjectService.GetWorkPackageAsync(wpId);

            if (wp == null)
            {
                return NotFound(new { Message = $"Work Package #{wpId} não encontrada no OpenProject ou acesso negado." });
            }

            return Ok(wp);
        }

        [HttpGet("activities")]
        public async Task<IActionResult> GetActivities([FromQuery] int? workPackageId)
        {
            var activities = workPackageId.HasValue
                ? await _openProjectService.GetTimeEntryActivitiesForWorkPackageAsync(workPackageId.Value)
                : await _openProjectService.GetTimeEntryActivitiesAsync();

            return Ok(activities);
        }

        [HttpPost]
        public async Task<IActionResult> LogTime([FromBody] CreateTimeEntryRequest request, [FromServices] AppDbContext context)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { Message = "Usuário não identificado no Token." });
            }

            // 1. Tenta converter as strings para TimeSpan de forma segura
            if (!TimeSpan.TryParse(request.StartTime, out var startTime) ||
                !TimeSpan.TryParse(request.EndTime, out var endTime))
            {
                return BadRequest(new { Message = "Formato de hora inválido para Início ou Término." });
            }

            // 2. Validação da Regra de Negócio: Fim não pode ser menor ou igual ao Início
            if (endTime <= startTime)
            {
                return BadRequest(new { Message = "A hora de término deve ser posterior à hora de início." });
            }

            // 3. A MÁGICA MATEMÁTICA: A API ignora o request.Hours e calcula a verdade
            var horasCalculadas = Math.Round((endTime - startTime).TotalHours, 2);

            var timeEntry = new TimeEntry
            {
                UserId = userId,
                WorkPackageId = request.WorkPackageId,
                SpentOn = DateTime.SpecifyKind(request.SpentOn, DateTimeKind.Utc),
                StartTime = startTime,
                EndTime = endTime,
                Hours = decimal.Parse(horasCalculadas.ToString()),
                Comment = request.Comment,
                ActivityId = request.ActivityId,
                SyncStatus = SyncStatus.Pending
            };

            context.Set<TimeEntry>().Add(timeEntry);
            await context.SaveChangesAsync();

            return Ok(new { success = true, Message = "Sucesso!", TimeEntryId = timeEntry.Id });
        }

        [HttpGet]
        public async Task<IActionResult> GetMyEntries([FromServices] AppDbContext context)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;

            var entries = await context.Set<TimeEntry>()
                .Include(x => x.WorkPackage)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.SpentOn)
                .ToListAsync();

            var openProjectBaseUrl = await _openProjectService.GetActiveOpenProjectBaseUrlAsync();
            var workPackageLookup = new Dictionary<int, WorkPackageCache?>();

            foreach (var workPackageId in entries.Select(x => x.WorkPackageId).Distinct())
            {
                var cachedWorkPackage = entries.FirstOrDefault(x => x.WorkPackageId == workPackageId)?.WorkPackage;
                if (cachedWorkPackage != null &&
                    cachedWorkPackage.ProjectId > 0 &&
                    !string.IsNullOrWhiteSpace(cachedWorkPackage.ProjectIdentifier))
                {
                    workPackageLookup[workPackageId] = cachedWorkPackage;
                    continue;
                }

                try
                {
                    workPackageLookup[workPackageId] = await _openProjectService.GetWorkPackageAsync(workPackageId) ?? cachedWorkPackage;
                }
                catch
                {
                    workPackageLookup[workPackageId] = cachedWorkPackage;
                }
            }

            var result = entries.Select(entry =>
            {
                var workPackage = workPackageLookup.GetValueOrDefault(entry.WorkPackageId) ?? entry.WorkPackage;
                var isSynced = entry.SyncStatus == SyncStatus.Synced;
                var lockReason = isSynced
                    ? "Este apontamento já foi sincronizado com o OpenProject. Para preservar o vínculo remoto, edite ou exclua pelo OpenProject."
                    : null;

                return new TimeEntryListItemDto
                {
                    Id = entry.Id,
                    WorkPackageId = entry.WorkPackageId,
                    WorkPackageSubject = workPackage?.Subject,
                    WorkPackageProjectName = workPackage?.ProjectName,
                    WorkPackageUrl = OpenProjectService.BuildWorkPackageWebUrl(openProjectBaseUrl, workPackage),
                    SpentOn = entry.SpentOn,
                    StartTime = entry.StartTime,
                    EndTime = entry.EndTime,
                    Hours = entry.Hours,
                    Comment = entry.Comment,
                    ActivityId = entry.ActivityId,
                    SyncStatus = entry.SyncStatus.ToString(),
                    SyncErrorMessage = entry.SyncErrorMessage,
                    OpenProjectTimeEntryId = entry.OpenProjectTimeEntryId,
                    OpenProjectTimeEntryUrl = OpenProjectService.BuildTimeEntryWebUrl(openProjectBaseUrl, entry.OpenProjectTimeEntryId, workPackage),
                    CanEdit = !isSynced,
                    CanDelete = !isSynced,
                    LockReason = lockReason
                };
            });

            return Ok(result);
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { Message = "Usuário não identificado no Token." });
            }

            var entries = await _context.Set<TimeEntry>()
                .Where(x => x.UserId == userId)
                .Select(x => new
                {
                    x.SyncStatus,
                    x.Hours,
                    x.SpentOn
                })
                .ToListAsync();

            var summary = new TimeEntrySummaryDto
            {
                TotalEntries = entries.Count,
                PendingEntries = entries.Count(x => x.SyncStatus == SyncStatus.Pending),
                SyncedEntries = entries.Count(x => x.SyncStatus == SyncStatus.Synced),
                ErrorEntries = entries.Count(x => x.SyncStatus == SyncStatus.Error),
                TotalHours = entries.Sum(x => x.Hours),
                PendingHours = entries.Where(x => x.SyncStatus == SyncStatus.Pending).Sum(x => x.Hours),
                SyncedHours = entries.Where(x => x.SyncStatus == SyncStatus.Synced).Sum(x => x.Hours),
                ErrorHours = entries.Where(x => x.SyncStatus == SyncStatus.Error).Sum(x => x.Hours),
                LastEntryDate = entries
                    .OrderByDescending(x => x.SpentOn)
                    .Select(x => (DateTime?)x.SpentOn)
                    .FirstOrDefault()
            };

            return Ok(summary);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateTimeEntryRequest request, [FromServices] AppDbContext context)
        {
            // Adicionado o fallback "sub" aqui também para manter o padrão do POST
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { Message = "Usuário não identificado no Token." });
            }

            var entry = await context.Set<TimeEntry>()
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

            if (entry == null) return NotFound();

            if (entry.SyncStatus == SyncStatus.Synced)
            {
                return BadRequest(new
                {
                    Message = "Este apontamento já foi sincronizado com o OpenProject. Para preservar o vínculo remoto, edite pelo OpenProject."
                });
            }

            // 1. Tenta converter as strings para TimeSpan de forma segura
            if (!TimeSpan.TryParse(request.StartTime, out var startTime) ||
                !TimeSpan.TryParse(request.EndTime, out var endTime))
            {
                return BadRequest(new { Message = "Formato de hora inválido para Início ou Término." });
            }

            // 2. Validação da Regra de Negócio: Fim não pode ser menor ou igual ao Início
            if (endTime <= startTime)
            {
                return BadRequest(new { Message = "A hora de término deve ser posterior à hora de início." });
            }

            // 3. A MÁGICA MATEMÁTICA: Calcula as novas horas
            var horasCalculadas = Math.Round((endTime - startTime).TotalHours, 2);

            // 4. Atualiza TODOS os campos necessários (incluindo os horários que faltavam)
            entry.WorkPackageId = request.WorkPackageId;
            entry.Comment = request.Comment;
            entry.ActivityId = request.ActivityId;
            entry.SpentOn = DateTime.SpecifyKind(request.SpentOn, DateTimeKind.Utc);

            // As linhas cruciais que estavam faltando no seu código original:
            entry.StartTime = startTime;
            entry.EndTime = endTime;
            entry.Hours = decimal.Parse(horasCalculadas.ToString());

            entry.SyncStatus = SyncStatus.Pending;

            await context.SaveChangesAsync();

            // Retornando um objeto Ok para o Front-end saber que deu tudo certo
            return Ok(new { success = true, Message = "Apontamento atualizado com sucesso!" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, [FromServices] AppDbContext context)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;

            var entry = await context.Set<TimeEntry>()
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

            if (entry == null) return NotFound();

            if (entry.SyncStatus == SyncStatus.Synced)
            {
                return BadRequest(new
                {
                    Message = "Este apontamento já foi sincronizado com o OpenProject. Para preservar o vínculo remoto, exclua pelo OpenProject."
                });
            }

            context.Set<TimeEntry>().Remove(entry);
            await context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("sync")]
        public async Task<IActionResult> SyncEntries([FromBody] Guid[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return BadRequest(new { success = false, message = "Nenhum apontamento foi selecionado para sincronização." });
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, message = "Usuário não identificado no Token." });
            }

            var selectedIds = ids.Distinct().ToArray();
            var entries = await _context.Set<TimeEntry>()
                .Where(x => selectedIds.Contains(x.Id) && x.UserId == userId)
                .ToListAsync();

            if (entries.Count == 0)
            {
                return NotFound(new { success = false, message = "Nenhum dos apontamentos selecionados foi encontrado." });
            }

            var errors = new List<string>();
            var results = new List<object>();

            foreach (var entry in entries)
            {
                var syncResult = await _openProjectService.SyncTimeEntryAsync(entry);

                if (syncResult.Success)
                {
                    entry.SyncStatus = SyncStatus.Synced;
                    entry.SyncErrorMessage = null;

                    if (syncResult.OpenProjectTimeEntryId.HasValue)
                    {
                        entry.OpenProjectTimeEntryId = syncResult.OpenProjectTimeEntryId.Value;
                    }

                    results.Add(new
                    {
                        id = entry.Id,
                        success = true,
                        openProjectTimeEntryId = entry.OpenProjectTimeEntryId
                    });
                }
                else
                {
                    entry.SyncStatus = SyncStatus.Error;
                    entry.SyncErrorMessage = syncResult.ErrorMessage;
                    errors.Add($"#{entry.Id}: {syncResult.ErrorMessage}");
                    results.Add(new
                    {
                        id = entry.Id,
                        success = false,
                        error = syncResult.ErrorMessage
                    });
                }
            }

            await _context.SaveChangesAsync();

            if (errors.Count > 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Alguns apontamentos não puderam ser sincronizados.",
                    errors,
                    results
                });
            }

            return Ok(new
            {
                success = true,
                message = $"{entries.Count} apontamento(s) sincronizado(s) com sucesso.",
                results
            });
        }

        [HttpGet("SearchWorkPackages")]
        public async Task<IActionResult> SearchWorkPackages([FromQuery] string query, [FromServices] AppDbContext context)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Ok(new List<object>());

            // Verifica se o que o usuário digitou é um número (para buscar pelo ID exato)
            bool isNumeric = int.TryParse(query, out int wpId);

            if (isNumeric)
            {
                var cached = await context.Set<WorkPackageCache>()
                    .Where(wp => wp.Id == wpId)
                    .Select(wp => new
                    {
                        id = wp.Id,
                        subject = wp.Subject
                    })
                    .FirstOrDefaultAsync();

                if (cached != null)
                {
                    return Ok(new[] { cached });
                }

                var fetched = await _openProjectService.GetWorkPackageAsync(wpId);
                if (fetched != null)
                {
                    return Ok(new[]
                    {
                        new
                        {
                            id = fetched.Id,
                            subject = fetched.Subject
                        }
                    });
                }

                return Ok(new List<object>());
            }

            var liveResults = await _openProjectService.SearchWorkPackagesAsync(query);
            if (liveResults.Count > 0)
            {
                return Ok(liveResults.Select(wp => new
                {
                    id = wp.Id,
                    subject = wp.Subject
                }));
            }

            // Fallback: mantém resultados locais caso o item já tenha sido cacheado por ID ou busca anterior.
            var workPackages = await context.Set<WorkPackageCache>()
                .Where(wp =>
                    wp.Subject != null && wp.Subject.Contains(query)
                )
                .OrderByDescending(wp => wp.Id)
                .Take(20)
                .Select(wp => new
                {
                    id = wp.Id,
                    subject = wp.Subject
                })
                .ToListAsync();

            return Ok(workPackages);
        }
    }
}
