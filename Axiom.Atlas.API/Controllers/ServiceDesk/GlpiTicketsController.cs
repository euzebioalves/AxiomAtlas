using System.Security.Claims;
using Axiom.Atlas.Application.DTOs.ServiceDesk;
using Axiom.Atlas.Infrastructure.Services.ServiceDesk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axiom.Atlas.API.Controllers.ServiceDesk
{
    [ApiController]
    [Route("api/glpi/tickets")]
    [Authorize]
    public class GlpiTicketsController : ControllerBase
    {
        private readonly GlpiService _glpiService;
        private readonly GlpiImprovementTicketSynchronizationQueue _synchronizationQueue;
        private readonly IConfiguration _configuration;
        public GlpiTicketsController(
            GlpiService glpiService,
            GlpiImprovementTicketSynchronizationQueue synchronizationQueue,
            IConfiguration configuration)
        {
            _glpiService = glpiService;
            _synchronizationQueue = synchronizationQueue;
            _configuration = configuration;
        }

        [HttpPost("import")]
        public async Task<IActionResult> Import([FromBody] ImportGlpiTicketRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query)) return BadRequest(new { message = "Informe o número ou assunto do chamado." });
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "Sistema";
            try { return Ok(await _glpiService.ImportTicketAsync(request.Query, userId)); }
            catch (Exception exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpGet("improvements")]
        public async Task<IActionResult> GetImprovementTickets(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? status = null,
            [FromQuery] bool refresh = false)
        {
            try
            {
                if (refresh)
                {
                    // GLPI plus OpenProject reconciliation can take longer than a browser request.
                    // Queue it and return the local projection immediately (stale while revalidate).
                    await _synchronizationQueue.RequestSynchronizationAsync(
                        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name);
                }

                var tickets = await _glpiService.GetImprovementTicketsAsync(page, pageSize, status);
                tickets.SynchronizationPending = await _synchronizationQueue.IsSynchronizationPendingAsync();
                tickets.SynchronizationIntervalSeconds = Math.Clamp(
                    _configuration.GetValue<int?>("GlpiSynchronization:IntervalSeconds") ?? 300,
                    60,
                    3600);
                return Ok(tickets);
            }
            catch (Exception exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpGet("kanban")]
        public async Task<IActionResult> GetUnifiedBacklog()
        {
            try
            {
                var backlog = await _glpiService.GetUnifiedBacklogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name);
                backlog.SynchronizationPending = await _synchronizationQueue.IsSynchronizationPendingAsync();
                backlog.SynchronizationIntervalSeconds = Math.Clamp(
                    _configuration.GetValue<int?>("GlpiSynchronization:IntervalSeconds") ?? 300,
                    60,
                    3600);
                return Ok(backlog);
            }
            catch (Exception exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        [HttpPost("improvements/synchronize")]
        public async Task<IActionResult> SynchronizeImprovementTickets()
        {
            await _synchronizationQueue.RequestSynchronizationAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name);
            return Accepted(new { message = "Atualização da fila solicitada. O quadro será reconciliado em segundo plano." });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id) => (await _glpiService.GetWorkspaceAsync(id)) is { } workspace ? Ok(workspace) : NotFound();

        [HttpPut("{id:guid}/draft")]
        public async Task<IActionResult> SaveDraft(Guid id, [FromBody] SaveRequirementDraftRequest request)
        {
            try { return Ok(await _glpiService.SaveDraftAsync(id, request.RequirementMarkdown)); }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpGet("openproject-projects")]
        public async Task<IActionResult> GetOpenProjectProjects()
        {
            try { return Ok(await _glpiService.GetOpenProjectProjectsAsync()); }
            catch (Exception exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpPost("{id:guid}/user-story")]
        public async Task<IActionResult> CreateUserStory(Guid id, [FromBody] CreateOpenProjectUserStoryRequest request)
        {
            try { return Ok(await _glpiService.CreateUserStoryAsync(id, request)); }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (Exception exception) { return BadRequest(new { message = exception.Message }); }
        }

        [HttpPost("{id:guid}/glpi-link/reprocess")]
        public async Task<IActionResult> ReprocessGlpiLink(Guid id)
        {
            var workspace = await _glpiService.GetWorkspaceAsync(id);
            if (workspace == null) return NotFound();
            if (!workspace.OpenProjectWorkPackageId.HasValue)
            {
                return BadRequest(new { message = "Crie ou vincule uma User Story antes de sincronizar o GLPI." });
            }

            var job = await _synchronizationQueue.RequestGlpiLinkUpdateAsync(
                id,
                workspace.GlpiTicketId,
                workspace.OpenProjectWorkPackageId,
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name);
            return Accepted(new
            {
                job.Id,
                status = job.Status.ToString(),
                message = "O vínculo com o GLPI foi colocado na fila de sincronização."
            });
        }

        [HttpGet("{id:guid}/attachments/{documentId:int}")]
        public async Task<IActionResult> DownloadAttachment(Guid id, int documentId)
        {
            try { var file = await _glpiService.DownloadAttachmentAsync(id, documentId); return File(file.Content, file.ContentType); }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (Exception exception) { return BadRequest(new { message = exception.Message }); }
        }
    }
}
